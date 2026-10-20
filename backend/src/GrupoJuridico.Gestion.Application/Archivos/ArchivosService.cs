using GrupoJuridico.Gestion.Application.Cobros;
using GrupoJuridico.Gestion.Application.Common;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Personas;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Archivos;

/// <summary>
/// Archivo del expediente tal como lo ve el usuario actual. <c>CuotaPago</c> es el número de cuota cuando el
/// archivo es el comprobante de un pago de Cobros (esos no se quitan desde aquí: se cambian editando el pago).
/// </summary>
public record ArchivoDto(int Id, int TipoDocumentoId, string Nombre, long Tamano, DateOnly FechaCarga, bool PuedeEliminar, int? CuotaPago = null);

/// <summary>Archivos visibles de la persona y los tipos de documento que el usuario puede subir.</summary>
public record ArchivosPersonaDto(IReadOnlyList<ArchivoDto> Archivos, IReadOnlyList<int> TiposSubir);

public record PermisoDocumentoDto(int TipoDocumentoId, string Rol, bool PuedeVer, bool PuedeSubir);

/// <summary>Cambia un permiso de la matriz de Configuración. Si puede subir, también puede ver.</summary>
public record GuardarPermisoDocumentoRequest(int TipoDocumentoId, string Rol, bool PuedeVer, bool PuedeSubir);

/// <summary>
/// Archivos del expediente de los clientes, agrupados por tipo de documento. Cada rol ve y sube
/// solo los tipos permitidos en Configuración → Archivos; eliminar requiere poder subir ese tipo.
/// </summary>
public class ArchivosService
{
    /// <summary>Máximo por envío: queda bajo el límite por defecto de IIS (unos 28 MB).</summary>
    public const long MaxPorEnvio = 25 * 1024 * 1024;
    public const int MaxArchivosPorEnvio = 10;

    private static readonly Dictionary<string, string> Tipos = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    private readonly IApplicationDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly IFechaActual _fecha;
    private readonly IAlmacenArchivos _almacen;

    public ArchivosService(IApplicationDbContext db, IUsuarioActual usuario, IFechaActual fecha, IAlmacenArchivos almacen)
    {
        _db = db;
        _usuario = usuario;
        _fecha = fecha;
        _almacen = almacen;
    }

    /// <summary>Permisos del rol actual por tipo de documento (sin fila = sin permiso).</summary>
    private async Task<Dictionary<int, (bool Ver, bool Subir)>> PermisosAsync(CancellationToken ct)
    {
        var rol = _usuario.Rol ?? "";
        return await _db.PermisosDocumento.AsNoTracking().Where(p => p.Rol == rol)
            .ToDictionaryAsync(p => p.TipoDocumentoId, p => (p.PuedeVer || p.PuedeSubir, p.PuedeSubir), ct);
    }

    /// <summary>La persona debe existir, ser accesible para el rol y ser cliente oficial.</summary>
    private async Task<Persona> PersonaAsync(int personaId, CancellationToken ct)
    {
        var persona = await _db.Personas.AsNoTracking().Include(p => p.Ventas).ThenInclude(v => v.Prima)
            .FirstOrDefaultAsync(p => p.Id == personaId, ct) ?? throw new NoEncontradoException("Persona", personaId);
        if (!PersonasService.AccesoPermitido(_usuario.Rol, persona))
            throw new ProhibidoException("No tiene permiso para acceder a esta persona.");
        return persona;
    }

    public async Task<ArchivosPersonaDto> ListarAsync(int personaId, CancellationToken ct = default)
    {
        var persona = await PersonaAsync(personaId, ct);
        var permisos = await PermisosAsync(ct);
        var visibles = permisos.Where(p => p.Value.Ver).Select(p => p.Key).ToList();
        var archivos = await _db.ArchivosPersona.AsNoTracking()
            .Where(a => a.PersonaId == personaId && visibles.Contains(a.TipoDocumentoId))
            .OrderByDescending(a => a.FechaCarga).ThenByDescending(a => a.Id)
            .Select(a => new { a.Id, a.TipoDocumentoId, a.Nombre, a.Tamano, a.FechaCarga, CuotaPago = a.PagoId == null ? (int?)null : a.Pago!.Cuota!.NumeroCuota })
            .ToListAsync(ct);
        var subibles = permisos.Where(p => p.Value.Subir).Select(p => p.Key).ToList();
        // Solo se sube a clientes oficiales (la pestaña Archivos es de la ficha del cliente).
        var tiposSubir = persona.EsCliente
            ? await _db.TiposDocumento.AsNoTracking().Where(t => subibles.Contains(t.Id)).OrderBy(t => t.Id).Select(t => t.Id).ToListAsync(ct)
            : new List<int>();
        return new ArchivosPersonaDto(
            archivos.Select(a => new ArchivoDto(a.Id, a.TipoDocumentoId, a.Nombre, a.Tamano, a.FechaCarga,
                permisos[a.TipoDocumentoId].Subir && a.CuotaPago == null, a.CuotaPago)).ToList(),
            tiposSubir);
    }

    public async Task<ArchivosPersonaDto> SubirAsync(int personaId, int tipoDocumentoId, IReadOnlyList<ArchivoSubido> archivos, CancellationToken ct = default)
    {
        var persona = await PersonaAsync(personaId, ct);
        if (!persona.EsCliente)
            throw new ValidacionException("archivos", "Los archivos se cargan cuando la persona es cliente oficial.");
        if (tipoDocumentoId <= 0 || !await _db.TiposDocumento.AnyAsync(t => t.Id == tipoDocumentoId, ct))
            throw new ValidacionException("tipoDocumentoId", "Seleccione el tipo de documento antes de subir.");
        var permisos = await PermisosAsync(ct);
        if (!permisos.TryGetValue(tipoDocumentoId, out var permiso) || !permiso.Subir)
            throw new ProhibidoException("Su rol no puede subir este tipo de documento.");

        if (archivos.Count == 0) throw new ValidacionException("archivos", "Seleccione al menos un archivo.");
        if (archivos.Count > MaxArchivosPorEnvio)
            throw new ValidacionException("archivos", $"Suba como máximo {MaxArchivosPorEnvio} archivos a la vez.");
        if (archivos.Sum(a => a.Tamano) > MaxPorEnvio)
            throw new ValidacionException("archivos", "Los archivos de un envío suman más de 25 MB. Súbalos en varias partes.");
        var validados = archivos.Select(a =>
        {
            var extension = Path.GetExtension(a.Nombre ?? "");
            if (a.Tamano <= 0 || a.Tamano > ArchivoPersona.MaxTamano || !Tipos.TryGetValue(extension, out var tipo))
                throw new ValidacionException("archivos",
                    $"\"{NombreSeguro(a.Nombre)}\" no se puede subir: use PDF, imagen (JPG, PNG, WEBP), Word o Excel de hasta 10 MB.");
            return (Archivo: a, Extension: extension, Tipo: tipo);
        }).ToList();

        var claves = new List<string>();
        try
        {
            var hoy = _fecha.Hoy;
            foreach (var v in validados)
            {
                var clave = await _almacen.GuardarAsync(v.Archivo.Contenido, v.Extension, ct);
                claves.Add(clave);
                _db.ArchivosPersona.Add(new ArchivoPersona
                {
                    PersonaId = personaId, TipoDocumentoId = tipoDocumentoId, Nombre = NombreSeguro(v.Archivo.Nombre),
                    TipoContenido = v.Tipo, Tamano = v.Archivo.Tamano, FechaCarga = hoy, Clave = clave,
                });
            }
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Si algo falla, no quedan archivos guardados sin su registro.
            foreach (var clave in claves) await _almacen.EliminarAsync(clave, CancellationToken.None);
            throw;
        }
        return await ListarAsync(personaId, ct);
    }

    public async Task<(Stream Contenido, string Nombre, string Tipo)> DescargarAsync(int archivoId, CancellationToken ct = default)
    {
        var archivo = await _db.ArchivosPersona.AsNoTracking().FirstOrDefaultAsync(a => a.Id == archivoId, ct)
            ?? throw new NoEncontradoException("Archivo", archivoId);
        await PersonaAsync(archivo.PersonaId, ct);
        var permisos = await PermisosAsync(ct);
        if (!permisos.TryGetValue(archivo.TipoDocumentoId, out var permiso) || !permiso.Ver)
            throw new ProhibidoException("Su rol no puede ver este tipo de documento.");
        var contenido = await _almacen.AbrirAsync(archivo.Clave, ct) ?? throw new NoEncontradoException("Contenido del archivo", archivoId);
        return (contenido, archivo.Nombre, string.IsNullOrEmpty(archivo.TipoContenido) ? "application/octet-stream" : archivo.TipoContenido);
    }

    public async Task<ArchivosPersonaDto> EliminarAsync(int archivoId, CancellationToken ct = default)
    {
        var archivo = await _db.ArchivosPersona.FirstOrDefaultAsync(a => a.Id == archivoId, ct)
            ?? throw new NoEncontradoException("Archivo", archivoId);
        await PersonaAsync(archivo.PersonaId, ct);
        var permisos = await PermisosAsync(ct);
        if (!permisos.TryGetValue(archivo.TipoDocumentoId, out var permiso) || !permiso.Subir)
            throw new ProhibidoException("Su rol no puede eliminar este tipo de documento.");
        if (archivo.PagoId is int pagoId)
        {
            var cuota = await _db.Pagos.Where(p => p.Id == pagoId).Select(p => p.Cuota!.NumeroCuota).FirstOrDefaultAsync(ct);
            throw new ValidacionException("archivo",
                $"Este archivo es el comprobante del pago de la cuota {cuota}. Para cambiarlo, edite el pago en el plan de pago.");
        }
        _db.ArchivosPersona.Remove(archivo);
        await _db.SaveChangesAsync(ct);
        // Primero se borra el registro: si falla el almacenamiento solo queda un archivo huérfano, nunca un registro roto.
        await _almacen.EliminarAsync(archivo.Clave, CancellationToken.None);
        return await ListarAsync(archivo.PersonaId, ct);
    }

    // ---------- Permisos (Configuración → Archivos) ----------

    private void ExigirAdministrador()
    {
        if (!_usuario.EsAdministrador) throw new ProhibidoException();
    }

    public async Task<IReadOnlyList<PermisoDocumentoDto>> PermisosDocumentoAsync(CancellationToken ct = default)
    {
        ExigirAdministrador();
        return await _db.PermisosDocumento.AsNoTracking().OrderBy(p => p.TipoDocumentoId).ThenBy(p => p.Rol)
            .Select(p => new PermisoDocumentoDto(p.TipoDocumentoId, p.Rol, p.PuedeVer || p.PuedeSubir, p.PuedeSubir)).ToListAsync(ct);
    }

    public async Task<PermisoDocumentoDto> GuardarPermisoAsync(GuardarPermisoDocumentoRequest r, CancellationToken ct = default)
    {
        ExigirAdministrador();
        if (!Roles.Todos.Contains(r.Rol)) throw new ValidacionException("rol", "El rol no existe.");
        if (!await _db.TiposDocumento.AnyAsync(t => t.Id == r.TipoDocumentoId, ct))
            throw new ValidacionException("tipoDocumentoId", "El tipo de documento ya no existe. Recargue la página.");
        var permiso = await _db.PermisosDocumento.FirstOrDefaultAsync(p => p.TipoDocumentoId == r.TipoDocumentoId && p.Rol == r.Rol, ct);
        if (permiso == null)
        {
            permiso = new PermisoDocumento { TipoDocumentoId = r.TipoDocumentoId, Rol = r.Rol };
            _db.PermisosDocumento.Add(permiso);
        }
        permiso.PuedeSubir = r.PuedeSubir;
        permiso.PuedeVer = r.PuedeVer || r.PuedeSubir;
        await _db.SaveChangesAsync(ct);
        return new PermisoDocumentoDto(permiso.TipoDocumentoId, permiso.Rol, permiso.PuedeVer, permiso.PuedeSubir);
    }

    private static string NombreSeguro(string? nombre) => Validacion.NombreArchivo(nombre, "archivo");
}
