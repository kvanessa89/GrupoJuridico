using GrupoJuridico.Gestion.Infrastructure;
using GrupoJuridico.Gestion.Infrastructure.Identity;
using GrupoJuridico.Gestion.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
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
    [InlineData("Personalizado")]
    [InlineData("")]
    public async Task Demo_en_entorno_no_autorizado_falla_sin_escribir(string entorno)
    {
        using var fixture = new Fixture(entorno, datosDemo: true);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Seeder.EjecutarAsync());

        Assert.Contains("Seed:DatosDemo", error.Message);
        Assert.Contains("Development o Testing", error.Message);
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
    [InlineData("Testing")]
    [InlineData("testing")]
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

    private sealed class Fixture : IDisposable
    {
        // Valor exclusivo de la base efímera de pruebas; no se usa como configuración de despliegue.
        public const string ClaveInicialPrueba = "solo para pruebas locales";
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        public ApplicationDbContext Db => _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        public UserManager<Usuario> Users => _scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        public DbSeeder Seeder => _scope.ServiceProvider.GetRequiredService<DbSeeder>();

        public Fixture(string entorno, bool datosDemo, bool relacional = false)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddSingleton<IHostEnvironment>(new EntornoPrueba { EnvironmentName = entorno });
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
