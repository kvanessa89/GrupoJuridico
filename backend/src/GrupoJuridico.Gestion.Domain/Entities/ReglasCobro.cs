namespace GrupoJuridico.Gestion.Domain.Entities;

/// <summary>
/// Reglas del módulo de Cobros. Son funciones puras sobre las cuotas, sus pagos y promesas,
/// para que la API y las pruebas calculen exactamente lo mismo.
/// </summary>
public static class ReglasCobro
{
    /// <summary>Fecha con el día de pago dentro del mes indicado; si el mes es más corto, el último día.</summary>
    public static DateOnly FechaDia(int anio, int mes, int dia)
    {
        var primero = new DateOnly(anio, 1, 1).AddMonths(mes - 1);
        var ultimo = DateTime.DaysInMonth(primero.Year, primero.Month);
        return new DateOnly(primero.Year, primero.Month, Math.Min(dia, ultimo));
    }

    public static decimal MontoMensual(decimal montoTotal, int cantidadCuotas) =>
        Math.Round(montoTotal / cantidadCuotas, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Cuotas del plan: una por mes desde la fecha de inicio. Si el día de pago de ese mes ya pasó,
    /// la primera cuota cae el mes siguiente. La última cuota absorbe la diferencia por redondeo.
    /// </summary>
    public static List<Cuota> GenerarCuotas(PlanPago plan)
    {
        var desfase = FechaDia(plan.FechaInicio.Year, plan.FechaInicio.Month, plan.DiaPago) < plan.FechaInicio ? 1 : 0;
        var cuotas = new List<Cuota>();
        for (var i = 0; i < plan.CantidadCuotas; i++)
        {
            var esUltima = i == plan.CantidadCuotas - 1;
            cuotas.Add(new Cuota
            {
                NumeroCuota = i + 1,
                FechaVencimiento = FechaDia(plan.FechaInicio.Year, plan.FechaInicio.Month + desfase + i, plan.DiaPago),
                MontoCuota = esUltima ? plan.MontoTotal - plan.MontoCuota * (plan.CantidadCuotas - 1) : plan.MontoCuota
            });
        }
        return cuotas;
    }

    public static decimal Pagado(Cuota c) => c.Pagos.Sum(p => p.MontoPago);

    /// <summary>
    /// Interés de morosidad de una cuota vencida que no se pagó completa a tiempo. No aplica si se acordó
    /// no cobrarlo (salvo con <paramref name="ignorarAcuerdo"/>, para mostrar cuánto sería).
    /// </summary>
    public static decimal Mora(Cuota c, decimal porcentaje, DateOnly hoy, bool ignorarAcuerdo = false)
    {
        if (c.FechaVencimiento >= hoy || (c.SinMora == true && !ignorarAcuerdo)) return 0;
        var aTiempo = c.Pagos.Where(p => p.FechaPago <= c.FechaVencimiento).Sum(p => p.MontoPago);
        return aTiempo < c.MontoCuota ? Math.Round(c.MontoCuota * porcentaje / 100, 0, MidpointRounding.AwayFromZero) : 0;
    }

    public static decimal Total(Cuota c, decimal porcentaje, DateOnly hoy) => c.MontoCuota + Mora(c, porcentaje, hoy);

    public static decimal Saldo(Cuota c, decimal porcentaje, DateOnly hoy) => Math.Max(0, Total(c, porcentaje, hoy) - Pagado(c));

    /// <summary>Recalcula el estado de pago (según lo pagado contra el total con interés) y el de vencimiento.</summary>
    public static bool RecalcularCuota(Cuota c, decimal porcentaje, DateOnly hoy)
    {
        var pagado = Pagado(c);
        var total = Total(c, porcentaje, hoy);
        var pago = pagado <= 0 ? EstadoCuotaPago.Pendiente : pagado < total ? EstadoCuotaPago.Parcial : EstadoCuotaPago.Pagada;
        var vencimiento = pago != EstadoCuotaPago.Pagada && c.FechaVencimiento < hoy ? EstadoCuotaVencimiento.Vencido : EstadoCuotaVencimiento.AlDia;
        if (c.EstadoCuotaPagoId == pago && c.EstadoCuotaVencimientoId == vencimiento) return false;
        c.EstadoCuotaPagoId = pago;
        c.EstadoCuotaVencimientoId = vencimiento;
        return true;
    }

    /// <summary>Cumplida si se pagó lo prometido; incumplida si pasó la fecha sin completarlo.</summary>
    public static bool RecalcularPromesa(PromesaPago p, DateOnly hoy)
    {
        var estado = p.MontoPagado >= p.MontoPrometido ? EstadoPromesa.Cumplida
            : p.FechaPromesa < hoy ? EstadoPromesa.Incumplida
            : EstadoPromesa.Pendiente;
        if (p.EstadoPromesaId == estado) return false;
        p.EstadoPromesaId = estado;
        return true;
    }

    /// <summary>
    /// Estado que calcula el sistema con las cuotas del plan: Estrella si todas están pagadas, Moroso si alguna
    /// está vencida y Al Día en otro caso (también sin plan de pago).
    /// </summary>
    public static int EstadoCalculado(IReadOnlyCollection<Cuota> cuotas)
    {
        if (cuotas.Count == 0) return EstadoCliente.AlDia;
        if (cuotas.All(c => c.EstadoCuotaPagoId == EstadoCuotaPago.Pagada)) return EstadoCliente.Estrella;
        if (cuotas.Any(c => c.EstadoCuotaVencimientoId == EstadoCuotaVencimiento.Vencido)) return EstadoCliente.Moroso;
        return EstadoCliente.AlDia;
    }

    /// <summary>El estado que se muestra: el manual si el cliente tiene uno; si no, el calculado.</summary>
    public static int EstadoEfectivo(int? estadoManualId, IReadOnlyCollection<Cuota> cuotas) =>
        estadoManualId ?? EstadoCalculado(cuotas);

    /// <summary>
    /// Recalcula cuotas, promesas y el estado del cliente de un plan (con sus pagos y promesas cargados).
    /// Devuelve si algo cambió y si cambió el estado del cliente.
    /// </summary>
    public static (bool Cambios, bool EstadoCliente) NormalizarPlan(PlanPago plan, Cliente? cliente, decimal porcentaje, DateOnly hoy)
    {
        var cambios = false;
        foreach (var c in plan.Cuotas)
        {
            cambios |= RecalcularCuota(c, porcentaje, hoy);
            foreach (var p in c.Promesas) cambios |= RecalcularPromesa(p, hoy);
        }
        if (cliente == null) return (cambios, false);
        var estado = EstadoEfectivo(cliente.EstadoManualId, plan.Cuotas);
        if (estado == cliente.EstadoClienteId) return (cambios, false);
        cliente.EstadoClienteId = estado;
        return (true, true);
    }

    /// <summary>El recordatorio se envía el día anterior al pago; si ese día es domingo, el sábado.</summary>
    public static DateOnly FechaRecordatorio(DateOnly vencimiento)
    {
        var dia = vencimiento.AddDays(-1);
        return dia.DayOfWeek == DayOfWeek.Sunday ? vencimiento.AddDays(-2) : dia;
    }

    /// <summary>Fechas de pago cuyo recordatorio toca hoy (el sábado incluye domingo y lunes).</summary>
    public static IReadOnlyList<DateOnly> FechasARecordar(DateOnly hoy) =>
        new[] { hoy.AddDays(1), hoy.AddDays(2) }.Where(d => FechaRecordatorio(d) == hoy).ToList();
}
