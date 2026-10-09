import { useCallback } from 'react'
import { useNavigate } from 'react-router'
import { api } from '../../api/endpoints'
import type { FiltrosLista } from '../../api/tipos'
import { permisos, useSesion } from '../../auth/Sesion'
import { ChipEstadoCliente, ChipPrima } from '../../components/Chips'
import { BarraFiltros, Celda, Paginacion, Tabla, type Columna, type FilaDef } from '../../components/Lista'
import { useListado } from '../../hooks/useListado'
import { useFiltros } from '../../layout/Filtros'
import { fFecha, origenEtiqueta, vendedorEtiqueta } from '../../utils/formato'

const POR_PAGINA = 10

/** Clientes oficiales. Cobros ve menos columnas (sin estado del cliente ni origen). */
export function ClientesPage() {
  const { usuario, catalogos } = useSesion()
  const navegar = useNavigate()
  const { filtros, set, limpiar } = useFiltros('clientes')
  const cargar = useCallback((f: FiltrosLista) => api.clientes(f, POR_PAGINA), [])
  const { datos, cargando, error } = useListado(cargar, filtros)

  const esCobros = usuario?.rol === 'Cobros'
  const ocultaPagadas = permisos.ocultaPagadas(usuario?.rol)

  const columnas: Columna[] = esCobros
    ? [
        { label: 'Expediente - Nombre', orden: 'expediente', ancho: '28%' },
        { label: 'Contacto', ancho: '28%' },
        { label: 'Finca', ancho: '14%' },
        { label: 'Ingreso', orden: 'ingreso', ancho: '15%' },
        { label: 'Estado prima', orden: 'estado', ancho: '15%' },
      ]
    : [
        { label: 'Expediente - Nombre', orden: 'expediente', ancho: '21%' },
        { label: 'Contacto', ancho: '19%' },
        { label: 'Finca', ancho: '9%' },
        { label: 'Ingreso', orden: 'ingreso', ancho: '11%' },
        { label: 'Estado prima', orden: 'estado', ancho: '12%' },
        { label: 'Estado del cliente', orden: 'estadoCliente', ancho: '12%' },
        { label: 'Origen del cliente', orden: 'origen', ancho: '16%' },
      ]

  const filas: FilaDef[] = (datos?.filas ?? []).map((c) => {
    const celdas = [
      <Celda t={c.expediente || 'Sin expediente'} s={c.nombreCompleto || 'Sin nombre'} clase="c-txt" subClase="c-sub-min" />,
      <Celda t={c.telefono ?? '—'} s={c.correo ?? '—'} clase="c-num-suave" subClase="c-corte" />,
      <Celda t={c.finca || '—'} clase="c-num-suave" />,
      <Celda t={fFecha(c.fechaIngreso)} clase="c-num-suave" />,
      c.estadoPrima ? <ChipPrima estadoId={c.estadoPrimaId} texto={c.estadoPrima.replace(/^Prima\s+/i, '')} /> : <Celda t="—" />,
    ]
    if (!esCobros) {
      celdas.push(<ChipEstadoCliente estadoId={c.estadoClienteId} texto={c.estadoCliente} />, <Celda t={c.origen ?? '—'} />)
    }
    return { id: c.id, celdas }
  })

  const cats = catalogos
  return (
    <div className="contenedor-lista">
      <div className="cabecera-lista">
        <div style={{ minWidth: 0 }}>
          <h1>Clientes</h1>
          <p>Personas con expediente asignado: clientes oficiales de la cartera.</p>
        </div>
      </div>

      <BarraFiltros
        q={filtros.q}
        onQ={(q) => set({ q })}
        placeholder="Nombre, cédula, expediente, teléfono o finca…"
        conteo={datos?.total ?? 0}
        onLimpiar={limpiar}
        filtros={[
          {
            valor: filtros.vendedorId,
            onChange: (v) => set({ vendedorId: v }),
            opciones: [{ v: '', l: 'Vendedores: Todos' }, ...(cats?.vendedores ?? []).map((x) => ({ v: String(x.id), l: vendedorEtiqueta(x) }))],
          },
          {
            valor: filtros.anio,
            onChange: (v) => set({ anio: v }),
            opciones: [{ v: '', l: 'Todos los años' }, ...(datos?.aniosIngreso ?? []).map((a) => ({ v: String(a), l: 'Ingreso ' + a }))],
          },
          {
            valor: filtros.procedenciaId,
            onChange: (v) => set({ procedenciaId: v }),
            opciones: [{ v: '', l: 'Procedencia: Todas' }, ...(cats?.procedencias ?? []).map((x) => ({ v: String(x.id), l: x.nombre }))],
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
            valor: filtros.estadoClienteId,
            onChange: (v) => set({ estadoClienteId: v }),
            opciones: [{ v: '', l: 'Estado: Todos' }, ...(cats?.estadosCliente ?? []).map((x) => ({ v: String(x.id), l: x.nombre }))],
          },
          {
            valor: filtros.origenId,
            onChange: (v) => set({ origenId: v }),
            opciones: [{ v: '', l: 'Origen: Todos' }, ...(cats?.origenes ?? []).map((o) => ({ v: String(o.id), l: origenEtiqueta(o) }))],
          },
        ]}
      />

      {error && <p className="texto-error" style={{ margin: '0 0 12px' }}>No se pudo cargar la lista de clientes.</p>}

      <Tabla
        columnas={columnas}
        filas={filas}
        filtros={filtros}
        setFiltros={set}
        cargando={cargando && !datos}
        onAbrir={(id) => navegar(`/gestion/clientes/${id}`)}
      />
      <Paginacion pagina={datos?.pagina ?? 1} total={datos?.total ?? 0} porPagina={POR_PAGINA} onPagina={(pagina) => set({ pagina })} />
    </div>
  )
}
