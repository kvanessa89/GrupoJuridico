import test from 'node:test'
import assert from 'node:assert/strict'
import { fMonto, fNumero, formatearMonto, limpiarMonto } from '../src/utils/formato.ts'

test('el monto solo admite dígitos y un punto, con hasta 2 decimales', () => {
  assert.equal(limpiarMonto('1,250,000.509'), '1250000.50')
  assert.equal(limpiarMonto('abc12x3'), '123')
  assert.equal(limpiarMonto('007'), '7')
  assert.equal(limpiarMonto('.5'), '0.5')
  assert.equal(limpiarMonto('1.2.'), '1.2')
  assert.equal(limpiarMonto('12.'), '12.')
  assert.equal(limpiarMonto(''), '')
})

test('el monto se muestra con coma de miles y punto decimal', () => {
  assert.equal(formatearMonto('1250000'), '1,250,000')
  assert.equal(formatearMonto('1250000.5'), '1,250,000.5')
  assert.equal(formatearMonto('999'), '999')
  assert.equal(formatearMonto('0.75'), '0.75')
})

test('los montos se muestran con coma de miles, punto decimal y 2 decimales solo si hay', () => {
  assert.equal(fMonto(1250000), '₡1,250,000')
  assert.equal(fMonto(1250000.5), '₡1,250,000.50')
  assert.equal(fMonto(0), '₡0')
  assert.equal(fMonto(null), '₡0')
  assert.equal(fNumero(1250000.5), '1,250,000.50')
})
