import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { guardarToken, leerToken, registrarExpiracion } from '../api/cliente'
import { api } from '../api/endpoints'
import type { Catalogos, Rol, Usuario } from '../api/tipos'

interface SesionCtx {
  usuario: Usuario | null
  cargando: boolean
  catalogos: Catalogos | null
  recargarCatalogos: () => Promise<void>
  entrar: (usuario: string, contrasena: string) => Promise<Usuario>
  salir: () => void
}

const Ctx = createContext<SesionCtx | null>(null)

export function SesionProvider({ children }: { children: ReactNode }) {
  const [usuario, setUsuario] = useState<Usuario | null>(null)
  const [catalogos, setCatalogos] = useState<Catalogos | null>(null)
  const [cargando, setCargando] = useState(() => !!leerToken())

  const salir = useCallback(() => {
    guardarToken(null)
    setUsuario(null)
    setCatalogos(null)
  }, [])

  const recargarCatalogos = useCallback(async () => {
    setCatalogos(await api.catalogos())
  }, [])

  useEffect(() => {
    registrarExpiracion(salir)
    if (!leerToken()) return
    let vivo = true
    Promise.all([api.yo(), api.catalogos()])
      .then(([u, c]) => {
        if (!vivo) return
        setUsuario(u)
        setCatalogos(c)
      })
      .catch(() => vivo && salir())
      .finally(() => vivo && setCargando(false))
    return () => {
      vivo = false
    }
  }, [salir])

  const entrar = useCallback(async (u: string, c: string) => {
    const r = await api.login(u, c)
    guardarToken(r.token)
    const cats = await api.catalogos()
    setCatalogos(cats)
    setUsuario(r.usuario)
    return r.usuario
  }, [])

  const valor = useMemo(
    () => ({ usuario, cargando, catalogos, recargarCatalogos, entrar, salir }),
    [usuario, cargando, catalogos, recargarCatalogos, entrar, salir],
  )
  return <Ctx.Provider value={valor}>{children}</Ctx.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useSesion() {
  const c = useContext(Ctx)
  if (!c) throw new Error('useSesion fuera de SesionProvider')
  return c
}

/** Reglas fijas por rol (qué secciones y columnas ve cada uno). */
// eslint-disable-next-line react-refresh/only-export-components
export const permisos = {
  esAdmin: (r?: Rol) => r === 'Administrador',
  veClientes: (r?: Rol) => r === 'Administrador' || r === 'Cobros',
  /** Asistente de Ventas y Cobros no ven primas pagadas. */
  ocultaPagadas: (r?: Rol) => r === 'Asistente de Ventas' || r === 'Cobros',
}
