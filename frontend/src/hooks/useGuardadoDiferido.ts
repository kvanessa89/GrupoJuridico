import { useCallback, useEffect, useRef } from 'react'

/** Conserva el último borrador y pausa ante errores. Nunca reintenta un conflicto automáticamente. */
export function useGuardadoDiferido<T, R>(
  guardar: (valor: T) => Promise<R>,
  alGuardar: (resultado: R, hayMasCambios: boolean, enviado: T) => void,
  alFallar: (error: unknown) => void,
  espera = 600,
) {
  const pendiente = useRef<{ valor: T } | null>(null)
  const temporizador = useRef<number | undefined>(undefined)
  const cola = useRef<Promise<void>>(Promise.resolve())
  const pausado = useRef(false)
  const activo = useRef(true)
  const vuelo = useRef(false)
  const fns = useRef({ guardar, alGuardar, alFallar })
  useEffect(() => { fns.current = { guardar, alGuardar, alFallar } })

  const ejecutar = useCallback(() => {
    cola.current = cola.current.then(async () => {
      if (pausado.current || !activo.current) return
      const p = pendiente.current
      if (!p) return
      pendiente.current = null
      vuelo.current = true
      try {
        const r = await fns.current.guardar(p.valor)
        if (activo.current) fns.current.alGuardar(r, pendiente.current !== null, p.valor)
      } catch (e) {
        pendiente.current ??= p
        pausado.current = true
        if (activo.current) fns.current.alFallar(e)
      } finally { vuelo.current = false }
    })
    return cola.current
  }, [])
  const programar = useCallback((valor: T) => {
    pendiente.current = { valor }
    window.clearTimeout(temporizador.current)
    if (!pausado.current) temporizador.current = window.setTimeout(ejecutar, espera)
  }, [ejecutar, espera])
  const flush = useCallback(async () => {
    window.clearTimeout(temporizador.current)
    await ejecutar()
    // Un cambio puede haberse añadido durante la solicitud anterior.
    if (pendiente.current && !pausado.current) await ejecutar()
    return !pendiente.current && !pausado.current
  }, [ejecutar])
  const cancelar = useCallback(() => {
    window.clearTimeout(temporizador.current)
    pendiente.current = null
    pausado.current = false
  }, [])
  const reanudar = useCallback(() => { pausado.current = false }, [])
  const hayPendientes = useCallback(() => !!pendiente.current || vuelo.current || pausado.current, [])
  useEffect(() => {
    activo.current = true
    return () => { activo.current = false; window.clearTimeout(temporizador.current) }
  }, [])
  return { programar, flush, cancelar, reanudar, hayPendientes }
}
