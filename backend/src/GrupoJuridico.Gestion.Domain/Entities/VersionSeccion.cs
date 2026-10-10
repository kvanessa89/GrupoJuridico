namespace GrupoJuridico.Gestion.Domain.Entities;

/// <summary>Versión independiente para cada grupo de campos que se reemplaza al guardar.</summary>
public class VersionSeccion
{
    public int PersonaId { get; set; }
    public string Seccion { get; set; } = "";
    public Guid Version { get; set; } = Guid.NewGuid();
    public static readonly string[] Secciones = { "datos", "venta", "prima", "familiares" };
}
