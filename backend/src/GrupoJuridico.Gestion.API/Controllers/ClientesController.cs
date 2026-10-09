using GrupoJuridico.Gestion.Application.Personas;
using GrupoJuridico.Gestion.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrupoJuridico.Gestion.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Administrador + "," + Roles.Cobros)]
[Route("api/clientes")]
public class ClientesController : ControllerBase
{
    private readonly ListasService _listas;

    public ClientesController(ListasService listas) => _listas = listas;

    /// <summary>Clientes oficiales (personas con cliente asignado).</summary>
    [HttpGet]
    public Task<ClientesListaDto> Listar([FromQuery] ListaQuery query, CancellationToken ct) => _listas.ClientesAsync(query, ct);
}
