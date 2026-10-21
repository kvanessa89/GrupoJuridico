import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { mensajeDe } from '../../api/cliente'
import { api } from '../../api/endpoints'
import type { PlanPersona } from '../../api/tipos'
import { useSesion } from '../../auth/Sesion'
import { EtiquetaEstadoGrande } from '../../components/Chips'
import { estadoMostrado } from '../../utils/estadoCliente'
import { Cargando } from '../../components/Cargando'
import { Atras, Check } from '../../components/Iconos'
import { fFecha, iniciales, plural } from '../../utils/formato'
import { CrearPlanForm, InfoPlan, ModalCuota, TablaCuotas, type FormCuota } from './PlanPago'

/** Título de la persona como en la ficha: "Expediente | Nombre". */
function tituloPersona(p: Pick<PlanPersona, 'expediente' | 'nombres' | 'apellidos'>) {
  const nombre = (p.nombres + ' ' + p.apellidos).trim()
  return (p.expediente.trim() || 'Sin expediente') + ' | ' + (nombre || 'Sin nombre')
}

/**
 * Plan de pago de un cliente: información, cuotas con pagos y promesas, y los modales para registrarlos.
 * `desde` indica a dónde vuelve: a la tabla de Cobros o a la ficha del cliente.
 */
export function PlanPagoPage({ desde }: { desde: 'cobros' | 'ficha' }) {
  const { id } = useParams()
  const personaId = Number(id)
  const navegar = useNavigate()
  const { catalogos } = useSesion()
  const [datos, setDatos] = useState<PlanPersona | null>(null)
  const [error, setError] = useState('')
  const [form, setForm] = useState<FormCuota | null>(null)
  const [guardado, setGuardado] = useState(false)

  useEffect(() => {
    let vivo = true
    api.planPago(personaId)
      .then((d) => vivo && setDatos(d))
      .catch((e) => vivo && setError(mensajeDe(e, 'No se pudo cargar el plan de pago.')))
    return () => {
      vivo = false
    }
  }, [personaId])

  const verFicha = () => navegar(`/gestion/clientes/${personaId}`, { state: { pestana: 'ventas' } })
  const volver = () => (desde === 'cobros' ? navegar('/gestion/cobros') : verFicha())
  const actualizar = (d: PlanPersona) => {
    setDatos(d)
    setForm(null)
    setGuardado(true)
  }

  if (error) {
    return (
      <div className="contenedor-ficha">
        <button className="volver" onClick={volver}><Atras /><span>{desde === 'cobros' ? 'Volver a cobros' : 'Volver a la ficha del cliente'}</span></button>
        <div role="alert" className="alerta">{error}</div>
      </div>
    )
  }
  if (!datos) return <div className="contenedor-ficha"><Cargando /></div>

  const plan = datos.plan
  const estado = datos.estadoClienteId != null && datos.estadoClienteCalculadoId != null
    ? estadoMostrado(catalogos, datos.estadoClienteId, datos.estadoClienteCalculadoId)
    : null
  const hoy = datos.hoy

  return (
    <div className="contenedor-ficha">
      {form && (
        <ModalCuota form={form} hoy={hoy} interesMora={datos.interesMora} onCerrar={() => setForm(null)} onGuardado={actualizar} />
      )}
      <button className="volver" onClick={volver}>
        <Atras />
        <span>{desde === 'cobros' ? 'Volver a cobros' : 'Volver a la ficha del cliente'}</span>
      </button>

      <div className="ficha-cabecera">
        <div className="ficha-identidad">
          <span className="ficha-avatar">{iniciales(datos.nombres, datos.apellidos) || '··'}</span>
          <div style={{ minWidth: 0 }}>
            <div className="plan-etiqueta" style={{ marginBottom: 4 }}>Plan de pago</div>
            <h1 style={{ marginBottom: 4 }}>{tituloPersona(datos)}</h1>
            <span className="plan-meta">Fecha de Ingreso: {fFecha(datos.fechaIngreso)}</span>
          </div>
        </div>
        <div className="ficha-acciones">
          {guardado && <span className="guardado"><Check /><span>Cambios guardados</span></span>}
          {estado?.nombre && <EtiquetaEstadoGrande estadoId={estado.clave} texto={estado.nombre} />}
          <button className="btn-primario" style={{ padding: '11px 17px', fontWeight: 600 }} onClick={verFicha}>Ver ficha del cliente</button>
        </div>
      </div>

      {!plan && (
        <div className="plan-bloque" style={{ gap: 6 }}>
          <div className="plan-etiqueta">Crear plan de pago</div>
          {datos.puedeGestionar && datos.esCliente ? (
            <CrearPlanForm
              personaId={personaId}
              texto="Al generar el plan se crean todas las cuotas con su fecha de vencimiento."
              inicial={{ total: datos.montoSugerido > 0 ? String(datos.montoSugerido) : '', cuotas: '12', inicio: hoy, dia: String(Number(hoy.slice(8, 10))) }}
              onCreado={actualizar}
            />
          ) : (
            <span className="vacio">
              {datos.esCliente ? 'Este cliente no tiene plan de pago.' : 'El plan de pago se crea cuando el prospecto se convierte en cliente oficial.'}
            </span>
          )}
        </div>
      )}

      {plan && (
        <div className="ficha-bloques">
          <div className="plan-bloque">
            <div className="plan-etiqueta">Información del plan</div>
            <InfoPlan plan={plan} grande />
          </div>
          <div className="tarjeta" style={{ overflow: 'hidden' }}>
            <div className="cabecera-seccion" style={{ padding: 'clamp(16px, 3vw, 22px) clamp(16px, 3vw, 22px) 14px', marginBottom: 0 }}>
              <span className="plan-etiqueta">Cuotas</span>
              <span style={{ fontSize: 11.5, color: 'var(--ink3)' }}>{plural(plan.cantidadCuotas, 'cuota')}</span>
            </div>
            <TablaCuotas
              cuotas={plan.cuotas}
              acciones={datos.puedeGestionar ? {
                onPagar: (cuota) => setForm({ modo: 'pago', cuota }),
                onPromesa: (cuota) => setForm({ modo: 'promesa', cuota }),
                onEditarPago: (cuota, pago) => setForm({ modo: 'editarPago', cuota, pago }),
                onEditarPromesa: (cuota, promesa) => setForm({ modo: 'editarPromesa', cuota, promesa }),
              } : undefined}
            />
          </div>
        </div>
      )}
    </div>
  )
}
