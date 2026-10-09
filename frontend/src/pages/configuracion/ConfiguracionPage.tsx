import { useRef, useState } from 'react'
import { mensajeDe } from '../../api/cliente'
import { api } from '../../api/endpoints'
import type { CatalogoItem, TipoCatalogo } from '../../api/tipos'
import { useSesion } from '../../auth/Sesion'
import { BasureroFino, Chevron, Mas } from '../../components/Iconos'

interface DefCatalogo {
  tipo: TipoCatalogo | 'roles'
  clave: 'vendedores' | 'procedencias' | 'metodos' | 'estadosPrima' | 'origenes' | 'estadosCliente' | 'roles'
  titulo: string
  sing: string
  plur: string
  fem?: boolean
  agregar?: string
  /** Solo se puede cambiar el nombre (sin agregar ni eliminar). */
  soloNombre?: boolean
  soloLectura?: boolean
  verCodigo?: boolean
}

const GRUPOS: { titulo: string; interes?: boolean; catalogos: DefCatalogo[] }[] = [
  {
    titulo: 'Ventas',
    interes: true,
    catalogos: [
      { tipo: 'vendedores', clave: 'vendedores', titulo: 'Vendedores', sing: 'vendedor', plur: 'vendedores', agregar: 'Agregar vendedor', verCodigo: true },
      { tipo: 'procedencias', clave: 'procedencias', titulo: 'Procedencia de venta', sing: 'procedencia', plur: 'procedencias', fem: true, agregar: 'Agregar procedencia' },
      { tipo: 'metodos', clave: 'metodos', titulo: 'Método de venta', sing: 'método', plur: 'métodos', agregar: 'Agregar método' },
      { tipo: 'estados-prima', clave: 'estadosPrima', titulo: 'Estado de prima', sing: 'estado', plur: 'estados', soloNombre: true },
    ],
  },
  {
    titulo: 'Clientes',
    catalogos: [
      { tipo: 'origenes', clave: 'origenes', titulo: 'Origen del cliente', sing: 'origen', plur: 'orígenes', agregar: 'Agregar origen', verCodigo: true },
      { tipo: 'estados-cliente', clave: 'estadosCliente', titulo: 'Estado del cliente', sing: 'estado', plur: 'estados', agregar: 'Agregar estado' },
    ],
  },
  {
    titulo: 'Usuarios',
    catalogos: [{ tipo: 'roles', clave: 'roles', titulo: 'Roles', sing: 'rol', plur: 'roles', soloLectura: true }],
  },
]

/** Fila local: `id` 0 mientras no tenga nombre (las filas vacías se descartan). */
interface Fila extends CatalogoItem {
  clave: string
}

function Catalogo({ def, abierto, onToggle }: { def: DefCatalogo; abierto: boolean; onToggle: () => void }) {
  const { catalogos, recargarCatalogos } = useSesion()
  const origen = (catalogos?.[def.clave] ?? []) as CatalogoItem[]
  const [filas, setFilas] = useState<Fila[]>([])
  const [error, setError] = useState('')
  const timers = useRef<Record<string, number>>({})
  const sec = useRef(0)

  // Al abrir se toma la lista vigente; al cerrar se recarga (las filas sin nombre se descartan).
  const alternar = () => {
    if (abierto) recargarCatalogos().catch(() => undefined)
    else setFilas(origen.map((x) => ({ ...x, clave: 'i' + x.id })))
    onToggle()
  }

  const ids = useRef<Record<string, number>>({})
  const creando = useRef<Partial<Record<string, Promise<CatalogoItem>>>>({})

  const guardar = (fila: Fila) => {
    if (def.soloLectura) return
    window.clearTimeout(timers.current[fila.clave])
    timers.current[fila.clave] = window.setTimeout(async () => {
      if (!fila.nombre.trim()) return
      const datos = { nombre: fila.nombre, codigo: def.verCodigo ? fila.codigo : undefined, apellidos: def.tipo === 'vendedores' ? fila.apellidos ?? '' : undefined }
      try {
        // Una fila nueva se crea una sola vez aunque se siga escribiendo mientras se guarda.
        if (creando.current[fila.clave]) await creando.current[fila.clave]
        const id = fila.id || ids.current[fila.clave]
        if (id) await api.actualizarCatalogo(def.tipo as TipoCatalogo, id, datos)
        else {
          const promesa = api.crearCatalogo(def.tipo as TipoCatalogo, datos)
          creando.current[fila.clave] = promesa
          const creado = await promesa.finally(() => delete creando.current[fila.clave])
          ids.current[fila.clave] = creado.id
          setFilas((fs) => fs.map((x) => (x.clave === fila.clave ? { ...x, id: creado.id, codigo: x.codigo || creado.codigo } : x)))
        }
        setError('')
      } catch (e) {
        setError(mensajeDe(e))
      }
    }, 500)
  }

  const editar = (clave: string, cambios: Partial<Fila>) => {
    const nuevas = filas.map((x) => (x.clave === clave ? { ...x, ...cambios } : x))
    setFilas(nuevas)
    const fila = nuevas.find((x) => x.clave === clave)
    if (fila) guardar(fila)
  }

  const eliminar = async (fila: Fila) => {
    try {
      window.clearTimeout(timers.current[fila.clave])
      const id = fila.id || ids.current[fila.clave]
      if (id) await api.eliminarCatalogo(def.tipo as TipoCatalogo, id)
      setFilas((fs) => fs.filter((x) => x.clave !== fila.clave))
      setError('')
    } catch (e) {
      setError(mensajeDe(e))
    }
  }

  const agregar = () => {
    let codigo = ''
    if (def.tipo === 'vendedores') codigo = String(Math.max(29, ...filas.map((x) => Number(x.codigo) || 0)) + 1)
    if (def.tipo === 'origenes') codigo = 'P' + (Math.max(0, ...filas.map((x) => Number(String(x.codigo).replace(/\D/g, '')) || 0)) + 1)
    setFilas((fs) => [...fs, { id: 0, codigo, nombre: '', apellidos: '', clave: 'n' + ++sec.current }])
  }

  const lista = abierto ? filas : origen
  const n = lista.filter((x) => x.nombre.trim()).length
  const resumen = n + ' ' + (n === 1 ? def.sing : def.plur) + ' configurad' + (def.fem ? 'a' : 'o') + (n === 1 ? '' : 's')
  const editable = !def.soloLectura && !def.soloNombre

  return (
    <div className="tarjeta" style={{ overflow: 'hidden' }}>
      <button className="plegable-cabecera" onClick={alternar}>
        <span>{def.titulo}</span>
        <span className="plegable-resumen">
          <span>{resumen}</span>
          <Chevron color="var(--gold)" estilo={{ transition: 'transform 0.15s', transform: abierto ? 'rotate(180deg)' : 'none', flex: 'none' }} />
        </span>
      </button>
      {abierto && (
        <div className="plegable-cuerpo">
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {filas.map((it) => (
              <div key={it.clave} className="item-config">
                {def.verCodigo && <input className="codigo" value={it.codigo} aria-label="Código" onChange={(e) => editar(it.clave, { codigo: e.target.value })} />}
                <input
                  value={it.nombre}
                  placeholder="Nombre"
                  readOnly={def.soloLectura}
                  style={{ flex: 1 }}
                  onChange={(e) => editar(it.clave, { nombre: e.target.value })}
                />
                {def.tipo === 'vendedores' && (
                  <input value={it.apellidos ?? ''} placeholder="Apellidos" style={{ flex: 1.4 }} onChange={(e) => editar(it.clave, { apellidos: e.target.value })} />
                )}
                {editable && (
                  <button className="btn-quitar" title="Eliminar" onClick={() => eliminar(it)}>
                    <BasureroFino />
                  </button>
                )}
              </div>
            ))}
          </div>
          {editable && def.agregar && (
            <button className="btn-agregar" onClick={agregar}>
              <Mas tamano={13} />
              <span>{def.agregar}</span>
            </button>
          )}
          {error && <p className="texto-error">{error}</p>}
        </div>
      )}
    </div>
  )
}

function InteresMora() {
  const { catalogos } = useSesion()
  const [valor, setValor] = useState(() => String(catalogos?.interesMora ?? 2.5))
  const [error, setError] = useState('')
  const timer = useRef<number | undefined>(undefined)
  const cambiar = (v: string) => {
    setValor(v)
    window.clearTimeout(timer.current)
    timer.current = window.setTimeout(() => {
      if (v === '' || Number.isNaN(Number(v))) return
      api.guardarInteresMora(Number(v)).then(() => setError('')).catch((e) => setError(mensajeDe(e)))
    }, 500)
  }
  return (
    <div className="tarjeta" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap', padding: '14px 20px' }}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 3, minWidth: 0 }}>
        <span style={{ fontSize: 14.5, fontWeight: 700, color: 'var(--head)' }}>Interés de morosidad</span>
        <span style={{ fontSize: 12, color: 'var(--ink3)' }}>Porcentaje que se aplica al saldo vencido.</span>
        {error && <span className="msg-error">{error}</span>}
      </div>
      <label style={{ position: 'relative', display: 'block', width: 130 }}>
        <input
          className="campo"
          type="number"
          min="0"
          step="0.1"
          value={valor}
          aria-label="Interés de morosidad"
          onChange={(e) => cambiar(e.target.value)}
          style={{ paddingRight: 34, fontSize: 14, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}
        />
        <span style={{ position: 'absolute', right: 13, top: '50%', transform: 'translateY(-50%)', fontSize: 13.5, fontWeight: 600, color: 'var(--ink3)', pointerEvents: 'none' }}>%</span>
      </label>
    </div>
  )
}

export function ConfiguracionPage() {
  // Todas las tarjetas entran colapsadas.
  const [abiertos, setAbiertos] = useState<Record<string, boolean>>({})
  return (
    <div className="contenedor-lista">
      <div className="cabecera-lista">
        <div style={{ minWidth: 0 }}>
          <h1>Configuración</h1>
          <p>Catálogos que alimentan los formularios de personas, ventas y primas.</p>
        </div>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 26 }}>
        {GRUPOS.map((g) => (
          <div key={g.titulo} className="grupo-config">
            <div className="grupo-titulo">{g.titulo}</div>
            {g.catalogos.map((c) => (
              <Catalogo key={c.clave} def={c} abierto={!!abiertos[c.clave]} onToggle={() => setAbiertos((a) => ({ ...a, [c.clave]: !a[c.clave] }))} />
            ))}
            {g.interes && <InteresMora />}
          </div>
        ))}
      </div>
    </div>
  )
}
