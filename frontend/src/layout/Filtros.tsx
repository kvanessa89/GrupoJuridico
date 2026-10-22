import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import type { CobrosFiltros, FiltrosLista } from '../api/tipos'
import { mesActual } from '../utils/formato'

export type SeccionLista = 'ventas' | 'clientes'

/** Cobros abre en Clientes morosos, del mayor atraso al menor. */
const COBROS_INICIALES: CobrosFiltros = { vista: 'morosos', q: '', orden: 'atraso', asc: false, pagina: 1 }

/** Filtros iniciales: Ventas abre en el mes actual ordenada por fecha estimada; Clientes por expediente. */
// eslint-disable-next-line react-refresh/only-export-components
export function filtrosIniciales(seccion: SeccionLista): FiltrosLista {
  return {
    q: '', vendedorId: '', estadoPrimaId: '', procedenciaId: '', anio: '', origenId: '', estadoClienteId: '',
    mes: seccion === 'ventas' ? mesActual() : '',
    orden: seccion === 'ventas' ? 'fechaEstimadaPago' : 'expediente',
    asc: true,
    pagina: 1,
  }
}

interface Ctx {
  filtros: Record<SeccionLista, FiltrosLista>
  cambiar: (s: SeccionLista, cambios: Partial<FiltrosLista>) => void
  reiniciar: (s: SeccionLista | 'cobros') => void
  cobros: CobrosFiltros
  cambiarCobros: (cambios: Partial<CobrosFiltros>) => void
}

const FiltrosCtx = createContext<Ctx | null>(null)

/** Los filtros se guardan en la pestaña para sobrevivir a una recarga (F5 o una versión nueva). */
const CLAVE_FILTROS = 'gj-filtros'

interface Guardados { ventas: FiltrosLista; clientes: FiltrosLista; cobros: CobrosFiltros }

function leerGuardados(): Partial<Guardados> {
  try {
    const v = JSON.parse(sessionStorage.getItem(CLAVE_FILTROS) ?? '{}') as unknown
    return v && typeof v === 'object' ? v as Partial<Guardados> : {}
  } catch {
    return {}
  }
}

/** Al cerrar sesión, el siguiente usuario de la pestaña empieza con los filtros iniciales. */
// eslint-disable-next-line react-refresh/only-export-components
export function borrarFiltrosGuardados() {
  try { sessionStorage.removeItem(CLAVE_FILTROS) } catch { /* sin almacenamiento: no hay nada que borrar */ }
}

/** Conserva los filtros de cada tabla al entrar y volver de una ficha. */
export function FiltrosProvider({ children }: { children: ReactNode }) {
  // Lo guardado se combina con los iniciales: un filtro agregado en una versión nueva toma su valor inicial.
  const [guardados] = useState(leerGuardados)
  const [filtros, setFiltros] = useState<Record<SeccionLista, FiltrosLista>>(() => ({
    ventas: { ...filtrosIniciales('ventas'), ...guardados.ventas },
    clientes: { ...filtrosIniciales('clientes'), ...guardados.clientes },
  }))
  const cambiar = useCallback((s: SeccionLista, cambios: Partial<FiltrosLista>) => {
    setFiltros((f) => ({ ...f, [s]: { ...f[s], ...cambios } }))
  }, [])
  const [cobros, setCobros] = useState<CobrosFiltros>(() => ({ ...COBROS_INICIALES, ...guardados.cobros }))
  const cambiarCobros = useCallback((cambios: Partial<CobrosFiltros>) => setCobros((f) => ({ ...f, ...cambios })), [])
  const reiniciar = useCallback((s: SeccionLista | 'cobros') => {
    if (s === 'cobros') setCobros(COBROS_INICIALES)
    else setFiltros((f) => ({ ...f, [s]: filtrosIniciales(s) }))
  }, [])
  useEffect(() => {
    try {
      sessionStorage.setItem(CLAVE_FILTROS, JSON.stringify({ ...filtros, cobros }))
    } catch { /* sin almacenamiento: los filtros solo duran mientras la página está abierta */ }
  }, [filtros, cobros])
  const valor = useMemo(() => ({ filtros, cambiar, reiniciar, cobros, cambiarCobros }), [filtros, cambiar, reiniciar, cobros, cambiarCobros])
  return <FiltrosCtx.Provider value={valor}>{children}</FiltrosCtx.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useFiltros(seccion: SeccionLista) {
  const c = useContext(FiltrosCtx)
  if (!c) throw new Error('useFiltros fuera de FiltrosProvider')
  const { filtros, cambiar, reiniciar } = c
  return {
    filtros: filtros[seccion],
    /** Cambiar un filtro vuelve a la página 1, salvo que se cambie la página misma. */
    set: useCallback(
      (cambios: Partial<FiltrosLista>) => cambiar(seccion, 'pagina' in cambios ? cambios : { ...cambios, pagina: 1 }),
      [cambiar, seccion],
    ),
    limpiar: useCallback(
      () => cambiar(seccion, { q: '', vendedorId: '', estadoPrimaId: '', procedenciaId: '', mes: '', anio: '', origenId: '', estadoClienteId: '', pagina: 1 }),
      [cambiar, seccion],
    ),
    reiniciar: useCallback(() => reiniciar(seccion), [reiniciar, seccion]),
  }
}

/** Filtros de Cobros: la tarjeta elegida (vista), la búsqueda, el orden y la página. */
// eslint-disable-next-line react-refresh/only-export-components
export function useFiltrosCobros() {
  const c = useContext(FiltrosCtx)
  if (!c) throw new Error('useFiltrosCobros fuera de FiltrosProvider')
  const { cobros, cambiarCobros } = c
  return {
    filtros: cobros,
    set: useCallback(
      (cambios: Partial<CobrosFiltros>) => cambiarCobros('pagina' in cambios ? cambios : { ...cambios, pagina: 1 }),
      [cambiarCobros],
    ),
  }
}

// eslint-disable-next-line react-refresh/only-export-components
export function useReiniciarFiltros() {
  const c = useContext(FiltrosCtx)
  if (!c) throw new Error('useReiniciarFiltros fuera de FiltrosProvider')
  return c.reiniciar
}
