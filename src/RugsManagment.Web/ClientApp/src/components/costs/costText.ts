/**
 * منطق خالص پنل ثبت هزینه و جزیره‌های هزینه (H.2، D.2، D.3): برچسب مبنا و روش، جملهٔ فقط‌خواندنی پنل، پیش‌فرض‌های
 * نوع هزینه، ساختن بدنهٔ `SaveCostEntryRequest` و پیش‌نویس پنل. همه بی DOM و بی شبکه تا در vitest آزموده شوند.
 *
 * چرا جمله در کلاینت: بازرگان باید همان لحظهٔ تایپ ببیند مبلغش چطور تقسیم می‌شود. عددهای جمله (تخته، متراژ و مبلغ کل)
 * از پیش‌نمایش سرور می‌آیند (`POST /api/costs/preview`) که با همان قاعدهٔ «هر تخته، بعد × تعداد» حساب می‌کند؛ این‌جا هیچ
 * ضرب و گرد کردنی نیست، فقط چیدن متن.
 */
import { appContext } from '@/lib/context'
import { formatMessageNumber, message } from '@/lib/messages'
import {
  AllocationMethod,
  AreaBasis,
  CostBasis,
  CostGroup,
  CostScope,
  CurrencyUnit,
  type AllocationPreview,
  type CostCategory,
  type Pin,
  type SaveCostEntryRequest,
} from '@/lib/types/costs'

// ── برچسب‌ها ───────────────────────────────────────────────────────────

/** سه بخش کلید «هر متر | هر تخته | کل» (H.2)، به همین ترتیب در RTL. */
export const BASIS_SEGMENTS: readonly { basis: CostBasis; label: string }[] = [
  { basis: CostBasis.PerM2, label: 'هر متر' },
  { basis: CostBasis.PerPiece, label: 'هر تخته' },
  { basis: CostBasis.Total, label: 'کل' },
]

/** برچسب خانهٔ مبلغ که با بخش انتخاب‌شده عوض می‌شود. */
export function amountLabel(basis: CostBasis): string {
  switch (basis) {
    case CostBasis.PerM2:
      return 'مبلغ هر متر'
    case CostBasis.PerPiece:
      return 'مبلغ هر تخته'
    default:
      return 'مبلغ کل'
  }
}

/** نام کوتاه مبنا («کل»، «هر متر»، «هر تخته»). */
export function basisLabel(basis: CostBasis): string {
  return BASIS_SEGMENTS.find((s) => s.basis === basis)?.label ?? 'کل'
}

/** واژه‌های روش تقسیم برای جملهٔ پنل و انتخابگر روش (H.2 «[تغییر]»). */
export const METHOD_LABELS: Readonly<Record<number, string>> = {
  [AllocationMethod.ByArea]: 'به نسبت متراژ',
  [AllocationMethod.ByCount]: 'به تعداد تخته',
  [AllocationMethod.Equal]: 'مساوی برای هر ردیف',
  [AllocationMethod.ByDeclaredValue]: 'به نسبت ارزش گمرکی',
  [AllocationMethod.ByPurchaseCost]: 'به نسبت قیمت خرید',
  [AllocationMethod.Manual]: 'خودم سهم هر فرش را می‌نویسم',
}

/** ترتیب گزینه‌های انتخابگر روش (H.2). «مساوی» فقط وقتی دسته‌ای در فرش‌ها هست. */
export const METHOD_ORDER: readonly AllocationMethod[] = [
  AllocationMethod.ByArea,
  AllocationMethod.ByCount,
  AllocationMethod.Equal,
  AllocationMethod.ByDeclaredValue,
  AllocationMethod.ByPurchaseCost,
  AllocationMethod.Manual,
]

/** برچسب پایهٔ متراژ (فقط نمای کامل؛ در نمای ساده هرگز کنترلش دیده نمی‌شود). */
export const AREA_BASIS_LABELS: Readonly<Record<number, string>> = {
  [AreaBasis.Intake]: 'اندازهٔ اول',
  [AreaBasis.Effective]: 'اندازهٔ نهایی',
}

/**
 * عبارت روش در جمله: «به نسبت متراژ (اندازهٔ اول)». پایهٔ متراژ فقط برای روشِ متراژی و فقط وقتی «اندازهٔ اول» است گفته
 * می‌شود (شستشو و رفو)؛ اندازهٔ نهایی همان اندازهٔ معمول فرش است و گفتنش جمله را شلوغ می‌کند.
 */
export function methodPhrase(method: AllocationMethod, areaBasis: AreaBasis): string {
  const label = METHOD_LABELS[method] ?? METHOD_LABELS[AllocationMethod.ByArea]
  if (method === AllocationMethod.ByArea && areaBasis === AreaBasis.Intake) return `${label} (${AREA_BASIS_LABELS[AreaBasis.Intake]})`
  return label
}

/** فهرست روش‌هایی که برای این ثبت معنا دارند. */
export function availableMethods(options: { group: CostGroup | null; hasLot: boolean }): AllocationMethod[] {
  return METHOD_ORDER.filter((m) => {
    // «مساوی برای هر ردیف» فقط وقتی با «به تعداد تخته» فرق دارد: یعنی دسته‌ای در فرش‌ها هست (H.2)
    if (m === AllocationMethod.Equal) return options.hasLot
    // خرید به نسبت قیمت خرید دور باطل است (E_CIRCULAR)
    if (m === AllocationMethod.ByPurchaseCost) return options.group !== CostGroup.Purchase
    return true
  })
}

/** واحد پول نمایشی یک ثبت («تومان»، «ریال» یا «دلار»). */
export function currencyWord(currency: CurrencyUnit): string {
  if (currency === CurrencyUnit.Usd) return 'دلار'
  if (currency === CurrencyUnit.Rial) return 'ریال'
  return 'تومان'
}

/** واحد پول پایهٔ کارگاه (از app-context). */
export function baseCurrencyWord(): string {
  return appContext().currency.label
}

/** ارز پایهٔ کارگاه به‌صورت enum. */
export function baseCurrency(): CurrencyUnit {
  return appContext().currency.code === 'IRR' ? CurrencyUnit.Rial : CurrencyUnit.Toman
}

/** عدد برای متن («۱۰٬۰۰۰٬۰۰۰»، «۲۰٫۸۱»). */
export function n(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : formatMessageNumber(value)
}

// ── جملهٔ پنل ──────────────────────────────────────────────────────────

export interface SentenceInput {
  basis: CostBasis
  method: AllocationMethod
  areaBasis: AreaBasis
  /** نام نوع هزینه («کرایه»، «شستشو») */
  category: string | null
  /** عدد تایپ‌شده: نرخ برای هر متر/هر تخته، مبلغ کل برای «کل» */
  typed: number | null
  currency: CurrencyUnit
  /** آخرین پیش‌نمایش سرور برای همین فرم، یا null */
  preview: Pick<AllocationPreview, 'pieces' | 'areaM2' | 'baseAmount' | 'originalAmount' | 'lines'> | null
  /** «برای» انتخاب شده؟ */
  hasTarget: boolean
}

/**
 * جملهٔ فقط‌خواندنی پنل (H.2):
 * - کل: «۱۰٬۰۰۰٬۰۰۰ تومان کرایه بین ۲۴۲ تخته به نسبت متراژ تقسیم می‌شود»
 * - هر متر: «۸۵٬۰۰۰ تومان برای هر متر × ۲۰٫۸۱ متر = ۱٬۷۶۸٬۸۵۰ تومان»
 * - هر تخته: «۵۰٬۰۰۰ تومان برای هر تخته × ۵۲ تخته = ۲٬۶۰۰٬۰۰۰ تومان»
 */
export function costSentence(input: SentenceInput): string {
  if (!input.hasTarget) return message('I_COST_SENTENCE_NO_TARGET')
  if (input.typed === null || input.typed === undefined) return message('I_COST_SENTENCE_NO_AMOUNT')

  const currency = currencyWord(input.currency)
  const p = input.preview
  const category = input.category?.trim() || 'هزینه'

  if (input.basis === CostBasis.PerM2) {
    return message('I_COST_SENTENCE_PER_M2', {
      rate: n(input.typed),
      area: p ? n(p.areaM2) : '…',
      amount: p ? n(p.originalAmount) : '…',
      currency,
    })
  }
  if (input.basis === CostBasis.PerPiece) {
    return message('I_COST_SENTENCE_PER_PIECE', {
      rate: n(input.typed),
      pieces: p ? n(p.pieces) : '…',
      amount: p ? n(p.originalAmount) : '…',
      currency,
    })
  }
  if (input.method === AllocationMethod.Manual) {
    return message('I_COST_SENTENCE_MANUAL', { amount: n(input.typed), currency, category })
  }
  return message('I_COST_SENTENCE_TOTAL', {
    amount: n(input.typed),
    currency,
    category,
    pieces: p ? n(p.pieces) : '…',
    method: methodPhrase(input.method, input.areaBasis),
  })
}

/**
 * خلاصهٔ یک‌خطی ثبت: «کل ۱٬۷۶۸٬۸۵۰ · به نسبت متراژ (اندازهٔ اول)». برای مبنای هر متر/هر تخته روش تقسیم همان مبناست
 * (D.3)، پس «به نسبت متراژ» یا «به تعداد تخته» می‌آید.
 */
export function costSummary(basis: CostBasis, typed: number | null, method: AllocationMethod, areaBasis: AreaBasis): string {
  const effectiveMethod =
    basis === CostBasis.PerM2 ? AllocationMethod.ByArea : basis === CostBasis.PerPiece ? AllocationMethod.ByCount : method
  return message('I_COST_SUMMARY', {
    basis: basisLabel(basis),
    amount: n(typed),
    method: methodPhrase(effectiveMethod, areaBasis),
  })
}

// ── «برای» ─────────────────────────────────────────────────────────────

/** یک انتخاب «برای». */
export type CostTarget =
  | { kind: 'shipment'; shipmentId: string; label: string }
  | { kind: 'bales'; shipmentId: string | null; baleIds: string[]; label: string }
  | { kind: 'rugs'; rugIds: string[]; label: string; scopeNote: string | null }
  | { kind: 'batch'; batchId: string; label: string }

/** کلید پایدار یک انتخاب (برای تراشهٔ انتخاب‌شده). */
export function targetKey(target: CostTarget | null): string {
  if (!target) return ''
  switch (target.kind) {
    case 'shipment':
      return `shipment:${target.shipmentId}`
    case 'bales':
      return `bales:${[...target.baleIds].sort().join(',')}`
    case 'batch':
      return `batch:${target.batchId}`
    case 'rugs':
      return `rugs:${[...target.rugIds].sort().join(',')}`
  }
}

/** دامنهٔ ثبت برای یک انتخاب «برای» (D.2). */
export function scopeOf(target: CostTarget): CostScope {
  switch (target.kind) {
    case 'shipment':
      return CostScope.Shipment
    case 'bales':
      return CostScope.Bale
    case 'batch':
      return CostScope.IntakeBatch
    default:
      return CostScope.Selection
  }
}

/** «۲۴۲ تخته» یا «۵۲ تخته (۳ ردیف)» (A.2: هر شمارش تخته است؛ با دسته ردیف‌ها در پرانتز). */
export function piecesText(pieces: number, lines?: number | null): string {
  const base = `${n(pieces)} تخته`
  return lines !== null && lines !== undefined && lines !== pieces ? `${base} (${n(lines)} ردیف)` : base
}

/** «محمولهٔ ۸» — نام یک محموله در تراشه‌ها و انتخابگر. */
export function shipmentName(number: number): string {
  return `محمولهٔ ${n(number)}`
}

/** متن‌های انتخابگر «فرش‌های دیگر…» که به محموله اشاره می‌کنند. */
export const PICKER_TEXT = {
  orShipment: 'یا یک محموله',
} as const

/** «معمولاً برای» در تنظیمات نوع هزینه (دامنهٔ پیش‌فرض، D.2)؛ «نگله» از واژهٔ کارگاه. */
export function scopeLabel(scope: CostScope): string {
  switch (scope) {
    case CostScope.Bale:
      return `یک ${appContext().terms.bale}`
    case CostScope.Shipment:
      return 'یک محموله'
    case CostScope.IntakeBatch:
      return 'یک ورودی'
    default:
      return 'فرش‌هایی که انتخاب می‌شوند'
  }
}

/** برچسب تراشهٔ «این محموله (۲۴۲ تخته)». */
export function shipmentChipLabel(pieces: number | null): string {
  return pieces === null ? 'این محموله' : `این محموله (${piecesText(pieces)})`
}

/** برچسب تراشهٔ «این نگله» / «این ۳ نگله». */
export function balesChipLabel(count: number, bale: string): string {
  return count <= 1 ? `این ${bale}` : `این ${n(count)} ${bale}`
}

/** برچسب تراشهٔ «این فرش‌ها (۵۲ تخته)»؛ تعداد تخته وقتی هنوز معلوم نیست، شمار ردیف. */
export function rugsChipLabel(rows: number, pieces: number | null): string {
  if (rows === 1 && (pieces === null || pieces === 1)) return 'این فرش'
  if (pieces === null) return `این فرش‌ها (${n(rows)} ردیف)`
  return `این فرش‌ها (${piecesText(pieces)})`
}

/** برچسب تراشهٔ یک ورودی امروز (E7: یکی برای هر فروشنده): «ورودی امروز · حاج رضا (۸ تخته)». */
export function batchChipLabel(seller: string | null, pieces: number | null, today: boolean): string {
  const head = today ? 'ورودی امروز' : 'این ورودی'
  const who = seller?.trim() ? ` · ${seller.trim()}` : ''
  const count = pieces === null ? '' : ` (${n(pieces)} تخته)`
  return `${head}${who}${count}`
}

// ── پیش‌فرض‌های نوع هزینه ───────────────────────────────────────────────

/** مقدارهایی که انتخاب یک نوع هزینه در فرم می‌گذارد. */
export interface CategoryDefaults {
  basis: CostBasis
  method: AllocationMethod
  areaBasis: AreaBasis
  currency: CurrencyUnit
  typed: number | null
  scope: CostScope | null
}

/**
 * پیش‌فرض‌های یک نوع (H.2: «نوع، مبنای پیش‌فرض را می‌دهد»). درصد فقط برای سطر خرید است، پس «کل» می‌شود؛ روش «به نسبت
 * قیمت خرید» برای نوع خرید دور باطل است و «متراژ» می‌شود.
 */
export function categoryDefaults(category: CostCategory, tenantCurrency: CurrencyUnit): CategoryDefaults {
  const basis = category.defaultBasis === CostBasis.Percent ? CostBasis.Total : category.defaultBasis
  let method = category.defaultMethod
  if (category.group === CostGroup.Purchase && method === AllocationMethod.ByPurchaseCost) method = AllocationMethod.ByArea
  const currency = category.defaultCurrency ?? tenantCurrency
  return {
    basis,
    method,
    areaBasis: category.defaultAreaBasis,
    currency: currency === CurrencyUnit.Usd ? CurrencyUnit.Usd : currency,
    typed: category.defaultRate ?? null,
    scope: category.defaultScope,
  }
}

/** نوع‌هایی که کاشی‌شان در پنل دیده می‌شود (H.2 «Purchase in one place»). */
export function visibleCategories(
  categories: readonly CostCategory[],
  options: { simple: boolean; fromBatch: boolean; selectedId?: string | null },
): CostCategory[] {
  return categories
    .filter((c) => {
      if (c.id === options.selectedId) return true
      if (!c.isActive) return false
      if (c.group === CostGroup.Expense) return true
      // نوع‌های خرید: در نمای کامل همه (جز سطرهای خودِ جدول ورود)؛ در نمای ساده فقط «خرید یکجا» و فقط از یک ورودی
      if (options.simple) return options.fromBatch && c.seedKey === 'purchase.lump'
      return !c.showInIntake
    })
    .sort((a, b) => a.group - b.group || a.sortOrder - b.sortOrder || a.name.localeCompare(b.name, 'fa'))
}

/** رسید ثابت می‌شود؟ هر متر/هر تخته با شمارهٔ رسید یا کارگاه (D.3 «Receipts freeze»). */
export function freezes(basis: CostBasis, receiptNo: string | null | undefined, providerId: string | null | undefined): boolean {
  return basis !== CostBasis.Total && (!!receiptNo?.trim() || !!providerId)
}

/** مبلغ ثابت مجاز است؟ فقط «کل» یا رسید ثابت‌شده (D.3، E9). */
export function pinsAllowed(basis: CostBasis, receiptNo: string | null | undefined, providerId: string | null | undefined): boolean {
  return basis === CostBasis.Total || freezes(basis, receiptNo, providerId)
}

// ── فرم و بدنهٔ درخواست ────────────────────────────────────────────────

/** وضعیت فرم پنل (همان که پیش‌نویس می‌شود). */
export interface CostForm {
  categoryId: string | null
  basis: CostBasis
  /** عدد تایپ‌شده: نرخ برای هر متر/هر تخته، مبلغ برای «کل» */
  typed: number | null
  currency: CurrencyUnit
  fxRate: number | null
  method: AllocationMethod
  areaBasis: AreaBasis
  target: CostTarget | null
  /** `yyyy-MM-dd` */
  date: string
  receiptNo: string
  providerId: string | null
  description: string
  pins: Pin[]
}

/** فرم خالی برای امروز. */
export function emptyForm(today: string, currency: CurrencyUnit): CostForm {
  return {
    categoryId: null,
    basis: CostBasis.Total,
    typed: null,
    currency,
    fxRate: null,
    method: AllocationMethod.ByArea,
    areaBasis: AreaBasis.Effective,
    target: null,
    date: today,
    receiptNo: '',
    providerId: null,
    description: '',
    pins: [],
  }
}

/** چه چیزی هنوز جلوی پیش‌نمایش یا ذخیره را گرفته (کد پیام)، یا null. */
export function missingPart(form: CostForm): 'E_COST_CATEGORY_REQUIRED' | 'E_COST_TARGET_REQUIRED' | 'E_COST_AMOUNT_REQUIRED' | 'E_FX_REQUIRED' | null {
  if (!form.categoryId) return 'E_COST_CATEGORY_REQUIRED'
  if (!form.target) return 'E_COST_TARGET_REQUIRED'
  if (form.typed === null || form.typed === undefined) return 'E_COST_AMOUNT_REQUIRED'
  if (form.currency === CurrencyUnit.Usd && !form.fxRate) return 'E_FX_REQUIRED'
  return null
}

/**
 * بدنهٔ `SaveCostEntryRequest` از فرم. نرخ برای هر متر/هر تخته در `rate`، مبلغ کل در `amount`؛ پیوندهای «برای» با
 * دامنه؛ مبلغ‌های ثابت فقط جایی که مجازند و نه صفر؛ متن‌های خالی null.
 */
export function buildRequest(
  form: CostForm,
  options: { version: number | null; clientKey: string | null },
): SaveCostEntryRequest {
  const target = form.target
  const perUnit = form.basis === CostBasis.PerM2 || form.basis === CostBasis.PerPiece
  const pins = pinsAllowed(form.basis, form.receiptNo, form.providerId) ? form.pins.filter((p) => p.amount !== 0) : []
  return {
    categoryId: form.categoryId ?? '',
    date: form.date,
    description: form.description.trim() || null,
    receiptNo: form.receiptNo.trim() || null,
    serviceProviderId: form.providerId,
    basis: form.basis,
    rate: perUnit ? form.typed : null,
    amount: perUnit ? null : form.typed,
    currency: form.currency,
    fxRate: form.currency === CurrencyUnit.Usd ? form.fxRate : null,
    areaBasis: form.areaBasis,
    method: form.basis === CostBasis.PerM2 ? AllocationMethod.ByArea : form.basis === CostBasis.PerPiece ? AllocationMethod.ByCount : form.method,
    scope: target ? scopeOf(target) : CostScope.Selection,
    shipmentId: target?.kind === 'shipment' ? target.shipmentId : target?.kind === 'bales' ? target.shipmentId : null,
    batchId: target?.kind === 'batch' ? target.batchId : null,
    rugIds: target?.kind === 'rugs' ? [...target.rugIds] : null,
    baleIds: target?.kind === 'bales' ? [...target.baleIds] : null,
    scopeNote: target?.kind === 'rugs' ? target.scopeNote : null,
    pins: pins.length > 0 ? pins.map((p) => ({ rugId: p.rugId, amount: p.amount })) : null,
    version: options.version,
    clientKey: options.clientKey,
  }
}

/** فرم از یک ثبت موجود (حالت ویرایش). برچسب «برای» را جزیره بعداً دقیق‌تر می‌کند. */
export function formFromRequest(request: SaveCostEntryRequest, targetsText: string | null): CostForm {
  const perUnit = request.basis === CostBasis.PerM2 || request.basis === CostBasis.PerPiece
  let target: CostTarget | null = null
  const label = targetsText ?? ''
  switch (request.scope) {
    case CostScope.Shipment:
      if (request.shipmentId) target = { kind: 'shipment', shipmentId: request.shipmentId, label: label || 'این محموله' }
      break
    case CostScope.Bale:
      if (request.baleIds?.length) target = { kind: 'bales', shipmentId: request.shipmentId, baleIds: request.baleIds, label: label || 'این نگله‌ها' }
      break
    case CostScope.IntakeBatch:
      if (request.batchId) target = { kind: 'batch', batchId: request.batchId, label: label || 'این ورودی' }
      else if (request.rugIds?.length) target = { kind: 'rugs', rugIds: request.rugIds, label: label || 'این فرش‌ها', scopeNote: request.scopeNote }
      break
    default:
      if (request.rugIds?.length) target = { kind: 'rugs', rugIds: request.rugIds, label: label || 'این فرش‌ها', scopeNote: request.scopeNote }
  }
  return {
    categoryId: request.categoryId,
    basis: request.basis,
    typed: perUnit ? request.rate : request.amount,
    currency: request.currency,
    fxRate: request.fxRate,
    method: request.method,
    areaBasis: request.areaBasis,
    target,
    date: request.date,
    receiptNo: request.receiptNo ?? '',
    providerId: request.serviceProviderId,
    description: request.description ?? '',
    pins: request.pins ? request.pins.map((p) => ({ ...p })) : [],
  }
}

// ── پیش‌نویس پنل (G.7) ─────────────────────────────────────────────────

/** پیش‌نویس ذخیره‌نشدهٔ پنل: همان فرم و همان کلید ارسال، تا «دوباره بفرست» ثبت دوم نسازد. */
export interface CostDraft {
  v: 1
  savedAt: number
  clientKey: string
  form: CostForm
}

export const COST_DRAFT_PREFIX = 'rugs:draft'

/** کلید پیش‌نویس: `rugs:draft:{tenant}:{user}:cost-entry:{context}`؛ بی کاربر هیچ. */
export function costDraftKey(tenantId: string | null, userId: string | null, context: string): string | null {
  if (!tenantId || !userId) return null
  return `${COST_DRAFT_PREFIX}:${tenantId}:${userId}:cost-entry:${context}`
}

/** خواندن امن پیش‌نویس (JSON خراب یا شکل دیگر → null). */
export function parseCostDraft(raw: string | null): CostDraft | null {
  if (!raw) return null
  try {
    const value = JSON.parse(raw) as Partial<CostDraft>
    if (!value || value.v !== 1 || typeof value.clientKey !== 'string' || typeof value.form !== 'object' || value.form === null) return null
    const form = value.form as Partial<CostForm>
    if (typeof form.date !== 'string' || typeof form.basis !== 'number') return null
    return {
      v: 1,
      savedAt: typeof value.savedAt === 'number' ? value.savedAt : 0,
      clientKey: value.clientKey,
      form: { ...emptyForm(form.date, form.currency ?? CurrencyUnit.Toman), ...form, pins: Array.isArray(form.pins) ? form.pins : [] },
    }
  } catch {
    return null
  }
}

/** آیا فرم چیزی دارد که ارزش پیش‌نویس داشته باشد (فقط تاریخ امروز یعنی خالی). */
export function formHasContent(form: CostForm): boolean {
  return form.typed !== null || !!form.receiptNo.trim() || !!form.description.trim() || form.pins.length > 0
}

// ── گروه‌های رادیویی (H.7) ──────────────────────────────────────────────

/**
 * خانهٔ بعدی یک گروه رادیویی با کلید (H.2، H.7: کاشی‌ها، بخش‌های مبنا و تراشه‌های «برای» در RTL): ← بعدی و → قبلی (در
 * راست‌به‌چپ «جلو» سمت چپ است)، ↓ بعدی و ↑ قبلی، Home اولی و End آخری؛ دوری. کلید دیگر → null.
 */
export function radioKeyTarget(key: string, index: number, count: number): number | null {
  if (count <= 0) return null
  switch (key) {
    case 'ArrowLeft':
    case 'ArrowDown':
      return (index + 1) % count
    case 'ArrowRight':
    case 'ArrowUp':
      return (index - 1 + count) % count
    case 'Home':
      return 0
    case 'End':
      return count - 1
    default:
      return null
  }
}

/** وزن یک سهم به زبان بازرگان: «۴٫۳۷ متر»، «۵۰ تخته»، «۱۹۶٫۶۵ دلار»؛ «—» برای مبلغ مستقیم. */
export function weightText(units: number, unit: string): string {
  switch (unit) {
    case 'm2x100':
      return `${n(units / 100)} متر`
    case 'pieces':
      return `${n(units)} تخته`
    case 'usdCents':
      return `${n(units / 100)} دلار`
    case 'purchaseCents':
      return `${n(units / 100)} ${baseCurrencyWord()}`
    case 'equal':
      return n(units)
    default:
      return '—'
  }
}

// ── وضعیت تقسیم ────────────────────────────────────────────────────────

/**
 * برچسب کوتاه هر هشدار برای تراشهٔ ردیف فهرست (متن کامل با عدد در پنل همان ثبت دیده می‌شود). کد ناشناخته خودِ کد
 * نمی‌شود؛ فهرست برایش «هشدار» می‌نویسد.
 */
export const WARNING_SHORT: Readonly<Record<string, string>> = {
  W_STEP_DOUBLE: 'در کارهای کارگاه هم هست',
  W_PURCHASE_DOUBLE: 'خرید دوبار؟',
  W_PIN_DROPPED: 'مبلغ ثابت جا ماند',
  W_ZERO_WEIGHTS: 'مساوی تقسیم شد',
  W_NO_DECLARED_VALUE: 'بی ارزش گمرکی',
  W_EMPTY_TARGET: 'فرشی ندارد',
  W_TARGET_DELETED: 'جایش حذف شد',
  W_LATE_COST: 'پس از قطعی',
  W_RATE_DRIFT: 'مبلغ رسید فرق کرده',
  W_PINS_RESIDUAL: 'بخشی بی‌صاحب ماند',
  W_NEGATIVE_WEIGHT: 'قیمت خرید منفی',
}

/** متن تراشهٔ وضعیت یک ثبت: «تقسیم‌نشده»، «۲۰۰ تومان هنوز تقسیم نشده.» یا «تقسیم شد». */
export function stateChipText(state: number, unallocated: number): string {
  if (state === 2) return message('I_COST_UNALLOCATED')
  if (state === 1) return message('I_COST_PARTIAL', { amount: n(unallocated), currency: baseCurrencyWord() })
  return message('I_COST_ALLOCATED')
}
