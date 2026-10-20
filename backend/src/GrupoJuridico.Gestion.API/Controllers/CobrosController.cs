using GrupoJuridico.Gestion.Application.Cobros;
using GrupoJuridico.Gestion.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrupoJuridico.Gestion.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cobros")]
public class CobrosController : ControllerBase
{
    private readonly CobrosService _cobros;

    public CobrosController(CobrosService cobros) => _cobros = cobros;

    /// <summary>
    /// Clientes de la sección Cobros con las tarjetas de totales. Vista: morosos, vencido, recordatorios, promesas o cobro-judicial.
    /// </summary>
    [Authorize(Roles = Roles.Administrador + "," + Roles.Cobros)]
    [HttpGet]
    public Task<CobrosListaDto> Listar([FromQuery] CobrosQuery query, CancellationToken ct) => _cobros.ListarAsync(query, ct);

    /// <summary>Registra un pago de la cuota con su comprobante (PDF o imagen, obligatorio).</summary>
    [Authorize(Roles = Roles.Administrador + "," + Roles.Cobros)]
    [HttpPost("cuotas/{cuotaId:int}/pagos")]
    [RequestSizeLimit(CobrosService.MaxComprobante + 1024 * 1024)]
    public Task<PlanPersonaDto> RegistrarPago(int cuotaId, [FromForm] PagoForm form, CancellationToken ct) =>
        _cobros.RegistrarPagoAsync(cuotaId, new RegistrarPagoRequest(form.MontoPago, form.FechaPago, form.CobrarMora), Archivo(form.Comprobante), ct);

    /// <summary>Edita monto y fecha de un pago; si llega un comprobante, reemplaza el anterior.</summary>
    [Authorize(Roles = Roles.Administrador + "," + Roles.Cobros)]
    [HttpPut("pagos/{pagoId:int}")]
    [RequestSizeLimit(CobrosService.MaxComprobante + 1024 * 1024)]
    public Task<PlanPersonaDto> EditarPago(int pagoId, [FromForm] PagoForm form, CancellationToken ct) =>
        _cobros.EditarPagoAsync(pagoId, new EditarPagoRequest(form.MontoPago, form.FechaPago), Archivo(form.Comprobante), ct);

    [Authorize(Roles = Roles.Administrador + "," + Roles.Cobros)]
    [HttpPost("cuotas/{cuotaId:int}/promesas")]
    public Task<PlanPersonaDto> CrearPromesa(int cuotaId, PromesaRequest request, CancellationToken ct) =>
        _cobros.CrearPromesaAsync(cuotaId, request, ct);

    [Authorize(Roles = Roles.Administrador + "," + Roles.Cobros)]
    [HttpPut("promesas/{promesaId:int}")]
    public Task<PlanPersonaDto> EditarPromesa(int promesaId, PromesaRequest request, CancellationToken ct) =>
        _cobros.EditarPromesaAsync(promesaId, request, ct);

    private static ArchivoSubido? Archivo(IFormFile? archivo) =>
        archivo == null ? null : new ArchivoSubido(archivo.FileName, archivo.ContentType, archivo.Length, archivo.OpenReadStream());

    /// <summary>Formulario multipart del pago: monto, fecha, si se cobra el interés y el comprobante.</summary>
    public class PagoForm
    {
        public decimal MontoPago { get; set; }
        public DateOnly? FechaPago { get; set; }
        public bool CobrarMora { get; set; } = true;
        public IFormFile? Comprobante { get; set; }
    }
}

[ApiController]
[Authorize]
[Route("api/personas/{personaId:int}/plan-pago")]
public class PlanPagoController : ControllerBase
{
    private readonly CobrosService _cobros;

    public PlanPagoController(CobrosService cobros) => _cobros = cobros;

    /// <summary>Plan de pago del cliente (Plan = null si todavía no tiene).</summary>
    [HttpGet]
    public Task<PlanPersonaDto> Obtener(int personaId, CancellationToken ct) => _cobros.ObtenerPlanAsync(personaId, ct);

    /// <summary>Crea el plan y genera todas sus cuotas (Administrador y Cobros).</summary>
    [Authorize(Roles = Roles.Administrador + "," + Roles.Cobros)]
    [HttpPost]
    public Task<PlanPersonaDto> Crear(int personaId, CrearPlanRequest request, CancellationToken ct) =>
        _cobros.CrearPlanAsync(personaId, request, ct);
}
