using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Infrastructure;
using GrupoJuridico.Gestion.Infrastructure.Identity;
using GrupoJuridico.Gestion.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace GrupoJuridico.Gestion.Application.Tests;

public class DbSeederEntornoTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    [InlineData("testing")]
    [InlineData("Personalizado")]
    [InlineData("")]
    public async Task Demo_en_entorno_no_autorizado_falla_sin_escribir(string entorno)
    {
        using var fixture = new Fixture(entorno, datosDemo: true);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Seeder.EjecutarAsync());

        Assert.Contains("Seed:DatosDemo", error.Message);
        Assert.Contains("Development", error.Message);
        Assert.DoesNotContain(Fixture.ClaveInicialPrueba, error.Message);
        Assert.Empty(await fixture.Db.Users.ToListAsync());
        Assert.Empty(await fixture.Db.Roles.ToListAsync());
        Assert.Empty(await fixture.Db.Vendedores.ToListAsync());
        Assert.Empty(await fixture.Db.Personas.ToListAsync());
        Assert.Empty(fixture.Db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Production_con_demo_falla_antes_de_acceder_a_la_base_o_migrar()
    {
        // Esta base SQLite no tiene esquema. Sin el control previo, migrar o consultar roles
        // causaría un error de proveedor; la excepción esperada debe ser la del entorno.
        using var fixture = new Fixture("Production", datosDemo: true, relacional: true);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Seeder.EjecutarAsync());
        Assert.Contains("Seed:DatosDemo", error.Message);
        Assert.Empty(fixture.Db.ChangeTracker.Entries());
        Assert.Equal(System.Data.ConnectionState.Closed, fixture.Db.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("development")]
    public async Task Entorno_autorizado_sigue_cargando_demo_y_es_idempotente(string entorno)
    {
        using var fixture = new Fixture(entorno, datosDemo: true);
        await fixture.Seeder.EjecutarAsync();
        Assert.NotNull(await fixture.Users.FindByNameAsync("ventas"));
        Assert.NotNull(await fixture.Users.FindByNameAsync("cobros"));
        Assert.NotNull(await fixture.Users.FindByNameAsync("admin"));
        Assert.True(await fixture.Db.Personas.AnyAsync());

        var personas = await fixture.Db.Personas.CountAsync();
        var usuarios = await fixture.Db.Users.CountAsync();
        await fixture.Seeder.EjecutarAsync();
        Assert.Equal(personas, await fixture.Db.Personas.CountAsync());
        Assert.Equal(usuarios, await fixture.Db.Users.CountAsync());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Personalizado")]
    [InlineData("Testing")]
    public async Task Demo_desactivado_permite_catalogos_roles_y_admin_sin_datos_ficticios(string entorno)
    {
        using var fixture = new Fixture(entorno, datosDemo: false);
        await fixture.Seeder.EjecutarAsync();

        Assert.True(await fixture.Db.Roles.AnyAsync());
        Assert.True(await fixture.Db.EstadosPrima.AnyAsync());
        Assert.NotNull(await fixture.Users.FindByNameAsync("inicial-prueba"));
        Assert.Null(await fixture.Users.FindByNameAsync("ventas"));
        Assert.Null(await fixture.Users.FindByNameAsync("cobros"));
        Assert.Null(await fixture.Users.FindByNameAsync("admin"));
        Assert.Empty(await fixture.Db.Personas.ToListAsync());
    }

    [Fact]
    public async Task Los_comprobantes_de_demostracion_son_archivos_del_expediente_que_se_pueden_descargar()
    {
        using var fixture = new Fixture("Development", datosDemo: true);
        await fixture.Seeder.EjecutarAsync();
        var tipo = await fixture.Db.TiposDocumento.SingleAsync(t => t.Codigo == TipoDocumento.CodigoComprobantesPago);
        var comprobantes = await fixture.Db.ArchivosPersona.Where(a => a.PagoId != null).ToListAsync();
        Assert.NotEmpty(comprobantes);
        Assert.All(comprobantes, a => Assert.Equal(tipo.Id, a.TipoDocumentoId));
        var almacen = fixture.Servicio<IAlmacenArchivos>();
        await using var pdf = await almacen.AbrirAsync(comprobantes[0].Clave);
        Assert.NotNull(pdf);
        var inicio = new byte[5];
        await pdf!.ReadExactlyAsync(inicio);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(inicio));
    }

    [Fact]
    public async Task Los_tipos_de_documento_iniciales_se_cargan_una_vez_y_el_de_comprobantes_siempre_existe()
    {
        using var fixture = new Fixture("Production", datosDemo: false);
        await fixture.Seeder.EjecutarAsync();
        Assert.Equal(new[] { "Poderes", "Contrato", "Comprobantes de pago", "Gestión legal" },
            await fixture.Db.TiposDocumento.OrderBy(t => t.Id).Select(t => t.Nombre).ToListAsync());
        Assert.Equal(12, await fixture.Db.PermisosDocumento.CountAsync());

        // Un tipo eliminado en Configuración no vuelve; el de comprobantes se recrea con sus permisos.
        fixture.Db.TiposDocumento.RemoveRange(fixture.Db.TiposDocumento.Where(t => t.Nombre == "Poderes" || t.Codigo == TipoDocumento.CodigoComprobantesPago));
        await fixture.Db.SaveChangesAsync();
        await fixture.Seeder.EjecutarAsync();
        Assert.False(await fixture.Db.TiposDocumento.AnyAsync(t => t.Nombre == "Poderes"));
        var comprobantes = await fixture.Db.TiposDocumento.Include(t => t.Permisos).SingleAsync(t => t.Codigo == TipoDocumento.CodigoComprobantesPago);
        Assert.Equal(new[] { Roles.Administrador, Roles.Cobros },
            comprobantes.Permisos.Where(p => p.PuedeSubir).Select(p => p.Rol).OrderBy(r => r).ToArray());
    }

    private sealed class Fixture : IDisposable
    {
        // Valor exclusivo de la base efímera de pruebas; no se usa como configuración de despliegue.
        public const string ClaveInicialPrueba = "solo para pruebas locales";
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        public UserManager<Usuario> Users => _scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        public DbSeeder Seeder => _scope.ServiceProvider.GetRequiredService<DbSeeder>();
        public T Servicio<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();
        private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "gj-seeder-" + Guid.NewGuid().ToString("N"));
        public string Carpeta => _carpeta;

        public Fixture(string entorno, bool datosDemo, bool relacional = false)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            // Los comprobantes de demostración se guardan en disco: en una carpeta temporal que se borra al terminar.
            services.AddSingleton<IHostEnvironment>(new EntornoPrueba { EnvironmentName = entorno, ContentRootPath = _carpeta });
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Gestion"] = "Host=localhost;Database=no-utilizada;Username=no-utilizado",
                ["Seed:DatosDemo"] = datosDemo.ToString(),
                ["Seed:AplicarMigraciones"] = "true",
                ["Seed:AdminUsuario"] = "inicial-prueba",
                ["Seed:AdminContrasena"] = ClaveInicialPrueba
            }).Build();
            services.AddInfrastructure(config);
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
            if (relacional) builder.UseSqlite("Data Source=:memory:");
            else builder.UseInMemoryDatabase(Guid.NewGuid().ToString());
            services.AddScoped(_ => new ApplicationDbContext(builder.Options));
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();
        }

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
            if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
        }
    }

    private sealed class EntornoPrueba : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "";
        public string ApplicationName { get; set; } = nameof(DbSeederEntornoTests);
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
