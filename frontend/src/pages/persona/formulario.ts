import type { CorreoDto, CrearProspecto, FamiliarDto, NumeroDto, PersonaDetalle } from '../../api/tipos'
import { estadoDePrima, hoyIso, num } from '../../utils/formato'

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
      telefonoPrincipal: '', whatsappPrincipal: '', correoPrincipal: '', estadoClienteId: '', origenClienteId: '',
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
      telefonoPrincipal: p.telefonoPrincipal, whatsappPrincipal: p.whatsappPrincipal, correoPrincipal: p.correoPrincipal,
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

/** Datos para PUT /personas/{id}/datos. Los contactos principales solo se envían para prospectos. */
export function payloadDatos(f: FormPersona, esCliente: boolean) {
  const d = f.datos
  return {
    nombres: d.nombres, apellidos: d.apellidos, cedula: d.cedula, finca: d.finca, expediente: d.expediente,
    fechaIngreso: d.fechaIngreso || hoyIso(), vendedorId: idONull(d.vendedorId),
    telefonoPrincipal: esCliente ? null : d.telefonoPrincipal,
    whatsappPrincipal: esCliente ? null : d.whatsappPrincipal,
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
    procedenciaVentaId: idONull(f.venta.procedenciaVentaId), metodoVentaId: idONull(f.venta.metodoVentaId),
    montoVenta: num(f.venta.monto), notasVenta: f.venta.notas,
    montoPrima: num(f.prima.monto), montoCancelado: num(f.prima.montoCancelado),
    fechaEstimadaPago: f.prima.fechaEstimadaPago || null, fechaPago: f.prima.fechaPago || null,
    familiares: f.familiares,
  }
}

const vacio = (x: string) => !x.trim()
const sinMonto = (x: string) => vacio(x) || !(num(x) > 0)
export const MSG_CONTACTO = 'Ingresá al menos un teléfono o un WhatsApp.'

/** Campos requeridos al registrar un prospecto (misma regla que el backend). */
export function faltantesNuevo(f: FormPersona): Record<string, string> {
  const e: Record<string, string> = {}
  const d = f.datos
  if (vacio(d.nombres)) e.nombres = 'Campo requerido.'
  if (vacio(d.apellidos)) e.apellidos = 'Campo requerido.'
  if (vacio(d.cedula)) e.cedula = 'Campo requerido.'
  if (vacio(d.finca)) e.finca = 'Campo requerido.'
  if (vacio(d.fechaIngreso)) e.fechaIngreso = 'Campo requerido.'
  if (!idONull(d.vendedorId)) e.vendedorId = 'Campo requerido.'
  if (vacio(d.telefonoPrincipal) && vacio(d.whatsappPrincipal)) {
    e.telefonoPrincipal = MSG_CONTACTO
    e.whatsappPrincipal = MSG_CONTACTO
  }
  if (!idONull(f.venta.procedenciaVentaId)) e.procedenciaVentaId = 'Campo requerido.'
  if (!idONull(f.venta.metodoVentaId)) e.metodoVentaId = 'Campo requerido.'
  if (sinMonto(f.venta.monto)) e.montoVenta = 'Campo requerido.'
  if (sinMonto(f.prima.monto)) e.montoPrima = 'Campo requerido.'
  return e
}
