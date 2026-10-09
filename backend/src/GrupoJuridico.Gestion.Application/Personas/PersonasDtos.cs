namespace GrupoJuridico.Gestion.Application.Personas;

// ---------- Listas ----------

public record ListaQuery
{
    public string? Q { get; init; }
    public int? VendedorId { get; init; }
    public int? EstadoPrimaId { get; init; }
    public int? ProcedenciaId { get; init; }
    /// <summary>Mes de la fecha estimada de pago, formato yyyy-MM (solo Ventas).</summary>
    public string? Mes { get; init; }
    /// <summary>Año de ingreso (solo Clientes).</summary>
    public int? Anio { get; init; }
    public int? OrigenId { get; init; }
    public int? EstadoClienteId { get; init; }
    public string? Orden { get; init; }
    public bool Asc { get; init; } = true;
    public int Pagina { get; init; } = 1;
    public int PorPagina { get; init; } = 10;
}

public record VentaFilaDto(
    int Id,
    string Expediente,
    string NombreCompleto,
    string? Telefono,
    string? Whatsapp,
    string? Procedencia,
    string? Metodo,
    string? Vendedor,
    decimal MontoPrima,
    decimal SaldoPendiente,
    DateOnly? FechaEstimadaPago,
    int EstadoPrimaId,
    string EstadoPrima,
    bool EsCliente);

public record VentasTotalesDto(decimal? MontoVentas, decimal MontoPrimas, decimal PrimasPagadas, decimal PrimasPendientes);

public record VentasListaDto(
    IReadOnlyList<VentaFilaDto> Filas, int Total, int Pagina, int PorPagina,
    VentasTotalesDto Totales, IReadOnlyList<string> MesesPago);

public record ClienteFilaDto(
    int Id,
    string Expediente,
    string NombreCompleto,
    string? Telefono,
    string? Correo,
    string? Finca,
    DateOnly FechaIngreso,
    int? EstadoPrimaId,
    string? EstadoPrima,
    int EstadoClienteId,
    string EstadoCliente,
    string? Origen);

public record ClientesListaDto(
    IReadOnlyList<ClienteFilaDto> Filas, int Total, int Pagina, int PorPagina,
    IReadOnlyList<int> AniosIngreso);

// ---------- Ficha ----------

public record VentaDto(int Id, int? ProcedenciaVentaId, int? MetodoVentaId, decimal Monto, string Notas);

public record PrimaDto(int Id, decimal Monto, decimal MontoCancelado, decimal SaldoPendiente,
    DateOnly? FechaEstimadaPago, DateOnly? FechaPago, int EstadoPrimaId);

public record ClienteDto(int Id, int OrigenClienteId, int EstadoClienteId, string Notas);

public record NumeroDto(int Id, string Numero, string Tipo, bool Principal);

public record CorreoDto(int Id, string Correo, bool Principal);

public record FamiliarDto(int Id, string NombreCompleto, string Parentesco, string Telefono, string CorreoElectronico, string Whatsapp);

public record ComentarioDto(int Id, int UsuarioId, string Autor, string? Rol, string Texto, DateTime Fecha, bool PuedeEliminar);

public record PersonaDetalleDto(
    int Id,
    bool EsCliente,
    string Expediente,
    string Nombres,
    string Apellidos,
    string Cedula,
    string Finca,
    DateOnly FechaIngreso,
    int? VendedorId,
    string TelefonoPrincipal,
    string WhatsappPrincipal,
    string CorreoPrincipal,
    VentaDto? Venta,
    PrimaDto? Prima,
    ClienteDto? Cliente,
    IReadOnlyList<NumeroDto> Numeros,
    IReadOnlyList<CorreoDto> Correos,
    IReadOnlyList<FamiliarDto> Familiares,
    IReadOnlyList<ComentarioDto> Comentarios);

// ---------- Comandos ----------

/// <summary>
/// Datos de la persona (autoguardado). Los campos de contacto principal solo se envían
/// para prospectos; si llegan nulos no se tocan. Estado y origen solo aplican a clientes.
/// </summary>
public record ActualizarDatosRequest(
    string Nombres,
    string Apellidos,
    string Cedula,
    string Finca,
    string Expediente,
    DateOnly FechaIngreso,
    int? VendedorId,
    string? TelefonoPrincipal,
    string? WhatsappPrincipal,
    string? CorreoPrincipal,
    int? EstadoClienteId,
    int? OrigenClienteId);

public record ActualizarVentaRequest(int? ProcedenciaVentaId, int? MetodoVentaId, decimal Monto, string? Notas);

public record ActualizarPrimaRequest(decimal Monto, decimal MontoCancelado, DateOnly? FechaEstimadaPago, DateOnly? FechaPago);

public record NumeroItem(int Id, string Numero, string Tipo, bool Principal);

public record CorreoItem(int Id, string Correo, bool Principal);

public record FamiliarItem(int Id, string? NombreCompleto, string? Parentesco, string? Telefono, string? CorreoElectronico, string? Whatsapp);

public record ConvertirClienteRequest(string? Expediente, int? OrigenClienteId, int? EstadoClienteId);

public record ComentarioRequest(string Texto);

/// <summary>Registro de un prospecto nuevo: se guarda todo junto al presionar "Guardar prospecto".</summary>
public record CrearProspectoRequest(
    string? Nombres,
    string? Apellidos,
    string? Cedula,
    string? Finca,
    string? Expediente,
    DateOnly? FechaIngreso,
    int? VendedorId,
    string? TelefonoPrincipal,
    string? WhatsappPrincipal,
    string? CorreoPrincipal,
    int? ProcedenciaVentaId,
    int? MetodoVentaId,
    decimal MontoVenta,
    string? NotasVenta,
    decimal MontoPrima,
    decimal MontoCancelado,
    DateOnly? FechaEstimadaPago,
    DateOnly? FechaPago,
    IReadOnlyList<FamiliarItem>? Familiares);
