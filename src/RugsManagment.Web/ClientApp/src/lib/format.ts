/**
 * قالب‌بندی فارسی سمت کلاینت — معادل PersianFormat.cs در سرور.
 * ارقام فارسی، تاریخ شمسی و واحد پول، تا نمایش رِیزور و Vue یکسان باشد.
 *
 * واحد پول هرگز ثابت نوشته نمی‌شود: «تومان» یا «ریال» و تعداد رقم اعشار مبلغ از app-context کارگاه
 * می‌آید (`currency.label`، `moneyScale`). دلار گمرکی جداست و همیشه «دلار» با دو رقم اعشار.
 */
import { appContext } from '@/lib/context'
import { CurrencyUnit } from '@/lib/types/platform'
import { round, toPlainString, tryDec } from '@/lib/num'

/** ارقام لاتین را به فارسی تبدیل می‌کند (جداکننده‌ها دست‌نخورده می‌مانند). */
export function toPersianDigits(value: string | number): string {
  return String(value).replace(/\d/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[Number(d)])
}

/** عدد با جداکنندهٔ هزارگان و ارقام فارسی؛ «—» برای مقدار خالی. */
export function faNumber(value: number | null | undefined, maximumFractionDigits = 0): string {
  if (value == null || Number.isNaN(value)) return '—'
  return toPersianDigits(new Intl.NumberFormat('en-US', { maximumFractionDigits }).format(value))
}

/**
 * عدد دقیق (رشتهٔ اعشاری یا عدد) با جداکنندهٔ هزارگان و ارقام فارسی، گرد به `maxDecimals` رقم (نصفه
 * دور از صفر) و دست‌کم `minDecimals` رقم: ۲٫۵ با (۲، ۲) → «۲.۵۰». همان شکل `faNumber` (جداکنندهٔ «,» و
 * ممیز «.» مثل نمایش سرور)، ولی بی‌خطای ممیز شناور.
 */
export function faDecimal(value: unknown, maxDecimals = 2, minDecimals = 0): string {
  const d = tryDec(value)
  if (!d) return '—'
  const plain = toPlainString(round(d, maxDecimals), Math.min(minDecimals, maxDecimals))
  const negative = plain.startsWith('-')
  const [intPart, fracPart] = plain.replace('-', '').split('.')
  const grouped = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, ',')
  return toPersianDigits((negative ? '-' : '') + grouped + (fracPart ? `.${fracPart}` : ''))
}

/** واحد پول کارگاه («تومان» یا «ریال») از app-context. */
export function currencyLabel(): string {
  return appContext().currency.label
}

/** مبلغ با واحد کارگاه: «۱,۲۵۰,۰۰۰ تومان» (یا «ریال» در کارگاه ریالی). */
export function faMoney(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return '—'
  const ctx = appContext()
  return `${faDecimal(value, ctx.moneyScale)} ${ctx.currency.label}`
}

/** مبلغ بدون واحد، با تعداد رقم اعشار کارگاه. */
export function faMoneyAmount(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return '—'
  return faDecimal(value, appContext().moneyScale)
}

/** دلار گمرکی: «۶۰۷.۹۵ دلار» (دو رقم اعشار). */
export function faUsd(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return '—'
  return `${faDecimal(value, 2)} دلار`
}

/** مبلغ با واحد صریح (مثلاً مبلغ رسید ریالی در کارگاه تومانی). */
export function faCurrency(value: number | null | undefined, unit: CurrencyUnit): string {
  if (value == null || Number.isNaN(value)) return '—'
  if (unit === CurrencyUnit.Usd) return faUsd(value)
  const label = unit === CurrencyUnit.Rial ? 'ریال' : 'تومان'
  return `${faDecimal(value, appContext().moneyScale)} ${label}`
}

/** متراژ: «۱۳.۵۱ متر» (دو رقم اعشار، صفر انتهایی حذف). */
export function faArea(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return '—'
  return `${faDecimal(value, 2)} متر`
}

/** شمارنده با ارقام فارسی. */
export function faCount(value: number | null | undefined): string {
  return faNumber(value)
}

/**
 * تاریخ شمسی. تقویم فارسی از خود مرورگر گرفته می‌شود (Intl)، پس
 * نیازی به کتابخانهٔ اضافه نیست. منطقهٔ زمانی روی تهران ثابت است تا
 * با نمایش سمت سرور یکی باشد.
 */
const dateFormatter = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
  year: 'numeric',
  month: 'long',
  day: 'numeric',
  timeZone: 'Asia/Tehran',
})

const dateTimeFormatter = new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
  year: 'numeric',
  month: 'long',
  day: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  timeZone: 'Asia/Tehran',
})

/** «۵ مرداد ۱۴۰۵» */
export function faDate(value: string | Date | null | undefined): string {
  if (!value) return '—'
  const d = value instanceof Date ? value : new Date(value)
  return Number.isNaN(d.getTime()) ? '—' : dateFormatter.format(d)
}

/** «۵ مرداد ۱۴۰۵، ۱۴:۳۰» */
export function faDateTime(value: string | Date | null | undefined): string {
  if (!value) return '—'
  const d = value instanceof Date ? value : new Date(value)
  return Number.isNaN(d.getTime()) ? '—' : dateTimeFormatter.format(d)
}

/** فاصلهٔ نسبی: «۳ روز پیش»، «۲ ساعت پیش»، «همین حالا». */
export function faRelative(value: string | Date | null | undefined): string {
  if (!value) return '—'
  const d = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(d.getTime())) return '—'

  const seconds = (Date.now() - d.getTime()) / 1000
  if (seconds < 0) return faDate(d)
  if (seconds < 60) return 'همین حالا'
  if (seconds < 3600) return `${toPersianDigits(Math.floor(seconds / 60))} دقیقه پیش`
  if (seconds < 86400) return `${toPersianDigits(Math.floor(seconds / 3600))} ساعت پیش`
  if (seconds < 86400 * 30) return `${toPersianDigits(Math.floor(seconds / 86400))} روز پیش`
  return faDate(d)
}
