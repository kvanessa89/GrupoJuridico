using GrupoJuridico.Gestion.Domain.Entities;

namespace GrupoJuridico.Gestion.Application.Tests;

public class ReglasCobroTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 15);

    [Fact]
    public void Genera_cuotas_mensuales_y_la_ultima_absorbe_el_redondeo()
    {
        var plan = new PlanPago { MontoTotal = 1000m, CantidadCuotas = 3, MontoCuota = ReglasCobro.MontoMensual(1000m, 3), FechaInicio = new DateOnly(2026, 1, 10), DiaPago = 15 };
        var cuotas = ReglasCobro.GenerarCuotas(plan);

        Assert.Equal(new[] { 333m, 333m, 334m }, cuotas.Select(c => c.MontoCuota));
        Assert.Equal(new[] { new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 15), new DateOnly(2026, 3, 15) }, cuotas.Select(c => c.FechaVencimiento));
        Assert.Equal(new[] { 1, 2, 3 }, cuotas.Select(c => c.NumeroCuota));
    }

    [Fact]
    public void Si_el_dia_de_pago_ya_paso_empieza_el_mes_siguiente_y_respeta_meses_cortos()
    {
        var plan = new PlanPago { MontoTotal = 300m, CantidadCuotas = 3, MontoCuota = 100m, FechaInicio = new DateOnly(2026, 12, 31), DiaPago = 30 };
        var cuotas = ReglasCobro.GenerarCuotas(plan);
        Assert.Equal(new[] { new DateOnly(2027, 1, 30), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 30) }, cuotas.Select(c => c.FechaVencimiento));
    }

    private static Cuota CuotaDe(decimal monto, DateOnly vence, params (decimal Monto, DateOnly Fecha)[] pagos)
    {
        var c = new Cuota { MontoCuota = monto, FechaVencimiento = vence };
        foreach (var p in pagos) c.Pagos.Add(new Pago { MontoPago = p.Monto, FechaPago = p.Fecha });
        return c;
    }

    [Fact]
    public void La_mora_solo_aplica_a_cuotas_vencidas_sin_pago_completo_a_tiempo()
    {
        Assert.Equal(0m, ReglasCobro.Mora(CuotaDe(300000m, Hoy), 2.5m, Hoy));
        Assert.Equal(7500m, ReglasCobro.Mora(CuotaDe(300000m, Hoy.AddDays(-1)), 2.5m, Hoy));
        Assert.Equal(0m, ReglasCobro.Mora(CuotaDe(300000m, Hoy.AddDays(-1), (300000m, Hoy.AddDays(-1))), 2.5m, Hoy));
        Assert.Equal(7500m, ReglasCobro.Mora(CuotaDe(300000m, Hoy.AddDays(-5), (300000m, Hoy)), 2.5m, Hoy));

        var acordada = CuotaDe(300000m, Hoy.AddDays(-1));
        acordada.SinMora = true;
        Assert.Equal(0m, ReglasCobro.Mora(acordada, 2.5m, Hoy));
        Assert.Equal(7500m, ReglasCobro.Mora(acordada, 2.5m, Hoy, ignorarAcuerdo: true));
    }

    [Fact]
    public void Una_cuota_vencida_solo_queda_pagada_al_cubrir_el_interes()
    {
        var c = CuotaDe(300000m, Hoy.AddDays(-3), (300000m, Hoy));
        ReglasCobro.RecalcularCuota(c, 2.5m, Hoy);
        Assert.Equal(EstadoCuotaPago.Parcial, c.EstadoCuotaPagoId);
        Assert.Equal(EstadoCuotaVencimiento.Vencido, c.EstadoCuotaVencimientoId);

        c.Pagos.Add(new Pago { MontoPago = 7500m, FechaPago = Hoy });
        ReglasCobro.RecalcularCuota(c, 2.5m, Hoy);
        Assert.Equal(EstadoCuotaPago.Pagada, c.EstadoCuotaPagoId);
        Assert.Equal(EstadoCuotaVencimiento.AlDia, c.EstadoCuotaVencimientoId);
    }

    [Theory]
    [InlineData(100, 100, 0, EstadoPromesa.Cumplida)]
    [InlineData(100, 50, -1, EstadoPromesa.Incumplida)]
    [InlineData(100, 50, 0, EstadoPromesa.Pendiente)]
    public void Estado_de_la_promesa(decimal prometido, decimal pagado, int dias, int esperado)
    {
        var p = new PromesaPago { MontoPrometido = prometido, MontoPagado = pagado, FechaPromesa = Hoy.AddDays(dias) };
        ReglasCobro.RecalcularPromesa(p, Hoy);
        Assert.Equal(esperado, p.EstadoPromesaId);
    }

    private static List<Cuota> Cuotas(params (int Pago, int Venc)[] estados) =>
        estados.Select(e => new Cuota { EstadoCuotaPagoId = e.Pago, EstadoCuotaVencimientoId = e.Venc }).ToList();

    [Fact]
    public void Estado_del_cliente_segun_su_plan()
    {
        var alDia = Cuotas((EstadoCuotaPago.Pagada, 1), (EstadoCuotaPago.Pendiente, 1));
        var vencido = Cuotas((EstadoCuotaPago.Pagada, 1), (EstadoCuotaPago.Pendiente, EstadoCuotaVencimiento.Vencido));
        var pagado = Cuotas((EstadoCuotaPago.Pagada, 1), (EstadoCuotaPago.Pagada, 1));

        Assert.Equal(EstadoCliente.Moroso, ReglasCobro.EstadoClientePara(EstadoCliente.AlDia, vencido));
        Assert.Equal(EstadoCliente.AlDia, ReglasCobro.EstadoClientePara(EstadoCliente.Moroso, alDia));
        Assert.Equal(EstadoCliente.Estrella, ReglasCobro.EstadoClientePara(EstadoCliente.Moroso, pagado));
        Assert.Equal(EstadoCliente.Moroso, ReglasCobro.EstadoClientePara(EstadoCliente.Estrella, vencido));
        Assert.Equal(EstadoCliente.AlDia, ReglasCobro.EstadoClientePara(EstadoCliente.Estrella, alDia));
        Assert.Equal(EstadoCliente.Terminado, ReglasCobro.EstadoClientePara(EstadoCliente.Terminado, vencido));
        Assert.Equal(EstadoCliente.Abandonado, ReglasCobro.EstadoClientePara(EstadoCliente.Abandonado, pagado));
        Assert.Equal(EstadoCliente.SinExpediente, ReglasCobro.EstadoClientePara(EstadoCliente.SinExpediente, alDia));
    }

    [Fact]
    public void Recordatorios_un_dia_antes_y_los_del_lunes_el_sabado()
    {
        var sabado = new DateOnly(2026, 10, 17);
        Assert.Equal(DayOfWeek.Saturday, sabado.DayOfWeek);
        Assert.Equal(new[] { sabado.AddDays(1), sabado.AddDays(2) }, ReglasCobro.FechasARecordar(sabado));
        Assert.Empty(ReglasCobro.FechasARecordar(sabado.AddDays(1)));
        Assert.Equal(new[] { new DateOnly(2026, 10, 16) }, ReglasCobro.FechasARecordar(new DateOnly(2026, 10, 15)));
        Assert.Equal(sabado, ReglasCobro.FechaRecordatorio(sabado.AddDays(2)));
    }
}
