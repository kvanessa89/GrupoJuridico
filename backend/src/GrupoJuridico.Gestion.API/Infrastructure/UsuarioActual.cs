using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Domain.Constants;

namespace GrupoJuridico.Gestion.Api.Infrastructure;

public class UsuarioActual : IUsuarioActual
{
    private readonly IHttpContextAccessor _http;

    public UsuarioActual(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public int? Id
    {
        get
        {
            var sub = Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Rol => Principal?.FindFirstValue(ClaimTypes.Role);

    public bool EsAdministrador => Rol == Roles.Administrador;
}
