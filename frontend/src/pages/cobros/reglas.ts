// Cálculos de pantalla del plan de pago. Las reglas que valen (mora, estados, cuotas) las aplica la API;
// aquí solo se arma la vista previa y los textos.

import type { CuotaDto, PlanDetalle } from '../../api/tipos'

const pad = (n: number) => String(n).padStart(2, '0')

/** Día `dia` del mes (base 1, puede pasar de 12); en meses más cortos, el último día. */
export function fechaDia(anio: number, mes: number, dia: number): string {
  const d = new Date(Date.UTC(anio, mes - 1, 1))
  const a = d.getUTCFullYear()
  const m = d.getUTCMonth() + 1
  const ultimo = new Date(Date.UTC(a, m, 0)).getUTCDate()
  return `${a}-${pad(m)}-${pad(Math.min(dia, ultimo))}`
}

/** Vencimiento de la primera cuota: el día de pago del mes de inicio o, si ya pasó, del mes siguiente. */
export function primeraCuota(fechaInicio: string, diaPago: number): string {
  const [a, m] = fechaInicio.split('-').map(Number)
  const enMes = fechaDia(a, m, diaPago)
  return enMes < fechaInicio ? fechaDia(a, m + 1, diaPago) : enMes
}

export interface DatosPlan {
  total: string
  cuotas: string
  inicio: string
  dia: string
}

/** Datos del formulario ya convertidos, o null si falta algo o está fuera de rango. */
export function planValido(d: DatosPlan): { total: number; cuotas: number; inicio: string; dia: number } | null {
  const total = Number(d.total)
  const cuotas = Number(d.cuotas)
  const dia = Number(d.dia)
  const ok = total > 0 && Number.isInteger(cuotas) && cuotas >= 1 && cuotas <= 120
    && Number.isInteger(dia) && dia >= 1 && dia <= 31 && /^\d{4}-\d{2}-\d{2}$/.test(d.inicio)
  return ok ? { total, cuotas, inicio: d.inicio, dia } : null
}

/** Monto mensual: total entre cuotas, redondeado (la última cuota ajusta la diferencia). */
export function montoMensual(total: number, cuotas: number): number {
  return Math.round(total / cuotas)
}

/** "Promesas: 1 pendiente, 2 cumplidas" o "Sin promesas de pago". */
export function textoPromesas(p: Pick<PlanDetalle, 'promesasPendientes' | 'promesasCumplidas' | 'promesasIncumplidas'>): string {
  const partes = ([
    [p.promesasPendientes, 'pendiente'],
    [p.promesasCumplidas, 'cumplida'],
    [p.promesasIncumplidas, 'incumplida'],
  ] as const).filter(([n]) => n > 0).map(([n, t]) => n + ' ' + t + (n === 1 ? '' : 's'))
  return partes.length ? 'Promesas: ' + partes.join(', ') : 'Sin promesas de pago'
}

/** Saldo de la cuota al registrar un pago, cobrando o no el interés de morosidad. */
export function saldoParaPago(c: Pick<CuotaDto, 'montoCuota' | 'moraPosible' | 'pagado'>, cobrarMora: boolean): number {
  return Math.max(0, c.montoCuota + (cobrarMora ? c.moraPosible : 0) - c.pagado)
}

/** La promesa que se muestra de una cuota: la pendiente o, si no hay, la última. */
export function promesaVisible<T extends { estadoId: number }>(promesas: T[]): T | undefined {
  return promesas.find((p) => p.estadoId === 1) ?? promesas[promesas.length - 1]
}
