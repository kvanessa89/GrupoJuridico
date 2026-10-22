import test from 'node:test'
import assert from 'node:assert/strict'
import { JSDOM } from 'jsdom'
import { createServer } from 'vite'

const respuesta = (cuerpo, ok = true) => async () => ({ ok, json: async () => cuerpo })

test('compara la versión publicada con la del build; los errores no avisan', async () => {
  const { hayVersionNueva } = await import('../src/hooks/useVersionNueva.ts')
  assert.equal(await hayVersionNueva('v1', respuesta({ version: 'v1' })), false)
  assert.equal(await hayVersionNueva('v1', respuesta({ version: 'v2' })), true)
  // En desarrollo (sin versión) nunca avisa.
  assert.equal(await hayVersionNueva('', respuesta({ version: 'v2' })), false)
  assert.equal(await hayVersionNueva('v1', respuesta({ version: 'v2' }, false)), false)
  assert.equal(await hayVersionNueva('v1', respuesta({})), false)
  assert.equal(await hayVersionNueva('v1', async () => { throw new Error('sin red') }), false)
})

test('con una versión nueva avisa y recarga al cambiar de pantalla; los filtros sobreviven a la recarga', async () => {
  const dom = new JSDOM('<div id="root"></div>', { url: 'http://localhost', pretendToBeVisual: true })
  globalThis.window = dom.window; globalThis.document = dom.window.document
  globalThis.sessionStorage = dom.window.sessionStorage; globalThis.IS_REACT_ACT_ENVIRONMENT = true
  const vite = await createServer({ server: { middlewareMode: true }, appType: 'custom' })
  let root
  try {
    const { createElement: h, act } = await import('react')
    const { createRoot } = await import('react-dom/client')
    const { createMemoryRouter, RouterProvider } = await vite.ssrLoadModule('react-router')
    const { useVersionNueva } = await vite.ssrLoadModule('/src/hooks/useVersionNueva.ts')
    const { FiltrosProvider, useFiltros, borrarFiltrosGuardados } = await vite.ssrLoadModule('/src/layout/Filtros.tsx')

    // Versión nueva: avisa sin recargar hasta que cambia la pantalla.
    let publicada = false
    let recargas = 0
    function Pantalla() {
      const nueva = useVersionNueva({ actual: 'v1', revisarVersion: async () => publicada, recargar: () => recargas++ })
      return h('p', null, nueva ? 'nueva' : 'igual')
    }
    const router = createMemoryRouter([{ path: '*', element: h(Pantalla) }], { initialEntries: ['/gestion/cobros'] })
    root = createRoot(document.getElementById('root'))
    await act(async () => root.render(h(RouterProvider, { router })))
    const volverALaPestana = () => act(async () => { document.dispatchEvent(new window.Event('visibilitychange')) })
    await volverALaPestana()
    assert.equal(document.body.textContent, 'igual')
    publicada = true
    await volverALaPestana()
    assert.equal(document.body.textContent, 'nueva')
    assert.equal(recargas, 0)
    await act(async () => router.navigate('/gestion/clientes'))
    assert.equal(recargas, 1)
    await act(async () => root.unmount())

    // Filtros: se guardan en la pestaña y vuelven después de recargar; al cerrar sesión se borran.
    let filtros
    function Lista() {
      filtros = useFiltros('clientes')
      return null
    }
    const montar = async () => {
      root = createRoot(document.getElementById('root'))
      await act(async () => root.render(h(FiltrosProvider, null, h(Lista))))
    }
    await montar()
    await act(async () => filtros.set({ q: 'vargas', estadoClienteId: 'c2' }))
    await act(async () => root.unmount())
    await montar()
    assert.equal(filtros.filtros.q, 'vargas')
    assert.equal(filtros.filtros.estadoClienteId, 'c2')
    await act(async () => root.unmount())
    borrarFiltrosGuardados()
    await montar()
    assert.equal(filtros.filtros.q, '')
    await act(async () => root.unmount())
    root = null
  } finally {
    if (root) root.unmount()
    await vite.close()
  }
})
