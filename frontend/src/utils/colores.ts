import type { CSSProperties } from 'react'

/** Colores fijos por usuario para el círculo de iniciales en Comentarios. */
const PALETA_AUTOR: [string, string][] = [
  ['#E8C158', '#3B2F0B'], ['#5FA37A', '#FFFFFF'], ['#5B8DD6', '#FFFFFF'],
  ['#D2744E', '#FFFFFF'], ['#9A7BC8', '#FFFFFF'], ['#3FA3A0', '#FFFFFF'],
]

export function colorAutor(usuarioId: number): CSSProperties {
  const c = PALETA_AUTOR[(usuarioId + PALETA_AUTOR.length - 1) % PALETA_AUTOR.length]
  return { background: c[0], color: c[1] }
}

export type Tono = 'verde' | 'ambar' | 'rojo' | 'gris'

const TONOS: Record<Tono, [string, string]> = {
  verde: ['rgba(22,163,74,0.14)', '#2E7D4F'],
  ambar: ['rgba(217,119,6,0.16)', '#B4780C'],
  rojo: ['rgba(220,38,38,0.14)', '#C2544A'],
  gris: ['rgba(168,162,158,0.18)', '#D6D3D1'],
}

/** Tonos de los estados de cobros (ids fijos). */
export const TONO_PAGO: Record<number, Tono> = { 1: 'gris', 2: 'ambar', 3: 'verde' }
export const TONO_VENCIMIENTO: Record<number, Tono> = { 1: 'verde', 2: 'rojo' }
export const TONO_PROMESA: Record<number, Tono> = { 1: 'ambar', 2: 'verde', 3: 'rojo' }

/** Fondo y texto de un tono (chips e íconos de estado). */
export function estiloTono(tono: Tono | undefined): CSSProperties {
  const c = TONOS[tono ?? 'gris'] ?? TONOS.gris
  return { background: c[0], color: c[1] }
}
