using GrupoJuridico.Gestion.Domain.Common;

namespace GrupoJuridico.Gestion.Domain.Entities;

/// <summary>Estado de pago de la cuota. Ids fijos porque se calcula de los pagos; el nombre es editable.</summary>
public class EstadoCuotaPago : Catalogo
{
    public const int Pendiente = 1;
    public const int Parcial = 2;
    public const int Pagada = 3;
}

/// <summary>Estado de vencimiento de la cuota: Al día o Vencido. Ids fijos.</summary>
public class EstadoCuotaVencimiento : Catalogo
{
    public const int AlDia = 1;
    public const int Vencido = 2;
}

/// <summary>Estado de la promesa de pago: Pendiente, Cumplida o Incumplida. Ids fijos.</summary>
public class EstadoPromesa : Catalogo
{
    public const int Pendiente = 1;
    public const int Cumplida = 2;
    public const int Incumplida = 3;
}

/// <summary>Plan de pago de un cliente oficial. Al crearlo se generan todas sus cuotas.</summary>
public class PlanPago : BaseEntity
{
    public const int MaxCuotas = 120;

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public decimal MontoTotal { get; set; }
    /// <summary>Monto mensual. La última cuota ajusta la diferencia por redondeo.</summary>
    public decimal MontoCuota { get; set; }
    public int CantidadCuotas { get; set; }
    /// <summary>Fecha de inicio de pago.</summary>
    public DateOnly FechaInicio { get; set; }
    /// <summary>Día de pago de cada mes (1-31; en meses más cortos se usa el último día).</summary>
    public int DiaPago { get; set; }

    public List<Cuota> Cuotas { get; set; } = new();
}

public class Cuota : BaseEntity
{
    public int PlanPagoId { get; set; }
    public PlanPago? PlanPago { get; set; }

    public int NumeroCuota { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    /// <summary>Monto base, sin interés de morosidad.</summary>
    public decimal MontoCuota { get; set; }

    public int EstadoCuotaPagoId { get; set; } = EstadoCuotaPago.Pendiente;
    public EstadoCuotaPago? EstadoCuotaPago { get; set; }
    public int EstadoCuotaVencimientoId { get; set; } = EstadoCuotaVencimiento.AlDia;
    public EstadoCuotaVencimiento? EstadoCuotaVencimiento { get; set; }

    /// <summary>
    /// Acuerdo sobre el interés de morosidad: null mientras no se registre un pago con mora,
    /// false si se cobró el interés y true si se acordó no cobrarlo.
    /// </summary>
    public bool? SinMora { get; set; }

    /// <summary>Cambia con cada pago o promesa; evita que dos registros simultáneos superen el saldo.</summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    public List<Pago> Pagos { get; set; } = new();
    public List<PromesaPago> Promesas { get; set; } = new();
}

public class Pago : BaseEntity
{
    public int CuotaId { get; set; }
    public Cuota? Cuota { get; set; }
    public decimal MontoPago { get; set; }
    public DateOnly FechaPago { get; set; }
    public List<ArchivoPago> Archivos { get; set; } = new();
}

/// <summary>Comprobante de un pago. El archivo se guarda en el almacenamiento con una clave interna.</summary>
public class ArchivoPago : BaseEntity
{
    public int PagoId { get; set; }
    /// <summary>Nombre original del archivo (solo para mostrar y descargar).</summary>
    public string Nombre { get; set; } = string.Empty;
    public string TipoContenido { get; set; } = string.Empty;
    public long Tamano { get; set; }
    public DateOnly FechaCarga { get; set; }
    /// <summary>Clave del archivo en el almacenamiento; vacía en los datos de demostración.</summary>
    public string Clave { get; set; } = string.Empty;
}

/// <summary>Acuerdo del cliente para pagar una cuota en una fecha. Los pagos posteriores se suman a la promesa pendiente.</summary>
public class PromesaPago : BaseEntity
{
    public int CuotaId { get; set; }
    public Cuota? Cuota { get; set; }
    public DateOnly FechaPromesa { get; set; }
    public decimal MontoPrometido { get; set; }
    public decimal MontoPagado { get; set; }
    public int EstadoPromesaId { get; set; } = EstadoPromesa.Pendiente;
    public EstadoPromesa? EstadoPromesa { get; set; }
}
