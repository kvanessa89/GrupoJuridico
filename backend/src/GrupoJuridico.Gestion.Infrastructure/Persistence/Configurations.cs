using GrupoJuridico.Gestion.Domain.Common;
using GrupoJuridico.Gestion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrupoJuridico.Gestion.Infrastructure.Persistence;

internal static class CatalogoConfig
{
    public static void Configurar<T>(EntityTypeBuilder<T> b, string tabla) where T : Catalogo
    {
        b.ToTable(tabla);
        b.HasKey(x => x.Id);
        b.Property(x => x.Codigo).HasMaxLength(20);
        b.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
    }
}

internal class VendedorConfig : IEntityTypeConfiguration<Vendedor>
{
    public void Configure(EntityTypeBuilder<Vendedor> b)
    {
        CatalogoConfig.Configurar(b, "Vendedores");
        b.Property(x => x.Apellidos).HasMaxLength(150);
        b.Ignore(x => x.Etiqueta);
    }
}

internal class ProcedenciaVentaConfig : IEntityTypeConfiguration<ProcedenciaVenta>
{
    public void Configure(EntityTypeBuilder<ProcedenciaVenta> b) => CatalogoConfig.Configurar(b, "ProcedenciasVenta");
}

internal class MetodoVentaConfig : IEntityTypeConfiguration<MetodoVenta>
{
    public void Configure(EntityTypeBuilder<MetodoVenta> b) => CatalogoConfig.Configurar(b, "MetodosVenta");
}

internal class EstadoPrimaConfig : IEntityTypeConfiguration<EstadoPrima>
{
    public void Configure(EntityTypeBuilder<EstadoPrima> b)
    {
        CatalogoConfig.Configurar(b, "EstadosPrima");
        b.Property(x => x.Id).ValueGeneratedNever();
    }
}

internal class OrigenClienteConfig : IEntityTypeConfiguration<OrigenCliente>
{
    public void Configure(EntityTypeBuilder<OrigenCliente> b)
    {
        CatalogoConfig.Configurar(b, "OrigenesCliente");
        b.Ignore(x => x.Etiqueta);
    }
}

internal class PaisConfig : IEntityTypeConfiguration<Pais>
{
    public void Configure(EntityTypeBuilder<Pais> b) => CatalogoConfig.Configurar(b, "Paises");
}

internal class ParentescoConfig : IEntityTypeConfiguration<Parentesco>
{
    public void Configure(EntityTypeBuilder<Parentesco> b) => CatalogoConfig.Configurar(b, "Parentescos");
}

internal class EstadoClienteConfig : IEntityTypeConfiguration<EstadoCliente>
{
    public void Configure(EntityTypeBuilder<EstadoCliente> b) => CatalogoConfig.Configurar(b, "EstadosCliente");
}

internal class EstadoClienteCalculadoConfig : IEntityTypeConfiguration<EstadoClienteCalculado>
{
    public void Configure(EntityTypeBuilder<EstadoClienteCalculado> b)
    {
        CatalogoConfig.Configurar(b, "EstadosClienteCalculado");
        b.Property(x => x.Id).ValueGeneratedNever();
    }
}

internal class ConfiguracionSistemaConfig : IEntityTypeConfiguration<ConfiguracionSistema>
{
    public void Configure(EntityTypeBuilder<ConfiguracionSistema> b)
    {
        b.ToTable("ConfiguracionSistema");
        b.HasKey(x => x.Clave);
        b.Property(x => x.Clave).HasMaxLength(60);
        b.Property(x => x.Valor).HasMaxLength(200);
    }
}

internal class PersonaConfig : IEntityTypeConfiguration<Persona>
{
    public void Configure(EntityTypeBuilder<Persona> b)
    {
        b.ToTable("Personas");
        b.Property(x => x.Nombres).HasMaxLength(150);
        b.Property(x => x.Apellidos).HasMaxLength(150);
        b.Property(x => x.Cedula).HasMaxLength(30);
        b.Property(x => x.Expediente).HasMaxLength(40);
        b.HasIndex(x => x.Cedula);
        b.HasIndex(x => x.Expediente);
        b.HasIndex(x => x.ClienteId).IsUnique();

        b.HasOne(x => x.Cliente).WithOne(c => c.Persona).HasForeignKey<Persona>(x => x.ClienteId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId).OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Ventas).WithOne(v => v.Persona).HasForeignKey(v => v.PersonaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Numeros).WithOne().HasForeignKey(n => n.PersonaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Correos).WithOne().HasForeignKey(c => c.PersonaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Fincas).WithOne().HasForeignKey(f => f.PersonaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Familiares).WithOne().HasForeignKey(f => f.PersonaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Comentarios).WithOne().HasForeignKey(c => c.PersonaId).OnDelete(DeleteBehavior.Cascade);

        b.Ignore(x => x.EsCliente);
        b.Ignore(x => x.NombreCompleto);
        b.Ignore(x => x.Venta);
        b.Ignore(x => x.Prima);
        b.Ignore(x => x.Finca);
        b.Ignore(x => x.CorreoPrincipal);
    }
}

internal class ClienteConfig : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("Clientes");
        b.Property(x => x.Notas).HasMaxLength(4000);
        b.HasOne(x => x.OrigenCliente).WithMany().HasForeignKey(x => x.OrigenClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.EstadoCliente).WithMany().HasForeignKey(x => x.EstadoClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.EstadoClienteCalculado).WithMany().HasForeignKey(x => x.EstadoClienteCalculadoId).OnDelete(DeleteBehavior.Restrict);
        b.Ignore(x => x.EsAutomatico);
        b.Ignore(x => x.NombreEstado);
    }
}

internal class VentaConfig : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> b)
    {
        b.ToTable("Ventas");
        // Una persona tiene una sola venta: se crea con el prospecto y la app siempre usa esa.
        b.HasIndex(x => x.PersonaId).IsUnique();
        b.Property(x => x.Monto).HasPrecision(14, 2);
        b.Property(x => x.Notas).HasMaxLength(4000);
        b.HasOne(x => x.ProcedenciaVenta).WithMany().HasForeignKey(x => x.ProcedenciaVentaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.MetodoVenta).WithMany().HasForeignKey(x => x.MetodoVentaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Prima).WithOne(p => p.Venta).HasForeignKey<Prima>(p => p.VentaId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class PrimaConfig : IEntityTypeConfiguration<Prima>
{
    public void Configure(EntityTypeBuilder<Prima> b)
    {
        b.ToTable("Primas");
        b.Property(x => x.Monto).HasPrecision(14, 2);
        b.Property(x => x.MontoCancelado).HasPrecision(14, 2);
        b.Property(x => x.SaldoPendiente).HasPrecision(14, 2);
        b.HasIndex(x => x.VentaId).IsUnique();
        b.HasIndex(x => x.FechaEstimadaPago);
        b.HasOne(x => x.EstadoPrima).WithMany().HasForeignKey(x => x.EstadoPrimaId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class NumeroConfig : IEntityTypeConfiguration<Numero>
{
    public void Configure(EntityTypeBuilder<Numero> b)
    {
        b.ToTable("Numeros");
        b.Property(x => x.Valor).HasColumnName("Numero").HasMaxLength(40);
        b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
        // Un país con teléfonos no se puede borrar (CatalogosService lo valida antes).
        b.HasOne(x => x.Pais).WithMany().HasForeignKey(x => x.PaisId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class CorreoConfig : IEntityTypeConfiguration<CorreoElectronico>
{
    public void Configure(EntityTypeBuilder<CorreoElectronico> b)
    {
        b.ToTable("CorreosElectronicos");
        b.Property(x => x.Correo).HasColumnName("CorreoElectronico").HasMaxLength(200);
    }
}

internal class FincaConfig : IEntityTypeConfiguration<Finca>
{
    public void Configure(EntityTypeBuilder<Finca> b)
    {
        b.ToTable("Fincas");
        b.Property(x => x.Numero).HasMaxLength(40);
        b.HasIndex(x => x.Numero);
    }
}

internal class FamiliarConfig : IEntityTypeConfiguration<PersonaFamiliar>
{
    public void Configure(EntityTypeBuilder<PersonaFamiliar> b)
    {
        b.ToTable("PersonaFamiliares");
        b.Property(x => x.NombreCompleto).HasMaxLength(200);
        b.HasOne(x => x.Parentesco).WithMany().HasForeignKey(x => x.ParentescoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Telefono).HasMaxLength(40);
        b.Property(x => x.CorreoElectronico).HasMaxLength(200);
        b.HasOne(x => x.TelefonoPais).WithMany().HasForeignKey(x => x.TelefonoPaisId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class ComentarioConfig : IEntityTypeConfiguration<Comentario>
{
    public void Configure(EntityTypeBuilder<Comentario> b)
    {
        b.ToTable("Comentarios");
        b.Property(x => x.Texto).HasMaxLength(4000).IsRequired();
        // Sin FK a AspNetUsers: al eliminar un usuario sus comentarios quedan como "Usuario eliminado".
        b.HasIndex(x => x.UsuarioId);
        b.HasIndex(x => new { x.PersonaId, x.Fecha });
    }
}


internal class VersionSeccionConfig : IEntityTypeConfiguration<VersionSeccion>
{
    public void Configure(EntityTypeBuilder<VersionSeccion> b)
    {
        b.ToTable("VersionesSeccion");
        b.HasKey(x => new { x.PersonaId, x.Seccion });
        b.Property(x => x.Seccion).HasMaxLength(20);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne<Persona>().WithMany(p => p.Versiones).HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class EstadoCuotaPagoConfig : IEntityTypeConfiguration<EstadoCuotaPago>
{
    public void Configure(EntityTypeBuilder<EstadoCuotaPago> b)
    {
        CatalogoConfig.Configurar(b, "EstadosCuotaPago");
        b.Property(x => x.Id).ValueGeneratedNever();
    }
}

internal class EstadoCuotaVencimientoConfig : IEntityTypeConfiguration<EstadoCuotaVencimiento>
{
    public void Configure(EntityTypeBuilder<EstadoCuotaVencimiento> b)
    {
        CatalogoConfig.Configurar(b, "EstadosCuotaVencimiento");
        b.Property(x => x.Id).ValueGeneratedNever();
    }
}

internal class EstadoPromesaConfig : IEntityTypeConfiguration<EstadoPromesa>
{
    public void Configure(EntityTypeBuilder<EstadoPromesa> b)
    {
        CatalogoConfig.Configurar(b, "EstadosPromesa");
        b.Property(x => x.Id).ValueGeneratedNever();
    }
}

internal class PlanPagoConfig : IEntityTypeConfiguration<PlanPago>
{
    public void Configure(EntityTypeBuilder<PlanPago> b)
    {
        b.ToTable("PlanesPago");
        b.Property(x => x.MontoTotal).HasPrecision(14, 2);
        b.Property(x => x.MontoCuota).HasPrecision(14, 2);
        // Un cliente tiene un solo plan; al eliminar el cliente se elimina su plan con cuotas, pagos y promesas.
        b.HasIndex(x => x.ClienteId).IsUnique();
        b.HasOne(x => x.Cliente).WithOne().HasForeignKey<PlanPago>(x => x.ClienteId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Cuotas).WithOne(c => c.PlanPago).HasForeignKey(c => c.PlanPagoId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class CuotaConfig : IEntityTypeConfiguration<Cuota>
{
    public void Configure(EntityTypeBuilder<Cuota> b)
    {
        b.ToTable("Cuotas");
        b.Property(x => x.MontoCuota).HasPrecision(14, 2);
        b.HasIndex(x => new { x.PlanPagoId, x.NumeroCuota }).IsUnique();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.FechaVencimiento);
        // Cuotas sin pagar que todavía figuran al día: así se encuentran rápido las que vencieron (ActualizadorCobros).
        b.HasIndex(x => x.FechaVencimiento, "IX_Cuotas_PendientesAlDia")
            .HasFilter("\"EstadoCuotaVencimientoId\" = 1 AND \"EstadoCuotaPagoId\" <> 3");
        b.HasOne(x => x.EstadoCuotaPago).WithMany().HasForeignKey(x => x.EstadoCuotaPagoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.EstadoCuotaVencimiento).WithMany().HasForeignKey(x => x.EstadoCuotaVencimientoId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Pagos).WithOne(p => p.Cuota).HasForeignKey(p => p.CuotaId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Promesas).WithOne(p => p.Cuota).HasForeignKey(p => p.CuotaId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class PagoConfig : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> b)
    {
        b.ToTable("Pagos");
        b.Property(x => x.MontoPago).HasPrecision(14, 2);
        b.HasMany(x => x.Comprobantes).WithOne(a => a.Pago).HasForeignKey(a => a.PagoId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class PromesaPagoConfig : IEntityTypeConfiguration<PromesaPago>
{
    public void Configure(EntityTypeBuilder<PromesaPago> b)
    {
        b.ToTable("PromesasPago");
        // Promesas pendientes, para detectar las que pasaron su fecha (ActualizadorCobros).
        b.HasIndex(x => x.FechaPromesa, "IX_PromesasPago_Pendientes")
            .HasFilter("\"EstadoPromesaId\" = 1");
        b.Property(x => x.MontoPrometido).HasPrecision(14, 2);
        b.Property(x => x.MontoPagado).HasPrecision(14, 2);
        b.HasOne(x => x.EstadoPromesa).WithMany().HasForeignKey(x => x.EstadoPromesaId).OnDelete(DeleteBehavior.Restrict);
    }
}

// ---------- Archivos del expediente ----------

internal class TipoDocumentoConfig : IEntityTypeConfiguration<TipoDocumento>
{
    public void Configure(EntityTypeBuilder<TipoDocumento> b)
    {
        CatalogoConfig.Configurar(b, "TiposDocumento");
        b.HasMany(x => x.Permisos).WithOne(p => p.TipoDocumento).HasForeignKey(p => p.TipoDocumentoId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class PermisoDocumentoConfig : IEntityTypeConfiguration<PermisoDocumento>
{
    public void Configure(EntityTypeBuilder<PermisoDocumento> b)
    {
        b.ToTable("PermisosDocumento");
        b.Property(x => x.Rol).HasMaxLength(50).IsRequired();
        b.HasIndex(x => new { x.TipoDocumentoId, x.Rol }).IsUnique();
    }
}

internal class ArchivoPersonaConfig : IEntityTypeConfiguration<ArchivoPersona>
{
    public void Configure(EntityTypeBuilder<ArchivoPersona> b)
    {
        b.ToTable("ArchivosPersona");
        b.Property(x => x.Nombre).HasMaxLength(255).IsRequired();
        b.Property(x => x.TipoContenido).HasMaxLength(100);
        b.Property(x => x.Clave).HasMaxLength(200).IsRequired();
        b.HasOne<Persona>().WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Cascade);
        // Un tipo con archivos no se puede borrar (CatalogosService lo valida antes).
        b.HasOne(x => x.TipoDocumento).WithMany().HasForeignKey(x => x.TipoDocumentoId).OnDelete(DeleteBehavior.Restrict);
    }
}
