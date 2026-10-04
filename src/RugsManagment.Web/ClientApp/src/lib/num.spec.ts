import { describe, expect, it } from 'vitest'
import numSource from './num.ts?raw'
import {
  add,
  cmp,
  convertPiece,
  dec,
  hasAtMostDecimals,
  lineArea,
  lineMoney,
  lineUsd,
  mul,
  pieceArea,
  pieceMoney,
  pieceMoneyPerPiece,
  pieceUsd,
  round,
  snap,
  sub,
  toFixedString,
  toNumber,
  toPlainString,
  toSquareFeet,
  tryDec,
} from './num'

/** بردارهای مشترک با xUnit (`tests/fixtures/math-vectors.json`)، اگر فاز سکو آن را گذاشته باشد. */
interface MathVector {
  id: string
  kind: 'area' | 'usd' | 'money'
  scale: number
  op: string
  [key: string]: unknown
}
const shared = import.meta.glob<{ default: unknown }>('../../../../../tests/fixtures/math-vectors.json', { eager: true })

/** رونوشت ۱۶۱ ردیف اظهارنامهٔ نمونه (فاز سکو)؛ مساحت و دلار ذخیره‌شدهٔ اکسل برای هر تخته. */
interface SampleLine {
  row: number
  bale: number
  qty: number
  length: number
  width: number
  usdPerM2: number
  cachedArea: number
  cachedUsd: number
}
interface SampleShipment {
  totals: { lines: number; pieces: number; areaM2: number; usd: number }
  lines: SampleLine[]
  summary: { bale: number; pieces: number; areaM2: number; usd: number }[]
}
const sample = import.meta.glob<{ default: SampleShipment }>('../../../../../tests/fixtures/sample-shipment.json', { eager: true })

const s = (d: Parameters<typeof toPlainString>[0], min = 0) => toPlainString(d, min)

describe('round — نصفه دور از صفر، هرگز بانکی', () => {
  it.each([
    ['2.675', 2, '2.68'],
    ['-2.675', 2, '-2.68'],
    ['0.005', 2, '0.01'],
    ['2.665', 2, '2.67'],
    ['2.5', 0, '3'],
    ['3.5', 0, '4'],
    ['-2.5', 0, '-3'],
    ['7.288', 0, '7'],
    ['765454.14', 0, '765454'],
    ['1.2345', 3, '1.235'],
  ])('%s با %i رقم → %s', (value, decimals, expected) => {
    expect(s(round(value, decimals))).toBe(expected)
  })

  it('رقم‌های ثابت را نگه می‌دارد', () => {
    expect(toFixedString('2.5', 2)).toBe('2.50')
    expect(toFixedString('0.3', 2)).toBe('0.30')
    expect(toFixedString(8, 2)).toBe('8.00')
  })
})

describe('pieceArea — ROUND(L×W, 2) نمونهٔ اظهارنامه', () => {
  it.each([
    // میانه‌های فایل نمونه (L.1)
    ['1.93', '1.50', '2.90'],
    ['2.30', '1.65', '3.80'],
    ['1.51', '0.50', '0.76'],
    ['0.77', '0.50', '0.39'],
    ['1.49', '0.50', '0.75'],
    ['0.79', '0.50', '0.40'],
    ['2.50', '0.37', '0.93'],
    ['1.50', '0.83', '1.25'],
    ['1.05', '0.50', '0.53'],
    ['0.45', '0.45', '0.20'],
    ['0.40', '0.40', '0.16'],
  ])('%s × %s → %s', (l, w, expected) => {
    expect(s(pieceArea(l, w), 2)).toBe(expected)
  })

  it('ضرب ممیز شناور جاوااسکریپت را دور می‌زند (2.30*1.65 = 3.7949999…)', () => {
    expect(2.3 * 1.65).not.toBe(3.795)
    expect(s(pieceArea(2.3, 1.65), 2)).toBe('3.80')
  })

  it('عدد اکسل با خطای ممیز (2.2999999999999998) همان ۳٫۸۰ می‌شود', () => {
    expect(s(pieceArea(snap(2.2999999999999998), '1.65'), 2)).toBe('3.80')
    expect(s(snap(2.2999999999999998), 2)).toBe('2.30')
    expect(s(snap(0.1 + 0.2), 2)).toBe('0.30')
  })
})

describe('قاعدهٔ دسته — اول هر تخته، بعد × تعداد', () => {
  const area = pieceArea('0.40', '0.40')

  it('دستهٔ ۵۰ تختهٔ ۰٫۴۰×۰٫۴۰ → ۸٫۰۰ متر', () => {
    expect(s(lineArea(area, 50), 2)).toBe('8.00')
  })

  it('دلار: ۰٫۱۶ × ۴۵٫۵۵ → ۷٫۲۹ هر تخته → ۳۶۴٫۵۰ (نه ۳۶۴٫۴۰)', () => {
    const piece = pieceUsd(area, '45.55')
    expect(s(piece, 2)).toBe('7.29')
    expect(s(lineUsd(piece, 50), 2)).toBe('364.50')
  })

  it('مبلغ متری ۴۵٫۵۵ با صفر رقم اعشار → ۷ × ۵۰ = ۳۵۰؛ با دو رقم → ۳۶۴٫۵۰', () => {
    expect(s(lineMoney(pieceMoney('45.55', area, 0), 50))).toBe('350')
    expect(s(lineMoney(pieceMoney('45.55', area, 2), 50), 2)).toBe('364.50')
  })

  it('۹٬۰۰۰٬۰۰۰ × ۰٫۱۶ → ۱٬۴۴۰٬۰۰۰ هر تخته', () => {
    const piece = pieceMoney('9000000', area, 0)
    expect(s(piece)).toBe('1440000')
    expect(s(lineMoney(piece, 50))).toBe('72000000')
  })

  it('ارز به ازای هر تخته: ۷٫۲۹ × ۱۰۵٬۰۰۰٫۵ × ۵۰ → ۳۸٬۲۷۲٬۷۰۰ (نه ۳۸٬۲۷۲٬۶۸۲)', () => {
    const piece = convertPiece('7.29', '105000.5', 0)
    expect(s(piece)).toBe('765454')
    expect(s(lineMoney(piece, 50))).toBe('38272700')
    expect(s(round(mul('364.50', '105000.5'), 0))).toBe('38272682')
  })

  it('نرخ دانه‌ای کسری: ROUND(12,345.5) = 12,346 × 3 = 37,038', () => {
    expect(s(lineMoney(pieceMoneyPerPiece('12345.5', 0), 3))).toBe('37038')
  })

  it('درصد یک ردیف: ۲٪ از ۱۵٬۰۰۰٬۰۰۰ = ۳۰۰٬۰۰۰', () => {
    expect(s(round(mul('15000000', '0.02'), 0))).toBe('300000')
  })

  it('تعداد منفی خطاست', () => {
    expect(() => lineArea(area, -1)).toThrow(RangeError)
    expect(() => lineArea(area, 1.5)).toThrow(RangeError)
  })

  it('فوت مربع با ضریب دقیق', () => {
    expect(s(toSquareFeet('1'))).toBe('10.7639104')
    expect(s(round(toSquareFeet('2.90'), 2), 2)).toBe('31.22')
  })
})

describe('خواندن و نوشتن', () => {
  it('رشته، عدد، BigInt و توان ده', () => {
    expect(s(dec('2.08'))).toBe('2.08')
    expect(s(dec(2.08))).toBe('2.08')
    expect(s(dec(10n))).toBe('10')
    expect(s(dec('1e21'))).toBe('1000000000000000000000')
    expect(s(dec(1.5e-7))).toBe('0.00000015')
    expect(s(dec('-0.50'))).toBe('-0.5')
    expect(s(dec('-0'))).toBe('0')
    expect(s(dec('.5'))).toBe('0.5')
  })

  it('ورودی نامعتبر', () => {
    expect(tryDec('')).toBeNull()
    expect(tryDec('abc')).toBeNull()
    expect(tryDec('1.2.3')).toBeNull()
    expect(tryDec(Number.NaN)).toBeNull()
    expect(() => dec('x')).toThrow(RangeError)
  })

  it('جمع، تفریق و مقایسه دقیق', () => {
    expect(s(add('0.1', '0.2'))).toBe('0.3')
    expect(s(sub('1', '0.99'))).toBe('0.01')
    expect(cmp('2.50', '2.5')).toBe(0)
    expect(cmp('-1', '0')).toBe(-1)
    expect(hasAtMostDecimals('2.080', 2)).toBe(true)
    expect(hasAtMostDecimals('2.081', 2)).toBe(false)
  })

  it('اعداد بزرگ‌تر از ۲^۵۳ دقیق می‌مانند', () => {
    expect(s(mul('9007199254740993', '1'))).toBe('9007199254740993')
    expect(s(add('999999999999999999', '1'))).toBe('1000000000000000000')
  })

  it('toNumber فقط در مرز', () => {
    expect(toNumber(pieceArea('2.30', '1.65'))).toBe(3.8)
  })
})

describe('بی‌ضرب ممیز شناور', () => {
  it('num.ts هیچ گرد یا ضرب Number ندارد', () => {
    const code = numSource
      .split('\n')
      .filter((line) => !line.trim().startsWith('*') && !line.trim().startsWith('//') && !line.trim().startsWith('/**'))
      .join('\n')
    expect(code).not.toMatch(/Math\.(round|floor|ceil)\(/)
    expect(code).not.toMatch(/\.toFixed\(/)
    expect(code).not.toMatch(/parseFloat\(/)
    // تنها ضرب‌ها روی BigInt است: «units *»، «* pow10»، «* 2n»، «* BigInt»
    for (const match of code.matchAll(/([\w.)\]]+)\s\*\s([\w.(]+)/g)) {
      const [, left, right] = match
      const bigintSide = /units|pow10|BigInt|^\d+n$|^q$|^r$|magnitude/.test(left) || /units|pow10|BigInt|^\d+n$|factor/.test(right)
      expect(bigintSide, `ضرب مشکوک: ${match[0]}`).toBe(true)
    }
  })
})

describe('اظهارنامهٔ نمونه (tests/fixtures/sample-shipment.json)', () => {
  const data = Object.values(sample)[0]?.default

  it.skipIf(!data)('هر ردیف ستون J (مساحت) و L (دلار) اکسل را می‌سازد و جمع‌ها می‌خوانند', () => {
    const shipment = data as SampleShipment
    let pieces = 0
    let area = dec(0)
    let usd = dec(0)
    const perBale = new Map<number, { pieces: number; area: ReturnType<typeof dec>; usd: ReturnType<typeof dec> }>()

    for (const line of shipment.lines) {
      const piece = pieceArea(snap(line.length), snap(line.width))
      expect(cmp(piece, line.cachedArea), `ردیف ${line.row}: مساحت ${toPlainString(piece)}`).toBe(0)
      const pUsd = pieceUsd(piece, line.usdPerM2)
      expect(cmp(pUsd, line.cachedUsd), `ردیف ${line.row}: دلار ${toPlainString(pUsd)}`).toBe(0)

      const lArea = lineArea(piece, line.qty)
      const lUsd = lineUsd(pUsd, line.qty)
      pieces += line.qty
      area = add(area, lArea)
      usd = add(usd, lUsd)
      const bale = perBale.get(line.bale) ?? { pieces: 0, area: dec(0), usd: dec(0) }
      perBale.set(line.bale, { pieces: bale.pieces + line.qty, area: add(bale.area, lArea), usd: add(bale.usd, lUsd) })
    }

    expect(shipment.lines).toHaveLength(shipment.totals.lines)
    expect(pieces).toBe(shipment.totals.pieces)
    expect(cmp(area, shipment.totals.areaM2), toPlainString(area)).toBe(0)
    expect(cmp(usd, shipment.totals.usd), toPlainString(usd)).toBe(0)
    for (const s of shipment.summary) {
      const b = perBale.get(s.bale)
      expect(b, `نگلهٔ ${s.bale}`).toBeDefined()
      expect(b!.pieces, `نگلهٔ ${s.bale}`).toBe(s.pieces)
      expect(cmp(b!.area, s.areaM2), `نگلهٔ ${s.bale} مساحت`).toBe(0)
      expect(cmp(b!.usd, s.usd), `نگلهٔ ${s.bale} دلار`).toBe(0)
    }
  })
})

describe('بردارهای مشترک با سرور (tests/fixtures/math-vectors.json)', () => {
  const file = Object.values(shared)[0]?.default as { vectors?: MathVector[] } | undefined
  const vectors: MathVector[] = file?.vectors ?? []
  const str = (v: unknown) => String(v)

  /** نتیجهٔ یک بردار: [تخته، ردیف] یا برای round/snap فقط [نتیجه]. */
  function evaluate(v: MathVector): { piece: string; line?: string } {
    const qty = typeof v.qty === 'number' ? v.qty : 1
    switch (v.op) {
      case 'round':
        return { piece: toFixedString(str(v.value), v.scale) }
      case 'snap':
        return { piece: toFixedString(snap(v.raw as number, v.scale), v.scale) }
      case 'pieceArea': {
        const piece = pieceArea(str(v.length), str(v.width))
        return { piece: toFixedString(piece, 2), line: toFixedString(lineArea(piece, qty), 2) }
      }
      case 'snapArea': {
        const piece = pieceArea(snap(v.lengthRaw as number, 2), snap(v.widthRaw as number, 2))
        return { piece: toFixedString(piece, 2), line: toFixedString(lineArea(piece, qty), 2) }
      }
      case 'pieceUsd': {
        const piece = pieceUsd(str(v.area), str(v.rate))
        return { piece: toFixedString(piece, 2), line: toFixedString(lineUsd(piece, qty), 2) }
      }
      case 'pieceMoney': {
        const piece = pieceMoney(str(v.rate), str(v.area), v.scale)
        return { piece: toFixedString(piece, v.scale), line: toFixedString(lineMoney(piece, qty), v.scale) }
      }
      case 'perPiece': {
        const piece = pieceMoneyPerPiece(str(v.rate), v.scale)
        return { piece: toFixedString(piece, v.scale), line: toFixedString(lineMoney(piece, qty), v.scale) }
      }
      case 'convertPiece': {
        const piece = convertPiece(str(v.amount), str(v.rate), v.scale)
        return { piece: toFixedString(piece, v.scale), line: toFixedString(lineMoney(piece, qty), v.scale) }
      }
      default:
        throw new Error(`عمل ناشناخته در math-vectors.json: ${String(v.op)}`)
    }
  }

  it.skipIf(vectors.length === 0)('هر بردار همان نتیجهٔ xUnit را می‌دهد', () => {
    for (const v of vectors) {
      const result = evaluate(v)
      const expectedPiece = v.expected ?? v.piece
      expect(cmp(result.piece, str(expectedPiece)), `${String(v.id)}: ${result.piece}`).toBe(0)
      if (v.line !== undefined) expect(cmp(result.line ?? result.piece, str(v.line)), `${String(v.id)} (ردیف)`).toBe(0)
    }
    expect(vectors.length).toBeGreaterThan(20)
  })
})
