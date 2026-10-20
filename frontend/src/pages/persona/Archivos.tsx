import { useState } from 'react'
import { mensajeDe } from '../../api/cliente'
import { api } from '../../api/endpoints'
import type { ArchivoDto, ArchivosPersona } from '../../api/tipos'
import { useSesion } from '../../auth/Sesion'
import { Modal, Select } from '../../components/Controles'
import { BasureroLleno, Descargar, Documento } from '../../components/Iconos'
import { guardarBlob } from '../../utils/descargas'
import { fFecha, plural } from '../../utils/formato'
import { ACEPTA_ARCHIVOS, agruparArchivos, fTamano } from './archivos'

/**
 * Pestaña Archivos de la ficha del cliente: se elige el tipo de documento y se suben uno o varios archivos.
 * Cada rol ve y sube solo los tipos permitidos en Configuración → Archivos.
 */
export function Archivos({
  personaId, datos, onCambio,
}: { personaId: number; datos: ArchivosPersona | null; onCambio: (d: ArchivosPersona) => void }) {
  const { catalogos } = useSesion()
  const tipos = catalogos?.tiposDocumento ?? []
  const [tipo, setTipo] = useState('')
  const [faltaTipo, setFaltaTipo] = useState(false)
  const [subiendo, setSubiendo] = useState(false)
  const [error, setError] = useState('')
  const [quitar, setQuitar] = useState<ArchivoDto | null>(null)

  const subibles = tipos.filter((t) => datos?.tiposSubir.includes(t.id))
  const grupos = agruparArchivos(datos?.archivos ?? [], tipos)

  const subir = async (archivos: File[]) => {
    if (!archivos.length) return
    setSubiendo(true)
    setError('')
    try {
      onCambio(await api.subirArchivos(personaId, Number(tipo), archivos))
      setTipo('')
    } catch (e) {
      setError(mensajeDe(e, 'No se pudieron subir los archivos.'))
    } finally {
      setSubiendo(false)
    }
  }

  const descargar = async (a: ArchivoDto) => {
    setError('')
    try {
      guardarBlob(await api.archivo(a.id), a.nombre)
    } catch (e) {
      setError(mensajeDe(e, 'No se pudo descargar el archivo.'))
    }
  }

  const confirmarQuitar = async () => {
    if (!quitar) return
    const a = quitar
    setQuitar(null)
    setError('')
    try {
      onCambio(await api.eliminarArchivo(a.id))
    } catch (e) {
      setError(mensajeDe(e, 'No se pudo quitar el archivo.'))
    }
  }

  return (
    <div className="tarjeta-seccion">
      {quitar && (
        <Modal
          titulo="Quitar archivo"
          texto={`Se eliminará "${quitar.nombre}" del expediente. No se puede deshacer.`}
          confirmar="Quitar"
          onCancelar={() => setQuitar(null)}
          onConfirmar={confirmarQuitar}
        />
      )}
      <div className="cabecera-seccion" style={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <span className="titulo-seccion">Archivos</span>
        {subibles.length > 0 && (
          <div className="archivos-subir">
            <span className={faltaTipo ? 'archivos-tipo error' : 'archivos-tipo'}>
              <Select
                valor={tipo}
                onChange={(v) => { setTipo(v); setFaltaTipo(false) }}
                opciones={[{ v: '', l: 'Tipo de documento…' }, ...subibles.map((t) => ({ v: String(t.id), l: t.nombre }))]}
                className="campo campo-select"
                ariaLabel="Tipo de documento"
                chevronTamano={13}
              />
            </span>
            <label className={'btn-enlace' + (subiendo ? ' deshabilitado' : '')} aria-disabled={subiendo || undefined}>
              <span>{subiendo ? 'Subiendo…' : '+ Subir archivos'}</span>
              <input
                type="file"
                multiple
                accept={ACEPTA_ARCHIVOS}
                disabled={subiendo}
                style={{ display: 'none' }}
                onClick={(e) => {
                  if (!tipo) {
                    e.preventDefault()
                    setFaltaTipo(true)
                  }
                }}
                onChange={(e) => {
                  const archivos = Array.from(e.target.files ?? [])
                  e.target.value = ''
                  subir(archivos)
                }}
              />
            </label>
          </div>
        )}
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {!datos && <span className="vacio">Cargando archivos…</span>}
        {grupos.map((g) => (
          <div key={g.tipoId} style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <div className="archivos-grupo">
              <span>{g.tipo}</span>
              <small>{plural(g.archivos.length, 'archivo')}</small>
            </div>
            {g.archivos.map((a) => (
              <div key={a.id} className="archivo">
                <span style={{ color: 'var(--ink3)', display: 'inline-flex' }}><Documento /></span>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div className="archivo-nombre" title={a.nombre}>{a.nombre}</div>
                  <div className="archivo-meta">
                    {a.cuotaPago ? `Comprobante del pago de la cuota ${a.cuotaPago} · ` : ''}Cargado el {fFecha(a.fechaCarga)} · {fTamano(a.tamano)}
                  </div>
                </div>
                <button className="btn-archivo" title="Descargar" aria-label={'Descargar ' + a.nombre} onClick={() => descargar(a)}>
                  <Descargar />
                </button>
                {a.puedeEliminar && (
                  <button className="btn-archivo quitar" title="Quitar" aria-label={'Quitar ' + a.nombre} onClick={() => setQuitar(a)}>
                    <BasureroLleno />
                  </button>
                )}
              </div>
            ))}
          </div>
        ))}
        {faltaTipo && <span role="alert" className="msg-error" style={{ fontSize: 12.5, marginTop: 0 }}>Seleccione el tipo de documento antes de subir.</span>}
        {error && <span role="alert" className="msg-error" style={{ fontSize: 12.5, marginTop: 0 }}>{error}</span>}
        {datos && grupos.length === 0 && <span className="vacio">Sin archivos cargados.</span>}
      </div>
    </div>
  )
}
