using GrupoJuridico.Gestion.Application.Catalogos;
using GrupoJuridico.Gestion.Application.Cobros;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Common.Models;
using GrupoJuridico.Gestion.Application.Personas;
using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Tests;

/// <summary>
/// Sin proceso periódico: los estados que cambian con la fecha se ponen al día al leer.
/// La consulta "qué planes necesitan recálculo" debe coincidir exactamente con las reglas de <see cref="ReglasCobro"/>.
/// </summary>
public sealed class ActualizadorCobrosTests : IDisposable
{
    // ---------- La consulta coincide con las reglas, día por día ----------

    /// <summary>
    /// Simula muchos planes durante meses con pagos, promesas y cambios manuales del estado del cliente.
    /// Cada día compara la consulta con lo que haría el recálculo completo: si la consulta dice que no hace falta,
    /// el recálculo no debe cambiar nada, y si dice que sí, debe cambiar algo.
    /// </summary>
    [Fact]
    public void La_consulta_detecta_exactamente_los_planes_que_cambian_con_la_fecha()
    {
        var azar = new Random(20261020);
        var estados = new[] { EstadoCliente.AlDia, EstadoCliente.Moroso, EstadoCliente.Estrella, EstadoCliente.Terminado, EstadoCliente.Abandonado };
        var consultas = new Dictionary<DateOnly, Func<PlanPago, bool>>();
        var revisados = 0;
        var conCambios = 0;
        for (var n = 0; n < 300; n++)
        {
            var inicio = new DateOnly(2026, 1, 1).AddDays(azar.Next(0, 60));
            var cuotas = azar.Next(1, 7);
            var plan = new PlanPago { MontoTotal = 1000m * cuotas, CantidadCuotas = cuotas, MontoCuota = 1000m, FechaInicio = inicio, DiaPago = azar.Next(1, 32) };
            plan.Cuotas.AddRange(ReglasCobro.GenerarCuotas(plan));
            plan.Cliente = new Cliente { EstadoCalculadoId = estados[azar.Next(estados.Length)] };
            ReglasCobro.NormalizarPlan(plan, plan.Cliente, 2.5m, inicio);

            var fin = plan.Cuotas[^1].FechaVencimiento.AddDays(45);
            for (var hoy = inicio; hoy <= fin; hoy = hoy.AddDays(1))
            {
                var pendientes = plan.Cuotas.Where(c => c.EstadoCuotaPagoId != EstadoCuotaPago.Pagada).ToList();
                var evento = azar.Next(100);
                if (evento < 6 && pendientes.Count > 0)
                {
                    // Pago (completo o parcial) con recálculo, como al registrarlo.
                    var c = pendientes[azar.Next(pendientes.Count)];
                    var saldo = ReglasCobro.Saldo(c, 2.5m, hoy);
                    c.Pagos.Add(new Pago { MontoPago = azar.Next(2) == 0 ? saldo : Math.Max(1, Math.Round(saldo / 2)), FechaPago = hoy });
                    foreach (var pm in c.Promesas.Where(x => x.EstadoPromesaId == EstadoPromesa.Pendiente)) pm.MontoPagado = pm.MontoPrometido;
                    ReglasCobro.NormalizarPlan(plan, plan.Cliente, 2.5m, hoy);
                }
                else if (evento < 9 && pendientes.Count > 0)
                {
                    // Promesa nueva, con recálculo, como al crearla.
                    var c = pendientes[azar.Next(pendientes.Count)];
                    c.Promesas.Add(new PromesaPago { FechaPromesa = hoy.AddDays(azar.Next(0, 10)), MontoPrometido = 500m, EstadoPromesaId = EstadoPromesa.Pendiente });
                    ReglasCobro.NormalizarPlan(plan, plan.Cliente, 2.5m, hoy);
                }
                else if (evento < 11)
                {
                    // Estado manual elegido o quitado en la ficha: no cambia el calculado ni pide recálculo.
                    var manuales = new int?[] { null, EstadoCliente.Terminado, EstadoCliente.Abandonado, EstadoCliente.SinExpediente };
                    plan.Cliente.EstadoManualId = manuales[azar.Next(manuales.Length)];
                }
                else if (evento < 12)
                {
                    // Estado calculado inconsistente (por ejemplo, datos cargados a mano).
                    plan.Cliente.EstadoCalculadoId = estados[azar.Next(estados.Length)];
                }

                // Lectura: no todos los días alguien abre la aplicación.
                if (azar.Next(3) == 0) continue;
                if (!consultas.TryGetValue(hoy, out var consulta))
                    consultas[hoy] = consulta = ActualizadorCobros.NecesitaRecalculo(hoy).Compile();
                var necesita = consulta(plan);
                var (cambios, _) = ReglasCobro.NormalizarPlan(plan, plan.Cliente, 2.5m, hoy);
                Assert.True(necesita == cambios,
                    $"Plan {n}, {hoy:yyyy-MM-dd}: la consulta dice {(necesita ? "sí" : "no")} y el recálculo {(cambios ? "cambió" : "no cambió")} algo.");
                revisados++;
                if (cambios) conCambios++;
            }
        }
        // La simulación debe ejercitar ambos casos.
        Assert.True(revisados > 10000);
        Assert.True(conCambios > 500);
    }

    // ---------- Sin proceso periódico: las pantallas muestran el estado del día ----------

    private DateOnly _hoy = new(2026, 10, 15);
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");
    private readonly DbContextOptions<ApplicationDbContext> _opciones;

    public ActualizadorCobrosTests()
    {
        _conexion.Open();
        _opciones = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options;
        using var db = new ApplicationDbContext(_opciones);
        db.Database.EnsureCreated();
        db.EstadosCliente.AddRange(new[] { "Al Día", "Moroso", "Estrella", "Terminado", "Abandonado", "Sin Expediente" }
            .Select((n, i) => new EstadoCliente { Codigo = "EC-" + (i + 1), Nombre = n }));
        db.OrigenesCliente.Add(new OrigenCliente { Codigo = "P1", Nombre = "Origen" });
        db.EstadosPrima.AddRange(
            new EstadoPrima { Id = 1, Nombre = "Pendiente" }, new EstadoPrima { Id = 2, Nombre = "Incompleta" }, new EstadoPrima { Id = 3, Nombre = "Pagada" });
        db.EstadosCuotaPago.AddRange(new EstadoCuotaPago { Id = 1, Nombre = "Pendiente" }, new EstadoCuotaPago { Id = 2, Nombre = "Parcial" }, new EstadoCuotaPago { Id = 3, Nombre = "Pagada" });
        db.EstadosCuotaVencimiento.AddRange(new EstadoCuotaVencimiento { Id = 1, Nombre = "Al día" }, new EstadoCuotaVencimiento { Id = 2, Nombre = "Vencido" });
        db.EstadosPromesa.AddRange(new EstadoPromesa { Id = 1, Nombre = "Pendiente" }, new EstadoPromesa { Id = 2, Nombre = "Cumplida" }, new EstadoPromesa { Id = 3, Nombre = "Incumplida" });
        // Tipo de los comprobantes (lo crea la carga inicial): Administrador y Cobros suben y ven.
        var comprobantes = new TipoDocumento { Codigo = TipoDocumento.CodigoComprobantesPago, Nombre = "Comprobantes de pago" };
        foreach (var rol in new[] { Roles.Administrador, Roles.Cobros })
            comprobantes.Permisos.Add(new PermisoDocumento { Rol = rol, PuedeVer = true, PuedeSubir = true });
        db.TiposDocumento.Add(comprobantes);
        db.Configuracion.Add(new ConfiguracionSistema { Clave = ConfiguracionSistema.InteresMora, Valor = "2.5" });
        db.SaveChanges();
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext Db() => new(_opciones);
    private Fecha Reloj() => new(() => _hoy);
    private ActualizadorCobros Actualizador(ApplicationDbContext db) => new(db, Reloj());
    private CobrosService Cobros(ApplicationDbContext db) => new(db, new Usuario(Roles.Cobros), Reloj(), new SinArchivos(), Actualizador(db));
    private ListasService Listas(ApplicationDbContext db) => new(db, new Usuario(Roles.Administrador), Actualizador(db));
    private PersonasService Personas(ApplicationDbContext db) => new(db, new Usuario(Roles.Administrador), new SinUsuarios(), Reloj(), Actualizador(db), new SinArchivos());

    /// <summary>Cliente Al Día con un plan de 3 cuotas de 300 000; la primera vence mañana (16 de octubre).</summary>
    private async Task<int> ClienteConCuotaQueVenceMananaAsync(string nombre = "Ana")
    {
        int id;
        using (var db = Db())
        {
            var prima = new Prima { Monto = 100m, MontoCancelado = 0m };
            prima.Recalcular();
            var p = new Persona { Nombres = nombre, Apellidos = "Vargas", Expediente = "EXP-" + nombre, FechaIngreso = _hoy };
            p.Ventas.Add(new Venta { Monto = 1100m, Prima = prima });
            p.Cliente = new Cliente { OrigenClienteId = 1, EstadoCalculadoId = EstadoCliente.AlDia };
            foreach (var s in VersionSeccion.Secciones) p.Versiones.Add(new VersionSeccion { Seccion = s });
            db.Personas.Add(p);
            db.SaveChanges();
            id = p.Id;
        }
        using (var db = Db())
            await Cobros(db).CrearPlanAsync(id, new CrearPlanRequest(900000m, 3, _hoy, _hoy.AddDays(1).Day));
        return id;
    }

    private async Task<int> PlanesQueNecesitanRecalculoAsync()
    {
        using var db = Db();
        return await db.PlanesPago.CountAsync(ActualizadorCobros.NecesitaRecalculo(_hoy));
    }

    private async Task<int> EstadoGuardadoAsync(int personaId)
    {
        using var db = Db();
        return await db.Personas.Where(p => p.Id == personaId).Select(p => p.Cliente!.EstadoManualId ?? p.Cliente.EstadoCalculadoId).SingleAsync();
    }

    [Fact]
    public async Task Al_dia_siguiente_del_vencimiento_Cobros_lo_muestra_moroso()
    {
        var id = await ClienteConCuotaQueVenceMananaAsync();
        _hoy = _hoy.AddDays(2);
        Assert.Equal(1, await PlanesQueNecesitanRecalculoAsync());

        using var db = Db();
        var lista = await Cobros(db).ListarAsync(new CobrosQuery { Vista = VistasCobros.Morosos });
        Assert.Equal(1, lista.Totales.Morosos);
        var fila = Assert.Single(lista.Filas);
        Assert.Equal(EstadoCliente.Moroso, fila.EstadoClienteId);
        Assert.Equal(1, fila.CuotasVencidas);
        Assert.Equal(1, fila.Atraso);
        Assert.Equal(EstadoCliente.Moroso, await EstadoGuardadoAsync(id));
        // Ya quedó guardado: la siguiente lectura no recalcula nada.
        Assert.Equal(0, await PlanesQueNecesitanRecalculoAsync());
    }

    [Fact]
    public async Task Al_dia_siguiente_del_vencimiento_la_tabla_de_Clientes_lo_muestra_moroso()
    {
        var id = await ClienteConCuotaQueVenceMananaAsync();
        _hoy = _hoy.AddDays(2);

        using var db = Db();
        var lista = await Listas(db).ClientesAsync(new ListaQuery { EstadoClienteId = EstadoCliente.Moroso });
        var fila = Assert.Single(lista.Filas);
        Assert.Equal(id, fila.Id);
        Assert.Equal(EstadoCliente.Moroso, fila.EstadoClienteId);
    }

    [Fact]
    public async Task Al_dia_siguiente_del_vencimiento_la_ficha_lo_muestra_moroso_con_la_version_vigente()
    {
        var id = await ClienteConCuotaQueVenceMananaAsync();
        Guid versionAnterior;
        using (var db = Db())
            versionAnterior = (await Personas(db).ObtenerAsync(id)).Versiones["datos"];
        _hoy = _hoy.AddDays(2);

        using (var db = Db())
        {
            var ficha = await Personas(db).ObtenerAsync(id);
            Assert.Equal(EstadoCliente.Moroso, ficha.Cliente!.EstadoClienteId);
            // El cambio de estado cambió la versión antes de entregar la ficha: el autoguardado no tendrá un conflicto falso.
            Assert.NotEqual(versionAnterior, ficha.Versiones["datos"]);
            using var otra = Db();
            var guardada = await otra.Set<VersionSeccion>().Where(v => v.PersonaId == id && v.Seccion == "datos").Select(v => v.Version).SingleAsync();
            Assert.Equal(guardada, ficha.Versiones["datos"]);
        }
        // El bloque del plan dentro de la ficha ya no cambia nada (ni la versión).
        using (var db = Db())
        {
            await Cobros(db).ObtenerPlanAsync(id);
            Assert.Equal(EstadoCliente.Moroso, await EstadoGuardadoAsync(id));
            var ficha = await Personas(db).ObtenerAsync(id);
            using var otra = Db();
            Assert.Equal(await otra.Set<VersionSeccion>().Where(v => v.PersonaId == id && v.Seccion == "datos").Select(v => v.Version).SingleAsync(), ficha.Versiones["datos"]);
        }
    }

    [Fact]
    public async Task Una_promesa_vencida_queda_incumplida_al_abrir_el_plan()
    {
        var id = await ClienteConCuotaQueVenceMananaAsync();
        using (var db = Db())
        {
            var plan = await Cobros(db).ObtenerPlanAsync(id);
            await Cobros(db).CrearPromesaAsync(plan.Plan!.Cuotas[0].Id, new PromesaRequest(_hoy.AddDays(3), 300000m));
        }
        _hoy = _hoy.AddDays(5);
        using (var db = Db())
        {
            var plan = await Cobros(db).ObtenerPlanAsync(id);
            Assert.Equal(EstadoPromesa.Incumplida, plan.Plan!.Cuotas[0].Promesas.Single().EstadoId);
            Assert.Equal(EstadoCliente.Moroso, plan.EstadoClienteId);
        }
    }

    [Fact]
    public async Task Un_estado_cambiado_a_mano_que_no_corresponde_se_corrige_al_leer()
    {
        var id = await ClienteConCuotaQueVenceMananaAsync();
        _hoy = _hoy.AddDays(2);
        using (var db = Db()) await Cobros(db).ListarAsync(new CobrosQuery());
        // El calculado queda Al Día por error (por ejemplo, editado en la base) aunque tiene una cuota vencida.
        using (var db = Db())
        {
            var cliente = await db.Clientes.SingleAsync();
            cliente.EstadoCalculadoId = EstadoCliente.AlDia;
            await db.SaveChangesAsync();
        }
        Assert.Equal(1, await PlanesQueNecesitanRecalculoAsync());
        using (var db = Db())
        {
            var lista = await Listas(db).ClientesAsync(new ListaQuery());
            Assert.Equal(EstadoCliente.Moroso, Assert.Single(lista.Filas).EstadoClienteId);
        }
        // Un estado manual (Terminado) se muestra aunque tenga cuotas vencidas, sin pedir recálculo;
        // el calculado sigue en Moroso.
        using (var db = Db())
        {
            var cliente = await db.Clientes.SingleAsync();
            cliente.EstadoManualId = EstadoCliente.Terminado;
            await db.SaveChangesAsync();
        }
        Assert.Equal(0, await PlanesQueNecesitanRecalculoAsync());
        Assert.Equal(EstadoCliente.Terminado, await EstadoGuardadoAsync(id));
        using (var db = Db()) Assert.Equal(EstadoCliente.Moroso, (await db.Clientes.SingleAsync()).EstadoCalculadoId);
    }

    [Fact]
    public async Task Sin_vencimientos_nuevos_no_se_recalcula_ningun_plan()
    {
        await ClienteConCuotaQueVenceMananaAsync("Ana");
        await ClienteConCuotaQueVenceMananaAsync("Luis");
        Assert.Equal(0, await PlanesQueNecesitanRecalculoAsync());
        _hoy = _hoy.AddDays(1); // vence hoy: todavía no está vencida
        Assert.Equal(0, await PlanesQueNecesitanRecalculoAsync());
        _hoy = _hoy.AddDays(1);
        Assert.Equal(2, await PlanesQueNecesitanRecalculoAsync());
    }

    [Fact]
    public async Task Cambiar_el_interes_de_mora_recalcula_las_cuotas_vencidas()
    {
        var id = await ClienteConCuotaQueVenceMananaAsync();
        _hoy = _hoy.AddDays(2);
        using (var db = Db())
        {
            var plan = await Cobros(db).ObtenerPlanAsync(id);
            // Paga la cuota con el interés de 2.5 % (307 500): queda pagada y el cliente vuelve a Al Día.
            await Cobros(db).RegistrarPagoAsync(plan.Plan!.Cuotas[0].Id, new RegistrarPagoRequest(307500m, _hoy, true),
                new ArchivoSubido("r.pdf", "application/pdf", 4, new MemoryStream(new byte[] { 1, 2, 3, 4 })));
        }
        Assert.Equal(EstadoCliente.AlDia, await EstadoGuardadoAsync(id));

        using (var db = Db())
            await new CatalogosService(db, new SinUsuarios(), Actualizador(db)).GuardarInteresMoraAsync(new InteresMoraRequest(5m));

        using (var db = Db())
        {
            var cuota = await db.Cuotas.OrderBy(c => c.NumeroCuota).FirstAsync();
            Assert.Equal(EstadoCuotaPago.Parcial, cuota.EstadoCuotaPagoId);
            Assert.Equal(EstadoCuotaVencimiento.Vencido, cuota.EstadoCuotaVencimientoId);
        }
        Assert.Equal(EstadoCliente.Moroso, await EstadoGuardadoAsync(id));
    }

    // ---------- Estado manual en la ficha ----------

    private int? _vendedorId;

    /// <summary>Datos válidos de la ficha con el estado indicado (el vendedor es obligatorio).</summary>
    private ActualizarDatosRequest Datos(int? estado)
    {
        if (_vendedorId == null)
        {
            using var db = Db();
            var v = new Vendedor { Codigo = "30", Nombre = "Marta" };
            db.Vendedores.Add(v);
            db.SaveChanges();
            _vendedorId = v.Id;
        }
        return new("Ana", "Vargas", "123", "01-20013", "", _hoy, _vendedorId, null, null, null, estado, null);
    }

    [Fact]
    public async Task En_la_ficha_se_elige_un_estado_manual_y_se_vuelve_al_automatico()
    {
        var id = await ClienteConCuotaQueVenceMananaAsync();
        _hoy = _hoy.AddDays(2);

        using (var db = Db())
        {
            var ficha = await Personas(db).ObtenerAsync(id);
            Assert.Equal(EstadoCliente.Moroso, ficha.Cliente!.EstadoClienteId);
            Assert.Null(ficha.Cliente.EstadoManualId);
            await Personas(db).ActualizarDatosAsync(id, Datos(EstadoCliente.Abandonado), version: ficha.Versiones["datos"]);
        }
        using (var db = Db())
        {
            var ficha = await Personas(db).ObtenerAsync(id);
            Assert.Equal(EstadoCliente.Abandonado, ficha.Cliente!.EstadoClienteId);
            Assert.Equal(EstadoCliente.Abandonado, ficha.Cliente.EstadoManualId);
            Assert.Equal(EstadoCliente.Moroso, ficha.Cliente.EstadoCalculadoId);
            // Cobros lo sigue listando por sus cuotas vencidas, con el estado que se muestra.
            var lista = await Cobros(db).ListarAsync(new CobrosQuery());
            Assert.Equal(EstadoCliente.Abandonado, Assert.Single(lista.Filas).EstadoClienteId);
            // 0 = automático: vuelve al estado calculado.
            await Personas(db).ActualizarDatosAsync(id, Datos(0), version: ficha.Versiones["datos"]);
        }
        using (var db = Db())
        {
            var ficha = await Personas(db).ObtenerAsync(id);
            Assert.Equal(EstadoCliente.Moroso, ficha.Cliente!.EstadoClienteId);
            Assert.Null(ficha.Cliente.EstadoManualId);
            // Nulo = no cambia el estado.
            await Personas(db).ActualizarDatosAsync(id, Datos(null), version: ficha.Versiones["datos"]);
            Assert.Equal(EstadoCliente.Moroso, await EstadoGuardadoAsync(id));
        }
    }

    [Theory]
    [InlineData(EstadoCliente.AlDia)]
    [InlineData(EstadoCliente.Moroso)]
    [InlineData(EstadoCliente.Estrella)]
    public async Task Los_estados_automaticos_no_se_eligen_a_mano(int estado)
    {
        var id = await ClienteConCuotaQueVenceMananaAsync();
        using var db = Db();
        var ficha = await Personas(db).ObtenerAsync(id);
        var ex = await Assert.ThrowsAsync<ValidacionException>(() =>
            Personas(db).ActualizarDatosAsync(id, Datos(estado), version: ficha.Versiones["datos"]));
        Assert.Contains("los asigna el sistema", ex.Errores["estadoClienteId"][0]);
    }

    [Fact]
    public async Task Al_convertir_en_cliente_el_estado_es_automatico_o_uno_manual()
    {
        int Prospecto(string nombre)
        {
            using var db = Db();
            var p = new Persona { Nombres = nombre, Apellidos = "Mora", FechaIngreso = _hoy };
            foreach (var s in VersionSeccion.Secciones) p.Versiones.Add(new VersionSeccion { Seccion = s });
            db.Personas.Add(p);
            db.SaveChanges();
            return p.Id;
        }
        async Task<PersonaDetalleDto> Convertir(int id, int? estado)
        {
            using var db = Db();
            var ficha = await Personas(db).ObtenerAsync(id);
            return await Personas(db).ConvertirEnClienteAsync(id, new ConvertirClienteRequest(null, 1, estado), version: ficha.Versiones["datos"]);
        }

        var automatico = await Convertir(Prospecto("Luis"), null);
        Assert.Equal(EstadoCliente.AlDia, automatico.Cliente!.EstadoClienteId);
        Assert.Null(automatico.Cliente.EstadoManualId);

        var manual = await Convertir(Prospecto("Eva"), EstadoCliente.SinExpediente);
        Assert.Equal(EstadoCliente.SinExpediente, manual.Cliente!.EstadoClienteId);
        Assert.Equal(EstadoCliente.SinExpediente, manual.Cliente.EstadoManualId);

        await Assert.ThrowsAsync<ValidacionException>(() => Convertir(Prospecto("Ana"), EstadoCliente.Estrella));
    }

    // ---------- Dobles de prueba ----------

    private sealed class Fecha(Func<DateOnly> hoy) : IFechaActual
    {
        public DateOnly Hoy => hoy();
        public DateTime AhoraUtc => hoy().ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
    }

    private sealed class Usuario(string rol) : IUsuarioActual
    {
        public int? Id => 1;
        public string? Rol => rol;
        public bool EsAdministrador => rol == Roles.Administrador;
    }

    private sealed class SinArchivos : IAlmacenArchivos
    {
        public Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct = default) => Task.FromResult("2026/10/" + Guid.NewGuid().ToString("N") + ".pdf");
        public Task<Stream?> AbrirAsync(string clave, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
        public Task EliminarAsync(string clave, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class SinUsuarios : IIdentityService
    {
        public Task<IReadOnlyDictionary<int, UsuarioDto>> MapaAsync(IEnumerable<int> ids) =>
            Task.FromResult<IReadOnlyDictionary<int, UsuarioDto>>(new Dictionary<int, UsuarioDto>());
        public Task<UsuarioDto?> ObtenerAsync(int id) => Task.FromResult<UsuarioDto?>(null);
        public Task<UsuarioDto?> ValidarCredencialesAsync(string usuario, string contrasena) => throw new NotSupportedException();
        public Task<IReadOnlyList<UsuarioDto>> ListarAsync() => throw new NotSupportedException();
        public Task<UsuarioDto> CrearAsync(GuardarUsuarioRequest request) => throw new NotSupportedException();
        public Task<UsuarioDto> ActualizarAsync(int id, GuardarUsuarioRequest request) => throw new NotSupportedException();
        public Task EliminarAsync(int id) => throw new NotSupportedException();
        public IReadOnlyList<RolDto> Roles() => throw new NotSupportedException();
    }
}
