using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<Usuario> _users;
    private readonly RoleManager<Rol> _roles;

    public IdentityService(UserManager<Usuario> users, RoleManager<Rol> roles)
    {
        _users = users;
        _roles = roles;
    }

    public async Task<UsuarioDto?> ValidarCredencialesAsync(string usuario, string contrasena)
    {
        var u = await _users.FindByNameAsync(usuario);
        if (u == null || !await _users.CheckPasswordAsync(u, contrasena)) return null;
        return await MapAsync(u);
    }

    public async Task<UsuarioDto?> ObtenerAsync(int id)
    {
        var u = await _users.FindByIdAsync(id.ToString());
        return u == null ? null : await MapAsync(u);
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync()
    {
        var mapa = await MapaAsync(null);
        return mapa.Values.OrderBy(u => u.Id).ToList();
    }

    public async Task<IReadOnlyDictionary<int, UsuarioDto>> MapaAsync(IEnumerable<int>? ids)
    {
        var lista = ids?.ToList();
        var query = _users.Users.AsNoTracking();
        if (lista != null) query = query.Where(u => lista.Contains(u.Id));
        var usuarios = await query.ToListAsync();
        var resultado = new Dictionary<int, UsuarioDto>();
        foreach (var u in usuarios) resultado[u.Id] = await MapAsync(u);
        return resultado;
    }

    public async Task<UsuarioDto> CrearAsync(GuardarUsuarioRequest r)
    {
        if (await _users.FindByNameAsync(r.Usuario) != null)
            throw new ValidacionException(nameof(r.Usuario), "Ese usuario ya existe.");
        var u = new Usuario { UserName = r.Usuario, NombreCompleto = r.Nombre };
        Verificar(await _users.CreateAsync(u, r.Contrasena!));
        Verificar(await _users.AddToRoleAsync(u, r.Rol));
        return await MapAsync(u);
    }

    public async Task<UsuarioDto> ActualizarAsync(int id, GuardarUsuarioRequest r)
    {
        var u = await _users.FindByIdAsync(id.ToString()) ?? throw new NoEncontradoException("Usuario", id);
        var otro = await _users.FindByNameAsync(r.Usuario);
        if (otro != null && otro.Id != id) throw new ValidacionException(nameof(r.Usuario), "Ese usuario ya existe.");

        u.NombreCompleto = r.Nombre;
        if (!string.Equals(u.UserName, r.Usuario, StringComparison.Ordinal))
            Verificar(await _users.SetUserNameAsync(u, r.Usuario));
        Verificar(await _users.UpdateAsync(u));

        if (!string.IsNullOrEmpty(r.Contrasena))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(u);
            Verificar(await _users.ResetPasswordAsync(u, token, r.Contrasena));
        }

        var actuales = await _users.GetRolesAsync(u);
        if (!actuales.SequenceEqual(new[] { r.Rol }))
        {
            if (actuales.Count > 0) Verificar(await _users.RemoveFromRolesAsync(u, actuales));
            Verificar(await _users.AddToRoleAsync(u, r.Rol));
        }
        return await MapAsync(u);
    }

    public async Task EliminarAsync(int id)
    {
        var u = await _users.FindByIdAsync(id.ToString()) ?? throw new NoEncontradoException("Usuario", id);
        Verificar(await _users.DeleteAsync(u));
    }

    public IReadOnlyList<RolDto> Roles() =>
        _roles.Roles.AsNoTracking().OrderBy(r => r.Id).Select(r => new RolDto(r.Id, r.Name!)).ToList();

    private async Task<UsuarioDto> MapAsync(Usuario u)
    {
        var roles = await _users.GetRolesAsync(u);
        return new UsuarioDto(u.Id, u.NombreCompleto, u.UserName ?? "", roles.FirstOrDefault() ?? Domain.Constants.Roles.AsistenteVentas);
    }

    private static void Verificar(IdentityResult resultado)
    {
        if (resultado.Succeeded) return;
        throw new ValidacionException(new Dictionary<string, string[]>
        {
            ["usuario"] = resultado.Errors.Select(e => e.Description).ToArray()
        });
    }
}
