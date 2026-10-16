import { afterEach, describe, expect, it } from 'vitest'
import { resetAppContext } from '@/lib/context'
import { message } from '@/lib/messages'
import { DeclaredRateSource, type Bale, type BaleRow, type BaleSummary, type LinesBatchResult } from '@/lib/types/shipments'
import type { GridColumn } from '@/lib/types/grid'
import {
  applyBatchResult,
  areaCellText,
  boardColumns,
  cellText,
  currentBaleKey,
  faFixed,
  footerText,
  inverseOps,
  maxBaleNumber,
  measureOp,
  parseSizeText,
  readCurrentBale,
  reorderSeq,
  resolveCurrent,
  shiftsOthers,
  sizeChangeBadge,
  targetState,
  targetText,
  upperBound,
  writeCurrentBale,
  type KeyValueStorage,
} from './baleModel'
import { rugIdFromUrl } from './useBalingBoard'

afterEach(() => resetAppContext({}))

/** نگلهٔ ۱ نمونه (tests/fixtures/sample-shipment.json): ۴ فرش، ۱۳٫۵۱ متر، ۶۰۷٫۹۵ دلار. */
interface SampleLine {
  bale: number
  serial: string
  qty: number
  cachedArea: number
  cachedUsd: number
}
const sample = import.meta.glob<{ default: { lines: SampleLine[] } }>('../../../../../../tests/fixtures/sample-shipment.json', { eager: true })

function row(partial: Partial<BaleRow> = {}): BaleRow {
  return {
    lineId: 'l1',
    lineVersion: 1,
    rugId: 'r1',
    rugVersion: 10,
    seq: 1,
    serial: '3778',
    qty: 1,
    fieldColor: 'سفید',
    borderColor: 'سفید',
    pattern: 'ساده',
    origin: 'گبه',
    material: 'پشم',
    intakeLength: 2.08,
    intakeWidth: 2.1,
    finalLength: null,
    finalWidth: null,
    sizeChangeCm: null,
    areaM2: 4.37,
    lineAreaM2: 4.37,
    usdPerM2: 45,
    rateSource: DeclaredRateSource.Table,
    pieceUsd: 196.65,
    lineUsd: 196.65,
    baleId: 'b1',
    baleNumber: 1,
    usdPerM2Override: null,
    sku: 'R-1',
    declaredKey: 'گبه',
    ...partial,
  }
}

function bale(id: string, number: number, rows: BaleRow[] = []): Bale {
  return { id, number, version: 1, weightKg: null, notes: null, pieces: rows.length, areaM2: 0, usdTotal: 0, targetAreaM2: 13, rows }
}

function summary(id: string, number: number, pieces = 0, areaM2 = 0, usdTotal = 0): BaleSummary {
  return { id, number, version: 2, lines: pieces, pieces, areaM2, usdTotal, weightKg: null }
}

describe('پانویس نگله (H.1)', () => {
  it('«نگلهٔ ۱ · شامل ۴ تخته · ۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار»', () => {
    expect(footerText({ number: 1, pieces: 4, areaM2: 13.51, usdTotal: 607.95 })).toBe(
      'نگلهٔ ۱ · شامل ۴ تخته · ۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار',
    )
  })

  it('دو رقم اعشار ثابت و «٬» هزارگان، مثل اکسل', () => {
    expect(faFixed(13.5)).toBe('۱۳٫۵۰')
    expect(faFixed(20322.5)).toBe('۲۰٬۳۲۲٫۵۰')
    expect(faFixed(null)).toBe('—')
  })

  it.skipIf(Object.keys(sample).length === 0)('جمع نگلهٔ ۱ نمونه با قاعدهٔ دسته همان پانویس را می‌دهد', async () => {
    const { add } = await import('@/lib/num')
    const lines = Object.values(sample)[0].default.lines.filter((l) => l.bale === 1)
    const area = lines.reduce((sum, l) => add(sum, l.cachedArea), add(0, 0))
    const usd = lines.reduce((sum, l) => add(sum, l.cachedUsd), add(0, 0))
    const pieces = lines.reduce((n, l) => n + l.qty, 0)
    expect(footerText({ number: 1, pieces, areaM2: Number(faFixed(area).replace(/[۰-۹]/g, (d) => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).replace('٫', '.')), usdTotal: Number(String(usd.units) ) / 10 ** usd.scale })).toBe(
      'نگلهٔ ۱ · شامل ۴ تخته · ۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار',
    )
  })

  it('واژهٔ «عدل» کارگاه: «عدل ۳»', () => {
    resetAppContext({ terms: { bale: 'عدل', bales: 'عدل‌ها', baleOf: 'عدل', baleFooter: 'عدل شماره' } })
    expect(footerText({ number: 3, pieces: 2, areaM2: 8, usdTotal: 360 })).toBe('عدل ۳ · شامل ۲ تخته · ۸٫۰۰ متر · ۳۶۰٫۰۰ دلار')
    expect(message('I_BOARD_ADD_TO', { n: 3 })).toBe('افزودن به عدل ۳')
    expect(message('I_BOARD_NEXT')).toBe('عدل بعد')
  })
})

describe('نوار هدف (C.2): ۱۳ متر با ۱۰٪', () => {
  it('مرز بالا دقیقاً ۱۴٫۳ است، بی خطای ممیز شناور', () => {
    expect(faFixed(upperBound(13, 10))).toBe('۱۴٫۳۰')
  })

  it('کمتر از ۱۱٫۷ خنثی، تا ۱۴٫۳ در بازه، بالای ۱۴٫۳ هشدار', () => {
    expect(targetState(11.69, 13, 10)).toBe('below')
    expect(targetState(11.7, 13, 10)).toBe('within')
    expect(targetState(13.51, 13, 10)).toBe('within')
    expect(targetState(14.3, 13, 10)).toBe('within')
    expect(targetState(14.31, 13, 10)).toBe('above')
  })

  it('بی هدف هیچ نواری نیست', () => {
    expect(targetState(20, null, 10)).toBe('none')
    expect(targetState(20, 0, 10)).toBe('none')
  })

  it('متن «بیشتر» هدف ۱۳ متر را نام می‌برد', () => {
    expect(targetText({ number: 3, pieces: 5, areaM2: 15.1, usdTotal: 0 }, 13, 10)).toBe(
      'نگلهٔ ۳ شد ۱۵٫۱ متر؛ از ۱۳ متر هدف بیشتر است.',
    )
  })
})

describe('نگلهٔ جاری (C.2)', () => {
  function memory(): KeyValueStorage & { data: Map<string, string> } {
    const data = new Map<string, string>()
    return { data, getItem: (k) => data.get(k) ?? null, setItem: (k, v) => void data.set(k, v), removeItem: (k) => void data.delete(k) }
  }

  it('بی نگله ۱؛ پیش‌فرض بزرگ‌ترین نگله', () => {
    expect(resolveCurrent(null, [])).toBe(1)
    expect(resolveCurrent(null, [{ number: 1 }, { number: 3 }])).toBe(3)
  })

  it('«نگلهٔ بعد» (بزرگ‌ترین + ۱) پس از بار می‌ماند؛ شمارهٔ بی‌معنا نه', () => {
    expect(resolveCurrent(4, [{ number: 1 }, { number: 3 }])).toBe(4)
    expect(resolveCurrent(9, [{ number: 1 }, { number: 3 }])).toBe(3)
    expect(resolveCurrent(2, [{ number: 1 }, { number: 3 }])).toBe(2)
  })

  it('برای هر کاربر و هر محموله؛ storage خراب چیزی را نمی‌شکند', () => {
    const key = currentBaleKey('t', 'u', 's')
    expect(key).toBe('rugs:current-bale:t:u:s')
    expect(currentBaleKey(null, 'u', 's')).toBeNull()
    const store = memory()
    writeCurrentBale(key, 3, store)
    expect(readCurrentBale(key, store)).toBe(3)
    const broken: KeyValueStorage = {
      getItem: () => {
        throw new Error('blocked')
      },
      setItem: () => {
        throw new Error('blocked')
      },
      removeItem: () => {},
    }
    expect(() => writeCurrentBale(key, 2, broken)).not.toThrow()
    expect(readCurrentBale(key, broken)).toBeNull()
  })

  it('بزرگ‌ترین شماره', () => {
    expect(maxBaleNumber([])).toBe(0)
    expect(maxBaleNumber([{ number: 2 }, { number: 7 }])).toBe(7)
  })
})

describe('نشاندن نتیجهٔ lines/batch', () => {
  it('اولین افزودن با شمارهٔ تازه نگله را می‌سازد (بی نگلهٔ خالی از پیش)', () => {
    const added = row({ lineId: 'l9', baleId: 'b2', baleNumber: 2 })
    const result: LinesBatchResult = {
      results: [{ clientOpId: 'c', ok: true, errors: [], row: added, prompt: null, op: 'add' }],
      bales: [summary('b1', 1, 1, 4.37, 196.65), summary('b2', 2, 1, 4.37, 196.65)],
      totals: { bales: 2, lines: 2, pieces: 2, areaM2: 8.74, usdTotal: 393.3 },
    }
    const next = applyBatchResult([bale('b1', 1, [row()])], result, [{ op: 'add', baleNumber: 2, serial: '3779' }])
    expect(next.map((b) => b.number)).toEqual([1, 2])
    expect(next[1].rows.map((r) => r.lineId)).toEqual(['l9'])
    expect(next[1].pieces).toBe(1)
  })

  it('خروج ردیف را برمی‌دارد و انتقال به نگلهٔ دیگر می‌برد', () => {
    const bales = [bale('b1', 1, [row(), row({ lineId: 'l2', rugId: 'r2', seq: 2 })]), bale('b2', 2)]
    const moved = row({ lineId: 'l2', rugId: 'r2', baleId: 'b2', baleNumber: 2, seq: 1, lineVersion: 5 })
    const result: LinesBatchResult = {
      results: [
        { clientOpId: 'a', ok: true, errors: [], row: row(), prompt: null, op: 'remove' },
        { clientOpId: 'b', ok: true, errors: [], row: moved, prompt: null, op: 'move' },
      ],
      bales: [summary('b1', 1), summary('b2', 2, 1)],
      totals: { bales: 2, lines: 1, pieces: 1, areaM2: 0, usdTotal: 0 },
    }
    const next = applyBatchResult(bales, result, [
      { op: 'remove', lineId: 'l1' },
      { op: 'move', lineId: 'l2', baleId: 'b2' },
    ])
    expect(next[0].rows).toEqual([])
    expect(next[1].rows[0].lineVersion).toBe(5)
  })

  it('نگلهٔ حذف‌شده از خلاصه‌ها برداشته می‌شود و شمارهٔ تازه به ردیف‌ها می‌رسد', () => {
    const next = applyBatchResult(
      [bale('b1', 1, [row()]), bale('b2', 2)],
      { results: [], bales: [summary('b1', 3, 1)], totals: { bales: 1, lines: 1, pieces: 1, areaM2: 0, usdTotal: 0 } },
      [],
    )
    expect(next.map((b) => b.id)).toEqual(['b1'])
    expect(next[0].rows[0].baleNumber).toBe(3)
  })
})

describe('«برگرد» و ترتیب', () => {
  const ids = (() => {
    let i = 0
    return () => `id-${++i}`
  })()

  it('«برگردان» خروج: همان نگله و همان Seq (C.3)', () => {
    const ops = inverseOps({ kind: 'remove', rugId: 'r1', baleId: 'b1', baleNumber: 1, seq: 3, serial: '3778' }, [], () => true, ids)
    expect(ops).toEqual([{ op: 'add', clientOpId: expect.any(String), rugId: 'r1', baleId: 'b1', seq: 3 }])
    expect(shiftsOthers(ops)).toBe(true)
  })

  it('نگلهٔ پاک‌شده: با شماره برمی‌گردد (اولین فرشش دوباره می‌سازدش)', () => {
    const ops = inverseOps({ kind: 'remove', rugId: 'r1', baleId: 'gone', baleNumber: 4, seq: 1, serial: null }, [], () => false, ids)
    expect(ops[0]).toMatchObject({ op: 'add', baleNumber: 4, seq: 1 })
    expect(ops[0].baleId).toBeUndefined()
  })

  it('«برگرد» اندازه: اندازهٔ قبلی یا پاک کردن، با نسخهٔ تازهٔ فرش', () => {
    const rows = [row({ rugVersion: 77, finalLength: 2, finalWidth: 2 })]
    expect(inverseOps({ kind: 'measure', rugId: 'r1', before: { kind: 'clear' }, serial: null }, rows, () => true, ids)[0]).toMatchObject({
      op: 'measure',
      rugId: 'r1',
      rugVersion: 77,
      clear: true,
    })
    expect(
      inverseOps({ kind: 'measure', rugId: 'r1', before: { kind: 'pair', length: 3.3, width: 2.5 }, serial: null }, rows, () => true, ids)[0],
    ).toMatchObject({ finalLength: 3.3, finalWidth: 2.5 })
  })

  it('گروه («بقیه همان اندازهٔ اول»): یک قدم برای همه', () => {
    const rows = [row(), row({ lineId: 'l2', rugId: 'r2' })]
    const ops = inverseOps(
      {
        kind: 'group',
        entries: [
          { kind: 'measure', rugId: 'r1', before: { kind: 'clear' }, serial: null },
          { kind: 'measure', rugId: 'r2', before: { kind: 'clear' }, serial: null },
        ],
      },
      rows,
      () => true,
      ids,
    )
    expect(ops).toHaveLength(2)
  })

  it('بالا/پایین در همان نگله: Seq ردیف قبلی، یا Seq ردیف بعدی + ۱', () => {
    const rows = [{ lineId: 'a', seq: 1 }, { lineId: 'b', seq: 2 }, { lineId: 'c', seq: 5 }]
    expect(reorderSeq(rows, 'b', 'up')).toBe(1)
    expect(reorderSeq(rows, 'b', 'down')).toBe(6)
    expect(reorderSeq(rows, 'a', 'up')).toBeNull()
    expect(reorderSeq(rows, 'c', 'down')).toBeNull()
  })

  it('کار اندازه: دقیقاً یکی از جفت، همان، پاک', () => {
    expect(measureOp(row(), { kind: 'same' })).toEqual({ op: 'measure', rugId: 'r1', rugVersion: 10, sameAsIntake: true })
    expect(measureOp(row(), { kind: 'pair', length: 3.3, width: 2.5 })).toMatchObject({ finalLength: 3.3, finalWidth: 2.5 })
  })
})

describe('ستون‌های جدول (G.2)', () => {
  const terms = { bale: 'نگله' }

  function col(key: string, label: string): GridColumn {
    return { key, label, type: 'text', width: 10, sticky: false, readOnly: false, required: false, options: null, money: false }
  }

  it('پیش‌فرض: ترتیب G.2؛ در حالت ساده فقط طول و عرض ایستگاه Tab‌اند', () => {
    const cols = boardColumns(null, { terms, simple: true, canRate: false })
    expect(cols.map((c) => c.key)).toEqual([
      'bale', 'serial', 'qty', 'fieldColor', 'borderColor', 'pattern', 'origin', 'material',
      'intake', 'finalLength', 'finalWidth', 'area', 'rate', 'usd',
    ])
    expect(cols.filter((c) => c.tabStop).map((c) => c.key)).toEqual(['finalLength', 'finalWidth'])
  })

  it('حالت کامل: ستون‌های توصیفی هم ایستگاه‌اند', () => {
    const cols = boardColumns(null, { terms, simple: false, canRate: true })
    expect(cols.filter((c) => c.tabStop).map((c) => c.key)).toEqual([
      'fieldColor', 'borderColor', 'pattern', 'origin', 'material', 'finalLength', 'finalWidth',
    ])
    expect(cols.find((c) => c.key === 'rate')?.edit).toBe('rate')
  })

  it('چیدمان کارگاه (کلیدهای BalesGridSource): ترتیب و برچسب، ستون ناشناخته نادیده', () => {
    const layout = [
      col('bale.number', 'عدل'),
      col('rug.serial', 'شماره'),
      col('rug.intakeSize', 'اندازهٔ اول'),
      col('rug.finalLength', 'طول'),
      col('rug.finalWidth', 'عرض'),
      col('line.area', 'مساحت'),
      col('line.usd', 'جمع کل دلار'),
      col('something.else', 'نامعلوم'),
    ]
    const cols = boardColumns(layout, { terms, simple: true, canRate: false })
    expect(cols.map((c) => c.key)).toEqual(['bale', 'serial', 'intake', 'finalLength', 'finalWidth', 'area', 'usd'])
    expect(cols[0].label).toBe('عدل')
  })

  it('چیدمانی که طول/عرض را پنهان کرده: اندازهٔ نهایی باز هم هست', () => {
    const cols = boardColumns([col('rug.serial', 'شماره'), col('rug.pattern', 'نقشه'), col('line.area', 'مساحت')], {
      terms,
      simple: true,
      canRate: false,
    })
    expect(cols.map((c) => c.key)).toEqual(['serial', 'pattern', 'area', 'finalLength', 'finalWidth'])
  })

  it('پیش‌نمایش «اندازه‌گیری»: شماره · اندازهٔ اول · طول · عرض · مساحت', () => {
    expect(boardColumns(null, { terms, simple: true, canRate: false, measuring: true }).map((c) => c.key)).toEqual([
      'serial', 'intake', 'finalLength', 'finalWidth', 'area',
    ])
  })

  it('واژهٔ کارگاه در برچسب ستون نگله', () => {
    expect(boardColumns(null, { terms: { bale: 'عدل' }, simple: true, canRate: false })[0].label).toBe('عدل')
  })

  it('خانه‌ها: اندازهٔ اول در حالت ساده فقط وقتی فرق دارد؛ مساحت دسته «۰٫۱۶ × ۵۰ = ۸٫۰۰»', () => {
    expect(cellText('intake', row(), true)).toBe('')
    expect(cellText('intake', row(), false)).toBe('۲٫۰۸ × ۲٫۱۰')
    expect(cellText('intake', row({ finalLength: 2, finalWidth: 2.1 }), true)).toBe('۲٫۰۸ × ۲٫۱۰')
    expect(areaCellText({ areaM2: 0.16, lineAreaM2: 8, qty: 50 })).toBe('۰٫۱۶ × ۵۰ = ۸٫۰۰')
    expect(cellText('serial', row({ serial: '3911 - 3960' }), true)).toBe('۳۹۱۱ - ۳۹۶۰')
  })
})

describe('خانهٔ اندازه (G.4)', () => {
  it('«۲٫۰۸» و «2,08» متر؛ «208» پرسش سانت با جواب ۲٫۰۸', () => {
    expect(parseSizeText('۲٫۰۸', false)).toEqual({ status: 'ok', value: '2.08' })
    expect(parseSizeText('2,08', false)).toEqual({ status: 'ok', value: '2.08' })
    const ask = parseSizeText('208', false)
    expect(ask.status).toBe('ask')
    if (ask.status === 'ask') {
      expect(ask.message).toBe('۲۰۸ ← ۲٫۰۸ متر؟')
      expect(ask.choices[0].value).toBe('2.08')
    }
  })

  it('«25» می‌پرسد متر یا سانت، سانت اول', () => {
    const ask = parseSizeText('25', false)
    expect(ask.status).toBe('ask')
    if (ask.status === 'ask') expect(ask.choices[0].value).toBe('0.25')
  })

  it('«208×210» در خانهٔ طول هر دو را پر می‌کند (با یک پرسش)', () => {
    const pair = parseSizeText('2.08×2.10', true)
    expect(pair).toEqual({ status: 'pair', values: ['2.08', '2.1'] })
    const ask = parseSizeText('208×210', true)
    expect(ask.status).toBe('ask')
    if (ask.status === 'ask') expect(ask.choices[0].values).toEqual(['2.08', '2.1'])
  })

  it('خالی، و ضلع نادرست', () => {
    expect(parseSizeText('', false)).toEqual({ status: 'empty' })
    expect(parseSizeText('0.01', false).status).toBe('error')
  })
})

describe('نشان تغییر اندازه (C.4)', () => {
  it('«↓۵ سانت» با برچسب صفحه‌خوان', () => {
    expect(sizeChangeBadge(-5)).toEqual({ text: '↓۵ سانت', label: '۵ سانت کوچک‌تر شده', down: true })
    expect(sizeChangeBadge(2)?.text).toBe('↑۲ سانت')
    expect(sizeChangeBadge(0)).toBeNull()
    expect(sizeChangeBadge(null)).toBeNull()
  })
})

describe('اسکن برچسب قدیمی', () => {
  it('نشانی صفحهٔ فرش همین سایت → شناسهٔ فرش؛ سایت دیگر یا شماره → null', () => {
    const id = '3f2c1d4e-0000-4000-8000-000000000001'
    expect(rugIdFromUrl(`https://app.test/Rugs/Details/${id}`, 'https://app.test')).toBe(id)
    expect(rugIdFromUrl(`https://evil.test/Rugs/Details/${id}`, 'https://app.test')).toBeNull()
    expect(rugIdFromUrl('3778', 'https://app.test')).toBeNull()
  })
})
