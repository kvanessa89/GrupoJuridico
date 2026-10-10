let siguienteIdLocal = -1
import { useState } from 'react'
import type { ComentarioDto, CorreoDto, FamiliarDto, NumeroDto, TipoNumero } from '../../api/tipos'
import { Select, TarjetaSeccion } from '../../components/Controles'
import { BasureroLleno, Estrella } from '../../components/Iconos'
import { colorAutor } from '../../utils/colores'
import { fFecha, iniciales } from '../../utils/formato'

function BotonEliminar({ onClick, titulo = 'Eliminar' }: { onClick: () => void; titulo?: string }) {
  return (
    <button className="btn-icono" title={titulo} aria-label={titulo} onClick={onClick}>
      <BasureroLleno />
    </button>
  )
}

/** Teléfonos y correos del cliente oficial: la estrella marca el principal (uno por tipo). */
export function Contactos({
  numeros, correos, onNumeros, onCorreos,
}: { numeros: NumeroDto[]; correos: CorreoDto[]; onNumeros: (n: NumeroDto[]) => void; onCorreos: (c: CorreoDto[]) => void }) {
  const cambiarTipo = (i: number, tipo: TipoNumero) => {
    const hay = numeros.some((x, j) => j !== i && x.tipo === tipo && x.principal)
    onNumeros(numeros.map((x, j) => (j === i ? { ...x, tipo, principal: !hay } : x)))
  }
  const marcarNumero = (i: number) => {
    const tipo = numeros[i].tipo
    onNumeros(numeros.map((x, j) => (x.tipo === tipo ? { ...x, principal: j === i } : x)))
  }
  const quitarNumero = (i: number) => {
    const n = numeros[i]
    let resto = numeros.filter((_, j) => j !== i)
    if (n.principal) {
      const sig = resto.findIndex((x) => x.tipo === n.tipo)
      if (sig >= 0) resto = resto.map((x, j) => (j === sig ? { ...x, principal: true } : x))
    }
    onNumeros(resto)
  }
  const quitarCorreo = (i: number) => {
    const c = correos[i]
    let resto = correos.filter((_, j) => j !== i)
    if (c.principal && resto.length) resto = resto.map((x, j) => (j === 0 ? { ...x, principal: true } : x))
    onCorreos(resto)
  }

  return (
    <div className="contactos">
      <TarjetaSeccion
        titulo="Teléfonos"
        accion={
          <button
            className="btn-enlace"
            onClick={() => onNumeros([...numeros, { id: siguienteIdLocal--, numero: '', tipo: 'Teléfono', principal: !numeros.some((x) => x.tipo === 'Teléfono' && x.principal) }])}
          >
            + Agregar
          </button>
        }
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {numeros.map((n, i) => {
            const titulo = n.principal ? n.tipo + ' principal' : 'Marcar como ' + n.tipo.toLowerCase() + ' principal'
            return (
              <div key={i} className="fila-contacto">
                <input className="campo" value={n.numero} placeholder="Número" onChange={(e) => onNumeros(numeros.map((x, j) => (j === i ? { ...x, numero: e.target.value } : x)))} />
                <span style={{ flex: 'none' }}>
                  <Select
                    valor={n.tipo}
                    onChange={(v) => cambiarTipo(i, v as TipoNumero)}
                    opciones={[{ v: 'Teléfono', l: 'Teléfono' }, { v: 'WhatsApp', l: 'WhatsApp' }]}
                    className="tipo-numero"
                    ariaLabel="Tipo de número"
                    chevronTamano={12}
                    chevronDerecha={10}
                  />
                </span>
                <button className={'estrella' + (n.principal ? ' on' : '')} title={titulo} aria-label={titulo} onClick={() => marcarNumero(i)}>
                  <Estrella llena={n.principal} />
                </button>
                <BotonEliminar onClick={() => quitarNumero(i)} />
              </div>
            )
          })}
          {numeros.length === 0 && <span className="vacio">Sin números registrados.</span>}
        </div>
        <p className="ayuda">La estrella marca el principal de cada tipo: un teléfono y un WhatsApp.</p>
      </TarjetaSeccion>

      <TarjetaSeccion
        titulo="Correos electrónicos"
        accion={
          <button className="btn-enlace" onClick={() => onCorreos([...correos, { id: siguienteIdLocal--, correo: '', principal: !correos.some((x) => x.principal) }])}>
            + Agregar
          </button>
        }
      >
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {correos.map((c, i) => {
            const titulo = c.principal ? 'Correo principal' : 'Marcar como correo principal'
            return (
              <div key={i} className="fila-contacto">
                <input className="campo" value={c.correo} placeholder="correo@dominio.com" onChange={(e) => onCorreos(correos.map((x, j) => (j === i ? { ...x, correo: e.target.value } : x)))} />
                <button className={'estrella' + (c.principal ? ' on' : '')} title={titulo} aria-label={titulo} onClick={() => onCorreos(correos.map((x, j) => ({ ...x, principal: j === i })))}>
                  <Estrella llena={c.principal} />
                </button>
                <BotonEliminar onClick={() => quitarCorreo(i)} />
              </div>
            )
          })}
          {correos.length === 0 && <span className="vacio">Sin correos registrados.</span>}
        </div>
      </TarjetaSeccion>
    </div>
  )
}

const CAMPOS_FAMILIAR: [string, keyof FamiliarDto][] = [
  ['Nombre completo', 'nombreCompleto'], ['Parentesco', 'parentesco'], ['Teléfono', 'telefono'], ['Correo', 'correoElectronico'],
]

export function Familiares({ familiares, onCambiar }: { familiares: FamiliarDto[]; onCambiar: (f: FamiliarDto[]) => void }) {
  return (
    <TarjetaSeccion
      titulo="Familiares"
      accion={
        <button
          className="btn-enlace"
          onClick={() => onCambiar([...familiares, { id: siguienteIdLocal--, nombreCompleto: '', parentesco: '', telefono: '', correoElectronico: '', whatsapp: '' }])}
        >
          + Agregar familiar
        </button>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {familiares.map((f, i) => (
          <div key={i} className="familiar">
            {CAMPOS_FAMILIAR.map(([label, campo]) => (
              <input
                key={campo}
                value={String(f[campo] ?? '')}
                placeholder={label}
                aria-label={label}
                onChange={(e) => onCambiar(familiares.map((x, j) => (j === i ? { ...x, [campo]: e.target.value } : x)))}
              />
            ))}
            <BotonEliminar titulo="Quitar familiar" onClick={() => onCambiar(familiares.filter((_, j) => j !== i))} />
          </div>
        ))}
        {familiares.length === 0 && <span className="vacio">Sin familiares registrados.</span>}
      </div>
    </TarjetaSeccion>
  )
}

export function Comentarios({
  comentarios, onComentar, onEliminar,
}: { comentarios: ComentarioDto[]; onComentar: (texto: string) => Promise<void>; onEliminar: (id: number) => void }) {
  const [texto, setTexto] = useState('')
  const [enviando, setEnviando] = useState(false)
  const comentar = async () => {
    if (!texto.trim() || enviando) return
    setEnviando(true)
    try {
      await onComentar(texto.trim())
      setTexto('')
    } finally {
      setEnviando(false)
    }
  }
  return (
    <TarjetaSeccion
      titulo="Comentarios"
      accion={<span style={{ fontSize: 11.5, color: 'var(--ink3)' }}>{comentarios.length === 1 ? '1 comentario' : comentarios.length + ' comentarios'}</span>}
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        <textarea className="nota" rows={3} placeholder="Escribí un comentario…" value={texto} onChange={(e) => setTexto(e.target.value)} />
        <button className="btn-primario" style={{ alignSelf: 'flex-end', padding: '10px 18px' }} disabled={enviando} onClick={comentar}>Comentar</button>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginTop: 16 }}>
        {comentarios.map((c) => {
          const [a, b] = c.autor.split(' ')
          const f = c.fecha ? new Date(c.fecha) : null
          const hora = f ? String(f.getHours()).padStart(2, '0') + ':' + String(f.getMinutes()).padStart(2, '0') : ''
          const fechaLocal = f ? f.getFullYear() + '-' + String(f.getMonth() + 1).padStart(2, '0') + '-' + String(f.getDate()).padStart(2, '0') : ''
          return (
            <div key={c.id} className="comentario">
              <span className="comentario-avatar" style={colorAutor(c.usuarioId)}>{iniciales(a, b || a)}</span>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, flexWrap: 'wrap' }}>
                  <span style={{ fontSize: 13, fontWeight: 700 }}>{c.autor}</span>
                  <span style={{ fontSize: 11.5, color: 'var(--ink3)' }}>{(c.rol ? c.rol + ' · ' : '') + fFecha(fechaLocal) + (hora ? ' · ' + hora : '')}</span>
                </div>
                <p>{c.texto}</p>
              </div>
              {c.puedeEliminar && (
                <button className="btn-icono" title="Eliminar comentario" aria-label="Eliminar comentario" onClick={() => onEliminar(c.id)}>
                  <BasureroLleno tamano={13} />
                </button>
              )}
            </div>
          )
        })}
        {comentarios.length === 0 && <span className="vacio">Sin comentarios todavía.</span>}
      </div>
    </TarjetaSeccion>
  )
}
