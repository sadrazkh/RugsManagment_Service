import { describe, expect, it } from 'vitest'
import {
  isLargeArea,
  looksLikeDimensionPair,
  normalizeDigits,
  parseDecimal,
  parseDimensionPair,
  parseNumber,
  type ParsedDecimal,
} from './money'

const money0 = { scale: 0, currencyLabel: 'تومان' }
const value = (r: ParsedDecimal) => (r.status === 'ok' ? r.value : r.status)

describe('parseDecimal — ممیز و رقم', () => {
  it.each([
    ['2.5', '2.5'],
    ['۲٫۵', '2.5'],
    ['۲/۵', '2.5'],
    ['٢٫٥', '2.5'],
    [' 2.5 ', '2.5'],
  ])('عدد ساده «%s» → %s', (text, expected) => {
    expect(value(parseDecimal(text, 'number'))).toBe(expected)
  })

  it('نیم‌فاصله، فاصلهٔ نشکن و نشانه‌های جهت (کپی از اکسل یا کیبورد) پاک می‌شوند', () => {
    const [zwnj, rlm, lrm, alm, nbsp, bom] = [0x200c, 0x200f, 0x200e, 0x061c, 0x00a0, 0xfeff].map((c) => String.fromCharCode(c))
    expect(value(parseDecimal(`${rlm}۲٫۵${lrm}`, 'number'))).toBe('2.5')
    expect(value(parseDecimal(`۱${nbsp}۲۵۰${zwnj}۰۰۰`, 'money', money0))).toBe('1250000')
    expect(value(parseDecimal(`${bom}${alm}2,08`, 'dim'))).toBe('2.08')
  })

  it('بیش از یک ممیز رد می‌شود', () => {
    expect(parseDecimal('1.2.3', 'number')).toEqual({ status: 'error', code: 'E_NUMBER' })
    expect(parseDecimal('1.000.000', 'money', money0)).toEqual({ status: 'error', code: 'E_NUMBER' })
  })

  it('خالی', () => {
    expect(parseDecimal('', 'money', money0)).toEqual({ status: 'empty' })
    expect(parseDecimal('  ', 'dim')).toEqual({ status: 'empty' })
  })

  it('منفی فقط با اجازه', () => {
    expect(parseDecimal('-5', 'number')).toEqual({ status: 'error', code: 'E_NEGATIVE' })
    expect(value(parseDecimal('-5', 'number', { allowNegative: true }))).toBe('-5')
    expect(value(parseDecimal('−۵۰۰', 'money', { ...money0, allowNegative: true }))).toBe('-500')
  })

  it('حداکثر رقم اعشار در عدد ساده', () => {
    expect(parseDecimal('45.555', 'number', { scale: 2 })).toEqual({ status: 'error', code: 'E_DECIMALS_MAX', tokens: { n: 2 } })
    expect(parseDecimal('3.5', 'number', { scale: 0 })).toEqual({ status: 'error', code: 'E_INTEGER' })
  })
})

describe('parseDecimal — مبلغ', () => {
  it.each([
    ['۱۰م', '10000000'],
    ['۱۰ میلیون', '10000000'],
    ['۸۵ه', '85000'],
    ['۸۵ هزار', '85000'],
    ['۲٫۵م', '2500000'],
    ['1,250,000', '1250000'],
    ['۱٬۲۵۰٬۰۰۰', '1250000'],
    ['۱۰۰۰ تومان', '1000'],
    ['10.0', '10'],
  ])('«%s» → %s', (text, expected) => {
    expect(value(parseDecimal(text, 'money', money0))).toBe(expected)
  })

  it('«10.000» با صفر رقم اعشار پرسیده می‌شود', () => {
    const r = parseDecimal('10.000', 'money', money0)
    expect(r.status).toBe('ask')
    if (r.status !== 'ask') return
    expect(r.code).toBe('Q_MONEY_THOUSANDS')
    expect(r.tokens.amount).toBe('۱۰٬۰۰۰')
    expect(r.choices.map((c) => c.value)).toEqual(['10000', '10'])
  })

  it('«10.500» اگر «نه» گفت دیگر عدد درستی نیست', () => {
    const r = parseDecimal('10.500', 'money', money0)
    expect(r.status === 'ask' && r.choices[1].value).toBe(null)
  })

  it('اعشار دیگر با صفر رقم رد می‌شود', () => {
    expect(parseDecimal('10.5', 'money', money0)).toEqual({ status: 'error', code: 'E_MONEY_DECIMALS', tokens: { currency: 'تومان' } })
    expect(parseDecimal('10.50', 'money', money0).status).toBe('error')
  })

  it('با دو رقم اعشار «10.000» همان ده است', () => {
    expect(value(parseDecimal('10.000', 'money', { scale: 2, currencyLabel: 'تومان' }))).toBe('10')
    expect(value(parseDecimal('10.25', 'money', { scale: 2, currencyLabel: 'تومان' }))).toBe('10.25')
    expect(parseDecimal('10.255', 'money', { scale: 2, currencyLabel: 'تومان' })).toEqual({
      status: 'error',
      code: 'E_MONEY_SCALE',
      tokens: { currency: 'تومان', n: 2 },
    })
  })
})

describe('parseDecimal — اندازه (G.4)', () => {
  it.each([
    ['۲/۰۸', '2.08'],
    ['۲٫۰۸', '2.08'],
    ['2,08', '2.08'],
    ['2.08', '2.08'],
    ['12', '12'],
    ['0.05', '0.05'],
    ['30.00', '30'],
  ])('«%s» → %s متر', (text, expected) => {
    expect(value(parseDecimal(text, 'dim'))).toBe(expected)
  })

  it('«40» و «208» تأیید سانت', () => {
    for (const [text, metres] of [
      ['40', '0.40'],
      ['208', '2.08'],
      ['3000', '30.00'],
    ]) {
      const r = parseDecimal(text, 'dim')
      expect(r.status, text).toBe('ask')
      if (r.status !== 'ask') continue
      expect(r.code).toBe('Q_DIM_CM')
      expect(r.choices[0].value).toBe(metres)
      expect(r.choices[1].value).toBeNull()
    }
  })

  it('«25» و «30» می‌پرسد متر یا سانت، سانت اول', () => {
    for (const [text, cm] of [
      ['25', '0.25'],
      ['30', '0.30'],
      ['13', '0.13'],
    ]) {
      const r = parseDecimal(text, 'dim')
      expect(r.status, text).toBe('ask')
      if (r.status !== 'ask') continue
      expect(r.code).toBe('Q_DIM_UNIT')
      expect(r.choices[0].value).toBe(cm)
      expect(r.choices[1].value).toBe(text)
    }
  })

  it('ضلع معقول از ترجیحات کارگاه', () => {
    expect(parseDecimal('11', 'dim', { maxPlausibleSideM: 10 }).status).toBe('ask')
    expect(value(parseDecimal('11', 'dim', { maxPlausibleSideM: 12 }))).toBe('11')
  })

  it('بیرون از حد', () => {
    for (const text of ['0.04', '30.01', '0', '-2', '2.081', '5000', 'abc', '1.2.3']) {
      expect(parseDecimal(text, 'dim'), text).toEqual({ status: 'error', code: 'E_DIMENSION' })
    }
  })
})

describe('جفت اندازه', () => {
  it('«208×210» هر دو خانه، یک پرسش', () => {
    const r = parseDimensionPair('208×210')
    expect(r?.status).toBe('ask')
    if (r?.status !== 'ask') return
    expect(r.choices[0].values).toEqual(['2.08', '2.10'])
    expect(r.choices[1].values).toBeNull()
  })

  it.each(['2.08×2.10', '2.08*2.10', '2.08x2.10', '۲٫۰۸ × ۲٫۱۰', '2.08 در 2.10'])('«%s»', (text) => {
    expect(parseDimensionPair(text)).toEqual({ status: 'ok', values: ['2.08', '2.1'] })
  })

  it('متن غیر جفت', () => {
    expect(parseDimensionPair('2.08')).toBeNull()
    expect(looksLikeDimensionPair('2.08')).toBe(false)
    expect(looksLikeDimensionPair('208×210')).toBe(true)
  })

  it('جفت با یک بخش نادرست', () => {
    expect(parseDimensionPair('2.08×0.01')).toEqual({ status: 'error', code: 'E_DIMENSION' })
  })

  it('هشدار مساحت بزرگ', () => {
    expect(isLargeArea('62.4')).toBe(true)
    expect(isLargeArea('60')).toBe(false)
    expect(isLargeArea(null)).toBe(false)
  })
})

describe('سازگاری قدیمی', () => {
  it('normalizeDigits', () => {
    expect(normalizeDigits('۱۲۳٤٥')).toBe('12345')
  })

  it('parseNumber «٫» را ممیز می‌خواند', () => {
    expect(parseNumber('۲٫۵')).toBe(2.5)
    expect(parseNumber('۱٬۲۵۰٬۰۰۰')).toBe(1250000)
    expect(parseNumber('1,250,000')).toBe(1250000)
    expect(parseNumber('')).toBeUndefined()
    expect(parseNumber('abc')).toBeUndefined()
  })
})

describe('جداکنندهٔ هزارگان فقط سر جای درست (یافتهٔ بازبینی فاز ۱)', () => {
  const grouping = { status: 'error', code: 'E_NUMBER_GROUPING' }

  it.each([
    ['1,234,567', 'number', '1234567'],
    ['۱٬۲۳۴٬۵۶۷', 'number', '1234567'],
    ['1,250,000', 'money', '1250000'],
    ['۱٬۲۵۰٬۰۰۰م', 'money', '1250000000000'],
    ['-1,000', 'money', '-1000'],
  ] as const)('«%s» (%s) → %s', (text, kind, expected) => {
    expect(value(parseDecimal(text, kind, { ...money0, allowNegative: true }))).toBe(expected)
  })

  it('با دو رقم اعشار، بخش اعشاری بعد از گروه‌ها پذیرفته است', () => {
    expect(value(parseDecimal('1,250.75', 'money', { scale: 2, currencyLabel: 'تومان' }))).toBe('1250.75')
  })

  it.each([
    // «٬» (هزارگان) به‌جای «٫» (ممیز): کلیدهای کنار هم روی کیبورد فارسی — ده یا صد برابر نمی‌شود
    ['۲٬۵م', 'money'],
    ['1,5م', 'money'],
    ['45,55', 'number'],
    ['۴۵٬۵۵', 'number'],
    ['1,23', 'number'],
    ['7,29', 'number'],
    ['1,2,3', 'number'],
    ['1234,567', 'number'],
    [',500', 'money'],
    ['1,000,00', 'money'],
  ] as const)('«%s» (%s) رد می‌شود، بی‌صدا بزرگ نمی‌شود', (text, kind) => {
    expect(parseDecimal(text, kind, money0)).toEqual(grouping)
  })

  it('اندازه همچنان «,» را ممیز می‌خواند (اکسل)', () => {
    expect(value(parseDecimal('2,08', 'dim'))).toBe('2.08')
  })
})

describe('بزرگ‌ترین ضلع معمول از app-context (یافتهٔ بازبینی فاز ۱)', () => {
  it('بی گزینهٔ صریح، مقدار کارگاه به کار می‌رود نه ۱۲ ثابت', async () => {
    const { resetAppContext } = await import('./context')
    try {
      resetAppContext({ maxPlausibleSideM: 8 })
      expect(parseDecimal('10', 'dim').status).toBe('ask')
      resetAppContext({ maxPlausibleSideM: 30 })
      expect(value(parseDecimal('25', 'dim'))).toBe('25')
      // گزینهٔ صریح هنوز مقدم است
      expect(parseDecimal('25', 'dim', { maxPlausibleSideM: 12 }).status).toBe('ask')
      // مقدار بیرون از بازهٔ سرور نادیده گرفته می‌شود (پیش‌فرض ۱۲)
      resetAppContext({ maxPlausibleSideM: 400 })
      expect(parseDecimal('13', 'dim').status).toBe('ask')
      expect(value(parseDecimal('12', 'dim'))).toBe('12')
    } finally {
      resetAppContext({})
    }
  })
})
