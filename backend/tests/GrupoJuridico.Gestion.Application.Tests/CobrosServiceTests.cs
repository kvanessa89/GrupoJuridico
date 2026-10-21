using GrupoJuridico.Gestion.Application.Archivos;
using GrupoJuridico.Gestion.Application.Cobros;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Tests;

/// <summary>Servicio de Cobros sobre el modelo productivo (SQLite en memoria).</summary>
public sealed class CobrosServiceTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 10, 15);
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");
    private readonly DbContextOptions<ApplicationDbContext> _opciones;
    private readonly AlmacenPrueba _almacen = new();

    public CobrosServiceTests()
    {
        _conexion.Open();
        _opciones = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options;
        using var db = new ApplicationDbContext(_opciones);
        db.Database.EnsureCreated();
        EstadosClientePrueba.Agregar(db);
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

    private CobrosService Servicio(ApplicationDbContext db, string rol = Roles.Cobros) =>
        new(db, new UsuarioPrueba(rol), new FechaPrueba(), _almacen, new ActualizadorCobros(db, new FechaPrueba()));

    private int CrearPersona(bool cliente = true, int primaEstado = EstadoPrima.Pendiente, string nombre = "Ana")
    {
        using var db = Db();
        var prima = new Prima { Monto = 100m, MontoCancelado = primaEstado == EstadoPrima.Pagada ? 100m : 0m };
        prima.Recalcular();
        var p = new Persona { Nombres = nombre, Apellidos = "Vargas", Expediente = "EXP-" + nombre, FechaIngreso = Hoy };
        p.Ventas.Add(new Venta { Monto = 1100m, Prima = prima });
        if (cliente) p.Cliente = new Cliente { OrigenClienteId = 1, EstadoClienteCalculadoId = EstadoClienteCalculado.AlDia };
        db.Personas.Add(p);
        db.SaveChanges();
        return p.Id;
    }

    private static ArchivoSubido Pdf(string nombre = "recibo.pdf") =>
        new(nombre, "application/pdf", 4, new MemoryStream(new byte[] { 1, 2, 3, 4 }));

    /// <summary>Plan de 3 cuotas de 300 000 con la primera vencida hace 5 días.</summary>
    private async Task<(int PersonaId, PlanPersonaDto Plan)> PlanConCuotaVencidaAsync()
    {
        var id = CrearPersona();
        using var db = Db();
        var plan = await Servicio(db).CrearPlanAsync(id, new CrearPlanRequest(900000m, 3, Hoy.AddDays(-10), Hoy.AddDays(-5).Day));
        return (id, plan);
    }

    [Fact]
    public async Task Crear_plan_genera_cuotas_y_marca_moroso_al_vencer()
    {
        var (id, dto) = await PlanConCuotaVencidaAsync();
        Assert.NotNull(dto.Plan);
        Assert.Equal(3, dto.Plan!.Cuotas.Count);
        Assert.Equal(300000m, dto.Plan.MontoCuota);
        Assert.Equal(EstadoCuotaVencimiento.Vencido, dto.Plan.Cuotas[0].EstadoVencimientoId);
        Assert.Equal(7500m, dto.Plan.Cuotas[0].Mora);
        Assert.Equal(EstadoClienteCalculado.Moroso, dto.EstadoClienteCalculadoId);
        Assert.Equal(1000m, dto.MontoSugerido);

        using var db = Db();
        await Assert.ThrowsAsync<ValidacionException>(() =>
            Servicio(db).CrearPlanAsync(id, new CrearPlanRequest(500m, 1, Hoy, 1)));
    }

    [Fact]
    public async Task Crear_plan_valida_datos_cliente_y_permisos()
    {
        var prospecto = CrearPersona(cliente: false, nombre: "Luis");
        var cliente = CrearPersona(nombre: "Eva");
        using var db = Db();
        var s = Servicio(db);
        await Assert.ThrowsAsync<ValidacionException>(() => s.CrearPlanAsync(prospecto, new CrearPlanRequest(1000m, 2, Hoy, 1)));
        var error = await Assert.ThrowsAsync<ValidacionException>(() => s.CrearPlanAsync(cliente, new CrearPlanRequest(0m, 121, null, 32)));
        Assert.Equal(new[] { "cantidadCuotas", "diaPago", "fechaInicio", "montoTotal" }, error.Errores.Keys.OrderBy(k => k));
        await Assert.ThrowsAsync<ProhibidoException>(() =>
            Servicio(db, Roles.AsistenteVentas).CrearPlanAsync(cliente, new CrearPlanRequest(1000m, 2, Hoy, 1)));
        Assert.False(await db.PlanesPago.AnyAsync());
    }

    [Fact]
    public async Task Registrar_pago_exige_comprobante_y_no_supera_el_saldo()
    {
        var (_, dto) = await PlanConCuotaVencidaAsync();
        var cuota = dto.Plan!.Cuotas[0];
        using var db = Db();
        var s = Servicio(db);

        await Assert.ThrowsAsync<ValidacionException>(() => s.RegistrarPagoAsync(cuota.Id, new RegistrarPagoRequest(1000m, Hoy, true), null));
        await Assert.ThrowsAsync<ValidacionException>(() => s.RegistrarPagoAsync(cuota.Id, new RegistrarPagoRequest(307501m, Hoy, true), Pdf()));
        await Assert.ThrowsAsync<ValidacionException>(() => s.RegistrarPagoAsync(cuota.Id, new RegistrarPagoRequest(1000m, Hoy.AddDays(1), true), Pdf()));
        await Assert.ThrowsAsync<ValidacionException>(() => s.RegistrarPagoAsync(cuota.Id, new RegistrarPagoRequest(1000m, Hoy, true), Pdf("virus.exe")));
        Assert.Empty(_almacen.Archivos);
        Assert.False(await db.Pagos.AnyAsync());
    }

    [Fact]
    public async Task Pagar_con_interes_pone_al_dia_y_al_completar_el_plan_queda_estrella()
    {
        var (_, dto) = await PlanConCuotaVencidaAsync();
        using var db = Db();
        var s = Servicio(db);

        var r = await s.RegistrarPagoAsync(dto.Plan!.Cuotas[0].Id, new RegistrarPagoRequest(307500m, Hoy, true), Pdf());
        Assert.Equal(EstadoCuotaPago.Pagada, r.Plan!.Cuotas[0].EstadoPagoId);
        Assert.Equal(EstadoClienteCalculado.AlDia, r.EstadoClienteCalculadoId);
        Assert.False(r.Plan.Cuotas[0].SinMora);
        Assert.Single(_almacen.Archivos);
        Assert.True(r.Plan.Cuotas[0].Pagos[0].Comprobantes[0].Disponible);

        foreach (var c in r.Plan.Cuotas.Skip(1))
            r = await s.RegistrarPagoAsync(c.Id, new RegistrarPagoRequest(c.Saldo, Hoy, true), Pdf());
        Assert.Equal(EstadoClienteCalculado.Estrella, r.EstadoClienteCalculadoId);
        Assert.Equal(0m, r.Plan!.Saldo);
    }

    [Fact]
    public async Task Pagar_sin_interes_por_acuerdo_guarda_el_acuerdo()
    {
        var (_, dto) = await PlanConCuotaVencidaAsync();
        using var db = Db();
        // El cliente prometió la cuota con el interés (307 500), pero se le acepta solo la cuota.
        await Servicio(db).CrearPromesaAsync(dto.Plan!.Cuotas[0].Id, new PromesaRequest(Hoy.AddDays(2), 307500m));
        var r = await Servicio(db).RegistrarPagoAsync(dto.Plan!.Cuotas[0].Id, new RegistrarPagoRequest(300000m, Hoy, false), Pdf());
        var cuota = r.Plan!.Cuotas[0];
        Assert.Equal(EstadoPromesa.Cumplida, cuota.Promesas.Single().EstadoId);
        Assert.True(cuota.SinMora);
        Assert.Equal(0m, cuota.Mora);
        Assert.Equal(7500m, cuota.MoraPosible);
        Assert.Equal(EstadoCuotaPago.Pagada, cuota.EstadoPagoId);
    }

    [Fact]
    public async Task Las_promesas_reciben_los_pagos_y_solo_se_editan_pendientes()
    {
        var (_, dto) = await PlanConCuotaVencidaAsync();
        var cuotaId = dto.Plan!.Cuotas[0].Id;
        using var db = Db();
        var s = Servicio(db);

        await Assert.ThrowsAsync<ValidacionException>(() => s.CrearPromesaAsync(cuotaId, new PromesaRequest(Hoy.AddDays(-1), 1000m)));
        var r = await s.CrearPromesaAsync(cuotaId, new PromesaRequest(Hoy.AddDays(3), 100000m));
        await Assert.ThrowsAsync<ValidacionException>(() => s.CrearPromesaAsync(cuotaId, new PromesaRequest(Hoy.AddDays(4), 1000m)));
        var promesaId = r.Plan!.Cuotas[0].Promesas.Single().Id;

        r = await s.RegistrarPagoAsync(cuotaId, new RegistrarPagoRequest(100000m, Hoy, true), Pdf());
        var promesa = r.Plan!.Cuotas[0].Promesas.Single();
        Assert.Equal(EstadoPromesa.Cumplida, promesa.EstadoId);
        Assert.Equal(100000m, promesa.MontoPagado);
        await Assert.ThrowsAsync<ValidacionException>(() => s.EditarPromesaAsync(promesaId, new PromesaRequest(Hoy.AddDays(5), 50000m)));

        // Bajar el pago devuelve la diferencia a la promesa, que vuelve a quedar pendiente.
        var pagoId = r.Plan.Cuotas[0].Pagos.Single().Id;
        r = await s.EditarPagoAsync(pagoId, new EditarPagoRequest(60000m, Hoy), null);
        promesa = r.Plan!.Cuotas[0].Promesas.Single();
        Assert.Equal(60000m, promesa.MontoPagado);
        Assert.Equal(EstadoPromesa.Pendiente, promesa.EstadoId);
        r = await s.EditarPromesaAsync(promesaId, new PromesaRequest(Hoy.AddDays(5), 50000m));
        Assert.Equal(50000m, r.Plan!.Cuotas[0].Promesas.Single().MontoPagado);
    }

    [Fact]
    public async Task Editar_pago_reemplaza_el_comprobante_y_borra_el_anterior()
    {
        var (_, dto) = await PlanConCuotaVencidaAsync();
        using var db = Db();
        var s = Servicio(db);
        var r = await s.RegistrarPagoAsync(dto.Plan!.Cuotas[0].Id, new RegistrarPagoRequest(1000m, Hoy, true), Pdf("a.pdf"));
        var anterior = _almacen.Archivos.Keys.Single();
        r = await s.EditarPagoAsync(r.Plan!.Cuotas[0].Pagos[0].Id, new EditarPagoRequest(2000m, Hoy), Pdf("b.pdf"));
        Assert.DoesNotContain(anterior, _almacen.Archivos.Keys);
        Assert.Equal("b.pdf", r.Plan!.Cuotas[0].Pagos[0].Comprobantes.Single().Nombre);

        // El comprobante es un archivo del expediente: se descarga como cualquier archivo y el anterior ya no está.
        var archivos = new ArchivosService(db, new UsuarioPrueba(Roles.Cobros), new FechaPrueba(), _almacen);
        var (contenido, nombre, tipo) = await archivos.DescargarAsync(r.Plan.Cuotas[0].Pagos[0].Comprobantes[0].Id);
        Assert.Equal("b.pdf", nombre);
        Assert.Equal("application/pdf", tipo);
        Assert.Equal(4, contenido.Length);
        Assert.Equal(1, await db.ArchivosPersona.CountAsync());
    }

    [Fact]
    public async Task El_comprobante_queda_en_los_archivos_del_cliente_y_no_se_quita_desde_ahi()
    {
        var (personaId, dto) = await PlanConCuotaVencidaAsync();
        using var db = Db();
        var r = await Servicio(db).RegistrarPagoAsync(dto.Plan!.Cuotas[0].Id, new RegistrarPagoRequest(1000m, Hoy, true), Pdf("recibo.pdf"));
        var comprobanteId = r.Plan!.Cuotas[0].Pagos[0].Comprobantes.Single().Id;

        var archivos = new ArchivosService(db, new UsuarioPrueba(Roles.Cobros), new FechaPrueba(), _almacen);
        var lista = await archivos.ListarAsync(personaId);
        var archivo = Assert.Single(lista.Archivos);
        Assert.Equal(comprobanteId, archivo.Id);
        Assert.Equal("recibo.pdf", archivo.Nombre);
        Assert.Equal(1, archivo.CuotaPago);
        Assert.False(archivo.PuedeEliminar);
        Assert.Equal(TipoDocumento.CodigoComprobantesPago, (await db.TiposDocumento.SingleAsync(t => t.Id == archivo.TipoDocumentoId)).Codigo);

        var ex = await Assert.ThrowsAsync<ValidacionException>(() => archivos.EliminarAsync(comprobanteId));
        Assert.Contains("cuota 1", ex.Errores["archivo"][0]);
        Assert.Single(_almacen.Archivos);
    }

    [Fact]
    public async Task Lista_filtra_por_tarjeta_y_cuenta_los_totales()
    {
        await PlanConCuotaVencidaAsync();
        var sinPlan = CrearPersona(nombre: "Eva");
        // Cliente que paga mañana (jueves 15 → recordatorio hoy para el viernes 16).
        var recordar = CrearPersona(nombre: "Leo");
        using (var db = Db())
            await Servicio(db).CrearPlanAsync(recordar, new CrearPlanRequest(200000m, 2, Hoy, 16));

        using var db2 = Db();
        var s = Servicio(db2);
        var morosos = await s.ListarAsync(new CobrosQuery { Vista = VistasCobros.Morosos });
        Assert.Equal(1, morosos.Totales.Morosos);
        Assert.Equal(307500m, morosos.Totales.MontoVencido);
        Assert.Equal(1, morosos.Totales.Recordatorios);
        Assert.Equal("EXP-Ana", morosos.Filas.Single().Expediente);
        Assert.Equal(5, morosos.Filas.Single().Atraso);

        var recordatorios = await s.ListarAsync(new CobrosQuery { Vista = VistasCobros.Recordatorios });
        Assert.Equal("EXP-Leo", recordatorios.Filas.Single().Expediente);
        Assert.Equal(1, recordatorios.Filas.Single().Recordatorio!.NumeroCuota);

        Assert.Empty((await s.ListarAsync(new CobrosQuery { Vista = VistasCobros.CobroJudicial })).Filas);
        Assert.DoesNotContain(morosos.Filas, f => f.Id == sinPlan);
        await Assert.ThrowsAsync<ValidacionException>(() => s.ListarAsync(new CobrosQuery { Vista = "otra" }));
        await Assert.ThrowsAsync<ProhibidoException>(() => Servicio(db2, Roles.AsistenteVentas).ListarAsync(new CobrosQuery()));
    }

    [Fact]
    public async Task Candidato_a_cobro_judicial_cuando_todo_el_plan_vencio_con_saldo()
    {
        var id = CrearPersona();
        using var db = Db();
        var s = Servicio(db);
        await s.CrearPlanAsync(id, new CrearPlanRequest(200000m, 2, Hoy.AddMonths(-3), 1));
        var lista = await s.ListarAsync(new CobrosQuery { Vista = VistasCobros.CobroJudicial });
        Assert.Equal(id, lista.Filas.Single().Id);
        Assert.Equal(1, lista.Totales.CobroJudicial);
    }

    [Fact]
    public async Task El_cambio_de_estado_del_cliente_cambia_la_version_de_datos()
    {
        var id = CrearPersona();
        Guid antes;
        using (var db = Db()) antes = db.Set<VersionSeccion>().Single(v => v.PersonaId == id && v.Seccion == "datos").Version;
        using (var db = Db()) await Servicio(db).CrearPlanAsync(id, new CrearPlanRequest(300000m, 1, Hoy.AddDays(-10), Hoy.AddDays(-5).Day));
        using var db2 = Db();
        Assert.NotEqual(antes, db2.Set<VersionSeccion>().Single(v => v.PersonaId == id && v.Seccion == "datos").Version);
        Assert.Equal(EstadoClienteCalculado.Moroso, db2.Clientes.Single().EstadoClienteCalculadoId);
    }

    [Fact]
    public async Task Ver_el_plan_respeta_el_acceso_a_la_persona()
    {
        var pagada = CrearPersona(primaEstado: EstadoPrima.Pagada, nombre: "Sol");
        using var db = Db();
        await Assert.ThrowsAsync<ProhibidoException>(() => Servicio(db, Roles.AsistenteVentas).ObtenerPlanAsync(pagada));
        var dto = await Servicio(db, Roles.Administrador).ObtenerPlanAsync(pagada);
        Assert.Null(dto.Plan);
        Assert.True(dto.PuedeGestionar);
        Assert.False((await Servicio(db, Roles.AsistenteVentas).ObtenerPlanAsync(CrearPersona(nombre: "Rio"))).PuedeGestionar);
    }

    private sealed class UsuarioPrueba(string rol) : IUsuarioActual
    {
        public int? Id => 1;
        public string? Rol => rol;
        public bool EsAdministrador => rol == Roles.Administrador;
    }

    private sealed class FechaPrueba : IFechaActual
    {
        public DateOnly Hoy => CobrosServiceTests.Hoy;
        public DateTime AhoraUtc => new(2026, 10, 15, 18, 0, 0, DateTimeKind.Utc);
    }

    private sealed class AlmacenPrueba : IAlmacenArchivos
    {
        public Dictionary<string, byte[]> Archivos { get; } = new();

        public async Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await contenido.CopyToAsync(ms, ct);
            var clave = Guid.NewGuid().ToString("N") + extension;
            Archivos[clave] = ms.ToArray();
            return clave;
        }

        public Task<Stream?> AbrirAsync(string clave, CancellationToken ct = default) =>
            Task.FromResult<Stream?>(Archivos.TryGetValue(clave, out var b) ? new MemoryStream(b) : null);

        public Task EliminarAsync(string clave, CancellationToken ct = default)
        {
            Archivos.Remove(clave);
            return Task.CompletedTask;
        }
    }
}
