import type { ArchivoDto } from '../../api/tipos'

export interface GrupoArchivos {
  tipoId: number
  tipo: string
  archivos: ArchivoDto[]
}

/** Archivos agrupados por tipo de documento, en el orden del catálogo; los grupos vacíos no se muestran. */
export function agruparArchivos(archivos: ArchivoDto[], tipos: { id: number; nombre: string }[]): GrupoArchivos[] {
  const grupos = tipos
    .map((t) => ({ tipoId: t.id, tipo: t.nombre, archivos: archivos.filter((a) => a.tipoDocumentoId === t.id) }))
  // Un archivo de un tipo que ya no está en el catálogo cargado igual se muestra, al final.
  const conocidos = new Set(tipos.map((t) => t.id))
  const otros = archivos.filter((a) => !conocidos.has(a.tipoDocumentoId))
  if (otros.length) grupos.push({ tipoId: 0, tipo: 'Otros', archivos: otros })
  return grupos.filter((g) => g.archivos.length > 0)
}

/** 2048 → "2 KB"; 1572864 → "1.5 MB". */
export function fTamano(bytes: number): string {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return Math.round(bytes / 1024) + ' KB'
  return (Math.round((bytes / (1024 * 1024)) * 10) / 10) + ' MB'
}

/** Formatos que acepta la API (PDF, imágenes, Word y Excel). */
export const ACEPTA_ARCHIVOS = '.pdf,.jpg,.jpeg,.png,.webp,.doc,.docx,.xls,.xlsx'
