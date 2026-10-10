using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Personas;
using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using GrupoJuridico.Gestion.Api.Controllers;
using GrupoJuridico.Gestion.Api.Infrastructure;

namespace GrupoJuridico.Gestion.Application.Tests;

public class PersonasAccesoTests
{
    [Theory]
    [InlineData(Roles.AsistenteVentas, EstadoPrima.Pendiente)]
    [InlineData(Roles.AsistenteVentas, EstadoPrima.Incompleta)]
    [InlineData(Roles.Administrador, EstadoPrima.Pagada)]
    [InlineData(Roles.Cobros, EstadoPrima.Pagada)]
    public async Task Obtener_permite_los_roles_y_estados_autorizados(string rol, int estado)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, estado);
        var dto = await CrearServicio(db, rol).ObtenerAsync(id);
        Assert.Equal(id, dto.Id);
        Assert.Equal(1000m, dto.Venta!.Monto);
    }

    [Fact]
    public async Task Asistente_no_puede_consultar_prima_pagada_por_id()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pagada);
        await Assert.ThrowsAsync<ProhibidoException>(() =>
            CrearServicio(db, Roles.AsistenteVentas).ObtenerAsync(id));
    }

    [Theory]
    [InlineData("datos")]
    [InlineData("venta")]
    [InlineData("prima")]
    [InlineData("numeros")]
    [InlineData("correos")]
    [InlineData("familiares")]
    [InlineData("convertir")]
    [InlineData("comentar")]
    [InlineData("eliminar-comentario")]
    public async Task Asistente_no_puede_modificar_persona_pagada(string operacion)
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pagada);
        var comentarioId = await db.Comentarios.Select(c => c.Id).SingleAsync();
        var servicio = CrearServicio(db, Roles.AsistenteVentas);
        var guardados = db.Guardados;

        Task Ejecutar() => operacion switch
        {
            "datos" => servicio.ActualizarDatosAsync(id, new ActualizarDatosRequest(
                "Otro", "Nombre", "123", "Finca", "", new DateOnly(2026, 10, 9),
                null, null, null, null, null, null)),
            "venta" => servicio.ActualizarVentaAsync(id, new ActualizarVentaRequest(null, null, 2000m, "Cambio")),
            "prima" => servicio.ActualizarPrimaAsync(id, new ActualizarPrimaRequest(100m, 0m, null, null)),
            "numeros" => servicio.ReemplazarNumerosAsync(id, Array.Empty<NumeroItem>()),
            "correos" => servicio.ReemplazarCorreosAsync(id, Array.Empty<CorreoItem>()),
            "familiares" => servicio.ReemplazarFamiliaresAsync(id, Array.Empty<FamiliarItem>()),
            "convertir" => servicio.ConvertirEnClienteAsync(id, new ConvertirClienteRequest("", 1, null)),
            "comentar" => servicio.ComentarAsync(id, new ComentarioRequest("Nuevo")),
            "eliminar-comentario" => servicio.EliminarComentarioAsync(comentarioId),
            _ => throw new InvalidOperationException()
        };

        await Assert.ThrowsAsync<ProhibidoException>(Ejecutar);
        Assert.Equal(guardados, db.Guardados);
        Assert.Single(await db.Comentarios.ToListAsync());
        Assert.Equal("Ana", (await db.Personas.FindAsync(id))!.Nombres);
        Assert.Equal(100m, (await db.Primas.SingleAsync()).MontoCancelado);
    }

    [Fact]
    public async Task Asistente_puede_completar_pago_pero_no_reabrir_ni_revertirlo()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Incompleta);
        var servicio = CrearServicio(db, Roles.AsistenteVentas);
        var dto = await servicio.ActualizarPrimaAsync(id,
            new ActualizarPrimaRequest(100m, 100m, null, new DateOnly(2026, 10, 9)),
            version: (await servicio.ObtenerAsync(id)).Versiones["prima"]);

        Assert.Equal(EstadoPrima.Pagada, dto.EstadoPrimaId);
        Assert.Equal(0m, dto.SaldoPendiente);
        await Assert.ThrowsAsync<ProhibidoException>(() => servicio.ObtenerAsync(id));
        await Assert.ThrowsAsync<ProhibidoException>(() => servicio.ActualizarPrimaAsync(id,
            new ActualizarPrimaRequest(100m, 0m, null, null)));
    }

    [Fact]
    public async Task Crear_con_prima_pagada_devuelve_exito_sin_permitir_consultas_posteriores()
    {
        using var db = CrearDb();
        var servicio = CrearServicio(db, Roles.AsistenteVentas);
        var dto = await servicio.CrearProspectoAsync(new CrearProspectoRequest(
            "Ana", "Vargas", "123", "Finca", "", new DateOnly(2026, 10, 9), 1,
            "88888888", "", "", 1, 1, 1000m, "", 100m, 100m,
            null, new DateOnly(2026, 10, 9), null));

        Assert.True(dto.Id > 0);
        Assert.Equal(EstadoPrima.Pagada, dto.Prima!.EstadoPrimaId);
        await Assert.ThrowsAsync<ProhibidoException>(() => servicio.ObtenerAsync(dto.Id));
    }

    [Fact]
    public async Task Eliminar_comentario_sigue_exigiendo_autoria()
    {
        using var db = CrearDb();
        await SembrarAsync(db, EstadoPrima.Pendiente);
        var c = await db.Comentarios.SingleAsync();
        c.UsuarioId = 99;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ProhibidoException>(() =>
            CrearServicio(db, Roles.Administrador).EliminarComentarioAsync(c.Id));
    }

    [Fact]
    public async Task Edicion_sin_version_no_guarda()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        Assert.Equal(4, (await servicio.ObtenerAsync(id)).Versiones.Count);
        var guardados = db.Guardados;
        await Assert.ThrowsAsync<VersionRequeridaException>(() =>
            servicio.ReemplazarNumerosAsync(id, Array.Empty<NumeroItem>()));
        Assert.Equal(guardados, db.Guardados);
    }

    [Fact]
    public async Task Version_desactualizada_rechaza_el_reemplazo_de_contactos()
    {
        using var db = CrearDb();
        var id = await SembrarAsync(db, EstadoPrima.Pendiente);
        var servicio = CrearServicio(db, Roles.Administrador);
        var version = (await servicio.ObtenerAsync(id)).Versiones["datos"];
        await servicio.ReemplazarNumerosAsync(id, new[] { new NumeroItem(0, "88888888", "Teléfono", true) }, version: version);
        await Assert.ThrowsAsync<ConflictoVersionException>(() =>
            servicio.ReemplazarNumerosAsync(id, Array.Empty<NumeroItem>(), version: version));
        Assert.Single(await db.Numeros.ToListAsync());
    }

    [Fact]
    public async Task Carrera_real_revierte_todos_los_cambios_del_segundo_guardado()
    {
        using var conexion = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await conexion.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDb>().UseSqlite(conexion).Options;
        int id;
        using (var seed = new TestDb(options))
        {
            await seed.Database.EnsureCreatedAsync();
            id = await SembrarAsync(seed, EstadoPrima.Pendiente);
        }
        using var a = new TestDb(options);
        using var b = new TestDb(options);
        var sa = CrearServicio(a, Roles.Administrador);
        var sb = CrearServicio(b, Roles.Administrador);
        var version = (await sa.ObtenerAsync(id)).Versiones["datos"];
        await sb.ObtenerAsync(id); // ambas sesiones ya cargaron la misma versión en EF
        await sa.ReemplazarNumerosAsync(id, new[] { new NumeroItem(0, "88888888", "Teléfono", true) }, version: version);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => sb.ActualizarDatosAsync(id,
            new ActualizarDatosRequest("Cambio perdido", "Vargas", "123", "", "", new DateOnly(2026, 10, 9),
                null, "99999999", null, null, null, null), version: version));
        using var verificar = new TestDb(options);
        Assert.Equal("Ana", (await verificar.Personas.SingleAsync()).Nombres);
        Assert.Equal("88888888", (await verificar.Numeros.SingleAsync()).Valor);
    }

    [Fact]
    public async Task Secciones_distintas_pueden_guardarse_desde_la_misma_lectura()
    {
        using var conexion = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await conexion.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDb>().UseSqlite(conexion).Options;
        int id;
        using (var seed = new TestDb(options))
        {
            await seed.Database.EnsureCreatedAsync();
            id = await SembrarAsync(seed, EstadoPrima.Pendiente);
        }
        using var a = new TestDb(options);
        using var b = new TestDb(options);
        var sa = CrearServicio(a, Roles.Administrador);
        var sb = CrearServicio(b, Roles.Administrador);
        var versiones = (await sa.ObtenerAsync(id)).Versiones;
        await sb.ObtenerAsync(id);
        await sa.ActualizarVentaAsync(id, new ActualizarVentaRequest(null, null, 2000m, "Venta"), version: versiones["venta"]);
        await sb.ActualizarPrimaAsync(id, new ActualizarPrimaRequest(100m, 50m, null, null), version: versiones["prima"]);
        using var verificar = new TestDb(options);
        Assert.Equal(2000m, (await verificar.Ventas.SingleAsync()).Monto);
        Assert.Equal(50m, (await verificar.Primas.SingleAsync()).MontoCancelado);
    }

    [Fact]
    public async Task Http_exige_version_devuelve_etag_y_rechaza_version_antigua()
    {
        using var conexion = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await conexion.OpenAsync();
        var options = new DbContextOptionsBuilder<TestDb>().UseSqlite(conexion).Options;
        int id;
        using (var seed = new TestDb(options))
        {
            await seed.Database.EnsureCreatedAsync();
            id = await SembrarAsync(seed, EstadoPrima.Pendiente);
        }
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(PersonasController).Assembly);
                // La autenticación se prueba por separado; aquí se aísla el contrato HTTP de concurrencia.
                services.AddAuthorization(o => o.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build());
                services.AddScoped(_ => new TestDb(options));
                services.AddScoped(sp => CrearServicio(sp.GetRequiredService<TestDb>(), Roles.Administrador));
                services.AddProblemDetails();
                services.AddExceptionHandler<ManejadorExcepciones>();
            })
            .Configure(app =>
            {
                app.UseExceptionHandler();
                app.UseRouting();
                app.UseAuthorization();
                app.UseEndpoints(e => e.MapControllers());
            }));
        using var http = server.CreateClient();
        var ficha = await http.GetFromJsonAsync<PersonaDetalleDto>($"/api/personas/{id}");
        var original = $"\"{ficha!.Versiones["datos"]:D}\"";
        var ruta = $"/api/personas/{id}/numeros";
        Assert.Equal((HttpStatusCode)428, (await http.PutAsJsonAsync(ruta, Array.Empty<NumeroItem>())).StatusCode);
        http.DefaultRequestHeaders.Add("If-Match", original);
        var primera = await http.PutAsJsonAsync(ruta, new[] { new NumeroItem(0, "88888888", "Teléfono", true) });
        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.NotNull(primera.Headers.ETag);
        Assert.NotEqual(original, primera.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PutAsJsonAsync(ruta, Array.Empty<NumeroItem>())).StatusCode);
        var actual = await http.GetFromJsonAsync<PersonaDetalleDto>($"/api/personas/{id}");
        Assert.Equal("88888888", Assert.Single(actual!.Numeros).Numero);
        http.DefaultRequestHeaders.Remove("If-Match");
        http.DefaultRequestHeaders.Add("If-Match", "*");
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsJsonAsync(ruta, Array.Empty<NumeroItem>())).StatusCode);
    }

    private static TestDb CrearDb() => new(new DbContextOptionsBuilder<TestDb>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<int> SembrarAsync(TestDb db, int estado)
    {
        var prima = new Prima
        {
            Monto = 100m,
            MontoCancelado = estado == EstadoPrima.Pagada ? 100m :
                estado == EstadoPrima.Incompleta ? 50m : 0m
        };
        prima.Recalcular();
        var persona = new Persona { Nombres = "Ana", Apellidos = "Vargas" };
        persona.Ventas.Add(new Venta { Monto = 1000m, Prima = prima });
        persona.Comentarios.Add(new Comentario { UsuarioId = 1, Texto = "Existente", Fecha = DateTime.UtcNow });
        db.Personas.Add(persona);
        await db.SaveChangesAsync();
        var id = persona.Id;
        db.ChangeTracker.Clear();
        return id;
    }

    private static PersonasService CrearServicio(TestDb db, string rol) =>
        new(db, new UsuarioPrueba(rol), new IdentityPrueba(), new FechaPrueba());

    private sealed class UsuarioPrueba(string rol) : IUsuarioActual
    {
        public int? Id => 1;
        public string? Rol => rol;
        public bool EsAdministrador => rol == Roles.Administrador;
    }

    private sealed class FechaPrueba : IFechaActual
    {
        public DateOnly Hoy => new(2026, 10, 9);
        public DateTime AhoraUtc => new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class IdentityPrueba : IIdentityService
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

    private sealed class TestDb(DbContextOptions<TestDb> options) : DbContext(options), IApplicationDbContext
    {
        public DbSet<Persona> Personas => Set<Persona>();
        public DbSet<Cliente> Clientes => Set<Cliente>();
        public DbSet<Venta> Ventas => Set<Venta>();
        public DbSet<Prima> Primas => Set<Prima>();
        public DbSet<Numero> Numeros => Set<Numero>();
        public DbSet<CorreoElectronico> Correos => Set<CorreoElectronico>();
        public DbSet<Finca> Fincas => Set<Finca>();
        public DbSet<PersonaFamiliar> Familiares => Set<PersonaFamiliar>();
        public DbSet<Comentario> Comentarios => Set<Comentario>();
        public DbSet<Vendedor> Vendedores => Set<Vendedor>();
        public DbSet<ProcedenciaVenta> ProcedenciasVenta => Set<ProcedenciaVenta>();
        public DbSet<MetodoVenta> MetodosVenta => Set<MetodoVenta>();
        public DbSet<EstadoPrima> EstadosPrima => Set<EstadoPrima>();
        public DbSet<OrigenCliente> OrigenesCliente => Set<OrigenCliente>();
        public DbSet<EstadoCliente> EstadosCliente => Set<EstadoCliente>();
        public DbSet<ConfiguracionSistema> Configuracion => Set<ConfiguracionSistema>();
        public int Guardados { get; private set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<Persona>().Where(e => e.State == EntityState.Added).ToList())
                foreach (var seccion in VersionSeccion.Secciones)
                    if (!entry.Entity.Versiones.Any(v => v.Seccion == seccion))
                        entry.Entity.Versiones.Add(new VersionSeccion { Seccion = seccion });
            Guardados++;
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<VersionSeccion>().HasKey(x => new { x.PersonaId, x.Seccion });
            model.Entity<VersionSeccion>().Property(x => x.Version).IsConcurrencyToken();
            model.Entity<VersionSeccion>().HasOne<Persona>().WithMany(p => p.Versiones)
                .HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Cascade);
            model.Entity<ConfiguracionSistema>().HasKey(c => c.Clave);
            model.Entity<Persona>().HasOne(p => p.Cliente).WithOne(c => c.Persona)
                .HasForeignKey<Persona>(p => p.ClienteId);
            model.Entity<Persona>().HasMany(p => p.Ventas).WithOne(v => v.Persona)
                .HasForeignKey(v => v.PersonaId);
            model.Entity<Venta>().HasOne(v => v.Prima).WithOne(p => p.Venta)
                .HasForeignKey<Prima>(p => p.VentaId);
            model.Entity<Persona>().HasMany(p => p.Comentarios).WithOne()
                .HasForeignKey(c => c.PersonaId);
            model.Entity<Persona>().Ignore(p => p.EsCliente).Ignore(p => p.NombreCompleto)
                .Ignore(p => p.Venta).Ignore(p => p.Prima).Ignore(p => p.Finca)
                .Ignore(p => p.CorreoPrincipal);
        }
    }
}
