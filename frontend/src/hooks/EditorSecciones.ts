export type SeccionVersion = 'datos' | 'venta' | 'prima' | 'familiares'
export type ResultadoVersion<T> = { data: T; version: string }

/** Serializa escrituras locales del mismo grupo y conserva su última versión confirmada. */
export class EditorSecciones {
  private versiones: Partial<Record<SeccionVersion, string>> = {}
  private colas: Partial<Record<SeccionVersion, Promise<unknown>>> = {}
  private bloqueadas = new Set<SeccionVersion>()
  cargar(versiones: Record<SeccionVersion, string>) { this.versiones = { ...versiones } }
  resolver(seccion: SeccionVersion, version: string) {
    this.versiones[seccion] = version
    this.bloqueadas.delete(seccion)
  }
  guardar<T>(seccion: SeccionVersion, enviar: (version: string) => Promise<ResultadoVersion<T>>): Promise<T> {
    const tarea = (this.colas[seccion] ?? Promise.resolve()).catch(() => {}).then(async () => {
      if (this.bloqueadas.has(seccion)) throw new Error('Revise el conflicto antes de volver a guardar esta sección.')
      const version = this.versiones[seccion]
      if (!version) throw new Error('La ficha todavía no tiene una versión de lectura.')
      try {
        const resultado = await enviar(version)
        this.versiones[seccion] = resultado.version
        return resultado.data
      } catch (e) {
        const status = (e as { response?: { status?: number } }).response?.status
        if (status === 409 || status === 428) this.bloqueadas.add(seccion)
        throw e
      }
    })
    this.colas[seccion] = tarea
    return tarea
  }
}
