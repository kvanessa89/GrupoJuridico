// Contratos de la API (ver backend/src/GrupoJuridico.Gestion.Application).

export type Rol = 'Administrador' | 'Asistente de Ventas' | 'Cobros'

export interface Usuario {
  id: number
  nombre: string
  usuario: string
  rol: Rol
}

export interface LoginResponse {
  token: string
  usuario: Usuario
}

export interface CatalogoItem {
  id: number
  codigo: string
  nombre: string
  apellidos?: string | null
}

export interface Catalogos {
  vendedores: CatalogoItem[]
  procedencias: CatalogoItem[]
  metodos: CatalogoItem[]
  estadosPrima: CatalogoItem[]
  origenes: CatalogoItem[]
  estadosCliente: CatalogoItem[]
  roles: { id: number; nombre: Rol }[]
  interesMora: number
}

export type TipoCatalogo = 'vendedores' | 'procedencias' | 'metodos' | 'estados-prima' | 'origenes' | 'estados-cliente'

export interface FiltrosLista {
  q: string
  vendedorId: string
  estadoPrimaId: string
  procedenciaId: string
  mes: string
  anio: string
  origenId: string
  estadoClienteId: string
  orden: string
  asc: boolean
  pagina: number
}

export interface VentaFila {
  id: number
  expediente: string
  nombreCompleto: string
  telefono: string | null
  whatsapp: string | null
  procedencia: string | null
  metodo: string | null
  vendedor: string | null
  montoPrima: number
  saldoPendiente: number
  fechaEstimadaPago: string | null
  estadoPrimaId: number
  estadoPrima: string
  esCliente: boolean
}

export interface VentasLista {
  filas: VentaFila[]
  total: number
  pagina: number
  porPagina: number
  totales: { montoVentas: number | null; montoPrimas: number; primasPagadas: number; primasPendientes: number }
  mesesPago: string[]
}

export interface ClienteFila {
  id: number
  expediente: string
  nombreCompleto: string
  telefono: string | null
  correo: string | null
  finca: string | null
  fechaIngreso: string
  estadoPrimaId: number | null
  estadoPrima: string | null
  estadoClienteId: number
  estadoCliente: string
  origen: string | null
}

export interface ClientesLista {
  filas: ClienteFila[]
  total: number
  pagina: number
  porPagina: number
  aniosIngreso: number[]
}

export interface VentaDto {
  id: number
  procedenciaVentaId: number | null
  metodoVentaId: number | null
  monto: number
  notas: string
}

export interface PrimaDto {
  id: number
  monto: number
  montoCancelado: number
  saldoPendiente: number
  fechaEstimadaPago: string | null
  fechaPago: string | null
  estadoPrimaId: number
}

export interface ClienteDto {
  id: number
  origenClienteId: number
  estadoClienteId: number
  notas: string
}

export type TipoNumero = 'Teléfono' | 'WhatsApp'

export interface NumeroDto {
  id: number
  numero: string
  tipo: TipoNumero
  principal: boolean
}

export interface CorreoDto {
  id: number
  correo: string
  principal: boolean
}

export interface FamiliarDto {
  id: number
  nombreCompleto: string
  parentesco: string
  telefono: string
  correoElectronico: string
  whatsapp: string
}

export interface ComentarioDto {
  id: number
  usuarioId: number
  autor: string
  rol: string | null
  texto: string
  fecha: string
  puedeEliminar: boolean
}

export interface PersonaDetalle {
  id: number
  esCliente: boolean
  expediente: string
  nombres: string
  apellidos: string
  cedula: string
  finca: string
  fechaIngreso: string
  vendedorId: number | null
  telefonoPrincipal: string
  whatsappPrincipal: string
  correoPrincipal: string
  venta: VentaDto | null
  prima: PrimaDto | null
  cliente: ClienteDto | null
  numeros: NumeroDto[]
  correos: CorreoDto[]
  familiares: FamiliarDto[]
  comentarios: ComentarioDto[]
}

export interface CrearProspecto {
  nombres: string
  apellidos: string
  cedula: string
  finca: string
  expediente: string
  fechaIngreso: string | null
  vendedorId: number | null
  telefonoPrincipal: string
  whatsappPrincipal: string
  correoPrincipal: string
  procedenciaVentaId: number | null
  metodoVentaId: number | null
  montoVenta: number
  notasVenta: string
  montoPrima: number
  montoCancelado: number
  fechaEstimadaPago: string | null
  fechaPago: string | null
  familiares: FamiliarDto[]
}

/** Respuesta de error de ASP.NET Core (ValidationProblemDetails). */
export interface Problema {
  title?: string
  status?: number
  errors?: Record<string, string[]>
}
