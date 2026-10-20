import test from 'node:test'
import assert from 'node:assert/strict'
import { fechaDia, montoMensual, planValido, primeraCuota, promesaVisible, saldoParaPago, textoPromesas } from '../src/pages/cobros/reglas.ts'
import { fDiaLargo, plural } from '../src/utils/formato.ts'

test('el día de pago se ajusta al último día en meses cortos y pasa de año', () => {
  assert.equal(fechaDia(2027, 2, 30), '2027-02-28')
  assert.equal(fechaDia(2026, 13, 15), '2027-01-15')
  assert.equal(fechaDia(2028, 2, 31), '2028-02-29')
})

test('la primera cuota es el día de pago del mes de inicio o del siguiente si ya pasó', () => {
  assert.equal(primeraCuota('2026-01-10', 15), '2026-01-15')
  assert.equal(primeraCuota('2026-01-15', 15), '2026-01-15')
  assert.equal(primeraCuota('2026-12-31', 30), '2027-01-30')
})

test('el formulario del plan exige monto, 1 a 120 cuotas, fecha y día entre 1 y 31', () => {
  assert.deepEqual(planValido({ total: '1000', cuotas: '3', inicio: '2026-10-15', dia: '15' }), { total: 1000, cuotas: 3, inicio: '2026-10-15', dia: 15 })
  assert.equal(planValido({ total: '', cuotas: '3', inicio: '2026-10-15', dia: '15' }), null)
  assert.equal(planValido({ total: '1000', cuotas: '121', inicio: '2026-10-15', dia: '15' }), null)
  assert.equal(planValido({ total: '1000', cuotas: '2.5', inicio: '2026-10-15', dia: '15' }), null)
  assert.equal(planValido({ total: '1000', cuotas: '3', inicio: '', dia: '15' }), null)
  assert.equal(planValido({ total: '1000', cuotas: '3', inicio: '2026-10-15', dia: '32' }), null)
  assert.equal(montoMensual(1000, 3), 333)
})

test('textos del plan', () => {
  assert.equal(textoPromesas({ promesasPendientes: 0, promesasCumplidas: 0, promesasIncumplidas: 0 }), 'Sin promesas de pago')
  assert.equal(textoPromesas({ promesasPendientes: 1, promesasCumplidas: 2, promesasIncumplidas: 0 }), 'Promesas: 1 pendiente, 2 cumplidas')
  assert.equal(plural(1, 'día'), '1 día')
  assert.equal(plural(3, 'día'), '3 días')
  assert.equal(fDiaLargo('2026-10-17'), 'sábado 17 oct')
})

test('el saldo del pago suma el interés solo si se cobra', () => {
  const c = { montoCuota: 300000, moraPosible: 7500, pagado: 100000 }
  assert.equal(saldoParaPago(c, true), 207500)
  assert.equal(saldoParaPago(c, false), 200000)
  assert.equal(saldoParaPago({ ...c, pagado: 400000 }, true), 0)
})

test('se muestra la promesa pendiente o la última', () => {
  assert.equal(promesaVisible([{ estadoId: 3, n: 1 }, { estadoId: 1, n: 2 }, { estadoId: 2, n: 3 }]).n, 2)
  assert.equal(promesaVisible([{ estadoId: 3, n: 1 }, { estadoId: 2, n: 3 }]).n, 3)
  assert.equal(promesaVisible([]), undefined)
})
