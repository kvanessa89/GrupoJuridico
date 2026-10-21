import type { Catalogos } from '../api/tipos'

/** Estado "Activo": es el automático, se muestra el estado calculado con el plan de pago. */
export const ESTADO_ACTIVO = 1

/** En el filtro de Clientes, los estados calculados llevan este prefijo para distinguirlos de los elegidos. */
export const PREFIJO_CALCULADO = 'c'

/**
 * Estado que se muestra: el calculado (Al Día, Moroso, Estrella) si el cliente está Activo; si no, el elegido.
 * `clave` es la de los colores: 1–3 los calculados y 4 en adelante los elegidos.
 */
export function estadoMostrado(cats: Catalogos | null | undefined, estadoClienteId: number, calculadoId: number, respaldo = '') {
  const activo = estadoClienteId === ESTADO_ACTIVO
  const clave = activo ? calculadoId : estadoClienteId
  const lista = activo ? cats?.estadosClienteCalculado : cats?.estadosCliente
  return { clave, nombre: lista?.find((x) => x.id === clave)?.nombre ?? respaldo }
}

/** Estado calculado "Moroso" (clave 2 de `estadoMostrado`). */
export const CALCULADO_MOROSO = 2

/** Al Moroso se le agrega el estado de su promesa de pago: "Moroso con promesa pendiente" o "Moroso sin promesa". */
export function conPromesa(cats: Catalogos | null | undefined, estado: { clave: number; nombre: string }, estadoPromesaId: number | null | undefined) {
  if (estado.clave !== CALCULADO_MOROSO) return estado.nombre
  const promesa = cats?.estadosPromesa?.find((x) => x.id === estadoPromesaId)?.nombre
  return promesa ? `${estado.nombre} con promesa ${promesa.toLowerCase()}` : `${estado.nombre} sin promesa`
}

/** Opciones del filtro por estado: los calculados (clientes Activos) y luego los demás estados elegidos. */
export function opcionesFiltroEstado(cats: Catalogos | null | undefined) {
  return [
    ...(cats?.estadosClienteCalculado ?? []).map((x) => ({ v: PREFIJO_CALCULADO + x.id, l: x.nombre })),
    ...(cats?.estadosCliente ?? []).filter((x) => x.id !== ESTADO_ACTIVO).map((x) => ({ v: String(x.id), l: x.nombre })),
  ]
}
