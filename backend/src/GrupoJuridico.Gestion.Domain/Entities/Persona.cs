using GrupoJuridico.Gestion.Domain.Common;
using GrupoJuridico.Gestion.Domain.Enums;

namespace GrupoJuridico.Gestion.Domain.Entities;

/// <summary>
/// Persona: nace como prospecto (ClienteId nulo) y pasa a cliente oficial cuando se le asigna un Cliente.
/// </summary>
public class Persona : BaseEntity
{
    public const int MaxTelefonos = 2;
    public const int MaxWhatsApp = 2;
    public const int MaxCorreos = 3;
    public const int MaxFamiliares = 4;
    public const int MaxFincas = 5;
    /// <summary>Largo máximo de Nombres y de Apellidos (regla de negocio; la columna admite más).</summary>
    public const int MaxNombre = 20;
    /// <summary>Largo máximo de la cédula (solo dígitos).</summary>
    public const int MaxCedula = 15;
    /// <summary>Largo máximo de un correo electrónico (principal, lista de correos y familiares).</summary>
    public const int MaxCorreo = 100;

    public List<VersionSeccion> Versiones { get; set; } = new();
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string Expediente { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public DateOnly FechaIngreso { get; set; }

    /// <summary>"Vendido por" (antes "localizado por"): FK a Vendedores.</summary>
    public int? VendedorId { get; set; }
    public Vendedor? Vendedor { get; set; }

    public DateTime FechaCreacion { get; set; }

    public List<Venta> Ventas { get; set; } = new();
    public List<Numero> Numeros { get; set; } = new();
    public List<CorreoElectronico> Correos { get; set; } = new();
    public List<Finca> Fincas { get; set; } = new();
    public List<PersonaFamiliar> Familiares { get; set; } = new();
    public List<Comentario> Comentarios { get; set; } = new();

    public bool EsCliente => ClienteId.HasValue;
    public string NombreCompleto => (Nombres + " " + Apellidos).Trim();

    /// <summary>La venta de la persona. Hay una sola: la base lo garantiza con un índice único en Ventas.PersonaId.</summary>
    public Venta? Venta => Ventas.OrderBy(v => v.Id).FirstOrDefault();
    public Prima? Prima => Venta?.Prima;
    /// <summary>La primera finca: la que muestran las listas y la que edita el prospecto.</summary>
    public Finca? Finca => Fincas.OrderBy(f => f.Id).FirstOrDefault();

    public Numero? NumeroPrincipal(TipoNumero tipo)
    {
        var delTipo = Numeros.Where(n => n.Tipo == tipo).OrderBy(n => n.Id).ToList();
        return delTipo.FirstOrDefault(n => n.Principal) ?? delTipo.FirstOrDefault();
    }

    public CorreoElectronico? CorreoPrincipal =>
        Correos.OrderBy(c => c.Id).FirstOrDefault(c => c.Principal) ?? Correos.OrderBy(c => c.Id).FirstOrDefault();
}

public class Cliente : BaseEntity
{
    public string Notas { get; set; } = string.Empty;

    public int OrigenClienteId { get; set; }
    public OrigenCliente? OrigenCliente { get; set; }

    /// <summary>
    /// Estado que calcula el sistema con el plan de pago: Al Día, Moroso o Estrella
    /// (<see cref="ReglasCobro.EstadoCalculado"/>). Siempre se guarda, aunque haya un estado manual. No se asigna a mano.
    /// </summary>
    public int EstadoCalculadoId { get; set; }
    public EstadoCliente? EstadoCalculado { get; set; }

    /// <summary>
    /// Estado elegido a mano (Terminado, Abandonado, Sin Expediente u otro creado en Configuración).
    /// Nulo = automático: se muestra el calculado.
    /// </summary>
    public int? EstadoManualId { get; set; }
    public EstadoCliente? EstadoManual { get; set; }

    /// <summary>Estado que se muestra y por el que se filtra: el manual si hay uno; si no, el calculado. No se guarda.</summary>
    public int EstadoId => EstadoManualId ?? EstadoCalculadoId;
    /// <summary>Catálogo de <see cref="EstadoId"/> (requiere cargar <see cref="EstadoCalculado"/> y <see cref="EstadoManual"/>).</summary>
    public EstadoCliente? Estado => EstadoManualId != null ? EstadoManual : EstadoCalculado;

    public Persona? Persona { get; set; }
}

public class Venta : BaseEntity
{
    public int PersonaId { get; set; }
    public Persona? Persona { get; set; }

    public int? ProcedenciaVentaId { get; set; }
    public ProcedenciaVenta? ProcedenciaVenta { get; set; }

    public int? MetodoVentaId { get; set; }
    public MetodoVenta? MetodoVenta { get; set; }

    public decimal Monto { get; set; }
    public string Notas { get; set; } = string.Empty;

    public Prima? Prima { get; set; }
}

/// <summary>Prima de la venta. El saldo y el estado se derivan del monto y lo cancelado.</summary>
public class Prima : BaseEntity
{
    public int VentaId { get; set; }
    public Venta? Venta { get; set; }

    public decimal Monto { get; set; }
    public decimal MontoCancelado { get; set; }
    public decimal SaldoPendiente { get; set; }
    public DateOnly? FechaEstimadaPago { get; set; }
    public DateOnly? FechaPago { get; set; }

    public int EstadoPrimaId { get; set; } = EstadoPrima.Pendiente;
    public EstadoPrima? EstadoPrima { get; set; }

    public static int EstadoPara(decimal monto, decimal cancelado) =>
        cancelado <= 0 ? Entities.EstadoPrima.Pendiente
        : cancelado < monto ? Entities.EstadoPrima.Incompleta
        : Entities.EstadoPrima.Pagada;

    /// <summary>Recalcula saldo pendiente y estado a partir de los montos.</summary>
    public void Recalcular()
    {
        SaldoPendiente = Math.Max(0, Monto - MontoCancelado);
        EstadoPrimaId = EstadoPara(Monto, MontoCancelado);
    }
}

public class Numero : BaseEntity
{
    public int PersonaId { get; set; }
    public int PaisId { get; set; }
    public Pais? Pais { get; set; }
    public string Valor { get; set; } = string.Empty;
    public TipoNumero Tipo { get; set; }
    public bool Principal { get; set; }
}

public class CorreoElectronico : BaseEntity
{
    public int PersonaId { get; set; }
    public string Correo { get; set; } = string.Empty;
    public bool Principal { get; set; }
}

public class Finca : BaseEntity
{
    public int PersonaId { get; set; }
    public string Numero { get; set; } = string.Empty;
}

public class PersonaFamiliar : BaseEntity
{
    /// <summary>Largo máximo del nombre completo (nombre y apellidos) del familiar.</summary>
    public const int MaxNombreCompleto = 60;

    public int PersonaId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public int? ParentescoId { get; set; }
    public Parentesco? Parentesco { get; set; }
    public int? TelefonoPaisId { get; set; }
    public Pais? TelefonoPais { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string CorreoElectronico { get; set; } = string.Empty;
}

public class Comentario : BaseEntity
{
    public int PersonaId { get; set; }
    public int UsuarioId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
}
