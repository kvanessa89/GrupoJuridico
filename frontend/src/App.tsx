import { Navigate, Outlet, Route, Routes, useParams } from 'react-router'
import type { Rol } from './api/tipos'
import { useSesion } from './auth/Sesion'
import { AppLayout } from './layout/AppLayout'
import { ClientesPage } from './pages/clientes/ClientesPage'
import { CobrosPage } from './pages/cobros/CobrosPage'
import { PlanPagoPage } from './pages/cobros/PlanPagoPage'
import { ConfiguracionPage } from './pages/configuracion/ConfiguracionPage'
import { Inicio } from './pages/Inicio'
import { Login } from './pages/Login'
import { PersonaPage } from './pages/persona/PersonaPage'
import { UsuariosPage } from './pages/usuarios/UsuariosPage'
import { VentasPage } from './pages/ventas/VentasPage'

/** Solo deja pasar a los roles indicados; el resto vuelve a la tabla de ventas. */
function SoloRoles({ roles }: { roles: Rol[] }) {
  const { usuario } = useSesion()
  return usuario && roles.includes(usuario.rol) ? <Outlet /> : <Navigate to="/gestion/ventas" replace />
}

/** Cada persona monta su propia ficha (así el estado no se arrastra entre fichas). */
function Ficha({ seccion }: { seccion: 'ventas' | 'clientes' }) {
  const { id } = useParams()
  return <PersonaPage key={seccion + id} seccion={seccion} />
}

/** El plan de pago también se monta de nuevo por persona. */
function Plan({ desde }: { desde: 'cobros' | 'ficha' }) {
  const { id } = useParams()
  return <PlanPagoPage key={desde + id} desde={desde} />
}

export function App() {
  return (
    <Routes>
      <Route path="/" element={<Inicio />} />
      <Route path="/gestion/login" element={<Login />} />
      <Route path="/gestion" element={<AppLayout />}>
        <Route index element={<Navigate to="ventas" replace />} />
        <Route path="ventas" element={<VentasPage />} />
        <Route path="ventas/nuevo" element={<PersonaPage key="nuevo" seccion="ventas" nuevo />} />
        <Route path="ventas/:id" element={<Ficha seccion="ventas" />} />
        <Route element={<SoloRoles roles={['Administrador', 'Cobros']} />}>
          <Route path="clientes" element={<ClientesPage />} />
          <Route path="clientes/:id" element={<Ficha seccion="clientes" />} />
          <Route path="clientes/:id/plan" element={<Plan desde="ficha" />} />
          <Route path="cobros" element={<CobrosPage />} />
          <Route path="cobros/:id" element={<Plan desde="cobros" />} />
        </Route>
        <Route element={<SoloRoles roles={['Administrador']} />}>
          <Route path="configuracion" element={<ConfiguracionPage />} />
          <Route path="usuarios" element={<UsuariosPage />} />
        </Route>
        <Route path="*" element={<Navigate to="ventas" replace />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
