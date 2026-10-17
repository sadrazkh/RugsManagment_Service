import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/lib/api'
import { resetAppContext } from '@/lib/context'
import { draftKey } from '@/lib/gridDrafts'
import { settleConfirm } from '@/lib/ui'
import {
  CostBasis,
  GridRowStatus,
  type GridBatchRequest,
  type GridBatchResponse,
  type GridColumn,
  type GridData,
  type GridOp,
  type GridOpResult,
  type GridRow,
} from '@/lib/types/grid'
import { SerialMode } from '@/lib/types/platform'
import { useGridController, type GridControllerOptions } from './useGridController'

/**
 * کنترل‌گر جدول از دید کاربر (K.2/P3): تایپ فقط با صفحه‌کلید، شمارهٔ بعدی و دسته، خانه‌های چسبان، پرسش‌های سانت،
 * «۱۰م»، چسباندن با یک پرسش، برگرد، ذخیرهٔ دسته‌ای با ClientRowId، تعارض، قطع شبکه، پیش‌نویس و حذف با «برگردان».
 */

function col(key: string, label: string, type: GridColumn['type'], extra: Partial<GridColumn> = {}): GridColumn {
  return {
    key,
    label,
    type,
    width: 10,
    sticky: false,
    readOnly: type === 'readonly',
    required: false,
    options: null,
    money: false,
    tabStop: type === 'readonly' ? false : undefined,
    ...extra,
  }
}

/** ستون‌های پیش‌فرض جدول ورود (G.2؛ همان RugGridColumns سرور). */
const INTAKE: GridColumn[] = [
  col('rug.serial', 'شماره', 'serial'),
  col('batch.seller', 'فروشنده', 'combo', { sticky: true }),
  col('rug.qty', 'تعداد', 'int'),
  col('rug.custom.bg_color', 'رنگ متن', 'combo', { sticky: true }),
  col('rug.origin', 'محل بافت', 'combo', { sticky: true }),
  col('rug.intakeLength', 'طول', 'dim', { required: true }),
  col('rug.intakeWidth', 'عرض', 'dim', { required: true }),
  col('rug.areaM2', 'متراژ', 'readonly'),
  col('purchase.price', 'قیمت خرید', 'money+unit', { sticky: true, money: true }),
  col('purchase.broker', 'دلالی ٪', 'percent', { sticky: true, money: true }),
  col('rug.purchaseTotal', 'جمع خرید', 'readonly', { money: true }),
  col('rug.notes', 'یادداشت', 'text'),
]
const at = (key: string) => INTAKE.findIndex((c) => c.key === key)

const BUSINESS = {
  tenantId: 't1',
  userId: 'u1',
  policies: { floor: true, business: true, configure: false },
  moneyVisible: true,
  currency: { code: 'IRT', label: 'تومان', scale: 0 },
  moneyScale: 0,
  gridEnter: 'down',
}

function data(rows: GridRow[] = [], nextSerial: number | null = 3778): GridData {
  return { columns: INTAKE, rows, totals: { rows: rows.length, pieces: 0, areaM2: 0, purchase: null }, nextSerial }
}

function serverRow(values: Record<string, unknown>, extra: Partial<GridRow> = {}): GridRow {
  return { rugId: 'rug-1', version: 7, sku: 'R-1', values: values as GridRow['values'], derived: {}, ...extra }
}

let rugSeq = 0
function saved(op: GridOp, over: Partial<GridOpResult> = {}): GridOpResult {
  return {
    clientRowId: op.clientRowId,
    rugId: op.rugId ?? `rug-new-${++rugSeq}`,
    version: (op.version ?? 0) + 1,
    sku: null,
    status: op.delete ? GridRowStatus.Deleted : GridRowStatus.Saved,
    derived: {},
    errors: null,
    current: null,
    ...over,
  }
}

function make(options: Partial<GridControllerOptions> = {}) {
  const requests: GridBatchRequest[] = []
  const send = vi.fn(async (request: GridBatchRequest): Promise<GridBatchResponse> => {
    requests.push(JSON.parse(JSON.stringify(request)) as GridBatchRequest)
    return { results: request.ops.map((op) => saved(op)), totals: null }
  })
  const ctrl = useGridController({
    gridId: 'intake',
    allowAdd: true,
    allowDelete: true,
    serialMode: () => SerialMode.SuggestNext,
    maxPlausibleSideM: () => 12,
    draftScope: () => ({ tenantId: 't1', userId: 'u1', gridId: 'intake', contextId: '2026-09-06' }),
    batchContext: () => ({ id: null, sellerName: null, receivedAt: '2026-09-06' }),
    send,
    fetchValues: async () => [],
    fetchNextSerial: async () => null,
    restoreRug: async () => null,
    ...options,
  })
  return { ctrl, send, requests }
}

type Ctrl = ReturnType<typeof make>['ctrl']

const press = (ctrl: Ctrl, key: string, extra: Record<string, unknown> = {}) => ctrl.onKeydown({ key, ...extra })
/** تایپ در خانهٔ فعال و Tab/Enter. */
function enter(ctrl: Ctrl, text: string, key = 'Tab') {
  ctrl.onInput(text)
  press(ctrl, key)
}
const real = (ctrl: Ctrl) => ctrl.rows.filter((r) => !r.placeholder)

beforeEach(() => {
  vi.useFakeTimers()
  resetAppContext(BUSINESS)
})

afterEach(() => {
  vi.useRealTimers()
  vi.unstubAllGlobals()
  resetAppContext({})
})

describe('تایپ فقط با صفحه‌کلید (۲۰ ردیف)', () => {
  it('Tab و Enter مثل اکسل؛ بازگشت به ستون شروع، شمارهٔ پیشنهادی، خانه‌های چسبان و یک درخواست برای همه', async () => {
    const { ctrl, send, requests } = make()
    ctrl.load(data())
    expect(ctrl.nav).toMatchObject({ row: 0, col: at('rug.serial') })

    // ردیف اول: Tab شمارهٔ پیشنهادی را می‌پذیرد
    press(ctrl, 'Tab')
    expect(ctrl.nav.col).toBe(at('batch.seller'))
    enter(ctrl, 'حاج رضا')
    press(ctrl, 'Tab') // تعداد خالی (۱)
    enter(ctrl, 'سفید')
    enter(ctrl, 'گبه')
    enter(ctrl, '2.08')
    enter(ctrl, '2.1')
    // متراژ فقط‌خواندنی ایستگاه Tab نیست
    expect(ctrl.nav.col).toBe(at('purchase.price'))
    enter(ctrl, '۹م')
    enter(ctrl, '2', 'Enter')
    // Enter پس از رشتهٔ Tab: ردیف بعد، ستون شروع رشته
    expect(ctrl.nav).toMatchObject({ row: 1, col: at('rug.serial') })

    for (let r = 1; r < 20; r++) {
      press(ctrl, 'Tab') // شمارهٔ بعدی؛ خانه‌های چسبان از ردیف بالا می‌آیند
      press(ctrl, 'Tab') // فروشنده (چسبان)
      press(ctrl, 'Tab') // تعداد
      press(ctrl, 'Tab') // رنگ متن (چسبان)
      press(ctrl, 'Tab') // محل بافت (چسبان)
      enter(ctrl, faDigits(`1.${50 + r}`))
      enter(ctrl, '1.2', 'Enter')
      expect(ctrl.nav).toMatchObject({ row: r + 1, col: at('rug.serial') })
    }

    const rows = real(ctrl)
    expect(rows).toHaveLength(20)
    expect(ctrl.rows[ctrl.rows.length - 1].placeholder).toBe(true)
    expect(rows.map((r) => r.values['rug.serial'])).toEqual(Array.from({ length: 20 }, (_, i) => String(3778 + i)))
    for (const row of rows) {
      expect(row.values['batch.seller']).toBe('حاج رضا')
      expect(row.values['rug.origin']).toBe('گبه')
      expect(row.values['rug.custom.bg_color']).toBe('سفید')
      expect(row.values['purchase.price']).toEqual({ rate: 9_000_000, basis: CostBasis.PerM2 })
      expect(row.values['purchase.broker']).toBe(2)
    }
    expect(rows[1].values['rug.intakeLength']).toBe(1.51)
    expect(ctrl.suggestion).toBe(3798)

    await ctrl.flush()
    expect(send).toHaveBeenCalledTimes(1)
    expect(requests[0].ops).toHaveLength(20)
    expect(new Set(requests[0].ops.map((o) => o.clientRowId)).size).toBe(20)
    expect(requests[0].batch).toEqual({ id: null, sellerName: null, receivedAt: '2026-09-06' })
    expect(requests[0].ops[0].values).toMatchObject({
      'rug.serial': '3778',
      'batch.seller': 'حاج رضا',
      'rug.intakeLength': 2.08,
      'rug.intakeWidth': 2.1,
      'purchase.price': { rate: 9_000_000, basis: CostBasis.PerM2 },
      'purchase.broker': 2,
    })
    expect(real(ctrl).every((r) => r.status === 'clean' && r.rugId !== null)).toBe(true)
    expect(ctrl.unsavedCount).toBe(0)
  })
})

describe('شمارهٔ بعدی (G.4)', () => {
  it('بعد از «3985 - 4003» پیشنهاد ۴۰۰۴؛ تعداد ۵۰ دستهٔ ۵۰ شماره‌ای می‌سازد', () => {
    const { ctrl } = make()
    ctrl.load(data([serverRow({ 'rug.serial': '3985 - 4003', 'rug.qty': 19 })], 4004))
    expect(ctrl.nav.row).toBe(1)
    expect(ctrl.suggestion).toBe(4004)

    press(ctrl, 'Tab')
    expect(ctrl.rows[1].values['rug.serial']).toBe('4004')
    press(ctrl, 'Tab')
    enter(ctrl, '۵۰')
    expect(ctrl.rows[1].values['rug.serial']).toBe('4004 - 4053')
    expect(ctrl.suggestion).toBe(4054)
  })

  it('بازهٔ بلندتر از تعداد پرسیده می‌شود؛ «درستش می‌کنم» تعداد را برمی‌گرداند', () => {
    const { ctrl } = make()
    ctrl.load(data([], null))
    ctrl.activate(0, at('rug.serial'))
    enter(ctrl, '3985 - 4003')
    expect(ctrl.rows[0].values['rug.qty']).toBe(19)
    ctrl.activate(0, at('rug.qty'))
    enter(ctrl, '10')
    expect(ctrl.question?.message).toBe('شماره‌ها از ۳۹۸۵ تا ۴۰۰۳ است (۱۹ شماره) ولی تعداد را ۱۰ نوشته‌اید؛ درست است؟')
    ctrl.answer(1)
    expect(ctrl.question).toBeNull()
    expect(ctrl.rows[0].values['rug.qty']).toBe(19)
    expect(ctrl.mode).toBe('edit')
  })

  it('حالت «خودکار»: خانهٔ شماره ایستگاه Tab نیست و لازم هم نیست', () => {
    const { ctrl } = make({ serialMode: () => SerialMode.Auto })
    ctrl.load(data())
    expect(ctrl.nav.col).toBe(at('batch.seller'))
    expect(ctrl.isEditable(INTAKE[at('rug.serial')], ctrl.rows[0])).toBe(false)
  })
})

describe('اندازه و مبلغ (G.4)', () => {
  it('«۲۰۸» تأیید سانت؛ «۲۵» متر یا سانت (سانت اول)؛ «۱۲» متر؛ «208×210» هر دو', () => {
    const { ctrl } = make()
    ctrl.load(data([], null))
    ctrl.activate(0, at('rug.intakeLength'))

    enter(ctrl, '۲۰۸')
    expect(ctrl.question?.message).toBe('۲۰۸ ← ۲٫۰۸ متر؟')
    expect(ctrl.nav.col).toBe(at('rug.intakeLength')) // حرکت منتظر پاسخ
    press(ctrl, 'Enter') // پاسخ اول
    expect(ctrl.rows[0].values['rug.intakeLength']).toBe(2.08)
    expect(ctrl.nav.col).toBe(at('rug.intakeWidth'))

    enter(ctrl, '25')
    expect(ctrl.question?.choices[0].label).toContain('سانت')
    ctrl.answer(0)
    expect(ctrl.rows[0].values['rug.intakeWidth']).toBe(0.25)

    ctrl.activate(0, at('rug.intakeLength'))
    enter(ctrl, '30')
    ctrl.answer(1)
    expect(ctrl.rows[0].values['rug.intakeLength']).toBe(30)

    ctrl.activate(0, at('rug.intakeLength'))
    enter(ctrl, '12')
    expect(ctrl.question).toBeNull()
    expect(ctrl.rows[0].values['rug.intakeLength']).toBe(12)

    ctrl.activate(0, at('rug.intakeLength'))
    enter(ctrl, '208×210')
    ctrl.answer(0)
    expect(ctrl.rows[0].values['rug.intakeLength']).toBe(2.08)
    expect(ctrl.rows[0].values['rug.intakeWidth']).toBe(2.1)

    // در خانهٔ عرض هم هر دو پر می‌شود، نه «عرض = ۳٫۰۰» و گم شدن عدد دوم
    ctrl.activate(0, at('rug.intakeWidth'))
    enter(ctrl, '3×2.5')
    expect(ctrl.rows[0].values['rug.intakeLength']).toBe(3)
    expect(ctrl.rows[0].values['rug.intakeWidth']).toBe(2.5)
    // و یک «برگرد» هر دو را برمی‌گرداند
    ctrl.undo()
    expect(ctrl.rows[0].values['rug.intakeLength']).toBe(2.08)
    expect(ctrl.rows[0].values['rug.intakeWidth']).toBe(2.1)
  })

  it('«۱۰م» ده میلیون با سطر «ده میلیون تومان»؛ Alt+↓ چیپ واحد؛ Delete سطر را حذف می‌کند', async () => {
    const { ctrl, requests } = make()
    ctrl.load(data([serverRow({ 'rug.serial': '3778', 'rug.intakeLength': 2, 'rug.intakeWidth': 2, 'purchase.price': null })]))
    ctrl.activate(0, at('purchase.price'))
    ctrl.onInput('۱۰م')
    expect(ctrl.wordsLine).toBe('ده میلیون تومان')
    press(ctrl, 'Enter')
    expect(ctrl.rows[0].values['purchase.price']).toEqual({ rate: 10_000_000, basis: CostBasis.PerM2 })

    ctrl.activate(0, at('purchase.price'))
    press(ctrl, 'ArrowDown', { altKey: true })
    expect(ctrl.rows[0].values['purchase.price']).toEqual({ rate: 10_000_000, basis: CostBasis.PerPiece })
    await ctrl.flush()
    expect(requests[0].ops[0]).toMatchObject({
      rugId: 'rug-1',
      version: 7,
      values: { 'purchase.price': { rate: 10_000_000, basis: CostBasis.PerPiece } },
      baseValues: { 'purchase.price': null },
    })

    press(ctrl, 'Delete')
    expect(ctrl.rows[0].values['purchase.price']).toBeNull()
    await ctrl.flush()
    expect(requests[1].ops[0].values).toEqual({ 'purchase.price': null })
  })

  it('«کل» قدیمی همان «کل» می‌ماند و خانهٔ چندسطری فقط‌خواندنی است', () => {
    const { ctrl } = make()
    ctrl.load(
      data([
        serverRow({ 'rug.serial': '1', 'purchase.price': { rate: 40_000_000, basis: CostBasis.Total, amount: 40_000_000 } }),
        serverRow({ 'rug.serial': '2', 'purchase.price': { rate: null, basis: CostBasis.PerM2, amount: 55_000_000, lines: 2 } }, { rugId: 'rug-2' }),
      ]),
    )
    expect(ctrl.rows[0].values['purchase.price']).toMatchObject({ rate: 40_000_000, basis: CostBasis.Total })
    expect(ctrl.isEditable(INTAKE[at('purchase.price')], ctrl.rows[0])).toBe(true)
    expect(ctrl.isEditable(INTAKE[at('purchase.price')], ctrl.rows[1])).toBe(false)
  })

  it('بی Business ستون پولی نیست', () => {
    resetAppContext({ ...BUSINESS, policies: { floor: true, business: false, configure: false }, moneyVisible: false })
    const { ctrl } = make()
    ctrl.load(data())
    expect(ctrl.columns.some((c) => c.money)).toBe(false)
    expect(ctrl.columns.map((c) => c.key)).not.toContain('purchase.price')
  })
})

describe('چسباندن از اکسل (G.3)', () => {
  it('سرستون رد، ارقام فارسی، یک پرسش برای همهٔ سانت‌ها و یک قدم «برگرد»', () => {
    const { ctrl } = make()
    ctrl.load(data([], null))
    const header = ['شماره', 'رنگ متن', 'رنگ حاشیه', 'نقشه', 'محل بافت', 'مواد مصرفی', 'طول', 'عرض'].join('\t')
    const text = [
      header,
      ['۳۷۷۸', 'سفید', 'سفید', 'ساده', 'گبه', 'پشم', '۲۰۸', '۲۱۰'].join('\t'),
      ['۳۷۷۹', 'سفید', 'سفید', 'ساده', 'گبه', 'پشم', '2.30', '1.71'].join('\t'),
      ['3911 - 3960', 'لاکی', 'لاکی', 'ساده', 'گبه', 'پشم', '40', '40'].join('\t'),
    ].join('\r\n')
    ctrl.paste(text)
    expect(ctrl.question?.message).toBe('۴ اندازه به سانت نوشته شده؛ همه به متر تبدیل شود؟')
    expect(ctrl.question?.choices.map((c) => c.label)).toEqual(['تبدیل کن', 'نه'])
    ctrl.answer(0)

    const rows = real(ctrl)
    expect(rows).toHaveLength(3)
    expect(rows.map((r) => r.values['rug.serial'])).toEqual(['3778', '3779', '3911 - 3960'])
    expect(rows[0].values).toMatchObject({ 'rug.intakeLength': 2.08, 'rug.intakeWidth': 2.1, 'rug.origin': 'گبه' })
    expect(rows[1].values).toMatchObject({ 'rug.intakeLength': 2.3, 'rug.intakeWidth': 1.71 })
    expect(rows[2].values).toMatchObject({ 'rug.qty': 50, 'rug.intakeLength': 0.4, 'rug.intakeWidth': 0.4 })
    expect(ctrl.areaOf(rows[2])).toBe('۰٫۱۶ × ۵۰ = ۸٫۰۰')

    ctrl.undo()
    expect(real(ctrl)).toHaveLength(0)
    expect(ctrl.rows).toHaveLength(1)
  })

  it('«نه»: اندازه‌های سانت‌نما قرمز می‌مانند و بقیه می‌نشینند', () => {
    const { ctrl } = make()
    ctrl.load(data([], null))
    ctrl.activate(0, at('rug.intakeLength'))
    ctrl.paste('208\t2.10')
    ctrl.answer(1)
    expect(ctrl.rows[0].values['rug.intakeWidth']).toBe(2.1)
    expect(ctrl.rows[0].errors['rug.intakeLength']).toBeTruthy()
  })
})

describe('برگرد و پر کردن', () => {
  it('Ctrl+D مقدار بالایی را می‌آورد و Ctrl+Z با چیدمان فارسی برمی‌گرداند', () => {
    const { ctrl } = make()
    ctrl.load(data([serverRow({ 'rug.serial': '1', 'rug.origin': 'گبه' }), serverRow({ 'rug.serial': '2', 'rug.origin': null }, { rugId: 'rug-2' })]))
    ctrl.activate(1, at('rug.origin'))
    press(ctrl, 'ی', { ctrlKey: true, code: 'KeyD' })
    expect(ctrl.rows[1].values['rug.origin']).toBe('گبه')
    press(ctrl, 'ظ', { ctrlKey: true, code: 'KeyZ' })
    expect(ctrl.rows[1].values['rug.origin']).toBeNull()
    expect(ctrl.rows[1].dirty).toEqual([])
  })
})

describe('ذخیره (G.6)', () => {
  it('نشست تمام‌شده (۴۰۱) در چند ذخیرهٔ پشت‌سرهم یک پیام می‌دهد، نه یک پیام برای هر بار', async () => {
    const { ctrl, send } = make()
    send.mockImplementation(async () => {
      throw new ApiError(401, 'نشست شما تمام شده؛ صفحه را تازه کنید و دوباره وارد شوید.', null)
    })
    ctrl.load(data([serverRow({ 'rug.serial': '3778', 'rug.origin': 'گبه' })]))
    ctrl.activate(0, at('rug.origin'))
    enter(ctrl, 'کاشان', 'Enter')
    await ctrl.flush()
    ctrl.activate(0, at('rug.origin'))
    enter(ctrl, 'تبریز', 'Enter')
    await ctrl.flush()
    expect(send).toHaveBeenCalledTimes(2)
    expect(ctrl.notices.filter((n) => n.text.startsWith('نشست شما تمام شده'))).toHaveLength(1)
    expect(ctrl.rows[0].status).not.toBe('clean')
  })

  it('ردیف تازهٔ ناقص فرستاده نمی‌شود و با بیرون رفتن، خانهٔ لازم قرمز می‌شود', async () => {
    const { ctrl, send } = make()
    ctrl.load(data())
    press(ctrl, 'Tab')
    press(ctrl, 'ArrowDown')
    await ctrl.flush()
    expect(send).not.toHaveBeenCalled()
    expect(ctrl.rows[0].errors['rug.intakeLength']).toBe('«طول» را بنویسید.')
  })

  it('خانهٔ دیگری که او عوض کرده بی‌صدا ادغام می‌شود؛ همان خانه تعارض با «نوشتهٔ من بماند»', async () => {
    const { ctrl, send, requests } = make()
    ctrl.load(data([serverRow({ 'rug.serial': '3778', 'rug.origin': 'گبه', 'rug.custom.bg_color': 'سفید' })]))
    ctrl.activate(0, at('rug.origin'))
    enter(ctrl, 'کاشان', 'Enter')

    send.mockImplementationOnce(async (request: GridBatchRequest) => {
      requests.push(request)
      return {
        results: [
          saved(request.ops[0], {
            status: GridRowStatus.Conflict,
            version: 9,
            current: { 'rug.serial': '3778', 'rug.origin': 'تبریز', 'rug.custom.bg_color': 'لاکی' },
          }),
        ],
      }
    })
    await ctrl.flush()
    const row = ctrl.rows[0]
    expect(row.status).toBe('conflict')
    expect(row.values['rug.custom.bg_color']).toBe('لاکی') // ادغام بی‌صدا
    expect(ctrl.conflictLines(row)).toEqual(['محل بافت: شما «کاشان» · او «تبریز»'])

    ctrl.resolveConflict(row.id, 'mine')
    await ctrl.flush()
    expect(requests[1].ops[0]).toMatchObject({ version: 9, values: { 'rug.origin': 'کاشان' }, baseValues: { 'rug.origin': 'تبریز' } })
    expect(ctrl.rows[0].status).toBe('clean')
  })

  it('«نوشتهٔ او را ببینم» مقدار سرور را می‌گذارد و چیزی نمی‌فرستد', async () => {
    const { ctrl, send } = make()
    ctrl.load(data([serverRow({ 'rug.serial': '3778', 'rug.origin': 'گبه' })]))
    ctrl.activate(0, at('rug.origin'))
    enter(ctrl, 'کاشان', 'Enter')
    send.mockImplementationOnce(async (request: GridBatchRequest) => ({
      results: [saved(request.ops[0], { status: GridRowStatus.Conflict, version: 9, current: { 'rug.origin': 'تبریز' } })],
    }))
    await ctrl.flush()
    ctrl.resolveConflict(ctrl.rows[0].id, 'theirs')
    expect(ctrl.rows[0].values['rug.origin']).toBe('تبریز')
    expect(ctrl.rows[0].status).toBe('clean')
    send.mockClear()
    await ctrl.flush()
    expect(send).not.toHaveBeenCalled()
  })

  it('قطع شبکه: نوار «دوباره امتحان می‌کنیم» و ارسال دوباره با همان ClientRowId', async () => {
    const { ctrl, send, requests } = make()
    ctrl.load(data([], null))
    ctrl.activate(0, at('rug.serial'))
    enter(ctrl, '3778')
    ctrl.activate(0, at('rug.intakeLength'))
    enter(ctrl, '2')
    enter(ctrl, '2')
    send.mockImplementationOnce(async () => {
      throw new TypeError('Failed to fetch')
    })
    await ctrl.flush()
    expect(ctrl.saveState.phase).toBe('retrying')
    expect(ctrl.saveState.retryIn).toBe(2)
    const id = ctrl.rows[0].id
    await vi.advanceTimersByTimeAsync(2000)
    expect(send).toHaveBeenCalledTimes(2)
    expect(requests[0].ops[0].clientRowId).toBe(id)
    expect(ctrl.rows[0].rugId).not.toBeNull()
    expect(ctrl.saveState.phase).toBe('saved')
  })

  it('همپوشانی شماره: «شمارهٔ … آزاد است؛ همان را بگذارم؟» و «بله» همان را می‌گذارد', async () => {
    const { ctrl, send } = make()
    ctrl.load(data())
    press(ctrl, 'Tab')
    ctrl.activate(0, at('rug.intakeLength'))
    enter(ctrl, '2')
    enter(ctrl, '2')
    send.mockImplementationOnce(async (request: GridBatchRequest) => ({
      results: [
        saved(request.ops[0], {
          rugId: null,
          status: GridRowStatus.Invalid,
          errors: [{ field: 'rug.serial', message: 'شمارهٔ ۳۷۷۸ تکراری است.' }],
          nextFree: 3779,
        }),
      ],
    }))
    await ctrl.flush()
    expect(ctrl.rows[0].errors['rug.serial']).toBe('شمارهٔ ۳۷۷۸ تکراری است.')
    expect(ctrl.rows[0].nextFree).toBe(3779)
    ctrl.acceptNextFree(ctrl.rows[0].id)
    expect(ctrl.rows[0].values['rug.serial']).toBe('3779')
    await ctrl.flush()
    expect(ctrl.rows[0].status).toBe('clean')
  })
})

describe('پیش‌نویس (G.7)', () => {
  function memoryStorage() {
    const map = new Map<string, string>()
    return {
      map,
      getItem: (k: string) => map.get(k) ?? null,
      setItem: (k: string, v: string) => void map.set(k, v),
      removeItem: (k: string) => void map.delete(k),
    }
  }
  const scope = { tenantId: 't1', userId: 'u1', gridId: 'intake', contextId: '2026-09-06' }

  it('بی شبکه ۵ ردیف، بار دوباره: پیشنهاد، برگرداندن و یک بار ذخیره با همان ClientRowId', async () => {
    const storage = memoryStorage()
    vi.stubGlobal('localStorage', storage)
    const first = make({ send: vi.fn(async () => Promise.reject(new TypeError('offline'))) })
    first.ctrl.load(data())
    for (let r = 0; r < 5; r++) {
      press(first.ctrl, 'Tab')
      first.ctrl.activate(r, at('rug.intakeLength'))
      enter(first.ctrl, '2')
      enter(first.ctrl, '1.5', 'Enter')
      first.ctrl.activate(r + 1, at('rug.serial'))
    }
    vi.advanceTimersByTime(400)
    const ids = real(first.ctrl).map((r) => r.id)
    expect(JSON.parse(storage.getItem(draftKey(scope)!)!).rows).toHaveLength(5)
    first.ctrl.dispose()

    // صفحهٔ تازه
    const second = make()
    second.ctrl.load(data())
    expect(second.ctrl.draftsPending).toHaveLength(5)
    second.ctrl.restoreDrafts()
    expect(real(second.ctrl).map((r) => r.id)).toEqual(ids)
    await second.ctrl.flush()
    expect(second.send).toHaveBeenCalledTimes(1)
    expect(second.requests[0].ops.map((o) => o.clientRowId)).toEqual(ids)
    vi.advanceTimersByTime(400)
    expect(storage.getItem(draftKey(scope)!)).toBeNull()
  })

  it('«پاک کردن» به rugs:draft-discarded می‌رود و «برگردان» روی toast ردیف‌ها را برمی‌گرداند', () => {
    const storage = memoryStorage()
    vi.stubGlobal('localStorage', storage)
    storage.setItem(
      draftKey(scope)!,
      JSON.stringify({
        v: 1,
        savedAt: Date.now(),
        rows: [{ clientRowId: 'c1', rugId: null, version: null, values: { 'rug.serial': '3778' }, base: {}, dirty: ['rug.serial'] }],
      }),
    )
    const { ctrl } = make()
    ctrl.load(data())
    expect(ctrl.draftsPending).toHaveLength(1)
    ctrl.discardDrafts()
    expect(ctrl.draftsPending).toBeNull()
    expect([...storage.map.keys()].some((k) => k.startsWith('rugs:draft-discarded:t1:u1:intake:'))).toBe(true)
    const toast = ctrl.notices.find((n) => n.text === 'پاک شد')
    expect(toast?.action?.label).toBe('برگردان')
    void toast!.action!.run()
    expect(real(ctrl).map((r) => r.id)).toEqual(['c1'])
  })
})

describe('حذف فرش (G.3، E6)', () => {
  it('deletes a create whose response was lost using the original client row id', async () => {
    let requestCount = 0
    const sent: GridBatchRequest[] = []
    const { ctrl } = make({ send: async (request) => {
      sent.push(request)
      requestCount++
      if (requestCount === 1) throw new TypeError('lost response')
      return { results: request.ops.map((op) => saved(op)) }
    } })
    ctrl.load(data([], null))
    ctrl.activate(0, at('rug.intakeLength'))
    enter(ctrl, '2')
    enter(ctrl, '3')
    await ctrl.flush()
    const row = ctrl.rows[0]
    expect(row.rugId).toBeNull()
    const pending = ctrl.deleteRow(row.id)
    settleConfirm(true)
    await pending
    expect(sent).toHaveLength(2)
    expect(sent[1].ops[0]).toMatchObject({ clientRowId: row.id, rugId: null, delete: true })
    await vi.advanceTimersByTimeAsync(60000)
    expect(sent).toHaveLength(2)
    expect(real(ctrl)).toHaveLength(0)
    ctrl.dispose()
  })

  it('pasting price retains total and sticky per-piece units', () => {
    const { ctrl } = make()
    ctrl.load(data([serverRow({ 'purchase.price': { rate: 100, basis: CostBasis.Total } })], null))
    ctrl.activate(0, at('purchase.price'))
    ctrl.paste('200')
    expect(ctrl.rows[0].values['purchase.price']).toMatchObject({ rate: 200, basis: CostBasis.Total })
    ctrl.rows[0].values['purchase.price'] = { rate: 300, basis: CostBasis.PerPiece }
    ctrl.activate(1, at('purchase.price'))
    enter(ctrl, '400')
    expect(ctrl.rows[1].values['purchase.price']).toMatchObject({ rate: 400, basis: CostBasis.PerPiece })
    ctrl.dispose()
  })
  it('تأیید، حذف، toast «حذف شد [برگردان]» و برگرداندن', async () => {
    const restoreRug = vi.fn(async () => serverRow({ 'rug.serial': '3778' }, { version: 12 }))
    const { ctrl, requests } = make({ restoreRug })
    ctrl.load(data([serverRow({ 'rug.serial': '3778' }, { derived: { 'row.canDelete': true } })]))
    expect(ctrl.canDeleteRow(ctrl.rows[0])).toBe(true)

    const pending = ctrl.deleteRow(ctrl.rows[0].id)
    settleConfirm(true)
    await pending
    expect(requests[0].ops[0]).toMatchObject({ rugId: 'rug-1', delete: true })
    expect(real(ctrl)).toHaveLength(0)

    const toast = ctrl.notices.find((n) => n.text === 'حذف شد')
    expect(toast?.action?.label).toBe('برگردان')
    await toast!.action!.run()
    expect(restoreRug).toHaveBeenCalledWith('rug-1')
    expect(real(ctrl)).toHaveLength(1)
    expect(real(ctrl)[0].version).toBe(12)
  })

  it('«حذف این فرش» وقتی قاعده نه می‌گوید پنهان است؛ رد سرور پیام «فقط بازرگان» می‌دهد', async () => {
    const { ctrl, send } = make()
    ctrl.load(
      data([
        serverRow({ 'rug.serial': '1' }, { derived: { 'row.canDelete': false } }),
        serverRow({ 'rug.serial': '2' }, { rugId: 'rug-2', derived: { 'row.canDelete': true } }),
      ]),
    )
    expect(ctrl.canDeleteRow(ctrl.rows[0])).toBe(false)
    send.mockImplementationOnce(async (request: GridBatchRequest) => ({
      results: [
        saved(request.ops[0], {
          status: GridRowStatus.Invalid,
          errors: [{ field: 'row', message: 'این فرش را فقط بازرگان می‌تواند حذف کند.' }],
        }),
      ],
    }))
    const pending = ctrl.deleteRow(ctrl.rows[1].id)
    settleConfirm(true)
    await pending
    expect(real(ctrl)).toHaveLength(2)
    expect(ctrl.notices.some((n) => n.text === 'این فرش را فقط بازرگان می‌تواند حذف کند.')).toBe(true)
  })

  it('شمارهٔ دسته در متن پرسش جدا از راست‌به‌چپ است تا برعکس دیده نشود (H.7)', () => {
    const { ctrl } = make()
    ctrl.load(data([serverRow({ 'rug.serial': '3911 - 3960', 'rug.qty': 50 })]))
    expect(ctrl.serialLabel(ctrl.rows[0])).toBe('⁦۳۹۱۱ - ۳۹۶۰⁩')
    expect(ctrl.serialLabel(ctrl.rows[1])).toBe('')
  })
})

function faDigits(value: string): string {
  return value.replace(/\d/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[Number(d)])
}
