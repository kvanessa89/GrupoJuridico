using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using GrupoJuridico.Gestion.Api.Infrastructure;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Infrastructure;
using GrupoJuridico.Gestion.Infrastructure.Identity;
using GrupoJuridico.Gestion.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace GrupoJuridico.Gestion.Application.Tests;

public class JwtRevocacionTests
{
    [Fact]
    public async Task Token_vigente_accede_y_dura_ocho_horas()
    {
        using var f = new Fixture();
        var id = await f.CrearUsuarioAsync();
        var token = await f.TokenAsync(id);
        Assert.Equal(HttpStatusCode.OK, await f.ConsultarAsync(token));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == TokenService.SessionStampClaim);
        Assert.InRange((jwt.ValidTo - DateTime.UtcNow).TotalMinutes, 479.9, 480.1);
    }

    [Fact]
    public async Task Cambio_de_password_revoca_token_y_permite_nuevo()
    {
        using var f = new Fixture();
        var id = await f.CrearUsuarioAsync();
        var token = await f.TokenAsync(id);
        await f.EditarAsync(id, "otra frase segura", Roles.Administrador);
        Assert.Equal(HttpStatusCode.Unauthorized, await f.ConsultarAsync(token));
        Assert.Equal(HttpStatusCode.OK, await f.ConsultarAsync(await f.TokenAsync(id)));
    }

    [Fact]
    public async Task Cambio_de_rol_revoca_token_anterior_y_nuevo_respeta_permisos()
    {
        using var f = new Fixture();
        var id = await f.CrearUsuarioAsync();
        var token = await f.TokenAsync(id);
        await f.EditarAsync(id, null, Roles.AsistenteVentas);
        Assert.Equal(HttpStatusCode.Unauthorized, await f.ConsultarAsync(token));
        var nuevo = await f.TokenAsync(id);
        Assert.Equal(HttpStatusCode.OK, await f.ConsultarAsync(nuevo));
        Assert.Equal(HttpStatusCode.Forbidden, await f.ConsultarAsync(nuevo, "/admin"));
    }

    [Fact]
    public async Task Volver_al_rol_original_no_reactiva_token_antiguo()
    {
        using var f = new Fixture();
        var id = await f.CrearUsuarioAsync();
        var token = await f.TokenAsync(id);
        await f.EditarAsync(id, null, Roles.AsistenteVentas);
        await f.EditarAsync(id, null, Roles.Administrador);
        Assert.Equal(HttpStatusCode.Unauthorized, await f.ConsultarAsync(token));
    }

    [Fact]
    public async Task Cuenta_eliminada_rechaza_token()
    {
        using var f = new Fixture();
        var id = await f.CrearUsuarioAsync();
        var token = await f.TokenAsync(id);
        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IIdentityService>().EliminarAsync(id);
        Assert.Equal(HttpStatusCode.Unauthorized, await f.ConsultarAsync(token));
    }

    [Fact]
    public async Task Editar_solo_nombre_no_revoca_sesion()
    {
        using var f = new Fixture();
        var id = await f.CrearUsuarioAsync();
        var token = await f.TokenAsync(id);
        await f.EditarAsync(id, null, Roles.Administrador);
        Assert.Equal(HttpStatusCode.OK, await f.ConsultarAsync(token));
    }

    [Fact]
    public async Task Token_legacy_sin_stamp_se_rechaza()
    {
        using var f = new Fixture();
        var id = await f.CrearUsuarioAsync();
        var actual = new JwtSecurityTokenHandler().ReadJwtToken(await f.TokenAsync(id));
        var legacy = f.Firmar(actual.Claims.Where(c => c.Type != TokenService.SessionStampClaim));
        Assert.Equal(HttpStatusCode.Unauthorized, await f.ConsultarAsync(legacy));
    }

    [Fact]
    public async Task Rol_retirado_directamente_tambien_se_rechaza()
    {
        using var f = new Fixture();
        var id = await f.CrearUsuarioAsync();
        var token = await f.TokenAsync(id);
        using (var scope = f.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var u = (await users.FindByIdAsync(id.ToString()))!;
            Assert.True((await users.RemoveFromRoleAsync(u, Roles.Administrador)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, await f.ConsultarAsync(token));
    }

    [Fact]
    public async Task Token_vencido_se_rechaza()
    {
        using var f = new Fixture();
        var id = await f.CrearUsuarioAsync();
        var actual = new JwtSecurityTokenHandler().ReadJwtToken(await f.TokenAsync(id));
        var token = f.Firmar(actual.Claims, DateTime.UtcNow.AddMinutes(-2));
        Assert.Equal(HttpStatusCode.Unauthorized, await f.ConsultarAsync(token));
    }

    private sealed class Fixture : IDisposable
    {
        private const string Key = "test-only-jwt-key-with-at-least-thirty-two-characters";
        private const string Issuer = "GrupoJuridico.Gestion";
        private const string Audience = "GrupoJuridico.Gestion.Web";
        private readonly TestServer _server;
        private readonly HttpClient _client;
        public IServiceProvider Services => _server.Services;

        public Fixture()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Gestion"] = "Host=localhost;Database=not-used;Username=not-used",
                ["Jwt:Key"] = Key
            }).Build();
            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            _server = new TestServer(new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddHttpContextAccessor();
                    services.AddInfrastructure(config);
                    services.AddScoped<ApplicationDbContext>(_ => new ApplicationDbContext(dbOptions));
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
                    {
                        options.Events = new JwtBearerEvents { OnTokenValidated = JwtSessionValidation.ValidarAsync };
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true, ValidIssuer = Issuer,
                            ValidateAudience = true, ValidAudience = Audience,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                            ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1)
                        };
                    });
                    services.AddAuthorization();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/protected", _ => Task.CompletedTask).RequireAuthorization();
                        endpoints.MapGet("/admin", _ => Task.CompletedTask)
                            .RequireAuthorization(policy => policy.RequireRole(Roles.Administrador));
                    });
                }));
            _client = _server.CreateClient();
        }

        public async Task<int> CrearUsuarioAsync()
        {
            using var scope = Services.CreateScope();
            var sp = scope.ServiceProvider;
            var roles = sp.GetRequiredService<RoleManager<Rol>>();
            foreach (var rol in Roles.Todos)
                Assert.True((await roles.CreateAsync(new Rol(rol))).Succeeded);
            var users = sp.GetRequiredService<UserManager<Usuario>>();
            var u = new Usuario { UserName = "prueba", NombreCompleto = "Prueba" };
            Assert.True((await users.CreateAsync(u, "una frase segura")).Succeeded);
            Assert.True((await users.AddToRoleAsync(u, Roles.Administrador)).Succeeded);
            return u.Id;
        }

        public async Task<string> TokenAsync(int id)
        {
            using var scope = Services.CreateScope();
            var sp = scope.ServiceProvider;
            var dto = (await sp.GetRequiredService<IIdentityService>().ObtenerAsync(id))!;
            return await sp.GetRequiredService<ITokenService>().GenerarAsync(dto);
        }

        public async Task EditarAsync(int id, string? password, string rol)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IIdentityService>().ActualizarAsync(id,
                new GuardarUsuarioRequest("Nombre actualizado", "prueba", password, rol));
        }

        public async Task<HttpStatusCode> ConsultarAsync(string token, string path = "/protected")
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _client.SendAsync(request);
            return response.StatusCode;
        }

        public string Firmar(IEnumerable<System.Security.Claims.Claim> claims, DateTime? expires = null)
        {
            var jwt = new JwtSecurityToken(Issuer, Audience,
                claims.Where(c => c.Type is not "exp" and not "iss" and not "aud"),
                expires: expires ?? DateTime.UtcNow.AddMinutes(480),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }

        public void Dispose()
        {
            _client.Dispose();
            _server.Dispose();
        }
    }
}
