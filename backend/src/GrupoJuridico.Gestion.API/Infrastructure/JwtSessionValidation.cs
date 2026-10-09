using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GrupoJuridico.Gestion.Infrastructure.Identity;
using GrupoJuridico.Gestion.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Api.Infrastructure;

public static class JwtSessionValidation
{
    public static async Task ValidarAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var sub = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = principal?.FindFirstValue(TokenService.SessionStampClaim);
        var roles = principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
            ?? Array.Empty<string>();
        if (!int.TryParse(sub, out var id) || string.IsNullOrWhiteSpace(stamp) || roles.Length != 1)
        {
            context.Fail("Sesión inválida.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var ct = context.HttpContext.RequestAborted;
        // Consulta fresca por petición: no usa caché ni entidades rastreadas de una operación anterior.
        var actual = await db.Users.AsNoTracking()
            .Where(u => u.Id == id).Select(u => u.SecurityStamp).SingleOrDefaultAsync(ct);
        if (actual == null || !string.Equals(stamp, actual, StringComparison.Ordinal))
        {
            context.Fail("Sesión inválida.");
            return;
        }

        var rolVigente = await (
            from userRole in db.UserRoles
            join role in db.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == id && role.Name == roles[0]
            select userRole.UserId).AnyAsync(ct);
        if (!rolVigente) context.Fail("Sesión inválida.");
    }
}
