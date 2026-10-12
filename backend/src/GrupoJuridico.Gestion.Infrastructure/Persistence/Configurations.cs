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

internal class EstadoClienteConfig : IEntityTypeConfiguration<EstadoCliente>
{
    public void Configure(EntityTypeBuilder<EstadoCliente> b) => CatalogoConfig.Configurar(b, "EstadosCliente");
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
        b.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId).OnDelete(DeleteBehavior.SetNull);

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
    }
}

internal class VentaConfig : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> b)
    {
        b.ToTable("Ventas");
        b.Property(x => x.Monto).HasPrecision(14, 2);
        b.Property(x => x.Notas).HasMaxLength(4000);
        b.HasOne(x => x.ProcedenciaVenta).WithMany().HasForeignKey(x => x.ProcedenciaVentaId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.MetodoVenta).WithMany().HasForeignKey(x => x.MetodoVentaId).OnDelete(DeleteBehavior.SetNull);
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
        b.Property(x => x.Parentesco).HasMaxLength(60);
        b.Property(x => x.Telefono).HasMaxLength(40);
        b.Property(x => x.Whatsapp).HasMaxLength(40);
        b.Property(x => x.CorreoElectronico).HasMaxLength(200);
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
