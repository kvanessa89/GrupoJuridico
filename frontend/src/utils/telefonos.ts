export interface PaisTelefono { id: number; codigo: string }

/** Número local: el prefijo se conserva separado en el país. Vacío se valida por la regla de requeridos. */
export function errorTelefono(numero: string, paisId: number | null, paises: readonly PaisTelefono[] = []): string | undefined {
  if (!numero.trim()) return
  const pais = paises.find(p => p.id === (paisId ?? paises[0]?.id))
  const codigo = pais?.codigo.replace(/\D/g, '')
  const largo = codigo === '506' ? 8 : undefined
  if (largo && numero.replace(/\D/g, '').length !== largo)
    return `El número debe tener ${largo} dígitos, sin el prefijo del país.`
}

