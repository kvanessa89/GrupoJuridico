using FluentValidation;
using GrupoJuridico.Gestion.Application.Common;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Usuarios;

namespace GrupoJuridico.Gestion.Application.Auth;

public record LoginRequest(string Usuario, string Contrasena);

public record LoginResponse(string Token, UsuarioDto Usuario);

public class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Usuario).NotEmpty();
        RuleFor(x => x.Contrasena).NotEmpty();
    }
}

public class AuthService
{
    private readonly IIdentityService _identity;
    private readonly ITokenService _tokens;
    private readonly LoginValidator _validator = new();

    public AuthService(IIdentityService identity, ITokenService tokens)
    {
        _identity = identity;
        _tokens = tokens;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        await _validator.ValidarAsync(request);
        var usuario = await _identity.ValidarCredencialesAsync(request.Usuario.Trim(), request.Contrasena)
            ?? throw new ValidacionException("credenciales", "Usuario o contraseña incorrectos.");
        return new LoginResponse(_tokens.Generar(usuario), usuario);
    }

    public async Task<UsuarioDto> YoAsync(int id) =>
        await _identity.ObtenerAsync(id) ?? throw new NoEncontradoException("Usuario", id);
}
