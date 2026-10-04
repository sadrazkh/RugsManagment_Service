/**
 * عدد به حروف فارسی — «ده میلیون تومان» زیر هر خانهٔ مبلغ (H.4).
 *
 * چرا: بازرگان صفرهای یک مبلغ را با چشم نمی‌شمارد و یک صفر اضافه یعنی ده برابر قیمت؛ خواندن مبلغ به حروف
 * این خطا را همان لحظه نشان می‌دهد. آینهٔ دقیق `App/Common/NumberWords.cs` (برای چاپ) است و باید
 * همان متن را بسازد؛ قاعده‌ها:
 * - ۰ → «صفر»؛ منفی → «منفی » + بقیه.
 * - گروه‌های سه‌رقمی با «هزار، میلیون، میلیارد، تریلیون، کوادریلیون» و میان گروه‌ها « و ».
 * - ۱۰۰۰ تنها «هزار» است (نه «یک هزار»)، ولی «یک میلیون» و «یک میلیارد».
 * - درون گروه: صدگان « و » دهگان « و » یکان (۱۲۵ → «صد و بیست و پنج»؛ ۱۱ تا ۱۹ یک واژه).
 * - اعشار تا سه رقم (بیشتر، دور از صفر گرد می‌شود؛ صفرهای انتهایی حذف): «دو و پنج دهم»،
 *   «بیست و پنج صدم»، «سه و صد و بیست و پنج هزارم».
 * - بزرگ‌تر از ۹۹۹ کوادریلیون: همان عدد با ارقام فارسی (بدون حروف، با «٬» و حداکثر دو رقم اعشار).
 *
 * حساب روی `lib/num.ts` (BigInt) است تا مبلغ‌های بزرگ هم دقیق خوانده شوند.
 */
import { abs, isNegative, round, toPlainString, tryDec, type DecInput } from '@/lib/num'

const ZERO = 'صفر'
const NEGATIVE = 'منفی'
const AND = ' و '
const MAX_SUPPORTED = 999_999_999_999_999_999n

const ONES = [
  '', 'یک', 'دو', 'سه', 'چهار', 'پنج', 'شش', 'هفت', 'هشت', 'نه',
  'ده', 'یازده', 'دوازده', 'سیزده', 'چهارده', 'پانزده', 'شانزده', 'هفده', 'هجده', 'نوزده',
]
const TENS = ['', '', 'بیست', 'سی', 'چهل', 'پنجاه', 'شصت', 'هفتاد', 'هشتاد', 'نود']
const HUNDREDS = ['', 'صد', 'دویست', 'سیصد', 'چهارصد', 'پانصد', 'ششصد', 'هفتصد', 'هشتصد', 'نهصد']
const SCALES = ['', 'هزار', 'میلیون', 'میلیارد', 'تریلیون', 'کوادریلیون']
const FRACTION_UNITS = ['دهم', 'صدم', 'هزارم']

function groupWords(value: number): string {
  const parts: string[] = []
  const hundreds = Math.trunc(value / 100)
  if (hundreds > 0) parts.push(HUNDREDS[hundreds])

  const rest = value % 100
  if (rest > 0 && rest < 20) {
    parts.push(ONES[rest])
  } else if (rest >= 20) {
    parts.push(TENS[Math.trunc(rest / 10)])
    if (rest % 10 > 0) parts.push(ONES[rest % 10])
  }
  return parts.join(AND)
}

function integerWords(value: bigint): string {
  const groups: string[] = []
  let scale = 0
  let rest = value
  while (rest > 0n) {
    const group = Number(rest % 1000n)
    rest /= 1000n
    if (group !== 0) {
      // «هزار» تنها، نه «یک هزار»؛ ولی «یک میلیون»
      const text =
        group === 1 && scale === 1 ? SCALES[1] : scale === 0 ? groupWords(group) : `${groupWords(group)} ${SCALES[scale]}`
      groups.push(text)
    }
    scale++
  }
  return groups.reverse().join(AND)
}

/** کسر ۰ تا ۰٫۹۹۹ (سه رقم) → «پنج دهم»، «بیست و پنج صدم»؛ صفرهای انتهایی حذف. */
function fractionWords(fractionDigits: string): string | null {
  let digits = fractionDigits.padEnd(3, '0').slice(0, 3)
  while (digits.length > 1 && digits.endsWith('0')) digits = digits.slice(0, -1)
  const scaled = BigInt(digits)
  if (scaled === 0n) return null
  return `${integerWords(scaled)} ${FRACTION_UNITS[digits.length - 1]}`
}

/** «#,0.##» با ارقام فارسی و «٬»/«٫» — همان `Messages.FormatNumber` سرور. */
function persianNumber(value: DecInput): string {
  const plain = toPlainString(round(value, 2))
  const negative = plain.startsWith('-')
  const [intPart, fracPart] = plain.replace('-', '').split('.')
  const grouped = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, '٬')
  const text = (negative ? '-' : '') + grouped + (fracPart ? `٫${fracPart}` : '')
  return text.replace(/\d/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[Number(d)])
}

/**
 * عدد به حروف: ۱۲۵۰۰۰۰ → «یک میلیون و دویست و پنجاه هزار»؛ ۲٫۵ → «دو و پنج دهم».
 * ورودی نامعتبر (null، متن غیرعددی) رشتهٔ خالی برمی‌گرداند.
 */
export function toWords(value: DecInput | null | undefined): string {
  const parsed = tryDec(value)
  if (!parsed) return ''

  const rounded = round(parsed, 3)
  const magnitude = toPlainString(abs(rounded))
  const [intText, fracText = ''] = magnitude.split('.')
  const integer = BigInt(intText)
  if (integer === 0n && !/[1-9]/.test(fracText)) return ZERO
  if (integer > MAX_SUPPORTED) return persianNumber(rounded)

  const words: string[] = []
  if (integer > 0n) words.push(integerWords(integer))
  const fraction = fractionWords(fracText)
  if (fraction) words.push(fraction)

  const text = words.join(AND)
  return isNegative(rounded) ? `${NEGATIVE} ${text}` : text
}

/** مبلغ به حروف همراه واحد: (۱۰٬۰۰۰٬۰۰۰، «تومان») → «ده میلیون تومان». */
export function moneyWords(amount: DecInput | null | undefined, currencyLabel: string): string {
  const words = toWords(amount)
  if (!words) return ''
  return currencyLabel.trim() ? `${words} ${currencyLabel}` : words
}
