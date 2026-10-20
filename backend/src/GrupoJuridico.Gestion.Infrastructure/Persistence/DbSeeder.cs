using System.Globalization;
using System.Text;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Domain.Enums;
using GrupoJuridico.Gestion.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GrupoJuridico.Gestion.Infrastructure.Persistence;

public class SeedOptions
{
    public const string Seccion = "Seed";

    /// <summary>Aplica las migraciones pendientes al iniciar.</summary>
    public bool AplicarMigraciones { get; set; } = true;
    /// <summary>Carga los usuarios y la cartera de demostración del prototipo (solo Development).</summary>
    public bool DatosDemo { get; set; }
    public string AdminUsuario { get; set; } = "admin";
    public string AdminNombre { get; set; } = "Administrador";
    /// <summary>Contraseña inicial del administrador. Si está vacía no se crea el usuario.</summary>
    public string AdminContrasena { get; set; } = string.Empty;
}

/// <summary>Catálogos base, roles, administrador inicial y (opcional) datos de demostración.</summary>
public class DbSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<Usuario> _users;
    private readonly RoleManager<Rol> _roles;
    private readonly SeedOptions _opciones;
    private readonly ILogger<DbSeeder> _log;
    private readonly IHostEnvironment _entorno;

    public DbSeeder(ApplicationDbContext db, UserManager<Usuario> users, RoleManager<Rol> roles, IOptions<SeedOptions> opciones, ILogger<DbSeeder> log, IHostEnvironment entorno)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _opciones = opciones.Value;
        _log = log;
        _entorno = entorno;
    }

    public async Task EjecutarAsync()
    {
        // Validar antes de migraciones o cualquier escritura, incluso si se invoca fuera del arranque de la API.
        if (_opciones.DatosDemo && !_entorno.IsDevelopment())
            throw new InvalidOperationException(
                $"Seed:DatosDemo solo está permitido en Development. Entorno actual: '{_entorno.EnvironmentName}'. Desactive Seed:DatosDemo para iniciar.");

        if (_opciones.AplicarMigraciones && _db.Database.IsRelational())
            await _db.Database.MigrateAsync();

        await RolesAsync();
        await CatalogosAsync();
        await UsuarioAsync(_opciones.AdminUsuario, _opciones.AdminNombre, _opciones.AdminContrasena, Roles.Administrador);

        if (_opciones.DatosDemo)
        {
            await UsuarioAsync("admin", "Natalia Ramírez", "demo-cartera-2026", Roles.Administrador);
            await UsuarioAsync("ventas", "Andrea Vargas", "demo-ventas-2026", Roles.AsistenteVentas);
            await UsuarioAsync("cobros", "Marcela Solís", "demo-cobros-2026", Roles.Cobros);
            await CarteraDemoAsync();
            await PlanesDemoAsync();
        }
    }

    private async Task RolesAsync()
    {
        foreach (var nombre in Roles.Todos)
            if (!await _roles.RoleExistsAsync(nombre))
                await _roles.CreateAsync(new Rol(nombre));
    }

    private async Task UsuarioAsync(string usuario, string nombre, string contrasena, string rol)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrEmpty(contrasena)) return;
        if (await _users.FindByNameAsync(usuario) != null) return;
        var u = new Usuario { UserName = usuario, NombreCompleto = nombre };
        var r = await _users.CreateAsync(u, contrasena);
        if (!r.Succeeded)
        {
            _log.LogWarning("No se pudo crear el usuario {Usuario}: {Errores}", usuario, string.Join("; ", r.Errors.Select(e => e.Description)));
            return;
        }
        await _users.AddToRoleAsync(u, rol);
    }

    private async Task CatalogosAsync()
    {
        if (!await _db.Vendedores.AnyAsync())
        {
            _db.Vendedores.AddRange(
                new Vendedor { Codigo = "30", Nombre = "Marta", Apellidos = "Solís Vargas" },
                new Vendedor { Codigo = "31", Nombre = "Diego", Apellidos = "Ramírez Quesada" },
                new Vendedor { Codigo = "32", Nombre = "Karla", Apellidos = "Montero Jiménez" },
                new Vendedor { Codigo = "33", Nombre = "Esteban", Apellidos = "Núñez Fallas" },
                new Vendedor { Codigo = "01", Nombre = "Grupo Jurídico", Apellidos = "" });
        }
        if (!await _db.ProcedenciasVenta.AnyAsync())
        {
            _db.ProcedenciasVenta.AddRange(
                new ProcedenciaVenta { Codigo = "PV-1", Nombre = "Presencial" },
                new ProcedenciaVenta { Codigo = "PV-2", Nombre = "Virtual" });
        }
        if (!await _db.MetodosVenta.AnyAsync())
        {
            var metodos = new[] { "Edictos", "Whatsapp", "Brouchure", "Recomendados", "Arrendamiento", "Página Facebook" };
            _db.MetodosVenta.AddRange(metodos.Select((n, i) => new MetodoVenta { Codigo = "MV-" + (i + 1), Nombre = n }));
        }
        if (!await _db.EstadosPrima.AnyAsync())
        {
            _db.EstadosPrima.AddRange(
                new EstadoPrima { Id = EstadoPrima.Pendiente, Codigo = "EP-1", Nombre = "Prima Pendiente" },
                new EstadoPrima { Id = EstadoPrima.Incompleta, Codigo = "EP-2", Nombre = "Prima Incompleta" },
                new EstadoPrima { Id = EstadoPrima.Pagada, Codigo = "EP-3", Nombre = "Prima Pagada" });
        }
        if (!await _db.OrigenesCliente.AnyAsync())
        {
            var origenes = new[]
            {
                "Demanda Ejecutiva Hipotecaria Entrando", "Con Edictos Anunciados", "Prospectos de Fideicomiso",
                "Prospectos con Proceso Concursal", "Estrellitas Activas", "Procesos en Abandono", "Sin Prima Pagada",
                "Citas Fallidas", "Citas en Abandono", "Prospectos de Redes Sociales", "Remate en 24 Horas",
                "Después del Remate", "Crédito Rechazado con Hipoteca Atrasada", "Hipoteca Atrasada sin Proceso Judicial"
            };
            _db.OrigenesCliente.AddRange(origenes.Select((n, i) => new OrigenCliente { Codigo = "P" + (i + 1), Nombre = n }));
        }
        if (!await _db.EstadosCliente.AnyAsync())
        {
            var estados = new[] { "Al Día", "Moroso", "Estrella", "Terminado", "Abandonado", "Sin Expediente" };
            _db.EstadosCliente.AddRange(estados.Select((n, i) => new EstadoCliente { Codigo = "EC-" + (i + 1), Nombre = n }));
        }
        if (!await _db.Paises.AnyAsync())
        {
            _db.Paises.AddRange(
                new Pais { Codigo = "+506", Nombre = "Costa Rica" },
                new Pais { Codigo = "+1", Nombre = "Estados Unidos" });
        }
        if (!await _db.Parentescos.AnyAsync())
        {
            var parentescos = new[] { "Cónyuge", "Hijo/a", "Madre", "Padre", "Hermano/a" };
            _db.Parentescos.AddRange(parentescos.Select((n, i) => new Parentesco { Codigo = "PA-" + (i + 1), Nombre = n }));
        }
        if (!await _db.EstadosCuotaPago.AnyAsync())
        {
            _db.EstadosCuotaPago.AddRange(
                new EstadoCuotaPago { Id = EstadoCuotaPago.Pendiente, Codigo = "ECP-1", Nombre = "Pendiente" },
                new EstadoCuotaPago { Id = EstadoCuotaPago.Parcial, Codigo = "ECP-2", Nombre = "Parcial" },
                new EstadoCuotaPago { Id = EstadoCuotaPago.Pagada, Codigo = "ECP-3", Nombre = "Pagada" });
        }
        if (!await _db.EstadosCuotaVencimiento.AnyAsync())
        {
            _db.EstadosCuotaVencimiento.AddRange(
                new EstadoCuotaVencimiento { Id = EstadoCuotaVencimiento.AlDia, Codigo = "ECV-1", Nombre = "Al día" },
                new EstadoCuotaVencimiento { Id = EstadoCuotaVencimiento.Vencido, Codigo = "ECV-2", Nombre = "Vencido" });
        }
        if (!await _db.EstadosPromesa.AnyAsync())
        {
            _db.EstadosPromesa.AddRange(
                new EstadoPromesa { Id = EstadoPromesa.Pendiente, Codigo = "EPP-1", Nombre = "Pendiente" },
                new EstadoPromesa { Id = EstadoPromesa.Cumplida, Codigo = "EPP-2", Nombre = "Cumplida" },
                new EstadoPromesa { Id = EstadoPromesa.Incumplida, Codigo = "EPP-3", Nombre = "Incumplida" });
        }
        if (!await _db.Configuracion.AnyAsync(c => c.Clave == ConfiguracionSistema.InteresMora))
            _db.Configuracion.Add(new ConfiguracionSistema { Clave = ConfiguracionSistema.InteresMora, Valor = "2.5" });
        await _db.SaveChangesAsync();
    }

    private record Semilla(string Nombres, string Apellidos, string Cedula, string Ingreso, int Vendedor, string? Expediente,
        int Procedencia, int Metodo, decimal Monto, decimal Cancelado, string FechaEstimada, string? FechaPago, string Notas);

    private static readonly Semilla[] Cartera =
    {
        new("Ana Lucía", "Vargas Mora", "1-0884-0219", "2025-11-04", 1, "00-000014-2026-CJ", 1, 1, 900000, 300000, "2026-04-12", null, "Cliente con finca en Grecia."),
        new("Roberto", "Cascante Alfaro", "2-0551-0733", "2025-12-18", 2, "00-000018-2026-CJ", 2, 2, 650000, 650000, "2026-02-20", "2026-02-19", "Prima cancelada de una sola vez."),
        new("María José", "Zamora Pérez", "1-1203-0447", "2026-01-09", 1, "00-000021-2026-CJ", 1, 4, 1200000, 1200000, "2026-03-01", "2026-02-27", ""),
        new("Fernando", "Solano Ureña", "4-0198-0612", "2026-01-22", 3, "00-000024-2026-CJ", 2, 6, 480000, 180000, "2026-05-15", null, "Pendiente de firmar addendum."),
        new("Gabriela", "Chinchilla Rojas", "1-0997-0338", "2026-02-03", 4, "00-000027-2026-CJ", 1, 3, 750000, 750000, "2026-04-04", "2026-04-02", ""),
        new("Luis Diego", "Herrera Campos", "3-0402-0855", "2026-02-14", 2, "00-000031-2026-CJ", 2, 2, 540000, 240000, "2026-06-10", null, ""),
        new("Sofía", "Araya Brenes", "1-1455-0290", "2026-02-27", 1, "00-000033-2026-CJ", 1, 5, 980000, 980000, "2026-04-25", "2026-04-21", "Arrendamiento de local comercial."),
        new("Jorge", "Benavides Soto", "2-0711-0509", "2026-03-05", 3, "00-000036-2026-CJ", 1, 1, 620000, 0, "2026-05-30", null, "Entregó copia de cédula."),
        new("Natalia", "Quirós Vega", "1-1088-0714", "2026-03-11", 2, null, 2, 2, 430000, 130000, "2026-06-05", null, "Consulta llegó por WhatsApp."),
        new("Álvaro", "Madrigal Pineda", "5-0266-0431", "2026-03-18", 4, null, 1, 4, 860000, 0, "2026-07-01", null, "Recomendado por cliente EXP-2026-021."),
        new("Carolina", "Espinoza Leiva", "1-1321-0925", "2026-03-24", 1, null, 2, 6, 510000, 255000, "2026-06-20", null, ""),
        new("Mauricio", "Villalobos Ruiz", "3-0587-0146", "2026-04-02", 2, null, 1, 3, 720000, 0, "2026-07-18", null, "Pidió brochure impreso."),
        new("Adriana", "Sánchez Muñoz", "1-1177-0682", "2026-04-09", 3, null, 2, 1, 395000, 95000, "2026-06-28", null, ""),
        new("Randall", "Picado Cordero", "4-0231-0798", "2026-04-15", 4, null, 1, 5, 1050000, 0, "2026-08-05", null, "Finca en proceso de arrendamiento."),
        new("Yendry", "Castro Obando", "2-0640-0357", "2026-04-21", 1, null, 2, 2, 470000, 470000, "2026-05-20", "2026-05-18", "Prima cancelada; falta expediente."),
        new("Óscar", "Jiménez Rodríguez", "1-0905-0263", "2026-04-28", 2, null, 1, 4, 680000, 0, "2026-08-22", null, ""),
        new("Gabriela", "Rojas Umaña", "1-1321-0578", "2026-08-19", 1, null, 2, 2, 560000, 0, "2026-09-15", null, "Contactó por WhatsApp."),
        new("Fabián", "Chaves Arias", "2-0782-0314", "2026-08-26", 3, null, 1, 1, 790000, 290000, "2026-09-24", null, "Abonó parte de la prima."),
        new("Paola", "Mena Salas", "3-0519-0962", "2026-09-02", 4, null, 2, 6, 640000, 0, "2026-09-30", null, "Llegó desde la página de Facebook.")
    };

    private static readonly string[] Telefonos = { "88124477", "70653391", "60438812", "83905527", "87441206", "72189043", "61557734", "84712298" };
    private static readonly string[] TextosComentario =
    {
        "Se le llamó para confirmar la fecha de pago de la prima.",
        "Pidió que lo contacten por WhatsApp después de las 5 p. m.",
        "Pendiente revisar el poder antes de la firma.",
        "Cliente confirmó datos de la finca."
    };

    /// <summary>
    /// Cartera de ejemplo del prototipo. Las fechas se corren al mes en curso (igual que el prototipo)
    /// para que la tabla de ventas, filtrada por el mes actual, siempre tenga datos.
    /// </summary>
    private async Task CarteraDemoAsync()
    {
        if (await _db.Personas.AnyAsync()) return;

        var vendedores = await _db.Vendedores.OrderBy(v => v.Id).Select(v => v.Id).ToListAsync();
        var cr = await _db.Paises.Where(x => x.Codigo == "+506").Select(x => x.Id).FirstAsync();
        var parentescos = await _db.Parentescos.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        var usuarios = await _users.Users.OrderBy(u => u.Id).Select(u => new { u.Id, u.UserName }).ToListAsync();
        int UsuarioId(string nombre) => usuarios.FirstOrDefault(u => u.UserName == nombre)?.Id ?? usuarios.First().Id;
        var autores = new[] { UsuarioId("ventas"), UsuarioId("cobros"), UsuarioId("admin"), UsuarioId("admin") };

        var hoy = DateTime.Today;
        var desplazamiento = Math.Max(0, (hoy.Year * 12 + hoy.Month) - (2026 * 12 + 9));
        DateOnly Mover(string f) => DateOnly.ParseExact(f, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddMonths(desplazamiento);

        var clientes = 0;
        for (var i = 0; i < Cartera.Length; i++)
        {
            var s = Cartera[i];
            var pid = i + 1;
            var persona = new Persona
            {
                Nombres = s.Nombres,
                Apellidos = s.Apellidos,
                Cedula = s.Cedula.Replace("-", ""),
                Expediente = s.Expediente ?? "",
                FechaIngreso = Mover(s.Ingreso),
                VendedorId = vendedores.ElementAtOrDefault(s.Vendedor - 1),
                FechaCreacion = DateTime.UtcNow
            };
            if (s.Expediente != null)
            {
                clientes++;
                var estado = clientes is 6 or 7 ? EstadoCliente.Moroso : new[] { 1, 1, 2, 3, 1, 4 }[i % 6];
                persona.Cliente = new Cliente { Notas = s.Notas, OrigenClienteId = (i % 14) + 1, EstadoClienteId = estado };
            }

            var prima = new Prima
            {
                Monto = Math.Round(s.Monto * 0.25m),
                MontoCancelado = Math.Round(s.Cancelado * 0.25m),
                FechaEstimadaPago = Mover(s.FechaEstimada),
                FechaPago = s.FechaPago == null ? null : Mover(s.FechaPago)
            };
            prima.Recalcular();
            persona.Ventas.Add(new Venta { ProcedenciaVentaId = s.Procedencia, MetodoVentaId = s.Metodo, Monto = s.Monto, Notas = s.Notas, Prima = prima });

            persona.Numeros.Add(new Numero { PaisId = cr, Valor = Telefonos[i % Telefonos.Length], Tipo = TipoNumero.Telefono, Principal = true });
            if (i % 3 == 0) persona.Numeros.Add(new Numero { PaisId = cr, Valor = Telefonos[(i + 3) % Telefonos.Length], Tipo = TipoNumero.Telefono, Principal = false });
            persona.Numeros.Add(new Numero { PaisId = cr, Valor = Telefonos[(i + 1) % Telefonos.Length], Tipo = TipoNumero.WhatsApp, Principal = true });

            var baseCorreo = SinTildes((s.Nombres.Split(' ')[0] + "." + s.Apellidos.Split(' ')[0]).ToLowerInvariant());
            persona.Correos.Add(new CorreoElectronico { Correo = baseCorreo + "@correo.cr", Principal = true });
            persona.Fincas.Add(new Finca { Numero = "0" + (pid % 3 + 1) + "-" + (20000 + pid * 137) });

            if (i % 2 == 0)
            {
                persona.Familiares.Add(new PersonaFamiliar
                {
                    NombreCompleto = s.Nombres.Split(' ')[0] + " " + s.Apellidos.Split(' ').ElementAtOrDefault(1),
                    ParentescoId = parentescos[i % parentescos.Count],
                    TelefonoPaisId = cr,
                    Telefono = Telefonos[(i + 4) % Telefonos.Length],
                    CorreoElectronico = baseCorreo + ".fam@correo.cr"
                });
            }
            if (i % 4 == 0)
            {
                var fecha = persona.FechaIngreso.ToDateTime(new TimeOnly(16, 30), DateTimeKind.Utc);
                persona.Comentarios.Add(new Comentario { UsuarioId = autores[i % 4], Texto = TextosComentario[i % 4], Fecha = fecha });
            }
            _db.Personas.Add(persona);
            // Se guarda una por una para que los ids sigan el orden de la cartera.
            await _db.SaveChangesAsync();
        }
        _log.LogInformation("Cartera de demostración cargada: {Personas} personas.", Cartera.Length);
    }

    /// <summary>
    /// Planes de pago de ejemplo (los del prototipo) para los primeros siete clientes. Las fechas se calculan
    /// desde hoy para que siempre haya morosos, recordatorios, promesas y candidatos a cobro judicial.
    /// </summary>
    private async Task PlanesDemoAsync()
    {
        if (await _db.PlanesPago.AnyAsync()) return;
        var clientes = await _db.Clientes.OrderBy(c => c.Id).Take(7).ToListAsync();
        if (clientes.Count == 0) return;

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var manana = hoy.AddDays(hoy.DayOfWeek == DayOfWeek.Saturday ? 2 : 1);
        // Cliente, total, cuotas, inicio, día de pago, pagadas antes de, abono parcial, fecha de promesa.
        var planes = new (decimal Total, int Cuotas, DateOnly Inicio, int Dia, DateOnly PagarAntes, decimal Parcial, DateOnly? Promesa)[]
        {
            (3600000, 12, hoy.AddDays(-215), hoy.AddDays(-12).Day, hoy.AddDays(-45), 100000, null),
            (1950000, 10, hoy.AddDays(-125), manana.Day, hoy, 0, null),
            (2400000, 12, hoy.AddDays(-155), hoy.AddDays(-20).Day, hoy.AddDays(-25), 0, hoy.AddDays(-5)),
            (1500000, 6, hoy.AddDays(-65), manana.Day, hoy, 0, null),
            (2940000, 12, hoy.AddDays(-95), hoy.AddDays(-6).Day, hoy.AddDays(-10), 0, hoy.AddDays(4)),
            (1800000, 6, hoy.AddDays(-250), hoy.AddDays(-15).Day, hoy.AddDays(-110), 0, null),
            (1200000, 4, hoy.AddDays(-190), hoy.AddDays(-40).Day, hoy.AddDays(-100), 150000, null)
        };
        var porcentaje = 2.5m;
        var interes = await _db.Configuracion.FirstOrDefaultAsync(c => c.Clave == ConfiguracionSistema.InteresMora);
        if (interes != null && decimal.TryParse(interes.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct)) porcentaje = pct;

        for (var i = 0; i < clientes.Count; i++)
        {
            var d = planes[i];
            var plan = new PlanPago
            {
                ClienteId = clientes[i].Id,
                MontoTotal = d.Total,
                MontoCuota = ReglasCobro.MontoMensual(d.Total, d.Cuotas),
                CantidadCuotas = d.Cuotas,
                FechaInicio = d.Inicio,
                DiaPago = d.Dia
            };
            plan.Cuotas = ReglasCobro.GenerarCuotas(plan);
            void Pagar(Cuota c, decimal monto)
            {
                var fecha = c.FechaVencimiento.AddDays(-1) < hoy ? c.FechaVencimiento.AddDays(-1) : hoy;
                var pago = new Pago { MontoPago = monto, FechaPago = fecha };
                pago.Archivos.Add(new ArchivoPago { Nombre = $"comprobante-cuota-{c.NumeroCuota}.pdf", TipoContenido = "application/pdf", FechaCarga = fecha });
                c.Pagos.Add(pago);
            }
            foreach (var c in plan.Cuotas.Where(c => c.FechaVencimiento < d.PagarAntes)) Pagar(c, c.MontoCuota);
            var vencidas = plan.Cuotas.Where(c => c.FechaVencimiento >= d.PagarAntes && c.FechaVencimiento < hoy).ToList();
            if (d.Parcial > 0 && vencidas.Count > 0) Pagar(vencidas[^1], d.Parcial);
            if (d.Promesa is DateOnly fechaPromesa && vencidas.Count > 0)
                vencidas[0].Promesas.Add(new PromesaPago { FechaPromesa = fechaPromesa, MontoPrometido = vencidas[0].MontoCuota });

            ReglasCobro.NormalizarPlan(plan, clientes[i], porcentaje, hoy);
            _db.PlanesPago.Add(plan);
        }
        await _db.SaveChangesAsync();
        _log.LogInformation("Planes de pago de demostración cargados: {Planes}.", clientes.Count);
    }

    private static string SinTildes(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        return new string(normalizado.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
    }
}
