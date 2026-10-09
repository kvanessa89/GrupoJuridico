using GrupoJuridico.Gestion.Application.Personas;
using GrupoJuridico.Gestion.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrupoJuridico.Gestion.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/personas")]
public class PersonasController : ControllerBase
{
    private readonly PersonasService _personas;

    public PersonasController(PersonasService personas) => _personas = personas;

    [HttpGet("{id:int}")]
    public Task<PersonaDetalleDto> Obtener(int id, CancellationToken ct) => _personas.ObtenerAsync(id, ct);

    /// <summary>Registra un prospecto con su venta, prima, contacto principal y familiares.</summary>
    [HttpPost]
    public async Task<ActionResult<PersonaDetalleDto>> Crear(CrearProspectoRequest request, CancellationToken ct)
    {
        var creada = await _personas.CrearProspectoAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:int}/datos")]
    public async Task<IActionResult> Datos(int id, ActualizarDatosRequest request, CancellationToken ct)
    {
        await _personas.ActualizarDatosAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("{id:int}/venta")]
    public Task<VentaDto> Venta(int id, ActualizarVentaRequest request, CancellationToken ct) => _personas.ActualizarVentaAsync(id, request, ct);

    [HttpPut("{id:int}/prima")]
    public Task<PrimaDto> Prima(int id, ActualizarPrimaRequest request, CancellationToken ct) => _personas.ActualizarPrimaAsync(id, request, ct);

    [HttpPut("{id:int}/numeros")]
    public Task<IReadOnlyList<NumeroDto>> Numeros(int id, IReadOnlyList<NumeroItem> items, CancellationToken ct) =>
        _personas.ReemplazarNumerosAsync(id, items, ct);

    [HttpPut("{id:int}/correos")]
    public Task<IReadOnlyList<CorreoDto>> Correos(int id, IReadOnlyList<CorreoItem> items, CancellationToken ct) =>
        _personas.ReemplazarCorreosAsync(id, items, ct);

    [HttpPut("{id:int}/familiares")]
    public Task<IReadOnlyList<FamiliarDto>> Familiares(int id, IReadOnlyList<FamiliarItem> items, CancellationToken ct) =>
        _personas.ReemplazarFamiliaresAsync(id, items, ct);

    /// <summary>Convierte un prospecto en cliente oficial (origen requerido, expediente opcional).</summary>
    [HttpPost("{id:int}/convertir")]
    public Task<PersonaDetalleDto> Convertir(int id, ConvertirClienteRequest request, CancellationToken ct) =>
        _personas.ConvertirEnClienteAsync(id, request, ct);

    /// <summary>Elimina la persona y todo lo relacionado (solo Administrador).</summary>
    [Authorize(Roles = Roles.Administrador)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await _personas.EliminarAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/comentarios")]
    public Task<ComentarioDto> Comentar(int id, ComentarioRequest request, CancellationToken ct) => _personas.ComentarAsync(id, request, ct);
}
