using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Infrastructure;
using GrupoJuridico.Gestion.Infrastructure.Identity;
using GrupoJuridico.Gestion.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrupoJuridico.Gestion.Application.Tests;

public class LoginSeguridadTests
{
    [Fact]
    public async Task Quinto_fallo_bloquea_y_contrasena_correcta_no_evade_bloqueo()
    {
        using var fixture = new Fixture();
        var u = await fixture.CrearAsync();
        for (var i = 0; i < 4; i++)
        {
            Assert.Null(await fixture.Login.ValidarCredencialesAsync("prueba", "incorrecta"));
            Assert.False(await fixture.Users.IsLockedOutAsync(u));
        }
        var antes = DateTimeOffset.UtcNow;
        Assert.Null(await fixture.Login.ValidarCredencialesAsync("prueba", "incorrecta"));
        Assert.True(await fixture.Users.IsLockedOutAsync(u));
        Assert.InRange((u.LockoutEnd!.Value - antes).TotalMinutes, 14.9, 15.1);
        Assert.Null(await fixture.Login.ValidarCredencialesAsync("prueba", Fixture.Password));
    }

    [Fact]
    public async Task Login_correcto_reinicia_el_contador()
    {
        using var fixture = new Fixture();
        var u = await fixture.CrearAsync();
        for (var i = 0; i < 4; i++)
            Assert.Null(await fixture.Login.ValidarCredencialesAsync("prueba", "incorrecta"));
        Assert.Equal(4, await fixture.Users.GetAccessFailedCountAsync(u));
        Assert.NotNull(await fixture.Login.ValidarCredencialesAsync("prueba", Fixture.Password));
        Assert.Equal(0, await fixture.Users.GetAccessFailedCountAsync(u));
        Assert.Null(await fixture.Login.ValidarCredencialesAsync("prueba", "incorrecta"));
        Assert.False(await fixture.Users.IsLockedOutAsync(u));
    }

    [Fact]
    public async Task Login_se_recupera_cuando_el_plazo_expira()
    {
        using var fixture = new Fixture();
        var u = await fixture.CrearAsync();
        for (var i = 0; i < 5; i++)
            await fixture.Login.ValidarCredencialesAsync("prueba", "incorrecta");
        Assert.True(await fixture.Users.IsLockedOutAsync(u));
        // Simula el fin del plazo sin esperar quince minutos.
        Assert.True((await fixture.Users.SetLockoutEndDateAsync(u, DateTimeOffset.UtcNow.AddSeconds(-1))).Succeeded);
        Assert.NotNull(await fixture.Login.ValidarCredencialesAsync("prueba", Fixture.Password));
        Assert.Equal(0, await fixture.Users.GetAccessFailedCountAsync(u));
    }

    [Fact]
    public async Task Usuario_inexistente_no_autentica()
    {
        using var fixture = new Fixture();
        Assert.Null(await fixture.Login.ValidarCredencialesAsync("inexistente", Fixture.Password));
    }

    [Fact]
    public async Task Identity_rechaza_nuevas_cortas_y_admite_frases_con_espacios()
    {
        using var fixture = new Fixture();
        var corta = await fixture.Users.CreateAsync(new Usuario { UserName = "corta" }, "123456");
        Assert.False(corta.Succeeded);
        Assert.Contains(corta.Errors, e => e.Code == "PasswordTooShort");
        Assert.True((await fixture.Users.CreateAsync(new Usuario { UserName = "frase" }, Fixture.Password)).Succeeded);
    }

    [Fact]
    public async Task Password_legacy_corta_sigue_permitiendo_login()
    {
        using var fixture = new Fixture();
        var options = fixture.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;
        options.Password.RequiredLength = 6;
        await fixture.CrearAsync("legacy", "123456");
        options.Password.RequiredLength = 12;
        Assert.NotNull(await fixture.Login.ValidarCredencialesAsync("legacy", "123456"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("123456", false)]
    [InlineData("doce letras!", true)]
    public void Edicion_sin_password_conserva_actual_y_reemplazo_exige_doce(string? password, bool valido)
    {
        var request = new GuardarUsuarioRequest("Ana", "ana", password, "Administrador");
        Assert.Equal(valido, new ActualizarUsuarioValidator().Validate(request).IsValid);
    }

    [Fact]
    public void Alta_exige_doce_caracteres()
    {
        var request = new GuardarUsuarioRequest("Ana", "ana", "123456", "Administrador");
        Assert.False(new CrearUsuarioValidator().Validate(request).IsValid);
        Assert.True(new CrearUsuarioValidator().Validate(request with { Contrasena = Fixture.Password }).IsValid);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Password = "una frase larga";
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        public IServiceProvider Services => _scope.ServiceProvider;
        public UserManager<Usuario> Users => Services.GetRequiredService<UserManager<Usuario>>();
        public IdentityService Login => (IdentityService)Services.GetRequiredService<GrupoJuridico.Gestion.Application.Common.Interfaces.IIdentityService>();

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthentication();
            services.AddHttpContextAccessor();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Gestion"] = "Host=localhost;Database=not-used;Username=not-used"
            }).Build();
            services.AddInfrastructure(config);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            // Sustituye el contexto PostgreSQL por InMemory; Identity usa su store real.
            services.AddScoped<ApplicationDbContext>(_ => new ApplicationDbContext(options));
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();
        }

        public async Task<Usuario> CrearAsync(string nombre = "prueba", string password = Password)
        {
            var u = new Usuario { UserName = nombre, NombreCompleto = "Prueba" };
            Assert.True((await Users.CreateAsync(u, password)).Succeeded);
            Assert.True(u.LockoutEnabled);
            return u;
        }

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
        }
    }
}
