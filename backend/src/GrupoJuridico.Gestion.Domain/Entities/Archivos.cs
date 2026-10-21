using GrupoJuridico.Gestion.Domain.Common;

namespace GrupoJuridico.Gestion.Domain.Entities;

/// <summary>Tipo de documento de los archivos del expediente (Poderes, Contrato…). Se administra en Configuración.</summary>
public class TipoDocumento : Catalogo
{
    /// <summary>
    /// Código fijo del tipo "Comprobantes de pago": ahí se guardan los comprobantes de los pagos de Cobros.
    /// Se puede renombrar pero no eliminar.
    /// </summary>
    public const string CodigoComprobantesPago = "COMPROBANTES-PAGO";

    public List<PermisoDocumento> Permisos { get; set; } = new();
}

/// <summary>
/// Qué puede hacer un rol con un tipo de documento. Quien puede subir también puede ver
/// (y eliminar los archivos de ese tipo).
/// </summary>
public class PermisoDocumento : BaseEntity
{
    public int TipoDocumentoId { get; set; }
    public TipoDocumento? TipoDocumento { get; set; }
    /// <summary>Nombre del rol (ver <see cref="Constants.Roles"/>).</summary>
    public string Rol { get; set; } = string.Empty;
    public bool PuedeVer { get; set; }
    public bool PuedeSubir { get; set; }
}

/// <summary>
/// Archivo del expediente de un cliente. El contenido está en el almacenamiento, bajo una clave interna.
/// Los comprobantes de pago son archivos del expediente asociados a su pago (<see cref="PagoId"/>).
/// </summary>
public class ArchivoPersona : BaseEntity
{
    public const long MaxTamano = 10 * 1024 * 1024;

    public int PersonaId { get; set; }
    public int TipoDocumentoId { get; set; }
    public TipoDocumento? TipoDocumento { get; set; }
    /// <summary>Nombre original del archivo (solo para mostrar y descargar).</summary>
    public string Nombre { get; set; } = string.Empty;
    public string TipoContenido { get; set; } = string.Empty;
    public long Tamano { get; set; }
    public DateOnly FechaCarga { get; set; }
    public string Clave { get; set; } = string.Empty;
    /// <summary>Pago al que pertenece, si es el comprobante de un pago de Cobros.</summary>
    public int? PagoId { get; set; }
    public Pago? Pago { get; set; }
}
