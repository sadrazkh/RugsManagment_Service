/**
 * قالب‌بندی فارسی سمت کلاینت — معادل PersianFormat.cs در سرور.
 * ارقام فارسی، تاریخ شمسی و واحد پول، تا نمایش رِیزور و Vue یکسان باشد.
 */

/** ارقام لاتین را به فارسی تبدیل می‌کند (جداکننده‌ها دست‌نخورده می‌مانند). */
export function toPersianDigits(value: string | number): string {
  return String(value).replace(/\d/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[Number(d)])
}

/** عدد با جداکنندهٔ هزارگان و ارقام فارسی؛ «—» برای مقدار خالی. */
export function faNumber(value: number | null | undefined, maximumFractionDigits = 0): string {
  if (value == null || Number.isNaN(value)) return '—'
  return toPersianDigits(new Intl.NumberFormat('en-US', { maximumFractionDigits }).format(value))
}

/** مبلغ با واحد: «۱٬۲۵۰٬۰۰۰ تومان» */
export function faMoney(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return '—'
  return `${faNumber(value)} تومان`
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
