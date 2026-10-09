import type { ReactNode } from 'react'
import type { FiltrosLista } from '../api/tipos'
import { Adelante, Atras, Basurero, Chevron, Lupa } from './Iconos'
import type { Opcion } from './Controles'

export interface Columna {
  label: string
  /** Clave de orden que entiende la API; sin clave la columna no se ordena. */
  orden?: string
  ancho: string
}

export interface FiltroDef {
  valor: string
  opciones: Opcion[]
  onChange: (v: string) => void
}

export interface TotalDef {
  label: string
  valor: string
  nota: string
}

export function Totales({ items }: { items: TotalDef[] }) {
  return (
    <div className="totales">
      {items.map((t) => (
        <div key={t.label} className="total">
          <div className="total-label">{t.label}</div>
          <div className="total-valor">{t.valor}</div>
          <div className="total-nota">{t.nota}</div>
        </div>
      ))}
    </div>
  )
}

export function BarraFiltros({
  q, onQ, placeholder, filtros, conteo, onLimpiar,
}: { q: string; onQ: (v: string) => void; placeholder: string; filtros: FiltroDef[]; conteo: number; onLimpiar: () => void }) {
  return (
    <div className="filtros">
      <div className="filtros-grilla">
        <div className="buscador">
          <Lupa />
          <input value={q} placeholder={placeholder} onChange={(e) => onQ(e.target.value)} />
        </div>
        {filtros.map((f, i) => (
          <label key={i} className="filtro">
            <Chevron />
            <select value={f.valor} onChange={(e) => f.onChange(e.target.value)}>
              {f.opciones.map((o) => (
                <option key={o.v} value={o.v}>{o.l}</option>
              ))}
            </select>
          </label>
        ))}
      </div>
      <div className="filtros-pie">
        <span>{conteo === 1 ? '1 registro' : conteo + ' registros'}</span>
        <button className="btn-texto" onClick={onLimpiar}>Limpiar filtros</button>
      </div>
    </div>
  )
}

export interface FilaDef {
  id: number
  celdas: ReactNode[]
}

export function Tabla({
  columnas, filas, filtros, setFiltros, onAbrir, onEliminar, cargando,
}: {
  columnas: Columna[]
  filas: FilaDef[]
  filtros: FiltrosLista
  setFiltros: (cambios: Partial<FiltrosLista>) => void
  onAbrir: (id: number) => void
  onEliminar?: (id: number) => void
  cargando?: boolean
}) {
  const ordenar = (clave?: string) => {
    if (!clave) return
    setFiltros({ orden: clave, asc: filtros.orden === clave ? !filtros.asc : true })
  }
  return (
    <div className={'tabla-caja' + (cargando ? ' cargando' : '')}>
      <div style={{ overflowX: 'auto' }}>
        <table className="tabla">
          <thead>
            <tr>
              {columnas.map((c) => (
                <th key={c.label} style={{ width: c.ancho }} className={c.orden ? 'ordenable' : undefined} onClick={() => ordenar(c.orden)}>
                  <span style={{ display: 'inline-flex', alignItems: 'center' }}>
                    <span>{c.label}</span>
                    {c.orden && filtros.orden === c.orden && <span className="flecha">{filtros.asc ? '▲' : '▼'}</span>}
                  </span>
                </th>
              ))}
              {onEliminar && <th style={{ width: '4%' }} />}
            </tr>
          </thead>
          <tbody>
            {filas.map((f) => (
              <tr key={f.id} onClick={() => onAbrir(f.id)}>
                {f.celdas.map((c, i) => (
                  <td key={i}>{c}</td>
                ))}
                {onEliminar && (
                  <td style={{ textAlign: 'right' }}>
                    <button
                      className="btn-basurero"
                      title="Eliminar prospecto"
                      aria-label="Eliminar prospecto"
                      onClick={(e) => {
                        e.stopPropagation()
                        onEliminar(f.id)
                      }}
                    >
                      <Basurero />
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {!cargando && filas.length === 0 && <div className="sin-resultados">No hay registros que coincidan con los filtros.</div>}
    </div>
  )
}

export function Paginacion({
  pagina, total, porPagina, onPagina,
}: { pagina: number; total: number; porPagina: number; onPagina: (n: number) => void }) {
  const totalPag = Math.max(1, Math.ceil(total / porPagina))
  if (totalPag <= 1) return null
  return (
    <div className="paginacion">
      <span>{(pagina - 1) * porPagina + 1}–{Math.min(pagina * porPagina, total)} de {total}</span>
      <div className="paginacion-botones">
        <button className="pagina" aria-label="Página anterior" onClick={() => onPagina(Math.max(1, pagina - 1))}><Atras tamano={14} /></button>
        {Array.from({ length: totalPag }, (_, i) => i + 1).map((n) => (
          <button key={n} className={'pagina' + (n === pagina ? ' activa' : '')} onClick={() => onPagina(n)}>{n}</button>
        ))}
        <button className="pagina" aria-label="Página siguiente" onClick={() => onPagina(Math.min(totalPag, pagina + 1))}><Adelante /></button>
      </div>
    </div>
  )
}

/** Celda de dos líneas (texto principal y secundario). */
export function Celda({ t, s, clase = 'c-suave', subClase = 'c-sub' }: { t: ReactNode; s?: ReactNode; clase?: string; subClase?: string }) {
  return (
    <span style={{ display: 'block' }}>
      <span className={clase}>{t}</span>
      {s ? <span className={subClase}>{s}</span> : null}
    </span>
  )
}
