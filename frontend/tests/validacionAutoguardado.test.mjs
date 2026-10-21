import test from 'node:test'
import assert from 'node:assert/strict'
import { formVacio, payloadDatos, erroresDatos, erroresVenta, erroresPrima, errorMonto, errorNumeros, errorFamiliares, errorCorreos, faltantesNuevo, etiquetaDe, avisoPendiente } from '../src/pages/persona/formulario.ts'

function valido() {
  const f = formVacio()
  Object.assign(f.datos, { nombres:'Ana', apellidos:'Vargas', cedula:'123', finca:'01-20013', vendedorId:'1', telefonoPrincipal:'88888888' })
  Object.assign(f.venta, { procedenciaVentaId:'1', metodoVentaId:'1', monto:'1000' })
  Object.assign(f.prima, { monto:'100', montoCancelado:'0' })
  return f
}
test('prospecto y cliente comparten los datos requeridos; correo permanece opcional', () => {
  for (const cliente of [false, true]) {
    const f=valido(); assert.deepEqual(erroresDatos(f,cliente),{})
    for (const k of ['nombres','apellidos','cedula',...(cliente ? [] : ['finca']),'fechaIngreso','vendedorId']) {
      const vacio=structuredClone(f); vacio.datos[k]=''
      assert.ok(erroresDatos(vacio,cliente)[k],k)
    }
  }
  const f=valido();f.datos.correoPrincipal='ana@';assert.ok(erroresDatos(f,false).correoPrincipal)
})
test('borrar fecha no genera hoy y las fechas inválidas quedan pendientes', () => {
  const f=valido();f.datos.fechaIngreso=''
  assert.equal(payloadDatos(f,false).fechaIngreso,'')
  assert.ok(erroresDatos(f,false).fechaIngreso)
  f.datos.fechaIngreso='2026-02-30';assert.ok(erroresDatos(f,false).fechaIngreso)
  f.prima.fechaEstimadaPago='';assert.deepEqual(erroresPrima(f),{})
})
test('montos vacíos, cero e incompletos bloquean; cancelado cero es válido', () => {
  for (const valor of ['', '0', '125.', '.']) assert.ok(errorMonto(valor), valor)
  assert.equal(errorMonto('125.50'),undefined)
  assert.equal(errorMonto('0',true),undefined)
  assert.ok(errorMonto('',true));assert.ok(errorMonto('-1',true))
  const f=valido();f.venta.monto='';f.prima.monto=''
  assert.ok(erroresVenta(f).montoVenta);assert.ok(erroresPrima(f).montoPrima)
  assert.equal(erroresPrima(f).montoCancelado,undefined)
  f.prima.montoCancelado='';assert.ok(erroresPrima(f).montoCancelado)
})
test('prima pagada exige fecha también al crear y cada familiar exige nombre y contacto', () => {
  const f=valido();f.prima.montoCancelado='100'
  assert.ok(faltantesNuevo(f).fechaPago)
  f.prima.fechaPago='2026-10-13';assert.equal(erroresPrima(f).fechaPago,undefined)
  const familiar={ id:-1,nombreCompleto:'Luis',parentescoId:null,telefono:'',correoElectronico:'' }
  assert.ok(errorFamiliares([familiar]))
  assert.equal(errorFamiliares([{...familiar,telefono:'88887777'}]),undefined)
  assert.equal(errorFamiliares([{...familiar,correoElectronico:'luis@mail.cr'}]),undefined)
  assert.ok(errorFamiliares([{...familiar,telefono:'88887777',correoElectronico:'correo-invalido'}]))
  assert.ok(errorFamiliares([{...familiar,nombreCompleto:'',telefono:'88887777'}]))
  assert.equal(errorFamiliares([]),undefined)
})
test('contactos requieren un número y nunca guardan una fila numérica vacía', () => {
  assert.ok(errorNumeros([]))
  assert.ok(errorNumeros([{numero:'',tipo:'Teléfono'}]))
  assert.equal(errorNumeros([{numero:'88888888',tipo:'WhatsApp'}]),undefined)
  assert.equal(errorCorreos([]),undefined)
  assert.ok(errorCorreos([{correo:'ana@'}]))
})
test('cada campo que puede dar error tiene etiqueta visible para el aviso de pendientes', () => {
  const f=formVacio()
  Object.assign(f.datos,{ fechaIngreso:'', correoPrincipal:'ana@', expediente:'123' })
  f.venta.notas='x'.repeat(1001)
  Object.assign(f.prima,{ montoCancelado:'', fechaEstimadaPago:'2026-13-40' })
  const claves=new Set([...Object.keys(erroresDatos(f,false)),...Object.keys(erroresVenta(f)),...Object.keys(erroresPrima(f))])
  f.prima.monto='100';f.prima.montoCancelado='100';f.prima.fechaPago=''
  for (const k of Object.keys(erroresPrima(f))) claves.add(k)
  assert.ok(claves.size>=18)
  for (const k of claves) assert.notEqual(etiquetaDe(k),k,`falta etiqueta para ${k}`)
})
test('el aviso de pendientes une etiqueta y mensaje en minúscula sin tocar el mensaje original', () => {
  assert.equal(avisoPendiente('procedenciaVentaId','Campo requerido.'),'Procedencia de venta, campo requerido.')
  assert.equal(avisoPendiente('fechaIngreso','Ingrese una fecha válida.'),'Fecha de ingreso, ingrese una fecha válida.')
  assert.equal(erroresVenta(formVacio()).procedenciaVentaId,'Campo requerido.')
})
test('el estado del cliente se envía tal cual y Activo muestra el calculado', async () => {
  const { formDesde } = await import('../src/pages/persona/formulario.ts')
  const { estadoMostrado, opcionesFiltroEstado, ESTADO_ACTIVO } = await import('../src/utils/estadoCliente.ts')
  const f=valido()
  assert.equal(payloadDatos(f,true).estadoClienteId,null)
  f.datos.estadoClienteId='5'
  assert.equal(payloadDatos(f,true).estadoClienteId,5)
  assert.equal(payloadDatos(f,false).estadoClienteId,null)
  const base={nombres:'Ana',apellidos:'Vargas',cedula:'',finca:'',expediente:'',fechaIngreso:'2026-10-13',vendedorId:null,telefonoPrincipal:'',whatsappPrincipal:'',correoPrincipal:'',numeros:[],correos:[],familiares:[]}
  assert.equal(formDesde({...base,cliente:{id:1,origenClienteId:1,estadoClienteId:4,estadoClienteCalculadoId:2,notas:''}}).datos.estadoClienteId,'4')

  const cats={estadosCliente:[{id:1,nombre:'Activo'},{id:4,nombre:'Terminado'}],estadosClienteCalculado:[{id:1,nombre:'Al Día'},{id:2,nombre:'Moroso'}]}
  assert.deepEqual(estadoMostrado(cats,ESTADO_ACTIVO,2),{clave:2,nombre:'Moroso'})
  assert.deepEqual(estadoMostrado(cats,4,2),{clave:4,nombre:'Terminado'})
  assert.deepEqual(estadoMostrado(null,4,2,'Respaldo'),{clave:4,nombre:'Respaldo'})
  // El filtro ofrece los calculados (prefijo "c") y los elegidos, sin Activo.
  assert.deepEqual(opcionesFiltroEstado(cats).map(o=>o.v),['c1','c2','4'])
})
