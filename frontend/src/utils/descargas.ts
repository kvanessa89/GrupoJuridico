/** Guarda un archivo descargado con el token de la sesión (la API no se puede enlazar directo). */
export function guardarBlob(blob: Blob, nombre: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = nombre
  a.click()
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}
