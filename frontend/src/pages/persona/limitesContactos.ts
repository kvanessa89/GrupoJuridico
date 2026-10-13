import type { NumeroDto } from '../../api/tipos'

export const MAX_TELEFONOS = 2
export const MAX_WHATSAPP = 2
export const MAX_CORREOS = 3
export const MAX_FAMILIARES = 4

export function limiteNumeros(numeros: Pick<NumeroDto, 'tipo'>[]): string | undefined {
  if (numeros.filter(n => n.tipo === 'Teléfono').length > MAX_TELEFONOS)
    return 'Se permiten como máximo 2 teléfonos por persona.'
  if (numeros.filter(n => n.tipo === 'WhatsApp').length > MAX_WHATSAPP)
    return 'Se permiten como máximo 2 números de WhatsApp por persona.'
}
export function limiteCorreos(correos: readonly unknown[]): string | undefined {
  if (correos.length > MAX_CORREOS) return 'Se permiten como máximo 3 correos por persona.'
}
export function limiteFamiliares(familiares: readonly unknown[]): string | undefined {
  if (familiares.length > MAX_FAMILIARES) return 'Se permiten como máximo 4 familiares por persona.'
}
