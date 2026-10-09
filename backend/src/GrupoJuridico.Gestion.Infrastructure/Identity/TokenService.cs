using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Usuarios;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GrupoJuridico.Gestion.Infrastructure.Identity;

public class JwtOptions
{
    public const string Seccion = "Jwt";

    public string Issuer { get; set; } = "GrupoJuridico.Gestion";
    public string Audience { get; set; } = "GrupoJuridico.Gestion.Web";
    /// <summary>Clave simétrica de al menos 32 caracteres. En Staging/Production se define por variable de entorno.</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpiraMinutos { get; set; } = 480;
}

public class TokenService : ITokenService
{
    private readonly JwtOptions _opciones;

    public TokenService(IOptions<JwtOptions> opciones) => _opciones = opciones.Value;

    public string Generar(UsuarioDto usuario)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, usuario.Usuario),
            new Claim(JwtRegisteredClaimNames.Name, usuario.Nombre),
            new Claim(ClaimTypes.Role, usuario.Rol),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
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
