import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { erroresDe, mensajeDe } from '../../api/cliente'
import { api } from '../../api/endpoints'
import type { ComentarioDto, CorreoDto, FamiliarDto, NumeroDto, PersonaDetalle } from '../../api/tipos'
import { useSesion } from '../../auth/Sesion'
import { EtiquetaEstadoGrande, EtiquetaPrimaGrande } from '../../components/Chips'
import { CampoNotas, CampoSelect, CampoTexto, Select, TarjetaSeccion, type Opcion } from '../../components/Controles'
import { Atras, Check } from '../../components/Iconos'
import { useGuardadoDiferido } from '../../hooks/useGuardadoDiferido'
import { fFecha, fMonto, iniciales, num, origenEtiqueta, vendedorEtiqueta } from '../../utils/formato'
import {
  faltantesNuevo, formDesde, formVacio, idONull, payloadCrear, payloadDatos, primaCalculada, type FormPersona,
} from './formulario'
import { Comentarios, Contactos, Familiares } from './Secciones'

type Pestana = 'general' | 'ventas' | 'familiares' | 'actividad'

/**
 * Ficha de la persona. `seccion` indica desde qué tabla se entró: cambia el texto de "Volver"
 * y la etiqueta de estado del cliente (solo se muestra al entrar desde Clientes).
 */
export function PersonaPage({ seccion, nuevo = false }: { seccion: 'ventas' | 'clientes'; nuevo?: boolean }) {
  const { id } = useParams()
  const personaId = Number(id)
  const navegar = useNavigate()
  const { catalogos } = useSesion()

  const [persona, setPersona] = useState<PersonaDetalle | null>(null)
  const [form, setForm] = useState<FormPersona>(formVacio)
  const [comentarios, setComentarios] = useState<ComentarioDto[]>([])
  const [cargaError, setCargaError] = useState('')
  const [guardado, setGuardado] = useState(false)
  const [errorGuardar, setErrorGuardar] = useState('')
  const [pestana, setPestana] = useState<Pestana>('general')
  const [volverIntento, setVolverIntento] = useState(false)
  const [crearIntento, setCrearIntento] = useState(false)
  const [erroresServidor, setErroresServidor] = useState<Record<string, string>>({})
  const [creando, setCreando] = useState(false)
  const [conv, setConv] = useState<{ expediente: string; origen: string; estado: string; error: string } | null>(null)

  useEffect(() => {
    if (nuevo) return
    let vivo = true
    api.persona(personaId)
      .then((p) => {
        if (!vivo) return
        setPersona(p)
        setForm(formDesde(p))
        setComentarios(p.comentarios)
        setPestana('general')
        setGuardado(false)
        setConv(null)
        setVolverIntento(false)
      })
      .catch((e) => vivo && setCargaError(mensajeDe(e, 'No se pudo cargar la ficha.')))
    return () => {
      vivo = false
    }
  }, [personaId, nuevo])

  const esCliente = !!persona?.esCliente
  const ok = useCallback(() => {
    setGuardado(true)
    setErrorGuardar('')
  }, [])
  const fallo = useCallback((e: unknown) => setErrorGuardar(mensajeDe(e, 'No se pudieron guardar los cambios.')), [])

  // Un guardador por sección, como en el prototipo (cada cambio se guarda solo).
  const gDatos = useGuardadoDiferido((f: FormPersona) => api.guardarDatos(personaId, payloadDatos(f, esCliente)), ok, fallo)
  const gVenta = useGuardadoDiferido(
    (f: FormPersona) => api.guardarVenta(personaId, {
      procedenciaVentaId: idONull(f.venta.procedenciaVentaId), metodoVentaId: idONull(f.venta.metodoVentaId),
      monto: num(f.venta.monto), notas: f.venta.notas,
    }),
    ok, fallo,
  )
  const gPrima = useGuardadoDiferido(
    (f: FormPersona) => api.guardarPrima(personaId, {
      monto: num(f.prima.monto), montoCancelado: num(f.prima.montoCancelado),
      fechaEstimadaPago: f.prima.fechaEstimadaPago || null, fechaPago: f.prima.fechaPago || null,
    }),
    ok, fallo,
  )
  // Las listas se reemplazan completas; al volver, se copian los ids nuevos si no hubo más cambios.
  const gNumeros = useGuardadoDiferido(
    (n: NumeroDto[]) => api.guardarNumeros(personaId, n),
    (r: NumeroDto[], hayMas: boolean) => {
      ok()
      if (!hayMas) setForm((f) => (f.numeros.length === r.length ? { ...f, numeros: f.numeros.map((x, i) => ({ ...x, id: r[i].id, principal: r[i].principal })) } : f))
    },
    fallo,
  )
  const gCorreos = useGuardadoDiferido(
    (c: CorreoDto[]) => api.guardarCorreos(personaId, c),
    (r: CorreoDto[], hayMas: boolean) => {
      ok()
      if (!hayMas) setForm((f) => (f.correos.length === r.length ? { ...f, correos: f.correos.map((x, i) => ({ ...x, id: r[i].id, principal: r[i].principal })) } : f))
    },
    fallo,
  )
  const gFamiliares = useGuardadoDiferido(
    (fs: FamiliarDto[]) => api.guardarFamiliares(personaId, fs),
    (r: FamiliarDto[], hayMas: boolean) => {
      ok()
      if (!hayMas) setForm((f) => (f.familiares.length === r.length ? { ...f, familiares: f.familiares.map((x, i) => ({ ...x, id: r[i].id })) } : f))
    },
    fallo,
  )

  const cambiar = (parte: 'datos' | 'venta' | 'prima', campo: string, valor: string) => {
    const nuevoForm = { ...form, [parte]: { ...form[parte], [campo]: valor } } as FormPersona
    setForm(nuevoForm)
    if (nuevo) return
    if (parte === 'datos') gDatos.programar(nuevoForm)
    else if (parte === 'venta') gVenta.programar(nuevoForm)
    else gPrima.programar(nuevoForm)
  }
  const cambiarLista = <K extends 'numeros' | 'correos' | 'familiares'>(lista: K, valor: FormPersona[K]) => {
    setForm((f) => ({ ...f, [lista]: valor }))
    if (nuevo) return
    if (lista === 'numeros') gNumeros.programar(valor as NumeroDto[])
    else if (lista === 'correos') gCorreos.programar(valor as CorreoDto[])
    else gFamiliares.programar(valor as FamiliarDto[])
  }

  const prima = primaCalculada(form)
  const fechaReq = !nuevo && prima.estado === 3
  const fechaFalta = fechaReq && !form.prima.fechaPago.trim()
  const errores = nuevo ? { ...faltantesNuevo(form), ...(crearIntento ? erroresServidor : {}) } : {}
  const err = (k: string) => (nuevo && crearIntento ? errores[k] : undefined)
  const req = nuevo

  const volver = async () => {
    if (fechaFalta) {
      setVolverIntento(true)
      return
    }
    if (!nuevo) await Promise.all([gDatos.flush(), gVenta.flush(), gPrima.flush(), gNumeros.flush(), gCorreos.flush(), gFamiliares.flush()])
    navegar(seccion === 'clientes' ? '/gestion/clientes' : '/gestion/ventas')
  }

  const crear = async () => {
    setCrearIntento(true)
    setErroresServidor({})
    if (Object.keys(faltantesNuevo(form)).length) return
    setCreando(true)
    try {
      const p = await api.crearProspecto(payloadCrear(form))
      navegar(`/gestion/ventas/${p.id}`, { replace: true })
    } catch (e) {
      setErroresServidor(erroresDe(e))
      setErrorGuardar(mensajeDe(e))
    } finally {
      setCreando(false)
    }
  }

  const confirmarConvertir = async () => {
    if (!conv) return
    if (!conv.origen) {
      setConv({ ...conv, error: 'El origen del cliente es requerido.' })
      return
    }
    try {
      await Promise.all([gDatos.flush(), gVenta.flush(), gPrima.flush()])
      await api.convertir(personaId, {
        expediente: conv.expediente.trim(),
        origenClienteId: Number(conv.origen),
        estadoClienteId: Number(conv.estado) || 1,
      })
      navegar(`/gestion/clientes/${personaId}`)
    } catch (e) {
      setConv({ ...conv, error: mensajeDe(e) })
    }
  }

  if (cargaError) {
    return (
      <div className="contenedor-ficha">
        <button className="volver" onClick={() => navegar(seccion === 'clientes' ? '/gestion/clientes' : '/gestion/ventas')}>
          <Atras /><span>{seccion === 'clientes' ? 'Volver a clientes' : 'Volver a la tabla de ventas'}</span>
        </button>
        <p className="texto-error">{cargaError}</p>
      </div>
    )
  }
  if (!nuevo && !persona) return <div className="contenedor-ficha" />

  const cats = catalogos
  const d = form.datos
  const sel = (vacioLabel: string, items: Opcion[]): Opcion[] => [{ v: '', l: vacioLabel }, ...items]
  const conTabs = esCliente
  const en = (k: Pestana) => !conTabs || pestana === k
  const nombre = (d.nombres + ' ' + d.apellidos).trim()
  const estadoCliente = cats?.estadosCliente.find((x) => String(x.id) === d.estadoClienteId)
  const nombrePrima = cats?.estadosPrima.find((x) => x.id === prima.estado)?.nombre ?? ''

  const bloqueDatos = (
    <TarjetaSeccion titulo="Datos de la persona">
      <div className="grilla-campos">
        <CampoTexto label="Nombres" valor={d.nombres} onChange={(v) => cambiar('datos', 'nombres', v)} requerido={req} error={err('nombres')} />
        <CampoTexto label="Apellidos" valor={d.apellidos} onChange={(v) => cambiar('datos', 'apellidos', v)} requerido={req} error={err('apellidos')} />
        <CampoTexto label="Cédula" valor={d.cedula} onChange={(v) => cambiar('datos', 'cedula', v)} requerido={req} error={err('cedula')} />
        <CampoTexto label="Número de finca" valor={d.finca} onChange={(v) => cambiar('datos', 'finca', v)} requerido={req} error={err('finca')} />
        <CampoTexto label="Número de expediente" valor={d.expediente} onChange={(v) => cambiar('datos', 'expediente', v)} />
        <CampoTexto label="Fecha de ingreso" tipo="date" valor={d.fechaIngreso} onChange={(v) => cambiar('datos', 'fechaIngreso', v)} requerido={req} error={err('fechaIngreso')} />
        <CampoSelect
          label="Vendido por" valor={d.vendedorId} onChange={(v) => cambiar('datos', 'vendedorId', v)} requerido={req} error={err('vendedorId')}
          opciones={sel('Seleccione…', (cats?.vendedores ?? []).map((x) => ({ v: String(x.id), l: vendedorEtiqueta(x) })))}
        />
        {esCliente && (
          <>
            <CampoSelect
              label="Estado del cliente" valor={d.estadoClienteId} onChange={(v) => cambiar('datos', 'estadoClienteId', v)}
              opciones={(cats?.estadosCliente ?? []).map((x) => ({ v: String(x.id), l: x.nombre }))}
            />
            <CampoSelect
              label="Origen del cliente" span={2} valor={d.origenClienteId} onChange={(v) => cambiar('datos', 'origenClienteId', v)}
              opciones={(cats?.origenes ?? []).map((x) => ({ v: String(x.id), l: origenEtiqueta(x) }))}
            />
          </>
        )}
        {!esCliente && (
          <>
            <CampoTexto label="Teléfono principal" valor={d.telefonoPrincipal} onChange={(v) => cambiar('datos', 'telefonoPrincipal', v)} requerido={req} error={err('telefonoPrincipal')} />
            <CampoTexto label="WhatsApp principal" valor={d.whatsappPrincipal} onChange={(v) => cambiar('datos', 'whatsappPrincipal', v)} requerido={req} error={err('whatsappPrincipal')} />
            <CampoTexto label="Correo electrónico principal" valor={d.correoPrincipal} onChange={(v) => cambiar('datos', 'correoPrincipal', v)} />
          </>
        )}
      </div>
    </TarjetaSeccion>
  )

  const bloqueVenta = (
    <TarjetaSeccion titulo="Venta">
      <div className="grilla-campos">
        <CampoSelect
          label="Procedencia de venta" valor={form.venta.procedenciaVentaId} onChange={(v) => cambiar('venta', 'procedenciaVentaId', v)}
          requerido={req} error={err('procedenciaVentaId')}
          opciones={sel('Seleccione…', (cats?.procedencias ?? []).map((x) => ({ v: String(x.id), l: x.nombre })))}
        />
        <CampoSelect
          label="Método de venta" valor={form.venta.metodoVentaId} onChange={(v) => cambiar('venta', 'metodoVentaId', v)}
          requerido={req} error={err('metodoVentaId')}
          opciones={sel('Seleccione…', (cats?.metodos ?? []).map((x) => ({ v: String(x.id), l: x.nombre })))}
        />
        <CampoTexto label="Monto de la venta" inputMode="numeric" valor={form.venta.monto} onChange={(v) => cambiar('venta', 'monto', v)} requerido={req} error={err('montoVenta')} />
      </div>
      <CampoNotas label="Notas de la venta" valor={form.venta.notas} onChange={(v) => cambiar('venta', 'notas', v)} />
    </TarjetaSeccion>
  )

  const bloquePrima = (
    <TarjetaSeccion titulo="Prima">
      <div className="grilla-campos">
        <CampoTexto label="Monto de prima" inputMode="numeric" valor={form.prima.monto} onChange={(v) => cambiar('prima', 'monto', v)} requerido={req} error={err('montoPrima')} />
        <CampoTexto label="Monto cancelado" inputMode="numeric" valor={form.prima.montoCancelado} onChange={(v) => cambiar('prima', 'montoCancelado', v)} />
        <CampoTexto label="Saldo pendiente" valor={fMonto(prima.saldo)} soloLectura />
        <CampoTexto label="Fecha estimada de pago" tipo="date" valor={form.prima.fechaEstimadaPago} onChange={(v) => cambiar('prima', 'fechaEstimadaPago', v)} />
        <CampoTexto
          label="Fecha de pago" tipo="date" valor={form.prima.fechaPago} onChange={(v) => cambiar('prima', 'fechaPago', v)}
          requerido={fechaReq} error={fechaFalta ? 'Requerida cuando la prima está pagada.' : err('fechaPago')}
        />
        <CampoTexto label="Estado de prima" valor={nombrePrima} soloLectura />
      </div>
    </TarjetaSeccion>
  )

  const pestanas: [Pestana, string, number][] = [
    ['general', 'General', 0],
    ['ventas', 'Ventas', 0],
    ['familiares', 'Familiares', form.familiares.length],
    ['actividad', 'Actividad', comentarios.length],
  ]

  return (
    <div className="contenedor-ficha">
      <button className="volver" onClick={volver}>
        <Atras />
        <span>{nuevo ? 'Descartar y volver' : seccion === 'clientes' ? 'Volver a clientes' : 'Volver a la tabla de ventas'}</span>
      </button>
      {volverIntento && fechaFalta && (
        <div role="alert" className="alerta">
          No podés volver a la tabla porque falta un campo requerido: Fecha de pago.
        </div>
      )}

      <div className="ficha-cabecera">
        <div className="ficha-identidad">
          <span className="ficha-avatar">{iniciales(d.nombres, d.apellidos) || '··'}</span>
          <div style={{ minWidth: 0 }}>
            <h1>{nombre || 'Nueva persona'}</h1>
            <div className="ficha-meta">
              <span
                className="chip"
                style={{
                  fontSize: 11.5, padding: '5px 11px',
                  background: esCliente ? 'rgba(22,163,74,0.14)' : 'rgba(232,193,88,0.18)',
                  color: esCliente ? '#2E7D4F' : 'var(--gold)',
                }}
              >
                {esCliente ? 'Cliente oficial' : 'Prospecto'}
              </span>
              <span>{(d.expediente ? d.expediente + ' · ' : '') + 'Ingreso ' + fFecha(d.fechaIngreso)}</span>
            </div>
          </div>
        </div>
        <div className="ficha-acciones">
          {!nuevo && guardado && (
            <span className="guardado"><Check /><span>Cambios guardados</span></span>
          )}
          {!nuevo && nombrePrima && <EtiquetaPrimaGrande estadoId={prima.estado} texto={nombrePrima} />}
          {esCliente && seccion === 'clientes' && estadoCliente && <EtiquetaEstadoGrande estadoId={estadoCliente.id} texto={estadoCliente.nombre} />}
          {!nuevo && !esCliente && (
            <button className="btn-primario" style={{ padding: '11px 17px' }} onClick={() => setConv({ expediente: d.expediente, origen: '', estado: '1', error: '' })}>
              <Check tamano={15} grosor={2.2} />
              <span>Convertir en cliente</span>
            </button>
          )}
        </div>
      </div>

      {errorGuardar && <div role="alert" className="alerta">{errorGuardar}</div>}

      {conv && (
        <div className="convertir">
          <div style={{ fontSize: 14.5, fontWeight: 700, color: 'var(--head)', marginBottom: 4 }}>Convertir en cliente oficial</div>
          <p style={{ margin: '0 0 14px', fontSize: 13, color: 'var(--ink2)' }}>
            {d.expediente.trim()
              ? 'Expediente ' + d.expediente + '. Seleccione el origen del cliente.'
              : 'Seleccione el origen del cliente. El número de expediente es opcional.'}
          </p>
          <div className="convertir-fila">
            {!d.expediente.trim() && (
              <label style={{ flex: 1, minWidth: 180 }}>
                <span className="etiqueta">Expediente</span>
                <input className="campo" value={conv.expediente} placeholder="XX-XXXXXX-XXXX-CJ" onChange={(e) => setConv({ ...conv, expediente: e.target.value })} />
              </label>
            )}
            <label style={{ flex: 1, minWidth: 180 }}>
              <span className="etiqueta">Estado del cliente</span>
              <Select valor={conv.estado} onChange={(v) => setConv({ ...conv, estado: v })} opciones={(cats?.estadosCliente ?? []).map((x) => ({ v: String(x.id), l: x.nombre }))} />
            </label>
            <label style={{ flex: 1.6, minWidth: 240 }}>
              <span className="etiqueta">Origen del cliente <span className="requerido">*</span></span>
              <Select
                valor={conv.origen}
                onChange={(v) => setConv({ ...conv, origen: v, error: '' })}
                className={'campo campo-select' + (conv.error && !conv.origen ? ' error' : '')}
                opciones={sel('Seleccione el origen…', (cats?.origenes ?? []).map((o) => ({ v: String(o.id), l: origenEtiqueta(o) })))}
              />
            </label>
            <button className="btn-primario" onClick={confirmarConvertir}>Confirmar</button>
            <button className="btn-secundario" style={{ padding: '11px 16px' }} onClick={() => setConv(null)}>Cancelar</button>
          </div>
          {conv.error && <p className="texto-error">{conv.error}</p>}
        </div>
      )}

      <div className="ficha-bloques">
        {conTabs && (
          <div role="tablist" className="pestanas sin-scroll">
            {pestanas.map(([k, l, n]) => (
              <button key={k} role="tab" aria-selected={pestana === k} className={'pestana' + (pestana === k ? ' activa' : '')} onClick={() => setPestana(k)}>
                {l}
                {n > 0 && <small>({n})</small>}
              </button>
            ))}
          </div>
        )}

        {en('general') && bloqueDatos}
        {esCliente && en('general') && (
          <Contactos numeros={form.numeros} correos={form.correos} onNumeros={(n) => cambiarLista('numeros', n)} onCorreos={(c) => cambiarLista('correos', c)} />
        )}
        {en('ventas') && persona?.venta !== null && bloqueVenta}
        {en('ventas') && persona?.prima !== null && bloquePrima}
        {en('familiares') && <Familiares familiares={form.familiares} onCambiar={(f) => cambiarLista('familiares', f)} />}
        {!nuevo && en('actividad') && (
          <Comentarios
            comentarios={comentarios}
            onComentar={async (texto) => {
              const c = await api.comentar(personaId, texto)
              setComentarios((cs) => [c, ...cs])
            }}
            onEliminar={async (cid) => {
              await api.eliminarComentario(cid)
              setComentarios((cs) => cs.filter((c) => c.id !== cid))
            }}
          />
        )}

        {nuevo && (
          <>
            <button className="btn-primario" style={{ alignSelf: 'flex-start', padding: '12px 22px', fontSize: 14 }} disabled={creando} onClick={crear}>
              Guardar prospecto
            </button>
            {crearIntento && Object.keys(errores).length > 0 && (
              <span role="alert" style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--rojo)' }}>Completá los campos requeridos antes de guardar.</span>
            )}
          </>
        )}
      </div>
    </div>
  )
}
