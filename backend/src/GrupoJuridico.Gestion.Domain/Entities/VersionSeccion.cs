using GrupoJuridico.Gestion.Domain.Common;

namespace GrupoJuridico.Gestion.Domain.Entities;

/// <summary>
/// Versión independiente para cada grupo de campos que se reemplaza al guardar.
/// La auditoría registra quién y cuándo cambió la versión por última vez.
/// </summary>
public class VersionSeccion : IAuditable
{
    public int PersonaId { get; set; }
    public string Seccion { get; set; } = "";
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTime ModificadoEn { get; set; }
    public int? ModificadoPorId { get; set; }
    public static readonly string[] Secciones = { "datos", "venta", "prima", "familiares" };
}
