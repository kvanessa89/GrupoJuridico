using GrupoJuridico.Gestion.Application.Usuarios;
using GrupoJuridico.Gestion.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Persona> Personas { get; }
    DbSet<Cliente> Clientes { get; }
    DbSet<Venta> Ventas { get; }
    DbSet<Prima> Primas { get; }
    DbSet<Numero> Numeros { get; }
    DbSet<CorreoElectronico> Correos { get; }
    DbSet<Finca> Fincas { get; }
    DbSet<PersonaFamiliar> Familiares { get; }
    DbSet<Comentario> Comentarios { get; }

    DbSet<Vendedor> Vendedores { get; }
    DbSet<ProcedenciaVenta> ProcedenciasVenta { get; }
    DbSet<MetodoVenta> MetodosVenta { get; }
    DbSet<EstadoPrima> EstadosPrima { get; }
    DbSet<OrigenCliente> OrigenesCliente { get; }
    DbSet<EstadoCliente> EstadosCliente { get; }
    DbSet<Pais> Paises { get; }
    DbSet<Parentesco> Parentescos { get; }
    DbSet<ConfiguracionSistema> Configuracion { get; }

    DbSet<PlanPago> PlanesPago { get; }
    DbSet<Cuota> Cuotas { get; }
    DbSet<Pago> Pagos { get; }
    DbSet<PromesaPago> PromesasPago { get; }
    DbSet<TipoDocumento> TiposDocumento { get; }
    DbSet<PermisoDocumento> PermisosDocumento { get; }
    DbSet<ArchivoPersona> ArchivosPersona { get; }
    DbSet<EstadoCuotaPago> EstadosCuotaPago { get; }
    DbSet<EstadoCuotaVencimiento> EstadosCuotaVencimiento { get; }
    DbSet<EstadoPromesa> EstadosPromesa { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Olvida las entidades cargadas y sus cambios sin guardar (después de perder una carrera de concurrencia).</summary>
    void DescartarCambios();
}

/// <summary>Almacenamiento de archivos subidos (comprobantes de pago). Las claves las genera el almacenamiento.</summary>
public interface IAlmacenArchivos
{
    /// <summary>Guarda el contenido y devuelve la clave para recuperarlo.</summary>
    Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct = default);
    /// <summary>Abre el archivo para lectura, o null si ya no existe.</summary>
    Task<Stream?> AbrirAsync(string clave, CancellationToken ct = default);
    Task EliminarAsync(string clave, CancellationToken ct = default);
}

/// <summary>Usuario autenticado de la petición actual.</summary>
public interface IUsuarioActual
{
    int? Id { get; }
    string? Rol { get; }
    bool EsAdministrador { get; }
}

/// <summary>Fecha "de negocio" en la zona horaria de Costa Rica.</summary>
public interface IFechaActual
{
    DateOnly Hoy { get; }
    DateTime AhoraUtc { get; }
}

public interface ITokenService
{
    Task<string> GenerarAsync(UsuarioDto usuario);
}

/// <summary>Usuarios del sistema sobre ASP.NET Core Identity.</summary>
public interface IIdentityService
{
    Task<UsuarioDto?> ValidarCredencialesAsync(string usuario, string contrasena);
    Task<UsuarioDto?> ObtenerAsync(int id);
    Task<IReadOnlyList<UsuarioDto>> ListarAsync();
    Task<IReadOnlyDictionary<int, UsuarioDto>> MapaAsync(IEnumerable<int> ids);
    Task<UsuarioDto> CrearAsync(GuardarUsuarioRequest request);
    Task<UsuarioDto> ActualizarAsync(int id, GuardarUsuarioRequest request);
    Task EliminarAsync(int id);
    IReadOnlyList<RolDto> Roles();
}
