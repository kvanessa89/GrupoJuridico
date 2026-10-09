import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router'
import { useSesion } from '../auth/Sesion'
import { Maletin } from '../components/Iconos'

export function Login() {
  const { usuario, cargando, entrar } = useSesion()
  const navegar = useNavigate()
  const [u, setU] = useState('')
  const [c, setC] = useState('')
  const [error, setError] = useState(false)
  const [enviando, setEnviando] = useState(false)

  if (!cargando && usuario) return <Navigate to="/gestion/ventas" replace />

  const enviar = async (e: FormEvent) => {
    e.preventDefault()
    setEnviando(true)
    try {
      await entrar(u.trim(), c)
      // Todos los roles entran a la tabla de ventas.
      navegar('/gestion/ventas', { replace: true })
    } catch {
      setError(true)
      setC('')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="app">
      <div className="login">
        <div className="login-caja">
          <div className="login-marca">
            <Maletin tamano={30} />
            <span>Grupo Jurídico</span>
          </div>
          <form className="login-tarjeta" onSubmit={enviar}>
            <h1>Iniciar sesión</h1>
            <p>Acceso al sistema de cartera</p>
            <label style={{ display: 'block', marginBottom: 14 }}>
              <span className="etiqueta">Usuario</span>
              <input className="campo" autoComplete="username" value={u} onChange={(e) => { setU(e.target.value); setError(false) }} />
            </label>
            <label style={{ display: 'block' }}>
              <span className="etiqueta">Contraseña</span>
              <input className="campo" type="password" autoComplete="current-password" value={c} onChange={(e) => { setC(e.target.value); setError(false) }} />
            </label>
            {error && <div className="login-error">Usuario o contraseña incorrectos.</div>}
            <button type="submit" className="btn-primario" disabled={enviando} style={{ width: '100%', marginTop: 20, padding: 12, fontSize: 14 }}>
              Entrar
            </button>
          </form>
        </div>
      </div>
    </div>
  )
}
