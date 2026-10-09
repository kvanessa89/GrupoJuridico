// Íconos SVG tomados del diseño.
import type { CSSProperties } from 'react'

const trazo = { fill: 'none', stroke: 'currentColor', strokeLinecap: 'round', strokeLinejoin: 'round' } as const

export function Maletin({ tamano = 25 }: { tamano?: number }) {
  return (
    <svg width={tamano} height={tamano} viewBox="0 0 24 24" fill="none" style={{ flex: 'none' }}>
      <rect x="3" y="7" width="18" height="13" rx="3" fill="#B8860B" />
      <path d="M9 7V5.6A1.6 1.6 0 0 1 10.6 4h2.8A1.6 1.6 0 0 1 15 5.6V7" stroke="#B8860B" strokeWidth="1.8" strokeLinecap="round" />
    </svg>
  )
}

export function Chevron({ tamano = 14, color = '#A8A29E', estilo }: { tamano?: number; color?: string; estilo?: CSSProperties }) {
  return (
    <svg width={tamano} height={tamano} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth="2.6" strokeLinecap="round" strokeLinejoin="round" style={estilo}>
      <path d="m6 9 6 6 6-6" />
    </svg>
  )
}

export function Lupa() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#A8A29E" strokeWidth="2" strokeLinecap="round">
      <circle cx="11" cy="11" r="7" />
      <path d="m20 20-3.6-3.6" />
    </svg>
  )
}

export function Mas({ tamano = 15, grosor = 2.4 }: { tamano?: number; grosor?: number }) {
  return (
    <svg width={tamano} height={tamano} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={grosor} strokeLinecap="round">
      <path d="M12 5v14M5 12h14" />
    </svg>
  )
}

export function Atras({ tamano = 15 }: { tamano?: number }) {
  return (
    <svg width={tamano} height={tamano} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round">
      <path d="M14.5 5.5 8 12l6.5 6.5" />
    </svg>
  )
}

export function Adelante({ tamano = 14 }: { tamano?: number }) {
  return (
    <svg width={tamano} height={tamano} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round">
      <path d="M9.5 5.5 16 12l-6.5 6.5" />
    </svg>
  )
}

export function Check({ tamano = 13, grosor = 2.4 }: { tamano?: number; grosor?: number }) {
  return (
    <svg width={tamano} height={tamano} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={grosor} strokeLinecap="round">
      <path d="M4 12.5 9 17.5 20 6.5" />
    </svg>
  )
}

/** Basurero de contorno (tablas y usuarios). */
export function Basurero({ tamano = 15 }: { tamano?: number }) {
  return (
    <svg width={tamano} height={tamano} viewBox="0 0 24 24" {...trazo} strokeWidth="2">
      <path d="M4 7h16" /><path d="M10 11v6" /><path d="M14 11v6" /><path d="M6 7l1 13h10l1-13" /><path d="M9 7V4h6v3" />
    </svg>
  )
}

/** Basurero relleno (filas de contacto, familiares y comentarios). */
export function BasureroLleno({ tamano = 14 }: { tamano?: number }) {
  return (
    <svg width={tamano} height={tamano} viewBox="0 0 24 24" fill="currentColor">
      <path d="M9 3h6l1 2h4v2H4V5h4l1-2Zm-3 6h12l-1 12H7L6 9Z" />
    </svg>
  )
}

/** Basurero de los catálogos de Configuración. */
export function BasureroFino() {
  return (
    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
      <path d="M4 7h16M10 4h4M9.5 7v11M14.5 7v11M6 7l1 14h10l1-14" />
    </svg>
  )
}

export function Estrella({ llena }: { llena: boolean }) {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill={llena ? 'currentColor' : 'none'} stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round">
      <path d="m12 3 2.8 5.7 6.2.9-4.5 4.4 1.1 6.2L12 17.3 6.4 20.2l1.1-6.2L3 9.6l6.2-.9Z" />
    </svg>
  )
}

export function Ojo({ tachado }: { tachado: boolean }) {
  return tachado ? (
    <svg width="16" height="16" viewBox="0 0 24 24" {...trazo} strokeWidth="2">
      <path d="M3 3l18 18" /><path d="M10.6 10.6a2 2 0 0 0 2.8 2.8" />
      <path d="M9.9 5.1A10 10 0 0 1 12 5c6 0 9.5 7 9.5 7a17 17 0 0 1-3.2 4.1" />
      <path d="M6.1 6.1C3.8 7.7 2.5 12 2.5 12S6 19 12 19a9.6 9.6 0 0 0 4.2-1" />
    </svg>
  ) : (
    <svg width="16" height="16" viewBox="0 0 24 24" {...trazo} strokeWidth="2">
      <path d="M2.5 12S6 5 12 5s9.5 7 9.5 7-3.5 7-9.5 7-9.5-7-9.5-7z" /><circle cx="12" cy="12" r="3" />
    </svg>
  )
}

export function IconoMenu() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" {...trazo} strokeWidth="1.8" style={{ flex: 'none' }}>
      <path d="M4 7h16" /><path d="M4 12h16" /><path d="M4 17h16" />
    </svg>
  )
}

export function IconoVentas() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" {...trazo} strokeWidth="1.8" style={{ flex: 'none' }}>
      <path d="M4 4v16h16" /><path d="M8 16v-4" /><path d="M12 16V8" /><path d="M16 16v-6" />
    </svg>
  )
}

export function IconoClientes() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" {...trazo} strokeWidth="1.8" style={{ flex: 'none' }}>
      <circle cx="9" cy="8" r="3.2" /><path d="M3.5 19c.6-3 2.8-4.6 5.5-4.6s4.9 1.6 5.5 4.6" />
      <path d="M15.5 5.2a3 3 0 0 1 0 5.6" /><path d="M17.5 14.6c1.6.6 2.6 2 3 4.4" />
    </svg>
  )
}

export function IconoConfig() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" {...trazo} strokeWidth="1.8" style={{ flex: 'none' }}>
      <path d="M4 6h12" /><path d="M4 11h12" /><path d="M4 16h7" /><path d="M17 15v6" /><path d="M14 18h6" />
    </svg>
  )
}

export function IconoUsuarios() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" {...trazo} strokeWidth="1.8" style={{ flex: 'none' }}>
      <circle cx="12" cy="8" r="3.5" /><path d="M5.5 20c.7-3.4 3.3-5.3 6.5-5.3s5.8 1.9 6.5 5.3" />
    </svg>
  )
}
