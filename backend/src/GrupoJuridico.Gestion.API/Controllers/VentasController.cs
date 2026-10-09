using GrupoJuridico.Gestion.Application.Personas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrupoJuridico.Gestion.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ventas")]
public class VentasController : ControllerBase
{
    private readonly ListasService _listas;

    public VentasController(ListasService listas) => _listas = listas;

    /// <summary>Tabla de ventas: personas con prima, filtradas y paginadas, con las tarjetas de totales.</summary>
    [HttpGet]
    public Task<VentasListaDto> Listar([FromQuery] ListaQuery query, CancellationToken ct) => _listas.VentasAsync(query, ct);
}
