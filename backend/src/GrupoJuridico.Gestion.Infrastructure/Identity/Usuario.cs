using GrupoJuridico.Gestion.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace GrupoJuridico.Gestion.Infrastructure.Identity;

public class Usuario : IdentityUser<int>, IAuditable
{
    public string NombreCompleto { get; set; } = string.Empty;

    public DateTime ModificadoEn { get; set; }
    public int? ModificadoPorId { get; set; }
}

public class Rol : IdentityRole<int>
{
    public Rol() { }
    public Rol(string nombre) : base(nombre) { }
}
