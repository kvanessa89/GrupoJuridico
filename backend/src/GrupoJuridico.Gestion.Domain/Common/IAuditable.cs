namespace GrupoJuridico.Gestion.Domain.Common;

/// <summary>
/// Último cambio del registro: quién y cuándo. Al crearlo se llenan con el usuario y la fecha de creación.
/// Los asigna automáticamente el DbContext al guardar; no se asignan a mano.
/// </summary>
public interface IAuditable
{
    DateTime ModificadoEn { get; set; }
    /// <summary>Id del usuario (AspNetUsers). Nulo cuando el cambio lo hace el sistema (seed, procesos).</summary>
    int? ModificadoPorId { get; set; }
}
