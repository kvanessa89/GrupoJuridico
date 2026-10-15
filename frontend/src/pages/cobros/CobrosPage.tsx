import { useCallback } from 'react'
import { useNavigate } from 'react-router'
import { api } from '../../api/endpoints'
import type { CobroFila, CobrosFiltros, CobrosLista, VistaCobros } from '../../api/tipos'
import { useSesion } from '../../auth/Sesion'
import { ChipEstadoCliente, ChipTono } from '../../components/Chips'
import { TONO_PROMESA } from '../../utils/colores'
import { Buscador, Celda, type Columna, EnlaceTelefono, type FilaDef, Paginacion, Tabla, Totales, type TotalDef } from '../../components/Lista'
import { useListado } from '../../hooks/useListado'
import { useFiltrosCobros } from '../../layout/Filtros'
import { fCorta, fDiaLargo, fMonto, plural } from '../../utils/formato'

const POR_PAGINA = 10

const TITULOS: Record<VistaCobros, string> = {
  morosos: 'Clientes morosos',
  vencido: 'Monto vencido',
  recordatorios: 'Recordatorios de hoy',
  promesas: 'Promesas pendientes',
  'cobro-judicial': 'Candidato a cobro judicial',
}

function nota(vista: VistaCobros, d: CobrosLista | null): string {
  switch (vista) {
    case 'recordatorios': {
      const fechas = d?.totales.fechasARecordar ?? []
      if (!d) return ''
      return fechas.length
        ? `Hoy es ${fDiaLargo(d.hoy)}. Recuérdele el pago a quienes pagan el ${fechas.map(fDiaLargo).join(' y el ')}. Los pagos de lunes se recuerdan el sábado.`
        : 'Hoy es domingo: no se envían recordatorios. Los pagos del lunes se recordaron el sábado.'
    }
    case 'vencido': return 'Clientes con saldo vencido en sus cuotas.'
    case 'promesas': return 'Clientes con una promesa de pago pendiente de cumplir.'
    case 'cobro-judicial': return 'Clientes morosos cuya última cuota del plan ya venció y todavía tienen saldo pendiente.'
    default: return 'Clientes con al menos una cuota vencida o en estado Moroso. Cuando vence una cuota, el estado del cliente pasa a Moroso automáticamente.'
  }
}

const COLUMNAS: Columna[] = [
  { label: 'Expediente - Nombre', orden: 'expediente', ancho: '22%' },
  { label: 'Teléfono y WhatsApp', ancho: '12%' },
  { label: 'Estado del cliente', orden: 'estadoCliente', ancho: '10%' },
  { label: 'Cuotas vencidas', orden: 'cuotasVenc', ancho: '10%' },
  { label: 'Monto vencido', orden: 'vencido', ancho: '11%' },
  { label: 'Atraso', orden: 'atraso', ancho: '9%' },
  { label: 'Próxima cuota', orden: 'proxima', ancho: '11%' },
  { label: 'Promesa', ancho: '15%' },
]

const COLUMNAS_RECORDATORIOS: Columna[] = [
  { label: 'Expediente - Nombre', orden: 'expediente', ancho: '25%' },
  { label: 'WhatsApp y teléfono', ancho: '14%' },
  { label: 'Cuota', ancho: '10%' },
  { label: 'Monto a pagar', ancho: '12%' },
  { label: 'Fecha de pago', orden: 'proxima', ancho: '14%' },
  { label: 'Estado del cliente', orden: 'estadoCliente', ancho: '11%' },
  { label: 'Promesa', ancho: '14%' },
]

/** Clientes de la cartera para cobrar: cada tarjeta de totales filtra la tabla; la fila abre el plan de pago. */
export function CobrosPage() {
  const navegar = useNavigate()
  const { catalogos } = useSesion()
  const { filtros, set } = useFiltrosCobros()
  const cargar = useCallback((f: CobrosFiltros) => api.cobros(f, POR_PAGINA), [])
  const { datos, cargando, error } = useListado(cargar, filtros)
  const vista = filtros.vista
  const t = datos?.totales

  const elegir = (v: VistaCobros) => {
    set({ vista: v, orden: v === 'recordatorios' ? 'expediente' : 'atraso', asc: v === 'recordatorios' })
    window.scrollTo(0, 0)
  }
  const tarjeta = (v: VistaCobros, valor: string, notaTarjeta: string): TotalDef => ({
    label: TITULOS[v], valor: t ? valor : '—', nota: notaTarjeta, activo: vista === v, onClick: () => elegir(v),
  })
  const fechas = t?.fechasARecordar ?? []
  const totales: TotalDef[] = [
    tarjeta('morosos', String(t?.morosos), 'Con cuotas vencidas o en estado Moroso'),
    tarjeta('vencido', fMonto(t?.montoVencido), 'Saldo de cuotas vencidas'),
    tarjeta('recordatorios', String(t?.recordatorios), fechas.length ? 'Pagan el ' + fechas.map(fDiaLargo).join(' y el ') : 'Hoy no se envían recordatorios'),
    tarjeta('promesas', String(t?.promesasPendientes), 'Acuerdos de pago por vencer'),
    tarjeta('cobro-judicial', String(t?.cobroJudicial), 'Plan vencido con saldo pendiente'),
  ]

  const nombreEstado = (id: number, texto: string) => catalogos?.estadosCliente.find((x) => x.id === id)?.nombre ?? texto
  const promesa = (f: CobroFila) => f.promesa
    ? <ChipTono tono={TONO_PROMESA[f.promesa.estadoId]} texto={(catalogos?.estadosPromesa.find((x) => x.id === f.promesa!.estadoId)?.nombre ?? '') + ' · ' + fCorta(f.promesa.fecha)} />
    : <Celda t="—" />
  const nombre = (f: CobroFila) => (
    <button
      className="enlace-nombre"
      title="Ver ficha del cliente"
      onClick={(e) => {
        e.stopPropagation()
        navegar(`/gestion/clientes/${f.id}`)
      }}
    >
      <Celda t={f.expediente || 'Sin expediente'} s={f.nombreCompleto || 'Sin nombre'} clase="c-txt" subClase="c-sub-min" />
    </button>
  )

  const filas: FilaDef[] = (datos?.filas ?? []).map((f) => {
    const estado = <ChipEstadoCliente estadoId={f.estadoClienteId} texto={nombreEstado(f.estadoClienteId, f.estadoCliente)} />
    if (vista === 'recordatorios') {
      const r = f.recordatorio
      return {
        id: f.id,
        celdas: [
          nombre(f),
          <Celda t={f.whatsapp ? 'WA ' + f.whatsapp : '—'} s={f.telefono ? <EnlaceTelefono numero={f.telefono} /> : undefined} clase="c-num-suave" subClase="c-sub-min" />,
          <Celda t={r ? 'Cuota ' + r.numeroCuota : '—'} s={f.tienePlan ? 'de ' + f.cantidadCuotas : undefined} clase="c-num-suave" subClase="c-sub-min" />,
          <Celda t={r ? fMonto(r.saldo) : '—'} clase="c-num" />,
          <Celda t={r ? fDiaLargo(r.fecha) : '—'} clase="c-num-suave" />,
          estado,
          promesa(f),
        ],
      }
    }
    return {
      id: f.id,
      celdas: [
        nombre(f),
        <Celda t={f.telefono ? <EnlaceTelefono numero={f.telefono} /> : '—'} s={f.whatsapp ? 'WA ' + f.whatsapp : undefined} clase="c-num-suave" subClase="c-sub-min" />,
        estado,
        <Celda t={f.tienePlan ? String(f.cuotasVencidas) : '—'} s={f.tienePlan ? 'de ' + plural(f.cantidadCuotas, 'cuota') : 'Sin plan'} clase="c-num" subClase="c-sub-min" />,
        <Celda t={f.tienePlan ? fMonto(f.montoVencido) : '—'} clase="c-num" />,
        <Celda t={f.atraso ? plural(f.atraso, 'día') : '—'} clase="c-num-suave" />,
        f.proxima
          ? <Celda t={fCorta(f.proxima.fecha)} s={fMonto(f.proxima.saldo)} clase="c-num-suave" subClase="c-sub-min" />
          : <Celda t={f.tienePlan ? 'Plan pagado' : 'Sin plan'} clase="c-num-suave" />,
        promesa(f),
      ],
    }
  })

  return (
    <div className="contenedor-lista">
      <div className="cabecera-lista">
        <div style={{ minWidth: 0 }}>
          <h1>Cobros</h1>
          <p>Clientes morosos y recordatorios de pago de los clientes oficiales.</p>
        </div>
      </div>

      <Totales items={totales} />

      <div className="cobros-vista">
        <div className="cobros-vista-titulo">{TITULOS[vista]}</div>
        <p>{nota(vista, datos)}</p>
      </div>

      <Buscador q={filtros.q} onQ={(q) => set({ q })} placeholder="Nombre, cédula, expediente o teléfono…" conteo={datos?.total ?? 0} />

      {error && <p className="texto-error" style={{ margin: '0 0 12px' }}>No se pudo cargar la lista de cobros.</p>}

      <Tabla
        columnas={vista === 'recordatorios' ? COLUMNAS_RECORDATORIOS : COLUMNAS}
        filas={filas}
        filtros={filtros}
        setFiltros={set}
        cargando={cargando}
        onAbrir={(id) => navegar(`/gestion/cobros/${id}`)}
      />
      <Paginacion pagina={datos?.pagina ?? 1} total={datos?.total ?? 0} porPagina={POR_PAGINA} onPagina={(pagina) => set({ pagina })} />
    </div>
  )
}
