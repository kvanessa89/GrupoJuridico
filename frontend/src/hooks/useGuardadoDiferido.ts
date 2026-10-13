import { GuardadoCanceladoError } from './EditorSecciones.ts'
import { useCallback, useEffect, useRef } from 'react'

/** Conserva el último borrador y pausa ante errores. Nunca reintenta un conflicto automáticamente. */
export function useGuardadoDiferido<T, R>(
  guardar: (valor: T, vigente: () => boolean) => Promise<R>,
  alGuardar: (resultado: R, hayMasCambios: boolean, enviado: T) => void,
  alFallar: (error: unknown) => void,
  espera = 600,
) {
  const revision = useRef(0)
  const pendiente = useRef<{ valor: T; revision: number } | null>(null)
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
        const r = await fns.current.guardar(p.valor, () => activo.current && revision.current === p.revision)
        if (activo.current) fns.current.alGuardar(r, pendiente.current !== null, p.valor)
      } catch (e) {
        if (!(e instanceof GuardadoCanceladoError)) {
          if (revision.current === p.revision) pendiente.current ??= p
          pausado.current = true
          if (activo.current) fns.current.alFallar(e)
        }
      } finally { vuelo.current = false }
    })
    return cola.current
  }, [])
  const programar = useCallback((valor: T) => {
    pendiente.current = { valor, revision: ++revision.current }
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
  // Retira envíos pendientes sin borrar un conflicto o error ya detectado.
  const suspender = useCallback(() => {
    revision.current++
    window.clearTimeout(temporizador.current)
    pendiente.current = null
  }, [])
  const cancelar = useCallback(() => {
    suspender()
    pausado.current = false
  }, [suspender])
  const reanudar = useCallback(() => { pausado.current = false }, [])
  const hayPendientes = useCallback(() => !!pendiente.current || vuelo.current || pausado.current, [])
  useEffect(() => {
    activo.current = true
    return () => { activo.current = false; window.clearTimeout(temporizador.current) }
  }, [])
  return { programar, flush, cancelar, suspender, reanudar, hayPendientes }
}

