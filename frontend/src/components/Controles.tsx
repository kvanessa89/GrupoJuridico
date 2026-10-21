import { useLayoutEffect, useRef, useState, type CSSProperties, type ReactNode } from 'react'
import { errorTelefono } from '../utils/telefonos'
import { useSesion } from '../auth/Sesion'
import { enlaceTel, formatearMonto, limpiarMonto, paisPorDefecto, soloDigitos } from '../utils/formato'
import { Chevron, Llamar, Ojo } from './Iconos'

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
  label, valor, onChange, tipo = 'text', requerido, error, soloLectura, span, placeholder, inputMode, maxLength,
}: CampoBase & {
  valor: string | number | null | undefined
  onChange?: (v: string) => void
  tipo?: string
  soloLectura?: boolean
  placeholder?: string
  inputMode?: 'numeric' | 'decimal' | 'text'
  maxLength?: number
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
        maxLength={maxLength}
        onChange={(e) => onChange?.(e.target.value)}
        onClick={tipo === 'date' ? (e) => (e.currentTarget as HTMLInputElement).showPicker?.() : undefined}
      />
      {error && <span className="msg-error">{error}</span>}
    </label>
  )
}

/**
 * Monto con miles separados por coma y decimales con punto ("1,250,000.50").
 * Solo admite dígitos y un punto; `valor` y `onChange` usan el número crudo ("1250000.50").
 */
export function CampoMonto({
  label, valor, onChange, requerido, error, span,
}: CampoBase & { valor: string; onChange: (v: string) => void }) {
  const ref = useRef<HTMLInputElement>(null)
  // Dígitos y punto que quedan a la izquierda del cursor; al reformatear se vuelve a esa posición.
  const antesDelCursor = useRef<number | null>(null)
  useLayoutEffect(() => {
    const el = ref.current
    if (!el || antesDelCursor.current == null) return
    let quedan = antesDelCursor.current
    let pos = 0
    while (pos < el.value.length && quedan > 0) {
      if (el.value[pos] !== ',') quedan--
      pos++
    }
    el.setSelectionRange(pos, pos)
    antesDelCursor.current = null
  })
  return (
    <label style={{ display: 'block', minWidth: 0, gridColumn: span ? `span ${span}` : undefined }}>
      <Etiqueta label={label} requerido={requerido} />
      <input
        ref={ref}
        className={'campo' + (error ? ' error' : '')}
        value={formatearMonto(valor)}
        inputMode="decimal"
        onChange={(e) => {
          const crudo = limpiarMonto(e.target.value)
          const izquierda = e.target.value.slice(0, e.target.selectionStart ?? e.target.value.length)
          antesDelCursor.current = Math.min(izquierda.replace(/[^\d.]/g, '').length, crudo.length)
          onChange(crudo)
        }}
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

/** Número de teléfono con su país: prefijo del catálogo de países + número. */
export function Telefono({
  paisId, numero, onPais, onNumero, error, placeholder = 'Número', ariaLabel, mostrarError = true, requerido, onBlur, llamar = true,
}: {
  paisId: number | null
  numero: string
  onPais: (id: number) => void
  onNumero: (v: string) => void
  error?: boolean
  mostrarError?: boolean
  requerido?: boolean
  onBlur?: () => void
  placeholder?: string
  ariaLabel?: string
  /** Muestra el botón de llamar dentro del campo; la lista de contactos del cliente lo pone aparte. */
  llamar?: boolean
}) {
  const { catalogos } = useSesion()
  const paises = catalogos?.paises ?? []
  const actual = paisId ?? paisPorDefecto(paises)
  const prefijo = paises.find((p) => p.id === actual)?.codigo ?? ''
  const mensaje = errorTelefono(numero, actual, paises)
  return (
    <span style={{ display: 'block', minWidth: 0, flex: 1 }}>
      <span className={'telefono' + (error || mensaje ? ' error' : '')} style={{ position: 'relative' }}>
        <span title={paises.find((p) => p.id === actual)?.nombre}>
          <Select
            valor={actual == null ? '' : String(actual)}
            onChange={(v) => onPais(Number(v))}
            opciones={paises.map((p) => ({ v: String(p.id), l: p.codigo }))}
            className="telefono-pais"
            ariaLabel="País"
            chevronTamano={10}
            chevronDerecha={6}
          />
        </span>
        <input
          className="campo"
          value={numero}
          placeholder={placeholder}
          aria-label={ariaLabel}
          inputMode="numeric"
          aria-invalid={error || !!mensaje || undefined}
          aria-required={requerido || undefined}
          style={requerido ? { paddingRight: 28 } : undefined}
          onBlur={onBlur}
          onChange={(e) => onNumero(soloDigitos(e.target.value))}
        />
        {requerido && <span className="requerido" aria-hidden="true" style={{ position: 'absolute', right: numero && llamar ? 60 : 12, top: 14 }}>*</span>}
        {numero && llamar && (
          <a className="btn-llamar" href={enlaceTel(prefijo + numero)} title={`Llamar al ${prefijo} ${numero}`} aria-label={`Llamar al ${prefijo} ${numero}`}>
            <Llamar />
          </a>
        )}
      </span>
      {mostrarError && mensaje && <span className="msg-error">{mensaje}</span>}
    </span>
  )
}

export function CampoTelefono({
  label, paisId, numero, onPais, onNumero, requerido, error, span,
}: CampoBase & { paisId: number | null; numero: string; onPais: (id: number) => void; onNumero: (v: string) => void }) {
  return (
    <label style={{ display: 'block', minWidth: 0, gridColumn: span ? `span ${span}` : undefined }}>
      <Etiqueta label={label} requerido={requerido} />
      <Telefono paisId={paisId} numero={numero} onPais={onPais} onNumero={onNumero} error={!!error} mostrarError={!error} ariaLabel={label} />
      {error && <span className="msg-error">{error}</span>}
    </label>
  )
}

export function CampoNotas({ label, valor, onChange, maxLength, error }: { label: string; valor: string; onChange: (v: string) => void; maxLength?: number; error?: string }) {
  return (
    <label style={{ display: 'block', marginTop: 15 }}>
      <span className="etiqueta">{label}</span>
      <textarea className={'nota' + (error ? ' campo error' : '')} rows={3} value={valor} maxLength={maxLength} aria-invalid={!!error || undefined} onChange={(e) => onChange(e.target.value)} />
      {maxLength != null && <span className="ayuda">{valor.length}/{maxLength} caracteres.</span>}
      {error && <span className="msg-error">{error}</span>}
    </label>
  )
}

/**
 * Contraseña con el ojo para mostrarla u ocultarla. Por defecto es para crear o cambiar una
 * contraseña (new-password); el login usa current-password para que el navegador la autocomplete.
 */
export function CampoContrasena({
  valor, onChange, placeholder, onBlur, autoComplete = 'new-password', fontSize = 13.5,
}: {
  valor: string
  onChange: (v: string) => void
  placeholder?: string
  onBlur?: () => void
  autoComplete?: 'new-password' | 'current-password'
  fontSize?: CSSProperties['fontSize']
}) {
  const [ver, setVer] = useState(false)
  const titulo = ver ? 'Ocultar contraseña' : 'Ver contraseña'
  return (
    <span style={{ position: 'relative', display: 'block' }}>
      <input
        className="campo"
        style={{ fontSize, paddingRight: 42 }}
        type={ver ? 'text' : 'password'}
        value={valor}
        placeholder={placeholder}
        autoComplete={autoComplete}
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



