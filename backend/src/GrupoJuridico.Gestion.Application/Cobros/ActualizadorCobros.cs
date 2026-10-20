using System.Globalization;
using System.Linq.Expressions;
using GrupoJuridico.Gestion.Application.Common.Interfaces;
using GrupoJuridico.Gestion.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GrupoJuridico.Gestion.Application.Cobros;

/// <summary>
/// Mantiene al día los estados de cobros que cambian con la fecha (cuotas vencidas, promesas incumplidas
/// y el estado del cliente). No hay un proceso periódico: cada pantalla que muestra estos estados llama
/// antes a este servicio, que solo recalcula los planes que lo necesitan (normalmente ninguno).
/// Debe llamarse antes de cargar otras entidades en el mismo contexto: si pierde una carrera de
/// concurrencia descarta lo que tenía en memoria.
/// </summary>
public class ActualizadorCobros
{
    private readonly IApplicationDbContext _db;
    private readonly IFechaActual _fecha;

    public ActualizadorCobros(IApplicationDbContext db, IFechaActual fecha)
    {
        _db = db;
        _fecha = fecha;
    }

    public async Task<decimal> PorcentajeMoraAsync(CancellationToken ct = default)
    {
        var fila = await _db.Configuracion.AsNoTracking().FirstOrDefaultAsync(c => c.Clave == ConfiguracionSistema.InteresMora, ct);
        return fila != null && decimal.TryParse(fila.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct) ? pct : 2.5m;
    }

    public IQueryable<PlanPago> PlanesCompletos() => _db.PlanesPago
        .Include(p => p.Cliente)
        .Include(p => p.Cuotas).ThenInclude(c => c.Pagos).ThenInclude(p => p.Archivos)
        .Include(p => p.Cuotas).ThenInclude(c => c.Promesas)
        .AsSplitQuery();

    /// <summary>
    /// Planes cuyos estados guardados ya no corresponden a la fecha de hoy, según las reglas de <see cref="ReglasCobro"/>.
    /// La base de datos lo resuelve sin cargar los planes:
    /// <list type="bullet">
    /// <item>una cuota sin pagar que ya venció pero sigue "al día", o una promesa pendiente con la fecha pasada;</item>
    /// <item>un estado del cliente que no corresponde a sus cuotas (por ejemplo, cambiado a mano en la ficha).</item>
    /// </list>
    /// Si cambian las reglas de <see cref="ReglasCobro.EstadoClientePara"/> hay que cambiar también esta consulta;
    /// la prueba ActualizadorCobrosTests verifica que coincidan día por día.
    /// </summary>
    public static Expression<Func<PlanPago, bool>> NecesitaRecalculo(DateOnly hoy) => p =>
        p.Cuotas.Any(c => c.EstadoCuotaPagoId != EstadoCuotaPago.Pagada
                          && c.EstadoCuotaVencimientoId == EstadoCuotaVencimiento.AlDia
                          && c.FechaVencimiento < hoy)
        || p.Cuotas.Any(c => c.Promesas.Any(pm => pm.EstadoPromesaId == EstadoPromesa.Pendiente && pm.FechaPromesa < hoy))
        || (p.Cliente != null && p.Cuotas.Any() && (
            (p.Cuotas.All(c => c.EstadoCuotaPagoId == EstadoCuotaPago.Pagada)
                && p.Cliente.EstadoClienteId != EstadoCliente.Estrella
                && p.Cliente.EstadoClienteId != EstadoCliente.Terminado
                && p.Cliente.EstadoClienteId != EstadoCliente.Abandonado)
            || (p.Cliente.EstadoClienteId == EstadoCliente.Estrella && p.Cuotas.Any(c => c.EstadoCuotaPagoId != EstadoCuotaPago.Pagada))
            || (p.Cuotas.Any(c => c.EstadoCuotaVencimientoId == EstadoCuotaVencimiento.Vencido)
                && p.Cliente.EstadoClienteId != EstadoCliente.Moroso
                && p.Cliente.EstadoClienteId != EstadoCliente.Terminado
                && p.Cliente.EstadoClienteId != EstadoCliente.Abandonado)
            || (p.Cliente.EstadoClienteId == EstadoCliente.Moroso
                && !p.Cuotas.Any(c => c.EstadoCuotaVencimientoId == EstadoCuotaVencimiento.Vencido))));

    /// <summary>Recalcula los planes que lo necesitan (antes de la tabla de Cobros o de Clientes).</summary>
    public Task ActualizarPendientesAsync(CancellationToken ct = default) => ActualizarAsync(_db.PlanesPago, ct);

    /// <summary>Recalcula el plan de un cliente si lo necesita (antes de su ficha o de su plan de pago).</summary>
    public Task ActualizarClienteAsync(int clienteId, CancellationToken ct = default) =>
        ActualizarAsync(_db.PlanesPago.Where(p => p.ClienteId == clienteId), ct);

    /// <summary>Recalcula todos los planes. Solo para cambios que no dependen de la fecha, como el porcentaje de mora.</summary>
    public async Task ActualizarTodosAsync(CancellationToken ct = default)
    {
        var planes = await PlanesCompletos().ToListAsync(ct);
        await NormalizarAsync(planes, await PorcentajeMoraAsync(ct), ct);
    }

    private async Task ActualizarAsync(IQueryable<PlanPago> origen, CancellationToken ct)
    {
        var ids = await origen.Where(NecesitaRecalculo(_fecha.Hoy)).Select(p => p.Id).ToListAsync(ct);
        if (ids.Count == 0) return;
        var planes = await PlanesCompletos().Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        await NormalizarAsync(planes, await PorcentajeMoraAsync(ct), ct);
    }

    /// <summary>
    /// Recalcula estados de cuotas, promesas y clientes y guarda lo que cambió. Si el estado de un cliente cambia,
    /// también cambia la versión de la sección "datos" de su ficha, porque ahí se edita ese estado.
    /// </summary>
    private async Task NormalizarAsync(IReadOnlyList<PlanPago> planes, decimal porcentaje, CancellationToken ct)
    {
        var hoy = _fecha.Hoy;
        var hayCambios = false;
        var clientesCambiados = new List<int>();
        foreach (var plan in planes)
        {
            var (cambios, estadoCliente) = ReglasCobro.NormalizarPlan(plan, plan.Cliente, porcentaje, hoy);
            hayCambios |= cambios;
            if (estadoCliente) clientesCambiados.Add(plan.ClienteId);
        }
        if (!hayCambios) return;
        if (clientesCambiados.Count > 0)
        {
            var versiones = await _db.Personas
                .Where(p => p.ClienteId != null && clientesCambiados.Contains(p.ClienteId.Value))
                .SelectMany(p => p.Versiones.Where(v => v.Seccion == "datos"))
                .ToListAsync(ct);
            foreach (var v in versiones) v.Version = Guid.NewGuid();
        }
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otra solicitud guardó primero (por ejemplo, un pago en la misma cuota). Se descarta lo calculado
            // para no mostrar valores sin guardar; la próxima lectura vuelve a detectar y recalcular el plan.
            _db.DescartarCambios();
        }
    }
}
