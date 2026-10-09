using FluentValidation;
using GrupoJuridico.Gestion.Application.Common;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Domain.Constants;

namespace GrupoJuridico.Gestion.Application.Usuarios;

public record UsuarioDto(int Id, string Nombre, string Usuario, string Rol);

public record RolDto(int Id, string Nombre);

/// <summary>Alta o edición de un usuario. En edición, una contraseña vacía deja la actual.</summary>
public record GuardarUsuarioRequest(string Nombre, string Usuario, string? Contrasena, string Rol);

public class CrearUsuarioValidator : AbstractValidator<GuardarUsuarioRequest>
{
    public CrearUsuarioValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("Nombre, usuario y contraseña son requeridos.");
        RuleFor(x => x.Usuario).NotEmpty().WithMessage("Nombre, usuario y contraseña son requeridos.");
        RuleFor(x => x.Contrasena).NotEmpty().WithMessage("Nombre, usuario y contraseña son requeridos.")
            .MinimumLength(6).WithMessage("La contraseña debe tener al menos 6 caracteres.");
        RuleFor(x => x.Rol).Must(r => Roles.Todos.Contains(r)).WithMessage("Rol no válido.");
    }
}

public class ActualizarUsuarioValidator : AbstractValidator<GuardarUsuarioRequest>
{
    public ActualizarUsuarioValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().WithMessage("El nombre es requerido.");
        RuleFor(x => x.Usuario).NotEmpty().WithMessage("El usuario es requerido.");
        RuleFor(x => x.Contrasena).MinimumLength(6).When(x => !string.IsNullOrEmpty(x.Contrasena))
            .WithMessage("La contraseña debe tener al menos 6 caracteres.");
        RuleFor(x => x.Rol).Must(r => Roles.Todos.Contains(r)).WithMessage("Rol no válido.");
    }
}

public class UsuariosService
{
    private readonly IIdentityService _identity;
    private readonly IUsuarioActual _actual;
    private readonly CrearUsuarioValidator _crear = new();
    private readonly ActualizarUsuarioValidator _actualizar = new();

    public UsuariosService(IIdentityService identity, IUsuarioActual actual)
    {
        _identity = identity;
        _actual = actual;
    }

    public Task<IReadOnlyList<UsuarioDto>> ListarAsync() => _identity.ListarAsync();

    public async Task<UsuarioDto> CrearAsync(GuardarUsuarioRequest request)
    {
        await _crear.ValidarAsync(request);
        return await _identity.CrearAsync(request with { Nombre = request.Nombre.Trim(), Usuario = request.Usuario.Trim() });
    }

    public async Task<UsuarioDto> ActualizarAsync(int id, GuardarUsuarioRequest request)
    {
        await _actualizar.ValidarAsync(request);
        if (id == _actual.Id && request.Rol != Roles.Administrador)
            throw new ValidacionException(nameof(request.Rol), "No podés quitarte el rol de Administrador.");
        return await _identity.ActualizarAsync(id, request with { Nombre = request.Nombre.Trim(), Usuario = request.Usuario.Trim() });
    }

    public async Task EliminarAsync(int id)
    {
        if (id == _actual.Id) throw new ValidacionException("id", "No podés eliminar tu propio usuario.");
        await _identity.EliminarAsync(id);
    }
}
