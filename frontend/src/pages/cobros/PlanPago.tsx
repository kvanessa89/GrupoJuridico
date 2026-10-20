import { Fragment, useState } from 'react'
import { mensajeDe } from '../../api/cliente'
import { api } from '../../api/endpoints'
import type { CuotaDto, PagoDto, PlanDetalle, PlanPersona, PromesaDto } from '../../api/tipos'
import { useSesion } from '../../auth/Sesion'
import { ChipTono } from '../../components/Chips'
import { estiloTono, TONO_PAGO, TONO_PROMESA, TONO_VENCIMIENTO } from '../../utils/colores'
import { CampoMonto, CampoTexto } from '../../components/Controles'
import { Check, Chevron, Documento, IconoPago, IconoPromesa, Lapiz, Subir } from '../../components/Iconos'
import { fCorta, fFecha, fMonto, plural } from '../../utils/formato'
import { montoMensual, planValido, primeraCuota, promesaVisible, saldoParaPago, textoPromesas, type DatosPlan } from './reglas'

function useNombres() {
  const { catalogos } = useSesion()
  const nombre = (lista: { id: number; nombre: string }[] | undefined, id: number) => lista?.find((x) => x.id === id)?.nombre ?? '—'
  return {
    pago: (id: number) => nombre(catalogos?.estadosCuotaPago, id),
    vencimiento: (id: number) => nombre(catalogos?.estadosCuotaVencimiento, id),
    promesa: (id: number) => nombre(catalogos?.estadosPromesa, id),
  }
}

// ---------- Información del plan ----------

/** Datos del plan, avance, promesas y el aviso de cuotas vencidas. */
export function InfoPlan({ plan, grande }: { plan: PlanDetalle; grande?: boolean }) {
  const stats: [string, string][] = [
    ['Monto total', fMonto(plan.montoTotal)],
    ['Monto mensual', fMonto(plan.montoCuota)],
    ['Cantidad de cuotas', String(plan.cantidadCuotas)],
    ['Fecha de inicio de pago', fFecha(plan.fechaInicio)],
    ['Día de pago', `Día ${plan.diaPago} de cada mes`],
    ['Saldo pendiente', fMonto(plan.saldo)],
  ]
  return (
    <>
      <div className="plan-stats">
        {stats.map(([l, v]) => (
          <div key={l} style={{ minWidth: 0 }}>
            <div className="plan-stat-label">{l}</div>
            <div className="plan-stat-valor" style={{ fontSize: grande ? 15 : 14.5 }}>{v}</div>
          </div>
        ))}
      </div>
      <div className="plan-progreso">
        <span>{plan.cuotasPagadas} de {plural(plan.cantidadCuotas, 'cuota')} pagadas · {fMonto(plan.pagadoTotal)} cobrado</span>
        <span>{textoPromesas(plan)}</span>
      </div>
      {plan.cuotasVencidas > 0 && (
        <div className="plan-alerta" role="status">
          {plural(plan.cuotasVencidas, 'cuota vencida', 'cuotas vencidas')} · {fMonto(plan.montoVencido)} vencido · {plural(plan.atraso, 'día')} de atraso
        </div>
      )}
    </>
  )
}

// ---------- Cuotas ----------

interface AccionesCuotas {
  onPagar: (c: CuotaDto) => void
  onPromesa: (c: CuotaDto) => void
  onEditarPago: (c: CuotaDto, p: PagoDto) => void
  onEditarPromesa: (c: CuotaDto, p: PromesaDto) => void
}

/** Descarga el comprobante con el token de la sesión. */
async function descargar(id: number, nombre: string) {
  const blob = await api.comprobante(id)
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = nombre
  a.click()
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}

function DetalleCuota({ c, acciones }: { c: CuotaDto; acciones?: AccionesCuotas }) {
  const nombres = useNombres()
  const [errorDescarga, setErrorDescarga] = useState('')
  return (
    <div className="cuota-detalle">
      <div className="cuota-detalle-col">
        <span className="cuota-detalle-titulo">Pagos<span className="contador verde">{c.pagos.length}</span></span>
        {c.pagos.map((p) => (
          <div key={p.id} className="mov">
            <span className="mov-icono" style={estiloTono('verde')}><Check tamano={15} /></span>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div className="mov-titulo">{fMonto(p.montoPago)}</div>
              <div className="mov-sub">Pagado el {fFecha(p.fechaPago)}</div>
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start' }}>
                {p.comprobantes.map((a) => a.disponible ? (
                  <button
                    key={a.id}
                    className="comprobante enlace"
                    title={'Descargar ' + a.nombre}
                    onClick={(e) => {
                      e.stopPropagation()
                      setErrorDescarga('')
                      descargar(a.id, a.nombre).catch((err) => setErrorDescarga(mensajeDe(err, 'No se pudo descargar el comprobante.')))
                    }}
                  >
                    <Documento /><span>{a.nombre}</span>
                  </button>
                ) : (
                  <span key={a.id} className="comprobante" title="Archivo no disponible"><Documento /><span>{a.nombre}</span></span>
                ))}
              </div>
            </div>
            {acciones && (
              <button className="btn-mini" title="Editar pago" aria-label="Editar pago" onClick={(e) => { e.stopPropagation(); acciones.onEditarPago(c, p) }}>
                <Lapiz />
              </button>
            )}
          </div>
        ))}
        {!c.pagos.length && <span className="vacio">Sin pagos registrados.</span>}
        {errorDescarga && <span className="msg-error">{errorDescarga}</span>}
      </div>
      <div className="cuota-detalle-col">
        <span className="cuota-detalle-titulo">Promesas de pago<span className="contador ambar">{c.promesas.length}</span></span>
        {c.promesas.map((p) => (
          <div key={p.id} className="mov">
            <span className="mov-icono" style={estiloTono(TONO_PROMESA[p.estadoId])}><IconoPromesa check={false} /></span>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div className="mov-titulo">Para el {fFecha(p.fechaPromesa)}</div>
              <div className="mov-sub">{fMonto(p.montoPrometido)}{p.montoPagado > 0 && p.montoPagado < p.montoPrometido ? ` · ${fMonto(p.montoPagado)} pagado` : ''}</div>
            </div>
            <ChipTono tono={TONO_PROMESA[p.estadoId]} texto={nombres.promesa(p.estadoId)} />
            {acciones && p.estadoId === 1 && (
              <button className="btn-mini" title="Editar promesa" aria-label="Editar promesa" onClick={(e) => { e.stopPropagation(); acciones.onEditarPromesa(c, p) }}>
                <Lapiz />
              </button>
            )}
          </div>
        ))}
        {!c.promesas.length && <span className="vacio">Sin promesas de pago.</span>}
      </div>
    </div>
  )
}

/**
 * Tabla de cuotas. Con `acciones` (pantalla del plan) las filas se expanden y tienen los botones
 * de pago y promesa; sin ellas (ficha del cliente) es solo lectura.
 */
export function TablaCuotas({ cuotas, acciones }: { cuotas: CuotaDto[]; acciones?: AccionesCuotas }) {
  const nombres = useNombres()
  const [abierta, setAbierta] = useState<number | null>(null)
  const detalle = !!acciones
  const clase = 'cuotas-grilla ' + (detalle ? 'detalle' : 'ligero')
  return (
    <div className="sin-scroll" style={{ overflowX: 'auto' }}>
      <div style={{ minWidth: detalle ? 640 : 624 }}>
        <div className={clase + ' cuotas-cabecera'}>
          <span>Cuota</span><span>Fecha</span><span>Monto</span><span>Saldo</span><span>Pago</span><span>Vencimiento</span><span>Promesa</span>
          <span style={{ textAlign: 'right' }}>{detalle ? 'Acciones' : ''}</span>
        </div>
        {cuotas.map((c) => {
          const esAbierta = detalle && abierta === c.id
          const pagada = c.estadoPagoId === 3
          const pm = promesaVisible(c.promesas)
          const actividad = [c.pagos.length ? plural(c.pagos.length, 'pago') : '', c.promesas.length ? plural(c.promesas.length, 'promesa') : ''].filter(Boolean).join(' · ')
          const notaMora = c.mora ? `Incl. ${fMonto(c.mora)} de interés` : c.sinMora === true && c.moraPosible ? 'Sin interés por acuerdo' : ''
          const alternar = () => detalle && setAbierta(esAbierta ? null : c.id)
          return (
            <Fragment key={c.id}>
              <div
                className={clase + ' cuota-fila' + (esAbierta ? ' abierta' : c.estadoVencimientoId === 2 ? ' vencida' : '')}
                role={detalle ? 'button' : undefined}
                tabIndex={detalle ? 0 : undefined}
                aria-expanded={detalle ? esAbierta : undefined}
                onClick={alternar}
                onKeyDown={(e) => {
                  if (detalle && e.target === e.currentTarget && (e.key === 'Enter' || e.key === ' ')) {
                    e.preventDefault()
                    alternar()
                  }
                }}
              >
                <span style={{ fontWeight: 700 }}>{c.numeroCuota}</span>
                <span className="cuota-celda">
                  <span>{fFecha(c.fechaVencimiento)}</span>
                  {detalle && actividad && <span className="cuota-actividad"><Check tamano={11} />{actividad}</span>}
                </span>
                <span className="cuota-celda">
                  <span>{fMonto(c.total)}</span>
                  {notaMora && <span style={{ fontSize: 11, color: 'var(--rojo-suave)' }}>{notaMora}</span>}
                </span>
                <span style={{ fontWeight: 700 }}>{fMonto(c.saldo)}</span>
                <span style={{ display: 'flex' }}><ChipTono tono={TONO_PAGO[c.estadoPagoId]} texto={nombres.pago(c.estadoPagoId)} /></span>
                <span style={{ display: 'flex' }}><ChipTono tono={TONO_VENCIMIENTO[c.estadoVencimientoId]} texto={nombres.vencimiento(c.estadoVencimientoId)} /></span>
                <span style={{ display: 'flex' }}>
                  {pm ? (
                    <span className="cuota-celda" style={{ alignItems: 'flex-start' }}>
                      <ChipTono tono={TONO_PROMESA[pm.estadoId]} texto={nombres.promesa(pm.estadoId)} />
                      <span style={{ fontSize: 11, color: 'var(--ink3)', whiteSpace: 'nowrap' }}>{fCorta(pm.fechaPromesa)}</span>
                    </span>
                  ) : <span style={{ color: 'var(--ink3)' }}>—</span>}
                </span>
                <span className="cuota-acciones">
                  {acciones && !pagada && (
                    <button className="btn-mini dorado" title="Registrar pago" aria-label="Registrar pago" onClick={(e) => { e.stopPropagation(); acciones.onPagar(c) }}>
                      <IconoPago />
                    </button>
                  )}
                  {acciones && !pagada && !c.promesas.some((p) => p.estadoId === 1) && (
                    <button className="btn-mini dorado" title="Crear promesa de pago" aria-label="Crear promesa de pago" onClick={(e) => { e.stopPropagation(); acciones.onPromesa(c) }}>
                      <IconoPromesa />
                    </button>
                  )}
                  {detalle && <Chevron color="var(--gold)" estilo={{ flex: 'none', transition: 'transform 0.15s', transform: esAbierta ? 'rotate(180deg)' : 'none' }} />}
                </span>
              </div>
              {esAbierta && <DetalleCuota c={c} acciones={acciones} />}
            </Fragment>
          )
        })}
      </div>
    </div>
  )
}

// ---------- Crear plan ----------

/** Formulario para generar el plan y sus cuotas. `inicial` trae los valores propuestos (vacíos en la ficha). */
export function CrearPlanForm({
  personaId, inicial, onCreado, texto,
}: { personaId: number; inicial: DatosPlan; onCreado: (p: PlanPersona) => void; texto: string }) {
  const [d, setD] = useState(inicial)
  const [error, setError] = useState('')
  const [enviando, setEnviando] = useState(false)
  const cambiar = (k: keyof DatosPlan, v: string) => {
    setD((x) => ({ ...x, [k]: v }))
    setError('')
  }
  const valido = planValido(d)
  const previa = valido
    ? `${plural(valido.cuotas, 'cuota')} de ${fMonto(montoMensual(valido.total, valido.cuotas))} · primera cuota el ${fFecha(primeraCuota(valido.inicio, valido.dia))}`
    : 'Complete los datos para ver las cuotas.'

  const generar = async () => {
    if (!valido) {
      setError('Revise los datos: monto mayor a cero, entre 1 y 120 cuotas, fecha de inicio y un día de pago entre 1 y 31.')
      return
    }
    setEnviando(true)
    try {
      onCreado(await api.crearPlan(personaId, { montoTotal: valido.total, cantidadCuotas: valido.cuotas, fechaInicio: valido.inicio, diaPago: valido.dia }))
    } catch (e) {
      setError(mensajeDe(e, 'No se pudo crear el plan de pago.'))
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div>
      <p className="plan-texto">{texto}</p>
      <div className="plan-form">
        <CampoMonto label="Monto total" requerido valor={d.total} onChange={(v) => cambiar('total', v)} />
        <label style={{ display: 'block', minWidth: 0 }}>
          <span className="etiqueta">Cantidad de cuotas<span className="requerido"> *</span></span>
          <input className="campo" type="number" min={1} max={120} value={d.cuotas} onChange={(e) => cambiar('cuotas', e.target.value)} />
        </label>
        <CampoTexto label="Fecha de inicio" requerido tipo="date" valor={d.inicio} onChange={(v) => cambiar('inicio', v)} />
        <label style={{ display: 'block', minWidth: 0 }}>
          <span className="etiqueta">Día de pago<span className="requerido"> *</span></span>
          <input className="campo" type="number" min={1} max={31} value={d.dia} onChange={(e) => cambiar('dia', e.target.value)} />
        </label>
      </div>
      <div className="plan-form-pie">
        <span style={{ fontSize: 13, color: 'var(--ink2)' }}>{previa}</span>
        <button className="btn-primario" disabled={enviando} onClick={generar}>Generar plan y cuotas</button>
      </div>
      {error && <p role="alert" className="texto-error">{error}</p>}
    </div>
  )
}

// ---------- Modales de pago y promesa ----------

export type FormCuota =
  | { modo: 'pago'; cuota: CuotaDto }
  | { modo: 'editarPago'; cuota: CuotaDto; pago: PagoDto }
  | { modo: 'promesa'; cuota: CuotaDto }
  | { modo: 'editarPromesa'; cuota: CuotaDto; promesa: PromesaDto }

const TITULOS: Record<FormCuota['modo'], [string, string]> = {
  pago: ['Registrar pago', 'Guardar pago'],
  editarPago: ['Editar pago', 'Guardar cambios'],
  promesa: ['Crear promesa de pago', 'Guardar promesa'],
  editarPromesa: ['Editar promesa de pago', 'Guardar cambios'],
}

const ACEPTA = '.pdf,.jpg,.jpeg,.png,.webp'

/** Modal para registrar o editar un pago o una promesa de una cuota. */
export function ModalCuota({
  form, hoy, interesMora, onCerrar, onGuardado,
}: { form: FormCuota; hoy: string; interesMora: number; onCerrar: () => void; onGuardado: (p: PlanPersona) => void }) {
  const { cuota } = form
  const esPago = form.modo === 'pago' || form.modo === 'editarPago'
  const [cobrarMora, setCobrarMora] = useState(cuota.sinMora !== true)
  const saldoBase = form.modo === 'editarPago' ? cuota.saldo + form.pago.montoPago : form.modo === 'pago' ? saldoParaPago(cuota, cobrarMora) : cuota.saldo
  const [monto, setMonto] = useState(() => {
    if (form.modo === 'editarPago') return String(form.pago.montoPago)
    if (form.modo === 'editarPromesa') return String(form.promesa.montoPrometido)
    return String(saldoBase)
  })
  const [fecha, setFecha] = useState(() => form.modo === 'editarPago' ? form.pago.fechaPago : form.modo === 'editarPromesa' ? form.promesa.fechaPromesa : form.modo === 'pago' ? hoy : '')
  const [archivo, setArchivo] = useState<File | null>(null)
  const [error, setError] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [titulo, boton] = TITULOS[form.modo]
  const hayMora = form.modo === 'pago' && cuota.moraPosible > 0

  const guardar = async () => {
    const m = Number(monto)
    const faltante = esPago
      ? !(m > 0) ? 'Ingrese el monto del pago.' : !fecha ? 'Ingrese la fecha de pago.' : form.modo === 'pago' && !archivo ? 'Suba el comprobante de pago.' : ''
      : !fecha ? 'Ingrese la fecha de la promesa.' : !(m > 0) ? 'Ingrese el monto prometido.' : ''
    if (faltante) {
      setError(faltante)
      return
    }
    setEnviando(true)
    try {
      const r = form.modo === 'pago' ? await api.registrarPago(cuota.id, { montoPago: m, fechaPago: fecha, cobrarMora: hayMora ? cobrarMora : true, comprobante: archivo })
        : form.modo === 'editarPago' ? await api.editarPago(form.pago.id, { montoPago: m, fechaPago: fecha, comprobante: archivo })
        : form.modo === 'promesa' ? await api.crearPromesa(cuota.id, { fechaPromesa: fecha, montoPrometido: m })
        : await api.editarPromesa(form.promesa.id, { fechaPromesa: fecha, montoPrometido: m })
      onGuardado(r)
    } catch (e) {
      setError(mensajeDe(e))
      setEnviando(false)
    }
  }

  const sub = `Vence el ${fFecha(cuota.fechaVencimiento)} · ` + (form.modo === 'editarPago' ? `Saldo sin este pago ${fMonto(saldoBase)}` : `Saldo ${fMonto(saldoBase)}`)

  return (
    <div className="modal-fondo" onClick={onCerrar}>
      <div className="modal modal-cuota" role="dialog" aria-modal="true" aria-labelledby="modal-cuota-titulo" onClick={(e) => e.stopPropagation()}>
        <div>
          <div id="modal-cuota-titulo" className="modal-titulo" style={{ marginBottom: 4 }}>{titulo} · Cuota {cuota.numeroCuota}</div>
          <p style={{ margin: 0, fontSize: 12.5, color: 'var(--ink3)' }}>{sub}</p>
        </div>
        <div className="modal-grilla">
          {esPago ? (
            <>
              <CampoMonto label="Monto del pago" requerido valor={monto} onChange={(v) => { setMonto(v); setError('') }} />
              <CampoTexto label="Fecha de pago" requerido tipo="date" valor={fecha} onChange={(v) => { setFecha(v); setError('') }} />
            </>
          ) : (
            <>
              <CampoTexto label="Fecha de la promesa" requerido tipo="date" valor={fecha} onChange={(v) => { setFecha(v); setError('') }} />
              <CampoMonto label="Monto prometido" requerido valor={monto} onChange={(v) => { setMonto(v); setError('') }} />
            </>
          )}
        </div>
        {hayMora && (
          <label className="casilla-mora">
            <input
              type="checkbox"
              checked={cobrarMora}
              onChange={(e) => {
                setCobrarMora(e.target.checked)
                setMonto(String(saldoParaPago(cuota, e.target.checked)))
                setError('')
              }}
            />
            <span style={{ display: 'flex', flexDirection: 'column', gap: 3, minWidth: 0 }}>
              <span style={{ fontSize: 13, fontWeight: 600, color: 'var(--head)' }}>Cobrar interés de morosidad ({interesMora}% · {fMonto(cuota.moraPosible)})</span>
              <span style={{ fontSize: 12, color: 'var(--ink3)' }}>
                {cobrarMora ? `Saldo con interés: ${fMonto(saldoBase)}` : `Sin interés por acuerdo. Saldo: ${fMonto(saldoBase)}`}
              </span>
            </span>
          </label>
        )}
        {esPago && (
          <label className="subir-archivo">
            <Subir />
            <span className="subir-texto">
              {archivo ? archivo.name : form.modo === 'editarPago' ? 'Reemplazar comprobante' : 'Subir comprobante de pago'}
            </span>
            {form.modo === 'pago' && <span className="requerido">*</span>}
            <input type="file" accept={ACEPTA} style={{ display: 'none' }} onChange={(e) => { setArchivo(e.target.files?.[0] ?? null); setError('') }} />
          </label>
        )}
        {error && <span role="alert" className="msg-error" style={{ fontSize: 12.5, marginTop: 0 }}>{error}</span>}
        <div className="modal-acciones">
          <button className="btn-secundario" onClick={onCerrar}>Cancelar</button>
          <button className="btn-primario" style={{ padding: '10px 16px' }} disabled={enviando} onClick={guardar}>{boton}</button>
        </div>
      </div>
    </div>
  )
}
