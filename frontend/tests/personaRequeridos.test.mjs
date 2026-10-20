import { readFile } from 'node:fs/promises'
import test from 'node:test'
import assert from 'node:assert/strict'
import { JSDOM } from 'jsdom'
import { createServer } from 'vite'

// Carga los componentes reales de TSX con Vite; la API se simula sin tocar el servidor.
test('ficha real: rojo en cliente, cancela correo pendiente y reanuda al corregir', async () => {
  const dom=new JSDOM('<div id="root"></div>',{url:'http://localhost'})
  globalThis.window=dom.window;globalThis.document=dom.window.document
  globalThis.sessionStorage=dom.window.sessionStorage;globalThis.IS_REACT_ACT_ENVIRONMENT=true
  sessionStorage.setItem('gj.token','prueba')
  const vite=await createServer({server:{middlewareMode:true},appType:'custom'})
  let root
  try {
    const {createElement,act}=await import('react')
    const {createRoot}=await import('react-dom/client')
    const {createMemoryRouter,RouterProvider}=await import('react-router')
    const {PersonaPage}=await vite.ssrLoadModule('/src/pages/persona/PersonaPage.tsx')
    const {SesionProvider}=await vite.ssrLoadModule('/src/auth/Sesion.tsx')
    const {api}=await vite.ssrLoadModule('/src/api/endpoints.ts')
    const cat={id:1,nombre:'Opción',codigo:'1'}
    api.yo=async()=>({id:1,nombre:'Admin',rol:'Administrador'})
    api.catalogos=async()=>({vendedores:[cat],procedencias:[cat],metodos:[cat],estadosCliente:[cat],origenes:[cat],estadosPrima:[{id:1,nombre:'Pendiente'}],paises:[{id:1,codigo:'+506',nombre:'Costa Rica'},{id:2,codigo:'+1',nombre:'Estados Unidos'}],parentescos:[]})
    api.persona=async()=>({id:1,esCliente:true,nombres:'Ana',apellidos:'Vargas',cedula:'123',finca:'01-20013',expediente:'',fechaIngreso:'2026-10-13',vendedorId:1,telefonoPrincipal:'88888888',whatsappPrincipal:'',correoPrincipal:'',venta:{id:1,procedenciaVentaId:1,metodoVentaId:1,monto:1000,notas:''},prima:{id:1,monto:100,montoCancelado:0,fechaEstimadaPago:null,fechaPago:null},cliente:{id:1,estadoClienteId:1,origenClienteId:1},numeros:[{id:1,numero:'88888888',tipo:'Teléfono',principal:true,paisId:1}],correos:[{id:1,correo:'ana@mail.cr',principal:true}],familiares:[],comentarios:[],versiones:{datos:'d1',venta:'v1',prima:'p1',familiares:'f1'}})
    const enviados=[]
    api.guardarDatos=async(_id,d)=>{enviados.push(['datos',d]);return {data:null,version:'d2'}}
    api.guardarNumeros=async(_id,n)=>{enviados.push(['numeros',n]);return {data:n,version:'d4'}}
    api.guardarCorreos=async(_id,c)=>{enviados.push(['correos',c]);return {data:c,version:'d3'}}
    const router=createMemoryRouter([{path:'/gestion/clientes/:id',element:createElement(SesionProvider,null,createElement(PersonaPage,{seccion:'clientes'}))}],{initialEntries:['/gestion/clientes/1']})
    root=createRoot(document.getElementById('root'))
    await act(async()=>{root.render(createElement(RouterProvider,{router}));await new Promise(r=>setTimeout(r,20))})
    const input=label=>[...document.querySelectorAll('label')].find(l=>l.querySelector('.etiqueta')?.textContent.startsWith(label))?.querySelector('input')
    const props=el=>el[Object.keys(el).find(k=>k.startsWith('__reactProps'))]
    assert.equal(input('Número de finca').placeholder,'')
    assert.equal(input('Número de expediente').placeholder,'')
    assert.equal(input('Número de expediente').maxLength,17)
    const boton=texto=>[...document.querySelectorAll('button')].find(b=>b.textContent.trim()===texto)
    await act(async()=>props(input('Número de finca')).onChange({target:{value:'12-123456'}}))
    assert.ok(input('Número de finca').classList.contains('error'))
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,0,'Una finca con formato incorrecto no se autoguarda')
    await act(async()=>props(input('Número de finca')).onChange({target:{value:'01-20013'}}))
    await act(async()=>props(input('Número de expediente')).onChange({target:{value:'12-123456-1234-A1'}}))
    assert.ok(input('Número de expediente').classList.contains('error'))
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,0,'Un expediente inválido cancela también el envío pendiente de datos')
    await act(async()=>props(input('Número de expediente')).onChange({target:{value:'12-123456-1234-AB'}}))
    await act(async()=>props(input('Nombres')).onChange({target:{value:''}}))
    assert.ok(input('Nombres').classList.contains('error'))
    assert.match(input('Nombres').closest('label').textContent,/Campo requerido/)
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,0)
    await act(async()=>props(input('Nombres')).onChange({target:{value:'Ana María'}}))
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,1);assert.equal(enviados[0][1].nombres,'Ana María')
    assert.ok(!input('Nombres').classList.contains('error'))
    let correo=document.querySelector('.fila-contacto input[type=email]')
    assert.ok(correo)
    await act(async()=>props(correo).onChange({target:{value:'nuevo@mail.cr'}}))
    correo=document.querySelector('.fila-contacto input[type=email]')
    await act(async()=>props(correo).onChange({target:{value:'nuevo@'}}))
    assert.ok(document.querySelector('.fila-contacto input[type=email]').classList.contains('error'))
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,1,'No envía el correo válido anterior después de invalidarlo')
    correo=document.querySelector('.fila-contacto input[type=email]')
    await act(async()=>props(correo).onChange({target:{value:'final@mail.cr'}}))
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,2);assert.equal(enviados[1][1][0].correo,'final@mail.cr')
    const numero=()=>document.querySelector('.fila-contacto input[inputmode="numeric"]')
    const paisNumero=()=>document.querySelector('.fila-contacto .telefono-pais')
    await act(async()=>props(numero()).onChange({target:{value:'8888777'}}))
    assert.ok(document.querySelector('.fila-contacto .telefono.error'))
    assert.match(document.body.textContent,/8 dígitos/)
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,2,'No guarda un número de Costa Rica incompleto')
    await act(async()=>props(paisNumero()).onChange({target:{value:'2'}}))
    assert.equal(document.querySelectorAll('.fila-contacto .telefono.error').length,0,'Otro país no exige longitud fija')
    await act(async()=>props(numero()).onChange({target:{value:'305555010012'}}))
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,3);assert.equal(enviados[2][1][0].paisId,2)
    await act(async()=>props(paisNumero()).onChange({target:{value:'1'}}))
    assert.match(document.body.textContent,/8 dígitos/)
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,3,'Cambiar país también bloquea el guardado si cambia la longitud requerida')
    await act(async()=>props(numero()).onChange({target:{value:'88887777'}}))
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,4)
    const antesNumeros=enviados.length
    await act(async()=>props(boton('+ Teléfono')).onClick())
    const ultimaFila=()=>[...document.querySelectorAll('.fila-contacto')].filter(f=>f.querySelector('input[inputmode="numeric"]')).at(-1)
    assert.equal(ultimaFila().querySelectorAll('.telefono.error').length,0,'Agregar teléfono no marca errores')
    assert.ok(ultimaFila().querySelector('.requerido'),'Solo el número tiene indicador requerido')
    assert.equal(document.querySelectorAll('.alerta').length,0)
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,antesNumeros,'La fila vacía no se envía')
    await act(async()=>props(ultimaFila().querySelector('input[inputmode="numeric"]')).onBlur())
    assert.ok(ultimaFila().querySelector('.telefono.error'),'Salir del número vacío muestra el error')
    const css=await readFile(new URL('../src/estilos.css',import.meta.url),'utf8')
    const estilo=document.createElement('style')
    estilo.textContent=css.split('\n').filter(l=>l.startsWith('.telefono-pais ') || l.startsWith('.telefono.error ')).join('\n')
      .replaceAll('var(--line)','rgb(204, 204, 204)').replaceAll('var(--rojo)','rgb(255, 0, 0)')
    document.head.append(estilo)
    assert.equal(window.getComputedStyle(ultimaFila().querySelector('input')).borderTopColor,'rgb(255, 0, 0)')
    assert.equal(window.getComputedStyle(ultimaFila().querySelector('.telefono-pais')).borderTopColor,'rgb(204, 204, 204)',
      'El selector de país conserva su borde normal cuando falta el número')
    await act(async()=>props(boton('+ WhatsApp')).onClick())
    assert.equal(ultimaFila().querySelectorAll('.telefono.error').length,0,'Agregar WhatsApp tampoco marca errores')
    assert.ok(ultimaFila().querySelector('.requerido'))
    const filas=[...document.querySelectorAll('.fila-contacto')].filter(f=>f.querySelector('input[inputmode="numeric"]'))
    await act(async()=>props(filas[1].querySelector('.btn-icono')).onClick())
    await act(async()=>props(ultimaFila().querySelector('.btn-icono')).onClick())
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.ok(enviados.slice(antesNumeros).every(([,n])=>n.every(x=>x.numero)), 'Quitar las filas vacías conserva solo números completos')
    const antesFamiliares=enviados.length
    api.guardarFamiliares=async(_id,fs)=>{enviados.push(['familiares',fs]);return {data:fs.map((f,i)=>({...f,id:i+1})),version:'f2'}}
    await act(async()=>props(boton('Familiares')).onClick())
    await act(async()=>props(boton('+ Agregar familiar')).onClick())
    assert.equal(document.querySelectorAll('.familiar .error').length,0,'Una fila nueva no empieza en rojo')
    assert.equal(document.querySelectorAll('[role=alert], .alerta, .familiar .msg-error').length,0,'Agregar familiar no muestra avisos preventivos')
    assert.ok(document.querySelector('.familiar .requerido'))
    assert.match(document.body.textContent,/al menos un teléfono o un correo por familiar/)
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,antesFamiliares,'La fila vacía permanece pendiente y no se guarda')
    assert.equal(document.querySelectorAll('.alerta').length,0,'Esperar el autoguardado de una fila nueva tampoco muestra avisos')
    await act(async()=>props(document.querySelector('.familiar input[aria-label="Nombre completo"]')).onChange({target:{value:'Luis'}}))
    assert.equal(document.querySelectorAll('.familiar .error').length,0,'No muestra errores en contactos aún no editados')
    await act(async()=>props(document.querySelector('.volver')).onClick())
    assert.ok(document.querySelector('.familiar .telefono.error'),'Intentar salir muestra el contacto pendiente')
    assert.equal(window.getComputedStyle(document.querySelector('.familiar .telefono-pais')).borderTopColor,'rgb(204, 204, 204)',
      'El país del familiar tampoco se marca en rojo cuando falta el teléfono')
    assert.ok(document.querySelector('[role=alert]'),'La validación sí avisa cuando se intenta salir sin completar')
    await act(async()=>props(document.querySelector('.familiar input[aria-label="Teléfono"]')).onChange({target:{value:'88887777'}}))
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,antesFamiliares+1)
    assert.equal(enviados[antesFamiliares][0],'familiares');assert.equal(enviados[antesFamiliares][1][0].telefono,'88887777')
    assert.equal(document.querySelectorAll('.familiar .error').length,0)
    await act(async()=>props(boton('+ Agregar familiar')).onClick())
    assert.equal(document.querySelectorAll('.alerta, .familiar .error, .familiar .msg-error').length,0,
      'Agregar otra fila después de corregir un intento anterior vuelve a empezar sin avisos')
    api.guardarVenta=async(_id,v)=>{enviados.push(['venta',v]);return {data:v,version:'v2'}}
    await act(async()=>props(boton('Ventas')).onClick())
    const notas=()=>document.querySelector('textarea.nota')
    assert.equal(notas().maxLength,1000)
    const antesNotas=enviados.length
    await act(async()=>props(notas()).onChange({target:{value:'x'.repeat(1001)}}))
    assert.match(document.body.textContent,/Máximo 1000 caracteres/)
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,antesNotas,'Notas excesivas no se autoguardan')
    await act(async()=>props(notas()).onChange({target:{value:'x'.repeat(1000)}}))
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,antesNotas+1)
    assert.equal(enviados.at(-1)[1].notas.length,1000)



  } finally {
    if(root) {const {act}=await import('react');await act(async()=>root.unmount())}
    await vite.close();dom.window.close()
  }
})




