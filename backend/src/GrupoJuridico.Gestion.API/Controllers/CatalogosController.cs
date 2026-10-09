using GrupoJuridico.Gestion.Application.Catalogos;
using GrupoJuridico.Gestion.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrupoJuridico.Gestion.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/catalogos")]
public class CatalogosController : ControllerBase
{
    private readonly CatalogosService _catalogos;

    public CatalogosController(CatalogosService catalogos) => _catalogos = catalogos;

    /// <summary>Todas las listas de referencia para dropdowns y filtros.</summary>
    [HttpGet]
    public Task<CatalogosDto> Obtener(CancellationToken ct) => _catalogos.ObtenerAsync(ct);

    /// <summary>tipo: vendedores, procedencias, metodos, estados-prima, origenes, estados-cliente.</summary>
    [Authorize(Roles = Roles.Administrador)]
    [HttpPost("{tipo}")]
    public Task<CatalogoItemDto> Crear(string tipo, GuardarCatalogoItemRequest request, CancellationToken ct) =>
        _catalogos.CrearAsync(tipo, request, ct);

    [Authorize(Roles = Roles.Administrador)]
    [HttpPut("{tipo}/{id:int}")]
    public Task<CatalogoItemDto> Actualizar(string tipo, int id, GuardarCatalogoItemRequest request, CancellationToken ct) =>
        _catalogos.ActualizarAsync(tipo, id, request, ct);

    [Authorize(Roles = Roles.Administrador)]
    [HttpDelete("{tipo}/{id:int}")]
    public async Task<IActionResult> Eliminar(string tipo, int id, CancellationToken ct)
    {
        await _catalogos.EliminarAsync(tipo, id, ct);
        return NoContent();
    }

    [Authorize(Roles = Roles.Administrador)]
    [HttpPut("interes-mora")]
    public Task<decimal> InteresMora(InteresMoraRequest request, CancellationToken ct) =>
        _catalogos.GuardarInteresMoraAsync(request, ct);
}
