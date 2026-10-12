import test from 'node:test'
import assert from 'node:assert/strict'
import { JSDOM } from 'jsdom'
import { createElement, act } from 'react'
import { createRoot } from 'react-dom/client'
import { EditorSecciones } from '../src/hooks/EditorSecciones.ts'
import { useGuardadoDiferido } from '../src/hooks/useGuardadoDiferido.ts'

const versiones = { datos: 'd1', venta: 'v1', prima: 'p1', familiares: 'f1' }
const diferido = () => { let resolve, reject; const promise = new Promise((r,j) => { resolve=r; reject=j }); return { promise, resolve, reject } }

test('serializa datos y contactos; la segunda escritura usa la versión confirmada', async () => {
  const editor = new EditorSecciones(); editor.cargar(versiones)
  const vuelo = diferido(); const recibidas=[]
  const a = editor.guardar('datos', v => { recibidas.push(v); return vuelo.promise })
  const b = editor.guardar('datos', async v => { recibidas.push(v); return {data: 'b', version:'d3'} })
  await new Promise(r => setImmediate(r)); assert.deepEqual(recibidas,['d1'])
  vuelo.resolve({ data:'a',version:'d2' }); await Promise.all([a,b])
  assert.deepEqual(recibidas,['d1','d2'])
})
test('un conflicto bloquea solicitudes en cola pero permite otra sección', async () => {
  const editor = new EditorSecciones(); editor.cargar(versiones); let llamadas=0
  const a=editor.guardar('datos', async () => { throw { response:{status:409} } })
  const b=editor.guardar('datos', async () => { llamadas++; return {data:0,version:'d2'} })
  await Promise.all([assert.rejects(a),assert.rejects(b)]); assert.equal(llamadas,0)
  assert.equal(await editor.guardar('venta',async v => ({data:v,version:'v2'})),'v1')
  editor.resolver('datos','d9')
  assert.equal(await editor.guardar('datos',async v => ({data:v,version:'d10'})),'d9')
})

const dom = new JSDOM('<div id="root"></div>')
globalThis.window=dom.window; globalThis.document=dom.window.document; globalThis.IS_REACT_ACT_ENVIRONMENT=true
async function montar(guardar, alGuardar=()=>{}, alFallar=()=>{}) {
  let hook; const root=createRoot(document.getElementById('root'))
  function Ficha() { hook=useGuardadoDiferido(guardar,alGuardar,alFallar,100000); return null }
  await act(async()=>root.render(createElement(Ficha)))
  return {get hook(){return hook}, desmontar:()=>act(async()=>root.unmount())}
}
test('conserva el último borrador al fallar y flush no declara éxito', async () => {
  const vuelo=diferido(); const enviados=[]; let fallos=0
  const ficha=await montar(v=>{enviados.push(v); return enviados.length===1?vuelo.promise:Promise.resolve(v)},()=>{},()=>fallos++)
  ficha.hook.programar('primero'); const guardado=ficha.hook.flush()
  await new Promise(r=>setImmediate(r)); ficha.hook.programar('último')
  vuelo.reject(new Error('conflicto')); assert.equal(await guardado,false)
  assert.equal(fallos,1); assert.equal(ficha.hook.hayPendientes(),true)
  assert.equal(await ficha.hook.flush(),false); assert.deepEqual(enviados,['primero'])
  ficha.hook.reanudar(); assert.equal(await ficha.hook.flush(),true)
  assert.deepEqual(enviados,['primero','último']); await ficha.desmontar()
})
test('flush espera también cambios añadidos durante un guardado', async()=>{
  const vuelo=diferido(); const enviados=[]
  const ficha=await montar(v=>{enviados.push(v);return enviados.length===1?vuelo.promise:Promise.resolve(v)})
  ficha.hook.programar('a');const fin=ficha.hook.flush()
  await new Promise(r=>setImmediate(r));ficha.hook.programar('b');vuelo.resolve('a')
  assert.equal(await fin,true);assert.deepEqual(enviados,['a','b']);await ficha.desmontar()
})
test('desmontar o descartar no envía un borrador pendiente',async()=>{
  const enviados=[];const ficha=await montar(async v=>{enviados.push(v);return v})
  ficha.hook.programar('borrador');ficha.hook.cancelar();assert.equal(ficha.hook.hayPendientes(),false)
  ficha.hook.programar('otro');await ficha.desmontar();assert.equal(enviados.length,0)
})
