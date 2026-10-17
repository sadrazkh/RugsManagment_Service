/**
 * نوع‌های دفتر هزینه و تقسیم از فاز ۵ — آینهٔ دستی DTOهای `Application/DTOs/Costs/*.cs` (spec I.3) و enumهای Domain.
 * کد تازه مستقیم از همین فایل import کند: `import type { CostEntry } from '@/lib/types/costs'`.
 *
 * قراردادها (مثل `rugs.ts` و `shipments.ts`):
 * - enumها در JSON عدد می‌آیند؛ هر enum یک شیء ثابت (برای مقدار) و یک نوع هم‌نام دارد. عددها همان C#اند و فقط
 *   افزودنی‌اند. `CostBasis`/`CostGroup` در `rugs.ts` و `CurrencyUnit`/`AllocationMethod`/`AreaBasis` در `platform.ts`
 *   از پیش هستند و این‌جا فقط دوباره export می‌شوند تا هر جزیرهٔ هزینه یک import داشته باشد.
 * - همهٔ مبلغ‌ها به ارز پایهٔ کارگاه‌اند (تومان یا ریال)، مگر جایی که نامش «Original» است (ارز خودِ ثبت). دلار گمرکی
 *   پول داخلی نیست. مبلغ‌ها را سرور با قاعدهٔ «هر تخته، بعد × تعداد» و «بیشترین باقی‌مانده» گرد کرده (D.3)؛ در فرانت
 *   هرگز دوباره ضرب و تقسیم float نکنید، فقط نشان دهید (جمع با `lib/num.ts`).
 * - `version` همان xmin ردیف است؛ هر نوشتن آن را برمی‌گرداند و نسخهٔ کهنه ۴۰۹ می‌گیرد.
 * - `clientKey` (uuid) ثبت را تکرارناپذیر می‌کند: دوباره فرستادن همان کلید همان ثبت را برمی‌گرداند، نه ثبت دوم.
 * - تاریخ ثبت `yyyy-MM-dd` (روز ایران، DateOnly)؛ زمان‌ها ISO با منطقهٔ زمانی.
 */

import { CostBasis, CostGroup } from './rugs'
import { AllocationMethod, AreaBasis, CurrencyUnit } from './platform'

export { AllocationMethod, AreaBasis, CostBasis, CostGroup, CurrencyUnit }

// ── enumها ─────────────────────────────────────────────────────────────

/** هزینه برای کدام فرش‌هاست (D.2). */
export const CostScope = { Selection: 0, Bale: 1, Shipment: 2, IntakeBatch: 3 } as const
export type CostScope = (typeof CostScope)[keyof typeof CostScope]

/** وضعیت تقسیم یک ثبت: کامل، بخشی ماند، یا هیچ (D.3). */
export const AllocationState = { Allocated: 0, Partial: 1, Unallocated: 2 } as const
export type AllocationState = (typeof AllocationState)[keyof typeof AllocationState]

/** یک سطر صورت‌حساب طرف خدمات: کار یک مرحله یا رسید یک ثبت هزینه (B.14، I.3). */
export const ProviderWorkKind = { Step: 0, Receipt: 1 } as const
export type ProviderWorkKind = (typeof ProviderWorkKind)[keyof typeof ProviderWorkKind]

/** واحد وزن یک سهم (`ShareWeightUnits` سرور). */
export const ShareWeightUnit = {
  /** صدم متر مربع */
  AreaHundredths: 'm2x100',
  Pieces: 'pieces',
  Equal: 'equal',
  UsdCents: 'usdCents',
  PurchaseCents: 'purchaseCents',
  /** مبلغ مستقیم (ثبت زندهٔ متری/دانه‌ای یا مبلغ ثابت) */
  Amount: 'amount',
} as const

/** کدهای هشدار و خطای تقسیم (D.8)؛ متن هر کد در `lib/messages/costs.ts`. */
export const CostCode = {
  StepDouble: 'W_STEP_DOUBLE',
  PurchaseDouble: 'W_PURCHASE_DOUBLE',
  PinDropped: 'W_PIN_DROPPED',
  ZeroWeights: 'W_ZERO_WEIGHTS',
  NoDeclaredValue: 'W_NO_DECLARED_VALUE',
  EmptyTarget: 'W_EMPTY_TARGET',
  TargetDeleted: 'W_TARGET_DELETED',
  LateCost: 'W_LATE_COST',
  RateDrift: 'W_RATE_DRIFT',
  PinsResidual: 'W_PINS_RESIDUAL',
  NegativeWeight: 'W_NEGATIVE_WEIGHT',
  PinsExceed: 'E_PINS_EXCEED',
  PinsBasis: 'E_PINS_BASIS',
  Circular: 'E_CIRCULAR',
  FxRequired: 'E_FX_REQUIRED',
  ScopeEmptySelection: 'E_SCOPE_EMPTY_SELECTION',
} as const
export type CostCode = (typeof CostCode)[keyof typeof CostCode]

/** کلید ثابت نوع‌های ساختهٔ برنامه (`CostCategory.SeedKey`) که فرانت به آن‌ها تکیه دارد. */
export const CostSeedKey = {
  PurchasePrice: 'purchase.price',
  PurchaseBroker: 'purchase.broker',
  PurchaseFreightIn: 'purchase.freightIn',
  /** «خرید یکجا»: فقط از یک ورودی پیشنهاد می‌شود (H.2) */
  PurchaseLump: 'purchase.lump',
} as const

// ── نوع هزینه ──────────────────────────────────────────────────────────

/** `GET /api/cost-categories` — آینهٔ `CostCategoryDto`. پیش‌فرض‌ها فقط فرم ثبت را پر می‌کنند. */
export interface CostCategory {
  id: string
  name: string
  group: CostGroup
  /** نام نماد در `/icons.svg` */
  icon: string | null
  sortOrder: number
  isActive: boolean
  defaultBasis: CostBasis
  defaultRate: number | null
  /** null یعنی ارز کارگاه */
  defaultCurrency: CurrencyUnit | null
  defaultMethod: AllocationMethod
  defaultAreaBasis: AreaBasis
  defaultScope: CostScope | null
  showInIntake: boolean
  stepTypeId: string | null
  seedKey: string | null
  version: number
}

/** `POST /api/cost-categories/quick` (Business): «+ نوع تازه» با پیش‌فرض‌های گروه. */
export interface QuickCostCategoryRequest {
  name: string
  group: CostGroup
}

/** `POST/PUT /api/cost-categories[/{id}]` (Configure). */
export interface SaveCostCategoryRequest {
  name: string
  group: CostGroup
  icon: string | null
  sortOrder: number
  isActive: boolean
  defaultBasis: CostBasis
  defaultRate: number | null
  defaultCurrency: CurrencyUnit | null
  defaultMethod: AllocationMethod
  defaultAreaBasis: AreaBasis
  defaultScope: CostScope | null
  showInIntake: boolean
  stepTypeId: string | null
  /** فقط برای ویرایش */
  version: number | null
}

// ── ثبت هزینه ──────────────────────────────────────────────────────────

/** مبلغ ثابت یک فرش (به ارز پایهٔ کارگاه، پس از تبدیل؛ D.3 «Pins»). */
export interface Pin {
  rugId: string
  amount: number
}

/** بدنهٔ `POST /api/costs`، `PUT /api/costs/{id}` و `POST /api/costs/preview` — آینهٔ `SaveCostEntryRequest`. */
export interface SaveCostEntryRequest {
  categoryId: string
  /** `yyyy-MM-dd` */
  date: string
  description: string | null
  receiptNo: string | null
  serviceProviderId: string | null
  basis: CostBasis
  /** نرخ هر متر یا هر تخته (مبنای PerM2/PerPiece) */
  rate: number | null
  /** مبلغ کل (مبنای Total) */
  amount: number | null
  currency: CurrencyUnit
  /** نرخ تبدیل دلار (فقط وقتی ارز دلار است) */
  fxRate: number | null
  areaBasis: AreaBasis
  method: AllocationMethod
  scope: CostScope
  shipmentId: string | null
  batchId: string | null
  rugIds: string[] | null
  baleIds: string[] | null
  /** چطور فهرست فرش‌ها ساخته شد: «۲۴۲ فرش انبار در ۱۴۰۵/۰۶/۱۵» */
  scopeNote: string | null
  pins: Pin[] | null
  /** فقط برای ویرایش */
  version: number | null
  /** ثبت تکرارناپذیر (uuid)؛ تلاش دوباره همان کلید را می‌فرستد */
  clientKey: string | null
}

/** یک هشدار تقسیم — آینهٔ `WarningDto`. `message` همان متن فارسی سرور است. */
export interface CostWarning {
  code: string
  message: string
  count: number
}

/** سهم یک فرش — آینهٔ `ShareDto`. */
export interface CostShare {
  rugId: string
  serial: string | null
  /** وزن صحیح (صدم متر، تعداد تخته، سنت دلار، …) */
  weightUnits: number
  /** واحد وزن (`ShareWeightUnit`) */
  weightUnit: string
  amount: number
  isPinned: boolean
  /** سهم هر تخته برای دسته؛ null برای فرش تکی */
  perPiece: number | null
}

/** `POST /api/costs/preview` و `CostEntry.result` — آینهٔ `AllocationPreviewDto`. */
export interface AllocationPreview {
  /** مبلغ به ارز خودِ ثبت */
  originalAmount: number
  /** مبلغ به ارز پایهٔ کارگاه (T) */
  baseAmount: number
  lines: number
  pieces: number
  areaM2: number
  /** هر متر (پایه)؛ null وقتی متراژ صفر است */
  perM2: number | null
  state: AllocationState
  unallocated: number
  warnings: CostWarning[]
  shares: CostShare[]
  /** جملهٔ آمادهٔ سرور («… بین ۲۴۲ تخته به نسبت متراژ تقسیم می‌شود») */
  sentence: string
}

/** `GET/PUT /api/costs/{id}`، `POST /api/costs` — آینهٔ `CostEntryDto`. */
export interface CostEntry {
  id: string
  number: number
  request: SaveCostEntryRequest
  version: number
  result: AllocationPreview
  createdAt: string
  // ── افزوده بر I.3 (دنباله‌دار) ──
  categoryName?: string | null
  group?: CostGroup
  /** زمان ثابت شدن رسید متری/دانه‌ای؛ null یعنی زنده یا «کل» */
  frozenAt?: string | null
  deleted?: boolean
  serviceProviderName?: string | null
  categoryIcon?: string | null
}

/** یک سطر `GET /api/costs` — آینهٔ `CostEntryListItemDto`. */
export interface CostEntryListItem {
  id: string
  number: number
  /** `yyyy-MM-dd` */
  date: string
  category: string
  group: CostGroup
  description: string | null
  /** «محمولهٔ ۸»، «۵۲ تخته»، «ورودی ۳»… */
  targetsText: string
  baseAmount: number
  /** مبلغ به ارز خودِ ثبت وقتی با ارز کارگاه فرق دارد */
  originalAmount: number | null
  currency: CurrencyUnit
  state: AllocationState
  unallocated: number
  deleted: boolean
  // ── افزوده بر I.3 (دنباله‌دار) ──
  /** نسخه برای حذف از همین فهرست */
  version?: number
  categoryIcon?: string | null
  basis?: CostBasis
  /** رسید متری/دانه‌ای ثابت‌شده */
  frozen?: boolean
  /** کدهای هشدار آخرین تقسیم (برای تراشه‌ها) */
  warningCodes?: string[] | null
}

/** پرسش `GET /api/costs`. */
export interface CostListQuery {
  shipmentId?: string | null
  batchId?: string | null
  categoryId?: string | null
  state?: AllocationState | null
  /** «تقسیم‌نشده»: هر ثبتی که همهٔ مبلغش تقسیم نشده (بخشی یا هیچ) */
  notFullyAllocated?: boolean
  /** ثبت‌های حذف‌شده هم بیایند */
  includeDeleted?: boolean
  /** فقط ثبت‌هایی که این فرش را دارند */
  rugId?: string | null
  /** `yyyy-MM-dd` */
  from?: string | null
  to?: string | null
  page?: number
  pageSize?: number
}

/** پاسخ صفحه‌بندی‌شده (`PagedResult<T>`). */
export interface CostPage<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages?: number
  hasPrevious?: boolean
  hasNext?: boolean
}

/** بدنهٔ `POST /api/costs/resolve-targets`: شماره‌ها، یک فیلتر فهرست فرش، یا «همهٔ انبار». */
export interface ResolveTargetsRequest {
  serialsText?: string | null
  filter?: Record<string, unknown> | null
  inStock?: boolean | null
}

/** پاسخ همان: شناسه‌ها هنگام ذخیره ثابت می‌شوند؛ هیچ‌وقت فیلتر زنده نیست (D.2). */
export interface ResolvedTargets {
  rugIds: string[]
  lines: number
  pieces: number
  areaM2: number
  /** شماره‌هایی که پیدا نشدند */
  unresolved: string[]
  scopeNote: string
}

// ── درآمد و سود محموله (D.6) ───────────────────────────────────────────

/** `GET /api/shipments/{id}/profit`، پاسخ `PUT …/revenue` — آینهٔ `ShipmentProfitDto` (با عضوهای دنباله‌دار اختیاری). */
export interface ShipmentProfit {
  pieces: number
  areaM2: number
  usdTotal: number
  purchase: number
  steps: number
  expenses: number
  total: number
  costPerM2: number | null
  /** فروش محموله به ارز پایه؛ null یعنی هنوز نوشته نشده */
  revenueBase: number | null
  profit: number | null
  finalized: boolean
  // اگر سرور این‌ها را بدهد، فرم فروش از همان‌ها پر می‌شود
  revenueAmount?: number | null
  revenueCurrency?: CurrencyUnit | null
  revenueFx?: number | null
  version?: number | null
}

/** بدنهٔ `PUT /api/shipments/{id}/revenue` (Business). */
export interface SaveRevenueRequest {
  amount: number | null
  currency: CurrencyUnit
  fx: number | null
  version: number
}

// ── پنل ثبت هزینه ─────────────────────────────────────────────────────

/**
 * props پنل `cost-entry` (I.5 «CostPanelPreset»): از کجا باز شد. همه اختیاری‌اند؛ پنل بی‌preset «برای» را خالی و
 * فوکوس‌دار می‌گذارد (A.4). `entryId` پنل را در حالت ویرایش باز می‌کند.
 */
export interface CostPanelPreset {
  scope?: CostScope
  shipmentId?: string | null
  batchId?: string | null
  baleIds?: string[] | null
  rugIds?: string[] | null
  /** نوع هزینهٔ از پیش انتخاب‌شده (مثلاً «purchase.lump» از ورودی) */
  categorySeedKey?: string | null
  /** ویرایش یک ثبت موجود */
  entryId?: string | null
  /** پس از ذخیره صفحه تازه شود (صفحه‌های رِیزوری که عدد سرور نشان می‌دهند) */
  reloadOnSave?: boolean
}

/** نتیجهٔ بسته شدن پنل پس از ذخیره (Promise همان `openPanel`). */
export interface CostPanelResult {
  saved: true
  entry: CostEntry
}

/** رویداد سراسری پس از هر ثبت، ویرایش، حذف یا بازگردانی هزینه؛ جزیره‌های فهرست با آن دوباره می‌خوانند. */
export const COSTS_CHANGED_EVENT = 'rugs:costs-changed'
