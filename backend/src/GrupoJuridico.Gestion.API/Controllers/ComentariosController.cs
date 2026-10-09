using GrupoJuridico.Gestion.Application.Personas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrupoJuridico.Gestion.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/comentarios")]
public class ComentariosController : ControllerBase
{
    private readonly PersonasService _personas;

    public ComentariosController(PersonasService personas) => _personas = personas;

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await _personas.EliminarComentarioAsync(id, ct);
        return NoContent();
    }
}
