import axios, { AxiosError } from 'axios'
import type { Problema } from './tipos'

const CLAVE_TOKEN = 'gj.token'

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  headers: { 'Content-Type': 'application/json' },
})

let alExpirar: (() => void) | null = null

/** La sesión registra qué hacer cuando la API responde 401 (token vencido). */
export function registrarExpiracion(fn: () => void) {
  alExpirar = fn
}

export function leerToken(): string | null {
  try {
    return sessionStorage.getItem(CLAVE_TOKEN)
  } catch {
    return null
  }
}

export function guardarToken(token: string | null) {
  try {
    if (token) sessionStorage.setItem(CLAVE_TOKEN, token)
    else sessionStorage.removeItem(CLAVE_TOKEN)
  } catch {
    // Sin almacenamiento la sesión dura lo que la pestaña.
  }
}

http.interceptors.request.use((config) => {
  const token = leerToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

http.interceptors.response.use(
  (r) => r,
  (error: AxiosError) => {
    if (error.response?.status === 401 && !error.config?.url?.endsWith('/auth/login')) alExpirar?.()
    return Promise.reject(error)
  },
)

/** Errores por campo de un 400 de validación (claves en camelCase). */
export function erroresDe(error: unknown): Record<string, string> {
  const data = (error as AxiosError<Problema>)?.response?.data
  const salida: Record<string, string> = {}
  if (data?.errors) for (const [k, v] of Object.entries(data.errors)) salida[k.charAt(0).toLowerCase() + k.slice(1)] = v[0]
  return salida
}

/** Primer mensaje legible de un error de la API. */
export function mensajeDe(error: unknown, porDefecto = 'No se pudo completar la acción.'): string {
  const data = (error as AxiosError<Problema>)?.response?.data
  const primero = data?.errors ? Object.values(data.errors)[0]?.[0] : undefined
  return primero ?? data?.title ?? porDefecto
}
