import { useEffect, useState } from 'react'

/**
 * Indicador de carga. Aparece solo si la espera pasa de un instante, así las
 * cargas rápidas no muestran un parpadeo; con `inmediato` aparece de una vez.
 */
export function Cargando({ texto = 'Cargando…', pantalla = false, inmediato = false }: { texto?: string; pantalla?: boolean; inmediato?: boolean }) {
  const [visible, setVisible] = useState(inmediato)
  useEffect(() => {
    if (inmediato) return
    const t = window.setTimeout(() => setVisible(true), 200)
    return () => window.clearTimeout(t)
  }, [inmediato])
  return (
    <div className={'cargando-caja' + (pantalla ? ' pantalla' : '')} role="status" aria-live="polite">
      {visible && (
        <>
          <span className="cargando-giro" aria-hidden="true" />
          <span>{texto}</span>
        </>
      )}
    </div>
  )
}
