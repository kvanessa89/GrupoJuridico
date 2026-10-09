import { Link } from 'react-router'
import './inicio.css'

const AREAS = [
  {
    titulo: 'Derecho Público',
    nota: 'Enfocado en la relación del ciudadano con el Estado.',
    items: ['Derecho Constitucional', 'Derecho Administrativo', 'Derecho Tributario y Municipal', 'Derecho Internacional', 'Derecho Aduanero', 'Contencioso administrativo'],
  },
  {
    titulo: 'Derecho Privado',
    nota: 'Regula las relaciones entre personas físicas o jurídicas.',
    items: ['Derecho Civil y Mercantil', 'Derecho de Familia', 'Derecho Laboral', 'Derecho Registral', 'Derecho Inmobiliario'],
  },
  {
    titulo: 'Derecho Empresarial y Corporativo',
    nota: 'Dirigido a empresas, sociedades y negocios.',
    items: ['Derecho Corporativo', 'Derecho Mercantil o Comercial', 'Procesos de cobros judiciales', 'Derecho Notarial'],
  },
  {
    titulo: 'Derecho Especializado',
    nota: 'Áreas con aplicación técnica o moderna.',
    items: ['Derecho Intelectual (marcas, propiedad industrial, derechos de autor)', 'Derecho Migratorio', 'Derecho Penal (defensas y litigios penales)'],
  },
]

/** Página principal pública (sin autenticación). */
export function Inicio() {
  return (
    <div className="inicio">
      <header className="inicio-header">
        <div className="inicio-header-fila">
          <div className="inicio-marca">
            <img src="/logo-gjha-transparente.png" alt="Grupo Jurídico Hernández & Asociados" />
            <div style={{ minWidth: 0 }}>
              <div className="inicio-marca-nombre">Grupo Jurídico Hernández &amp; Asociados</div>
              <div className="inicio-marca-lema">Defiéndase con los expertos</div>
            </div>
          </div>
        </div>
      </header>

      <section className="inicio-hero">
        <div className="inicio-hero-grilla">
          <div>
            <div className="inicio-hero-kicker">Costa Rica · Bufete de abogados</div>
            <h1>El que busca<br />encuentra</h1>
            <p>
              Grupo Jurídico Hernández &amp; Asociados acompaña a personas, familias y empresas en todas las áreas del derecho:
              público, privado, corporativo y especializado.
            </p>
            <div className="inicio-hero-botones">
              <a href="#areas" className="inicio-btn">
                <span>Áreas de práctica</span>
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"><path d="M5 12h14M13 6l6 6-6 6" /></svg>
              </a>
              <a href="https://grupo-juridico.com/quienes-somos/" className="inicio-btn-sec">¿Quiénes somos?</a>
            </div>
          </div>
          <div className="inicio-hero-logo">
            <img src="/logo-gjha-transparente.png" alt="Grupo Jurídico Hernández & Asociados" />
          </div>
        </div>
      </section>

      <section id="areas" className="inicio-areas">
        <h2>Áreas de Práctica</h2>
        <p className="inicio-areas-sub">Cuatro grandes ramas de especialización, con equipos dedicados a cada materia.</p>
        <div className="inicio-areas-grilla">
          {AREAS.map((a) => (
            <article key={a.titulo} className="inicio-area">
              <div className="inicio-area-titulo">{a.titulo}</div>
              <p>{a.nota}</p>
              <ul>
                {a.items.map((it) => (
                  <li key={it}><span className="punto" /><span>{it}</span></li>
                ))}
              </ul>
            </article>
          ))}
        </div>
      </section>

      <section className="inicio-acceso">
        <div className="inicio-acceso-fila">
          <a href="https://grupo-juridico.com/" className="inicio-enlace-sitio">Consultas y valoración de casos · grupo-juridico.com</a>
          <div className="inicio-acceso-links">
            <span className="inicio-acceso-titulo">Acceso interno</span>
            <a href="https://www.ventas.grupo-juridicocrm.com" className="inicio-acceso-link">CRM Ventas</a>
            <Link to="/gestion" className="inicio-acceso-link">Grupo Jurídico Gestión</Link>
          </div>
        </div>
      </section>

      <footer className="inicio-footer">
        <div className="inicio-footer-fila">
          <div className="inicio-footer-nombre">Grupo Jurídico Hernández &amp; Asociados</div>
          <div className="inicio-footer-lema">“Defiéndase con los expertos”</div>
        </div>
        <div className="inicio-footer-legal">
          <span>© 2026 Grupo Jurídico CRM · Todos los derechos reservados · Desarrollado por</span>
          <a href="https://www.kiub.co" target="_blank" rel="noopener" className="inicio-kiub">
            <img src="/kiub-logo.png" alt="Kiub" />
          </a>
        </div>
      </footer>
    </div>
  )
}
