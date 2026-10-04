/**
 * خواندن عددی که آدم تایپ کرده — مبلغ، اندازه و عدد ساده — با همهٔ شکل‌هایی که در بازار نوشته می‌شود
 * (G.4). آینهٔ سرور: `App/Common/NumberParser` (از فاز فرش).
 *
 * چرا این‌قدر قاعده: کیبورد فارسی «٫» می‌دهد نه «.»؛ بازرگان «۱۰م» می‌نویسد یعنی ده میلیون؛ اکسل
 * «2,08» می‌نویسد یعنی ۲٫۰۸ متر؛ و «208» در ستون متر یعنی ۲۰۸ سانت. اگر هر کدام بی‌صدا غلط خوانده شود،
 * قیمت یا متراژ ده یا صد برابر می‌شود. پس هر چیز مبهم پرسیده می‌شود، نه حدس زده.
 *
 * خروجی همیشه رشتهٔ اعشاری لاتین دقیق است («2.08»، «10000000») تا حساب در `lib/num.ts` دقیق بماند.
 */
import { appContext } from '@/lib/context'
import type { MessageTokens } from '@/lib/messages'
import {
  cmp,
  dec,
  isInteger,
  mul,
  normalize,
  significantDecimals,
  toPlainString,
  type Dec,
} from '@/lib/num'

const FA_DIGITS = '۰۱۲۳۴۵۶۷۸۹'
const AR_DIGITS = '٠١٢٣٤٥٦٧٨٩'

/** ارقام فارسی و عربی را لاتین می‌کند (بقیهٔ نویسه‌ها دست‌نخورده). */
export function normalizeDigits(input: string): string {
  let out = ''
  for (const ch of input) {
    const fa = FA_DIGITS.indexOf(ch)
    const ar = AR_DIGITS.indexOf(ch)
    if (fa > -1) out += String(fa)
    else if (ar > -1) out += String(ar)
    else out += ch
  }
  return out
}

/** نمایش عدد با جداکنندهٔ هزارگان (بدون اعشار) — ویژهٔ MoneyInput قدیمی. */
export function formatThousands(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return ''
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(value)
}

// ── پاک‌سازی متن ──────────────────────────────────────────────────────

/**
 * فاصله، نیم‌فاصله و نشانه‌های جهت متن که کیبورد یا کپی از اکسل جا می‌گذارد: `\s` فاصلهٔ نشکن را هم دارد و
 * `\p{Cf}` (نویسه‌های قالب) نیم‌فاصله، RLM/LRM/ALM، نشانه‌های جاسازی جهت و BOM را.
 */
const INVISIBLES = /[\s\p{Cf}]/gu

/**
 * پاک‌سازی پایه: ارقام لاتین، حذف فاصله‌ها و نشانه‌های جهت، «ي/ك» عربی به فارسی، «−» یونیکد به «-»،
 * «٫» و «/» میان دو رقم به «.».
 */
export function normalizeNumericText(raw: string): string {
  return normalizeDigits(raw)
    .replace(INVISIBLES, '')
    .replace(/ي/g, 'ی')
    .replace(/ك/g, 'ک')
    .replace(/[−‒–—﹣－]/g, '-')
    .replace(/٫/g, '.')
    .replace(/(\d)\/(?=\d)/g, '$1.')
}

// ── نتیجهٔ خواندن ─────────────────────────────────────────────────────

/** یک پاسخ دکمه‌ای به پرسش؛ `value` null یعنی «پس این عدد پذیرفته نیست». */
export interface ParseChoice {
  value: string | null
  label: string
}

export type ParsedDecimal =
  | { status: 'empty' }
  | { status: 'ok'; value: string }
  /** مبهم است؛ با دو دکمه پرسیده می‌شود. اولی پیشنهاد اصلی است (Enter)، دومی «نه» (Esc). */
  | { status: 'ask'; code: string; tokens: MessageTokens; choices: [ParseChoice, ParseChoice] }
  | { status: 'error'; code: string; tokens?: MessageTokens }

export type DecimalKind = 'money' | 'dim' | 'number'

export interface ParseDecimalOptions {
  /**
   * رقم اعشار مجاز. مبلغ: MoneyScale کارگاه (پیش‌فرض از app-context)؛ اندازه همیشه ۲؛ عدد ساده
   * پیش‌فرض ۲ (۰ یعنی فقط عدد صحیح).
   */
  scale?: number
  /** منفی پذیرفته شود؟ (فقط مبلغ و عدد ساده؛ اندازه هرگز منفی نیست) */
  allowNegative?: boolean
  /** مبلغ: برچسب واحد برای پیام «مبلغ تومان اعشار ندارد» (پیش‌فرض از app-context) */
  currencyLabel?: string
  /** اندازه: ضلع تا این عدد بی‌پرسش متر است (TenantPreferences.MaxPlausibleSideM، پیش‌فرض ۱۲) */
  maxPlausibleSideM?: number
}

/** حدهای سخت اندازه (G.4): از ۵ سانت تا ۳۰ متر، حداکثر دو رقم اعشار. */
export const MIN_SIDE_M = '0.05'
export const MAX_SIDE_M = '30'
/** عدد صحیح تا این اندازه، اگر به متر نشدنی باشد، احتمالاً سانت است. */
const MAX_CM = 3000
/** مساحت یک تخته بیش از این، هشدار غیرمانع می‌گیرد. */
export const LARGE_AREA_M2 = '60'

const NUMBER_SHAPE = /^-?(\d+\.?\d*|\.\d+)$/

/** متن عددی فارسی برای پیام‌ها و دکمه‌ها: «۲٫۰۸»، «۱۰٬۰۰۰». */
function faText(value: Dec | string, minDecimals = 0): string {
  const plain = toPlainString(dec(value), minDecimals)
  const [intPart, fracPart] = plain.replace('-', '').split('.')
  const grouped = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, ',')
  const latin = (plain.startsWith('-') ? '-' : '') + grouped + (fracPart ? `.${fracPart}` : '')
  return latin.replace(/[0-9,.]/g, (c) => (c === ',' ? '٬' : c === '.' ? '٫' : FA_DIGITS[Number(c)]))
}

function invalid(code: string, tokens?: MessageTokens): ParsedDecimal {
  return tokens ? { status: 'error', code, tokens } : { status: 'error', code }
}

/** علامت را جدا می‌کند؛ منفی فقط در ابتدای متن پذیرفته است. */
function splitSign(text: string): { negative: boolean; body: string } {
  if (text.startsWith('-')) return { negative: true, body: text.slice(1) }
  if (text.startsWith('+')) return { negative: false, body: text.slice(1) }
  return { negative: false, body: text }
}

// ── مبلغ ──────────────────────────────────────────────────────────────

const MONEY_SUFFIXES: { pattern: RegExp; factor: string }[] = [
  { pattern: /(میلیون|ملیون|م)$/, factor: '1000000' },
  { pattern: /(هزار|ه)$/, factor: '1000' },
]

function parseMoney(raw: string, options: ParseDecimalOptions): ParsedDecimal {
  const scale = options.scale ?? appContext().moneyScale
  const currency = options.currencyLabel ?? appContext().currency.label
  // «۱۰۰۰ تومان» را هم بپذیر: نام واحد در انتها فقط توضیح است
  let text = normalizeNumericText(raw).replace(/(تومان|ریال)$/, '')
  if (!text) return { status: 'empty' }

  let factor: string | null = null
  for (const suffix of MONEY_SUFFIXES) {
    if (suffix.pattern.test(text)) {
      factor = suffix.factor
      text = text.replace(suffix.pattern, '')
      break
    }
  }

  // در مبلغ «,» و «٬» جداکنندهٔ هزارگان‌اند
  text = text.replace(/[,٬]/g, '')
  const { negative, body } = splitSign(text)
  if (!body) return invalid('E_NUMBER')
  if ((body.match(/\./g) ?? []).length > 1 || !NUMBER_SHAPE.test(body)) return invalid('E_NUMBER')
  if (negative && !options.allowNegative) return invalid('E_NEGATIVE')

  const literal = dec((negative ? '-' : '') + body)

  // «10.000» بدون پسوند وقتی مبلغ اعشار ندارد: شاید منظور ده هزار بوده (نقطه به‌جای جداکننده)
  if (factor === null && scale === 0 && /^\d+\.\d{3}$/.test(body)) {
    const asThousands = dec((negative ? '-' : '') + body.replace('.', ''))
    const asTyped = isInteger(literal) ? toPlainString(literal) : null
    return {
      status: 'ask',
      code: 'Q_MONEY_THOUSANDS',
      tokens: { amount: faText(asThousands) },
      choices: [
        { value: toPlainString(asThousands), label: 'بله' },
        { value: asTyped, label: 'نه' },
      ],
    }
  }

  const value = factor === null ? literal : mul(literal, factor)
  if (significantDecimals(value) > scale) {
    return scale === 0
      ? invalid('E_MONEY_DECIMALS', { currency })
      : invalid('E_MONEY_SCALE', { currency, n: scale })
  }
  return { status: 'ok', value: toPlainString(normalize(value)) }
}

// ── عدد ساده (دلار هر متر، درصد، تعداد، نرخ ارز) ──────────────────────

function parsePlainNumber(raw: string, options: ParseDecimalOptions): ParsedDecimal {
  const scale = options.scale ?? 2
  let text = normalizeNumericText(raw)
  if (!text) return { status: 'empty' }
  text = text.replace(/[,٬]/g, '')
  const { negative, body } = splitSign(text)
  if (!body || (body.match(/\./g) ?? []).length > 1 || !NUMBER_SHAPE.test(body)) return invalid('E_NUMBER')
  if (negative && !options.allowNegative) return invalid('E_NEGATIVE')

  const value = dec((negative ? '-' : '') + body)
  if (significantDecimals(value) > scale) {
    return scale === 0 ? invalid('E_INTEGER') : invalid('E_DECIMALS_MAX', { n: scale })
  }
  return { status: 'ok', value: toPlainString(normalize(value)) }
}

// ── اندازه (متر) ──────────────────────────────────────────────────────

function dimensionText(raw: string): string {
  // در اندازه «,» و «٬» ممیزند («2,08» از اکسل)
  return normalizeNumericText(raw).replace(/[,٬]/g, '.')
}

function inRange(value: Dec): boolean {
  return cmp(value, MIN_SIDE_M) >= 0 && cmp(value, MAX_SIDE_M) <= 0 && significantDecimals(value) <= 2
}

function parseDimensionValue(text: string, options: ParseDecimalOptions): ParsedDecimal {
  if (!text) return { status: 'empty' }
  if (text.startsWith('-')) return invalid('E_DIMENSION')
  const body = text.startsWith('+') ? text.slice(1) : text
  if ((body.match(/\./g) ?? []).length > 1 || !NUMBER_SHAPE.test(body)) return invalid('E_DIMENSION')

  const value = dec(body)
  const maxPlausible = dec(options.maxPlausibleSideM ?? 12)

  // فقط عددی که بی‌ممیز تایپ شده مبهم است؛ «30.00» یعنی صریحاً متر
  if (!body.includes('.') && isInteger(value)) {
    // ۳۱ تا ۳۰۰۰: قطعاً سانت است؛ فقط تأیید
    if (cmp(value, 30) > 0 && cmp(value, MAX_CM) <= 0) {
      const metres = toPlainString(dec(`${toPlainString(value)}e-2`), 2)
      return {
        status: 'ask',
        code: 'Q_DIM_CM',
        tokens: { typed: faText(value), metres: faText(metres, 2) },
        choices: [
          { value: metres, label: `بله، ${faText(metres, 2)} متر` },
          { value: null, label: 'نه' },
        ],
      }
    }
    // بزرگ‌تر از ضلع معقول تا ۳۰: هم متر ممکن است هم سانت؛ سانت اول (تا ۲۵×۲۵ سانت ۶۲۵ متر نشود)
    if (cmp(value, maxPlausible) > 0 && cmp(value, MAX_SIDE_M) <= 0) {
      const metres = toPlainString(dec(`${toPlainString(value)}e-2`), 2)
      const typed = toPlainString(value)
      return {
        status: 'ask',
        code: 'Q_DIM_UNIT',
        tokens: { n: faText(value), metres: faText(metres, 2) },
        choices: [
          { value: inRange(dec(metres)) ? metres : null, label: `${faText(value)} سانت` },
          { value: typed, label: `${faText(value)} متر` },
        ],
      }
    }
  }

  return inRange(value) ? { status: 'ok', value: toPlainString(normalize(value)) } : invalid('E_DIMENSION')
}

/**
 * خواندن یک عدد تایپ‌شده (G.4).
 * - `money`: «,» و «٬» جداکنندهٔ هزارگان؛ پسوند «م/میلیون» × ۱٬۰۰۰٬۰۰۰ و «ه/هزار» × ۱٬۰۰۰؛ با MoneyScale صفر،
 *   «10.000» پرسیده می‌شود و هر اعشار دیگری رد.
 * - `dim`: «,» ممیز است؛ ۰٫۰۵ تا ۳۰ متر با حداکثر دو رقم اعشار؛ عدد صحیح ۳۱ تا ۳۰۰۰ تأیید سانت، و عدد صحیح
 *   بزرگ‌تر از ضلع معقول تا ۳۰ پرسش «متر یا سانت؟».
 * - `number`: عدد ساده با حداکثر `scale` رقم اعشار.
 * بیش از یک ممیز همیشه رد می‌شود.
 */
export function parseDecimal(raw: string, kind: DecimalKind, options: ParseDecimalOptions = {}): ParsedDecimal {
  if (kind === 'money') return parseMoney(raw, options)
  if (kind === 'dim') return parseDimensionValue(dimensionText(raw), options)
  return parsePlainNumber(raw, options)
}

// ── جفت اندازه «208×210» ──────────────────────────────────────────────

/** جداکننده‌های «طول × عرض»: ×، x، *، ✕ و «در». */
const PAIR_SEPARATOR = /\s*(?:[×xX*✕✖]|\sدر\s)\s*/

export type ParsedPair =
  | { status: 'ok'; values: [string, string] }
  | { status: 'ask'; code: string; tokens: MessageTokens; choices: [ParsePairChoice, ParsePairChoice] }
  | { status: 'error'; code: string; tokens?: MessageTokens }

export interface ParsePairChoice {
  values: [string, string] | null
  label: string
}

/** آیا متن شکل «طول × عرض» دارد؟ */
export function looksLikeDimensionPair(raw: string): boolean {
  const parts = normalizeDigits(raw).trim().split(PAIR_SEPARATOR)
  return parts.length === 2 && parts.every((p) => /\d/.test(p))
}

/**
 * «208×210»، «208*210»، «208x210»، «2.08×2.10» یا «۲ در ۳» هر دو خانه را پر می‌کند (با همان قاعدهٔ سانت).
 * اگر هر دو سانت‌اند یک پرسش مشترک می‌آید، نه دو تا. null یعنی متن جفت نیست.
 */
export function parseDimensionPair(raw: string, options: ParseDecimalOptions = {}): ParsedPair | null {
  if (!looksLikeDimensionPair(raw)) return null
  const [a, b] = normalizeDigits(raw).trim().split(PAIR_SEPARATOR)
  const first = parseDecimal(a, 'dim', options)
  const second = parseDecimal(b, 'dim', options)

  if (first.status === 'error' || first.status === 'empty' || second.status === 'error' || second.status === 'empty') {
    return { status: 'error', code: 'E_DIMENSION' }
  }
  if (first.status === 'ok' && second.status === 'ok') return { status: 'ok', values: [first.value, second.value] }

  // یک یا هر دو مبهم: پاسخ «اول» برای هر دو اعمال می‌شود (همان روال «یک پرسش برای کل چسباندن»)
  const resolve = (p: ParsedDecimal, choice: 0 | 1): string | null =>
    p.status === 'ok' ? p.value : p.status === 'ask' ? p.choices[choice].value : null
  const yes0 = resolve(first, 0)
  const yes1 = resolve(second, 0)
  const no0 = resolve(first, 1)
  const no1 = resolve(second, 1)
  // هر دو بخش بالا خوانده شدند، پس متنشان عدد درستی است
  const typed = `${faText(dimensionText(a).replace(/^\+/, ''))}×${faText(dimensionText(b).replace(/^\+/, ''))}`
  const yesValues: [string, string] | null = yes0 !== null && yes1 !== null ? [yes0, yes1] : null
  const noValues: [string, string] | null = no0 !== null && no1 !== null ? [no0, no1] : null
  const yesText = yesValues ? `${faText(yesValues[0], 2)} × ${faText(yesValues[1], 2)}` : ''

  return {
    status: 'ask',
    code: 'Q_DIM_PAIR_CM',
    tokens: { typed, metres: yesText },
    choices: [
      { values: yesValues, label: yesValues ? `بله، ${yesText} متر` : 'بله' },
      { values: noValues, label: 'نه' },
    ],
  }
}

/** مساحت تخته بیش از ۶۰ متر؟ (هشدار غیرمانع «مساحت این فرش … متر شد؛ درست است؟») */
export function isLargeArea(area: Dec | string | number | null | undefined): boolean {
  if (area === null || area === undefined || area === '') return false
  return cmp(dec(area), LARGE_AREA_M2) > 0
}

/** متن فارسی یک عدد برای پیام‌ها («۲٫۰۸»، «۱۰٬۰۰۰»). */
export function faDecimalText(value: string | number, minDecimals = 0): string {
  return typeof value === 'number' && !Number.isFinite(value) ? '' : faText(dec(value), minDecimals)
}

// ── سازگاری با MoneyInput قدیمی ───────────────────────────────────────

/**
 * تبدیل ورودی متنی به عدد (ارقام فارسی و جداکننده‌ها را می‌پذیرد) — فقط برای MoneyInput قدیمی.
 * «٫» و «/» میان رقم‌ها ممیزند و «٬» و «,» جداکنندهٔ هزارگان؛ منفی پذیرفته نیست. کد تازه از
 * `parseDecimal` (و NumberField) استفاده کند.
 */
export function parseNumber(raw: string): number | undefined {
  const text = normalizeNumericText(raw).replace(/[,٬]/g, '').replace(/[^\d.]/g, '')
  if (!text || (text.match(/\./g) ?? []).length > 1 || !NUMBER_SHAPE.test(text)) return undefined
  const n = Number(text)
  return Number.isFinite(n) ? n : undefined
}

