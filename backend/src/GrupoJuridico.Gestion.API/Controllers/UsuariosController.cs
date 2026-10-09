using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrupoJuridico.Gestion.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Administrador)]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly UsuariosService _usuarios;

    public UsuariosController(UsuariosService usuarios) => _usuarios = usuarios;

    [HttpGet]
    public Task<IReadOnlyList<UsuarioDto>> Listar() => _usuarios.ListarAsync();

    [HttpPost]
    public Task<UsuarioDto> Crear(GuardarUsuarioRequest request) => _usuarios.CrearAsync(request);

    /// <summary>Edita nombre, usuario y rol. Si se envía contraseña, se reemplaza.</summary>
    [HttpPut("{id:int}")]
    public Task<UsuarioDto> Actualizar(int id, GuardarUsuarioRequest request) => _usuarios.ActualizarAsync(id, request);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await _usuarios.EliminarAsync(id);
        return NoContent();
    }
}
