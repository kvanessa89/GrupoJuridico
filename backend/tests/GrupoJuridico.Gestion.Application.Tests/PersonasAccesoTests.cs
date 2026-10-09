using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Personas;
using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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
            new ActualizarPrimaRequest(100m, 100m, null, new DateOnly(2026, 10, 9)));

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
            Guardados++;
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
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
