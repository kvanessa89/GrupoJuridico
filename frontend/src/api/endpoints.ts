import { http } from './cliente'
import type {
  Catalogos, CatalogoItem, ClientesLista, ComentarioDto, CorreoDto, CrearProspecto, FamiliarDto, FiltrosLista,
  LoginResponse, NumeroDto, PersonaDetalle, PrimaDto, TipoCatalogo, Usuario, VentaDto, VentasLista,
} from './tipos'

import type { AxiosResponse } from 'axios'

function versionado<T>(r: AxiosResponse<T>) {
  const version = String(r.headers.etag ?? '').replaceAll('"', '')
  if (!version) throw new Error('La API no devolvió la versión del guardado.')
  return { data: r.data, version }
}
function condicion(version: string) { return { headers: { 'If-Match': `"${version}"` } } }

/** Convierte los filtros de pantalla a query string, sin los vacíos. */
function params(f: FiltrosLista, porPagina: number) {
  const p: Record<string, string | number | boolean> = { orden: f.orden, asc: f.asc, pagina: f.pagina, porPagina }
  if (f.q.trim()) p.q = f.q.trim()
  for (const k of ['vendedorId', 'estadoPrimaId', 'procedenciaId', 'mes', 'anio', 'origenId', 'estadoClienteId'] as const)
    if (f[k]) p[k] = f[k]
  return p
}

export const api = {
  login: (usuario: string, contrasena: string) =>
    http.post<LoginResponse>('/auth/login', { usuario, contrasena }).then((r) => r.data),
  yo: () => http.get<Usuario>('/auth/yo').then((r) => r.data),

  catalogos: () => http.get<Catalogos>('/catalogos').then((r) => r.data),
  crearCatalogo: (tipo: TipoCatalogo, item: { nombre: string; codigo?: string; apellidos?: string }) =>
    http.post<CatalogoItem>(`/catalogos/${tipo}`, item).then((r) => r.data),
  actualizarCatalogo: (tipo: TipoCatalogo, id: number, item: { nombre: string; codigo?: string; apellidos?: string }) =>
    http.put<CatalogoItem>(`/catalogos/${tipo}/${id}`, item).then((r) => r.data),
  eliminarCatalogo: (tipo: TipoCatalogo, id: number) => http.delete(`/catalogos/${tipo}/${id}`),
  guardarInteresMora: (porcentaje: number) => http.put<number>('/catalogos/interes-mora', { porcentaje }).then((r) => r.data),

  ventas: (f: FiltrosLista, porPagina: number) =>
    http.get<VentasLista>('/ventas', { params: params(f, porPagina) }).then((r) => r.data),
  clientes: (f: FiltrosLista, porPagina: number) =>
    http.get<ClientesLista>('/clientes', { params: params(f, porPagina) }).then((r) => r.data),

  persona: (id: number) => http.get<PersonaDetalle>(`/personas/${id}`).then((r) => r.data),
  crearProspecto: (datos: CrearProspecto) => http.post<PersonaDetalle>('/personas', datos).then((r) => r.data),
  guardarDatos: (id: number, datos: unknown, version: string) => http.put(`/personas/${id}/datos`, datos, condicion(version)).then(versionado),
  guardarVenta: (id: number, venta: Omit<VentaDto, 'id'>, version: string) => http.put<VentaDto>(`/personas/${id}/venta`, venta, condicion(version)).then(versionado),
  guardarPrima: (id: number, prima: Pick<PrimaDto, 'monto' | 'montoCancelado' | 'fechaEstimadaPago' | 'fechaPago'>, version: string) =>
    http.put<PrimaDto>(`/personas/${id}/prima`, prima, condicion(version)).then(versionado),
  guardarNumeros: (id: number, numeros: NumeroDto[], version: string) => http.put<NumeroDto[]>(`/personas/${id}/numeros`, numeros, condicion(version)).then(versionado),
  guardarCorreos: (id: number, correos: CorreoDto[], version: string) => http.put<CorreoDto[]>(`/personas/${id}/correos`, correos, condicion(version)).then(versionado),
  guardarFamiliares: (id: number, familiares: FamiliarDto[], version: string) =>
    http.put<FamiliarDto[]>(`/personas/${id}/familiares`, familiares, condicion(version)).then(versionado),
  convertir: (id: number, datos: { expediente: string | null; origenClienteId: number | null; estadoClienteId: number | null }, version: string) =>
    http.post<PersonaDetalle>(`/personas/${id}/convertir`, datos, condicion(version)).then(versionado),
  eliminarPersona: (id: number) => http.delete(`/personas/${id}`),
  comentar: (id: number, texto: string) => http.post<ComentarioDto>(`/personas/${id}/comentarios`, { texto }).then((r) => r.data),
  eliminarComentario: (id: number) => http.delete(`/comentarios/${id}`),

  usuarios: () => http.get<Usuario[]>('/usuarios').then((r) => r.data),
  crearUsuario: (u: { nombre: string; usuario: string; contrasena: string; rol: string }) =>
    http.post<Usuario>('/usuarios', u).then((r) => r.data),
  actualizarUsuario: (id: number, u: { nombre: string; usuario: string; contrasena: string | null; rol: string }) =>
    http.put<Usuario>(`/usuarios/${id}`, u).then((r) => r.data),
  eliminarUsuario: (id: number) => http.delete(`/usuarios/${id}`),
}
