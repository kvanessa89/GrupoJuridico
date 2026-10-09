import { useCallback, useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import { mensajeDe } from '../../api/cliente'
import { api } from '../../api/endpoints'
import type { FiltrosLista, VentaFila } from '../../api/tipos'
import { permisos, useSesion } from '../../auth/Sesion'
import { ChipPrima, ChipTipo } from '../../components/Chips'
import { Modal } from '../../components/Controles'
import { Mas } from '../../components/Iconos'
import { BarraFiltros, Celda, Paginacion, Tabla, Totales, type Columna, type FilaDef } from '../../components/Lista'
import { useListado } from '../../hooks/useListado'
import { useFiltros } from '../../layout/Filtros'
import { fCorta, fMes, fMonto, mesActual, vendedorEtiqueta } from '../../utils/formato'

const POR_PAGINA = 10

export function VentasPage() {
  const { usuario, catalogos } = useSesion()
  const navegar = useNavigate()
  const { filtros, set, limpiar } = useFiltros('ventas')
  const cargar = useCallback((f: FiltrosLista) => api.ventas(f, POR_PAGINA), [])
  const { datos, cargando, error, recargar } = useListado(cargar, filtros)
  const [eliminar, setEliminar] = useState<VentaFila | null>(null)
  const [errorEliminar, setErrorEliminar] = useState('')

  const esAdmin = permisos.esAdmin(usuario?.rol)
  const ocultaPagadas = permisos.ocultaPagadas(usuario?.rol)

  const columnas: Columna[] = [
    { label: 'Expediente - Nombre', orden: 'expediente', ancho: esAdmin ? '21%' : '25%' },
    { label: 'Teléfono y WhatsApp', ancho: '10%' },
    { label: 'Procedencia y método', orden: 'procedencia', ancho: '11%' },
    { label: 'Vendido por', orden: 'vendedor', ancho: '10%' },
    { label: 'Monto prima', orden: 'prima', ancho: '9%' },
    { label: 'Saldo pendiente prima', orden: 'saldo', ancho: '9%' },
    { label: 'Fecha estimada de pago', orden: 'fechaEstimadaPago', ancho: '8%' },
    { label: 'Estado prima', orden: 'estado', ancho: '9%' },
    { label: 'Tipo', orden: 'tipo', ancho: '9%' },
  ]

  const meses = useMemo(() => {
    const todos = new Set([...(datos?.mesesPago ?? []), mesActual()])
    if (filtros.mes) todos.add(filtros.mes)
    return [...todos].sort()
  }, [datos?.mesesPago, filtros.mes])

  const filas: FilaDef[] = (datos?.filas ?? []).map((v) => ({
    id: v.id,
    celdas: [
      <Celda t={v.expediente || 'Sin expediente'} s={v.nombreCompleto || 'Sin nombre'} clase="c-txt" subClase="c-sub-min" />,
      <Celda t={v.telefono ?? '—'} s={v.whatsapp ? 'WA ' + v.whatsapp : ''} clase="c-num-suave" subClase="c-sub-min" />,
      <Celda t={v.procedencia ?? '—'} s={v.metodo ?? ''} />,
      <Celda t={v.vendedor ?? '—'} />,
      <Celda t={fMonto(v.montoPrima)} clase="c-num" />,
      <Celda t={fMonto(v.saldoPendiente)} clase="c-num" />,
      <Celda t={fCorta(v.fechaEstimadaPago)} clase="c-num-suave" />,
      <ChipPrima estadoId={v.estadoPrimaId} texto={v.estadoPrima.replace(/^Prima\s+/i, '')} />,
      <ChipTipo esCliente={v.esCliente} />,
    ],
  }))

  const t = datos?.totales
  const total = datos?.total ?? 0
  const totales = t
    ? [
        ...(t.montoVentas != null
          ? [{ label: 'Monto total de ventas', valor: fMonto(t.montoVentas), nota: total === 1 ? '1 venta en la lista' : total + ' ventas en la lista' }]
          : []),
        { label: 'Monto total de primas', valor: fMonto(t.montoPrimas), nota: 'Primas de las ventas en la lista' },
        { label: 'Primas pagadas', valor: fMonto(t.primasPagadas), nota: 'Pagos recibidos' },
        { label: 'Primas pendientes de pagar', valor: fMonto(t.primasPendientes), nota: 'Saldo por cobrar' },
      ]
    : []

  const cats = catalogos
  const confirmarEliminar = async () => {
    if (!eliminar) return
    try {
      await api.eliminarPersona(eliminar.id)
      setEliminar(null)
      recargar()
    } catch (e) {
      setErrorEliminar(mensajeDe(e))
      setEliminar(null)
    }
  }

  return (
    <div className="contenedor-lista">
      {eliminar && (
        <Modal
          titulo={`¿Eliminar a ${eliminar.nombreCompleto || 'este prospecto'}?`}
          texto={
            eliminar.esCliente
              ? 'Esta persona ya es cliente. Se borrarán sus datos personales, la venta, la prima, el registro de cliente, teléfonos, correos, finca, familiares y comentarios. No se puede deshacer.'
              : 'Se borrarán sus datos personales, la venta, la prima, teléfonos, correos, finca, familiares y comentarios. No se puede deshacer.'
          }
          onCancelar={() => setEliminar(null)}
          onConfirmar={confirmarEliminar}
        />
      )}
      <div className="cabecera-lista">
        <div style={{ minWidth: 0 }}>
          <h1>Tabla de ventas</h1>
          <p>Todas las ventas registradas, con el estado de su prima y la fecha estimada de pago.</p>
        </div>
        <button className="btn-primario" onClick={() => navegar('/gestion/ventas/nuevo')}>
          <Mas />
          <span>Registrar prospecto</span>
        </button>
      </div>

      {totales.length > 0 && <Totales items={totales} />}

      <BarraFiltros
        q={filtros.q}
        onQ={(q) => set({ q })}
        placeholder="Nombre, cédula, teléfono, correo o finca…"
        conteo={total}
        onLimpiar={limpiar}
        filtros={[
          {
            valor: filtros.vendedorId,
            onChange: (v) => set({ vendedorId: v }),
            opciones: [{ v: '', l: 'Vendedores: Todos' }, ...(cats?.vendedores ?? []).map((x) => ({ v: String(x.id), l: vendedorEtiqueta(x) }))],
          },
          {
            valor: filtros.estadoPrimaId,
            onChange: (v) => set({ estadoPrimaId: v }),
            opciones: [
              { v: '', l: 'Primas: Todas' },
              ...(cats?.estadosPrima ?? []).filter((x) => !(ocultaPagadas && x.id === 3)).map((x) => ({ v: String(x.id), l: x.nombre })),
            ],
          },
          {
            valor: filtros.procedenciaId,
            onChange: (v) => set({ procedenciaId: v }),
            opciones: [{ v: '', l: 'Procedencia: Todas' }, ...(cats?.procedencias ?? []).map((x) => ({ v: String(x.id), l: x.nombre }))],
          },
          {
            valor: filtros.mes,
            onChange: (v) => set({ mes: v }),
            opciones: [{ v: '', l: 'Mes de pago: Todos' }, ...meses.map((m) => ({ v: m, l: fMes(m) }))],
          },
        ]}
      />

      {(error || errorEliminar) && <p className="texto-error" style={{ margin: '0 0 12px' }}>{errorEliminar || 'No se pudo cargar la tabla de ventas.'}</p>}

      <Tabla
        columnas={columnas}
        filas={filas}
        filtros={filtros}
        setFiltros={set}
        cargando={cargando && !datos}
        onAbrir={(id) => navegar(`/gestion/ventas/${id}`)}
        onEliminar={esAdmin ? (id) => { setErrorEliminar(''); setEliminar(datos?.filas.find((f) => f.id === id) ?? null) } : undefined}
      />
      <Paginacion pagina={datos?.pagina ?? 1} total={total} porPagina={POR_PAGINA} onPagina={(pagina) => set({ pagina })} />
    </div>
  )
}
