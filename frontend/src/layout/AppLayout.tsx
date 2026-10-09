import { useEffect, useState } from 'react'
import { NavLink, Navigate, Outlet, useLocation } from 'react-router'
import { permisos, useSesion } from '../auth/Sesion'
import { IconoClientes, IconoConfig, IconoMenu, IconoUsuarios, IconoVentas, Maletin } from '../components/Iconos'
import { iniciales } from '../utils/formato'
import { FiltrosProvider, useReiniciarFiltros, type SeccionLista } from './Filtros'

const MARCA = 'Grupo Jurídico'

function Menu() {
  const { usuario } = useSesion()
  const reiniciar = useReiniciarFiltros()
  const [abierto, setAbierto] = useState(() => window.matchMedia?.('(min-width: 761px)').matches ?? true)
  const rol = usuario?.rol
  const items = [
    { a: '/gestion/ventas', label: 'Tabla de ventas', icono: <IconoVentas />, seccion: 'ventas' as SeccionLista, ver: true },
    { a: '/gestion/clientes', label: 'Clientes', icono: <IconoClientes />, seccion: 'clientes' as SeccionLista, ver: permisos.veClientes(rol) },
    { a: '/gestion/configuracion', label: 'Configuración', icono: <IconoConfig />, ver: permisos.esAdmin(rol) },
    { a: '/gestion/usuarios', label: 'Usuarios', icono: <IconoUsuarios />, ver: permisos.esAdmin(rol) },
  ].filter((i) => i.ver)

  return (
    <aside className="menu sin-scroll" style={{ width: abierto ? 212 : 62 }}>
      <div className="menu-lista">
        <button className="menu-titulo" title="Administración" onClick={() => setAbierto(!abierto)}>
          <IconoMenu />
          {abierto && <span>Administración</span>}
        </button>
        {items.map((i) => (
          <NavLink
            key={i.a}
            to={i.a}
            title={i.label}
            className={({ isActive }) => 'menu-item' + (isActive ? ' activo' : '')}
            onClick={() => i.seccion && reiniciar(i.seccion)}
          >
            {i.icono}
            {abierto && <span>{i.label}</span>}
          </NavLink>
        ))}
      </div>
    </aside>
  )
}

/** Encabezado, menú lateral fijo y área de contenido de la parte privada. */
export function AppLayout() {
  const { usuario, cargando, salir } = useSesion()
  const { pathname } = useLocation()

  // Al cambiar de pantalla el scroll vuelve arriba.
  useEffect(() => {
    window.scrollTo(0, 0)
  }, [pathname])

  if (cargando) return <div className="app" />
  if (!usuario) return <Navigate to="/gestion/login" replace />

  const [nombre, ...resto] = usuario.nombre.split(' ')
  return (
    <FiltrosProvider>
      <div className="app">
        <header className="encabezado">
          <div className="encabezado-fila">
            <div className="encabezado-marca">
              <Maletin />
              <span>{MARCA}</span>
            </div>
            <div className="encabezado-usuario">
              <span className="avatar" title={usuario.nombre + ' · ' + usuario.rol}>{iniciales(nombre, resto[0] ?? nombre.slice(1))}</span>
              <button className="btn-salir" onClick={salir}>Salir</button>
            </div>
          </div>
        </header>
        <div className="cuerpo">
          <Menu />
          <main className="principal">
            <Outlet />
          </main>
        </div>
      </div>
    </FiltrosProvider>
  )
}
