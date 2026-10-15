import { describe, expect, it } from 'vitest'
import {
  addIsoDays,
  addJalaliMonths,
  faJalali,
  faJalaliLong,
  gregorianToJalali,
  isLeapJalaliYear,
  isoToJalali,
  jalaliDotted,
  jalaliMonthLength,
  jalaliToGregorian,
  jalaliToIso,
  jalaliWeekday,
  parseJalali,
} from './jalali'

/** تاریخ شمسی همان روز از Intl (ICU) — داور مستقل برای الگوریتم. */
const intl = new Intl.DateTimeFormat('en-US-u-ca-persian-nu-latn', {
  year: 'numeric',
  month: 'numeric',
  day: 'numeric',
  timeZone: 'UTC',
})
function intlJalali(gy: number, gm: number, gd: number) {
  const parts = intl.formatToParts(new Date(Date.UTC(gy, gm - 1, gd)))
  const get = (t: string) => Number(parts.find((p) => p.type === t)?.value)
  return { jy: get('year'), jm: get('month'), jd: get('day') }
}

describe('تبدیل شمسی ↔ میلادی', () => {
  it('۱۴۰۵/۰۶/۱۵ ↔ 2026-09-06 (پذیرش فاز سکو)', () => {
    expect(isoToJalali('2026-09-06')).toEqual({ jy: 1405, jm: 6, jd: 15 })
    expect(jalaliToIso({ jy: 1405, jm: 6, jd: 15 })).toBe('2026-09-06')
  })

  it.each([
    ['2025-03-21', 1404, 1, 1],
    ['2024-03-20', 1403, 1, 1],
    ['2025-03-20', 1403, 12, 30],
    ['2026-03-20', 1404, 12, 29],
    ['2026-03-21', 1405, 1, 1],
    ['2000-01-01', 1378, 10, 11],
    ['1979-02-11', 1357, 11, 22],
    ['2025-11-02', 1404, 8, 11],
  ])('%s = %i/%i/%i', (iso, jy, jm, jd) => {
    expect(isoToJalali(iso)).toEqual({ jy, jm, jd })
    expect(jalaliToIso({ jy, jm, jd })).toBe(iso)
  })

  it('بیش از ۲۰۰ تاریخ (۱۳۵۰ تا ۱۴۳۰) با Intl و رفت‌وبرگشت یکی است', () => {
    let checked = 0
    for (let day = Date.UTC(1971, 2, 1); day <= Date.UTC(2051, 2, 1); day += 86_400_000 * 97) {
      const d = new Date(day)
      const [gy, gm, gd] = [d.getUTCFullYear(), d.getUTCMonth() + 1, d.getUTCDate()]
      const j = gregorianToJalali(gy, gm, gd)
      expect(j, `${gy}-${gm}-${gd}`).toEqual(intlJalali(gy, gm, gd))
      expect(jalaliToGregorian(j.jy, j.jm, j.jd)).toEqual({ gy, gm, gd })
      checked++
    }
    expect(checked).toBeGreaterThan(200)
  })

  it('نوروز و آخر اسفند هر سال ۱۳۷۰ تا ۱۴۲۰ با Intl یکی است', () => {
    for (let jy = 1370; jy <= 1420; jy++) {
      const first = jalaliToGregorian(jy, 1, 1)
      expect(intlJalali(first.gy, first.gm, first.gd), `${jy}/1/1`).toEqual({ jy, jm: 1, jd: 1 })
      const lastDay = jalaliMonthLength(jy, 12)
      const last = jalaliToGregorian(jy, 12, lastDay)
      expect(intlJalali(last.gy, last.gm, last.gd), `${jy}/12/${lastDay}`).toEqual({ jy, jm: 12, jd: lastDay })
    }
  })

  it('سال کبیسه', () => {
    expect(isLeapJalaliYear(1403)).toBe(true)
    expect(isLeapJalaliYear(1404)).toBe(false)
    expect(isLeapJalaliYear(1405)).toBe(false)
    expect(isLeapJalaliYear(1408)).toBe(true)
    expect(jalaliMonthLength(1403, 12)).toBe(30)
    expect(jalaliMonthLength(1405, 12)).toBe(29)
    expect(jalaliMonthLength(1405, 6)).toBe(31)
    expect(jalaliMonthLength(1405, 7)).toBe(30)
  })

  it('روز هفته: ۱۴۰۵/۰۶/۱۵ یکشنبه است (شنبه ۰)', () => {
    expect(jalaliWeekday({ jy: 1405, jm: 6, jd: 15 })).toBe(1)
    expect(jalaliWeekday({ jy: 1405, jm: 6, jd: 14 })).toBe(0)
  })

  it('جابه‌جایی روز و ماه', () => {
    expect(addIsoDays('2026-09-06', -1)).toBe('2026-09-05')
    expect(addIsoDays('2026-03-21', -1)).toBe('2026-03-20')
    expect(addJalaliMonths({ jy: 1405, jm: 6, jd: 31 }, 1)).toEqual({ jy: 1405, jm: 7, jd: 30 })
    expect(addJalaliMonths({ jy: 1405, jm: 1, jd: 1 }, -1)).toEqual({ jy: 1404, jm: 12, jd: 1 })
  })

  it('ISO نامعتبر', () => {
    expect(isoToJalali('2026-02-30')).toBeNull()
    expect(isoToJalali('')).toBeNull()
    expect(isoToJalali('x')).toBeNull()
  })
})

describe('خواندن تاریخ تایپ‌شده', () => {
  const ok = (text: string) => {
    const r = parseJalali(text)
    return r.status === 'ok' ? r.iso : r
  }

  it.each([
    '1405/6/15',
    '1405/06/15',
    '1405.06.15',
    '1405-06-15',
    '14050615',
    '۱۴۰۵/۰۶/۱۵',
    '١٤٠٥/٠٦/١٥',
    '۱۴۰۵٫۰۶٫۱۵',
    ' 1405 / 6 / 15 ',
    '15/06/1405',
    '05/6/15',
  ])('«%s» → 2026-09-06', (text) => {
    expect(ok(text)).toBe('2026-09-06')
  })

  it('نشانه‌های جهت متن (کپی از متن فارسی) نادیده گرفته می‌شوند', () => {
    const rlm = String.fromCharCode(0x200f)
    const lrm = String.fromCharCode(0x200e)
    expect(ok(`${rlm}۱۴۰۵/۰۶/۱۵${lrm}`)).toBe('2026-09-06')
  })

  it('سال دو رقمی پیش از ۵۰ = ۱۴۰۰ به بعد، از ۵۰ = ۱۳۰۰ به بعد', () => {
    expect(ok('99/1/1')).toBe(jalaliToIso({ jy: 1399, jm: 1, jd: 1 }))
  })

  it('روز ناممکن', () => {
    expect(parseJalali('1405/06/32')).toEqual({ status: 'error', code: 'E_DATE_IMPOSSIBLE', date: '۱۴۰۵/۰۶/۳۲' })
    expect(parseJalali('1405/12/30')).toEqual({ status: 'error', code: 'E_DATE_IMPOSSIBLE', date: '۱۴۰۵/۱۲/۳۰' })
    expect(parseJalali('1403/12/30').status).toBe('ok')
    expect(parseJalali('1405/13/01').status).toBe('error')
  })

  it('سال میلادی', () => {
    expect(parseJalali('2026/09/06')).toEqual({ status: 'error', code: 'E_DATE_YEAR' })
    expect(parseJalali('1501/01/01')).toEqual({ status: 'error', code: 'E_DATE_YEAR' })
  })

  it('شکل نادرست', () => {
    expect(parseJalali('1405/06').status).toBe('error')
    expect(parseJalali('abc').status).toBe('error')
    expect(parseJalali('140/06/15').status).toBe('error')
    expect(parseJalali('')).toEqual({ status: 'empty' })
  })
})

/**
 * فیکسچر مشترک تاریخ با xUnit (`tests/fixtures/jalali-dates.json`، H.4) اگر گذاشته شده باشد:
 * `{ "dates": [{ "iso": "2026-09-06", "jalali": "1405/06/15" }], "parse": [{ "text": "۱۴۰۵/۰۶/۳۲", "iso": null, "error": "E_DATE_IMPOSSIBLE" }] }`
 */
interface JalaliFixture {
  dates?: { iso: string; jalali: string }[]
  parse?: { text: string; iso: string | null; error?: string }[]
}
const sharedFixture = Object.values(
  import.meta.glob<JalaliFixture>('../../../../../tests/fixtures/jalali-dates.json', { import: 'default', eager: true }),
)[0]

describe('فیکسچر مشترک تاریخ (tests/fixtures/jalali-dates.json)', () => {
  it.skipIf(!sharedFixture?.dates?.length)('هر جفت شمسی ↔ میلادی', () => {
    for (const { iso, jalali } of sharedFixture!.dates!) {
      const j = isoToJalali(iso)
      expect(j && `${j.jy}/${String(j.jm).padStart(2, '0')}/${String(j.jd).padStart(2, '0')}`, iso).toBe(jalali)
      const [jy, jm, jd] = jalali.split('/').map(Number)
      expect(jalaliToIso({ jy, jm, jd }), jalali).toBe(iso)
    }
  })

  it.skipIf(!sharedFixture?.parse?.length)('هر متن تایپ‌شده', () => {
    for (const { text, iso, error } of sharedFixture!.parse!) {
      const r = parseJalali(text)
      if (iso) expect(r.status === 'ok' ? r.iso : r, text).toBe(iso)
      else expect(r.status === 'error' ? r.code : r.status, text).toBe(error ?? 'error')
    }
  })
})

describe('نمایش', () => {
  it('ارقام فارسی و شکل اکسل', () => {
    expect(faJalali('2026-09-06')).toBe('۱۴۰۵/۰۶/۱۵')
    expect(faJalaliLong('2026-09-06')).toBe('۱۵ شهریور ۱۴۰۵')
    expect(jalaliDotted('2026-09-06')).toBe('1405.06.15')
    expect(faJalali(null)).toBe('—')
  })
})

describe('سال بیرون از بازه (یافتهٔ بازبینی فاز ۱)', () => {
  it.each(['0024-05-01', '0001-01-01', '9999-12-31', '1500-06-01', '2300-01-01'])('«%s» → null و «—»، نه خطا', (iso) => {
    expect(() => isoToJalali(iso)).not.toThrow()
    expect(isoToJalali(iso)).toBeNull()
    expect(faJalali(iso)).toBe('—')
    expect(faJalaliLong(iso)).toBe('—')
    expect(jalaliDotted(iso)).toBe('')
  })

  it('مرزهای بازه همچنان تبدیل می‌شوند', () => {
    expect(isoToJalali('1821-03-21')).toEqual({ jy: 1200, jm: 1, jd: 1 })
    expect(faJalali('2026-09-06')).toBe('۱۴۰۵/۰۶/۱۵')
  })
})
