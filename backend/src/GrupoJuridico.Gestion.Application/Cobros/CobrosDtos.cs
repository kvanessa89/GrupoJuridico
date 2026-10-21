namespace GrupoJuridico.Gestion.Application.Cobros;

/// <summary>Vista de la sección Cobros: cada tarjeta filtra la tabla.</summary>
public static class VistasCobros
{
    public const string Morosos = "morosos";
    public const string Vencido = "vencido";
    public const string Recordatorios = "recordatorios";
    public const string Promesas = "promesas";
    /// <summary>Candidato a cobro judicial: moroso con el plan vencido y saldo pendiente.</summary>
    public const string CobroJudicial = "cobro-judicial";

    public static readonly string[] Todas = { Morosos, Vencido, Recordatorios, Promesas, CobroJudicial };
}

public record CobrosQuery
{
    public string? Vista { get; init; }
    public string? Q { get; init; }
    public string? Orden { get; init; }
    public bool Asc { get; init; } = true;
    public int Pagina { get; init; } = 1;
    public int PorPagina { get; init; } = 10;
}

public record ProximaCuotaDto(DateOnly Fecha, decimal Saldo);

public record PromesaResumenDto(int EstadoId, DateOnly Fecha);

public record RecordatorioDto(int NumeroCuota, decimal Saldo, DateOnly Fecha);

public record CobroFilaDto(
    int Id,
    string Expediente,
    string NombreCompleto,
    string? Telefono,
    string? Whatsapp,
    int EstadoClienteId,
    int EstadoClienteCalculadoId,
    /// <summary>Nombre del estado que se muestra: el calculado si el cliente está Activo; si no, el elegido.</summary>
    string EstadoCliente,
    bool TienePlan,
    int CantidadCuotas,
    int CuotasVencidas,
    decimal MontoVencido,
    int Atraso,
    ProximaCuotaDto? Proxima,
    PromesaResumenDto? Promesa,
    RecordatorioDto? Recordatorio);

public record CobrosTotalesDto(
    int Morosos,
    decimal MontoVencido,
    int Recordatorios,
    int PromesasPendientes,
    int CobroJudicial,
    IReadOnlyList<DateOnly> FechasARecordar);

public record CobrosListaDto(
    IReadOnlyList<CobroFilaDto> Filas, int Total, int Pagina, int PorPagina,
    CobrosTotalesDto Totales, DateOnly Hoy);

// ---------- Plan de pago ----------

/// <summary>Comprobante de un pago. <c>Id</c> es el del archivo del expediente (se descarga con GET api/archivos/{id}).</summary>
public record ComprobanteDto(int Id, string Nombre, bool Disponible);

public record PagoDto(int Id, decimal MontoPago, DateOnly FechaPago, IReadOnlyList<ComprobanteDto> Comprobantes);

public record PromesaDto(int Id, DateOnly FechaPromesa, decimal MontoPrometido, decimal MontoPagado, int EstadoId);

/// <summary>
/// Cuota con sus montos de hoy. <c>Mora</c> es el interés que se cobra (0 si se acordó no cobrarlo);
/// <c>MoraPosible</c> es el que correspondería aunque haya acuerdo (para la casilla "Cobrar interés").
/// </summary>
public record CuotaDto(
    int Id,
    int NumeroCuota,
    DateOnly FechaVencimiento,
    decimal MontoCuota,
    decimal Mora,
    decimal MoraPosible,
    bool? SinMora,
    decimal Total,
    decimal Pagado,
    decimal Saldo,
    int EstadoPagoId,
    int EstadoVencimientoId,
    IReadOnlyList<PagoDto> Pagos,
    IReadOnlyList<PromesaDto> Promesas);

public record PlanDetalleDto(
    int Id,
    decimal MontoTotal,
    decimal MontoCuota,
    int CantidadCuotas,
    DateOnly FechaInicio,
    int DiaPago,
    decimal Saldo,
    decimal PagadoTotal,
    int CuotasPagadas,
    int CuotasVencidas,
    decimal MontoVencido,
    int Atraso,
    int PromesasPendientes,
    int PromesasCumplidas,
    int PromesasIncumplidas,
    IReadOnlyList<CuotaDto> Cuotas);

/// <summary>
/// Plan de pago de una persona, con lo necesario para el encabezado de la pantalla del plan.
/// <c>MontoSugerido</c> es el monto de la venta menos la prima, que se propone al crear el plan.
/// </summary>
public record PlanPersonaDto(
    int PersonaId,
    bool EsCliente,
    string Expediente,
    string Nombres,
    string Apellidos,
    DateOnly FechaIngreso,
    int? EstadoClienteId,
    int? EstadoClienteCalculadoId,
    decimal InteresMora,
    decimal MontoSugerido,
    bool PuedeGestionar,
    DateOnly Hoy,
    PlanDetalleDto? Plan);

// ---------- Comandos ----------

public record CrearPlanRequest(decimal MontoTotal, int CantidadCuotas, DateOnly? FechaInicio, int DiaPago);

/// <summary>Archivo recibido en un formulario (comprobante de pago).</summary>
public record ArchivoSubido(string Nombre, string TipoContenido, long Tamano, Stream Contenido);

public record RegistrarPagoRequest(decimal MontoPago, DateOnly? FechaPago, bool CobrarMora);

public record EditarPagoRequest(decimal MontoPago, DateOnly? FechaPago);

public record PromesaRequest(DateOnly? FechaPromesa, decimal MontoPrometido);
