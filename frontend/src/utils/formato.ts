const MESES = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'set', 'oct', 'nov', 'dic']
const MESES_LARGOS = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Setiembre', 'Octubre', 'Noviembre', 'Diciembre']

/** 2026-04-12 → "12 abr 2026" */
export function fFecha(iso?: string | null): string {
  if (!iso) return '—'
  const p = iso.slice(0, 10).split('-')
  if (p.length < 3) return iso
  return Number(p[2]) + ' ' + MESES[Number(p[1]) - 1] + ' ' + p[0]
}

/** 2026-04-12 → "12/04/26" */
export function fCorta(iso?: string | null): string {
  if (!iso) return 'Sin fecha'
  const p = iso.slice(0, 10).split('-')
  if (p.length < 3) return iso
  return p[2] + '/' + p[1] + '/' + p[0].slice(2)
}

/** "2026-10" → "Octubre 2026" */
export function fMes(m: string): string {
  return MESES_LARGOS[Number(m.slice(5, 7)) - 1] + ' ' + m.slice(0, 4)
}

export function fMonto(n?: number | null): string {
  return '₡' + Number(n || 0).toLocaleString('es-CR')
}

/** Convierte lo escrito en un campo de monto a número (ignora separadores y símbolos). */
export function num(v: unknown): number {
  const n = Number(String(v ?? '').replace(/[^\d.-]/g, ''))
  return Number.isNaN(n) ? 0 : n
}

export function iniciales(a?: string, b?: string): string {
  return ((a || ' ')[0] + (b || ' ')[0]).trim().toUpperCase()
}

/** Fecha local de hoy en formato ISO (yyyy-MM-dd). */
export function hoyIso(): string {
  const d = new Date()
  return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0')
}

export function mesActual(): string {
  return hoyIso().slice(0, 7)
}

export function vendedorEtiqueta(v: { codigo: string; nombre: string }): string {
  return 'Asesor ' + (v.codigo ? v.codigo + ' - ' : '') + v.nombre
}

export function origenEtiqueta(o: { codigo: string; nombre: string }): string {
  return o.codigo + ' - ' + o.nombre
}

/** Estado de la prima según montos: 1 pendiente, 2 incompleta, 3 pagada. */
export function estadoDePrima(monto: number, cancelado: number): number {
  if (cancelado <= 0) return 1
  if (cancelado < monto) return 2
  return 3
}
