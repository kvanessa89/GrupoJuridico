import type { CSSProperties } from 'react'

const GRIS: [string, string] = ['rgba(168,162,158,0.16)', '#78716C']

/** Colores del estado de la prima: rojo pendiente, ámbar incompleta, verde pagada. */
const PRIMA: Record<number, [string, string]> = {
  1: ['rgba(220,38,38,0.14)', '#C2544A'],
  2: ['rgba(217,119,6,0.16)', '#B4780C'],
  3: ['rgba(22,163,74,0.14)', '#2E7D4F'],
}

/** Colores del estado del cliente en la tabla. */
const CLIENTE_TABLA: Record<number, [string, string]> = {
  1: ['rgba(22,163,74,0.14)', '#2E7D4F'],
  2: ['rgba(180,35,24,0.12)', '#B42318'],
  3: ['rgba(232,193,88,0.2)', '#B8862B'],
  4: ['rgba(91,141,214,0.15)', '#5B8DD6'],
}

/** Colores del estado del cliente en la cabecera de la ficha. */
const CLIENTE_FICHA: Record<number, [string, string]> = {
  1: ['rgba(22,163,74,0.14)', '#2E7D4F'],
  2: ['rgba(180,35,24,0.12)', '#B42318'],
  3: ['rgba(232,193,88,0.2)', '#8C6A10'],
  4: ['rgba(91,141,214,0.15)', '#2F5FA8'],
  5: ['rgba(210,116,78,0.15)', '#A4471F'],
  6: ['rgba(168,162,158,0.2)', '#57534E'],
}

const colores = (c: [string, string]): CSSProperties => ({ background: c[0], color: c[1] })

export function ChipPrima({ estadoId, texto }: { estadoId: number | null | undefined; texto: string }) {
  return <span className="chip" style={colores(PRIMA[estadoId ?? 0] ?? ['rgba(168,162,158,0.16)', '#78716C'])}>{texto}</span>
}

export function ChipEstadoCliente({ estadoId, texto }: { estadoId: number; texto: string }) {
  return <span className="chip" style={colores(CLIENTE_TABLA[estadoId] ?? ['rgba(168,162,158,0.16)', '#A8A29E'])}>{texto}</span>
}

export function ChipTipo({ esCliente }: { esCliente: boolean }) {
  return (
    <span className="chip" style={esCliente ? { background: 'rgba(22,163,74,0.14)', color: '#2E7D4F' } : { background: 'rgba(232,193,88,0.16)', color: '#B8862B' }}>
      {esCliente ? 'Cliente' : 'Prospecto'}
    </span>
  )
}

export function EtiquetaPrimaGrande({ estadoId, texto }: { estadoId: number; texto: string }) {
  return <span className="etiqueta-grande" style={colores(PRIMA[estadoId] ?? GRIS)}>{texto}</span>
}

export function EtiquetaEstadoGrande({ estadoId, texto }: { estadoId: number; texto: string }) {
  return <span className="etiqueta-grande" style={colores(CLIENTE_FICHA[estadoId] ?? CLIENTE_FICHA[6])}>{texto}</span>
}
