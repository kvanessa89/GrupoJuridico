using System.Text.RegularExpressions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GrupoJuridico.Gestion.Infrastructure.Services;

public class AlmacenArchivosOptions
{
    public const string Seccion = "Archivos";

    /// <summary>Carpeta donde se guardan los comprobantes. Relativa a la carpeta de la aplicación si no es absoluta.</summary>
    public string Ruta { get; set; } = "App_Data/archivos";
}

/// <summary>
/// Guarda los archivos en disco con un nombre generado (aaaa/mm/guid.ext). El nombre original
/// nunca se usa como ruta, así que no hay forma de escribir o leer fuera de la carpeta.
/// </summary>
public partial class AlmacenArchivosLocal : IAlmacenArchivos
{
    private readonly string _raiz;

    public AlmacenArchivosLocal(IOptions<AlmacenArchivosOptions> opciones, IHostEnvironment entorno)
    {
        var ruta = opciones.Value.Ruta;
        _raiz = Path.GetFullPath(Path.IsPathRooted(ruta) ? ruta : Path.Combine(entorno.ContentRootPath, ruta));
    }

    [GeneratedRegex(@"^\d{4}/\d{2}/[0-9a-f]{32}(\.[a-z0-9]{1,8})?$")]
    private static partial Regex ClaveValida();

    public async Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct = default)
    {
        var ext = extension.ToLowerInvariant().TrimStart('.');
        ext = Regex.IsMatch(ext, "^[a-z0-9]{1,8}$") ? "." + ext : "";
        var hoy = DateTime.UtcNow;
        var clave = $"{hoy:yyyy}/{hoy:MM}/{Guid.NewGuid():N}{ext}";
        var ruta = Ruta(clave);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        await using var destino = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write);
        await contenido.CopyToAsync(destino, ct);
        return clave;
    }

    public Task<Stream?> AbrirAsync(string clave, CancellationToken ct = default)
    {
        if (!ClaveValida().IsMatch(clave)) return Task.FromResult<Stream?>(null);
        var ruta = Ruta(clave);
        return Task.FromResult<Stream?>(File.Exists(ruta) ? new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read) : null);
    }

    public Task EliminarAsync(string clave, CancellationToken ct = default)
    {
        if (ClaveValida().IsMatch(clave))
        {
            var ruta = Ruta(clave);
            if (File.Exists(ruta)) File.Delete(ruta);
        }
        return Task.CompletedTask;
    }

    private string Ruta(string clave) => Path.Combine(_raiz, clave.Replace('/', Path.DirectorySeparatorChar));
}
