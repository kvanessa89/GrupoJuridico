using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Usuarios;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace GrupoJuridico.Gestion.Infrastructure.Identity;

public class JwtOptions
{
    public const string Seccion = "Jwt";

    public string Issuer { get; set; } = "GrupoJuridico.Gestion";
    public string Audience { get; set; } = "GrupoJuridico.Gestion.Web";
    /// <summary>Clave simétrica de al menos 32 caracteres. En Staging/Production se define por variable de entorno.</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpiraMinutos { get; set; } = 60;
}

public class TokenService : ITokenService
{
    public const string SessionStampClaim = "session_stamp";
    private readonly JwtOptions _opciones;
    private readonly UserManager<Usuario> _users;

    public TokenService(IOptions<JwtOptions> opciones, UserManager<Usuario> users)
    {
        _opciones = opciones.Value;
        _users = users;
    }

    public async Task<string> GenerarAsync(UsuarioDto usuario)
    {
        var cuenta = await _users.FindByIdAsync(usuario.Id.ToString())
            ?? throw new InvalidOperationException("La cuenta ya no existe.");
        var stamp = await _users.GetSecurityStampAsync(cuenta);
        if (string.IsNullOrWhiteSpace(stamp))
            throw new InvalidOperationException("La cuenta no tiene identificador de seguridad.");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, usuario.Usuario),
            new Claim(JwtRegisteredClaimNames.Name, usuario.Nombre),
            new Claim(ClaimTypes.Role, usuario.Rol),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(SessionStampClaim, stamp)
        };
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _opciones.Issuer, _opciones.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(_opciones.ExpiraMinutos),
            signingCredentials: credenciales);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
