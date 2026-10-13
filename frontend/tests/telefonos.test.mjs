import test from 'node:test'
import assert from 'node:assert/strict'
import { errorTelefono } from '../src/utils/telefonos.ts'
import { formVacio, erroresDatos, errorNumeros, errorFamiliares, faltantesNuevo } from '../src/pages/persona/formulario.ts'

const paises=[{id:17,codigo:'+506'},{id:42,codigo:'+1'},{id:99,codigo:'+44'}]
test('longitud local por país, sin depender de ids y sin sumar el prefijo',()=>{
  for(const [id,largo] of [[17,8],[42,10]]) {
    assert.equal(errorTelefono('8'.repeat(largo),id,paises),undefined)
    assert.match(errorTelefono('8'.repeat(largo-1),id,paises),new RegExp(`${largo} dígitos`))
    assert.match(errorTelefono('8'.repeat(largo+1),id,paises),new RegExp(`${largo} dígitos`))
    assert.equal(errorTelefono('',id,paises),undefined)
  }
  assert.equal(errorTelefono('88887777',null,paises),undefined)
  assert.match(errorTelefono('50688887777',17,paises),/8 dígitos/)
  assert.match(errorTelefono('13055550100',42,paises),/10 dígitos/)
  assert.equal(errorTelefono('123456789',99,paises),undefined)
})
test('prospecto, contactos y familiares invalidan el borrador al cambiar país o longitud',()=>{
  const f=formVacio();Object.assign(f.datos,{telefonoPrincipal:'8888777',telefonoPrincipalPaisId:'17'})
  assert.match(erroresDatos(f,false,paises).telefonoPrincipal,/8 dígitos/)
  assert.match(faltantesNuevo(f,paises).telefonoPrincipal,/8 dígitos/)
  f.datos.telefonoPrincipal='88887777';f.datos.whatsappPrincipal='305555010';f.datos.whatsappPrincipalPaisId='42'
  assert.match(erroresDatos(f,false,paises).whatsappPrincipal,/10 dígitos/)
  assert.match(errorNumeros([{numero:'88887777',tipo:'WhatsApp',paisId:42}],paises),/10 dígitos/)
  const familiar={id:-1,nombreCompleto:'Luis',telefono:'8888777',telefonoPaisId:17,correoElectronico:'luis@mail.cr',whatsapp:'',whatsappPaisId:null}
  assert.match(errorFamiliares([familiar],paises),/8 dígitos/)
  assert.equal(errorFamiliares([{...familiar,telefono:''}],paises),undefined,'Con correo, teléfono vacío sigue siendo opcional')
  assert.match(errorFamiliares([{...familiar,telefono:'88887777',whatsapp:'305555010',whatsappPaisId:42}],paises),/10 dígitos/)
})
