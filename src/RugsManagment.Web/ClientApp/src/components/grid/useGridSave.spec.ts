import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/lib/api'
import { CostBasis, GridRowStatus, type GridBatchRequest, type GridColumn, type GridOpResult } from '@/lib/types/grid'
import {
  applyOpResult,
  buildOp,
  columnMap,
  emptyRow,
  keepMine,
  rowFromServer,
  takeTheirs,
  type GridRowState,
} from './gridModel'
import { retryDelay, useGridSave } from './useGridSave'

const col = (key: string, type: GridColumn['type'], extra: Partial<GridColumn> = {}): GridColumn => ({
  key,
  label: key,
  type,
  width: 8,
  sticky: false,
  readOnly: false,
  required: false,
  options: null,
  money: false,
  ...extra,
})

const COLUMNS = columnMap([
  col('rug.serial', 'serial'),
  col('rug.origin', 'combo'),
  col('rug.pattern', 'combo'),
  col('rug.intakeLength', 'dim', { required: true }),
  col('rug.intakeWidth', 'dim', { required: true }),
  col('purchase.price', 'money+unit', { money: true }),
  col('rug.areaM2', 'readonly', { readOnly: true }),
])

function serverRow(values: Record<string, unknown>, version = 7): GridRowState {
  return rowFromServer({ rugId: 'r1', version, sku: 'R-1', values: values as never, derived: {} }, COLUMNS)
}

describe('ساختن عمل ذخیره (G.6)', () => {
  it('ردیف تازه همهٔ خانه‌های پر را بی پایه می‌فرستد؛ خانهٔ خرید {rate, basis}', () => {
    const row = emptyRow({
      values: { 'rug.serial': '3778', 'rug.intakeLength': 2.08, 'rug.intakeWidth': 2.1, 'purchase.price': { rate: 9_000_000, basis: CostBasis.PerM2 } },
      dirty: ['rug.serial'],
    })
    const built = buildOp(row, COLUMNS)!
    expect(built.op.rugId).toBeNull()
    expect(built.op.clientRowId).toBe(row.id)
    expect(built.op.baseValues).toBeNull()
    expect(built.op.values).toEqual({
      'rug.serial': '3778',
      'rug.intakeLength': 2.08,
      'rug.intakeWidth': 2.1,
      'purchase.price': { rate: 9_000_000, basis: CostBasis.PerM2 },
    })
  })

  it('ردیف موجود فقط کلیدهای تغییرکرده را با مقدار پایه‌شان می‌فرستد', () => {
    const row = serverRow({ 'rug.origin': 'گبه', 'rug.pattern': 'ساده' })
    row.values['rug.origin'] = 'کاشان'
    row.dirty = ['rug.origin']
    const built = buildOp(row, COLUMNS)!
    expect(built.op).toMatchObject({ rugId: 'r1', version: 7, values: { 'rug.origin': 'کاشان' }, baseValues: { 'rug.origin': 'گبه' } })
  })

  it('پاک کردن خانهٔ خرید null می‌فرستد (حذف سطر)', () => {
    const row = serverRow({ 'purchase.price': { rate: 40_000_000, basis: CostBasis.Total } })
    row.values['purchase.price'] = null
    row.dirty = ['purchase.price']
    expect(buildOp(row, COLUMNS)!.op.values).toEqual({ 'purchase.price': null })
    expect(buildOp(row, COLUMNS)!.op.baseValues).toEqual({ 'purchase.price': { rate: 40_000_000, basis: CostBasis.Total } })
  })

  it('خانهٔ خرید چندسطری و ستون فقط‌خواندنی هرگز فرستاده نمی‌شوند', () => {
    const row = serverRow({ 'purchase.price': { rate: null, basis: 1, amount: 55_000_000, lines: 2 } })
    row.dirty = ['purchase.price', 'rug.areaM2']
    expect(buildOp(row, COLUMNS)).toBeNull()
  })

  it('ردیف خالیِ ته جدول چیزی نمی‌فرستد', () => {
    expect(buildOp(emptyRow({ placeholder: true, values: { 'rug.serial': '1' } }), COLUMNS)).toBeNull()
  })
})

describe('نشاندن نتیجه', () => {
  const result = (over: Partial<GridOpResult>): GridOpResult => ({
    clientRowId: 'x',
    rugId: 'r1',
    version: 8,
    sku: 'R-1',
    status: GridRowStatus.Saved,
    derived: null,
    errors: null,
    current: null,
    ...over,
  })

  it('ذخیره: نسخه و شناسه؛ کلیدی که در این فاصله دوباره عوض شده تغییرکرده می‌ماند', () => {
    const row = emptyRow({ values: { 'rug.serial': '3778', 'rug.origin': 'گبه' }, dirty: ['rug.serial', 'rug.origin'] })
    const sent = { 'rug.serial': '3778', 'rug.origin': 'گبه' }
    row.values['rug.origin'] = 'کاشان'
    expect(applyOpResult(row, result({ derived: { 'rug.areaM2': 4.37 } }), sent, COLUMNS)).toBe('saved')
    expect(row.rugId).toBe('r1')
    expect(row.version).toBe(8)
    expect(row.dirty).toEqual(['rug.origin'])
    expect(row.base['rug.origin']).toBe('گبه')
    expect(row.derived['rug.areaM2']).toBe(4.37)
    expect(row.status).toBe('dirty')
  })

  it('شمارهٔ خودکار سرور در خانهٔ شماره می‌نشیند', () => {
    const row = emptyRow({ values: { 'rug.intakeLength': 2 }, dirty: ['rug.intakeLength'] })
    applyOpResult(row, result({ derived: { 'rug.serial': '4004' } }), { 'rug.intakeLength': 2 }, COLUMNS)
    expect(row.values['rug.serial']).toBe('4004')
  })

  it('نادرست: خطا زیر همان خانه، خطای کلید ناشناخته خطای ردیف؛ nextFree', () => {
    const row = emptyRow({ values: { 'rug.serial': '3911' }, dirty: ['rug.serial'] })
    const outcome = applyOpResult(
      row,
      result({
        status: GridRowStatus.Invalid,
        rugId: null,
        errors: [
          { field: 'rug.serial', message: 'همپوشانی' },
          { field: 'rug.weird', message: 'دیگر' },
        ],
        derived: { nextFree: 4005 },
      }),
      { 'rug.serial': '3911' },
      COLUMNS,
    )
    expect(outcome).toBe('invalid')
    expect(row.errors['rug.serial']).toBe('همپوشانی')
    expect(row.rowError).toBe('دیگر')
    expect(row.nextFree).toBe(4005)
    expect(row.dirty).toEqual(['rug.serial'])
  })

  it('تعارض: خانهٔ دیگرِ تغییرکرده بی‌صدا ادغام، همان خانه تعارض', () => {
    const row = serverRow({ 'rug.origin': 'گبه', 'rug.pattern': 'ساده' })
    row.values['rug.origin'] = 'کاشان'
    row.dirty = ['rug.origin']
    const outcome = applyOpResult(
      row,
      result({ status: GridRowStatus.Conflict, version: 9, current: { 'rug.origin': 'تبریز', 'rug.pattern': 'لچک ترنج' } }),
      { 'rug.origin': 'کاشان' },
      COLUMNS,
    )
    expect(outcome).toBe('conflict')
    expect(row.values['rug.pattern']).toBe('لچک ترنج')
    expect(row.conflict?.fields).toEqual(['rug.origin'])
    expect(row.status).toBe('conflict')

    const mine = structuredClone(row)
    keepMine(mine)
    expect(mine.values['rug.origin']).toBe('کاشان')
    expect(mine.base['rug.origin']).toBe('تبریز')
    expect(mine.version).toBe(9)
    expect(mine.dirty).toEqual(['rug.origin'])

    takeTheirs(row)
    expect(row.values['rug.origin']).toBe('تبریز')
    expect(row.dirty).toEqual([])
    expect(row.status).toBe('clean')
  })

  it('تعارضی که فقط آن طرف عوض کرده تعارض نیست (دوباره فرستاده می‌شود)', () => {
    const row = serverRow({ 'rug.origin': 'گبه', 'rug.pattern': 'ساده' })
    row.values['rug.origin'] = 'کاشان'
    row.dirty = ['rug.origin']
    const outcome = applyOpResult(
      row,
      result({ status: GridRowStatus.Conflict, version: 9, current: { 'rug.origin': 'گبه', 'rug.pattern': 'لچک ترنج' } }),
      { 'rug.origin': 'کاشان' },
      COLUMNS,
    )
    expect(outcome).toBe('saved')
    expect(row.dirty).toEqual(['rug.origin'])
    expect(row.version).toBe(9)
    expect(row.values['rug.pattern']).toBe('لچک ترنج')
  })
})

describe('موتور ذخیره', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  function setup(send: (req: GridBatchRequest) => Promise<unknown>) {
    const rows: GridRowState[] = []
    const save = useGridSave({
      rows: () => rows,
      columns: () => COLUMNS,
      request: (ops) => ({ gridId: 'intake', batch: null, shipmentId: null, ops }),
      send,
      ready: (r) => r.rugId !== null || (r.values['rug.intakeLength'] != null && r.values['rug.intakeWidth'] != null),
    })
    return { rows, save }
  }

  const fresh = (serial: string) =>
    emptyRow({
      values: { 'rug.serial': serial, 'rug.intakeLength': 2, 'rug.intakeWidth': 3 },
      dirty: ['rug.serial', 'rug.intakeLength', 'rug.intakeWidth'],
      status: 'dirty',
    })

  it('does not resend rejected rows until explicit retry or editing', async () => {
    const send = vi.fn(async (req: GridBatchRequest) => req.ops.map((op) => ({
      clientRowId: op.clientRowId, rugId: null, version: null, sku: null,
      status: GridRowStatus.Invalid, derived: null, errors: [{ field: 'rug.serial', message: 'invalid' }], current: null,
    })))
    const { rows, save } = setup(send)
    rows.push(fresh('1'))
    await save.flush()
    await vi.advanceTimersByTimeAsync(60000)
    expect(send).toHaveBeenCalledTimes(1)
    await save.retryNow()
    expect(send).toHaveBeenCalledTimes(2)
    save.dispose()
  })

  it('۱٫۵ ثانیه پس از آخرین تغییر، همه در یک درخواست', async () => {
    const send = vi.fn(async (req: GridBatchRequest) =>
      req.ops.map((op, i) => ({ clientRowId: op.clientRowId, rugId: `r${i}`, version: 1, sku: null, status: GridRowStatus.Saved, derived: null, errors: null, current: null })),
    )
    const { rows, save } = setup(send)
    rows.push(fresh('1'), fresh('2'))
    save.schedule()
    await vi.advanceTimersByTimeAsync(1499)
    expect(send).not.toHaveBeenCalled()
    await vi.advanceTimersByTimeAsync(1)
    expect(send).toHaveBeenCalledTimes(1)
    expect(send.mock.calls[0][0].ops).toHaveLength(2)
    expect(rows.every((r) => r.status === 'clean' && r.rugId)).toBe(true)
    expect(save.state.phase).toBe('saved')
  })

  it('ردیف تازهٔ ناقص فرستاده نمی‌شود', async () => {
    const send = vi.fn(async () => [])
    const { rows, save } = setup(send)
    rows.push(emptyRow({ values: { 'rug.serial': '9' }, dirty: ['rug.serial'] }))
    await save.flush()
    expect(send).not.toHaveBeenCalled()
  })

  it('قطع شبکه: دوباره پس از ۲، ۴، ۸، ۱۶ و ۳۰ ثانیه، با همان ClientRowId', async () => {
    let fail = 3
    const send = vi.fn(async (req: GridBatchRequest) => {
      if (fail-- > 0) throw new TypeError('Failed to fetch')
      return { results: req.ops.map((op) => ({ clientRowId: op.clientRowId, rugId: 'r', version: 1, sku: null, status: 0, derived: null, errors: null, current: null })) }
    })
    const { rows, save } = setup(send)
    rows.push(fresh('1'))
    await save.flush()
    expect(save.state.phase).toBe('retrying')
    expect(save.state.retryIn).toBe(2)
    await vi.advanceTimersByTimeAsync(2000)
    expect(save.state.retryIn).toBe(4)
    await vi.advanceTimersByTimeAsync(4000)
    expect(save.state.retryIn).toBe(8)
    await vi.advanceTimersByTimeAsync(8000)
    expect(send).toHaveBeenCalledTimes(4)
    const ids = new Set(send.mock.calls.map((c) => c[0].ops[0].clientRowId))
    expect(ids.size).toBe(1)
    expect(rows[0].status).toBe('clean')
    expect(retryDelay(5)).toBe(30)
    expect(retryDelay(9)).toBe(30)
  })

  it('«الان دوباره بفرست» صبر نمی‌کند', async () => {
    let fail = true
    const send = vi.fn(async (req: GridBatchRequest) => {
      if (fail) throw new TypeError('offline')
      return req.ops.map((op) => ({ clientRowId: op.clientRowId, rugId: 'r', version: 1, sku: null, status: 0, derived: null, errors: null, current: null }))
    })
    const { rows, save } = setup(send)
    rows.push(fresh('1'))
    await save.flush()
    fail = false
    await save.retryNow()
    expect(send).toHaveBeenCalledTimes(2)
    expect(rows[0].status).toBe('clean')
  })

  it('۴۰۱ (نشست تمام شد) خودبه‌خود دوباره فرستاده نمی‌شود؛ ردیف ذخیره‌نشده می‌ماند و تغییر بعدی دوباره امتحان می‌کند', async () => {
    let expired = true
    const send = vi.fn(async (req: GridBatchRequest) => {
      if (expired) throw new ApiError(401, 'نشست شما تمام شده؛ صفحه را تازه کنید و دوباره وارد شوید.', null)
      return req.ops.map((op) => ({ clientRowId: op.clientRowId, rugId: 'r', version: 1, sku: null, status: 0, derived: null, errors: null, current: null }))
    })
    const onRequestError = vi.fn()
    const rows: GridRowState[] = [fresh('1')]
    const save = useGridSave({
      rows: () => rows,
      columns: () => COLUMNS,
      request: (ops) => ({ gridId: 'intake', batch: null, shipmentId: null, ops }),
      send,
      ready: () => true,
      onRequestError,
    })
    save.schedule()
    await vi.advanceTimersByTimeAsync(1500)
    expect(send).toHaveBeenCalledTimes(1)
    // پیش‌تر هر ۱٫۵ ثانیه دوباره می‌رفت و هر بار یک پیام تازه می‌ساخت
    await vi.advanceTimersByTimeAsync(60_000)
    expect(send).toHaveBeenCalledTimes(1)
    expect(onRequestError).toHaveBeenCalledTimes(1)
    expect(save.state.phase).toBe('error')
    expect(rows[0].status).not.toBe('clean')

    expired = false
    save.schedule()
    await vi.advanceTimersByTimeAsync(1500)
    expect(send).toHaveBeenCalledTimes(2)
    expect(rows[0].status).toBe('clean')
  })

  it('۴۰۹ کل درخواست با چند ردیف: ردیف‌ها یکی‌یکی فرستاده می‌شوند تا خراب جدا شود', async () => {
    const send = vi.fn(async (req: GridBatchRequest) => {
      if (req.ops.some((op) => op.values['rug.serial'] === '3950 - 3970')) {
        throw new ApiError(409, 'شمارهٔ ۳۹۵۰ داخل دستهٔ ۳۹۱۱ تا ۳۹۶۰ است.', { message: 'x', field: 'rug.serial', nextFree: 3961 })
      }
      return req.ops.map((op) => ({ clientRowId: op.clientRowId, rugId: 'ok', version: 1, sku: null, status: 0, derived: null, errors: null, current: null }))
    })
    const { rows, save } = setup(send)
    rows.push(fresh('3778'), fresh('3950 - 3970'))
    await save.flush()
    expect(send).toHaveBeenCalledTimes(3)
    expect(rows[0].status).toBe('clean')
    expect(rows[1].status).toBe('invalid')
    expect(rows[1].errors['rug.serial']).toContain('۳۹۵۰')
    expect(rows[1].nextFree).toBe(3961)
  })
})
