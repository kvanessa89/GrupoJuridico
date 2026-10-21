import test from 'node:test'
import assert from 'node:assert/strict'
import { agruparArchivos, fTamano } from '../src/pages/persona/archivosUtil.ts'

const tipos = [{ id: 1, nombre: 'Poderes' }, { id: 2, nombre: 'Contrato' }, { id: 3, nombre: 'Comprobantes de pago' }]
const archivo = (id, tipoDocumentoId) => ({ id, tipoDocumentoId, nombre: 'a' + id + '.pdf', tamano: 10, fechaCarga: '2026-10-20', puedeEliminar: false })

test('los archivos se agrupan por tipo en el orden del catálogo y sin grupos vacíos', () => {
  const grupos = agruparArchivos([archivo(1, 2), archivo(2, 1), archivo(3, 2)], tipos)
  assert.deepEqual(grupos.map((g) => g.tipo), ['Poderes', 'Contrato'])
  assert.deepEqual(grupos[1].archivos.map((a) => a.id), [1, 3])
  assert.deepEqual(agruparArchivos([], tipos), [])
})

test('un archivo de un tipo que no está en el catálogo cargado se muestra en Otros', () => {
  const grupos = agruparArchivos([archivo(1, 9)], tipos)
  assert.equal(grupos.length, 1)
  assert.equal(grupos[0].tipo, 'Otros')
})

test('el tamaño se muestra en B, KB o MB', () => {
  assert.equal(fTamano(512), '512 B')
  assert.equal(fTamano(2048), '2 KB')
  assert.equal(fTamano(1572864), '1.5 MB')
  assert.equal(fTamano(10 * 1024 * 1024), '10 MB')
})
