import { useCallback, useEffect, useRef, useState } from 'react'
import type { FiltrosLista } from '../api/tipos'

/**
 * Carga una tabla paginada cada vez que cambian los filtros. El buscador espera
 * un momento antes de consultar y las respuestas viejas se descartan.
 */
export function useListado<T>(cargar: (f: FiltrosLista) => Promise<T>, filtros: FiltrosLista) {
  const [datos, setDatos] = useState<T | null>(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState(false)
  const turno = useRef(0)
  const qPrevia = useRef(filtros.q)
  const [recarga, setRecarga] = useState(0)

  useEffect(() => {
    const mio = ++turno.current
    const espera = filtros.q !== qPrevia.current ? 250 : 0
    qPrevia.current = filtros.q
    const t = window.setTimeout(() => {
      setCargando(true)
      cargar(filtros)
        .then((d) => {
          if (mio !== turno.current) return
          setDatos(d)
          setError(false)
        })
        .catch(() => mio === turno.current && setError(true))
        .finally(() => mio === turno.current && setCargando(false))
    }, espera)
    return () => window.clearTimeout(t)
  }, [cargar, filtros, recarga])

  const recargar = useCallback(() => setRecarga((n) => n + 1), [])
  return { datos, cargando, error, recargar }
}
