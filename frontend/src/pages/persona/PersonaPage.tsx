import { useCallback, useEffect, useRef, useState } from 'react'
import { useBlocker, useNavigate, useParams } from 'react-router'
import { erroresDe, mensajeDe } from '../../api/cliente'
import { api } from '../../api/endpoints'
import type { ComentarioDto, CorreoDto, FamiliarDto, NumeroDto, PersonaDetalle } from '../../api/tipos'
import { useSesion } from '../../auth/Sesion'
import { EtiquetaEstadoGrande, EtiquetaPrimaGrande } from '../../components/Chips'
import { CampoNotas, CampoSelect, CampoTexto, Select, TarjetaSeccion, type Opcion } from '../../components/Controles'
import { Atras, Check } from '../../components/Iconos'
import { EditorSecciones, type SeccionVersion } from '../../hooks/EditorSecciones'
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

  const editor = useRef(new EditorSecciones())
  const idsNuevos = useRef({ numeros: new Map<number, number>(), correos: new Map<number, number>(), familiares: new Map<number, number>() })
  const [resolviendo, setResolviendo] = useState(false)
  const [conflictos, setConflictos] = useState<Partial<Record<SeccionVersion, PersonaDetalle | null>>>({})
  const [persona, setPersona] = useState<PersonaDetalle | null>(null)
  const [form, setForm] = useState<FormPersona>(formVacio)
  const formRef = useRef(form)
  useEffect(() => { formRef.current = form })
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
        editor.current.cargar(p.versiones)
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

  const guardadoresRef = useRef<{ hayPendientes: () => boolean }[]>([])
  const esCliente = !!persona?.esCliente
  const ok = useCallback(() => {
    window.setTimeout(() => setGuardado(guardadoresRef.current.every(g => !g.hayPendientes())), 0)
  }, [])
  const fallo = (seccion: SeccionVersion) => (e: unknown) => {
    setGuardado(false)
    setErrorGuardar(mensajeDe(e, e instanceof Error ? e.message : 'No se pudieron guardar los cambios.'))
    if ([409, 428].includes((e as { response?: { status?: number } }).response?.status ?? 0)) {
      setConflictos(cs => ({ ...cs, [seccion]: null }))
      void revisar(seccion)
    }
  }
  const revisar = async (seccion: SeccionVersion) => {
    try {
      const actual = await api.persona(personaId)
      setConflictos(cs => ({ ...cs, [seccion]: actual }))
    } catch (e) { setErrorGuardar(mensajeDe(e, 'No se pudo obtener la versión guardada. Tu borrador sigue en pantalla.')) }
  }
  const enviar = <T,>(seccion: SeccionVersion, fn: (version: string) => Promise<{ data: T; version: string }>) => editor.current.guardar(seccion, fn)
  const traducirIds = <T extends { id: number }>(lista: 'numeros' | 'correos' | 'familiares', items: T[]) =>
    items.map(x => ({ ...x, id: idsNuevos.current[lista].get(x.id) ?? x.id }))
  const confirmarIds = <T extends { id: number }>(lista: 'numeros' | 'correos' | 'familiares', enviados: T[], recibidos: T[]) => {
    const existentes = new Set(traducirIds(lista, enviados).filter(x => x.id > 0).map(x => x.id))
    const nuevos = recibidos.filter(x => !existentes.has(x.id))
    enviados.filter(x => x.id <= 0 && !idsNuevos.current[lista].has(x.id)).forEach((x, i) => {
      if (nuevos[i]) idsNuevos.current[lista].set(x.id, nuevos[i].id)
    })
    setForm(f => ({ ...f, [lista]: f[lista].map(x => ({ ...x, id: idsNuevos.current[lista].get(x.id) ?? x.id })) }))
    ok()
  }

  // Un guardador por sección, como en el prototipo (cada cambio se guarda solo).
  const gDatos = useGuardadoDiferido((f: FormPersona) => enviar('datos', v => api.guardarDatos(personaId, payloadDatos(f, esCliente), v)), ok, fallo('datos'))
  const gVenta = useGuardadoDiferido(
    (f: FormPersona) => enviar('venta', v => api.guardarVenta(personaId, {
      procedenciaVentaId: idONull(f.venta.procedenciaVentaId), metodoVentaId: idONull(f.venta.metodoVentaId),
      monto: num(f.venta.monto), notas: f.venta.notas,
    }, v)),
    ok, fallo('venta'),
  )
  const gPrima = useGuardadoDiferido(
    (f: FormPersona) => enviar('prima', v => api.guardarPrima(personaId, {
      monto: num(f.prima.monto), montoCancelado: num(f.prima.montoCancelado),
      fechaEstimadaPago: f.prima.fechaEstimadaPago || null, fechaPago: f.prima.fechaPago || null,
    }, v)),
    ok, fallo('prima'),
  )
  const gNumeros = useGuardadoDiferido(
    (n: NumeroDto[]) => enviar('datos', v => api.guardarNumeros(personaId, traducirIds('numeros', n), v)),
    (r, _hayMas, enviado) => confirmarIds('numeros', enviado, r), fallo('datos'),
  )
  const gCorreos = useGuardadoDiferido(
    (c: CorreoDto[]) => enviar('datos', v => api.guardarCorreos(personaId, traducirIds('correos', c), v)),
    (r, _hayMas, enviado) => confirmarIds('correos', enviado, r), fallo('datos'),
  )
  const gFamiliares = useGuardadoDiferido(
    (fs: FamiliarDto[]) => enviar('familiares', v => api.guardarFamiliares(personaId, traducirIds('familiares', fs), v)),
    (r, _hayMas, enviado) => confirmarIds('familiares', enviado, r), fallo('familiares'),
  )
  const todos = [gDatos, gVenta, gPrima, gNumeros, gCorreos, gFamiliares]
  useEffect(() => { guardadoresRef.current = todos })
  const grupo = (seccion: SeccionVersion) => seccion === 'datos' ? [gDatos, gNumeros, gCorreos] :
    seccion === 'venta' ? [gVenta] : seccion === 'prima' ? [gPrima] : [gFamiliares]
  const hayCambios = () => guardadoresRef.current.some(g => g.hayPendientes())
  const blocker = useBlocker(() => !nuevo && hayCambios())
  useEffect(() => {
    const prevenir = (e: BeforeUnloadEvent) => {
      if (hayCambios()) { e.preventDefault(); e.returnValue = '' }
    }
    window.addEventListener('beforeunload', prevenir)
    return () => window.removeEventListener('beforeunload', prevenir)
  }, [])
  const guardarPendientes = async () => {
    const resultados = await Promise.all(todos.map(g => g.flush()))
    const exito = resultados.every(Boolean) && !hayCambios()
    if (!exito) setErrorGuardar('Hay cambios pendientes o un conflicto. Revisá el aviso antes de salir.')
    return exito
  }
  const resolver = async (seccion: SeccionVersion, conservar: boolean) => {
    const actual = conflictos[seccion]
    if (!actual) return
    setResolviendo(true)
    await Promise.all(grupo(seccion).map(g => g.flush()))
    grupo(seccion).forEach(g => g.cancelar())
    const borrador = formRef.current
    editor.current.resolver(seccion, actual.versiones[seccion])
    const remoto = formDesde(actual)
    if (!conservar) {
      setForm(f => seccion === 'datos' ? { ...f, datos: remoto.datos, numeros: remoto.numeros, correos: remoto.correos } : { ...f, [seccion]: remoto[seccion] })
    } else if (seccion === 'datos') {
      gDatos.programar(borrador)
      if (actual.esCliente) { gNumeros.programar(borrador.numeros); gCorreos.programar(borrador.correos) }
    } else if (seccion === 'venta') gVenta.programar(borrador)
    else if (seccion === 'prima') gPrima.programar(borrador)
    else gFamiliares.programar(borrador.familiares)
    if (seccion === 'datos') setPersona(p => p ? { ...p, esCliente: actual.esCliente, cliente: actual.cliente } : p)
    setConflictos(cs => { const copia = { ...cs }; delete copia[seccion]; return copia })
    setErrorGuardar('')
    setGuardado(false)
    setResolviendo(false)
  }
  const vistaGrupo = (f: FormPersona, seccion: SeccionVersion): Record<string, string> => {
    if (seccion === 'familiares') return { Familiares: f.familiares.map(x =>
      [x.nombreCompleto, x.parentesco, x.telefono, x.correoElectronico, x.whatsapp].filter(Boolean).join(' · ')).join('\n') }
    const etiquetas: Record<string, string> = {
      nombres: 'Nombres', apellidos: 'Apellidos', cedula: 'Cédula', finca: 'Finca', expediente: 'Expediente',
      fechaIngreso: 'Fecha de ingreso', vendedorId: 'Vendedor', telefonoPrincipal: 'Teléfono principal',
      whatsappPrincipal: 'WhatsApp principal', correoPrincipal: 'Correo principal', estadoClienteId: 'Estado del cliente',
      origenClienteId: 'Origen', procedenciaVentaId: 'Procedencia', metodoVentaId: 'Método de venta', monto: 'Monto',
      notas: 'Notas', montoCancelado: 'Monto cancelado', fechaEstimadaPago: 'Fecha estimada de pago', fechaPago: 'Fecha de pago',
    }
    const opciones: Record<string, { id: number; nombre: string }[] | undefined> = {
      vendedorId: catalogos?.vendedores, estadoClienteId: catalogos?.estadosCliente, origenClienteId: catalogos?.origenes,
      procedenciaVentaId: catalogos?.procedencias, metodoVentaId: catalogos?.metodos,
    }
    const filas = Object.fromEntries(Object.entries(f[seccion]).map(([k, v]) =>
      [etiquetas[k] ?? k, opciones[k]?.find(x => String(x.id) === v)?.nombre ?? v]))
    if (seccion === 'datos') {
      filas['Números de contacto'] = f.numeros.map(x => `${x.tipo}: ${x.numero}${x.principal ? ' (principal)' : ''}`).join('\n')
      filas['Correos de contacto'] = f.correos.map(x => `${x.correo}${x.principal ? ' (principal)' : ''}`).join('\n')
    }
    return filas
  }

  const cambiar = (parte: 'datos' | 'venta' | 'prima', campo: string, valor: string) => {
    const nuevoForm = { ...form, [parte]: { ...form[parte], [campo]: valor } } as FormPersona
    setGuardado(false)
    setForm(nuevoForm)
    if (nuevo) return
    if (parte === 'datos') gDatos.programar(nuevoForm)
    else if (parte === 'venta') gVenta.programar(nuevoForm)
    else gPrima.programar(nuevoForm)
  }
  const cambiarLista = <K extends 'numeros' | 'correos' | 'familiares'>(lista: K, valor: FormPersona[K]) => {
    setGuardado(false)
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
    if (!nuevo && !await guardarPendientes()) return
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
      if (!await guardarPendientes()) return
      await enviar('datos', v => api.convertir(personaId, {
        expediente: conv.expediente.trim(),
        origenClienteId: Number(conv.origen),
        estadoClienteId: Number(conv.estado) || 1,
      }, v))
      navegar(`/gestion/clientes/${personaId}`)
    } catch (e) {
      fallo('datos')(e)
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

      {Object.entries(conflictos).map(([nombreSeccion, actual]) => {
        const seccion = nombreSeccion as SeccionVersion
        return <div key={seccion} role="alert" className="alerta">
          <strong>Conflicto en {seccion === 'datos' ? 'Datos y contactos' : seccion}.</strong>
          <p>Otra sesión guardó cambios. Tu borrador sigue en pantalla y el autoguardado de esta sección está pausado.</p>
          {actual ? <>
            <div style={{ overflowX: 'auto' }}><table style={{ width: '100%', textAlign: 'left' }}>
              <thead><tr><th>Campo</th><th>Mi borrador</th><th>Versión guardada</th></tr></thead>
              <tbody>{Object.entries(vistaGrupo(form, seccion)).map(([campo, valor]) => <tr key={campo}>
                <th>{campo}</th><td style={{ whiteSpace: 'pre-wrap' }}>{valor || '—'}</td>
                <td style={{ whiteSpace: 'pre-wrap' }}>{vistaGrupo(formDesde(actual), seccion)[campo] || '—'}</td>
              </tr>)}</tbody>
            </table></div>
            <p>Guardar tu borrador reemplaza los datos de esta sección, incluidas sus listas. Revisá ambas versiones antes de elegir.</p>
            <button className="btn-secundario" disabled={resolviendo} onClick={() => void resolver(seccion, false)}>Usar versión guardada</button>{' '}
            <button className="btn-primario" disabled={resolviendo} onClick={() => void resolver(seccion, true)}>Guardar mi borrador revisado</button>
          </> : <p>Obteniendo la versión guardada…</p>}
          <button className="btn-enlace" onClick={() => void revisar(seccion)}>Actualizar comparación</button>
        </div>
      })}
      {errorGuardar && !Object.keys(conflictos).length && <button className="btn-secundario" onClick={async () => {
        todos.forEach(g => g.reanudar())
        if (await guardarPendientes()) { setErrorGuardar(''); setGuardado(true) }
      }}>Reintentar guardado</button>}
      {blocker.state === 'blocked' && <div role="alert" className="alerta">
        <p>Hay cambios sin guardar. Permanecé en la ficha para guardarlos o resolver el conflicto.</p>
        <button className="btn-primario" onClick={() => blocker.reset()}>Seguir editando</button>{' '}
        <button className="btn-secundario" onClick={async () => { if (await guardarPendientes()) blocker.proceed() }}>Guardar y salir</button>
      </div>}

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
