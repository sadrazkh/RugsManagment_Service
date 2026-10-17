import { beforeEach, describe, expect, it } from 'vitest'
import { resetAppContext } from '@/lib/context'
import {
  AllocationMethod,
  AreaBasis,
  CostBasis,
  CostGroup,
  CostScope,
  CurrencyUnit,
  type CostCategory,
} from '@/lib/types/costs'
import {
  amountLabel,
  availableMethods,
  batchChipLabel,
  buildRequest,
  categoryDefaults,
  costDraftKey,
  costSentence,
  costSummary,
  emptyForm,
  formFromRequest,
  formHasContent,
  freezes,
  methodPhrase,
  missingPart,
  parseCostDraft,
  pinsAllowed,
  radioKeyTarget,
  rugsChipLabel,
  scopeOf,
  shipmentChipLabel,
  stateChipText,
  targetKey,
  visibleCategories,
  weightText,
  type CostForm,
} from './costText'

function category(over: Partial<CostCategory> = {}): CostCategory {
  return {
    id: 'c1',
    name: 'شستشو',
    group: CostGroup.Expense,
    icon: 'droplet',
    sortOrder: 10,
    isActive: true,
    defaultBasis: CostBasis.PerM2,
    defaultRate: null,
    defaultCurrency: null,
    defaultMethod: AllocationMethod.ByArea,
    defaultAreaBasis: AreaBasis.Intake,
    defaultScope: null,
    showInIntake: false,
    stepTypeId: null,
    seedKey: 'expense.washing',
    version: 1,
    ...over,
  }
}

const preview = { pieces: 52, lines: 3, areaM2: 20.81, baseAmount: 1_768_850, originalAmount: 1_768_850 }

beforeEach(() => resetAppContext({}))

describe('برچسب‌ها و جملهٔ پنل (H.2)', () => {
  it('برچسب مبلغ با بخش انتخاب‌شده عوض می‌شود', () => {
    expect(amountLabel(CostBasis.PerM2)).toBe('مبلغ هر متر')
    expect(amountLabel(CostBasis.PerPiece)).toBe('مبلغ هر تخته')
    expect(amountLabel(CostBasis.Total)).toBe('مبلغ کل')
  })

  it('جملهٔ «کل» همان H.2 است', () => {
    const text = costSentence({
      basis: CostBasis.Total,
      method: AllocationMethod.ByArea,
      areaBasis: AreaBasis.Effective,
      category: 'کرایه',
      typed: 10_000_000,
      currency: CurrencyUnit.Toman,
      preview: { ...preview, pieces: 242 },
      hasTarget: true,
    })
    expect(text).toBe('۱۰٬۰۰۰٬۰۰۰ تومان کرایه بین ۲۴۲ تخته به نسبت متراژ تقسیم می‌شود')
  })

  it('جملهٔ «هر متر» با متراژ و جمع پیش‌نمایش (D.7 #4)', () => {
    const text = costSentence({
      basis: CostBasis.PerM2,
      method: AllocationMethod.ByArea,
      areaBasis: AreaBasis.Intake,
      category: 'شستشو',
      typed: 85_000,
      currency: CurrencyUnit.Toman,
      preview,
      hasTarget: true,
    })
    expect(text).toBe('۸۵٬۰۰۰ تومان برای هر متر × ۲۰٫۸۱ متر = ۱٬۷۶۸٬۸۵۰ تومان')
  })

  it('جملهٔ «هر تخته»', () => {
    const text = costSentence({
      basis: CostBasis.PerPiece,
      method: AllocationMethod.ByCount,
      areaBasis: AreaBasis.Effective,
      category: 'بسته‌بندی',
      typed: 50_000,
      currency: CurrencyUnit.Toman,
      preview: { ...preview, originalAmount: 2_600_000 },
      hasTarget: true,
    })
    expect(text).toBe('۵۰٬۰۰۰ تومان برای هر تخته × ۵۲ تخته = ۲٬۶۰۰٬۰۰۰ تومان')
  })

  it('خلاصهٔ یک‌خطی: «کل ۱٬۷۶۸٬۸۵۰ · به نسبت متراژ (اندازهٔ اول)»', () => {
    expect(costSummary(CostBasis.Total, 1_768_850, AllocationMethod.ByArea, AreaBasis.Intake)).toBe(
      'کل ۱٬۷۶۸٬۸۵۰ · به نسبت متراژ (اندازهٔ اول)',
    )
    expect(costSummary(CostBasis.PerPiece, 50_000, AllocationMethod.Manual, AreaBasis.Effective)).toBe(
      'هر تخته ۵۰٬۰۰۰ · به تعداد تخته',
    )
  })

  it('بی «برای» یا بی مبلغ، جمله می‌گوید چه کم است', () => {
    const base = {
      basis: CostBasis.Total,
      method: AllocationMethod.ByArea,
      areaBasis: AreaBasis.Effective,
      category: 'کرایه',
      currency: CurrencyUnit.Toman,
      preview: null,
    }
    expect(costSentence({ ...base, typed: 1, hasTarget: false })).toContain('برای کدام فرش‌هاست')
    expect(costSentence({ ...base, typed: null, hasTarget: true })).toContain('مبلغ را بنویسید')
  })

  it('پایهٔ متراژ فقط برای «اندازهٔ اول» در جمله می‌آید', () => {
    expect(methodPhrase(AllocationMethod.ByArea, AreaBasis.Intake)).toBe('به نسبت متراژ (اندازهٔ اول)')
    expect(methodPhrase(AllocationMethod.ByArea, AreaBasis.Effective)).toBe('به نسبت متراژ')
    expect(methodPhrase(AllocationMethod.ByDeclaredValue, AreaBasis.Intake)).toBe('به نسبت ارزش گمرکی')
  })

  it('در جمله و خلاصه هیچ حرف لاتینی نیست', () => {
    const text = costSummary(CostBasis.PerM2, 85_000, AllocationMethod.ByArea, AreaBasis.Intake)
    expect(text).not.toMatch(/[A-Za-z]/)
  })
})

describe('روش‌ها', () => {
  it('«مساوی برای هر ردیف» فقط با دسته؛ «به نسبت قیمت خرید» نه برای خرید', () => {
    expect(availableMethods({ group: CostGroup.Expense, hasLot: false })).not.toContain(AllocationMethod.Equal)
    expect(availableMethods({ group: CostGroup.Expense, hasLot: true })).toContain(AllocationMethod.Equal)
    expect(availableMethods({ group: CostGroup.Purchase, hasLot: true })).not.toContain(AllocationMethod.ByPurchaseCost)
    expect(availableMethods({ group: CostGroup.Expense, hasLot: false })[0]).toBe(AllocationMethod.ByArea)
  })
})

describe('تراشه‌های «برای» (همه تخته)', () => {
  it('برچسب‌ها', () => {
    expect(shipmentChipLabel(242)).toBe('این محموله (۲۴۲ تخته)')
    expect(shipmentChipLabel(null)).toBe('این محموله')
    expect(rugsChipLabel(3, 52)).toBe('این فرش‌ها (۵۲ تخته)')
    expect(rugsChipLabel(1, 1)).toBe('این فرش')
    expect(rugsChipLabel(3, null)).toBe('این فرش‌ها (۳ ردیف)')
    expect(batchChipLabel('حاج رضا', 8, true)).toBe('ورودی امروز · حاج رضا (۸ تخته)')
    expect(batchChipLabel(null, null, false)).toBe('این ورودی')
  })

  it('دامنه و کلید هر انتخاب', () => {
    expect(scopeOf({ kind: 'shipment', shipmentId: 's', label: '' })).toBe(CostScope.Shipment)
    expect(scopeOf({ kind: 'bales', shipmentId: 's', baleIds: ['b'], label: '' })).toBe(CostScope.Bale)
    expect(scopeOf({ kind: 'batch', batchId: 'x', label: '' })).toBe(CostScope.IntakeBatch)
    expect(scopeOf({ kind: 'rugs', rugIds: ['r'], label: '', scopeNote: null })).toBe(CostScope.Selection)
    expect(targetKey({ kind: 'rugs', rugIds: ['b', 'a'], label: '', scopeNote: null })).toBe('rugs:a,b')
    expect(targetKey(null)).toBe('')
  })
})

describe('پیش‌فرض‌های نوع هزینه', () => {
  it('درصد «کل» می‌شود و خرید به نسبت قیمت خرید نمی‌شود', () => {
    const d = categoryDefaults(
      category({ group: CostGroup.Purchase, defaultBasis: CostBasis.Percent, defaultMethod: AllocationMethod.ByPurchaseCost }),
      CurrencyUnit.Toman,
    )
    expect(d.basis).toBe(CostBasis.Total)
    expect(d.method).toBe(AllocationMethod.ByArea)
  })

  it('شستشو: هر متر، اندازهٔ اول، ارز کارگاه', () => {
    const d = categoryDefaults(category({ defaultRate: 85_000 }), CurrencyUnit.Rial)
    expect(d).toMatchObject({ basis: CostBasis.PerM2, areaBasis: AreaBasis.Intake, currency: CurrencyUnit.Rial, typed: 85_000 })
  })

  it('کاشی‌ها: نمای ساده فقط هزینه، و «خرید یکجا» فقط از ورودی', () => {
    const list = [
      category({ id: 'w' }),
      category({ id: 'p', group: CostGroup.Purchase, seedKey: 'purchase.price', showInIntake: true }),
      category({ id: 'l', group: CostGroup.Purchase, seedKey: 'purchase.lump' }),
      category({ id: 'off', isActive: false }),
    ]
    expect(visibleCategories(list, { simple: true, fromBatch: false }).map((c) => c.id)).toEqual(['w'])
    expect(visibleCategories(list, { simple: true, fromBatch: true }).map((c) => c.id)).toEqual(['l', 'w'])
    expect(visibleCategories(list, { simple: false, fromBatch: false }).map((c) => c.id)).toEqual(['l', 'w'])
    // نوع غیرفعالی که ثبت در حال ویرایش دارد می‌ماند
    expect(visibleCategories(list, { simple: true, fromBatch: false, selectedId: 'off' }).map((c) => c.id)).toContain('off')
  })
})

describe('رسید ثابت و مبلغ ثابت (D.3، E9)', () => {
  it('هر متر با شمارهٔ رسید یا کارگاه ثابت می‌شود؛ مبلغ ثابت فقط «کل» یا رسید ثابت', () => {
    expect(freezes(CostBasis.PerM2, '', null)).toBe(false)
    expect(freezes(CostBasis.PerM2, ' ۱۲ ', null)).toBe(true)
    expect(freezes(CostBasis.PerPiece, '', 'prov')).toBe(true)
    expect(freezes(CostBasis.Total, 'x', 'p')).toBe(false)
    expect(pinsAllowed(CostBasis.Total, '', null)).toBe(true)
    expect(pinsAllowed(CostBasis.PerM2, '', null)).toBe(false)
    expect(pinsAllowed(CostBasis.PerM2, 'r1', null)).toBe(true)
  })
})

describe('بدنهٔ درخواست', () => {
  const rugs = { kind: 'rugs' as const, rugIds: ['a', 'b', 'c'], label: 'این فرش‌ها', scopeNote: null }

  it('شستشوی «کل» D.7: مبلغ در amount، روش و پایه همان، دامنهٔ انتخاب', () => {
    const form: CostForm = {
      ...emptyForm('2026-09-06', CurrencyUnit.Toman),
      categoryId: 'c1',
      basis: CostBasis.Total,
      typed: 1_768_850,
      method: AllocationMethod.ByArea,
      areaBasis: AreaBasis.Intake,
      target: rugs,
    }
    const body = buildRequest(form, { version: null, clientKey: 'k1' })
    expect(body).toMatchObject({
      categoryId: 'c1',
      date: '2026-09-06',
      basis: CostBasis.Total,
      amount: 1_768_850,
      rate: null,
      areaBasis: AreaBasis.Intake,
      method: AllocationMethod.ByArea,
      scope: CostScope.Selection,
      rugIds: ['a', 'b', 'c'],
      shipmentId: null,
      pins: null,
      clientKey: 'k1',
      description: null,
      receiptNo: null,
    })
  })

  it('هر متر: نرخ در rate و روش متراژ؛ مبلغ ثابت روی نرخ زنده فرستاده نمی‌شود', () => {
    const form: CostForm = {
      ...emptyForm('2026-09-06', CurrencyUnit.Toman),
      categoryId: 'c1',
      basis: CostBasis.PerM2,
      typed: 85_000,
      method: AllocationMethod.Manual,
      target: rugs,
      pins: [{ rugId: 'a', amount: 100 }],
    }
    const body = buildRequest(form, { version: 7, clientKey: null })
    expect(body.rate).toBe(85_000)
    expect(body.amount).toBeNull()
    expect(body.method).toBe(AllocationMethod.ByArea)
    expect(body.pins).toBeNull()
    expect(body.version).toBe(7)
  })

  it('دلار: نرخ ارز می‌خواهد؛ محموله و نگله پیوند درست', () => {
    const form: CostForm = {
      ...emptyForm('2026-09-06', CurrencyUnit.Toman),
      categoryId: 'c1',
      typed: 100,
      currency: CurrencyUnit.Usd,
      target: { kind: 'bales', shipmentId: 's1', baleIds: ['b1'], label: 'این نگله' },
    }
    expect(missingPart(form)).toBe('E_FX_REQUIRED')
    const body = buildRequest({ ...form, fxRate: 105_000 }, { version: null, clientKey: 'k' })
    expect(body).toMatchObject({ scope: CostScope.Bale, shipmentId: 's1', baleIds: ['b1'], fxRate: 105_000 })
    expect(missingPart({ ...form, fxRate: 105_000 })).toBeNull()
  })

  it('چه چیزی کم است، به ترتیب پنل', () => {
    const form = emptyForm('2026-09-06', CurrencyUnit.Toman)
    expect(missingPart(form)).toBe('E_COST_CATEGORY_REQUIRED')
    expect(missingPart({ ...form, categoryId: 'c' })).toBe('E_COST_TARGET_REQUIRED')
    expect(missingPart({ ...form, categoryId: 'c', target: rugs })).toBe('E_COST_AMOUNT_REQUIRED')
  })

  it('فرم از ثبت موجود و برگشت همان بدنه', () => {
    const form: CostForm = {
      ...emptyForm('2026-09-06', CurrencyUnit.Toman),
      categoryId: 'c1',
      typed: 10_000_000,
      target: { kind: 'shipment', shipmentId: 's8', label: 'محمولهٔ ۸' },
      receiptNo: 'R-1',
      pins: [{ rugId: 'a', amount: 5 }],
    }
    const body = buildRequest(form, { version: 3, clientKey: 'k' })
    const back = formFromRequest(body, 'محمولهٔ ۸')
    expect(buildRequest(back, { version: 3, clientKey: 'k' })).toEqual(body)
  })
})

describe('پیش‌نویس پنل (G.7)', () => {
  it('کلید بی کاربر ساخته نمی‌شود', () => {
    expect(costDraftKey(null, 'u', 'new')).toBeNull()
    expect(costDraftKey('t', 'u', 'new')).toBe('rugs:draft:t:u:cost-entry:new')
  })

  it('خواندن امن: خراب → null؛ سالم → همان کلید ارسال', () => {
    expect(parseCostDraft('{')).toBeNull()
    expect(parseCostDraft(JSON.stringify({ v: 2 }))).toBeNull()
    const form = { ...emptyForm('2026-09-06', CurrencyUnit.Toman), typed: 5 }
    const draft = parseCostDraft(JSON.stringify({ v: 1, savedAt: 1, clientKey: 'same', form }))
    expect(draft?.clientKey).toBe('same')
    expect(draft?.form.typed).toBe(5)
  })

  it('فرم خالی ارزش پیش‌نویس ندارد', () => {
    const form = emptyForm('2026-09-06', CurrencyUnit.Toman)
    expect(formHasContent(form)).toBe(false)
    expect(formHasContent({ ...form, typed: 0 })).toBe(true)
  })
})

describe('گروه رادیویی در RTL و وزن سهم', () => {
  it('← بعدی، → قبلی، دوری؛ Home و End', () => {
    expect(radioKeyTarget('ArrowLeft', 0, 3)).toBe(1)
    expect(radioKeyTarget('ArrowRight', 0, 3)).toBe(2)
    expect(radioKeyTarget('ArrowDown', 2, 3)).toBe(0)
    expect(radioKeyTarget('ArrowUp', 1, 3)).toBe(0)
    expect(radioKeyTarget('Home', 2, 3)).toBe(0)
    expect(radioKeyTarget('End', 0, 3)).toBe(2)
    expect(radioKeyTarget('Enter', 0, 3)).toBeNull()
    expect(radioKeyTarget('ArrowLeft', 0, 0)).toBeNull()
  })

  it('وزن به واحد بازرگان', () => {
    expect(weightText(437, 'm2x100')).toBe('۴٫۳۷ متر')
    expect(weightText(50, 'pieces')).toBe('۵۰ تخته')
    expect(weightText(19_665, 'usdCents')).toBe('۱۹۶٫۶۵ دلار')
    expect(weightText(0, 'amount')).toBe('—')
  })
})

describe('تراشهٔ وضعیت', () => {
  it('«تقسیم‌نشده» و مانده با واحد کارگاه', () => {
    expect(stateChipText(2, 0)).toBe('تقسیم‌نشده')
    expect(stateChipText(1, 200_000)).toBe('۲۰۰٬۰۰۰ تومان هنوز تقسیم نشده.')
    resetAppContext({ currency: { code: 'IRR', label: 'ریال', scale: 0 } })
    expect(stateChipText(1, 10)).toBe('۱۰ ریال هنوز تقسیم نشده.')
  })
})
