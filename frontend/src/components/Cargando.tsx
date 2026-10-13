import { useEffect, useState } from 'react'

/**
 * Indicador de carga. Aparece solo si la espera pasa de un instante, así las
 * cargas rápidas no muestran un parpadeo.
 */
export function Cargando({ texto = 'Cargando…', pantalla = false }: { texto?: string; pantalla?: boolean }) {
  const [visible, setVisible] = useState(false)
  useEffect(() => {
    const t = window.setTimeout(() => setVisible(true), 200)
    return () => window.clearTimeout(t)
  }, [])
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
