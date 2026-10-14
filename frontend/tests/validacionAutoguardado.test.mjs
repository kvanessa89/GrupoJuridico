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
    for (const k of ['nombres','apellidos','cedula','finca','fechaIngreso','vendedorId']) {
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
  const familiar={ id:-1,nombreCompleto:'Luis',parentescoId:null,telefono:'',correoElectronico:'',whatsapp:'' }
  assert.ok(errorFamiliares([familiar]))
  assert.ok(errorFamiliares([{...familiar,whatsapp:'88888888'}]))
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
