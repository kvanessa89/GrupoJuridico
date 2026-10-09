using Microsoft.AspNetCore.Identity;

namespace GrupoJuridico.Gestion.Infrastructure.Identity;

// Email, PasswordHash, UserName ya vienen incluidos de IdentityUser<int>.
public class ApplicationUser : IdentityUser<int>
{
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
