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
