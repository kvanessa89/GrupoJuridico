import { useEffect, useRef, useState } from 'react'
import { mensajeDe } from '../../api/cliente'
import { api } from '../../api/endpoints'
import type { Usuario } from '../../api/tipos'
import { useSesion } from '../../auth/Sesion'
import { CampoContrasena, Select } from '../../components/Controles'
import { Basurero } from '../../components/Iconos'

/** Las contraseñas se guardan cifradas: en la lista el campo queda vacío y escribir una nueva la reemplaza. */
const PUNTOS = '••••••••'

interface FilaUsuario extends Usuario {
  contrasena: string
}

export function UsuariosPage() {
  const { usuario: yo, catalogos } = useSesion()
  const [usuarios, setUsuarios] = useState<FilaUsuario[]>([])
  const [nuevo, setNuevo] = useState({ nombre: '', usuario: '', contrasena: '', rol: 'Administrador' })
  const [errorNuevo, setErrorNuevo] = useState('')
  const [errorLista, setErrorLista] = useState('')
  const timers = useRef<Record<number, number>>({})

  useEffect(() => {
    api.usuarios().then((us) => setUsuarios(us.map((u) => ({ ...u, contrasena: '' })))).catch((e) => setErrorLista(mensajeDe(e)))
  }, [])

  const roles = (catalogos?.roles ?? []).map((r) => ({ v: r.nombre, l: r.nombre }))

  const guardar = (u: FilaUsuario, inmediato = false) => {
    window.clearTimeout(timers.current[u.id])
    const enviar = async () => {
      if (!u.nombre.trim() || !u.usuario.trim()) return
      try {
        await api.actualizarUsuario(u.id, { nombre: u.nombre, usuario: u.usuario, contrasena: u.contrasena || null, rol: u.rol })
        // Una vez guardada, la contraseña nueva no se vuelve a enviar.
        if (u.contrasena) setUsuarios((us) => us.map((x) => (x.id === u.id ? { ...x, contrasena: '' } : x)))
        setErrorLista('')
      } catch (e) {
        setErrorLista(mensajeDe(e))
      }
    }
    if (inmediato) void enviar()
    else timers.current[u.id] = window.setTimeout(enviar, 600)
  }

  const editar = (id: number, cambios: Partial<FilaUsuario>, inmediato = false) => {
    const nuevos = usuarios.map((x) => (x.id === id ? { ...x, ...cambios } : x))
    setUsuarios(nuevos)
    const u = nuevos.find((x) => x.id === id)
    // La contraseña se envía al salir del campo, no con cada tecla.
    if (u && !('contrasena' in cambios)) guardar(u, inmediato)
  }

  const agregar = async () => {
    if (!nuevo.nombre.trim() || !nuevo.usuario.trim() || !nuevo.contrasena) {
      setErrorNuevo('Nombre, usuario y contraseña son requeridos.')
      return
    }
    if (usuarios.some((x) => x.usuario.toLowerCase() === nuevo.usuario.trim().toLowerCase())) {
      setErrorNuevo('Ese usuario ya existe.')
      return
    }
    try {
      const u = await api.crearUsuario(nuevo)
      setUsuarios((us) => [...us, { ...u, contrasena: '' }])
      setNuevo({ nombre: '', usuario: '', contrasena: '', rol: 'Administrador' })
      setErrorNuevo('')
    } catch (e) {
      setErrorNuevo(mensajeDe(e))
    }
  }

  const eliminar = async (id: number) => {
    try {
      await api.eliminarUsuario(id)
      setUsuarios((us) => us.filter((x) => x.id !== id))
    } catch (e) {
      setErrorLista(mensajeDe(e))
    }
  }

  return (
    <div className="contenedor-lista">
      <div className="cabecera-lista">
        <div style={{ minWidth: 0 }}>
          <h1>Usuarios del sistema</h1>
          <p>{usuarios.length + (usuarios.length === 1 ? ' usuario' : ' usuarios')} con acceso al sistema</p>
        </div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        <div className="tarjeta" style={{ padding: '22px 26px' }}>
          <div className="grupo-titulo" style={{ marginBottom: 14 }}>Nuevo usuario</div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))', gap: 12, alignItems: 'end' }}>
            <label style={{ display: 'block', minWidth: 0 }}>
              <span className="etiqueta">Nombre</span>
              <input className="campo" style={{ fontSize: 13.5 }} value={nuevo.nombre} onChange={(e) => { setNuevo({ ...nuevo, nombre: e.target.value }); setErrorNuevo('') }} />
            </label>
            <label style={{ display: 'block', minWidth: 0 }}>
              <span className="etiqueta">Usuario</span>
              <input className="campo" style={{ fontSize: 13.5 }} autoComplete="off" value={nuevo.usuario} onChange={(e) => { setNuevo({ ...nuevo, usuario: e.target.value }); setErrorNuevo('') }} />
            </label>
            <div style={{ minWidth: 0 }}>
              <span className="etiqueta">Contraseña</span>
              <CampoContrasena valor={nuevo.contrasena} onChange={(v) => { setNuevo({ ...nuevo, contrasena: v }); setErrorNuevo('') }} />
            </div>
            <label style={{ display: 'block', minWidth: 0 }}>
              <span className="etiqueta">Rol</span>
              <Select valor={nuevo.rol} onChange={(v) => setNuevo({ ...nuevo, rol: v })} opciones={roles} />
            </label>
            <button className="btn-primario" style={{ padding: '12px 18px' }} onClick={agregar}>Agregar</button>
          </div>
          {errorNuevo && <p className="texto-error">{errorNuevo}</p>}
        </div>

        <div className="tarjeta sin-scroll" style={{ padding: '22px 26px', overflowX: 'auto' }}>
          <div style={{ minWidth: 680, display: 'flex', flexDirection: 'column', gap: 10 }}>
            <div className="grilla-usuarios">
              <span className="etiqueta" style={{ margin: 0 }}>Nombre</span>
              <span className="etiqueta" style={{ margin: 0 }}>Usuario</span>
              <span className="etiqueta" style={{ margin: 0 }}>Contraseña</span>
              <span className="etiqueta" style={{ margin: 0 }}>Rol</span>
              <span />
            </div>
            {usuarios.map((u) => {
              const esYo = yo?.id === u.id
              return (
                <div key={u.id} className="grilla-usuarios">
                  <input className="campo" style={{ fontSize: 13.5 }} value={u.nombre} onChange={(e) => editar(u.id, { nombre: e.target.value })} />
                  <input className="campo" style={{ fontSize: 13.5 }} value={u.usuario} autoComplete="off" onChange={(e) => editar(u.id, { usuario: e.target.value })} />
                  <CampoContrasena
                    valor={u.contrasena}
                    placeholder={PUNTOS}
                    onChange={(v) => editar(u.id, { contrasena: v })}
                    onBlur={() => u.contrasena && guardar(u, true)}
                  />
                  <Select valor={u.rol} onChange={(v) => editar(u.id, { rol: v as Usuario['rol'] }, true)} opciones={roles} />
                  {esYo ? (
                    <span title="Tu usuario" style={{ fontSize: 10.5, fontWeight: 700, color: 'var(--ink3)', textAlign: 'center' }}>Tú</span>
                  ) : (
                    <button className="btn-basurero" style={{ width: 32, height: 32 }} title="Eliminar usuario" aria-label="Eliminar usuario" onClick={() => eliminar(u.id)}>
                      <Basurero />
                    </button>
                  )}
                </div>
              )
            })}
          </div>
          {errorLista && <p className="texto-error">{errorLista}</p>}
        </div>
      </div>
    </div>
  )
}
