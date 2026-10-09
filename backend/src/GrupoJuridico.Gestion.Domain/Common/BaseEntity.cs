namespace GrupoJuridico.Gestion.Domain.Common;

public abstract class BaseEntity : IAuditable
{
    public int Id { get; set; }
    public DateTime ModificadoEn { get; set; }
    public int? ModificadoPorId { get; set; }
}
