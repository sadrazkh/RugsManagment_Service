/**
 * حساب دقیق اعشاری — آینهٔ `Domain/Common/Num` و `AreaMath` در سرور.
 *
 * چرا: ضرب ممیز شناور جاوااسکریپت برای متراژ فرش غلط است (`2.30 * 1.65 = 3.7949999999999995`، که گرد
 * به دو رقم می‌شود ۳٫۷۹ و نه ۳٫۸۰ِ اکسل بازرگان). پس هر عدد این‌جا یک «عدد صحیح بزرگ × ۱۰ به توان
 * منفیِ تعداد رقم اعشار» است (`BigInt`) و ضرب و جمع دقیق انجام می‌شود؛ هیچ‌جا ضرب `Number` نیست.
 *
 * گردکردن همیشه «نصفه دور از صفر» است (۲٫۶۷۵ → ۲٫۶۸، −۲٫۶۷۵ → −۲٫۶۸)، مثل ROUND اکسل و
 * `round(numeric, n)` پستگرس و `MidpointRounding.AwayFromZero` سرور؛ نه گرد بانکی.
 *
 * «قاعدهٔ دسته» (D.3): هر چیز اول برای یک تخته گرد می‌شود و بعد در تعداد ضرب، همه‌جا یکسان.
 */

/** عدد اعشاری دقیق: `units × 10^-scale`. */
export interface Dec {
  readonly units: bigint
  readonly scale: number
}

/** ورودی‌های پذیرفتنی: رشتهٔ عددی لاتین («2.08»، «-1e3»)، عدد جاوااسکریپت، BigInt یا Dec. */
export type DecInput = Dec | string | number | bigint

const DECIMAL = /^([+-]?)(\d*)(?:\.(\d*))?(?:e([+-]?\d+))?$/i

function pow10(n: number): bigint {
  return 10n ** BigInt(n)
}

function isDec(value: unknown): value is Dec {
  return typeof value === 'object' && value !== null && typeof (value as Dec).units === 'bigint'
}

function parseText(text: string): Dec | null {
  const m = DECIMAL.exec(text.trim())
  if (!m) return null
  const [, sign, intPart = '', fracPart = '', expPart] = m
  if (intPart === '' && fracPart === '') return null

  let digits = (intPart + fracPart).replace(/^0+(?=\d)/, '') || '0'
  let scale = fracPart.length
  const exp = expPart ? Number(expPart) : 0
  if (!Number.isSafeInteger(exp) || Math.abs(exp) > 400) return null

  // توان ده: جابه‌جایی ممیز
  if (exp > 0) {
    if (exp >= scale) {
      digits += '0'.repeat(exp - scale)
      scale = 0
    } else {
      scale -= exp
    }
  } else if (exp < 0) {
    scale += -exp
  }

  const units = BigInt(digits)
  return { units: sign === '-' ? -units : units, scale }
}

/**
 * ساخت Dec. عدد جاوااسکریپت با نمایش کوتاه‌ترش خوانده می‌شود (`String(2.3)` = «2.3»)، پس 2.3 همان ۲٫۳
 * است نه ۲٫۲۹۹۹۹…؛ ورودی نامعتبر خطا می‌دهد.
 */
export function dec(value: DecInput): Dec {
  if (isDec(value)) return value
  if (typeof value === 'bigint') return { units: value, scale: 0 }
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) throw new RangeError(`عدد نامعتبر: ${value}`)
    const parsed = parseText(String(value))
    if (!parsed) throw new RangeError(`عدد نامعتبر: ${value}`)
    return parsed
  }
  const parsed = parseText(value)
  if (!parsed) throw new RangeError(`عدد نامعتبر: «${value}»`)
  return parsed
}

/** مثل `dec` ولی برای ورودی نامعتبر (یا null/undefined/رشتهٔ خالی) null برمی‌گرداند. */
export function tryDec(value: unknown): Dec | null {
  if (value === null || value === undefined || value === '') return null
  if (isDec(value)) return value
  if (typeof value === 'bigint') return { units: value, scale: 0 }
  if (typeof value === 'number') return Number.isFinite(value) ? parseText(String(value)) : null
  if (typeof value === 'string') return parseText(value)
  return null
}

/** هم‌مقیاس کردن (فقط بالا بردن مقیاس؛ مقدار عوض نمی‌شود). */
function rescale(d: Dec, scale: number): bigint {
  return scale === d.scale ? d.units : d.units * pow10(scale - d.scale)
}

export function add(a: DecInput, b: DecInput): Dec {
  const x = dec(a)
  const y = dec(b)
  const scale = Math.max(x.scale, y.scale)
  return { units: rescale(x, scale) + rescale(y, scale), scale }
}

export function sub(a: DecInput, b: DecInput): Dec {
  const y = dec(b)
  return add(a, { units: -y.units, scale: y.scale })
}

export function mul(a: DecInput, b: DecInput): Dec {
  const x = dec(a)
  const y = dec(b)
  return { units: x.units * y.units, scale: x.scale + y.scale }
}

export function neg(a: DecInput): Dec {
  const x = dec(a)
  return { units: -x.units, scale: x.scale }
}

export function abs(a: DecInput): Dec {
  const x = dec(a)
  return x.units < 0n ? { units: -x.units, scale: x.scale } : x
}

/** مقایسه: −۱، ۰ یا ۱. */
export function cmp(a: DecInput, b: DecInput): -1 | 0 | 1 {
  const x = dec(a)
  const y = dec(b)
  const scale = Math.max(x.scale, y.scale)
  const l = rescale(x, scale)
  const r = rescale(y, scale)
  return l < r ? -1 : l > r ? 1 : 0
}

export function isZero(a: DecInput): boolean {
  return dec(a).units === 0n
}

export function isNegative(a: DecInput): boolean {
  return dec(a).units < 0n
}

/**
 * گرد به `decimals` رقم اعشار، نصفه دور از صفر. نتیجه دقیقاً `decimals` رقم مقیاس دارد
 * (۲٫۵ با ۲ رقم ← ۲٫۵۰)، پس `toFixedString` همان رقم‌ها را می‌نویسد.
 */
export function round(a: DecInput, decimals: number): Dec {
  if (!Number.isInteger(decimals) || decimals < 0) throw new RangeError(`رقم اعشار نامعتبر: ${decimals}`)
  const x = dec(a)
  if (decimals >= x.scale) return { units: rescale(x, decimals), scale: decimals }

  const factor = pow10(x.scale - decimals)
  const negative = x.units < 0n
  const magnitude = negative ? -x.units : x.units
  let q = magnitude / factor
  const r = magnitude % factor
  if (r * 2n >= factor) q += 1n
  return { units: negative ? -q : q, scale: decimals }
}

/** حذف صفرهای انتهایی اعشار (۲٫۵۰ → ۲٫۵). */
export function normalize(a: DecInput): Dec {
  let { units, scale } = dec(a)
  while (scale > 0 && units % 10n === 0n) {
    units /= 10n
    scale--
  }
  return { units, scale }
}

/** تعداد رقم اعشار معنادار (۲٫۵۰ → ۱، ۳ → ۰). */
export function significantDecimals(a: DecInput): number {
  return normalize(a).scale
}

/** آیا بیش از `decimals` رقم اعشار غیرصفر ندارد (آینهٔ `Num.HasAtMostDecimals`). */
export function hasAtMostDecimals(a: DecInput, decimals: number): boolean {
  return significantDecimals(a) <= decimals
}

export function isInteger(a: DecInput): boolean {
  return significantDecimals(a) === 0
}

/**
 * عددی که از اکسل یا JSON به‌صورت double آمده (2.2999999999999998) را تمیز می‌کند: اول ۱۵ رقم معنادار
 * (همان تبدیل double→decimal سرور در `Num.Snap`) و بعد گرد به `decimals` رقم.
 */
export function snap(value: number, decimals = 2): Dec {
  if (!Number.isFinite(value)) throw new RangeError(`عدد نامعتبر: ${value}`)
  return round(dec(value.toPrecision(15)), decimals)
}

/**
 * متن لاتین بدون جداکنندهٔ هزارگان: «2.08»، «-10000000». صفرهای انتهایی اعشار تا `minDecimals` نگه داشته
 * و بقیه حذف می‌شوند (۲٫۵۰ با minDecimals=۲ ← «2.50»، با ۰ ← «2.5»).
 */
export function toPlainString(a: DecInput, minDecimals = 0): string {
  const x = dec(a)
  const target = Math.max(minDecimals, significantDecimals(x))
  const fixed = target >= x.scale ? { units: rescale(x, target), scale: target } : round(x, target)
  const negative = fixed.units < 0n
  const digits = (negative ? -fixed.units : fixed.units).toString().padStart(fixed.scale + 1, '0')
  const intPart = fixed.scale === 0 ? digits : digits.slice(0, -fixed.scale)
  const fracPart = fixed.scale === 0 ? '' : digits.slice(-fixed.scale)
  const text = fracPart ? `${intPart}.${fracPart}` : intPart
  return negative && fixed.units !== 0n ? `-${text}` : text
}

/** همان رقم‌ها با دقیقاً `decimals` رقم اعشار (پس از گرد): ۲٫۵ → «2.50». */
export function toFixedString(a: DecInput, decimals: number): string {
  return toPlainString(round(a, decimals), decimals)
}

/**
 * عدد جاوااسکریپت برای JSON یا نمایش. فقط در مرز (ارسال به سرور) به کار برود؛ حساب همیشه روی Dec.
 * عدد تا ۱۵ رقم معنادار دقیق است که برای مبلغ تومانی (تا ۹۹۹ تریلیون) کافی است.
 */
export function toNumber(a: DecInput): number {
  return Number(toPlainString(a))
}

// ═══════════════════════════════════════════════════════════════════
// قاعدهٔ دسته — آینهٔ AreaMath.cs
// ═══════════════════════════════════════════════════════════════════

/** ضریب دقیق متر مربع به فوت مربع (نه ۱۰٫۷۶۳۹). */
export const SQUARE_FEET_PER_SQUARE_METER = '10.7639104'

/** دقت ابعاد و مساحت (متر). */
export const DIMENSION_DECIMALS = 2

/** دقت مبلغ دلاری. */
export const USD_DECIMALS = 2

function quantity(qty: number): bigint {
  if (!Number.isSafeInteger(qty) || qty < 0) throw new RangeError(`تعداد نامعتبر: ${qty}`)
  return BigInt(qty)
}

/** مساحت یک تخته: ROUND(L×W, 2)؛ هر ضلع هم اول به دو رقم گرد می‌شود (ستون numeric(9,2)). */
export function pieceArea(length: DecInput, width: DecInput): Dec {
  return round(mul(round(length, DIMENSION_DECIMALS), round(width, DIMENSION_DECIMALS)), DIMENSION_DECIMALS)
}

/** مساحت یک ردیف: مساحت تخته × تعداد. */
export function lineArea(area: DecInput, qty: number): Dec {
  return mul(area, quantity(qty))
}

/** ارزش دلاری یک تخته: ROUND(مساحت تخته × دلار هر متر، 2). */
export function pieceUsd(area: DecInput, usdPerM2: DecInput): Dec {
  return round(mul(area, usdPerM2), USD_DECIMALS)
}

/** ارزش دلاری یک ردیف: دلار تخته × تعداد. */
export function lineUsd(usd: DecInput, qty: number): Dec {
  return mul(usd, quantity(qty))
}

/** مبلغ متری یک تخته: ROUND(نرخ × مساحت تخته، MoneyScale). */
export function pieceMoney(ratePerM2: DecInput, area: DecInput, moneyScale: number): Dec {
  return round(mul(ratePerM2, area), moneyScale)
}

/** مبلغ دانه‌ای یک تخته: ROUND(نرخ، MoneyScale). */
export function pieceMoneyPerPiece(ratePerPiece: DecInput, moneyScale: number): Dec {
  return round(ratePerPiece, moneyScale)
}

/** مبلغ یک ردیف: مبلغ تخته × تعداد. */
export function lineMoney(money: DecInput, qty: number): Dec {
  return mul(money, quantity(qty))
}

/**
 * تبدیل مبلغ یک تخته از ارز دیگر به ارز پایه، باز هم برای هر تخته: ROUND(مبلغ × نرخ، MoneyScale).
 * ۷٫۲۹ × ۱۰۵٬۰۰۰٫۵ → ۷۶۵٬۴۵۴ برای هر تخته → ۳۸٬۲۷۲٬۷۰۰ برای ۵۰ تخته.
 */
export function convertPiece(amount: DecInput, rate: DecInput, moneyScale: number): Dec {
  return round(mul(amount, rate), moneyScale)
}

/** مساحت به فوت مربع. */
export function toSquareFeet(squareMeters: DecInput): Dec {
  return mul(squareMeters, SQUARE_FEET_PER_SQUARE_METER)
}

/** کوتاه‌نوشت برای جزیره‌ها: مساحت یک تخته به‌صورت عدد (یا null اگر یکی از ضلع‌ها نیست). */
export function pieceAreaNumber(length: unknown, width: unknown): number | null {
  const l = tryDec(length)
  const w = tryDec(width)
  if (!l || !w) return null
  return toNumber(pieceArea(l, w))
}
