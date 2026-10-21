import test from 'node:test'
import assert from 'node:assert/strict'
import { formVacio, erroresDatos, erroresVenta, errorExpediente, errorFincas, faltantesNuevo, MAX_FINCA, MAX_EXPEDIENTE, MAX_NOTAS_VENTA } from '../src/pages/persona/formulario.ts'
const valido=()=>{const f=formVacio();Object.assign(f.datos,{nombres:'Ana',apellidos:'Vargas',cedula:'123',finca:'12-12345',vendedorId:'1',telefonoPrincipal:'88887777'});Object.assign(f.venta,{procedenciaVentaId:'1',metodoVentaId:'1',monto:'1000'});f.prima.monto='100';return f}
test('finca: mismo formato requerido para crear y editar prospecto o cliente',()=>{
  assert.equal(MAX_FINCA,8)
  for(const finca of ['12-12345','01-00001']) {
    const f=valido();f.datos.finca=finca;assert.equal(faltantesNuevo(f).finca,undefined)
    assert.equal(errorFincas([{id:1,numero:finca}]),undefined)
  }
  for(const finca of ['', '1-12345','12-123456','12-ABCDE','1212345']) {
    const f=valido();f.datos.finca=finca
    assert.ok(faltantesNuevo(f).finca)
    assert.ok(erroresDatos(f,false).finca)
    // El cliente no usa el campo único: su finca se valida en la lista.
    assert.equal(erroresDatos(f,true).finca,undefined)
    assert.ok(errorFincas([{id:1,numero:finca}]))
  }
})
test('fincas del cliente: al menos una, sin repetir y hasta 5',()=>{
  const lista=n=>Array.from({length:n},(_,i)=>({id:i+1,numero:'12-1234'+i}))
  assert.equal(errorFincas([]),'Ingrese al menos una finca.')
  assert.equal(errorFincas(lista(5)),undefined)
  assert.match(errorFincas(lista(6)),/máximo 5 fincas/)
  assert.equal(errorFincas([{id:1,numero:'12-12345'},{id:-1,numero:' 12-12345 '}]),'La finca ya está en la lista.')
  assert.equal(errorFincas([{id:1,numero:'12-12345'},{id:-1,numero:''}]),'Complete la finca o quite la fila vacía.')
})
test('expediente opcional: 2, 6 y 4 dígitos y dos letras, separados por guiones',()=>{
  assert.equal(MAX_EXPEDIENTE,17)
  for(const exp of ['', '12-123456-1234-AB','12-123456-1234-ab']) assert.equal(errorExpediente(exp),undefined)
  for(const exp of ['1-123456-1234-AB','12-12345-1234-AB','12-123456-123-AB','12-123456-1234-A1','12-123456-1234-ABC']) {
    assert.ok(errorExpediente(exp));const f=valido();f.datos.expediente=exp;assert.ok(erroresDatos(f,true).expediente)
  }
})
test('notas venta: 1000 caracteres se guardan, 1001 quedan inválidos también al crear',()=>{
  assert.equal(MAX_NOTAS_VENTA,1000)
  const f=valido();f.venta.notas='x'.repeat(1000);assert.equal(erroresVenta(f).notasVenta,undefined)
  assert.equal(faltantesNuevo(f).notasVenta,undefined)
  f.venta.notas+='x';assert.ok(erroresVenta(f).notasVenta);assert.ok(faltantesNuevo(f).notasVenta)
})
