using GrupoJuridico.Gestion.Application.Archivos;
using GrupoJuridico.Gestion.Application.Cobros;
using GrupoJuridico.Gestion.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrupoJuridico.Gestion.Api.Controllers;

/// <summary>Archivos del expediente del cliente. Los permisos por tipo de documento se revisan en el servicio.</summary>
[ApiController]
[Authorize]
public class ArchivosController : ControllerBase
{
    private readonly ArchivosService _archivos;

    public ArchivosController(ArchivosService archivos) => _archivos = archivos;

    /// <summary>Archivos que el usuario puede ver y los tipos de documento que puede subir.</summary>
    [HttpGet("api/personas/{personaId:int}/archivos")]
    public Task<ArchivosPersonaDto> Listar(int personaId, CancellationToken ct) => _archivos.ListarAsync(personaId, ct);

    /// <summary>Sube uno o varios archivos de un mismo tipo de documento (multipart: tipoDocumentoId y archivos).</summary>
    [HttpPost("api/personas/{personaId:int}/archivos")]
    [RequestSizeLimit(ArchivosService.MaxPorEnvio + 1024 * 1024)]
    public Task<ArchivosPersonaDto> Subir(int personaId, [FromForm] SubirArchivosForm form, CancellationToken ct) =>
        _archivos.SubirAsync(personaId, form.TipoDocumentoId,
            (form.Archivos ?? new List<IFormFile>()).Select(a => new ArchivoSubido(a.FileName, a.ContentType, a.Length, a.OpenReadStream())).ToList(), ct);

    /// <summary>Descarga un archivo del expediente.</summary>
    [HttpGet("api/archivos/{id:int}")]
    public async Task<IActionResult> Descargar(int id, CancellationToken ct)
    {
        var (contenido, nombre, tipo) = await _archivos.DescargarAsync(id, ct);
        return File(contenido, tipo, nombre);
    }

    [HttpDelete("api/archivos/{id:int}")]
    public Task<ArchivosPersonaDto> Eliminar(int id, CancellationToken ct) => _archivos.EliminarAsync(id, ct);

    /// <summary>Matriz de permisos por tipo de documento y rol (Configuración → Archivos).</summary>
    [Authorize(Roles = Roles.Administrador)]
    [HttpGet("api/archivos/permisos")]
    public Task<IReadOnlyList<PermisoDocumentoDto>> Permisos(CancellationToken ct) => _archivos.PermisosDocumentoAsync(ct);

    [Authorize(Roles = Roles.Administrador)]
    [HttpPut("api/archivos/permisos")]
    public Task<PermisoDocumentoDto> GuardarPermiso(GuardarPermisoDocumentoRequest request, CancellationToken ct) =>
        _archivos.GuardarPermisoAsync(request, ct);

    public class SubirArchivosForm
    {
        public int TipoDocumentoId { get; set; }
        public List<IFormFile>? Archivos { get; set; }
    }
}
