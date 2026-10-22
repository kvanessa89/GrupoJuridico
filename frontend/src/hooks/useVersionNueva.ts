import { useEffect, useRef, useState } from 'react'
import { useLocation } from 'react-router'

/** Versión de este build; vacía en desarrollo y en las pruebas (no hay version.json). */
export const VERSION_ACTUAL = typeof __VERSION_APP__ === 'undefined' ? '' : __VERSION_APP__

/** Cada cuánto se revisa si se publicó una versión nueva (además de al volver a la pestaña). */
const INTERVALO = 5 * 60 * 1000

/** Compara la versión publicada (/version.json, sin caché) con la de este build. Un error cuenta como "sin cambios". */
export async function hayVersionNueva(actual = VERSION_ACTUAL, obtener: typeof fetch = fetch): Promise<boolean> {
  if (!actual) return false
  try {
    const r = await obtener('/version.json', { cache: 'no-store' })
    if (!r.ok) return false
    const { version } = await r.json() as { version?: unknown }
    return typeof version === 'string' && version !== '' && version !== actual
  } catch {
    return false
  }
}

/**
 * Detecta un deploy nuevo y lo carga al cambiar de pantalla, sin que nadie tenga que refrescar.
 * Recargar al cambiar de dirección es seguro: la ficha guarda sus cambios antes de dejar salir.
 * Devuelve si hay una versión nueva esperando, para mostrar el aviso.
 */
export function useVersionNueva({
  actual = VERSION_ACTUAL,
  revisarVersion = hayVersionNueva,
  recargar = () => window.location.reload(),
}: { actual?: string; revisarVersion?: (actual: string) => Promise<boolean>; recargar?: () => void } = {}) {
  const [nueva, setNueva] = useState(false)
  const { pathname } = useLocation()
  const anterior = useRef(pathname)

  useEffect(() => {
    if (nueva || !actual) return
    let vivo = true
    const revisar = async () => { if (await revisarVersion(actual) && vivo) setNueva(true) }
    const alVolver = () => { if (document.visibilityState === 'visible') void revisar() }
    const t = window.setInterval(revisar, INTERVALO)
    document.addEventListener('visibilitychange', alVolver)
    return () => {
      vivo = false
      window.clearInterval(t)
      document.removeEventListener('visibilitychange', alVolver)
    }
  }, [nueva, actual, revisarVersion])

  const recargarRef = useRef(recargar)
  useEffect(() => { recargarRef.current = recargar })
  useEffect(() => {
    if (nueva && pathname !== anterior.current) recargarRef.current()
    anterior.current = pathname
  }, [nueva, pathname])

  return nueva
}
