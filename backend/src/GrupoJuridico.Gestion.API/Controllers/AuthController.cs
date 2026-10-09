using GrupoJuridico.Gestion.Application.Auth;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GrupoJuridico.Gestion.Api.Infrastructure;

namespace GrupoJuridico.Gestion.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly IUsuarioActual _actual;

    public AuthController(AuthService auth, IUsuarioActual actual)
    {
        _auth = auth;
        _actual = actual;
    }

    /// <summary>Inicia sesión y devuelve el JWT con el rol del usuario.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(LoginRateLimiting.PolicyName)]
    public Task<LoginResponse> Login(LoginRequest request) => _auth.LoginAsync(request);

    [Authorize]
    [HttpGet("yo")]
    public Task<UsuarioDto> Yo() => _auth.YoAsync(_actual.Id ?? throw new ProhibidoException());
}
