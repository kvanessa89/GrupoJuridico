using System.Net;
using GrupoJuridico.Gestion.Api.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrupoJuridico.Gestion.Application.Tests;

public class LoginRateLimitingTests
{
    [Fact]
    public async Task Login_excesivo_devuelve_429_retry_after_y_no_limita_otras_rutas()
    {
        using var server = CrearServer();
        using var client = server.CreateClient();
        using var a = await client.PostAsync("/login", null);
        using var b = await client.PostAsync("/login", null);
        using var c = await client.PostAsync("/login", null);
        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal(HttpStatusCode.OK, b.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, c.StatusCode);
        Assert.True(c.Headers.Contains("Retry-After"));
        using var other = await client.GetAsync("/other");
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task Cambiar_forwarded_for_no_evade_limite()
    {
        using var server = CrearServer();
        using var client = server.CreateClient();
        for (var i = 1; i <= 3; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/login");
            request.Headers.Add("X-Forwarded-For", "192.0.2." + i);
            using var response = await client.SendAsync(request);
            Assert.Equal(i <= 2 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task Cada_ip_tiene_su_propio_cupo()
    {
        using var server = CrearServer();
        async Task<int> Enviar(string ip)
        {
            var result = await server.SendAsync(context =>
            {
                context.Request.Method = "POST";
                context.Request.Path = "/login";
                context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            });
            return result.Response.StatusCode;
        }

        Assert.Equal(200, await Enviar("192.0.2.1"));
        Assert.Equal(200, await Enviar("192.0.2.1"));
        Assert.Equal(429, await Enviar("192.0.2.1"));
        Assert.Equal(200, await Enviar("192.0.2.2"));
    }

    [Fact]
    public void Configuracion_invalida_no_desactiva_limite_silenciosamente()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LoginRateLimit:PermitLimit"] = "0"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddLoginRateLimiting(config));
    }

    private static TestServer CrearServer()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LoginRateLimit:PermitLimit"] = "2",
            ["LoginRateLimit:WindowSeconds"] = "60"
        }).Build();
        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddLoginRateLimiting(config);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseRateLimiter();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapPost("/login", context =>
                    {
                        context.Response.StatusCode = 200;
                        return Task.CompletedTask;
                    }).RequireRateLimiting(LoginRateLimiting.PolicyName);
                    endpoints.MapGet("/other", context =>
                    {
                        context.Response.StatusCode = 200;
                        return Task.CompletedTask;
                    });
                });
            }));
    }
}
