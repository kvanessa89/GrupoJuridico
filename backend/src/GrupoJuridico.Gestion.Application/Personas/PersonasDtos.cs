using System.Text.Json.Serialization;

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
    [property: JsonRequired] decimal MontoPrima,
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

/// <summary>
/// <c>EstadoClienteId</c> es el que se muestra: el manual (<c>EstadoManualId</c>) si hay uno; si no, el calculado
/// con el plan de pago (<c>EstadoCalculadoId</c>: Al Día, Moroso o Estrella).
/// </summary>
public record ClienteDto(int Id, int OrigenClienteId, int EstadoClienteId, string Notas, int? EstadoManualId, int EstadoCalculadoId);

public record NumeroDto(int Id, string Numero, string Tipo, bool Principal, int PaisId);

public record CorreoDto(int Id, string Correo, bool Principal);

public record FincaDto(int Id, string Numero);

public record FamiliarDto(int Id, string NombreCompleto, int? ParentescoId, string Telefono, string CorreoElectronico, int? TelefonoPaisId);

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
    IReadOnlyList<ComentarioDto> Comentarios,
    IReadOnlyDictionary<string, Guid> Versiones,
    int? TelefonoPrincipalPaisId,
    int? WhatsappPrincipalPaisId,
    IReadOnlyList<FincaDto> Fincas);

// ---------- Comandos ----------

/// <summary>
/// Datos de la persona (autoguardado). Los campos de contacto principal y la finca solo se envían
/// para prospectos; si llegan nulos no se tocan (el cliente edita sus fincas en PUT fincas).
/// Estado y origen solo aplican a clientes.
/// </summary>
public record ActualizarDatosRequest(
    string Nombres,
    string Apellidos,
    string Cedula,
    string? Finca,
    string Expediente,
    [property: JsonRequired] DateOnly FechaIngreso,
    int? VendedorId,
    string? TelefonoPrincipal,
    string? WhatsappPrincipal,
    string? CorreoPrincipal,
    // Estado manual del cliente: 0 = automático (según el plan de pago), nulo = no cambia.
    int? EstadoClienteId,
    int? OrigenClienteId,
    int? TelefonoPrincipalPaisId = null,
    int? WhatsappPrincipalPaisId = null);

public record ActualizarVentaRequest(int? ProcedenciaVentaId, int? MetodoVentaId, [property: JsonRequired] decimal Monto, string? Notas);

public record ActualizarPrimaRequest([property: JsonRequired] decimal Monto, [property: JsonRequired] decimal MontoCancelado, DateOnly? FechaEstimadaPago, DateOnly? FechaPago);

/// <summary>PaisId nulo toma el país por defecto (el primero del catálogo).</summary>
public record NumeroItem(int Id, string Numero, string Tipo, bool Principal, int? PaisId = null);

public record CorreoItem(int Id, string Correo, bool Principal);

public record FincaItem(int Id, string? Numero);

public record FamiliarItem(int Id, string? NombreCompleto, int? ParentescoId, string? Telefono, string? CorreoElectronico,
    int? TelefonoPaisId = null);

/// <summary><c>EstadoClienteId</c>: estado manual inicial; nulo o 0 = automático según el plan de pago.</summary>
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
    [property: JsonRequired] decimal MontoVenta,
    string? NotasVenta,
    [property: JsonRequired] decimal MontoPrima,
    [property: JsonRequired] decimal MontoCancelado,
    DateOnly? FechaEstimadaPago,
    DateOnly? FechaPago,
    IReadOnlyList<FamiliarItem>? Familiares,
    int? TelefonoPrincipalPaisId = null,
    int? WhatsappPrincipalPaisId = null);

