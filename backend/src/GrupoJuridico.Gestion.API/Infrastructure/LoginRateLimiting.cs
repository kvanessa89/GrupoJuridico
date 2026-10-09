using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace GrupoJuridico.Gestion.Api.Infrastructure;

public static class LoginRateLimiting
{
    public const string PolicyName = "login";

    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("LoginRateLimit:PermitLimit", 30);
        var windowSeconds = configuration.GetValue("LoginRateLimit:WindowSeconds", 60);
        if (permitLimit <= 0 || windowSeconds <= 0)
            throw new InvalidOperationException("Los límites de login deben ser mayores que cero.");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, context =>
            {
                // RemoteIpAddress solo incorpora forwarded headers procesados por un proxy confiable.
                var ip = context.Connection.RemoteIpAddress;
                var key = ip == null ? "unknown" :
                    (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });
            options.OnRejected = async (context, ct) =>
            {
                var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
                    ? Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds)) : windowSeconds;
                context.HttpContext.Response.Headers["Retry-After"] = retry.ToString(CultureInfo.InvariantCulture);
                await Results.Problem(
                    statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Demasiados intentos. Intentá nuevamente más tarde.")
                    .ExecuteAsync(context.HttpContext);
            };
        });
        return services;
    }
}
