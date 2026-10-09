using GrupoJuridico.Gestion.Domain.Common;
using GrupoJuridico.Gestion.Domain.Enums;

namespace GrupoJuridico.Gestion.Domain.Entities;

/// <summary>
/// Persona: nace como prospecto (ClienteId nulo) y pasa a cliente oficial cuando se le asigna un Cliente.
/// </summary>
public class Persona : BaseEntity
{
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

    public Venta? Venta => Ventas.OrderBy(v => v.Id).FirstOrDefault();
    public Prima? Prima => Venta?.Prima;
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

    public int EstadoClienteId { get; set; }
    public EstadoCliente? EstadoCliente { get; set; }

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
    public int PersonaId { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Parentesco { get; set; } = string.Empty;
    public string Whatsapp { get; set; } = string.Empty;
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
