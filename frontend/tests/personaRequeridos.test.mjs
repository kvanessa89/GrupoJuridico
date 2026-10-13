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
    api.catalogos=async()=>({vendedores:[cat],procedencias:[cat],metodos:[cat],estadosCliente:[cat],origenes:[cat],estadosPrima:[{id:1,nombre:'Pendiente'}],paises:[{id:1,codigo:'+506',nombre:'Costa Rica'}],parentescos:[]})
    api.persona=async()=>({id:1,esCliente:true,nombres:'Ana',apellidos:'Vargas',cedula:'123',finca:'Finca',expediente:'',fechaIngreso:'2026-10-13',vendedorId:1,telefonoPrincipal:'88888888',whatsappPrincipal:'',correoPrincipal:'',venta:{id:1,procedenciaVentaId:1,metodoVentaId:1,monto:1000,notas:''},prima:{id:1,monto:100,montoCancelado:0,fechaEstimadaPago:null,fechaPago:null},cliente:{id:1,estadoClienteId:1,origenClienteId:1},numeros:[{id:1,numero:'88888888',tipo:'Teléfono',principal:true,paisId:1}],correos:[{id:1,correo:'ana@mail.cr',principal:true}],familiares:[],comentarios:[],versiones:{datos:'d1',venta:'v1',prima:'p1',familiares:'f1'}})
    const enviados=[]
    api.guardarDatos=async(_id,d)=>{enviados.push(['datos',d]);return {data:null,version:'d2'}}
    api.guardarCorreos=async(_id,c)=>{enviados.push(['correos',c]);return {data:c,version:'d3'}}
    const router=createMemoryRouter([{path:'/gestion/clientes/:id',element:createElement(SesionProvider,null,createElement(PersonaPage,{seccion:'clientes'}))}],{initialEntries:['/gestion/clientes/1']})
    root=createRoot(document.getElementById('root'))
    await act(async()=>{root.render(createElement(RouterProvider,{router}));await new Promise(r=>setTimeout(r,20))})
    const input=label=>[...document.querySelectorAll('label')].find(l=>l.querySelector('.etiqueta')?.textContent.startsWith(label))?.querySelector('input')
    const props=el=>el[Object.keys(el).find(k=>k.startsWith('__reactProps'))]
    await act(async()=>props(input('Nombres')).onChange({target:{value:''}}))
    assert.ok(input('Nombres').classList.contains('error'))
    assert.match(document.body.textContent,/Cambios sin guardar/)
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
    api.guardarFamiliares=async(_id,fs)=>{enviados.push(['familiares',fs]);return {data:fs.map((f,i)=>({...f,id:i+1})),version:'f2'}}
    const boton=texto=>[...document.querySelectorAll('button')].find(b=>b.textContent.trim()===texto)
    await act(async()=>props(boton('Familiares')).onClick())
    await act(async()=>props(boton('+ Agregar familiar')).onClick())
    assert.equal(document.querySelectorAll('.familiar .error').length,0,'Una fila nueva no empieza en rojo')
    assert.ok(document.querySelector('.familiar .requerido'))
    assert.match(document.body.textContent,/al menos un teléfono o un correo por familiar/)
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,2,'La fila vacía permanece pendiente y no se guarda')
    await act(async()=>props(document.querySelector('.familiar input[aria-label="Nombre completo"]')).onChange({target:{value:'Luis'}}))
    assert.equal(document.querySelectorAll('.familiar .error').length,0,'No muestra errores en contactos aún no editados')
    await act(async()=>props(document.querySelector('.volver')).onClick())
    assert.ok(document.querySelector('.familiar .telefono.error'),'Intentar salir muestra el contacto pendiente')
    await act(async()=>props(document.querySelector('.familiar input[aria-label="Teléfono"]')).onChange({target:{value:'88887777'}}))
    await act(async()=>{await new Promise(r=>setTimeout(r,650))})
    assert.equal(enviados.length,3)
    assert.equal(enviados[2][0],'familiares');assert.equal(enviados[2][1][0].telefono,'88887777')
    assert.equal(document.querySelectorAll('.familiar .error').length,0)

  } finally {
    if(root) {const {act}=await import('react');await act(async()=>root.unmount())}
    await vite.close();dom.window.close()
  }
})

