using System.Globalization;
using GrupoJuridico.Gestion.Application.Common.Exceptions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Application.Common.Models;
using GrupoJuridico.Gestion.Application.Personas;
using GrupoJuridico.Gestion.Domain.Constants;
using GrupoJuridico.Gestion.Domain.Entities;
using GrupoJuridico.Gestion.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Cobros;

/// <summary>
/// Módulo de Cobros: planes de pago, cuotas, pagos con comprobante y promesas de pago.
/// Los estados dependen de la fecha: antes de cada lectura <see cref="ActualizadorCobros"/> recalcula
/// (y guarda) los planes que lo necesitan.
/// </summary>
public class CobrosService
{
    /// <summary>Tamaño máximo de un comprobante.</summary>
    public const long MaxComprobante = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> TiposComprobante = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private readonly IApplicationDbContext _db;
    private readonly IUsuarioActual _usuario;
    private readonly IFechaActual _fecha;
    private readonly IAlmacenArchivos _almacen;
    private readonly ActualizadorCobros _actualizador;

    public CobrosService(IApplicationDbContext db, IUsuarioActual usuario, IFechaActual fecha, IAlmacenArchivos almacen, ActualizadorCobros actualizador)
    {
        _db = db;
        _usuario = usuario;
        _fecha = fecha;
        _almacen = almacen;
        _actualizador = actualizador;
    }

    /// <summary>Administrador y Cobros registran planes, pagos y promesas; los demás solo consultan el plan.</summary>
    private bool PuedeGestionar => _usuario.Rol is Roles.Administrador or Roles.Cobros;

    private void ExigirGestion()
    {
        if (!PuedeGestionar) throw new ProhibidoException("Solo Administrador y Cobros pueden gestionar cobros.");
    }

    private Task<decimal> PorcentajeMoraAsync(CancellationToken ct) => _actualizador.PorcentajeMoraAsync(ct);

    private IQueryable<PlanPago> PlanesCompletos() => _actualizador.PlanesCompletos();

    // ---------- Resumen de un plan ----------

    private sealed record InfoCuota(Cuota Cuota, decimal Pagado, decimal Mora, decimal Total, decimal Saldo);

    private sealed class Resumen
    {
        public required PlanPago Plan { get; init; }
        public required List<InfoCuota> Info { get; init; }
        public List<InfoCuota> Vencidas => Info.Where(i => i.Cuota.EstadoCuotaVencimientoId == EstadoCuotaVencimiento.Vencido).ToList();
        public List<InfoCuota> Pendientes => Info.Where(i => i.Cuota.EstadoCuotaPagoId != EstadoCuotaPago.Pagada).ToList();
        public decimal MontoVencido => Vencidas.Sum(i => i.Saldo);
        public decimal Saldo => Pendientes.Sum(i => i.Saldo);
        public List<PromesaPago> Promesas => Info.SelectMany(i => i.Cuota.Promesas).ToList();
    }

    private static Resumen Resumir(PlanPago plan, decimal porcentaje, DateOnly hoy) => new()
    {
        Plan = plan,
        Info = plan.Cuotas.OrderBy(c => c.NumeroCuota).Select(c =>
        {
            var pagado = ReglasCobro.Pagado(c);
            var mora = ReglasCobro.Mora(c, porcentaje, hoy);
            var total = c.MontoCuota + mora;
            return new InfoCuota(c, pagado, mora, total, Math.Max(0, total - pagado));
        }).ToList()
    };

    private static int Atraso(Resumen r, DateOnly hoy)
    {
        var primera = r.Vencidas.FirstOrDefault();
        return primera == null ? 0 : hoy.DayNumber - primera.Cuota.FechaVencimiento.DayNumber;
    }

    private static bool EsMoroso(Cliente cliente, Resumen? r) =>
        (r != null && r.Vencidas.Count > 0) || cliente.EstadoClienteId == EstadoCliente.Moroso;

    /// <summary>Moroso con todo el plan vencido (pasó la última cuota) y todavía con saldo.</summary>
    private static bool EsCobroJudicial(Cliente cliente, Resumen? r, DateOnly hoy) =>
        r != null && r.Info.Count > 0 && EsMoroso(cliente, r) && r.Saldo > 0 && hoy > r.Info[^1].Cuota.FechaVencimiento;

    private static InfoCuota? CuotaARecordar(Resumen? r, DateOnly hoy) =>
        r?.Pendientes.FirstOrDefault(i => i.Cuota.FechaVencimiento > hoy && ReglasCobro.FechaRecordatorio(i.Cuota.FechaVencimiento) == hoy);

    // ---------- Lista de Cobros ----------

    public async Task<CobrosListaDto> ListarAsync(CobrosQuery q, CancellationToken ct = default)
    {
        ExigirGestion();
        var vista = string.IsNullOrWhiteSpace(q.Vista) ? VistasCobros.Morosos : q.Vista.Trim();
        if (!VistasCobros.Todas.Contains(vista)) throw new ValidacionException("vista", "Vista de cobros no válida.");

        var hoy = _fecha.Hoy;
        await _actualizador.ActualizarPendientesAsync(ct);
        var porcentaje = await PorcentajeMoraAsync(ct);
        var planes = await PlanesCompletos().AsNoTracking().ToListAsync(ct);
        var resumenes = planes.ToDictionary(p => p.ClienteId, p => Resumir(p, porcentaje, hoy));

        var personas = await _db.Personas.AsNoTracking()
            .Include(p => p.Cliente).ThenInclude(c => c!.EstadoCliente)
            .Include(p => p.Numeros).ThenInclude(n => n.Pais)
            .Where(p => p.ClienteId != null)
            .AsSplitQuery()
            .ToListAsync(ct);

        var todos = personas.Select(p => (Persona: p, R: resumenes.GetValueOrDefault(p.ClienteId!.Value))).ToList();
        var morosos = todos.Where(x => EsMoroso(x.Persona.Cliente!, x.R)).ToList();
        var totales = new CobrosTotalesDto(
            morosos.Count,
            morosos.Sum(x => x.R?.MontoVencido ?? 0),
            todos.Count(x => CuotaARecordar(x.R, hoy) != null),
            todos.Sum(x => x.R?.Promesas.Count(p => p.EstadoPromesaId == EstadoPromesa.Pendiente) ?? 0),
            todos.Count(x => EsCobroJudicial(x.Persona.Cliente!, x.R, hoy)),
            ReglasCobro.FechasARecordar(hoy));

        var texto = q.Q?.Trim().ToLowerInvariant();
        var filtrados = todos.Where(x =>
        {
            var c = x.Persona.Cliente!;
            var incluir = vista switch
            {
                VistasCobros.Promesas => x.R != null && x.R.Promesas.Any(p => p.EstadoPromesaId == EstadoPromesa.Pendiente),
                VistasCobros.CobroJudicial => EsCobroJudicial(c, x.R, hoy),
                VistasCobros.Vencido => EsMoroso(c, x.R) && x.R != null && x.R.MontoVencido > 0,
                VistasCobros.Recordatorios => CuotaARecordar(x.R, hoy) != null,
                _ => EsMoroso(c, x.R)
            };
            if (!incluir) return false;
            if (string.IsNullOrEmpty(texto)) return true;
            var p = x.Persona;
            var blob = string.Join(" ", new[]
            {
                p.NombreCompleto, p.Cedula, p.Expediente,
                string.Join(" ", p.Numeros.Select(n => n.Valor + " " + Pais.Formatear(n.Pais, n.Valor)))
            }).ToLowerInvariant();
            return blob.Contains(texto);
        }).Select(x => Fila(x.Persona, x.R, hoy)).ToList();

        var orden = q.Orden ?? (vista == VistasCobros.Recordatorios ? "expediente" : "atraso");
        object Clave(CobroFilaDto f) => orden switch
        {
            "estadoCliente" => f.EstadoClienteId,
            "cuotasVenc" => f.CuotasVencidas,
            "vencido" => f.MontoVencido,
            "atraso" => f.Atraso,
            "proxima" => f.Proxima?.Fecha.ToString("yyyy-MM-dd") ?? "9999",
            _ => f.Expediente
        };
        var comparador = Comparer<object>.Create((a, b) => (a, b) switch
        {
            (int x, int y) => x.CompareTo(y),
            (decimal x, decimal y) => x.CompareTo(y),
            _ => string.Compare(a?.ToString(), b?.ToString(), new CultureInfo("es-CR"), CompareOptions.IgnoreCase)
        });
        var ordenados = (q.Asc ? filtrados.OrderBy(Clave, comparador) : filtrados.OrderByDescending(Clave, comparador))
            .ThenBy(f => f.Id).ToList();

        var pagina = Paginado<CobroFilaDto>.Crear(ordenados, q.Pagina, q.PorPagina);
        return new CobrosListaDto(pagina.Filas, pagina.Total, pagina.Pagina, pagina.PorPagina, totales, hoy);
    }

    private static CobroFilaDto Fila(Persona p, Resumen? r, DateOnly hoy)
    {
        var c = p.Cliente!;
        var proxima = r?.Pendientes.FirstOrDefault();
        var conPromesa = r?.Pendientes.SelectMany(i => i.Cuota.Promesas).ToList() ?? new List<PromesaPago>();
        var promesa = conPromesa.FirstOrDefault(x => x.EstadoPromesaId == EstadoPromesa.Pendiente) ?? conPromesa.LastOrDefault();
        var recordar = CuotaARecordar(r, hoy);
        return new CobroFilaDto(
            p.Id, p.Expediente, p.NombreCompleto,
            Completo(p.NumeroPrincipal(TipoNumero.Telefono)),
            Completo(p.NumeroPrincipal(TipoNumero.WhatsApp)),
            c.EstadoClienteId, c.EstadoCliente?.Nombre ?? "",
            r != null,
            r?.Plan.CantidadCuotas ?? 0,
            r?.Vencidas.Count ?? 0,
            r?.MontoVencido ?? 0,
            r == null ? 0 : Atraso(r, hoy),
            proxima == null ? null : new ProximaCuotaDto(proxima.Cuota.FechaVencimiento, proxima.Saldo),
            promesa == null ? null : new PromesaResumenDto(promesa.EstadoPromesaId, promesa.FechaPromesa),
            recordar == null ? null : new RecordatorioDto(recordar.Cuota.NumeroCuota, recordar.Saldo, recordar.Cuota.FechaVencimiento));
    }

    private static string? Completo(Numero? n) => n == null ? null : Pais.Formatear(n.Pais, n.Valor);

    // ---------- Plan de una persona ----------

    private async Task<Persona> CargarPersonaAsync(int personaId, CancellationToken ct)
    {
        var persona = await _db.Personas.AsNoTracking()
            .Include(p => p.Cliente)
            .Include(p => p.Ventas).ThenInclude(v => v.Prima)
            .FirstOrDefaultAsync(p => p.Id == personaId, ct)
            ?? throw new NoEncontradoException("Persona", personaId);
        if (!PersonasService.AccesoPermitido(_usuario.Rol, persona))
            throw new ProhibidoException("No tiene permiso para acceder a esta persona.");
        return persona;
    }

    public async Task<PlanPersonaDto> ObtenerPlanAsync(int personaId, CancellationToken ct = default)
    {
        // La persona se lee sin seguimiento (solo para revisar el acceso), así que el recálculo no la deja desactualizada.
        var persona = await CargarPersonaAsync(personaId, ct);
        var porcentaje = await PorcentajeMoraAsync(ct);
        PlanPago? plan = null;
        if (persona.ClienteId is int clienteId)
        {
            await _actualizador.ActualizarClienteAsync(clienteId, ct);
            plan = await PlanesCompletos().FirstOrDefaultAsync(p => p.ClienteId == clienteId, ct);
        }
        var estadoCliente = plan?.Cliente?.EstadoClienteId ?? persona.Cliente?.EstadoClienteId;
        var venta = persona.Venta;
        var sugerido = Math.Max(0, (venta?.Monto ?? 0) - (venta?.Prima?.Monto ?? 0));
        return new PlanPersonaDto(
            persona.Id, persona.EsCliente, persona.Expediente, persona.Nombres, persona.Apellidos, persona.FechaIngreso,
            estadoCliente, porcentaje, sugerido, PuedeGestionar, _fecha.Hoy,
            plan == null ? null : Detalle(plan, porcentaje, _fecha.Hoy));
    }

    private static PlanDetalleDto Detalle(PlanPago plan, decimal porcentaje, DateOnly hoy)
    {
        var r = Resumir(plan, porcentaje, hoy);
        var promesas = r.Promesas;
        return new PlanDetalleDto(
            plan.Id, plan.MontoTotal, plan.MontoCuota, plan.CantidadCuotas, plan.FechaInicio, plan.DiaPago,
            r.Saldo, r.Info.Sum(i => i.Pagado), r.Info.Count - r.Pendientes.Count, r.Vencidas.Count, r.MontoVencido, Atraso(r, hoy),
            promesas.Count(p => p.EstadoPromesaId == EstadoPromesa.Pendiente),
            promesas.Count(p => p.EstadoPromesaId == EstadoPromesa.Cumplida),
            promesas.Count(p => p.EstadoPromesaId == EstadoPromesa.Incumplida),
            r.Info.Select(i => new CuotaDto(
                i.Cuota.Id, i.Cuota.NumeroCuota, i.Cuota.FechaVencimiento, i.Cuota.MontoCuota,
                i.Mora, ReglasCobro.Mora(i.Cuota, porcentaje, hoy, ignorarAcuerdo: true), i.Cuota.SinMora,
                i.Total, i.Pagado, i.Saldo, i.Cuota.EstadoCuotaPagoId, i.Cuota.EstadoCuotaVencimientoId,
                i.Cuota.Pagos.OrderBy(p => p.FechaPago).ThenBy(p => p.Id).Select(p => new PagoDto(
                    p.Id, p.MontoPago, p.FechaPago,
                    p.Archivos.OrderBy(a => a.Id).Select(a => new ComprobanteDto(a.Id, a.Nombre, !string.IsNullOrEmpty(a.Clave))).ToList())).ToList(),
                i.Cuota.Promesas.OrderBy(p => p.Id).Select(p => new PromesaDto(
                    p.Id, p.FechaPromesa, p.MontoPrometido, p.MontoPagado, p.EstadoPromesaId)).ToList())).ToList());
    }

    public async Task<PlanPersonaDto> CrearPlanAsync(int personaId, CrearPlanRequest r, CancellationToken ct = default)
    {
        ExigirGestion();
        var persona = await CargarPersonaAsync(personaId, ct);
        if (persona.ClienteId is not int clienteId)
            throw new ValidacionException("plan", "El plan de pago se crea cuando el prospecto se convierte en cliente oficial.");
        if (await _db.PlanesPago.AnyAsync(p => p.ClienteId == clienteId, ct))
            throw new ValidacionException("plan", "Este cliente ya tiene un plan de pago.");

        var errores = new Dictionary<string, string[]>();
        if (r.MontoTotal <= 0) errores["montoTotal"] = new[] { "Ingrese un monto total mayor a cero." };
        if (r.CantidadCuotas is < 1 or > PlanPago.MaxCuotas) errores["cantidadCuotas"] = new[] { "Ingrese entre 1 y 120 cuotas." };
        if (r.FechaInicio == null) errores["fechaInicio"] = new[] { "Ingrese la fecha de inicio de pago." };
        if (r.DiaPago is < 1 or > 31) errores["diaPago"] = new[] { "Ingrese un día de pago entre 1 y 31." };
        if (errores.Count > 0) throw new ValidacionException(errores);

        var plan = new PlanPago
        {
            ClienteId = clienteId,
            MontoTotal = r.MontoTotal,
            MontoCuota = ReglasCobro.MontoMensual(r.MontoTotal, r.CantidadCuotas),
            CantidadCuotas = r.CantidadCuotas,
            FechaInicio = r.FechaInicio!.Value,
            DiaPago = r.DiaPago
        };
        plan.Cuotas = ReglasCobro.GenerarCuotas(plan);
        _db.PlanesPago.Add(plan);
        await _db.SaveChangesAsync(ct);
        return await ObtenerPlanAsync(personaId, ct);
    }

    // ---------- Pagos y promesas ----------

    private async Task<(PlanPago Plan, Cuota Cuota)> CargarCuotaAsync(int cuotaId, CancellationToken ct)
    {
        var plan = await PlanesCompletos().FirstOrDefaultAsync(p => p.Cuotas.Any(c => c.Id == cuotaId), ct)
            ?? throw new NoEncontradoException("Cuota", cuotaId);
        return (plan, plan.Cuotas.Single(c => c.Id == cuotaId));
    }

    private async Task<int> PersonaDelPlanAsync(PlanPago plan, CancellationToken ct) =>
        await _db.Personas.Where(p => p.ClienteId == plan.ClienteId).Select(p => p.Id).FirstAsync(ct);

    private static string Monto(decimal n) => "₡" + n.ToString("#,##0.##", CultureInfo.InvariantCulture);

    private static void Validar(params (bool Falla, string Campo, string Mensaje)[] reglas)
    {
        var primera = reglas.FirstOrDefault(x => x.Falla);
        if (primera.Falla) throw new ValidacionException(primera.Campo, primera.Mensaje);
    }

    private static (string Extension, string Tipo) ValidarComprobante(ArchivoSubido archivo)
    {
        var extension = Path.GetExtension(archivo.Nombre ?? "");
        if (archivo.Tamano <= 0 || archivo.Tamano > MaxComprobante || !TiposComprobante.TryGetValue(extension, out var tipo))
            throw new ValidacionException("comprobante", "El comprobante debe ser un PDF o una imagen (JPG, PNG o WEBP) de hasta 10 MB.");
        return (extension, tipo);
    }

    /// <summary>
    /// Guarda el comprobante, aplica el cambio y recalcula el plan. Si el guardado falla, el archivo nuevo se borra.
    /// </summary>
    private async Task GuardarMovimientoAsync(PlanPago plan, Cuota cuota, decimal porcentaje, CancellationToken ct, string? claveNueva = null)
    {
        cuota.Version = Guid.NewGuid();
        await NormalizarPlanYGuardarAsync(plan, porcentaje, ct, claveNueva);
    }

    private async Task NormalizarPlanYGuardarAsync(PlanPago plan, decimal porcentaje, CancellationToken ct, string? claveNueva)
    {
        var (_, estadoCliente) = ReglasCobro.NormalizarPlan(plan, plan.Cliente, porcentaje, _fecha.Hoy);
        if (estadoCliente)
        {
            var version = await _db.Personas.Where(p => p.ClienteId == plan.ClienteId)
                .SelectMany(p => p.Versiones.Where(v => v.Seccion == "datos")).FirstOrDefaultAsync(ct);
            if (version != null) version.Version = Guid.NewGuid();
        }
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (claveNueva != null) await _almacen.EliminarAsync(claveNueva, CancellationToken.None);
            throw new ValidacionException("cuota", "Otra persona registró un movimiento en esta cuota. Recargue el plan e intente de nuevo.");
        }
        catch
        {
            if (claveNueva != null) await _almacen.EliminarAsync(claveNueva, CancellationToken.None);
            throw;
        }
    }

    public async Task<PlanPersonaDto> RegistrarPagoAsync(int cuotaId, RegistrarPagoRequest r, ArchivoSubido? comprobante, CancellationToken ct = default)
    {
        ExigirGestion();
        var (plan, cuota) = await CargarCuotaAsync(cuotaId, ct);
        var personaId = await PersonaDelPlanAsync(plan, ct);
        var hoy = _fecha.Hoy;
        var porcentaje = await PorcentajeMoraAsync(ct);
        var moraPosible = ReglasCobro.Mora(cuota, porcentaje, hoy, ignorarAcuerdo: true);
        var cobrarMora = moraPosible > 0 && r.CobrarMora;
        var saldo = Math.Max(0, cuota.MontoCuota + (cobrarMora ? moraPosible : 0) - ReglasCobro.Pagado(cuota));

        Validar(
            (cuota.EstadoCuotaPagoId == EstadoCuotaPago.Pagada && saldo <= 0, "cuota", "La cuota ya está pagada."),
            (r.MontoPago <= 0, "montoPago", "Ingrese el monto del pago."),
            (r.MontoPago > saldo, "montoPago", $"El monto no puede ser mayor al saldo de la cuota ({Monto(saldo)})."),
            (r.FechaPago == null, "fechaPago", "Ingrese la fecha de pago."),
            (r.FechaPago > hoy, "fechaPago", "La fecha de pago no puede ser posterior a hoy."),
            (comprobante == null, "comprobante", "Suba el comprobante de pago."));
        var (extension, tipo) = ValidarComprobante(comprobante!);

        var clave = await _almacen.GuardarAsync(comprobante!.Contenido, extension, ct);
        var pago = new Pago { CuotaId = cuota.Id, MontoPago = r.MontoPago, FechaPago = r.FechaPago!.Value };
        pago.Archivos.Add(new ArchivoPago { Nombre = NombreSeguro(comprobante.Nombre), TipoContenido = tipo, Tamano = comprobante.Tamano, FechaCarga = hoy, Clave = clave });
        cuota.Pagos.Add(pago);

        // El pago se suma primero a las promesas pendientes de la cuota.
        var resto = r.MontoPago;
        foreach (var promesa in cuota.Promesas.Where(p => p.EstadoPromesaId == EstadoPromesa.Pendiente).OrderBy(p => p.Id))
        {
            if (resto <= 0) break;
            var abono = Math.Min(resto, promesa.MontoPrometido - promesa.MontoPagado);
            if (abono <= 0) continue;
            promesa.MontoPagado += abono;
            resto -= abono;
        }
        if (moraPosible > 0) cuota.SinMora = !cobrarMora;

        await GuardarMovimientoAsync(plan, cuota, porcentaje, ct, clave);
        return await ObtenerPlanAsync(personaId, ct);
    }

    public async Task<PlanPersonaDto> EditarPagoAsync(int pagoId, EditarPagoRequest r, ArchivoSubido? comprobante, CancellationToken ct = default)
    {
        ExigirGestion();
        var plan = await PlanesCompletos().FirstOrDefaultAsync(p => p.Cuotas.Any(c => c.Pagos.Any(x => x.Id == pagoId)), ct)
            ?? throw new NoEncontradoException("Pago", pagoId);
        var cuota = plan.Cuotas.Single(c => c.Pagos.Any(x => x.Id == pagoId));
        var pago = cuota.Pagos.Single(x => x.Id == pagoId);
        var personaId = await PersonaDelPlanAsync(plan, ct);
        var hoy = _fecha.Hoy;
        var porcentaje = await PorcentajeMoraAsync(ct);
        var saldoSinEstePago = ReglasCobro.Saldo(cuota, porcentaje, hoy) + pago.MontoPago;

        Validar(
            (r.MontoPago <= 0, "montoPago", "Ingrese el monto del pago."),
            (r.MontoPago > saldoSinEstePago, "montoPago", $"El monto no puede ser mayor al saldo de la cuota ({Monto(saldoSinEstePago)})."),
            (r.FechaPago == null, "fechaPago", "Ingrese la fecha de pago."),
            (r.FechaPago > hoy, "fechaPago", "La fecha de pago no puede ser posterior a hoy."),
            (comprobante == null && pago.Archivos.Count == 0, "comprobante", "Suba el comprobante de pago."));

        string? claveNueva = null;
        var anteriores = new List<string>();
        if (comprobante != null)
        {
            var (extension, tipo) = ValidarComprobante(comprobante);
            claveNueva = await _almacen.GuardarAsync(comprobante.Contenido, extension, ct);
            anteriores = pago.Archivos.Select(a => a.Clave).Where(c => !string.IsNullOrEmpty(c)).ToList();
            pago.Archivos.Clear();
            pago.Archivos.Add(new ArchivoPago { Nombre = NombreSeguro(comprobante.Nombre), TipoContenido = tipo, Tamano = comprobante.Tamano, FechaCarga = hoy, Clave = claveNueva });
        }

        // La diferencia del monto se refleja en las promesas de la cuota que no estén incumplidas.
        var delta = r.MontoPago - pago.MontoPago;
        foreach (var promesa in cuota.Promesas.Where(p => p.EstadoPromesaId != EstadoPromesa.Incumplida).OrderBy(p => p.Id))
        {
            if (delta == 0) break;
            var nuevo = Math.Max(0, Math.Min(promesa.MontoPrometido, promesa.MontoPagado + delta));
            delta -= nuevo - promesa.MontoPagado;
            promesa.MontoPagado = nuevo;
        }
        pago.MontoPago = r.MontoPago;
        pago.FechaPago = r.FechaPago!.Value;

        await GuardarMovimientoAsync(plan, cuota, porcentaje, ct, claveNueva);
        foreach (var clave in anteriores) await _almacen.EliminarAsync(clave, CancellationToken.None);
        return await ObtenerPlanAsync(personaId, ct);
    }

    public async Task<PlanPersonaDto> CrearPromesaAsync(int cuotaId, PromesaRequest r, CancellationToken ct = default)
    {
        ExigirGestion();
        var (plan, cuota) = await CargarCuotaAsync(cuotaId, ct);
        var personaId = await PersonaDelPlanAsync(plan, ct);
        var hoy = _fecha.Hoy;
        var porcentaje = await PorcentajeMoraAsync(ct);
        var saldo = ReglasCobro.Saldo(cuota, porcentaje, hoy);

        Validar(
            (cuota.EstadoCuotaPagoId == EstadoCuotaPago.Pagada, "cuota", "La cuota ya está pagada."),
            (cuota.Promesas.Any(p => p.EstadoPromesaId == EstadoPromesa.Pendiente), "cuota", "La cuota ya tiene una promesa de pago pendiente."),
            (r.FechaPromesa == null, "fechaPromesa", "Ingrese la fecha de la promesa."),
            (r.FechaPromesa < hoy, "fechaPromesa", "La fecha de la promesa debe ser hoy o posterior."),
            (r.MontoPrometido <= 0, "montoPrometido", "Ingrese el monto prometido."),
            (r.MontoPrometido > saldo, "montoPrometido", $"El monto no puede ser mayor al saldo de la cuota ({Monto(saldo)})."));

        cuota.Promesas.Add(new PromesaPago { CuotaId = cuota.Id, FechaPromesa = r.FechaPromesa!.Value, MontoPrometido = r.MontoPrometido });
        await GuardarMovimientoAsync(plan, cuota, porcentaje, ct);
        return await ObtenerPlanAsync(personaId, ct);
    }

    public async Task<PlanPersonaDto> EditarPromesaAsync(int promesaId, PromesaRequest r, CancellationToken ct = default)
    {
        ExigirGestion();
        var plan = await PlanesCompletos().FirstOrDefaultAsync(p => p.Cuotas.Any(c => c.Promesas.Any(x => x.Id == promesaId)), ct)
            ?? throw new NoEncontradoException("Promesa", promesaId);
        var cuota = plan.Cuotas.Single(c => c.Promesas.Any(x => x.Id == promesaId));
        var promesa = cuota.Promesas.Single(x => x.Id == promesaId);
        var personaId = await PersonaDelPlanAsync(plan, ct);
        var hoy = _fecha.Hoy;
        var porcentaje = await PorcentajeMoraAsync(ct);
        var tope = ReglasCobro.Saldo(cuota, porcentaje, hoy) + promesa.MontoPagado;

        Validar(
            (promesa.EstadoPromesaId != EstadoPromesa.Pendiente, "promesa", "Solo se pueden editar promesas pendientes."),
            (r.FechaPromesa == null, "fechaPromesa", "Ingrese la fecha de la promesa."),
            (r.FechaPromesa < hoy, "fechaPromesa", "La fecha de la promesa debe ser hoy o posterior."),
            (r.MontoPrometido <= 0, "montoPrometido", "Ingrese el monto prometido."),
            (r.MontoPrometido > tope, "montoPrometido", $"El monto no puede ser mayor al saldo de la cuota ({Monto(tope)})."));

        promesa.FechaPromesa = r.FechaPromesa!.Value;
        promesa.MontoPrometido = r.MontoPrometido;
        promesa.MontoPagado = Math.Min(promesa.MontoPagado, r.MontoPrometido);
        await GuardarMovimientoAsync(plan, cuota, porcentaje, ct);
        return await ObtenerPlanAsync(personaId, ct);
    }

    /// <summary>Comprobante para descargar; cualquier rol con acceso a la persona puede verlo.</summary>
    public async Task<(Stream Contenido, string Nombre, string Tipo)> ComprobanteAsync(int archivoId, CancellationToken ct = default)
    {
        var archivo = await _db.ArchivosPago.AsNoTracking().FirstOrDefaultAsync(a => a.Id == archivoId, ct)
            ?? throw new NoEncontradoException("Comprobante", archivoId);
        var clienteId = await _db.Pagos.Where(p => p.Id == archivo.PagoId).Select(p => p.Cuota!.PlanPago!.ClienteId).FirstAsync(ct);
        var personaId = await _db.Personas.Where(p => p.ClienteId == clienteId).Select(p => p.Id).FirstAsync(ct);
        await CargarPersonaAsync(personaId, ct);
        var contenido = string.IsNullOrEmpty(archivo.Clave) ? null : await _almacen.AbrirAsync(archivo.Clave, ct);
        if (contenido == null) throw new NoEncontradoException("Archivo del comprobante", archivoId);
        return (contenido, archivo.Nombre, string.IsNullOrEmpty(archivo.TipoContenido) ? "application/octet-stream" : archivo.TipoContenido);
    }

    /// <summary>Nombre para mostrar: sin rutas ni caracteres de control y de largo acotado.</summary>
    private static string NombreSeguro(string nombre)
    {
        var limpio = new string(Path.GetFileName(nombre ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (limpio.Length == 0) limpio = "comprobante";
        return limpio.Length > 200 ? limpio[..200] : limpio;
    }
}
