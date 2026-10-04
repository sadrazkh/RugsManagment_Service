/**
 * تقویم شمسی بدون کتابخانه: تبدیل شمسی ↔ میلادی، خواندن تاریخ تایپ‌شده و قالب‌بندی.
 *
 * چرا خودمان: Intl فقط شمسی «نشان» می‌دهد و راهی برای برگرداندن «۱۴۰۵/۰۶/۱۵» به میلادی ندارد؛
 * کتابخانهٔ تقویم هم برای ۶۰ خط حساب، وابستگی و حجم اضافه است. الگوریتم همان روش «سال‌های شکست»
 * (Borkowski، که jalaali-js هم به کار می‌برد) است و برای سال‌های ۱۲۰۰ تا ۱۵۰۰ با تقویم رسمی و با
 * `PersianCalendar` سرور یکی است. آزمون (`jalali.spec.ts`) صدها تاریخ را با Intl مرورگر مقایسه می‌کند.
 *
 * مقدار تاریخ در API همیشه میلادی ISO است («2026-09-06»، برای `DateOnly`)؛ شمسی فقط برای نمایش و تایپ.
 */
import { normalizeDigits } from '@/lib/money'

export interface JalaliDate {
  jy: number
  jm: number
  jd: number
}

export const JALALI_MONTHS = [
  'فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور',
  'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند',
] as const

/** نام کوتاه روزهای هفته، شنبه اول. */
export const JALALI_WEEKDAYS_SHORT = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج'] as const
export const JALALI_WEEKDAYS = ['شنبه', 'یکشنبه', 'دوشنبه', 'سه‌شنبه', 'چهارشنبه', 'پنجشنبه', 'جمعه'] as const

/** بازهٔ سال‌هایی که تبدیل برایش معتبر است و ورودی پذیرفته می‌شود. */
export const MIN_JALALI_YEAR = 1200
export const MAX_JALALI_YEAR = 1500

// ── هستهٔ الگوریتم ─────────────────────────────────────────────────────

const BREAKS = [-61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181, 1210, 1635, 2060, 2097, 2192, 2262, 2324, 2394, 2456, 3178]

const div = (a: number, b: number) => Math.trunc(a / b)
const mod = (a: number, b: number) => a - Math.trunc(a / b) * b

/** سال کبیسه‌ای؟ (`leap` صفر یعنی کبیسه) و روزِ ۱ فروردین در مارس میلادی. */
function jalCal(jy: number): { leap: number; gy: number; march: number } {
  const gy = jy + 621
  let leapJ = -14
  let jp = BREAKS[0]
  let jump = 0
  if (jy < jp || jy >= BREAKS[BREAKS.length - 1]) throw new RangeError(`سال شمسی نامعتبر: ${jy}`)

  for (let i = 1; i < BREAKS.length; i++) {
    const jm = BREAKS[i]
    jump = jm - jp
    if (jy < jm) break
    leapJ += div(jump, 33) * 8 + div(mod(jump, 33), 4)
    jp = jm
  }
  let n = jy - jp
  leapJ += div(n, 33) * 8 + div(mod(n, 33) + 3, 4)
  if (mod(jump, 33) === 4 && jump - n === 4) leapJ += 1

  const leapG = div(gy, 4) - div((div(gy, 100) + 1) * 3, 4) - 150
  const march = 20 + leapJ - leapG
  if (jump - n < 6) n = n - jump + div(jump + 4, 33) * 33
  let leap = mod(mod(n + 1, 33) - 1, 4)
  if (leap === -1) leap = 4
  return { leap, gy, march }
}

/** شمارهٔ روز ژولینی یک تاریخ میلادی. */
function g2d(gy: number, gm: number, gd: number): number {
  const d = div((gy + div(gm - 8, 6) + 100100) * 1461, 4) + div(153 * mod(gm + 9, 12) + 2, 5) + gd - 34840408
  return d - div(div(gy + 100100 + div(gm - 8, 6), 100) * 3, 4) + 752
}

function d2g(jdn: number): { gy: number; gm: number; gd: number } {
  let j = 4 * jdn + 139361631
  j = j + div(div(4 * jdn + 183187720, 146097) * 3, 4) * 4 - 3908
  const i = div(mod(j, 1461), 4) * 5 + 308
  const gd = div(mod(i, 153), 5) + 1
  const gm = mod(div(i, 153), 12) + 1
  const gy = div(j, 1461) - 100100 + div(8 - gm, 6)
  return { gy, gm, gd }
}

function j2d(jy: number, jm: number, jd: number): number {
  const r = jalCal(jy)
  return g2d(r.gy, 3, r.march) + (jm - 1) * 31 - div(jm, 7) * (jm - 7) + jd - 1
}

function d2j(jdn: number): JalaliDate {
  const gy = d2g(jdn).gy
  let jy = gy - 621
  const r = jalCal(jy)
  let k = jdn - g2d(gy, 3, r.march)
  if (k >= 0) {
    if (k <= 185) return { jy, jm: 1 + div(k, 31), jd: mod(k, 31) + 1 }
    k -= 186
  } else {
    jy -= 1
    k += 179
    if (r.leap === 1) k += 1
  }
  return { jy, jm: 7 + div(k, 30), jd: mod(k, 30) + 1 }
}

// ── تبدیل‌ها ───────────────────────────────────────────────────────────

export function isLeapJalaliYear(jy: number): boolean {
  return jalCal(jy).leap === 0
}

export function jalaliMonthLength(jy: number, jm: number): number {
  if (jm <= 6) return 31
  if (jm <= 11) return 30
  return isLeapJalaliYear(jy) ? 30 : 29
}

export function isValidJalali(jy: number, jm: number, jd: number): boolean {
  return (
    Number.isInteger(jy) && Number.isInteger(jm) && Number.isInteger(jd) &&
    jy >= MIN_JALALI_YEAR && jy <= MAX_JALALI_YEAR &&
    jm >= 1 && jm <= 12 && jd >= 1 && jd <= jalaliMonthLength(jy, jm)
  )
}

const pad = (n: number, width = 2) => String(n).padStart(width, '0')

/** میلادی (سال، ماه، روز) → شمسی. */
export function gregorianToJalali(gy: number, gm: number, gd: number): JalaliDate {
  return d2j(g2d(gy, gm, gd))
}

/** شمسی → میلادی. */
export function jalaliToGregorian(jy: number, jm: number, jd: number): { gy: number; gm: number; gd: number } {
  return d2g(j2d(jy, jm, jd))
}

const ISO_DATE = /^(\d{4})-(\d{2})-(\d{2})/

/** «2026-09-06» (یا شروع یک ISO کامل) → شمسی؛ متن نامعتبر null. */
export function isoToJalali(iso: string | null | undefined): JalaliDate | null {
  const m = iso ? ISO_DATE.exec(iso) : null
  if (!m) return null
  const [gy, gm, gd] = [Number(m[1]), Number(m[2]), Number(m[3])]
  if (gm < 1 || gm > 12 || gd < 1 || gd > 31) return null
  const j = gregorianToJalali(gy, gm, gd)
  // روز ۳۱ ماه ۳۰ روزه و مانند آن را رد کن: برگشت باید همان تاریخ باشد
  const back = jalaliToGregorian(j.jy, j.jm, j.jd)
  return back.gy === gy && back.gm === gm && back.gd === gd ? j : null
}

/** شمسی → «2026-09-06». */
export function jalaliToIso(j: JalaliDate): string {
  const g = jalaliToGregorian(j.jy, j.jm, j.jd)
  return `${pad(g.gy, 4)}-${pad(g.gm)}-${pad(g.gd)}`
}

/** «1405/06/15» با ارقام لاتین (جداکنندهٔ دلخواه؛ «.» برای شکل اکسل «1405.06.15»). */
export function formatJalali(j: JalaliDate, separator = '/'): string {
  return `${pad(j.jy, 4)}${separator}${pad(j.jm)}${separator}${pad(j.jd)}`
}

/** شمارهٔ روز ژولینی برای حساب روزها (فاصله، افزودن). */
export function jalaliDayNumber(j: JalaliDate): number {
  return j2d(j.jy, j.jm, j.jd)
}

export function jalaliFromDayNumber(jdn: number): JalaliDate {
  return d2j(jdn)
}

/** چند روز جلو/عقب. */
export function addJalaliDays(j: JalaliDate, days: number): JalaliDate {
  return d2j(j2d(j.jy, j.jm, j.jd) + days)
}

/** ISO چند روز جلو/عقب («دیروز» = −۱). */
export function addIsoDays(iso: string, days: number): string | null {
  const j = isoToJalali(iso)
  return j ? jalaliToIso(addJalaliDays(j, days)) : null
}

/** جای روز در هفتهٔ شمسی: شنبه ۰ … جمعه ۶. */
export function jalaliWeekday(j: JalaliDate): number {
  // شمارهٔ روز ژولینی ۰ دوشنبه است؛ پس شنبه = (jdn + 2) mod 7
  return (((j2d(j.jy, j.jm, j.jd) + 2) % 7) + 7) % 7
}

/** ماه بعد/قبل (برای تقویم): روز به آخرین روز ماه مقصد بریده می‌شود. */
export function addJalaliMonths(j: JalaliDate, months: number): JalaliDate {
  const index = j.jy * 12 + (j.jm - 1) + months
  const jy = Math.floor(index / 12)
  const jm = (index % 12 + 12) % 12 + 1
  return { jy, jm, jd: Math.min(j.jd, jalaliMonthLength(jy, jm)) }
}

/** امروز به وقت ایران، ISO — وقتی app-context تاریخ نداده (آزمون، صفحهٔ بی‌layout). */
export function iranTodayIso(now: Date = new Date()): string {
  try {
    return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Tehran' }).format(now)
  } catch {
    return now.toISOString().slice(0, 10)
  }
}

// ── خواندن تاریخ تایپ‌شده ─────────────────────────────────────────────

export type ParsedJalali =
  | { status: 'empty' }
  | { status: 'ok'; date: JalaliDate; iso: string }
  /** `date` همان متن نوشته‌شده با ارقام فارسی، برای «۱۴۰۵/۰۶/۳۲ وجود ندارد» */
  | { status: 'error'; code: 'E_DATE_FORMAT' | 'E_DATE_YEAR' | 'E_DATE_IMPOSSIBLE'; date?: string }

const SEPARATORS = /[/.\-٫\\،,\s]+/

function toPersianDigits(text: string): string {
  return text.replace(/\d/g, (d) => '۰۱۲۳۴۵۶۷۸۹'[Number(d)])
}

/**
 * «1405/6/15»، «1405.06.15»، «14050615»، «۱۴۰۵-۰۶-۱۵» (ارقام لاتین، فارسی یا عربی؛ جداکننده‌های / . - ٫).
 * سال دو رقمی کوتاه‌نوشت است («۰۵/۶/۱۵» = ۱۴۰۵، «۹۹/۱/۱» = ۱۳۹۹)؛ روز/ماه/سال («۱۵/۰۶/۱۴۰۵») هم پذیرفته است.
 * سال بالای ۱۵۰۰ یعنی کاربر میلادی نوشته: «سال را شمسی بنویسید».
 */
export function parseJalali(raw: string): ParsedJalali {
  // نیم‌فاصله و نشانه‌های جهت متن (RLM، LRM، ALM؛ نویسه‌های قالب) که کیبورد یا کپی جا می‌گذارد
  const text = normalizeDigits(raw).replace(/\p{Cf}/gu, '').trim()
  if (!text) return { status: 'empty' }

  let parts: string[]
  if (/^\d{8}$/.test(text)) parts = [text.slice(0, 4), text.slice(4, 6), text.slice(6, 8)]
  else parts = text.split(SEPARATORS).filter(Boolean)

  if (parts.length !== 3 || !parts.every((p) => /^\d{1,4}$/.test(p))) return { status: 'error', code: 'E_DATE_FORMAT' }

  // روز/ماه/سال: سال چهاررقمی آخر آمده
  if (parts[2].length === 4 && parts[0].length <= 2) parts = [parts[2], parts[1], parts[0]]

  const [ys, ms, ds] = parts
  if (ms.length > 2 || ds.length > 2) return { status: 'error', code: 'E_DATE_FORMAT' }

  let jy = Number(ys)
  if (ys.length <= 2) jy = jy >= 50 ? 1300 + jy : 1400 + jy
  else if (ys.length === 3) return { status: 'error', code: 'E_DATE_FORMAT' }
  const jm = Number(ms)
  const jd = Number(ds)

  if (jy > MAX_JALALI_YEAR) return { status: 'error', code: 'E_DATE_YEAR' }
  if (jy < MIN_JALALI_YEAR) return { status: 'error', code: 'E_DATE_FORMAT' }

  const shown = toPersianDigits(`${pad(jy, 4)}/${pad(jm)}/${pad(jd)}`)
  if (!isValidJalali(jy, jm, jd)) return { status: 'error', code: 'E_DATE_IMPOSSIBLE', date: shown }

  const date = { jy, jm, jd }
  return { status: 'ok', date, iso: jalaliToIso(date) }
}

// ── نمایش ─────────────────────────────────────────────────────────────

/** ISO → «۱۴۰۵/۰۶/۱۵» (ارقام فارسی)؛ «—» برای خالی یا نامعتبر. */
export function faJalali(iso: string | null | undefined, separator = '/'): string {
  const j = isoToJalali(iso)
  return j ? toPersianDigits(formatJalali(j, separator)) : '—'
}

/** ISO → «۱۵ شهریور ۱۴۰۵». */
export function faJalaliLong(iso: string | null | undefined): string {
  const j = isoToJalali(iso)
  return j ? toPersianDigits(`${j.jd} ${JALALI_MONTHS[j.jm - 1]} ${j.jy}`) : '—'
}

/** ISO → «1405.06.15» (ارقام لاتین، شکل عنوان اکسل؛ آینهٔ `PersianFormat.JalaliDotted`). */
export function jalaliDotted(iso: string | null | undefined): string {
  const j = isoToJalali(iso)
  return j ? formatJalali(j, '.') : ''
}
