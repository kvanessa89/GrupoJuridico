import test from 'node:test'
import assert from 'node:assert/strict'
import { JSDOM } from 'jsdom'
import { createServer } from 'vite'

// Salir de la ficha por el menú o el Atrás siempre intenta "guardar y salir", sin aviso intermedio.
test('salir de la ficha: se queda con campos inválidos o error del servidor; si no, guarda y sale', async () => {
  const dom=new JSDOM('<div id="root"></div>',{url:'http://localhost'})
  globalThis.window=dom.window;globalThis.document=dom.window.document
  globalThis.sessionStorage=dom.window.sessionStorage;globalThis.IS_REACT_ACT_ENVIRONMENT=true
  window.scrollTo=()=>{}
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
    api.catalogos=async()=>({vendedores:[cat],procedencias:[cat],metodos:[cat],estadosCliente:[cat],estadosClienteCalculado:[cat],origenes:[cat],estadosPrima:[{id:1,nombre:'Pendiente'}],paises:[{id:1,codigo:'+506',nombre:'Costa Rica'}],parentescos:[]})
    api.persona=async()=>({id:1,esCliente:false,nombres:'Ana',apellidos:'Vargas',cedula:'123',finca:'01-20013',expediente:'',fechaIngreso:'2026-10-13',vendedorId:1,telefonoPrincipal:'88888888',telefonoPrincipalPaisId:1,whatsappPrincipal:'',correoPrincipal:'',venta:{id:1,procedenciaVentaId:1,metodoVentaId:1,monto:1000,notas:''},prima:{id:1,monto:100,montoCancelado:0,fechaEstimadaPago:null,fechaPago:null},cliente:null,numeros:[],correos:[],familiares:[],comentarios:[],versiones:{datos:'d1',venta:'v1',prima:'p1',familiares:'f1'}})
    // El guardado de datos responde cuando el test lo libera, para ver el "Guardando…".
    const enviados=[];let liberar;let fallar=false
    api.guardarDatos=(_id,d)=>new Promise((ok,mal)=>{enviados.push(d);liberar=()=>fallar?mal(new Error('Falla del servidor')):ok({data:null,version:'d'+(enviados.length+1)})})
    const router=createMemoryRouter([
      {path:'/gestion/ventas/:id',element:createElement(SesionProvider,null,createElement(PersonaPage,{seccion:'ventas'}))},
      {path:'/gestion/clientes',element:createElement('div',{id:'otra'},'Otra pantalla')},
    ],{initialEntries:['/gestion/ventas/1']})
    root=createRoot(document.getElementById('root'))
    await act(async()=>{root.render(createElement(RouterProvider,{router}));await new Promise(r=>setTimeout(r,20))})
    const input=label=>[...document.querySelectorAll('label')].find(l=>l.querySelector('.etiqueta')?.textContent.startsWith(label))?.querySelector('input')
    const props=el=>el[Object.keys(el).find(k=>k.startsWith('__reactProps'))]
    const texto=()=>document.body.textContent
    const irAlMenu=()=>act(async()=>{router.navigate('/gestion/clientes');await new Promise(r=>setTimeout(r,20))})

    // 1. Campo obligatorio vacío: no sale y señala el campo; ya no hay aviso intermedio.
    await act(async()=>props(input('Nombres')).onChange({target:{value:''}}))
    await irAlMenu()
    assert.equal(router.state.location.pathname,'/gestion/ventas/1')
    assert.match(texto(),/Cambio pendiente: Nombres, campo requerido\./)
    assert.doesNotMatch(texto(),/Hay cambios sin guardar/)
    assert.equal(enviados.length,0)

    // 2. Error del servidor al guardar: muestra "Guardando…", luego se queda con el error.
    await act(async()=>props(input('Nombres')).onChange({target:{value:'Ana María'}}))
    fallar=true
    await irAlMenu()
    assert.match(texto(),/Guardando…/)
    await act(async()=>{liberar();await new Promise(r=>setTimeout(r,20))})
    assert.equal(router.state.location.pathname,'/gestion/ventas/1')
    assert.doesNotMatch(texto(),/Guardando…/)
    assert.match(texto(),/Falla del servidor/)

    // 3. Sin errores: "Reintentar guardado" reanuda; al salir guarda lo recién escrito y sale.
    fallar=false
    const reintentar=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Reintentar guardado')
    await act(async()=>{props(reintentar).onClick();await new Promise(r=>setTimeout(r,20))})
    await act(async()=>{liberar();await new Promise(r=>setTimeout(r,20))})
    await act(async()=>props(input('Apellidos')).onChange({target:{value:'Vargas Mora'}}))
    await irAlMenu()
    assert.match(texto(),/Guardando…/)
    await act(async()=>{liberar();await new Promise(r=>setTimeout(r,20))})
    assert.equal(router.state.location.pathname,'/gestion/clientes')
    assert.equal(enviados.at(-1).apellidos,'Vargas Mora')
  } finally {
    if(root) {const {act}=await import('react');await act(async()=>root.unmount())}
    await vite.close();dom.window.close()
  }
})
