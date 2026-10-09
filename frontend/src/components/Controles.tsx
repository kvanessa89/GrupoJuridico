import { useState, type ReactNode } from 'react'
import { Chevron, Ojo } from './Iconos'

export interface Opcion {
  v: string
  l: string
}

interface CampoBase {
  label: string
  requerido?: boolean
  error?: string
  span?: number
}

function Etiqueta({ label, requerido }: { label: string; requerido?: boolean }) {
  return (
    <span className="etiqueta">
      {label}
      {requerido && <span className="requerido"> *</span>}
    </span>
  )
}

export function CampoTexto({
  label, valor, onChange, tipo = 'text', requerido, error, soloLectura, span, placeholder, inputMode,
}: CampoBase & {
  valor: string | number | null | undefined
  onChange?: (v: string) => void
  tipo?: string
  soloLectura?: boolean
  placeholder?: string
  inputMode?: 'numeric' | 'decimal' | 'text'
}) {
  return (
    <label style={{ display: 'block', minWidth: 0, gridColumn: span ? `span ${span}` : undefined }}>
      <Etiqueta label={label} requerido={requerido} />
      <input
        className={'campo' + (error ? ' error' : '')}
        value={valor ?? ''}
        type={tipo}
        readOnly={soloLectura}
        placeholder={placeholder}
        inputMode={inputMode}
        onChange={(e) => onChange?.(e.target.value)}
        onClick={tipo === 'date' ? (e) => (e.currentTarget as HTMLInputElement).showPicker?.() : undefined}
      />
      {error && <span className="msg-error">{error}</span>}
    </label>
  )
}

export function Select({
  valor, onChange, opciones, className = 'campo campo-select', ariaLabel, chevronTamano = 14, chevronDerecha,
}: {
  valor: string
  onChange: (v: string) => void
  opciones: Opcion[]
  className?: string
  ariaLabel?: string
  chevronTamano?: number
  chevronDerecha?: number
}) {
  return (
    <span className="select-envoltura">
      <Chevron tamano={chevronTamano} estilo={chevronDerecha != null ? { right: chevronDerecha } : undefined} />
      <select className={className} value={valor} aria-label={ariaLabel} onChange={(e) => onChange(e.target.value)}>
        {opciones.map((o) => (
          <option key={o.v} value={o.v}>{o.l}</option>
        ))}
      </select>
    </span>
  )
}

export function CampoSelect({
  label, valor, onChange, opciones, requerido, error, span,
}: CampoBase & { valor: string; onChange: (v: string) => void; opciones: Opcion[] }) {
  return (
    <label style={{ display: 'block', minWidth: 0, gridColumn: span ? `span ${span}` : undefined }}>
      <Etiqueta label={label} requerido={requerido} />
      <Select valor={valor} onChange={onChange} opciones={opciones} className={'campo campo-select' + (error ? ' error' : '')} />
      {error && <span className="msg-error">{error}</span>}
    </label>
  )
}

export function CampoNotas({ label, valor, onChange }: { label: string; valor: string; onChange: (v: string) => void }) {
  return (
    <label style={{ display: 'block', marginTop: 15 }}>
      <span className="etiqueta">{label}</span>
      <textarea className="nota" rows={3} value={valor} onChange={(e) => onChange(e.target.value)} />
    </label>
  )
}

/** Contraseña con el ojo para mostrarla u ocultarla. */
export function CampoContrasena({
  valor, onChange, placeholder, onBlur,
}: { valor: string; onChange: (v: string) => void; placeholder?: string; onBlur?: () => void }) {
  const [ver, setVer] = useState(false)
  const titulo = ver ? 'Ocultar contraseña' : 'Ver contraseña'
  return (
    <span style={{ position: 'relative', display: 'block' }}>
      <input
        className="campo"
        style={{ fontSize: 13.5, paddingRight: 42 }}
        type={ver ? 'text' : 'password'}
        value={valor}
        placeholder={placeholder}
        autoComplete="new-password"
        onChange={(e) => onChange(e.target.value)}
        onBlur={onBlur}
      />
      <button type="button" className="btn-ojo" title={titulo} aria-label={titulo} onClick={() => setVer(!ver)}>
        <Ojo tachado={ver} />
      </button>
    </span>
  )
}

export function TarjetaSeccion({ titulo, accion, children }: { titulo: string; accion?: ReactNode; children: ReactNode }) {
  return (
    <div className="tarjeta-seccion">
      {accion ? (
        <div className="cabecera-seccion">
          <span className="titulo-seccion">{titulo}</span>
          {accion}
        </div>
      ) : (
        <div className="titulo-seccion" style={{ marginBottom: 16 }}>{titulo}</div>
      )}
      {children}
    </div>
  )
}

export function Modal({
  titulo, texto, onCancelar, onConfirmar, confirmar = 'Eliminar',
}: { titulo: string; texto: string; onCancelar: () => void; onConfirmar: () => void; confirmar?: string }) {
  return (
    <div className="modal-fondo" onClick={onCancelar}>
      <div className="modal" role="dialog" aria-modal="true" onClick={(e) => e.stopPropagation()}>
        <div className="modal-titulo">{titulo}</div>
        <p>{texto}</p>
        <div className="modal-acciones">
          <button className="btn-secundario" onClick={onCancelar}>Cancelar</button>
          <button className="btn-peligro" onClick={onConfirmar}>{confirmar}</button>
        </div>
      </div>
    </div>
  )
}
