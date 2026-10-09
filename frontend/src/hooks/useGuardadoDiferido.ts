import { useCallback, useEffect, useRef } from 'react'

/**
 * Autoguardado: agrupa los cambios rápidos de un campo y los envía uno a la vez, en orden.
 * `flush` envía lo pendiente de inmediato (por ejemplo antes de volver a la tabla).
 */
export function useGuardadoDiferido<T, R>(
  guardar: (valor: T) => Promise<R>,
  alGuardar: (resultado: R, hayMasCambios: boolean) => void,
  alFallar: (error: unknown) => void,
  espera = 600,
) {
  const pendiente = useRef<{ valor: T } | null>(null)
  const temporizador = useRef<number | undefined>(undefined)
  const cola = useRef<Promise<void>>(Promise.resolve())
  const fns = useRef({ guardar, alGuardar, alFallar })

  useEffect(() => {
    fns.current = { guardar, alGuardar, alFallar }
  })

  const ejecutar = useCallback(() => {
    cola.current = cola.current.then(async () => {
      const p = pendiente.current
      pendiente.current = null
      if (!p) return
      try {
        const r = await fns.current.guardar(p.valor)
        fns.current.alGuardar(r, pendiente.current !== null)
      } catch (e) {
        fns.current.alFallar(e)
      }
    })
    return cola.current
  }, [])

  const programar = useCallback(
    (valor: T) => {
      pendiente.current = { valor }
      window.clearTimeout(temporizador.current)
      temporizador.current = window.setTimeout(ejecutar, espera)
    },
    [ejecutar, espera],
  )

  const flush = useCallback(() => {
    window.clearTimeout(temporizador.current)
    return ejecutar()
  }, [ejecutar])

  // Si se sale de la ficha con cambios en espera, se envían igual.
  useEffect(() => () => void flush(), [flush])

  return { programar, flush }
}
