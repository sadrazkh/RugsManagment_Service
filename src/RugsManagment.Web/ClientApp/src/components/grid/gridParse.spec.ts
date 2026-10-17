import { describe, expect, it } from 'vitest'
import { resetAppContext } from '@/lib/context'
import { CostBasis, type GridColumn } from '@/lib/types/grid'
import {
  areaText,
  displayText,
  editableText,
  faNum,
  isFooterLine,
  isHeaderLine,
  nextSerialLocal,
  parseCellInput,
  parseSerial,
  parseTsv,
  planPaste,
  readPurchaseCell,
  rowArea,
  rowPurchase,
  serialCovers,
  serialForLot,
  serialInfo,
  splitUnitSuffix,
  suggestionIndex,
  toTsv,
  writePurchaseCell,
} from './gridParse'

resetAppContext({ moneyScale: 0, currency: { code: 'IRT', label: 'تومان', scale: 0 }, moneyVisible: true })

function col(key: string, label: string, type: GridColumn['type'], extra: Partial<GridColumn> = {}): GridColumn {
  return { key, label, type, width: 10, sticky: false, readOnly: type === 'readonly', required: false, options: null, money: false, ...extra }
}

/** ستون‌های پیش‌فرض جدول ورود (G.2). */
const INTAKE: GridColumn[] = [
  col('rug.serial', 'شماره', 'serial'),
  col('batch.seller', 'فروشنده', 'combo', { sticky: true }),
  col('rug.qty', 'تعداد', 'int'),
  col('rug.custom.bg_color', 'رنگ متن', 'combo', { sticky: true }),
  col('rug.custom.border_color', 'رنگ حاشیه', 'combo', { sticky: true }),
  col('rug.pattern', 'نقشه', 'combo'),
  col('rug.origin', 'محل بافت', 'combo', { sticky: true }),
  col('rug.material', 'مواد مصرفی', 'combo', { sticky: true }),
  col('rug.intakeLength', 'طول', 'dim', { required: true }),
  col('rug.intakeWidth', 'عرض', 'dim', { required: true }),
  col('rug.areaM2', 'متراژ', 'readonly'),
  col('purchase.price', 'قیمت خرید', 'money+unit', { sticky: true, money: true }),
  col('purchase.broker', 'دلالی ٪', 'percent', { sticky: true, money: true }),
  col('rug.purchaseTotal', 'جمع خرید', 'readonly', { money: true }),
  col('rug.notes', 'یادداشت', 'text'),
]

const CTX = { moneyScale: 0, currencyLabel: 'تومان', maxPlausibleSideM: 12 }
const byKey = (key: string) => INTAKE.find((c) => c.key === key)!

// ── بردارهای مشترک شماره با xUnit (tests/fixtures/math-vectors.json) ──

interface SerialVector {
  id: string
  input: string
  display?: string | null
  from?: number | null
  to?: number | null
  count?: number
  next?: number
  error?: string
}
interface SerialQtyVector {
  id: string
  input: string
  qty: number
  covers: boolean
}
const shared = import.meta.glob<{ default: { serialVectors?: SerialVector[]; serialQtyVectors?: SerialQtyVector[] } }>(
  '../../../../../../tests/fixtures/math-vectors.json',
  { eager: true },
)
const fixture = Object.values(shared)[0]?.default
const serialVectors = fixture?.serialVectors ?? []
const serialQtyVectors = fixture?.serialQtyVectors ?? []

describe('شماره (G.4، آینهٔ SerialNumber)', () => {
  it.skipIf(serialVectors.length === 0).each(serialVectors)('$id', (v) => {
    const parsed = parseSerial(v.input)
    if (v.error) {
      expect(parsed).toEqual({ status: 'error', code: v.error })
      return
    }
    if (v.display === null) {
      expect(parsed.status).toBe('empty')
      return
    }
    expect(parsed.status).toBe('ok')
    if (parsed.status !== 'ok') return
    expect(parsed.display).toBe(v.display)
    expect(parsed.from).toBe(v.from ?? null)
    expect(parsed.to).toBe(v.to ?? null)
    if (v.count !== undefined) expect(parsed.count).toBe(v.count)
    if (v.next !== undefined) expect(nextSerialLocal(0, [parsed.display])).toBe(v.next)
  })

  it.skipIf(serialQtyVectors.length === 0).each(serialQtyVectors)('$id', (v) => {
    const info = serialInfo(v.input)
    expect(info).not.toBeNull()
    expect(serialCovers(info!, v.qty)).toBe(v.covers)
  })

  it('شکل‌های پذیرفته و نمایش «3911 - 3960»', () => {
    for (const input of ['3911 - 3960', '3911–3960', '3911_3960', '3911 تا 3960', '۳۹۱۱ تا ۳۹۶۰', '٣٩١١-٣٩٦٠']) {
      const parsed = parseSerial(input)
      expect(parsed.status, input).toBe('ok')
      if (parsed.status === 'ok') expect(parsed.display, input).toBe('3911 - 3960')
    }
    expect(parseSerial('۳۷۷۸')).toMatchObject({ status: 'ok', display: '3778', from: 3778, count: 1 })
    expect(parseSerial('A-12')).toMatchObject({ status: 'ok', display: 'A-12', from: null })
  })

  it('برعکس، بیش از ۱۰٬۰۰۰ و بی‌شکل خطاست', () => {
    expect(parseSerial('3960-3911')).toEqual({ status: 'error', code: 'E_SERIAL_REVERSED' })
    expect(parseSerial('1 - 10001')).toEqual({ status: 'error', code: 'E_SERIAL_SPAN' })
    expect(parseSerial('3911 3960')).toEqual({ status: 'error', code: 'E_SERIAL_FORMAT' })
  })
})

describe('شمارهٔ بعدی در خود جدول (G.4)', () => {
  it('بعد از «3985 - 4003» پیشنهاد ۴۰۰۴ است، نه ۳۹۹۵', () => {
    expect(nextSerialLocal(3990, ['3985 - 4003'])).toBe(4004)
  })

  it('از شمارهٔ سرور پایین‌تر نمی‌آید و شمارهٔ متنی را نمی‌شمارد', () => {
    expect(nextSerialLocal(4100, ['2026', '2028', 'A-12'])).toBe(4100)
    expect(nextSerialLocal(3000, ['3778', null, '3911 - 3960'])).toBe(3961)
  })

  it('افزایش محلی پس از ردیف دسته (پیش‌نویس‌ها)', () => {
    const serials = ['4004']
    serials.push(serialForLot(nextSerialLocal(4004, serials)!, 50))
    expect(serials[1]).toBe('4005 - 4054')
    expect(nextSerialLocal(4004, serials)).toBe(4055)
  })

  it('حالت دستی پیشنهاد ندارد', () => {
    expect(nextSerialLocal(null, ['4004'])).toBeNull()
  })

  it('ردیفی که فقط تعداد ۵۰ گرفته ۵۰ شماره می‌گیرد', () => {
    expect(serialForLot(4004, 50)).toBe('4004 - 4053')
    expect(serialForLot(4004, 1)).toBe('4004')
  })
})

describe('خواندن خانه بر اساس نوع ستون', () => {
  it('اندازه: «۲۰۸» تأیید سانت، «۲۵» و «۳۰» پرسش متر یا سانت (سانت اول)، «۱۲» متر', () => {
    const dim = byKey('rug.intakeLength')
    const cm = parseCellInput(dim, '۲۰۸', CTX)
    expect(cm.status).toBe('ask')
    if (cm.status === 'ask') {
      expect(cm.message).toBe('۲۰۸ ← ۲٫۰۸ متر؟')
      expect(cm.cmLike).toBe(true)
      expect(cm.choices[0].value).toBe(2.08)
      expect(cm.choices[1].value).toBeUndefined()
    }
    for (const typed of ['25', '30']) {
      const r = parseCellInput(dim, typed, CTX)
      expect(r.status, typed).toBe('ask')
      if (r.status === 'ask') {
        expect(r.choices[0].label).toContain('سانت')
        expect(r.choices[0].value).toBe(Number(typed) / 100)
        expect(r.choices[1].value).toBe(Number(typed))
      }
    }
    expect(parseCellInput(dim, '12', CTX)).toEqual({ status: 'ok', value: 12 })
    expect(parseCellInput(dim, '40', CTX).status).toBe('ask')
    expect(parseCellInput(dim, '2,08', CTX)).toEqual({ status: 'ok', value: 2.08 })
  })

  it('«208×210» هر دو خانه را پر می‌کند (با پرسش سانت)', () => {
    const r = parseCellInput(byKey('rug.intakeLength'), '208×210', CTX)
    expect(r.status).toBe('ask')
    if (r.status === 'ask') {
      expect(r.choices[0].value).toBe(2.08)
      expect(r.choices[0].pair).toBe(2.1)
    }
    expect(parseCellInput(byKey('rug.intakeLength'), '2.08×2.10', CTX)).toEqual({ status: 'ok', value: 2.08, pair: 2.1 })
  })

  it('مبلغ: «۱۰م» ده میلیون، «۸۵ه» ۸۵ هزار، «10.000» پرسش', () => {
    const money = col('m', 'مبلغ', 'money', { money: true })
    expect(parseCellInput(money, '۱۰م', CTX)).toEqual({ status: 'ok', value: 10_000_000 })
    expect(parseCellInput(money, '۸۵ه', CTX)).toEqual({ status: 'ok', value: 85_000 })
    const ask = parseCellInput(money, '10.000', CTX)
    expect(ask.status).toBe('ask')
    if (ask.status === 'ask') expect(ask.message).toBe('منظورتان ۱۰٬۰۰۰ است؟')
    expect(parseCellInput(money, '10.5', CTX).status).toBe('error')
  })

  it('مبلغ + واحد: پیش‌فرض «هر متر»، واژهٔ واحد در انتها، چیپ فعلی می‌ماند', () => {
    const price = byKey('purchase.price')
    expect(parseCellInput(price, '۹م', CTX)).toEqual({ status: 'ok', value: { rate: 9_000_000, basis: CostBasis.PerM2 } })
    expect(parseCellInput(price, '۳۰۰ه هر تخته', CTX)).toEqual({ status: 'ok', value: { rate: 300_000, basis: CostBasis.PerPiece } })
    expect(parseCellInput(price, '300000', { ...CTX, current: { rate: 1, basis: CostBasis.Total } })).toEqual({
      status: 'ok',
      value: { rate: 300_000, basis: CostBasis.Total },
    })
    expect(parseCellInput(price, '', CTX)).toEqual({ status: 'ok', value: null })
  })

  it('درصد و تعداد و گزینه', () => {
    expect(parseCellInput(byKey('purchase.broker'), '۲٪', CTX)).toEqual({ status: 'ok', value: 2 })
    expect(parseCellInput(byKey('purchase.broker'), '120', CTX).status).toBe('error')
    expect(parseCellInput(byKey('rug.qty'), '۵۰', CTX)).toEqual({ status: 'ok', value: 50 })
    expect(parseCellInput(byKey('rug.qty'), '0', CTX).status).toBe('error')
    const select = col('s', 'نوع', 'select', { options: ['کاشان', 'گبه'] })
    expect(parseCellInput(select, 'كاشان', CTX)).toEqual({ status: 'ok', value: 'کاشان' })
    expect(parseCellInput(select, 'تبریز', CTX).status).toBe('error')
    expect(parseCellInput(byKey('rug.origin'), '  كاشان  ', CTX)).toEqual({ status: 'ok', value: 'کاشان' })
  })

  it('شماره در خانه: دستهٔ برعکس خطای فارسی می‌دهد', () => {
    const r = parseCellInput(byKey('rug.serial'), '3960-3911', CTX)
    expect(r.status).toBe('error')
    if (r.status === 'error') expect(r.message).toContain('کوچک‌تر')
  })

  it('واژهٔ واحد جدا می‌شود', () => {
    expect(splitUnitSuffix('۳۰۰ه هر تخته')).toEqual({ text: '۳۰۰ه', basis: CostBasis.PerPiece })
    expect(splitUnitSuffix('۱۰م کل')).toEqual({ text: '۱۰م', basis: CostBasis.Total })
    expect(splitUnitSuffix('۹م')).toEqual({ text: '۹م', basis: null })
  })
})

describe('نمایش', () => {
  it('prefers final dimensions for display while purchase uses intake dimensions', () => {
    const values = { 'rug.intakeLength': 3.35, 'rug.intakeWidth': 2.52, 'rug.finalLength': 3.3, 'rug.finalWidth': 2.5 }
    expect(areaText(rowArea(values))).toBe('۸٫۲۵')
    expect(areaText(rowArea(values, 'intake'))).toBe('۸٫۴۴')
  })
  it('ارقام فارسی با «٬» و «٫»', () => {
    expect(faNum(10_000_000, 0)).toBe('۱۰٬۰۰۰٬۰۰۰')
    expect(faNum('2.5', 2, 2)).toBe('۲٫۵۰')
    expect(faNum(null)).toBe('')
  })

  it('متراژ دسته «۰٫۱۶ × ۵۰ = ۸٫۰۰»', () => {
    const area = rowArea({ 'rug.intakeLength': 0.4, 'rug.intakeWidth': 0.4, 'rug.qty': 50 })
    expect(areaText(area)).toBe('۰٫۱۶ × ۵۰ = ۸٫۰۰')
    expect(areaText(rowArea({ 'rug.intakeLength': 2.08, 'rug.intakeWidth': 2.1 }))).toBe('۴٫۳۷')
    // نیمه‌ها: ۲٫۳۰ × ۱٫۶۵ = ۳٫۷۹۵ → ۳٫۸۰ (نه ۳٫۷۹ با ضرب float)
    expect(areaText(rowArea({ 'rug.intakeLength': 2.3, 'rug.intakeWidth': 1.65 }))).toBe('۳٫۸۰')
  })

  it('خانهٔ خرید: «کل» قدیمی همان «کل» می‌ماند، چندسطری فقط‌خواندنی', () => {
    const price = byKey('purchase.price')
    expect(readPurchaseCell({ rate: 40_000_000, basis: CostBasis.Total }, price)).toEqual({ rate: 40_000_000, basis: CostBasis.Total })
    expect(readPurchaseCell({ rate: 40_000_000, basis: 'Total' }, price)?.basis).toBe(CostBasis.Total)
    expect(readPurchaseCell(300_000, price)).toEqual({ rate: 300_000, basis: CostBasis.PerM2 })
    const multi = readPurchaseCell({ rate: null, basis: 0, amount: 55_000_000, lines: 2 }, price)
    expect(multi?.lines).toBe(2)
    expect(displayText(price, { rate: null, basis: 0, amount: 55_000_000, lines: 2 }, 0)).toBe('۵۵٬۰۰۰٬۰۰۰')
    expect(writePurchaseCell({ rate: 300_000, basis: CostBasis.PerPiece }, 'money+unit')).toEqual({ rate: 300_000, basis: CostBasis.PerPiece })
    expect(writePurchaseCell({ rate: 2, basis: CostBasis.Percent }, 'percent')).toBe(2)
    expect(writePurchaseCell({ rate: null, basis: CostBasis.PerM2 }, 'money+unit')).toBeNull()
  })

  it('خانهٔ درصدی خرید از سرور شیء است ({rate, basis, amount})؛ نرخ دیده و ویرایش می‌شود', () => {
    const broker = byKey('purchase.broker')
    expect(displayText(broker, { rate: 2, basis: CostBasis.Percent, amount: 786_600 }, 0)).toBe('۲')
    expect(editableText(broker, { rate: 2.5, basis: CostBasis.Percent, amount: 1 })).toBe('۲.۵')
    expect(displayText(broker, 2, 0)).toBe('۲')
    expect(displayText(broker, { rate: null, basis: CostBasis.Percent, amount: 900_000, lines: 2 }, 0)).toBe('۹۰۰٬۰۰۰')
    expect(displayText(broker, null, 0)).toBe('')
  })

  it('متن ویرایشی: ارقام فارسی بی هزارگان', () => {
    expect(editableText(byKey('rug.intakeLength'), 2.08)).toBe('۲.۰۸')
    expect(editableText(byKey('purchase.price'), { rate: 9_000_000, basis: 1 })).toBe('۹۰۰۰۰۰۰')
    expect(editableText(byKey('rug.serial'), '3911 - 3960')).toBe('۳۹۱۱ - ۳۹۶۰')
  })
})

describe('جمع خرید ردیف (D.7 #1–#3 تایپ‌شده در جدول)', () => {
  const lines = (price: { rate: number; basis: number }, qty: number, l: number, w: number) => ({
    'rug.intakeLength': l,
    'rug.intakeWidth': w,
    'rug.qty': qty,
    'purchase.price': price,
    'purchase.broker': 2,
  })

  it('A هر متر ۹٬۰۰۰٬۰۰۰ + دلالی ۲ → ۴۰٬۱۱۶٬۶۰۰', () => {
    const r = rowPurchase(lines({ rate: 9_000_000, basis: CostBasis.PerM2 }, 1, 2.08, 2.1), INTAKE, 0)
    expect(faNum(r!.total, 0)).toBe('۴۰٬۱۱۶٬۶۰۰')
    expect(r!.text).toBe('۹٬۰۰۰٬۰۰۰ × ۴٫۳۷ + ۲٪ = ۴۰٬۱۱۶٬۶۰۰')
  })

  it('B «هر تخته» ۳۰۰٬۰۰۰ روی دستهٔ ۵۰ → ۱۵٬۳۰۰٬۰۰۰', () => {
    const r = rowPurchase(lines({ rate: 300_000, basis: CostBasis.PerPiece }, 50, 0.4, 0.4), INTAKE, 0)
    expect(faNum(r!.total, 0)).toBe('۱۵٬۳۰۰٬۰۰۰')
    expect(r!.text).toBe('۳۰۰٬۰۰۰ × ۵۰ + ۲٪ = ۱۵٬۳۰۰٬۰۰۰')
  })

  it('C هر متر ۸٬۵۰۰٬۰۰۰ → ۷۳٬۱۷۴٬۸۰۰', () => {
    const r = rowPurchase(lines({ rate: 8_500_000, basis: CostBasis.PerM2 }, 1, 3.35, 2.52), INTAKE, 0)
    expect(faNum(r!.total, 0)).toBe('۷۳٬۱۷۴٬۸۰۰')
  })

  it('هر متر روی دسته: یک تخته گرد و بعد × تعداد (۴۵٫۵۵ × ۰٫۱۶ → ۷ × ۵۰ = ۳۵۰)', () => {
    const r = rowPurchase({ 'rug.intakeLength': 0.4, 'rug.intakeWidth': 0.4, 'rug.qty': 50, 'purchase.price': { rate: 45.55, basis: 1 } }, INTAKE, 0)
    expect(r!.text).toBe('۷ × ۵۰ = ۳۵۰')
  })

  it('بی‌نرخ → null؛ «هر متر» بی اندازه هنوز حساب نمی‌شود', () => {
    expect(rowPurchase({ 'rug.intakeLength': 2, 'rug.intakeWidth': 2 }, INTAKE, 0)).toBeNull()
    expect(rowPurchase({ 'purchase.price': { rate: 9_000_000, basis: CostBasis.PerM2 }, 'purchase.broker': 2 }, INTAKE, 0)).toBeNull()
    expect(rowPurchase({ 'purchase.price': { rate: 300_000, basis: CostBasis.PerPiece } }, INTAKE, 0)?.text).toBe('۳۰۰٬۰۰۰')
  })
})

describe('پیشنهاد خانهٔ combo (G.3)', () => {
  it('همان نوشته اگر در فهرست هست برجسته می‌شود، حتی وقتی مقدار بلندترِ پرتکرارتر بالاتر است', () => {
    // فهرست سرور به ترتیب تکرار است: «گبه پشم» بالاتر از «گبه»
    expect(suggestionIndex(['گبه پشم', 'گبه', 'لچک ترنج'], 'گبه')).toBe(1)
    expect(suggestionIndex(['گبه پشم', 'گبه'], 'گبه ')).toBe(1)
    expect(suggestionIndex(['كاشان', 'کاشان قدیم'], 'کاشان')).toBe(0) // «ک» عربی و فارسی یکی‌اند
  })

  it('بی مقدار هم‌نام، اولین پیشنهادی که با نوشته شروع می‌شود؛ پیشنهادِ «دارد» برجسته نمی‌شود', () => {
    expect(suggestionIndex(['حاج محمود', 'حاج رضا'], 'حاج مح')).toBe(0)
    expect(suggestionIndex(['لچک ترنج'], 'ترنج')).toBe(-1)
    expect(suggestionIndex(['ساده'], '')).toBe(-1)
  })
})

describe('TSV', () => {
  it('نقل‌قول، Tab و خط تازه داخل خانه، CRLF و خط خالی آخر', () => {
    expect(parseTsv('a\tb\r\nc\td\r\n')).toEqual([
      ['a', 'b'],
      ['c', 'd'],
    ])
    expect(parseTsv('"x\ty"\t"he said ""hi"""\n"line1\nline2"\tz')).toEqual([
      ['x\ty', 'he said "hi"'],
      ['line1\nline2', 'z'],
    ])
    expect(parseTsv('only')).toEqual([['only']])
    expect(toTsv([['a\tb', 'c'], ['"q"', 'd']])).toBe('"a\tb"\tc\r\n"""q"""\td')
  })

  it('سرستون و پانویس نگله شناخته می‌شوند', () => {
    expect(isHeaderLine(['شماره', 'رنگ متن', 'رنگ حاشیه', 'نقشه', 'محل بافت', 'مواد مصرفی', 'طول', 'عرض'], INTAKE)).toBe(true)
    expect(isHeaderLine(['3778', 'سفید', 'سفید', 'ساده', 'گبه', 'پشم', '2.08', '2.1'], INTAKE)).toBe(false)
    expect(isFooterLine(['', '1', 'شامل', '4', 'تخته', '', '', '13.51'])).toBe(true)
    expect(isFooterLine(['3778', 'سفید'])).toBe(false)
  })
})

describe('چسباندن (G.3)', () => {
  const header = ['شماره', 'رنگ متن', 'رنگ حاشیه', 'نقشه', 'محل بافت', 'مواد مصرفی', 'طول', 'عرض'].join('\t')

  it('سرستون رد و ستون‌ها با نام جفت می‌شوند؛ ارقام فارسی؛ یک پرسش برای همهٔ سانت‌ها', () => {
    const text = [
      header,
      ['۳۷۷۸', 'سفید', 'سفید', 'ساده', 'گبه', 'پشم', '۲۰۸', '۲۱۰'].join('\t'),
      ['۳۷۷۹', 'سفید', 'سفید', 'ساده', 'گبه', 'پشم', '2.30', '1.71'].join('\t'),
      ['', '1', 'شامل', '2', 'تخته', '', '', '8.30'].join('\t'),
      ['3911 - 3960', 'لاکی', 'لاکی', 'ساده', 'گبه', 'پشم', '40', '40'].join('\t'),
    ].join('\r\n')
    const plan = planPaste(text, INTAKE, 0, CTX)
    expect(plan.byHeader).toBe(true)
    expect(plan.headerSkipped).toBe(1)
    expect(plan.footerSkipped).toBe(1)
    expect(plan.rows).toBe(3)
    expect(plan.cmCount).toBe(4)
    const at = (row: number, key: string) => plan.cells.find((c) => c.row === row && c.key === key)
    expect(at(0, 'rug.serial')?.value).toBe('3778')
    expect(at(0, 'rug.origin')?.value).toBe('گبه')
    expect(at(0, 'rug.intakeLength')?.cm).toEqual({ yes: 2.08, no: undefined, noError: expect.any(String) })
    expect(at(1, 'rug.intakeLength')?.value).toBe(2.3)
    expect(at(2, 'rug.serial')?.value).toBe('3911 - 3960')
    expect(at(2, 'rug.intakeWidth')?.cm?.yes).toBe(0.4)
    // ستون فروشنده و تعداد در چسباندن نیامده‌اند
    expect(plan.cells.some((c) => c.key === 'batch.seller')).toBe(false)
  })

  it('بی سرستون: از ستون فعال به ترتیب، خانهٔ فقط‌خواندنی نادیده', () => {
    const lengthCol = INTAKE.findIndex((c) => c.key === 'rug.intakeLength')
    const plan = planPaste('2.08\t2.10\t99\t9000000', INTAKE, lengthCol, CTX)
    expect(plan.byHeader).toBe(false)
    expect(plan.cells.map((c) => [c.key, c.value])).toEqual([
      ['rug.intakeLength', 2.08],
      ['rug.intakeWidth', 2.1],
      ['purchase.price', { rate: 9_000_000, basis: CostBasis.PerM2 }],
    ])
  })

  it('«208×210» در یک خانه طول و عرض را با یک پرسش پر می‌کند', () => {
    const lengthCol = INTAKE.findIndex((c) => c.key === 'rug.intakeLength')
    const plan = planPaste('208×210', INTAKE, lengthCol, CTX)
    expect(plan.cells.map((c) => [c.key, c.cm?.yes])).toEqual([
      ['rug.intakeLength', 2.08],
      ['rug.intakeWidth', 2.1],
    ])
  })

  it('«208×210» در ستون عرض هم طول و عرض را پر می‌کند (عدد اول طول است)', () => {
    const widthCol = INTAKE.findIndex((c) => c.key === 'rug.intakeWidth')
    const plan = planPaste('208×210\n2.08×1.40', INTAKE, widthCol, CTX)
    expect(plan.cmCount).toBe(2)
    expect(plan.cells.map((c) => [c.row, c.key, c.cm?.yes ?? c.value])).toEqual([
      [0, 'rug.intakeLength', 2.08],
      [0, 'rug.intakeWidth', 2.1],
      [1, 'rug.intakeLength', 2.08],
      [1, 'rug.intakeWidth', 1.4],
    ])
  })

  it('خانهٔ نادرست خطا می‌گیرد، بقیه می‌نشینند', () => {
    const plan = planPaste('3960-3911\tسفید', INTAKE, 0, CTX)
    expect(plan.cells[0].error).toBeTruthy()
    expect(plan.cells[1]).toMatchObject({ key: 'batch.seller', value: 'سفید' })
  })
})
