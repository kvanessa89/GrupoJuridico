using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace GrupoJuridico.Gestion.Infrastructure.Services;

/// <summary>Credenciales del bucket de Cloudflare R2 (sección Archivos:R2).</summary>
public class R2Options
{
    /// <summary>Id de la cuenta de Cloudflare; arma el endpoint https://{CuentaId}.r2.cloudflarestorage.com.</summary>
    public string CuentaId { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    /// <summary>Endpoint S3 explícito. Solo para otro servicio compatible con S3 o pruebas locales; si está vacío se usa el de R2.</summary>
    public string Endpoint { get; set; } = string.Empty;

    public string EndpointEfectivo => string.IsNullOrWhiteSpace(Endpoint) ? $"https://{CuentaId}.r2.cloudflarestorage.com" : Endpoint;

    /// <summary>Lista los datos que faltan, con el nombre de la variable de entorno (sin mostrar valores).</summary>
    public IReadOnlyList<string> Faltantes()
    {
        var faltan = new List<string>();
        if (string.IsNullOrWhiteSpace(CuentaId) && string.IsNullOrWhiteSpace(Endpoint)) faltan.Add("Archivos__R2__CuentaId");
        if (string.IsNullOrWhiteSpace(AccessKeyId)) faltan.Add("Archivos__R2__AccessKeyId");
        if (string.IsNullOrWhiteSpace(SecretAccessKey)) faltan.Add("Archivos__R2__SecretAccessKey");
        if (string.IsNullOrWhiteSpace(Bucket)) faltan.Add("Archivos__R2__Bucket");
        return faltan;
    }
}

/// <summary>
/// Guarda los archivos en un bucket privado de Cloudflare R2 (API compatible con S3), con las mismas claves
/// que el almacenamiento local (aaaa/mm/guid.ext): se puede migrar copiando la carpeta al bucket.
/// Los usuarios nunca acceden al bucket; la API descarga y entrega cada archivo después de revisar permisos.
/// </summary>
public sealed class AlmacenArchivosR2 : IAlmacenArchivos, IDisposable
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public AlmacenArchivosR2(IOptions<AlmacenArchivosOptions> opciones)
    {
        var r2 = opciones.Value.R2;
        var faltan = r2.Faltantes();
        if (faltan.Count > 0)
            throw new InvalidOperationException("Archivos:Proveedor es R2 pero falta configurar: " + string.Join(", ", faltan) + ".");
        _bucket = r2.Bucket;
        _s3 = new AmazonS3Client(new BasicAWSCredentials(r2.AccessKeyId, r2.SecretAccessKey), new AmazonS3Config
        {
            ServiceURL = r2.EndpointEfectivo,
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            // R2 no acepta todas las sumas de verificación que el SDK agrega por defecto.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });
    }

    public async Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct = default)
    {
        var clave = ClavesArchivo.Nueva(extension);
        // Los comprobantes son chicos (máx. 10 MB): se cargan en memoria para enviar el largo exacto.
        using var copia = new MemoryStream();
        await contenido.CopyToAsync(copia, ct);
        copia.Position = 0;
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = clave,
            InputStream = copia,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
        }, ct);
        return clave;
    }

    public async Task<Stream?> AbrirAsync(string clave, CancellationToken ct = default)
    {
        if (!ClavesArchivo.Valida(clave)) return null;
        try
        {
            var r = await _s3.GetObjectAsync(_bucket, clave, ct);
            return r.ResponseStream;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task EliminarAsync(string clave, CancellationToken ct = default)
    {
        if (ClavesArchivo.Valida(clave)) await _s3.DeleteObjectAsync(_bucket, clave, ct);
    }

    public void Dispose() => _s3.Dispose();
}
