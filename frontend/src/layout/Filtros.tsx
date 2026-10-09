import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import type { FiltrosLista } from '../api/tipos'
import { mesActual } from '../utils/formato'

export type SeccionLista = 'ventas' | 'clientes'

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
  reiniciar: (s: SeccionLista) => void
}

const FiltrosCtx = createContext<Ctx | null>(null)

/** Conserva los filtros de cada tabla al entrar y volver de una ficha. */
export function FiltrosProvider({ children }: { children: ReactNode }) {
  const [filtros, setFiltros] = useState<Record<SeccionLista, FiltrosLista>>({
    ventas: filtrosIniciales('ventas'),
    clientes: filtrosIniciales('clientes'),
  })
  const cambiar = useCallback((s: SeccionLista, cambios: Partial<FiltrosLista>) => {
    setFiltros((f) => ({ ...f, [s]: { ...f[s], ...cambios } }))
  }, [])
  const reiniciar = useCallback((s: SeccionLista) => {
    setFiltros((f) => ({ ...f, [s]: filtrosIniciales(s) }))
  }, [])
  const valor = useMemo(() => ({ filtros, cambiar, reiniciar }), [filtros, cambiar, reiniciar])
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

// eslint-disable-next-line react-refresh/only-export-components
export function useReiniciarFiltros() {
  const c = useContext(FiltrosCtx)
  if (!c) throw new Error('useReiniciarFiltros fuera de FiltrosProvider')
  return c.reiniciar
}
