import { afterEach, describe, expect, it, vi } from 'vitest'
import { parseDecimal } from '@/lib/money'
import { CostBasis } from '@/lib/types/rugs'
import { SerialMode } from '@/lib/types/platform'
import { hasLumpPurchaseShare, purchaseRateText } from './costBreakdownText'
import { acceptedSerialSuggestion, adoptSavedVersion, blocksImplicitSubmit, isAutoSerial } from './rugFormRules'
import { announceRugChanged, onRugChanged } from './rugEvents'
import { previewInvestment, salePreview } from './salePreview'

/**
 * یافته‌های تأییدشدهٔ بازبینی فاز ۲ در فرانت (p2-review.json). منطق در ماژول‌های خالص است و این‌جا بی DOM آزموده می‌شود؛
 * چیزهایی که فقط در قالب جزیره‌اند (جای ویرایشگر خرید در فرم، صفحه‌کلید خانهٔ شماره، خبر نسخهٔ تازه) با خواندن متن خود
 * فایل‌ها پاسبانی می‌شوند تا برگشتشان دیده شود.
 */

/** متن خام جزیره‌ها (vite `?raw`). */
const sources = import.meta.glob<string>(
  ['../RugForm.vue', './RugFinalSize.island.vue', './RugSplit.island.vue', '../RugSalePanel.vue', '../../components/PurchaseLinesEditor.vue', './RugsGrid.island.vue'],
  { query: '?raw', import: 'default', eager: true },
)
function source(name: string): string {
  const entry = Object.entries(sources).find(([path]) => path.endsWith(name))
  if (!entry) throw new Error(`${name} پیدا نشد`)
  return entry[1]
}

describe('ریز هزینه: جمع دقیق و نرخ ذخیره‌شده (#7، #21، #22)', () => {
  it('جمع سطرها با lib/num: سطر دروغین «سهم خریدهای یکجا» نمی‌آید', () => {
    expect(0.1 + 0.2).not.toBe(0.3) // همان دامِ جمع float
    expect(hasLumpPurchaseShare(0.3, [0.1, 0.2])).toBe(false)
    expect(hasLumpPurchaseShare(40116600.3, [39330000.1, 786600.2])).toBe(false)
    expect(hasLumpPurchaseShare(12595678.2, [12345678.15, 250000.05])).toBe(false)
    // سهم واقعی خرید یکجا هنوز دیده می‌شود
    expect(hasLumpPurchaseShare(1500, [1000, 200])).toBe(true)
    expect(hasLumpPurchaseShare(0, [])).toBe(false)
  })

  it('نرخ متری تا ۴ رقم اعشار، نه گردشده به MoneyScale', () => {
    expect(purchaseRateText(CostBasis.PerM2, 45.55, 'تومان')).toBe('هر متر ۴۵.۵۵ تومان')
    expect(purchaseRateText(CostBasis.PerPiece, 300000.1234, 'تومان')).toBe('هر تخته ۳۰۰,۰۰۰.۱۲۳۴ تومان')
    expect(purchaseRateText(CostBasis.Total, 40000000, 'تومان')).toBe('کل ۴۰,۰۰۰,۰۰۰ تومان')
    expect(purchaseRateText(CostBasis.Percent, 2)).toBe('۲٪')
  })
})

describe('پیش‌نمایش فروش (#6)', () => {
  it('ویرایش فروش با عکس بهای لحظهٔ فروش، فروش تازه با بهای امروز', () => {
    const existing = { totalInvestment: 1000 }
    expect(previewInvestment(existing, 1200)).toBe(1000)
    expect(salePreview(1600, 0, previewInvestment(existing, 1200))).toEqual({ net: 1600, profit: 600 })
    expect(previewInvestment(null, 1200)).toBe(1200)
    expect(salePreview(1600, 0, previewInvestment(null, 1200))).toEqual({ net: 1600, profit: 400 })
  })

  it('خالص کف صفر دارد و حساب دقیق است', () => {
    expect(salePreview(100, 150, 10)).toEqual({ net: 0, profit: -10 })
    expect(salePreview(0.3, 0.1, 0.1)).toEqual({ net: 0.2, profit: 0.1 })
  })

  it('جزیره سرمایهٔ پیش‌نمایش را از previewInvestment می‌گیرد', () => {
    const panel = source('RugSalePanel.vue')
    expect(panel).toContain('previewInvestment(sale.value, props.totalInvestment)')
    expect(panel).not.toMatch(/net - \(props\.totalInvestment/)
  })
})

describe('نرخ خرید و پرسش «۱۰٬۰۰۰؟» (#16)', () => {
  it('نرخ «هر متر» (دقت ۴) در کارگاه تومانی می‌پرسد', () => {
    const r = parseDecimal('300.000', 'money', { scale: 4, moneyScale: 0, currencyLabel: 'تومان' })
    expect(r.status).toBe('ask')
    if (r.status !== 'ask') return
    expect(r.code).toBe('Q_MONEY_THOUSANDS')
    expect(r.choices.map((c) => c.value)).toEqual(['300000', '300'])
  })

  it('بی moneyScale صریح، MoneyScale کارگاه از app-context (پیش‌فرض ۰) معیار است', () => {
    const r = parseDecimal('۳۰۰٫۰۰۰', 'money', { scale: 4, currencyLabel: 'تومان' })
    expect(r.status).toBe('ask')
  })

  it('«نه» روی نرخ اعشاری همان نرخ است، نه رد', () => {
    const r = parseDecimal('10.500', 'money', { scale: 4, moneyScale: 0, currencyLabel: 'تومان' })
    expect(r.status === 'ask' && r.choices.map((c) => c.value)).toEqual(['10500', '10.5'])
  })

  it('کارگاه با اعشار (USD یا MoneyScale ۲) نمی‌پرسد', () => {
    expect(parseDecimal('300.000', 'money', { scale: 4, moneyScale: 2, currencyLabel: 'تومان' })).toEqual({ status: 'ok', value: '300' })
    expect(parseDecimal('10.000', 'money', { scale: 2, currencyLabel: 'تومان' })).toEqual({ status: 'ok', value: '10' })
  })
})

describe('فرم فرش: شماره (#17، #19، #24)', () => {
  it('«پیشنهاد بعدی»: Enter و Tab در خانهٔ خالی می‌پذیرند', () => {
    expect(acceptedSerialSuggestion('Enter', false, '', '4004', SerialMode.SuggestNext)).toBe('4004')
    expect(acceptedSerialSuggestion('Tab', false, '  ', '3911 - 3960', SerialMode.SuggestNext)).toBe('3911 - 3960')
    expect(acceptedSerialSuggestion('Tab', false, '', '4004', undefined)).toBe('4004')
  })

  it('متن نوشته‌شده، Shift+Tab، کلید دیگر و حالت‌های دیگر نمی‌پذیرند', () => {
    expect(acceptedSerialSuggestion('Enter', false, '3778', '4004', SerialMode.SuggestNext)).toBeNull()
    expect(acceptedSerialSuggestion('Tab', true, '', '4004', SerialMode.SuggestNext)).toBeNull()
    expect(acceptedSerialSuggestion('a', false, '', '4004', SerialMode.SuggestNext)).toBeNull()
    expect(acceptedSerialSuggestion('Enter', false, '', null, SerialMode.SuggestNext)).toBeNull()
    expect(acceptedSerialSuggestion('Enter', false, '', '4004', SerialMode.Auto)).toBeNull()
    expect(acceptedSerialSuggestion('Enter', false, '', '4004', SerialMode.Manual)).toBeNull()
  })

  it('«خودکار» فقط برای فرش تازه خانهٔ نمایشی دارد', () => {
    expect(isAutoSerial(SerialMode.Auto, false)).toBe(true)
    expect(isAutoSerial(SerialMode.Auto, true)).toBe(false)
    expect(isAutoSerial(SerialMode.SuggestNext, false)).toBe(false)
    expect(isAutoSerial(null, false)).toBe(false)
  })

  it('قالب: Enter/Tab پیشنهاد را می‌پذیرد، «خودکار» نوشتنی نیست، صفحه‌کلید شماره متنی است', () => {
    const form = source('RugForm.vue')
    expect(form).toContain('@keydown="onSerialKeydown"')
    expect(form).toContain("message('I_SERIAL_AUTO'")
    expect(form).toContain('serialMode.value = n.mode ?? null')
    const split = source('RugSplit.island.vue')
    for (const text of [form, split]) expect(text).not.toContain('inputmode="numeric"')
    expect(split.match(/^\s+inputmode="text"$/gm)?.length).toBe(2) // هر دو خانهٔ شمارهٔ برش
  })
})

describe('فرم فرش و ویرایشگر خرید (#14، #18، #20)', () => {
  it('Enter در input ویرایشگر جلوی ارسال ضمنی فرم را می‌گیرد؛ دکمه و textarea و ترکیب حروف نه', () => {
    expect(blocksImplicitSubmit({ key: 'Enter', target: { tagName: 'INPUT' } })).toBe(true)
    expect(blocksImplicitSubmit({ key: 'Enter', target: { tagName: 'input' } })).toBe(true)
    expect(blocksImplicitSubmit({ key: 'Enter', target: { tagName: 'BUTTON' } })).toBe(false)
    expect(blocksImplicitSubmit({ key: 'Enter', target: { tagName: 'TEXTAREA' } })).toBe(false)
    expect(blocksImplicitSubmit({ key: 'Enter', target: { tagName: 'INPUT' }, isComposing: true })).toBe(false)
    expect(blocksImplicitSubmit({ key: 'a', target: { tagName: 'INPUT' } })).toBe(false)
    expect(blocksImplicitSubmit({ key: 'Enter', target: null })).toBe(false)
  })

  it('قالب: ویرایشگر در پوشه‌ای است که Enter را می‌گیرد و ذخیرهٔ موفق «در حال ذخیره» را باز می‌کند', () => {
    const form = source('RugForm.vue')
    expect(form).toMatch(/<div v-if="canSeeMoney && isEdit && rugId" @keydown="onPurchaseEditorKeydown">\s*<PurchaseLinesEditor/)
    expect(form).toMatch(/version\.value = rug\.version\s+initial = buildValues\(\)\s+initialMetadata = \{ \.\.\.metadata \}\s+saving\.value = false/)
  })

  it('نسخهٔ ذخیرهٔ ویرایشگر فقط روی همان نسخهٔ فرم برداشته می‌شود', () => {
    expect(adoptSavedVersion(7, 7, 9)).toBe(9)
    // ویرایشگر بعد از ۴۰۹ تغییر کس دیگری (۸) را خوانده و روی آن ذخیره کرده: فرم ۷ می‌ماند تا ۴۰۹ بگیرد
    expect(adoptSavedVersion(7, 8, 9)).toBe(7)
    const editor = source('PurchaseLinesEditor.vue')
    expect(editor).toContain("emit('saved', result, baseVersion)")
  })

  it('ویرایشگر خرید فقط با تغییر واقعی نرخ «تغییر کرده» می‌شود', () => {
    const editor = source('PurchaseLinesEditor.vue')
    expect(editor).toContain('@update:model-value="setRate(row, $event)"')
    expect(editor).toMatch(/function setRate\(row: Row, value: number \| null\) \{\s+if \(row\.rate === value\) return/)
    expect(editor).not.toContain('@update:model-value="markDirty"')
  })
})

describe('بزرگ‌ترین ضلع معمول کارگاه (#23)', () => {
  it('خانه‌های اندازهٔ فرم و اندازهٔ نهایی عدد ثابتی نمی‌دهند تا NumberField از app-context بخواند', () => {
    for (const name of ['RugForm.vue', 'RugFinalSize.island.vue']) {
      const text = source(name)
      expect(text, name).toContain('kind="dim"')
      expect(text, name).not.toMatch(/max-plausible-side-m|maxPlausibleSideM/)
    }
  })

  it('جدول فرش‌ها هم عدد کارگاه را از app-context می‌خواند، نه ۱۲ ثابت', () => {
    const text = source('RugsGrid.island.vue')
    expect(text).toMatch(/maxPlausibleSideM: \(\) => ctx\.maxPlausibleSideM/)
    expect(text).not.toMatch(/maxPlausibleSideM: \(\) => \d/)
  })
})

describe('نسخهٔ فرش میان جزیره‌ها (#15)', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('اندازهٔ نهایی و برش دسته نسخهٔ تازه را خبر می‌دهند و به خبر دیگران گوش می‌دهند', () => {
    const finalSize = source('RugFinalSize.island.vue')
    expect(finalSize).toMatch(/announceRugChanged\(\{ rugId: props\.rugId, version: saved\.version, source: SOURCE \}\)/)
    expect(finalSize).toContain('version.value = saved.version')
    expect(finalSize).not.toContain('props.final[')
    const split = source('RugSplit.island.vue')
    expect(split).toContain('onRugChanged(props.rugId, SOURCE')
    expect(split).toContain('version: version.value')
    expect(split).not.toContain('version: props.version')
  })

  it('خبر نسخهٔ تازه به جزیرهٔ دیگر همین فرش می‌رسد، نه به خودِ فرستنده', () => {
    vi.stubGlobal('window', new EventTarget())
    const seen: number[] = []
    const stopEditor = onRugChanged('r1', 'purchase-lines-editor', (d) => seen.push(d.version))
    const stopSelf = onRugChanged('r1', 'rug-final-size', () => seen.push(-1))
    announceRugChanged({ rugId: 'r1', version: 42, source: 'rug-final-size' })
    announceRugChanged({ rugId: 'r2', version: 99, source: 'rug-final-size' })
    stopEditor()
    stopSelf()
    expect(seen).toEqual([42])
  })
})
