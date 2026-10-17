/**
 * خواندن و نوشتن خانه‌های جدول — بی Vue و بی DOM، تا همه‌اش آزمون واحد داشته باشد (L.3 `gridParse.spec.ts`).
 *
 * - متن تایپ‌شده یا چسبانده‌شدهٔ هر خانه بر اساس نوع ستون (G.2): شماره و دسته (آینهٔ `Domain/Common/SerialNumber`)،
 *   تعداد، اندازه با قاعدهٔ سانت (`lib/money.ts`، G.4)، مبلغ با «۱۰م»، مبلغ + واحد («هر متر | هر تخته | کل»)، درصد،
 *   متن و گزینه.
 * - چسباندن TSV اکسل (G.3): نقل‌قول و CRLF، ارقام فارسی، سطر سرستون (و پانویس «نگله شماره … شامل … تخته») کنار
 *   گذاشته می‌شود و اگر سرستون ستون‌ها را نام برد، ستون‌ها با نام جفت می‌شوند؛ وگرنه از خانهٔ فعال به ترتیب. همهٔ
 *   اندازه‌های سانت‌نمای یک چسباندن یک پرسش می‌گیرند.
 * - «شمارهٔ بعدی» در خود جدول (G.4): بعد از هر ردیف ذخیره‌شده یا پیش‌نویس، (SerialTo ?? SerialFrom) + 1؛ ردیفی که فقط
 *   تعداد گرفته دستهٔ From..From + qty − 1 می‌شود.
 * - نمایش عددها با ارقام فارسی و «٬»/«٫»، و حساب متراژ و جمع خرید (قاعدهٔ دسته، D.3) بی ضرب ممیز شناور.
 */
import { message, type MessageTokens } from '@/lib/messages'
import { normalizeDigits, parseDecimal, parseDimensionPair, looksLikeDimensionPair, type ParsedDecimal } from '@/lib/money'
import { add, cmp, dec, lineArea, mul, pieceArea, round, toNumber, toPlainString, tryDec, type Dec } from '@/lib/num'
import { purchaseAmounts, type PreviewLine } from '@/islands/rugs/purchasePreview'
import {
  CostBasis,
  UNIT_LABELS,
  type GridColumn,
  type GridColumnType,
  type JsonValue,
  type PurchaseCellValue,
} from '@/lib/types/grid'

// ═════════════════════════════════════════════════════════════════════
// کلیدهای شناخته‌شده
// ═════════════════════════════════════════════════════════════════════

export const KEY_SERIAL = 'rug.serial'
export const KEY_QTY = 'rug.qty'
export const KEY_SELLER = 'batch.seller'
export const KEY_LENGTH = 'rug.intakeLength'
export const KEY_WIDTH = 'rug.intakeWidth'
export const KEY_FINAL_LENGTH = 'rug.finalLength'
export const KEY_FINAL_WIDTH = 'rug.finalWidth'
export const KEY_AREA = 'rug.areaM2'
export const KEY_TOTAL_AREA = 'rug.totalAreaM2'
export const KEY_PURCHASE_TOTAL = 'rug.purchaseTotal'
export const PURCHASE_PREFIX = 'purchase.'
/** کلید قراردادی در `derived`: آیا «حذف این فرش» به این کاربر نشان داده شود. */
export const DERIVED_CAN_DELETE = 'row.canDelete'

/** جفت طول/عرض: «208×210» در یکی هر دو را پر می‌کند. */
export const DIMENSION_PAIRS: Readonly<Record<string, string>> = {
  [KEY_LENGTH]: KEY_WIDTH,
  [KEY_FINAL_LENGTH]: KEY_FINAL_WIDTH,
}

/** وارونهٔ همان جفت: «208×210» که در خانهٔ عرض نوشته شد هم طول را پر می‌کند (عدد اول طول است، نه عرض). */
export const DIMENSION_PAIR_LENGTHS: Readonly<Record<string, string>> = {
  [KEY_WIDTH]: KEY_LENGTH,
  [KEY_FINAL_WIDTH]: KEY_FINAL_LENGTH,
}

/**
 * خانه‌هایی که یک مقدار اندازه پر می‌کند: بی جفت فقط همان خانه؛ «208×210» در خانهٔ طول، طول و عرض؛ در خانهٔ عرض هم
 * طول و عرض (نه «عرض = ۲٫۰۸» و گم شدن ۲٫۱۰). `has` می‌گوید جدول آن ستون را دارد یا نه.
 */
export function dimensionTargets<T>(
  key: string,
  value: T,
  pair: T | undefined,
  has: (key: string) => boolean = () => true,
): { key: string; value: T }[] {
  if (pair === undefined) return [{ key, value }]
  const width = DIMENSION_PAIRS[key]
  if (width && has(width)) return [{ key, value }, { key: width, value: pair }]
  const length = DIMENSION_PAIR_LENGTHS[key]
  if (length && has(length)) return [{ key: length, value }, { key, value: pair }]
  return [{ key, value }]
}

export function isPurchaseKey(key: string): boolean {
  return key.startsWith(PURCHASE_PREFIX)
}

// ═════════════════════════════════════════════════════════════════════
// متن
// ═════════════════════════════════════════════════════════════════════

const FA_DIGITS = '۰۱۲۳۴۵۶۷۸۹'

/** نشانه‌های جهت متن، BOM و ZWJ که کپی از اکسل یا کیبورد جا می‌گذارد (نیم‌فاصله جدا: فاصله می‌شود). */
const BIDI_MARKS = /[‎‏؜﻿‍‪-‮⁦-⁩]/g

/** ارقام لاتین → فارسی. */
export function faDigits(value: string | number): string {
  return String(value).replace(/\d/g, (d) => FA_DIGITS[Number(d)])
}

/**
 * یکسان‌سازی متن خانه (رنگ، نقشه، فروشنده): «ي/ك» عربی به فارسی، بی نشانهٔ جهت، فاصله‌های پشت‌سرهم یکی، بی فاصلهٔ
 * دو سر. ارقام دست نمی‌خورند («نائین ۹ لا» همان می‌ماند؛ سرور یکسانش می‌کند). خالی → null.
 */
export function normalizeText(raw: string | null | undefined): string | null {
  if (raw === null || raw === undefined) return null
  // نیم‌فاصله (U+200C) جزو \s نیست و در واژه‌ای مثل «تخته‌ای» می‌ماند
  const text = raw.replace(BIDI_MARKS, '').replace(/ي/g, 'ی').replace(/ك/g, 'ک').replace(/\s+/g, ' ').trim()
  return text.length > 0 ? text : null
}

/** کلید مقایسه: ارقام لاتین، «ی/ک» فارسی، بی نیم‌فاصله و نشانهٔ جهت، فاصلهٔ یکسان، حروف کوچک. */
export function fold(value: string | null | undefined): string {
  if (!value) return ''
  return normalizeDigits(value)
    .replace(BIDI_MARKS, '')
    .replace(/ي/g, 'ی')
    .replace(/ك/g, 'ک')
    .replace(/[ةۀ]/g, 'ه')
    .replace(/‌/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase()
}

/**
 * کدام پیشنهاد خانهٔ combo برجسته شود (همان که Enter و Tab برمی‌دارند): اول همان نوشته اگر در فهرست هست، بعد اولین
 * پیشنهادی که با نوشته شروع می‌شود؛ ‎-1 یعنی Enter همان نوشته را نگه می‌دارد. فهرست به ترتیب تکرار است، پس بی قاعدهٔ
 * اول، نوشتن «گبه» و Enter در جدولی که «گبه پشم» پرتکرارتر است، بی‌صدا «گبه پشم» می‌نشاند.
 */
export function suggestionIndex(items: readonly string[], typed: string): number {
  const key = fold(typed)
  if (!key) return -1
  const exact = items.findIndex((v) => fold(v) === key)
  return exact >= 0 ? exact : items.findIndex((v) => fold(v).startsWith(key))
}

// ═════════════════════════════════════════════════════════════════════
// شماره (آینهٔ Domain/Common/SerialNumber)
// ═════════════════════════════════════════════════════════════════════

export const SERIAL_MAX_LENGTH = 40
export const SERIAL_MAX_RANGE = 10_000
const INT32_MAX = 2_147_483_647

export interface SerialInfo {
  /** شکل نمایشی و ذخیره‌شده: «3778»، «3911 - 3960» یا «A-12» (ارقام لاتین) */
  display: string
  /** null برای شمارهٔ متنی */
  from: number | null
  to: number | null
  /** تعداد شماره‌ها (متنی: ۱) */
  count: number
}

export type ParsedSerial =
  | { status: 'empty' }
  | ({ status: 'ok' } & SerialInfo)
  | { status: 'error'; code: string }

const DASHES = new Set(['-', '–', '—', '−', '‐', '‑', '‒', '_'])
const TA = 'تا'

function normalizeSerialText(raw: string | null | undefined): string {
  if (!raw) return ''
  let out = ''
  let lastWasSpace = true
  for (const ch0 of raw.replace(BIDI_MARKS, '')) {
    let ch = normalizeDigits(ch0)
    if (ch === 'ي') ch = 'ی'
    else if (ch === 'ك') ch = 'ک'
    else if (ch === 'ة' || ch === 'ۀ') ch = 'ه'
    else if (ch === '‌') ch = ' '
    if (/\s/.test(ch)) {
      if (!lastWasSpace) out += ' '
      lastWasSpace = true
      continue
    }
    // نویسه‌های کنترلی
    if (/[\u0000-\u001F\u007F-\u009F]/.test(ch)) continue
    out += ch
    lastWasSpace = false
  }
  return out.endsWith(' ') ? out.slice(0, -1) : out
}

function readNumeric(text: string): { from: string; to: string | null } | null {
  let i = 0
  const readDigits = () => {
    const start = i
    while (i < text.length && text[i] >= '0' && text[i] <= '9') i++
    return text.slice(start, i)
  }
  const first = readDigits()
  if (!first) return null
  if (i === text.length) return { from: first, to: null }
  while (text[i] === ' ') i++
  if (DASHES.has(text[i])) i++
  else if (text.startsWith(TA, i)) i += TA.length
  else return null
  while (text[i] === ' ') i++
  const second = readDigits()
  if (!second || i !== text.length) return null
  return { from: first, to: second }
}

function toInt(digits: string): number | null {
  if (!/^\d+$/.test(digits)) return null
  const n = Number(digits)
  return Number.isSafeInteger(n) && n <= INT32_MAX ? n : null
}

function looksNumeric(text: string): boolean {
  const rest = text.split(TA).join('')
  let hasDigit = false
  for (const ch of rest) {
    if (ch >= '0' && ch <= '9') hasDigit = true
    else if (ch !== ' ' && !DASHES.has(ch)) return false
  }
  return hasDigit
}

/**
 * «3778»، «۳۷۷۸»، «3911 - 3960»، «3911–3960»، «3911_3960»، «3911 تا 3960» و «A-12» (G.4). خالی → `empty`.
 * «3960-3911» `E_SERIAL_REVERSED`، بیش از ۱۰٬۰۰۰ شماره `E_SERIAL_SPAN`، رقم و خط تیرهٔ بی‌شکل `E_SERIAL_FORMAT`.
 */
export function parseSerial(raw: string | null | undefined): ParsedSerial {
  const text = normalizeSerialText(raw)
  if (!text) return { status: 'empty' }

  const numeric = readNumeric(text)
  if (numeric) {
    if (numeric.to === null) {
      const single = toInt(numeric.from)
      if (single === null) return textSerial(text)
      return { status: 'ok', display: numeric.from, from: single, to: single, count: 1 }
    }
    const from = toInt(numeric.from)
    const to = toInt(numeric.to)
    if (from === null || to === null) return { status: 'error', code: 'E_SERIAL_FORMAT' }
    if (from > to) return { status: 'error', code: 'E_SERIAL_REVERSED' }
    if (to - from + 1 > SERIAL_MAX_RANGE) return { status: 'error', code: 'E_SERIAL_SPAN' }
    if (from === to) return { status: 'ok', display: numeric.from, from, to, count: 1 }
    return { status: 'ok', display: `${numeric.from} - ${numeric.to}`, from, to, count: to - from + 1 }
  }

  if (looksNumeric(text)) return { status: 'error', code: 'E_SERIAL_FORMAT' }
  return textSerial(text)
}

function textSerial(text: string): ParsedSerial {
  if (text.length > SERIAL_MAX_LENGTH) return { status: 'error', code: 'E_SERIAL_TOO_LONG' }
  return { status: 'ok', display: text, from: null, to: null, count: 1 }
}

/** اطلاعات شمارهٔ یک مقدار ذخیره‌شده (یا null برای خالی و نادرست). */
export function serialInfo(value: JsonValue | undefined): SerialInfo | null {
  if (value === null || value === undefined) return null
  const parsed = parseSerial(String(value))
  return parsed.status === 'ok' ? { display: parsed.display, from: parsed.from, to: parsed.to, count: parsed.count } : null
}

/** آیا بازه برای این تعداد بس است (متنی همیشه؛ بازه هرگز کوتاه‌تر از تعداد نیست). */
export function serialCovers(info: SerialInfo, qty: number): boolean {
  return info.from === null || info.count >= qty
}

/** شمارهٔ دسته از اولین شماره و تعداد: ۴۰۰۴ با ۵۰ → «4004 - 4053». */
export function serialForLot(from: number, qty: number): string {
  const count = Math.max(1, Math.trunc(qty))
  return count === 1 ? String(from) : `${from} - ${from + count - 1}`
}

/**
 * شمارهٔ پیشنهادی ردیف تازه در خود جدول: بزرگ‌ترینِ «شمارهٔ بعدیِ سرور» و (SerialTo ?? SerialFrom) + 1 هر ردیف جدول
 * (ذخیره‌شده یا پیش‌نویس). سرور null بدهد (حالت دستی) → null.
 */
export function nextSerialLocal(serverNext: number | null, serials: Iterable<JsonValue | undefined>): number | null {
  if (serverNext === null) return null
  let next = serverNext
  for (const value of serials) {
    const info = serialInfo(value)
    if (info && info.to !== null && info.to < INT32_MAX) next = Math.max(next, info.to + 1)
  }
  return next
}

// ═════════════════════════════════════════════════════════════════════
// نمایش عدد
// ═════════════════════════════════════════════════════════════════════

/**
 * عدد دقیق با ارقام فارسی، «٬» هزارگان و «٫» ممیز: ۱۰۰۰۰۰۰۰ → «۱۰٬۰۰۰٬۰۰۰»، ۲٫۵ با (۲، ۲) → «۲٫۵۰». null/نادرست → «».
 */
export function faNum(value: unknown, maxDecimals = 2, minDecimals = 0): string {
  const d = tryDec(value as never)
  if (!d) return ''
  const plain = toPlainString(round(d, maxDecimals), Math.min(minDecimals, maxDecimals))
  const negative = plain.startsWith('-')
  const [intPart, fracPart] = plain.replace('-', '').split('.')
  const grouped = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, ',')
  const latin = (negative ? '-' : '') + grouped + (fracPart ? `.${fracPart}` : '')
  return latin.replace(/[0-9,.]/g, (c) => (c === ',' ? '٬' : c === '.' ? '٫' : FA_DIGITS[Number(c)]))
}

/** متن ویرایشی یک مقدار: ارقام فارسی، بی هزارگان، با «.» (همان شکل NumberField در فوکوس). */
export function editableNumber(value: unknown): string {
  const d = tryDec(value as never)
  return d ? faDigits(toPlainString(d)) : ''
}

// ═════════════════════════════════════════════════════════════════════
// خانهٔ خرید
// ═════════════════════════════════════════════════════════════════════

const BASIS_NAMES: Readonly<Record<string, CostBasis>> = {
  total: CostBasis.Total,
  perm2: CostBasis.PerM2,
  perpiece: CostBasis.PerPiece,
  percent: CostBasis.Percent,
}

function basisFrom(value: unknown): CostBasis | null {
  if (typeof value === 'number' && [0, 1, 2, 3].includes(value)) return value as CostBasis
  if (typeof value === 'string') {
    const named = BASIS_NAMES[value.trim().toLowerCase()]
    if (named !== undefined) return named
    if (/^[0-3]$/.test(value.trim())) return Number(value.trim()) as CostBasis
  }
  return null
}

/**
 * مبنای پیش‌فرض یک ستون خرید: درصد برای ستون درصد؛ وگرنه اولین مبنای نام‌برده در `options` (اگر سرور فرستاد)؛ وگرنه
 * «هر متر» (G.2: «default «هر متر»»).
 */
export function purchaseDefaultBasis(column: Pick<GridColumn, 'type' | 'options'>): CostBasis {
  if (column.type === 'percent') return CostBasis.Percent
  for (const option of column.options ?? []) {
    const basis = basisFrom(option)
    if (basis !== null && basis !== CostBasis.Percent) return basis
  }
  return CostBasis.PerM2
}

/** مقدار خانهٔ خرید از سرور (عدد، `{rate, basis}`، یا چندسطری) → شکل یکسان؛ خالی → null. */
export function readPurchaseCell(raw: JsonValue | undefined, column: Pick<GridColumn, 'type' | 'options'>): PurchaseCellValue | null {
  if (raw === null || raw === undefined || raw === '') return null
  const fallback = purchaseDefaultBasis(column)
  if (typeof raw === 'number' || typeof raw === 'string') {
    const rate = tryDec(raw)
    return rate ? { rate: toNumber(rate), basis: fallback } : null
  }
  if (typeof raw !== 'object' || Array.isArray(raw)) return null
  const obj = raw as Record<string, JsonValue>
  const basis = basisFrom(obj.basis) ?? fallback
  const rateDec = tryDec(obj.rate as never)
  const amountDec = tryDec(obj.amount as never)
  const lines = typeof obj.lines === 'number' ? obj.lines : typeof obj.lineCount === 'number' ? obj.lineCount : obj.multi === true ? 2 : undefined
  const cell: PurchaseCellValue = { rate: rateDec ? toNumber(rateDec) : null, basis }
  if (amountDec) cell.amount = toNumber(amountDec)
  if (lines !== undefined) cell.lines = lines
  if (cell.rate === null && cell.amount === undefined && (cell.lines ?? 0) < 2) {
    // فقط چیپ واحد (نرخ هنوز نوشته نشده)
    return { rate: null, basis }
  }
  return cell
}

/** آیا خانهٔ خرید چندسطری (فقط‌خواندنی، چیپ «چند سطر») است. */
export function isMultiLine(cell: PurchaseCellValue | null): boolean {
  return (cell?.lines ?? 0) >= 2
}

/**
 * مقدار ارسالی خانهٔ خرید (`RugFieldWriter`): «مبلغ + واحد» → `{rate, basis}`؛ درصد → عدد؛ بی‌نرخ → null (سطر حذف).
 */
export function writePurchaseCell(cell: PurchaseCellValue | null, type: GridColumnType): JsonValue {
  if (!cell || cell.rate === null) return null
  return type === 'percent' ? cell.rate : { rate: cell.rate, basis: cell.basis }
}

/** واژه‌های واحد در انتهای مبلغ: «۳۰۰ه هر تخته» → «۳۰۰ه» با مبنای «هر تخته». */
const UNIT_WORDS: { pattern: RegExp; basis: CostBasis }[] = [
  { pattern: /(هر\s*متر|متری|\/\s*متر)$/u, basis: CostBasis.PerM2 },
  { pattern: /(هر\s*تخته|تخته‌ای|تخته ای|دانه‌ای|\/\s*تخته)$/u, basis: CostBasis.PerPiece },
  { pattern: /(کل|یکجا)$/u, basis: CostBasis.Total },
]

export function splitUnitSuffix(raw: string): { text: string; basis: CostBasis | null } {
  const text = raw.replace(BIDI_MARKS, '').replace(/ي/g, 'ی').trim()
  for (const unit of UNIT_WORDS) {
    const m = unit.pattern.exec(text)
    if (m) return { text: text.slice(0, m.index).trim(), basis: unit.basis }
  }
  return { text, basis: null }
}

// ═════════════════════════════════════════════════════════════════════
// خواندن متن یک خانه
// ═════════════════════════════════════════════════════════════════════

export interface CellChoice {
  label: string
  /** مقدار این پاسخ؛ `undefined` یعنی «پذیرفته نیست» (خطای `error`) */
  value?: JsonValue
  /** خانهٔ دوم جفت اندازه (عرض) */
  pair?: JsonValue
  error?: string
}

export type CellParse =
  | { status: 'ok'; value: JsonValue; pair?: JsonValue }
  | {
      status: 'ask'
      code: string
      message: string
      tokens: MessageTokens
      choices: [CellChoice, CellChoice]
      /** پرسش سانت/متر (در چسباندن همه با یک پرسش) */
      cmLike: boolean
    }
  | { status: 'error'; message: string }

export interface CellParseContext {
  moneyScale: number
  currencyLabel: string
  maxPlausibleSideM: number
  /** مقدار فعلی خانه (برای نگه داشتن چیپ واحد) */
  current?: JsonValue
}

function fromDecimal(parsed: ParsedDecimal, fallbackCode: string, wrap: (v: string) => JsonValue): CellParse {
  switch (parsed.status) {
    case 'empty':
      return { status: 'ok', value: null }
    case 'ok':
      return { status: 'ok', value: wrap(parsed.value) }
    case 'error':
      return { status: 'error', message: message(parsed.code, parsed.tokens) }
    case 'ask':
      return {
        status: 'ask',
        code: parsed.code,
        message: message(parsed.code, parsed.tokens),
        tokens: parsed.tokens,
        cmLike: parsed.code === 'Q_DIM_CM' || parsed.code === 'Q_DIM_UNIT',
        choices: parsed.choices.map((c) =>
          c.value !== null ? { label: c.label, value: wrap(c.value) } : { label: c.label, error: message(fallbackCode) },
        ) as [CellChoice, CellChoice],
      }
  }
}

function decimalNumber(value: string): number {
  return toNumber(dec(value))
}

/**
 * متن تایپ‌شده یا چسبانده‌شدهٔ یک خانه را بر اساس نوع ستون می‌خواند. `ok` با `value` null یعنی «خالی کن».
 * خانهٔ اندازه با «208×210» `pair` (عرض) هم می‌دهد.
 */
export function parseCellInput(column: GridColumn, raw: string, ctx: CellParseContext): CellParse {
  const text = raw ?? ''
  switch (column.type) {
    case 'readonly':
      return { status: 'error', message: message('E_FIELD_READONLY', { label: column.label }) }

    case 'serial': {
      const parsed = parseSerial(text)
      if (parsed.status === 'empty') return { status: 'ok', value: null }
      if (parsed.status === 'error') return { status: 'error', message: message(parsed.code) }
      return { status: 'ok', value: parsed.display }
    }

    case 'int': {
      const parsed = parseDecimal(text, 'number', { scale: 0 })
      if (parsed.status === 'ok') {
        const n = decimalNumber(parsed.value)
        if (column.key === KEY_QTY && n < 1) return { status: 'error', message: message('E_RUG_QUANTITY') }
        if (n < 0) return { status: 'error', message: message('E_NEGATIVE') }
        if (n > SERIAL_MAX_RANGE && column.key === KEY_QTY) {
          return { status: 'error', message: message('E_OUT_OF_RANGE', { label: column.label, min: 1, max: SERIAL_MAX_RANGE }) }
        }
        return { status: 'ok', value: n }
      }
      return fromDecimal(parsed, 'E_INTEGER', decimalNumber)
    }

    case 'dim': {
      const options = { maxPlausibleSideM: ctx.maxPlausibleSideM }
      if (looksLikeDimensionPair(text)) {
        const pair = parseDimensionPair(text, options)
        if (!pair) return { status: 'error', message: message('E_DIMENSION') }
        if (pair.status === 'ok') {
          return { status: 'ok', value: decimalNumber(pair.values[0]), pair: decimalNumber(pair.values[1]) }
        }
        if (pair.status === 'error') return { status: 'error', message: message(pair.code, pair.tokens) }
        return {
          status: 'ask',
          code: pair.code,
          message: message(pair.code, pair.tokens),
          tokens: pair.tokens,
          cmLike: true,
          choices: pair.choices.map((c) =>
            c.values
              ? { label: c.label, value: decimalNumber(c.values[0]), pair: decimalNumber(c.values[1]) }
              : { label: c.label, error: message('E_DIMENSION') },
          ) as [CellChoice, CellChoice],
        }
      }
      return fromDecimal(parseDecimal(text, 'dim', options), 'E_DIMENSION', decimalNumber)
    }

    case 'money':
      return fromDecimal(
        parseDecimal(text, 'money', { scale: ctx.moneyScale, currencyLabel: ctx.currencyLabel }),
        'E_MONEY_DECIMALS',
        decimalNumber,
      )

    case 'money+unit': {
      const current = readPurchaseCell(ctx.current, column)
      const { text: amountText, basis: typedBasis } = splitUnitSuffix(text)
      const basis = typedBasis ?? current?.basis ?? purchaseDefaultBasis(column)
      const parsed = parseDecimal(amountText, 'money', { scale: ctx.moneyScale, currencyLabel: ctx.currencyLabel })
      if (parsed.status === 'empty') return { status: 'ok', value: null }
      return fromDecimal(parsed, 'E_MONEY_DECIMALS', (v) => ({ rate: decimalNumber(v), basis }))
    }

    case 'percent': {
      const cleaned = text.replace(/[٪%]/g, '')
      const parsed = parseDecimal(cleaned, 'number', { scale: 4 })
      if (parsed.status === 'ok') {
        const n = dec(parsed.value)
        if (cmp(n, 0) < 0 || cmp(n, 100) > 0) return { status: 'error', message: message('E_PURCHASE_PERCENT') }
      }
      return fromDecimal(parsed, 'E_NUMBER', decimalNumber)
    }

    case 'select': {
      const value = normalizeText(text)
      if (value === null) return { status: 'ok', value: null }
      const match = (column.options ?? []).find((o) => fold(o) === fold(value))
      return match !== undefined
        ? { status: 'ok', value: match }
        : { status: 'error', message: message('E_GRID_SELECT_OPTION', { value, label: column.label }) }
    }

    case 'text':
    case 'combo':
    default:
      return { status: 'ok', value: normalizeText(text) }
  }
}

/** متن ویرایشی یک مقدار ذخیره‌شده (وقتی خانه فعال می‌شود). */
export function editableText(column: GridColumn, value: JsonValue | undefined): string {
  if (value === null || value === undefined) return ''
  switch (column.type) {
    case 'serial':
      return faDigits(String(value))
    case 'int':
    case 'dim':
    case 'money':
      return editableNumber(value)
    case 'percent':
    case 'money+unit': {
      // خانهٔ خرید درصدی هم از سرور `{rate, basis, amount}` می‌آید، نه عدد خالی
      if (column.type === 'percent' && typeof value !== 'object') return editableNumber(value)
      const cell = readPurchaseCell(value, column)
      return cell && cell.rate !== null ? editableNumber(cell.rate) : ''
    }
    default:
      return typeof value === 'string' ? value : String(value)
  }
}

/** متن نمایشی یک خانه (بیرون از ویرایش). */
export function displayText(column: GridColumn, value: JsonValue | undefined, moneyScale: number): string {
  if (value === null || value === undefined || value === '') return ''
  switch (column.type) {
    case 'serial':
      return faDigits(String(value))
    case 'int':
      return faNum(value, 0)
    case 'dim':
      return faNum(value, 2, 2)
    case 'money':
      return faNum(value, moneyScale)
    case 'percent':
    case 'money+unit': {
      if (column.type === 'percent' && typeof value !== 'object') return faNum(value, 4)
      // خانهٔ خرید (درصدی هم): `{rate, basis, amount}`؛ چندسطری جمع مبلغ را نشان می‌دهد
      const cell = readPurchaseCell(value, column)
      if (!cell) return ''
      if (isMultiLine(cell)) return faNum(cell.amount ?? 0, moneyScale)
      return cell.rate === null ? '' : faNum(cell.rate, column.type === 'percent' ? 4 : Math.max(moneyScale, 4))
    }
    default:
      return typeof value === 'string' ? value : String(value)
  }
}

/** برچسب چیپ واحد. */
export function unitLabel(basis: CostBasis): string {
  return UNIT_LABELS[basis] ?? ''
}

// ═════════════════════════════════════════════════════════════════════
// حساب ردیف: متراژ و جمع خرید
// ═════════════════════════════════════════════════════════════════════

export type RowValues = Record<string, JsonValue | undefined>

/** تعداد ردیف (خالی = ۱). */
export function rowQty(values: RowValues): number {
  const q = values[KEY_QTY]
  const n = typeof q === 'number' ? q : typeof q === 'string' ? Number(normalizeDigits(q)) : NaN
  return Number.isSafeInteger(n) && n >= 1 ? n : 1
}

export interface RowArea {
  piece: Dec
  qty: number
  total: Dec
}

/** مساحت یک تخته در اندازهٔ اول (ROUND(L×W, 2)) و × تعداد؛ null اگر طول یا عرض نیست. */
export function rowArea(values: RowValues, basis: 'effective' | 'intake' = 'effective'): RowArea | null {
  const final = basis === 'effective' && values['rug.finalLength'] != null && values['rug.finalWidth'] != null
  const l = tryDec(values[final ? 'rug.finalLength' : KEY_LENGTH] as never)
  const w = tryDec(values[final ? 'rug.finalWidth' : KEY_WIDTH] as never)
  if (!l || !w) return null
  const piece = pieceArea(l, w)
  const qty = rowQty(values)
  return { piece, qty, total: lineArea(piece, qty) }
}

/** متن خانهٔ «متراژ»: «۴٫۳۷» یا برای دسته «۰٫۱۶ × ۵۰ = ۸٫۰۰». */
export function areaText(area: RowArea | null): string {
  if (!area) return ''
  if (area.qty === 1) return faNum(area.piece, 2, 2)
  return `${faNum(area.piece, 2, 2)} × ${faNum(area.qty, 0)} = ${faNum(area.total, 2, 2)}`
}

export interface RowPurchase {
  total: Dec
  /** «۳۰۰٬۰۰۰ × ۵۰ + ۲٪ = ۱۵٬۳۰۰٬۰۰۰» */
  text: string
}

/**
 * پیش‌نمایش جمع خرید یک ردیف از خانه‌های خرید جدول (همان قاعدهٔ سرور، `purchasePreview`). فقط برای نشان دادن
 * حساب هنگام تایپ است؛ عدد ذخیره‌شده را سرور می‌دهد (`derived['rug.purchaseTotal']`). null وقتی هیچ نرخی نیست.
 */
export function rowPurchase(values: RowValues, columns: readonly GridColumn[], moneyScale: number): RowPurchase | null {
  const lines: PreviewLine[] = []
  for (const column of columns) {
    if (!isPurchaseKey(column.key) || (column.type !== 'money+unit' && column.type !== 'percent')) continue
    const cell = readPurchaseCell(values[column.key], column)
    if (!cell || cell.rate === null || isMultiLine(cell)) continue
    lines.push({ basis: column.type === 'percent' ? CostBasis.Percent : cell.basis, rate: String(cell.rate) })
  }
  if (lines.length === 0) return null

  const area = rowArea(values, 'intake')
  // نرخ «هر متر» بی طول و عرض هنوز حساب‌شدنی نیست («… = ۰» گمراه می‌کند)
  if (!area && lines.some((l) => l.basis === CostBasis.PerM2)) return null
  const piece = area?.piece ?? dec(0)
  const qty = rowQty(values)
  const amounts = purchaseAmounts(lines, piece, qty, moneyScale)
  const total = amounts.reduce<Dec>((sum, a) => add(sum, a), dec(0))

  const main = lines.filter((l) => l.basis !== CostBasis.Percent)
  const percents = lines.filter((l) => l.basis === CostBasis.Percent)
  const percentText = percents.map((p) => ` + ${faNum(p.rate, 4)}٪`).join('')
  let left = ''
  if (main.length === 1) {
    const only = main[0]
    const rate = only.rate ?? 0
    if (only.basis === CostBasis.PerM2 && qty === 1 && area) left = `${faNum(rate, moneyScale)} × ${faNum(area.piece, 2, 2)}`
    else if (only.basis === CostBasis.PerM2 && area) left = `${faNum(round(mul(rate, area.piece), moneyScale), moneyScale)} × ${faNum(qty, 0)}`
    else if (only.basis === CostBasis.PerPiece && qty > 1) left = `${faNum(round(rate, moneyScale), moneyScale)} × ${faNum(qty, 0)}`
    else if (percents.length > 0) left = faNum(round(rate, moneyScale), moneyScale)
  }
  const text = left ? `${left}${percentText} = ${faNum(total, moneyScale)}` : faNum(total, moneyScale)
  return { total, text }
}

// ═════════════════════════════════════════════════════════════════════
// TSV (چسباندن از اکسل)
// ═════════════════════════════════════════════════════════════════════

/**
 * متن کلیپ‌بورد اکسل → سطرها و خانه‌ها. خانهٔ نقل‌قول‌دار («…») می‌تواند Tab، خط تازه و «""» داشته باشد؛ CRLF و CR هم
 * خط تازه‌اند؛ خط خالیِ آخر (اکسل همیشه می‌گذارد) حذف می‌شود.
 */
export function parseTsv(text: string): string[][] {
  const rows: string[][] = []
  let row: string[] = []
  let cell = ''
  let i = 0
  let quoted = false
  let cellStart = true

  while (i < text.length) {
    const ch = text[i]
    if (quoted) {
      if (ch === '"') {
        if (text[i + 1] === '"') {
          cell += '"'
          i += 2
          continue
        }
        quoted = false
        i++
        continue
      }
      cell += ch
      i++
      continue
    }
    if (ch === '"' && cellStart) {
      quoted = true
      cellStart = false
      i++
      continue
    }
    if (ch === '\t') {
      row.push(cell)
      cell = ''
      cellStart = true
      i++
      continue
    }
    if (ch === '\r' || ch === '\n') {
      row.push(cell)
      rows.push(row)
      row = []
      cell = ''
      cellStart = true
      i += ch === '\r' && text[i + 1] === '\n' ? 2 : 1
      continue
    }
    cell += ch
    cellStart = false
    i++
  }
  if (cell !== '' || row.length > 0) {
    row.push(cell)
    rows.push(row)
  }
  return rows
}

/** خانه‌ها → متن TSV برای کپی (نقل‌قول فقط وقتی لازم است). */
export function toTsv(rows: readonly (readonly string[])[]): string {
  return rows
    .map((r) => r.map((c) => (/[\t\n\r"]/.test(c) ? `"${c.replace(/"/g, '""')}"` : c)).join('\t'))
    .join('\r\n')
}

/** نام‌های دیگرِ سرستون‌ها (برگهٔ اکسل بازرگان) → کلید ستون. */
const HEADER_SYNONYMS: Readonly<Record<string, string>> = {
  شماره: KEY_SERIAL,
  'شماره فرش': KEY_SERIAL,
  'شمارهٔ فرش': KEY_SERIAL,
  تعداد: KEY_QTY,
  فروشنده: KEY_SELLER,
  'رنگ متن': 'rug.custom.bg_color',
  'رنگ زمینه': 'rug.custom.bg_color',
  'رنگ حاشیه': 'rug.custom.border_color',
  نقشه: 'rug.pattern',
  'محل بافت': 'rug.origin',
  'مواد مصرفی': 'rug.material',
  مواد: 'rug.material',
  طول: KEY_LENGTH,
  عرض: KEY_WIDTH,
  مساحت: KEY_AREA,
  متراژ: KEY_AREA,
  یادداشت: 'rug.notes',
  توضیح: 'rug.notes',
  'قیمت خرید': 'purchase.price',
  دلالی: 'purchase.broker',
  'دلالی ٪': 'purchase.broker',
}

/** سرستون‌هایی که ستون جدول ندارند ولی سطر را سرستون نشان می‌دهند (برگهٔ اظهار). */
const OTHER_HEADERS = new Set(['نگله', 'عدل', 'ارزش متر مربع', 'جمع کل دلار', 'ردیف', 'جمع خرید'].map(fold))

function headerKey(cell: string, columns: readonly GridColumn[]): string | null {
  const f = fold(cell)
  if (!f) return null
  const byLabel = columns.find((c) => fold(c.label) === f)
  if (byLabel) return byLabel.key
  const synonym = Object.entries(HEADER_SYNONYMS).find(([name]) => fold(name) === f)
  if (synonym && columns.some((c) => c.key === synonym[1])) return synonym[1]
  return null
}

/** آیا یک خط سرستون است: دست‌کم دو خانهٔ پر، همه نام ستون، هیچ‌کدام عدد. */
export function isHeaderLine(cells: readonly string[], columns: readonly GridColumn[]): boolean {
  const filled = cells.map((c) => c.trim()).filter((c) => c.length > 0)
  if (filled.length === 0) return false
  if (filled.some((c) => /\d/.test(normalizeDigits(c)))) return false
  const known = filled.filter((c) => headerKey(c, columns) !== null || OTHER_HEADERS.has(fold(c))).length
  return filled.length >= 2 ? known / filled.length >= 0.6 : known === 1 && columns.length > 1
}

/** پانویس نگلهٔ برگهٔ اظهار («نگله شماره ۳ شامل ۴ تخته …») یا عنوان برگه — داده نیست. */
export function isFooterLine(cells: readonly string[], baleFooter = 'نگله شماره'): boolean {
  const folded = cells.map(fold)
  if (folded.some((c) => c === 'شامل') && folded.some((c) => c === 'تخته')) return true
  const footer = fold(baleFooter)
  return folded.some((c) => c === footer || c.startsWith('نگله شماره') || c.startsWith('عدل شماره') || c.startsWith('صورت نگله'))
}

export interface PasteCell {
  raw?: string
  /** چندمین سطر دادهٔ چسبانده‌شده (از ۰) */
  row: number
  key: string
  /** مقدار خوانده‌شده؛ `undefined` وقتی خطا دارد یا منتظر پرسش سانت است */
  value?: JsonValue
  error?: string
  /** اندازهٔ سانت‌نما: پاسخ «تبدیل کن» و «نه» */
  cm?: { yes: JsonValue; no: JsonValue | undefined; noError?: string }
}

export interface PastePlan {
  /** تعداد سطرهای دادهٔ چسبانده‌شده */
  rows: number
  cells: PasteCell[]
  /** تعداد خانه‌های سانت‌نما (یک پرسش برای همه) */
  cmCount: number
  headerSkipped: number
  footerSkipped: number
  /** ستون‌ها با سرستون جفت شدند (نه با جای خانه) */
  byHeader: boolean
}

export interface PasteOptions extends CellParseContext {
  baleFooter?: string
}

/**
 * نقشهٔ چسباندن: کدام مقدار به کدام خانه برود. سرستون و پانویس کنار گذاشته می‌شوند؛ اگر سرستون دست‌کم دو ستون را
 * نام برد، ستون‌ها با نام جفت می‌شوند، وگرنه از ستون `startCol` به ترتیب (خانهٔ ستون فقط‌خواندنی نادیده). «208×210»
 * در خانهٔ طول عرض را هم پر می‌کند. هر خانهٔ سانت‌نما هر دو پاسخ را با خود دارد تا یک پرسش برای همه کافی باشد.
 */
export function planPaste(
  text: string,
  columns: readonly GridColumn[],
  startCol: number,
  options: PasteOptions,
): PastePlan {
  const lines = parseTsv(text)
  let headerSkipped = 0
  let footerSkipped = 0
  let mapping: (string | null)[] | null = null
  const data: string[][] = []

  for (const line of lines) {
    if (line.every((c) => c.trim() === '')) continue
    if (isHeaderLine(line, columns)) {
      headerSkipped++
      const keys = line.map((c) => headerKey(c, columns))
      if (mapping === null && keys.filter((k) => k !== null).length >= 2) mapping = keys
      continue
    }
    if (isFooterLine(line, options.baleFooter)) {
      footerSkipped++
      continue
    }
    data.push(line)
  }

  const cells: PasteCell[] = []
  let cmCount = 0
  const writable = (key: string) => columns.some((c) => c.key === key && !c.readOnly && c.type !== 'readonly')
  data.forEach((line, r) => {
    const touched = new Set<string>()
    line.forEach((raw, j) => {
      const key = mapping ? mapping[j] ?? null : columns[startCol + j]?.key ?? null
      if (!key) return
      const column = columns.find((c) => c.key === key)
      if (!column || column.readOnly || column.type === 'readonly') return
      if (touched.has(key) && raw.trim() === '') return
      const parsed = parseCellInput(column, raw, options)
      touched.add(key)
      if (parsed.status === 'ok') {
        for (const target of dimensionTargets(key, parsed.value, parsed.pair, writable)) {
          cells.push({ row: r, key: target.key, value: target.value, raw })
          touched.add(target.key)
        }
      } else if (parsed.status === 'error') {
        cells.push({ row: r, key, error: parsed.message })
      } else if (parsed.cmLike) {
        // «208×210» دو خانه است (طول و عرض)، هر کدام با هر دو پاسخ؛ یک پرسش برای همه
        const [yes, no] = parsed.choices
        const isPair = yes.pair !== undefined || no.pair !== undefined
        for (const target of dimensionTargets<'first' | 'second'>(key, 'first', isPair ? 'second' : undefined, writable)) {
          const pick = (choice: CellChoice) => (target.value === 'first' ? choice.value : choice.pair)
          cmCount++
          cells.push({
            row: r,
            key: target.key,
            cm: {
              yes: pick(yes) ?? null,
              no: pick(no),
              noError: pick(no) === undefined ? no.error ?? message('E_DIMENSION') : undefined,
            },
          })
          touched.add(target.key)
        }
      } else {
        // پرسشِ غیر سانت در چسباندن («منظورتان ۱۰٬۰۰۰ است؟»): خانه قرمز می‌ماند تا خودش درست بنویسد
        cells.push({ row: r, key, error: message('E_GRID_MONEY_ASK_PASTE', parsed.tokens) })
      }
    })
  })

  return { rows: data.length, cells, cmCount, headerSkipped, footerSkipped, byHeader: mapping !== null }
}

/** پیام پرسش یک‌بارهٔ سانت. */
export function pasteCmMessage(count: number, tokens: MessageTokens = {}): string {
  return message('Q_GRID_PASTE_CM', { n: count, ...tokens })
}
