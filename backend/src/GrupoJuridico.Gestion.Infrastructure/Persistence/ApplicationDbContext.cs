using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<Usuario, Rol, int>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<Prima> Primas => Set<Prima>();
    public DbSet<Numero> Numeros => Set<Numero>();
    public DbSet<CorreoElectronico> Correos => Set<CorreoElectronico>();
    public DbSet<Finca> Fincas => Set<Finca>();
    public DbSet<PersonaFamiliar> Familiares => Set<PersonaFamiliar>();
    public DbSet<Comentario> Comentarios => Set<Comentario>();

    public DbSet<Vendedor> Vendedores => Set<Vendedor>();
    public DbSet<ProcedenciaVenta> ProcedenciasVenta => Set<ProcedenciaVenta>();
    public DbSet<MetodoVenta> MetodosVenta => Set<MetodoVenta>();
    public DbSet<EstadoPrima> EstadosPrima => Set<EstadoPrima>();
    public DbSet<OrigenCliente> OrigenesCliente => Set<OrigenCliente>();
    public DbSet<EstadoCliente> EstadosCliente => Set<EstadoCliente>();
    public DbSet<ConfiguracionSistema> Configuracion => Set<ConfiguracionSistema>();

    private void InicializarVersiones()
    {
        foreach (var entry in ChangeTracker.Entries<Persona>().Where(e => e.State == EntityState.Added).ToList())
            foreach (var seccion in VersionSeccion.Secciones)
                if (!entry.Entity.Versiones.Any(v => v.Seccion == seccion))
                    entry.Entity.Versiones.Add(new VersionSeccion { Seccion = seccion });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        InicializarVersiones();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        InicializarVersiones();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
