import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router'
import { mensajeDe } from '../../api/cliente'
import { api } from '../../api/endpoints'
import type { PlanPersona } from '../../api/tipos'
import { Cargando } from '../../components/Cargando'
import { CrearPlanForm, InfoPlan, TablaCuotas } from './PlanPago'

/**
 * Plan de pago dentro de la pestaña Ventas de la ficha: solo lectura. Administrador y Cobros ven el enlace
 * al detalle del plan o, si no tiene, el formulario para crearlo (con los campos vacíos).
 */
export function PlanPagoFicha({ personaId }: { personaId: number }) {
  const navegar = useNavigate()
  const [datos, setDatos] = useState<PlanPersona | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    let vivo = true
    api.planPago(personaId)
      .then((d) => vivo && setDatos(d))
      .catch((e) => vivo && setError(mensajeDe(e, 'No se pudo cargar el plan de pago.')))
    return () => {
      vivo = false
    }
  }, [personaId])

  const plan = datos?.plan
  return (
    <div className="tarjeta-seccion">
      <div className="cabecera-seccion">
        <span className="titulo-seccion">Plan de pago</span>
        {datos?.puedeGestionar && plan && (
          <button className="btn-enlace" onClick={() => navegar(`/gestion/clientes/${personaId}/plan`)}>Ver detalle del plan</button>
        )}
      </div>
      {error && <p className="texto-error" style={{ margin: 0 }}>{error}</p>}
      {!datos && !error && <Cargando />}
      {datos && !plan && (datos.puedeGestionar ? (
        <CrearPlanForm
          personaId={personaId}
          texto="Este cliente no tiene plan de pago. Al generarlo se crean todas las cuotas con su fecha de vencimiento."
          inicial={{ total: '', cuotas: '', inicio: '', dia: '' }}
          onCreado={setDatos}
        />
      ) : <span className="vacio">Este cliente no tiene plan de pago.</span>)}
      {plan && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <InfoPlan plan={plan} />
          <div className="cuotas-caja">
            <TablaCuotas cuotas={plan.cuotas} />
          </div>
        </div>
      )}
    </div>
  )
}
