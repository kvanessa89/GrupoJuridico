const MESES = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'set', 'oct', 'nov', 'dic']
const MESES_LARGOS = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Setiembre', 'Octubre', 'Noviembre', 'Diciembre']

const DIAS = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado']

/** País por defecto de un teléfono nuevo: el primero del catálogo (Costa Rica). */
export function paisPorDefecto(paises: { id: number }[] | undefined): number | null {
  return paises?.[0]?.id ?? null
}

/** Los teléfonos solo aceptan dígitos; el prefijo va en el país. */
export function soloDigitos(v: string): string {
  return v.replace(/\D/g, '')
}

/**
 * Texto de un campo de monto → valor crudo: solo dígitos y un punto, hasta 2 decimales.
 * "1,250,000.509" → "1250000.50"; "007" → "7".
 */
export function limpiarMonto(texto: string): string {
  const [entero = '', ...resto] = texto.replace(/[^\d.]/g, '').split('.')
  const enteroLimpio = entero.replace(/^0+(?=\d)/, '')
  return resto.length ? (enteroLimpio || '0') + '.' + resto.join('').slice(0, 2) : enteroLimpio
}

/** Valor crudo → texto del campo: miles con coma y decimales con punto. "1250000.5" → "1,250,000.5" */
export function formatearMonto(crudo: string): string {
  const [entero, decimales] = crudo.split('.')
  const conMiles = entero.replace(/\B(?=(\d{3})+(?!\d))/g, ',')
  return decimales === undefined ? conMiles : conMiles + '.' + decimales
}

/** "+506 88124477" → "tel:+50688124477" */
export function enlaceTel(numeroCompleto: string): string {
  return 'tel:' + numeroCompleto.replace(/[^\d+]/g, '')
}

/** 2026-04-12 → "12 abr 2026" */
export function fFecha(iso?: string | null): string {
  if (!iso) return '—'
  const p = iso.slice(0, 10).split('-')
  if (p.length < 3) return iso
  return Number(p[2]) + ' ' + MESES[Number(p[1]) - 1] + ' ' + p[0]
}

/** 2026-10-16 → "viernes 16 oct" */
export function fDiaLargo(iso?: string | null): string {
  if (!iso) return '—'
  const [a, m, d] = iso.slice(0, 10).split('-').map(Number)
  return DIAS[new Date(Date.UTC(a, m - 1, d)).getUTCDay()] + ' ' + d + ' ' + MESES[m - 1]
}

/** "1 día" / "3 días" (y lo mismo para cuotas, pagos, etc.). */
export function plural(n: number, singular: string, pluralTexto = singular + 's'): string {
  return n + ' ' + (n === 1 ? singular : pluralTexto)
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

/** 1250000.5 → "1,250,000.50": miles con coma y punto decimal; sin decimales si el monto es entero. */
export function fNumero(n?: number | null): string {
  const valor = Number(n || 0)
  const decimales = Number.isInteger(valor) ? 0 : 2
  return valor.toLocaleString('en-US', { minimumFractionDigits: decimales, maximumFractionDigits: 2 })
}

/** Igual que fNumero, con el símbolo de colones (tablas y totales). */
export function fMonto(n?: number | null): string {
  return '₡' + fNumero(n)
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
