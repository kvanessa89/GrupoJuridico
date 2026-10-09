namespace GrupoJuridico.Gestion.Domain.Common;

/// <summary>
/// Base para las listas de referencia (id, código, nombre) que alimentan los dropdowns.
/// </summary>
public abstract class Catalogo : BaseEntity
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}
