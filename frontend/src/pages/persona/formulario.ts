import { errorTelefono, type PaisTelefono } from '../../utils/telefonos.ts'
import type { CorreoDto, CrearProspecto, FamiliarDto, NumeroDto, PersonaDetalle } from '../../api/tipos'
import { limiteFamiliares, limiteNumeros, limiteCorreos } from './limitesContactos.ts'
import { estadoDePrima, hoyIso, num } from '../../utils/formato.ts'

/** Estado editable de la ficha. Los montos y selects se guardan como texto, igual que en los campos. */
export interface FormPersona {
  datos: {
    nombres: string
    apellidos: string
    cedula: string
    finca: string
    expediente: string
    fechaIngreso: string
    vendedorId: string
    telefonoPrincipal: string
    whatsappPrincipal: string
    /** Id del país como texto; vacío toma el país por defecto. */
    telefonoPrincipalPaisId: string
    whatsappPrincipalPaisId: string
    correoPrincipal: string
    estadoClienteId: string
    origenClienteId: string
  }
  venta: { procedenciaVentaId: string; metodoVentaId: string; monto: string; notas: string }
  prima: { monto: string; montoCancelado: string; fechaEstimadaPago: string; fechaPago: string }
  numeros: NumeroDto[]
  correos: CorreoDto[]
  familiares: FamiliarDto[]
}

const s = (v: unknown) => (v == null ? '' : String(v))

export function formVacio(): FormPersona {
  const hoy = hoyIso()
  return {
    datos: {
      nombres: '', apellidos: '', cedula: '', finca: '', expediente: '', fechaIngreso: hoy, vendedorId: '',
      telefonoPrincipal: '', whatsappPrincipal: '', telefonoPrincipalPaisId: '', whatsappPrincipalPaisId: '',
      correoPrincipal: '', estadoClienteId: '', origenClienteId: '',
    },
    venta: { procedenciaVentaId: '', metodoVentaId: '', monto: '0', notas: '' },
    // La prima nueva nace con fecha estimada hoy, para que aparezca en el mes actual.
    prima: { monto: '0', montoCancelado: '0', fechaEstimadaPago: hoy, fechaPago: '' },
    numeros: [],
    correos: [],
    familiares: [],
  }
}

export function formDesde(p: PersonaDetalle): FormPersona {
  return {
    datos: {
      nombres: p.nombres, apellidos: p.apellidos, cedula: p.cedula, finca: p.finca, expediente: p.expediente,
      fechaIngreso: p.fechaIngreso, vendedorId: s(p.vendedorId),
      telefonoPrincipal: p.telefonoPrincipal, whatsappPrincipal: p.whatsappPrincipal,
      telefonoPrincipalPaisId: s(p.telefonoPrincipalPaisId), whatsappPrincipalPaisId: s(p.whatsappPrincipalPaisId),
      correoPrincipal: p.correoPrincipal,
      estadoClienteId: s(p.cliente?.estadoClienteId), origenClienteId: s(p.cliente?.origenClienteId),
    },
    venta: {
      procedenciaVentaId: s(p.venta?.procedenciaVentaId), metodoVentaId: s(p.venta?.metodoVentaId),
      monto: s(p.venta?.monto ?? 0), notas: p.venta?.notas ?? '',
    },
    prima: {
      monto: s(p.prima?.monto ?? 0), montoCancelado: s(p.prima?.montoCancelado ?? 0),
      fechaEstimadaPago: s(p.prima?.fechaEstimadaPago), fechaPago: s(p.prima?.fechaPago),
    },
    numeros: p.numeros,
    correos: p.correos,
    familiares: p.familiares,
  }
}

export const idONull = (v: string) => (Number(v) > 0 ? Number(v) : null)

export function primaCalculada(f: FormPersona) {
  const monto = num(f.prima.monto)
  const cancelado = num(f.prima.montoCancelado)
  return { monto, cancelado, saldo: Math.max(0, monto - cancelado), estado: estadoDePrima(monto, cancelado) }
}

/** La prima quedó pagada (cancelado ≥ monto) pero falta la fecha de pago: no se puede guardar así. */
export function faltaFechaPago(f: FormPersona): boolean {
  return primaCalculada(f).estado === 3 && !f.prima.fechaPago.trim()
}

/** Datos para PUT /personas/{id}/datos. Los contactos principales solo se envían para prospectos. */
export function payloadDatos(f: FormPersona, esCliente: boolean) {
  const d = f.datos
  return {
    nombres: d.nombres, apellidos: d.apellidos, cedula: d.cedula, finca: d.finca, expediente: d.expediente,
    fechaIngreso: d.fechaIngreso, vendedorId: idONull(d.vendedorId),
    telefonoPrincipal: esCliente ? null : d.telefonoPrincipal,
    whatsappPrincipal: esCliente ? null : d.whatsappPrincipal,
    telefonoPrincipalPaisId: esCliente ? null : idONull(d.telefonoPrincipalPaisId),
    whatsappPrincipalPaisId: esCliente ? null : idONull(d.whatsappPrincipalPaisId),
    correoPrincipal: esCliente ? null : d.correoPrincipal,
    estadoClienteId: esCliente ? idONull(d.estadoClienteId) : null,
    origenClienteId: esCliente ? idONull(d.origenClienteId) : null,
  }
}

export function payloadCrear(f: FormPersona): CrearProspecto {
  const d = f.datos
  return {
    nombres: d.nombres, apellidos: d.apellidos, cedula: d.cedula, finca: d.finca, expediente: d.expediente,
    fechaIngreso: d.fechaIngreso || null, vendedorId: idONull(d.vendedorId),
    telefonoPrincipal: d.telefonoPrincipal, whatsappPrincipal: d.whatsappPrincipal, correoPrincipal: d.correoPrincipal,
    telefonoPrincipalPaisId: idONull(d.telefonoPrincipalPaisId), whatsappPrincipalPaisId: idONull(d.whatsappPrincipalPaisId),
    procedenciaVentaId: idONull(f.venta.procedenciaVentaId), metodoVentaId: idONull(f.venta.metodoVentaId),
    montoVenta: num(f.venta.monto), notasVenta: f.venta.notas,
    montoPrima: num(f.prima.monto), montoCancelado: num(f.prima.montoCancelado),
    fechaEstimadaPago: f.prima.fechaEstimadaPago || null, fechaPago: f.prima.fechaPago || null,
    familiares: f.familiares,
  }
}

const vacio = (x: string) => !x.trim()
export const MSG_CONTACTO = 'Ingrese un teléfono o un WhatsApp.'

/**
 * Etiqueta visible de cada campo, por la misma clave que usan erroresDatos/Venta/Prima.
 * La ficha toma de aquí el label de cada input y el aviso de pendientes nombra el campo con ella.
 */
export const ETIQUETAS = {
  nombres: 'Nombres',
  apellidos: 'Apellidos',
  cedula: 'Cédula',
  finca: 'Número de finca',
  fechaIngreso: 'Fecha de ingreso',
  vendedorId: 'Vendido por',
  telefonoPrincipal: 'Teléfono principal',
  whatsappPrincipal: 'WhatsApp principal',
  correoPrincipal: 'Correo electrónico principal',
  procedenciaVentaId: 'Procedencia de venta',
  metodoVentaId: 'Método de venta',
  montoVenta: 'Monto de la venta',
  montoPrima: 'Monto de prima',
  montoCancelado: 'Monto cancelado',
  fechaEstimadaPago: 'Fecha estimada de pago',
  fechaPago: 'Fecha de pago',
} as const

/** Etiqueta del campo; si la clave no está en el mapa, la clave tal cual. */
export const etiquetaDe = (clave: string): string => (ETIQUETAS as Record<string, string>)[clave] ?? clave

/** Largos máximos (iguales a Persona.MaxNombre / MaxCedula / MaxCorreo en el backend). */
export const MAX_NOMBRE = 20
export const MAX_CEDULA = 15
export const MAX_CORREO = 100
export const MAX_NOMBRE_FAMILIAR = 60
export const MSG_CORREO = 'Correo electrónico inválido.'

/** Misma regla que el backend: usuario@dominio.ext sin espacios. Vacío es válido (el correo es opcional). */
export function correoValido(v: string): boolean {
  return vacio(v) || /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(v.trim())
}

export const MSG_NOMBRE_FAMILIAR = 'Ingrese el nombre del familiar.'

/** Fila de familiar sin ningún dato: se ignora (no se guarda ni muestra error). */
export function familiarVacio(x: FamiliarDto): boolean {
  return vacio(x.nombreCompleto) && x.parentescoId == null && vacio(x.telefono) && vacio(x.correoElectronico) && vacio(x.whatsapp)
}

/** El nombre es requerido en cualquier familiar con datos (o ya guardado). */
export function faltaNombreFamiliar(x: FamiliarDto): boolean {
  return vacio(x.nombreCompleto)
}

/** Datos inválidos en la ficha: mientras haya alguno, esa parte no se envía al autoguardado. */
export const correosValidos = {
  datos: (f: FormPersona) => correoValido(f.datos.correoPrincipal),
  correos: (f: FormPersona) => f.correos.every((c) => correoValido(c.correo)),
  familiares: (f: FormPersona) => f.familiares.every((x) => correoValido(x.correoElectronico) && !faltaNombreFamiliar(x)),
}

export const MSG_CONTACTO_FAMILIAR = 'Ingrese un teléfono o un correo del familiar.'

export function faltaContactoFamiliar(x: FamiliarDto): boolean {
  return !x.telefono.replace(/\D/g, '') && vacio(x.correoElectronico)
}

export function errorMonto(v: string, permiteCero = false): string | undefined {
  if (vacio(v)) return 'Campo requerido.'
  if (!/^\d+(?:\.\d{1,2})?$/.test(v) || !Number.isFinite(Number(v))) return 'Complete un monto válido.'
  if (permiteCero ? Number(v) < 0 : Number(v) <= 0) return permiteCero ? 'No puede ser negativo.' : 'Debe ser mayor que cero.'
}

function fechaValida(v: string): boolean {
  return /^\d{4}-\d{2}-\d{2}$/.test(v) && v.slice(0, 4) !== '0000' &&
    !isNaN(Date.parse(v)) && new Date(v).toISOString().slice(0, 10) === v
}

export function erroresDatos(f: FormPersona, esCliente: boolean, paises: readonly PaisTelefono[] = []): Record<string, string> {
  const e: Record<string, string> = {}
  const d = f.datos
  for (const k of ['nombres', 'apellidos', 'cedula', 'finca'] as const) if (vacio(d[k])) e[k] = 'Campo requerido.'
  if (!fechaValida(d.fechaIngreso)) e.fechaIngreso = 'Ingrese una fecha válida.'
  if (!idONull(d.vendedorId)) e.vendedorId = 'Campo requerido.'
  if (!esCliente) {
    if (!correoValido(d.correoPrincipal)) e.correoPrincipal = MSG_CORREO
    const telefono = errorTelefono(d.telefonoPrincipal, idONull(d.telefonoPrincipalPaisId), paises)
    const whatsapp = errorTelefono(d.whatsappPrincipal, idONull(d.whatsappPrincipalPaisId), paises)
    if (telefono) e.telefonoPrincipal = telefono
    if (whatsapp) e.whatsappPrincipal = whatsapp
    if (!d.telefonoPrincipal.replace(/\D/g, '') && !d.whatsappPrincipal.replace(/\D/g, '')) {
      e.telefonoPrincipal = MSG_CONTACTO
      e.whatsappPrincipal = MSG_CONTACTO
    }
  }
  return e
}

export function erroresVenta(f: FormPersona): Record<string, string> {
  const e: Record<string, string> = {}
  if (!idONull(f.venta.procedenciaVentaId)) e.procedenciaVentaId = 'Campo requerido.'
  if (!idONull(f.venta.metodoVentaId)) e.metodoVentaId = 'Campo requerido.'
  const monto = errorMonto(f.venta.monto)
  if (monto) e.montoVenta = monto
  return e
}

export function erroresPrima(f: FormPersona): Record<string, string> {
  const e: Record<string, string> = {}
  const monto = errorMonto(f.prima.monto)
  const cancelado = errorMonto(f.prima.montoCancelado, true)
  if (monto) e.montoPrima = monto
  if (cancelado) e.montoCancelado = cancelado
  if (faltaFechaPago(f)) e.fechaPago = 'Requerida cuando la prima está pagada.'
  for (const k of ['fechaEstimadaPago', 'fechaPago'] as const)
    if (f.prima[k] && !fechaValida(f.prima[k])) e[k] = 'Ingrese una fecha válida.'
  return e
}

export function errorNumeros(numeros: NumeroDto[], paises: readonly PaisTelefono[] = []): string | undefined {
  return limiteNumeros(numeros) ?? (numeros.length === 0 ? MSG_CONTACTO :
    numeros.some(n => !n.numero.replace(/\D/g, '')) ? 'Complete el número o quite la fila vacía.' :
    numeros.map(n => errorTelefono(n.numero, n.paisId, paises)).find(Boolean))
}

export function errorCorreos(correos: CorreoDto[]): string | undefined {
  return limiteCorreos(correos) ?? (correos.every(c => correoValido(c.correo)) ? undefined : MSG_CORREO)
}

export function errorFamiliares(familiares: FamiliarDto[], paises: readonly PaisTelefono[] = []): string | undefined {
  return limiteFamiliares(familiares) ?? (familiares.some(faltaNombreFamiliar) ? MSG_NOMBRE_FAMILIAR :
    familiares.some(faltaContactoFamiliar) ? MSG_CONTACTO_FAMILIAR :
    !familiares.every(f => correoValido(f.correoElectronico)) ? MSG_CORREO :
    familiares.map(f => errorTelefono(f.telefono, f.telefonoPaisId, paises) ?? errorTelefono(f.whatsapp, f.whatsappPaisId, paises)).find(Boolean))
}

/** Crear y editar comparten las reglas; cada familiar agregado debe completarse o quitarse. */
export function faltantesNuevo(f: FormPersona, paises: readonly PaisTelefono[] = []): Record<string, string> {
  const e = { ...erroresDatos(f, false, paises), ...erroresVenta(f), ...erroresPrima(f) }
  const familiares = errorFamiliares(f.familiares, paises)
  if (familiares) e.familiares = familiares
  return e
}

