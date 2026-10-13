import test from 'node:test'
import assert from 'node:assert/strict'
import { limiteNumeros, limiteCorreos, limiteFamiliares, MAX_TELEFONOS, MAX_WHATSAPP, MAX_CORREOS, MAX_FAMILIARES } from '../src/pages/persona/limitesContactos.ts'

test('acepta 2 teléfonos y 2 WhatsApp juntos y rechaza un tercero de cualquier tipo', () => {
  const n = [{ tipo: 'Teléfono' }, { tipo: 'Teléfono' }, { tipo: 'WhatsApp' }, { tipo: 'WhatsApp' }]
  assert.equal(limiteNumeros(n), undefined)
  assert.match(limiteNumeros([...n, { tipo: 'Teléfono' }]), /2 teléfonos/)
  assert.match(limiteNumeros([...n, { tipo: 'WhatsApp' }]), /2 números de WhatsApp/)
  assert.equal(MAX_TELEFONOS, 2); assert.equal(MAX_WHATSAPP, 2)
})
test('convertir teléfono a WhatsApp no puede superar el cupo del tipo de destino', () => {
  const n = [{ tipo: 'Teléfono' }, { tipo: 'WhatsApp' }, { tipo: 'WhatsApp' }]
  assert.match(limiteNumeros(n.map((x, i) => i === 0 ? { tipo: 'WhatsApp' } : x)), /WhatsApp/)
  assert.equal(limiteNumeros(n), undefined)
})
test('correos y familiares aceptan el límite y rechazan una fila más, incluso si está vacía', () => {
  assert.equal(MAX_CORREOS, 3); assert.equal(MAX_FAMILIARES, 4)
  assert.equal(limiteCorreos(Array(3).fill({})), undefined)
  assert.match(limiteCorreos(Array(4).fill({})), /3 correos/)
  assert.equal(limiteFamiliares(Array(4).fill({})), undefined)
  assert.match(limiteFamiliares(Array(5).fill({})), /4 familiares/)
})
test('se puede reducir una lista antigua excesiva hasta cumplir los límites', () => {
  const antiguos = Array(4).fill({ tipo: 'Teléfono' })
  assert.match(limiteNumeros(antiguos), /2 teléfonos/)
  assert.equal(limiteNumeros(antiguos.slice(0, 2)), undefined)
  assert.equal(limiteCorreos([]), undefined)
  assert.equal(limiteFamiliares([]), undefined)
})
