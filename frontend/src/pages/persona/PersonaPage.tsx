import { useCallback, useEffect, useRef, useState } from 'react'
import { useBlocker, useNavigate, useParams } from 'react-router'
import { erroresDe, mensajeDe } from '../../api/cliente'
import { api } from '../../api/endpoints'
import type { ComentarioDto, CorreoDto, FamiliarDto, NumeroDto, PersonaDetalle } from '../../api/tipos'
import { useSesion } from '../../auth/Sesion'
import { EtiquetaEstadoGrande, EtiquetaPrimaGrande } from '../../components/Chips'
import { CampoMonto, CampoNotas, CampoSelect, CampoTelefono, CampoTexto, Select, TarjetaSeccion, type Opcion } from '../../components/Controles'
import { Cargando } from '../../components/Cargando'
import { Atras, Check } from '../../components/Iconos'
import { EditorSecciones, SeccionBloqueadaError, type SeccionVersion } from '../../hooks/EditorSecciones'
import { useGuardadoDiferido } from '../../hooks/useGuardadoDiferido'
import { fFecha, fNumero, iniciales, num, origenEtiqueta, soloDigitos, vendedorEtiqueta } from '../../utils/formato'
import {
  correoValido, ETIQUETAS, etiquetaDe, errorExpediente, MAX_FINCA, MAX_EXPEDIENTE, MAX_NOTAS_VENTA, faltaFechaPago, erroresDatos, erroresVenta, erroresPrima, errorNumeros, errorCorreos, errorFamiliares, faltantesNuevo, formDesde, formVacio, idONull, MAX_CEDULA, MAX_CORREO, MAX_NOMBRE, MSG_CORREO,
  payloadCrear, payloadDatos, primaCalculada, type FormPersona,
} from './formulario'
import { Comentarios, Contactos, Familiares } from './Secciones'

type ParteGuardada = 'datos' | 'venta' | 'prima' | 'numeros' | 'correos' | 'familiares'

type Pestana = 'general' | 'ventas' | 'familiares' | 'actividad'

/** Sección rechazada por la API (409) y quién la había guardado, según la respuesta. */
type Conflicto = { seccion: SeccionVersion; modificadoPorId: number | null; modificadoPor: string | null; modificadoEn: string | null }

/** "a las 10:42" si fue hoy; si no, "el 12 oct 2026 a las 10:42". */
function momento(iso: string): string {
  const d = new Date(iso)
  if (isNaN(d.getTime())) return ''
  const hora = d.toLocaleTimeString('es-CR', { hour: '2-digit', minute: '2-digit' })
  return d.toDateString() === new Date().toDateString() ? `a las ${hora}` : `el ${fFecha(d.toLocaleDateString('sv'))} a las ${hora}`
}

/**
 * Ficha de la persona. `seccion` indica desde qué tabla se entró: cambia el texto de "Volver"
 * y la etiqueta de estado del cliente (solo se muestra al entrar desde Clientes).
 */
export function PersonaPage({ seccion, nuevo = false }: { seccion: 'ventas' | 'clientes'; nuevo?: boolean }) {
  const { id } = useParams()
  const personaId = Number(id)
  const navegar = useNavigate()
  const { catalogos, usuario } = useSesion()

  const editor = useRef(new EditorSecciones())
  const idsNuevos = useRef({ numeros: new Map<number, number>(), correos: new Map<number, number>(), familiares: new Map<number, number>() })
  const [recargando, setRecargando] = useState(false)
  const [conflictos, setConflictos] = useState<Conflicto[]>([])
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

  const pendientesRef = useRef<{ campo: string; pestana: Pestana }[]>([])
  const ultimoForm = useRef(form)
  useEffect(() => { ultimoForm.current = form })
  const sucias = useRef(new Set<ParteGuardada>())
  const guardadoresRef = useRef<{ hayPendientes: () => boolean }[]>([])
  const esCliente = !!persona?.esCliente
  const ok = useCallback(() => {
    window.setTimeout(() => setGuardado(!pendientesRef.current.length && guardadoresRef.current.every(g => !g.hayPendientes())), 0)
  }, [])
  const fallo = (seccion: SeccionVersion) => (e: unknown) => {
    setGuardado(false)
    const respuesta = (e as { response?: { status?: number; data?: Partial<Conflicto> } }).response
    if (e instanceof SeccionBloqueadaError || [409, 428].includes(respuesta?.status ?? 0)) {
      const d = respuesta?.data
      setConflictos(cs => cs.some(c => c.seccion === seccion) ? cs
        : [...cs, { seccion, modificadoPorId: d?.modificadoPorId ?? null, modificadoPor: d?.modificadoPor ?? null, modificadoEn: d?.modificadoEn ?? null }])
      return
    }
    setErrorGuardar(mensajeDe(e, e instanceof Error ? e.message : 'No se pudieron guardar los cambios.'))
  }
  const enviar = <T,>(seccion: SeccionVersion, fn: (version: string) => Promise<{ data: T; version: string }>, vigente?: () => boolean) => editor.current.guardar(seccion, fn, vigente)
  const confirmarParte = (parte: ParteGuardada, enviado: FormPersona) => {
    if (ultimoForm.current[parte] === enviado[parte]) sucias.current.delete(parte)
    ok()
  }
  const traducirIds = <T extends { id: number }>(lista: 'numeros' | 'correos' | 'familiares', items: T[]) =>
    items.map(x => ({ ...x, id: idsNuevos.current[lista].get(x.id) ?? x.id }))
  const confirmarIds = <T extends { id: number }>(lista: 'numeros' | 'correos' | 'familiares', enviados: T[], recibidos: T[]) => {
    if ((ultimoForm.current[lista] as readonly { id: number }[]) === enviados) sucias.current.delete(lista)
    const existentes = new Set(traducirIds(lista, enviados).filter(x => x.id > 0).map(x => x.id))
    const nuevos = recibidos.filter(x => !existentes.has(x.id))
    enviados.filter(x => x.id <= 0 && !idsNuevos.current[lista].has(x.id)).forEach((x, i) => {
      if (nuevos[i]) idsNuevos.current[lista].set(x.id, nuevos[i].id)
    })
    setForm(f => ({ ...f, [lista]: f[lista].map(x => ({ ...x, id: idsNuevos.current[lista].get(x.id) ?? x.id })) }))
    ok()
  }

  // Un guardador por sección, como en el prototipo (cada cambio se guarda solo).
  const gDatos = useGuardadoDiferido((f: FormPersona, vigente) => enviar('datos', v => api.guardarDatos(personaId, payloadDatos(f, esCliente), v), vigente), (_r, _hayMas, f) => confirmarParte('datos', f), fallo('datos'))
  const gVenta = useGuardadoDiferido(
    (f: FormPersona, vigente) => enviar('venta', v => api.guardarVenta(personaId, {
      procedenciaVentaId: idONull(f.venta.procedenciaVentaId), metodoVentaId: idONull(f.venta.metodoVentaId),
      monto: num(f.venta.monto), notas: f.venta.notas,
    }, v), vigente),
    (_r, _hayMas, f) => confirmarParte('venta', f), fallo('venta'),
  )
  const gPrima = useGuardadoDiferido(
    (f: FormPersona, vigente) => enviar('prima', v => api.guardarPrima(personaId, {
      monto: num(f.prima.monto), montoCancelado: num(f.prima.montoCancelado),
      fechaEstimadaPago: f.prima.fechaEstimadaPago || null, fechaPago: f.prima.fechaPago || null,
    }, v), vigente),
    (_r, _hayMas, f) => confirmarParte('prima', f), fallo('prima'),
  )
  const gNumeros = useGuardadoDiferido(
    (n: NumeroDto[], vigente) => enviar('datos', v => api.guardarNumeros(personaId, traducirIds('numeros', n), v), vigente),
    (r, _hayMas, enviado) => confirmarIds('numeros', enviado, r), fallo('datos'),
  )
  const gCorreos = useGuardadoDiferido(
    (c: CorreoDto[], vigente) => enviar('datos', v => api.guardarCorreos(personaId, traducirIds('correos', c), v), vigente),
    (r, _hayMas, enviado) => confirmarIds('correos', enviado, r), fallo('datos'),
  )
  const gFamiliares = useGuardadoDiferido(
    (fs: FamiliarDto[], vigente) => enviar('familiares', v => api.guardarFamiliares(personaId,
      traducirIds('familiares', fs), v), vigente),
    (r, _hayMas, enviado) => confirmarIds('familiares', enviado, r), fallo('familiares'),
  )
  const todos = [gDatos, gVenta, gPrima, gNumeros, gCorreos, gFamiliares]
  useEffect(() => { guardadoresRef.current = todos })
  const grupo = (seccion: SeccionVersion) => seccion === 'datos' ? [gDatos, gNumeros, gCorreos] :
    seccion === 'venta' ? [gVenta] : seccion === 'prima' ? [gPrima] : [gFamiliares]
  /**
   * Campos requeridos que impiden salir de la ficha, con la pestaña donde están. Mientras falten,
   * esa parte no se autoguarda: cuentan como cambios pendientes para no perderlos al salir.
   */
  const pendientesDe = (f: FormPersona): { campo: string; pestana: Pestana }[] => {
    if (nuevo) return []
    const lista: { campo: string; pestana: Pestana }[] = []
    const agregar = (e: Record<string, string>, pestana: Pestana) =>
      Object.entries(e).forEach(([campo, mensaje]) => lista.push({ campo: `${etiquetaDe(campo)} ${mensaje}`, pestana }))
    agregar(erroresDatos(f, esCliente, catalogos?.paises), 'general')
    agregar(erroresVenta(f), 'ventas')
    agregar(erroresPrima(f), 'ventas')
    if (esCliente) {
      const numeros = errorNumeros(f.numeros, catalogos?.paises)
      const correos = errorCorreos(f.correos)
      if (numeros) lista.push({ campo: numeros, pestana: 'general' })
      if (correos) lista.push({ campo: correos, pestana: 'general' })
    }
    const familiares = errorFamiliares(f.familiares, catalogos?.paises)
    if (familiares) lista.push({ campo: familiares, pestana: 'familiares' })
    return lista
  }
  useEffect(() => { pendientesRef.current = pendientesDe(form) })
  /** Muestra el aviso y lleva a la pestaña del primer campo pendiente. Devuelve si había alguno. */
  const avisarPendientes = (lista: { pestana: Pestana }[]) => {
    if (!lista.length) return false
    setVolverIntento(true)
    setPestana(lista[0].pestana)
    return true
  }
  const hayCambios = () => pendientesRef.current.length > 0 || guardadoresRef.current.some(g => g.hayPendientes())
  const blocker = useBlocker(() => !nuevo && hayCambios())
  useEffect(() => {
    const prevenir = (e: BeforeUnloadEvent) => {
      if (hayCambios()) { e.preventDefault(); e.returnValue = '' }
    }
    window.addEventListener('beforeunload', prevenir)
    return () => window.removeEventListener('beforeunload', prevenir)
  }, [])
  const guardarPendientes = async () => {
    if (avisarPendientes(pendientesRef.current)) return false
    const resultados = await Promise.all(todos.map(g => g.flush()))
    const exito = resultados.every(Boolean) && !hayCambios()
    // Conserva un error más específico (ej. el del servidor) en lugar de taparlo con el genérico.
    if (!exito) setErrorGuardar(prev => prev || 'Hay cambios pendientes o un conflicto. Revise el aviso antes de salir.')
    return exito
  }
  // Descarta el borrador de la sección y trae lo que guardó el otro usuario.
  const recargar = async (seccion: SeccionVersion) => {
    setRecargando(true)
    try {
      await Promise.all(grupo(seccion).map(g => g.flush()))
      const actual = await api.persona(personaId)
      grupo(seccion).forEach(g => g.cancelar())
      const partes: ParteGuardada[] = seccion === 'datos' ? ['datos', 'numeros', 'correos'] : [seccion]
      partes.forEach(p => sucias.current.delete(p))
      editor.current.resolver(seccion, actual.versiones[seccion])
      const remoto = formDesde(actual)
      setForm(f => seccion === 'datos' ? { ...f, datos: remoto.datos, numeros: remoto.numeros, correos: remoto.correos } : { ...f, [seccion]: remoto[seccion] })
      if (seccion === 'datos') setPersona(p => p ? { ...p, esCliente: actual.esCliente, cliente: actual.cliente } : p)
      setConflictos(cs => cs.filter(c => c.seccion !== seccion))
    } catch (e) {
      setErrorGuardar(mensajeDe(e, 'No se pudo recargar la sección. Intente de nuevo.'))
    } finally { setRecargando(false) }
  }
  const nombreSeccion = (s: SeccionVersion) =>
    s === 'datos' ? (esCliente ? 'Datos y contactos' : 'Datos de la persona') : s === 'venta' ? 'Venta' : s === 'prima' ? 'Prima' : 'Familiares'
  const avisoConflicto = (c: Conflicto) => {
    const seccionTxt = <strong>{nombreSeccion(c.seccion)}</strong>
    const cuando = c.modificadoEn ? ' ' + momento(c.modificadoEn) : ''
    if (c.modificadoPorId != null && c.modificadoPorId === usuario?.id)
      return <>Usted modificó la sección {seccionTxt} desde otra pestaña o sesión{cuando}.</>
    if (c.modificadoPor) return <><strong>{c.modificadoPor}</strong> modificó la sección {seccionTxt}{cuando}.</>
    return <>Otro usuario modificó la sección {seccionTxt}{cuando}.</>
  }

  // Datos, números y correos comparten sección/version. Si uno es inválido se retiran
  // todos sus envíos pendientes; al corregirlo se programan los últimos valores editados.
  const programarValidos = (f: FormPersona, parte: ParteGuardada) => {
    ultimoForm.current = f
    sucias.current.add(parte)
    pendientesRef.current = pendientesDe(f)
    if (nuevo) return
    const grupoDatosValido = !Object.keys(erroresDatos(f, esCliente, catalogos?.paises)).length &&
      (!esCliente || (!errorNumeros(f.numeros, catalogos?.paises) && !errorCorreos(f.correos)))
    if (['datos', 'numeros', 'correos'].includes(parte)) {
      if (!grupoDatosValido) [gDatos, gNumeros, gCorreos].forEach(g => g.suspender())
      else {
        if (sucias.current.has('numeros')) gNumeros.programar(f.numeros)
        if (sucias.current.has('datos')) gDatos.programar(f)
        if (sucias.current.has('correos')) gCorreos.programar(f.correos)
      }
    }
    if (parte === 'venta') {
      if (Object.keys(erroresVenta(f)).length) gVenta.suspender()
      else if (sucias.current.has('venta')) gVenta.programar(f)
    }
    if (parte === 'prima') {
      if (Object.keys(erroresPrima(f)).length) gPrima.suspender()
      else if (sucias.current.has('prima')) gPrima.programar(f)
    }
    if (parte === 'familiares') {
      if (errorFamiliares(f.familiares, catalogos?.paises)) gFamiliares.suspender()
      else if (sucias.current.has('familiares')) gFamiliares.programar(f.familiares)
    }
  }

  const cambiar = (parte: 'datos' | 'venta' | 'prima', campo: string, valor: string) => {
    const siguiente = { ...form, [parte]: { ...form[parte], [campo]: valor } } as FormPersona
    setGuardado(false)
    setForm(siguiente)
    programarValidos(siguiente, parte)
  }
  const cambiarLista = <K extends 'numeros' | 'correos' | 'familiares'>(lista: K, valor: FormPersona[K]) => {
    if ((lista === 'familiares' || lista === 'numeros') && valor.some(f => !form[lista].some(actual => actual.id === f.id))) {
      setVolverIntento(false)
      setCrearIntento(false)
    }
    const siguiente = { ...form, [lista]: valor }
    setGuardado(false)
    setForm(siguiente)
    programarValidos(siguiente, lista)
  }

  const prima = primaCalculada(form)
  const fechaReq = prima.estado === 3
  const fechaFalta = (!nuevo || crearIntento) && faltaFechaPago(form)
  const errores = nuevo ? { ...faltantesNuevo(form, catalogos?.paises), ...(crearIntento ? erroresServidor : {}) } :
    { ...erroresDatos(form, esCliente, catalogos?.paises), ...erroresVenta(form), ...erroresPrima(form) }
  const err = (k: string) => (!nuevo || crearIntento ? errores[k] : undefined)
  const req = true

  const volver = async () => {
    if (avisarPendientes(pendientesDe(form))) return
    if (!nuevo && !await guardarPendientes()) return
    navegar(seccion === 'clientes' ? '/gestion/clientes' : '/gestion/ventas')
  }

  const crear = async () => {
    setCrearIntento(true)
    setErroresServidor({})
    if (Object.keys(faltantesNuevo(form, catalogos?.paises)).length) return
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
    const expedienteError = errorExpediente(conv.expediente)
    if (expedienteError) { setConv({ ...conv, error: expedienteError }); return }
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
      // Un conflicto de versión va al aviso general ("Recargar sección"); el resto queda en el panel,
      // donde se reintenta con "Confirmar" (el "Reintentar guardado" de arriba no repite la conversión).
      const status = (e as { response?: { status?: number } }).response?.status
      if (e instanceof SeccionBloqueadaError || status === 409 || status === 428) fallo('datos')(e)
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
  if (!nuevo && !persona) return <div className="contenedor-ficha"><Cargando pantalla texto="Cargando ficha…" /></div>

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
        <CampoTexto label={ETIQUETAS.nombres} valor={d.nombres} onChange={(v) => cambiar('datos', 'nombres', v)} maxLength={MAX_NOMBRE} requerido={req} error={err('nombres')} />
        <CampoTexto label={ETIQUETAS.apellidos} valor={d.apellidos} onChange={(v) => cambiar('datos', 'apellidos', v)} maxLength={MAX_NOMBRE} requerido={req} error={err('apellidos')} />
        <CampoTexto label={ETIQUETAS.cedula} valor={d.cedula} onChange={(v) => cambiar('datos', 'cedula', soloDigitos(v))} inputMode="numeric" maxLength={MAX_CEDULA} requerido={req} error={err('cedula')} />
        <CampoTexto label={ETIQUETAS.finca} valor={d.finca} onChange={(v) => cambiar('datos', 'finca', v)} placeholder="12-12345" maxLength={MAX_FINCA} requerido={req} error={err('finca')} />
        <CampoTexto label={ETIQUETAS.expediente} valor={d.expediente} onChange={(v) => cambiar('datos', 'expediente', v)} placeholder="12-123456-1234-AB" maxLength={MAX_EXPEDIENTE} error={err('expediente')} />
        <CampoTexto label={ETIQUETAS.fechaIngreso} tipo="date" valor={d.fechaIngreso} onChange={(v) => cambiar('datos', 'fechaIngreso', v)} requerido={req} error={err('fechaIngreso')} />
        <CampoSelect
          label={ETIQUETAS.vendedorId} valor={d.vendedorId} onChange={(v) => cambiar('datos', 'vendedorId', v)} requerido={req} error={err('vendedorId')}
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
            <CampoTelefono
              label={ETIQUETAS.telefonoPrincipal} paisId={idONull(d.telefonoPrincipalPaisId)} numero={d.telefonoPrincipal}
              onPais={(v) => cambiar('datos', 'telefonoPrincipalPaisId', String(v))} onNumero={(v) => cambiar('datos', 'telefonoPrincipal', v)}
              requerido={req} error={err('telefonoPrincipal')}
            />
            <CampoTelefono
              label={ETIQUETAS.whatsappPrincipal} paisId={idONull(d.whatsappPrincipalPaisId)} numero={d.whatsappPrincipal}
              onPais={(v) => cambiar('datos', 'whatsappPrincipalPaisId', String(v))} onNumero={(v) => cambiar('datos', 'whatsappPrincipal', v)}
              requerido={req} error={err('whatsappPrincipal')}
            />
            <CampoTexto
              label={ETIQUETAS.correoPrincipal} tipo="email" valor={d.correoPrincipal} maxLength={MAX_CORREO}
              onChange={(v) => cambiar('datos', 'correoPrincipal', v)}
              error={correoValido(d.correoPrincipal) ? err('correoPrincipal') : MSG_CORREO}
            />
          </>
        )}
      </div>
    </TarjetaSeccion>
  )

  const bloqueVenta = (
    <TarjetaSeccion titulo="Venta">
      <div className="grilla-campos">
        <CampoSelect
          label={ETIQUETAS.procedenciaVentaId} valor={form.venta.procedenciaVentaId} onChange={(v) => cambiar('venta', 'procedenciaVentaId', v)}
          requerido={req} error={err('procedenciaVentaId')}
          opciones={sel('Seleccione…', (cats?.procedencias ?? []).map((x) => ({ v: String(x.id), l: x.nombre })))}
        />
        <CampoSelect
          label={ETIQUETAS.metodoVentaId} valor={form.venta.metodoVentaId} onChange={(v) => cambiar('venta', 'metodoVentaId', v)}
          requerido={req} error={err('metodoVentaId')}
          opciones={sel('Seleccione…', (cats?.metodos ?? []).map((x) => ({ v: String(x.id), l: x.nombre })))}
        />
        <CampoMonto label={ETIQUETAS.montoVenta} valor={form.venta.monto} onChange={(v) => cambiar('venta', 'monto', v)} requerido={req} error={err('montoVenta')} />
      </div>
      <CampoNotas label={ETIQUETAS.notasVenta} maxLength={MAX_NOTAS_VENTA} error={err('notasVenta')} valor={form.venta.notas} onChange={(v) => cambiar('venta', 'notas', v)} />
    </TarjetaSeccion>
  )

  const bloquePrima = (
    <TarjetaSeccion titulo="Prima">
      <div className="grilla-campos">
        <CampoMonto label={ETIQUETAS.montoPrima} valor={form.prima.monto} onChange={(v) => cambiar('prima', 'monto', v)} requerido={req} error={err('montoPrima')} />
        <CampoMonto label={ETIQUETAS.montoCancelado} valor={form.prima.montoCancelado} onChange={(v) => cambiar('prima', 'montoCancelado', v)} requerido error={err('montoCancelado')} />
        <CampoTexto label="Saldo pendiente" valor={fNumero(prima.saldo)} soloLectura />
        <CampoTexto label={ETIQUETAS.fechaEstimadaPago} tipo="date" valor={form.prima.fechaEstimadaPago} onChange={(v) => cambiar('prima', 'fechaEstimadaPago', v)} />
        <CampoTexto
          label={ETIQUETAS.fechaPago} tipo="date" valor={form.prima.fechaPago} onChange={(v) => cambiar('prima', 'fechaPago', v)}
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
      {volverIntento && pendientesDe(form).map(({ campo }) => (
        <div key={campo} role="alert" className="alerta">
          Cambio pendiente: {campo}
        </div>
      ))}

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

      {errorGuardar && <div role="alert" className="alerta">
        {errorGuardar}
        {/* Con un conflicto abierto se resuelve con "Recargar sección", no reintentando. */}
        {!conflictos.length && <>{' '}<button className="btn-enlace en-alerta" onClick={async () => {
          todos.forEach(g => g.reanudar())
          if (await guardarPendientes()) { setErrorGuardar(''); setGuardado(true) }
        }}>Reintentar guardado</button></>}
      </div>}

      {conflictos.map(c => <div key={c.seccion} role="alert" className="alerta">
        <p>{avisoConflicto(c)} Sus cambios en esta sección no se guardaron.</p>
        <button className="btn-secundario" disabled={recargando} onClick={() => void recargar(c.seccion)}>Recargar sección</button>
      </div>)}
      {blocker.state === 'blocked' && <div role="alert" className="alerta">
        <p>Hay cambios sin guardar. Permanezca en la ficha para guardarlos o resolver el conflicto.</p>
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
                <input className="campo" value={conv.expediente} placeholder="12-123456-1234-AB" maxLength={MAX_EXPEDIENTE} onChange={(e) => setConv({ ...conv, expediente: e.target.value })} />
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
          <Contactos mostrarErrores={volverIntento || blocker.state === 'blocked'} numeros={form.numeros} correos={form.correos} onNumeros={(n) => cambiarLista('numeros', n)} onCorreos={(c) => cambiarLista('correos', c)} />
        )}
        {en('ventas') && persona?.venta !== null && bloqueVenta}
        {en('ventas') && persona?.prima !== null && bloquePrima}
        {en('familiares') && <Familiares familiares={form.familiares} mostrarErrores={volverIntento || crearIntento || blocker.state === 'blocked'} onCambiar={(f) => cambiarLista('familiares', f)} />}
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
              <span role="alert" style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--rojo)' }}>Complete los campos requeridos antes de guardar.</span>
            )}
          </>
        )}
      </div>
    </div>
  )
}





