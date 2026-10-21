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
  /** Países de los teléfonos: `codigo` es el prefijo ("+506"). */
  paises: CatalogoItem[]
  parentescos: CatalogoItem[]
  roles: { id: number; nombre: Rol }[]
  interesMora: number
  /** Estados de cobros (solo lectura): ids fijos, el nombre viene del catálogo. */
  estadosCuotaPago: CatalogoItem[]
  estadosCuotaVencimiento: CatalogoItem[]
  estadosPromesa: CatalogoItem[]
  /** Tipos de documento de los archivos del expediente. */
  tiposDocumento: CatalogoItem[]
}

export type TipoCatalogo = 'vendedores' | 'procedencias' | 'metodos' | 'estados-prima' | 'origenes' | 'estados-cliente' | 'paises' | 'parentescos' | 'tipos-documento'

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
  /** Nulo solo en filas nuevas que todavía no eligieron país (la API toma el primero). */
  paisId: number | null
}

export interface CorreoDto {
  id: number
  correo: string
  principal: boolean
}

export interface FincaDto {
  id: number
  numero: string
}

export interface FamiliarDto {
  id: number
  nombreCompleto: string
  /** Id del catálogo de parentescos; nulo si no se eligió. */
  parentescoId: number | null
  telefono: string
  correoElectronico: string
  telefonoPaisId: number | null
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
  versiones: Record<'datos' | 'venta' | 'prima' | 'familiares', string>
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
  telefonoPrincipalPaisId: number | null
  whatsappPrincipalPaisId: number | null
  correoPrincipal: string
  venta: VentaDto | null
  prima: PrimaDto | null
  cliente: ClienteDto | null
  numeros: NumeroDto[]
  correos: CorreoDto[]
  /** Todas las fincas, en orden; `finca` es la primera. */
  fincas: FincaDto[]
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
  telefonoPrincipalPaisId: number | null
  whatsappPrincipalPaisId: number | null
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

// ---------- Cobros ----------

/** Tarjeta de la sección Cobros que filtra la tabla. */
export type VistaCobros = 'morosos' | 'vencido' | 'recordatorios' | 'promesas' | 'cobro-judicial'

export interface CobrosFiltros {
  vista: VistaCobros
  q: string
  orden: string
  asc: boolean
  pagina: number
}

export interface CobroFila {
  id: number
  expediente: string
  nombreCompleto: string
  telefono: string | null
  whatsapp: string | null
  estadoClienteId: number
  estadoCliente: string
  tienePlan: boolean
  cantidadCuotas: number
  cuotasVencidas: number
  montoVencido: number
  atraso: number
  proxima: { fecha: string; saldo: number } | null
  promesa: { estadoId: number; fecha: string } | null
  recordatorio: { numeroCuota: number; saldo: number; fecha: string } | null
}

export interface CobrosLista {
  filas: CobroFila[]
  total: number
  pagina: number
  porPagina: number
  totales: { morosos: number; montoVencido: number; recordatorios: number; promesasPendientes: number; cobroJudicial: number; fechasARecordar: string[] }
  hoy: string
}

export interface PagoDto {
  id: number
  montoPago: number
  fechaPago: string
  comprobantes: { id: number; nombre: string; disponible: boolean }[]
}

export interface PromesaDto {
  id: number
  fechaPromesa: string
  montoPrometido: number
  montoPagado: number
  estadoId: number
}

export interface CuotaDto {
  id: number
  numeroCuota: number
  fechaVencimiento: string
  montoCuota: number
  /** Interés que se cobra hoy (0 si se acordó no cobrarlo). */
  mora: number
  /** Interés que correspondería aunque haya acuerdo. */
  moraPosible: number
  sinMora: boolean | null
  total: number
  pagado: number
  saldo: number
  estadoPagoId: number
  estadoVencimientoId: number
  pagos: PagoDto[]
  promesas: PromesaDto[]
}

export interface PlanDetalle {
  id: number
  montoTotal: number
  montoCuota: number
  cantidadCuotas: number
  fechaInicio: string
  diaPago: number
  saldo: number
  pagadoTotal: number
  cuotasPagadas: number
  cuotasVencidas: number
  montoVencido: number
  atraso: number
  promesasPendientes: number
  promesasCumplidas: number
  promesasIncumplidas: number
  cuotas: CuotaDto[]
}

export interface PlanPersona {
  personaId: number
  esCliente: boolean
  expediente: string
  nombres: string
  apellidos: string
  fechaIngreso: string
  estadoClienteId: number | null
  interesMora: number
  /** Monto de la venta menos la prima: se propone al crear el plan. */
  montoSugerido: number
  puedeGestionar: boolean
  hoy: string
  plan: PlanDetalle | null
}

export interface CrearPlan {
  montoTotal: number
  cantidadCuotas: number
  fechaInicio: string | null
  diaPago: number
}

// ---------- Archivos del expediente ----------

export interface ArchivoDto {
  id: number
  tipoDocumentoId: number
  nombre: string
  tamano: number
  fechaCarga: string
  /** Solo quien puede subir ese tipo de documento puede quitarlo (los comprobantes de un pago, nunca desde aquí). */
  puedeEliminar: boolean
  /** Número de cuota si es el comprobante de un pago de Cobros. */
  cuotaPago?: number | null
}

export interface ArchivosPersona {
  /** Solo los tipos de documento que el rol puede ver. */
  archivos: ArchivoDto[]
  /** Tipos de documento que el rol puede subir (vacío si no es cliente). */
  tiposSubir: number[]
}

export interface PermisoDocumento {
  tipoDocumentoId: number
  rol: Rol
  puedeVer: boolean
  puedeSubir: boolean
}

/** Respuesta de error de ASP.NET Core (ValidationProblemDetails). */
export interface Problema {
  title?: string
  status?: number
  errors?: Record<string, string[]>
}
