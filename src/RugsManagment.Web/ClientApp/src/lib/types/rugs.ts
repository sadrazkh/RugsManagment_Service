/**
 * نوع‌های فرش از فاز ۲ — آینهٔ دستی DTOهای `Application/DTOs/Rugs/*.cs` (spec I.3) و enumهای Domain.
 *
 * چرا جدا از `legacy.ts`: آن فایل ثابت است و فازها هر کدام فایل خودشان را دارند (K.1). نوع‌های این‌جا نوع‌های قدیمی
 * (`Rug`، `RugListItem`، `RugCostSummary`) را **گسترش** می‌دهند، نه جایگزین: پس یک `Rug` فاز ۲ هنوز به هر تابع یا
 * جزیرهٔ قدیمی که `Rug` قدیمی می‌خواهد داده می‌شود. کد تازه از همین فایل import کند:
 * `import type { Rug } from '@/lib/types/rugs'`.
 *
 * قراردادها:
 * - enumها در JSON عدد می‌آیند؛ هر enum یک شیء ثابت (برای مقدار) و یک نوع هم‌نام دارد، مثل `platform.ts`. عددها با C#
 *   یکی‌اند و فقط افزودنی‌اند.
 * - پول (تومان/ریال) برای غیر Business null است (A.5): هر عضو پولی `| null` دارد. دلار گمرکی پنهان نمی‌شود.
 * - `version` همان xmin ردیف است (عدد)؛ هر نوشتنی آن را برمی‌گرداند و نسخهٔ کهنه ۴۰۹ می‌گیرد.
 * - همهٔ مساحت‌ها و مبلغ‌ها را سرور گرد کرده است؛ در فرانت با `lib/num.ts` حساب کنید، هرگز با ضرب float.
 */

import type {
  PagedResult,
  Rug as LegacyRug,
  RugCostSummary as LegacyRugCostSummary,
  RugListItem as LegacyRugListItem,
} from './legacy'
import type { AllocationMethod, RugStatus } from './platform'

export type { PagedResult }

// ── enumها ─────────────────────────────────────────────────────────────

/** گروه نوع هزینه: خرید (در `purchaseCost`) یا هزینه (در `expenseCost`). */
export const CostGroup = { Purchase: 0, Expense: 1 } as const
export type CostGroup = (typeof CostGroup)[keyof typeof CostGroup]

/** مبنای یک مبلغ. `Percent` فقط برای سطر خرید است (دلالی). */
export const CostBasis = { Total: 0, PerM2: 1, PerPiece: 2, Percent: 3 } as const
export type CostBasis = (typeof CostBasis)[keyof typeof CostBasis]

/** جای فرش (C.10)؛ محاسبه می‌شود، ذخیره نمی‌شود. برچسب «در نگله» از `terms` می‌آید، نه از متن ثابت. */
export const RugLocation = { InStock: 0, InBale: 1, Exported: 2, Sold: 3, Trashed: 4 } as const
export type RugLocation = (typeof RugLocation)[keyof typeof RugLocation]

/** وضعیت یک مرحلهٔ فرش. */
export const WorkflowStepStatus = { Pending: 0, InProgress: 1, Completed: 2, Skipped: 3, Cancelled: 4 } as const
export type WorkflowStepStatus = (typeof WorkflowStepStatus)[keyof typeof WorkflowStepStatus]

/** ستون مرتب‌سازی فهرست فرش‌ها. `TotalCost` برای غیر Business به `CreatedAt` برمی‌گردد. */
export const RugSortBy = {
  CreatedAt: 0,
  Sku: 1,
  Title: 2,
  Area: 3,
  TotalCost: 4,
  Status: 5,
  CurrentStep: 6,
  /** شماره: عددی بر اساس اولِ بازه؛ متنی و بی‌شماره همیشه آخر */
  Serial: 7,
} as const
export type RugSortBy = (typeof RugSortBy)[keyof typeof RugSortBy]

/** نوع تطبیق در `GET /api/rugs/by-serial/{serial}` (رشته در JSON، نه عدد). */
export const SerialMatchType = { Exact: 'exact', InRange: 'inRange' } as const
export type SerialMatchType = (typeof SerialMatchType)[keyof typeof SerialMatchType]

// ── مقدار فیلدها (RugFieldWriter) ─────────────────────────────────────

/** هر مقدار JSON. */
export type JsonValue = string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue }

/**
 * مقدار فیلدها با کلید FieldCatalog (`rug.serial`، `rug.qty`، `rug.intakeLength`، `rug.custom.bg_color`، …).
 * `null` یعنی «خالی کن»؛ کلیدی که نیامده یعنی «دست نزن». کلید پولی از غیر Business → ۴۰۳ برای کل درخواست.
 */
export type FieldValues = Record<string, JsonValue>

/**
 * یک فیلد در کاتالوگ (`GET /api/fields?purpose=grid|document&kind=`) — آینهٔ `FieldDefDto` (I.3).
 * فیلدهای پولی فقط برای Business می‌آیند؛ دلار گمرکی `money: false` است.
 */
export interface FieldDef {
  key: string
  label: string
  scope: string
  dataType: string
  defaultFormat: string
  aggregatable: boolean
  money: boolean
  gridEditable: boolean
}

// ── فرش ────────────────────────────────────────────────────────────────

/** یک عکس در گالری فرش — آینهٔ `RugImageDto`. */
export interface RugImage {
  id: string
  url: string
  thumbnailUrl: string
  width: number
  height: number
  sizeBytes: number
  sortOrder: number
  isPrimary: boolean
}

/** یک سطر قیمت خرید — آینهٔ `PurchaseLineDto`. فقط Business. */
export interface PurchaseLine {
  /** null برای سطر تازه در درخواست */
  id: string | null
  /** نوع هزینهٔ گروه خرید؛ null برای سطر آزاد یا سطر قدیمی «قیمت خرید» */
  categoryId: string | null
  label: string
  basis: CostBasis
  /** مبلغ برای «کل»، نرخ برای «هر متر»/«هر تخته»، درصد برای «درصد» */
  rate: number
  /** مبلغ حساب‌شده (فقط خواندنی؛ در درخواست نادیده گرفته می‌شود) */
  amount: number
  sortOrder: number
  notes: string | null
}

/**
 * خلاصهٔ بهای تمام‌شده (فقط Business) — آینهٔ `RugCostSummaryDto`. از `RugCostTotal`:
 * `totalProcessCost` = مراحل، `purchaseCost` = خرید با سهم «خرید یکجا»، `totalInvestment` = بهای تمام‌شده.
 */
export interface RugCostSummary extends LegacyRugCostSummary {
  actualSaleAmount?: number | null
  /** فروش خالص − (`costAtSale` ?? بهای تمام‌شده) */
  actualProfit?: number | null
  stepCost: number
  expenseCost: number
  /** null وقتی مساحت صفر است */
  costPerM2: number | null
  costPerPiece: number
  exportRevenueShare: number | null
  /** false یعنی پاس هنوز این فرش را حساب نکرده و عددها صفر پیش‌فرض‌اند */
  computed: boolean
  /** عکس بها در لحظهٔ فروش؛ اگر با `totalInvestment` فرق کند چیپ «هزینه پس از فروش تغییر کرده» */
  costAtSale: number | null
}

/**
 * فرش کامل از فاز ۲ — آینهٔ `RugDto`. `widthMeters`/`lengthMeters` جفت مؤثر‌اند (نهایی ?? اول) و
 * `areaSquareMeters` مساحت یک تخته در همان اندازه. `purchaseCost` جمع سطرهای خرید است (بی سهم خرید یکجا).
 */
export interface Rug extends LegacyRug {
  costs: RugCostSummary | null
  currentStepNameFa?: string | null
  currentStepIndex?: number
  images?: RugImage[] | null
  /** «3778»، «3911 - 3960» یا «A-12»؛ null یعنی بی‌شماره */
  serialNo: string | null
  /** تعداد تخته‌ها (دسته)، دست‌کم ۱ */
  quantity: number
  intakeLength: number
  intakeWidth: number
  /** هر دو پر یا هر دو null */
  finalLength: number | null
  finalWidth: number | null
  /** مساحت یک تخته × تعداد */
  totalAreaM2: number
  location: RugLocation
  shipmentId: string | null
  shipmentNumber: number | null
  baleId: string | null
  baleNumber: number | null
  /** دلار گمرکی هر متر (برای همه) */
  usdPerM2: number | null
  /** دلار کل خط = دلار یک تخته × تعداد */
  lineUsd: number | null
  version: number
  /** فقط Business؛ برای بقیه null */
  purchaseLines: PurchaseLine[] | null
  /** مساحت یک تخته در اندازهٔ اول (مبنای خرید «هر متر») */
  intakeAreaM2: number
  finalMeasuredAt: string | null
  batchNumber: number | null
  sellerName: string | null
}

/** ردیف فهرست فرش‌ها از فاز ۲ — آینهٔ `RugListItemDto`. */
export interface RugListItem extends LegacyRugListItem {
  /** بندانگشتی عکس شاخص (نام درست عضو سرور؛ `imageUrl` قدیمی هرگز پر نمی‌شد) */
  thumbnailUrl: string | null
  serialNo: string | null
  quantity: number
  totalAreaM2: number
  /** false یعنی `totalInvestment` هنوز عدد واقعی نیست */
  costComputed: boolean
  /** فقط Business */
  costPerM2: number | null
  location: RugLocation
  baleNumber: number | null
  shipmentNumber: number | null
  version: number
}

/**
 * پارامترهای query فهرست (`GET /api/rugs`، صفحهٔ `/Rugs`) — آینهٔ `RugQuery`. همه اختیاری؛ نام‌ها همان کلیدهای
 * query string‌اند. `serial` تطبیق دقیق یا «داخل دسته» است؛ `search` «شامل» (شماره را هم می‌گردد).
 */
export interface RugQuery {
  search?: string
  serial?: string
  status?: RugStatus
  location?: RugLocation
  batchId?: string
  withoutBatch?: boolean
  shipmentId?: string
  baleId?: string
  notBaled?: boolean
  stepTypeId?: string
  /** yyyy-MM-dd (روز ایران) */
  createdFrom?: string
  createdTo?: string
  sortBy?: RugSortBy
  descending?: boolean
  page?: number
  pageSize?: number
}

/** ردیف سطل زباله — آینهٔ `DeletedRugDto`. */
export interface DeletedRug {
  id: string
  sku: string
  title: string | null
  origin: string | null
  widthMeters: number
  lengthMeters: number
  status: RugStatus
  totalInvestment: number
  deletedAt: string
  deletedByName: string | null
  serialNo: string | null
  quantity: number
}

// ── درخواست‌های نوشتن ──────────────────────────────────────────────────

/** یک مرحلهٔ سفارشی هنگام ساخت — آینهٔ `CustomRugStepRequest`. */
export interface CustomRugStep {
  processStepTypeId: string
  isOptional: boolean
  serviceProviderId: string | null
}

/**
 * `POST /api/rugs` به شکل فاز ۲: فقط `values` و مسیر. عضوهای قدیمی (اندازه، قیمت خرید، …) را نفرستید؛ با `values`
 * پر، سرور آن‌ها را نادیده می‌گیرد.
 */
export interface CreateRugRequest {
  values: FieldValues
  workflowTemplateId?: string | null
  skippedOptionalStepIds?: string[] | null
  customSteps?: CustomRugStep[] | null
}

/** `PUT /api/rugs/{id}` به شکل فاز ۲: فقط `values` و `version` (RugForm دیگر پول و طول/عرض مؤثر نمی‌فرستد). */
export interface UpdateRugRequest {
  values: FieldValues
  version: number
}

/** `PUT /api/rugs/{id}/final-size` → `Rug`. دقیقاً یکی: طول و عرض، یا `sameAsIntake`، یا `clear`. */
export interface SetFinalSizeRequest {
  length?: number | null
  width?: number | null
  sameAsIntake?: boolean | null
  clear?: boolean | null
  version: number
}

/**
 * `POST /api/rugs/{id}/split` (D.9). هر دو شماره null → خودکار (فقط وقتی طول بازه = تعداد؛ فرش تازه شماره‌های آخر را
 * می‌گیرد). وگرنه هر دو لازم: `serialFrom` شمارهٔ تازهٔ فرش مبدأ، `serialTo` شمارهٔ فرش تازه.
 */
export interface SplitRugRequest {
  /** تعداد تخته‌ای که به فرش تازه می‌رود (۱ ≤ k < تعداد) */
  quantity: number
  serialFrom?: string | null
  serialTo?: string | null
  version: number
}

export interface SplitRugResult {
  original: Rug
  created: Rug
}

// ── خرید ───────────────────────────────────────────────────────────────

/** `PUT /api/rugs/{id}/purchase-lines` (Business): جایگزینی همه با نسخهٔ فرش. */
export interface ReplacePurchaseLinesRequest {
  lines: PurchaseLine[]
  version: number
}

/** پاسخ همان PUT. */
export interface PurchaseLinesResult {
  lines: PurchaseLine[]
  /** جمع مبلغ سطرها */
  purchaseCost: number
  /** نسخهٔ تازهٔ فرش */
  version: number
  /** خلاصهٔ تازهٔ بها */
  costs: RugCostSummary | null
}

/** یک نوع هزینهٔ گروه خرید برای انتخاب در ویرایشگر سطرها — آینهٔ `PurchaseCategoryOptionDto`. */
export interface PurchaseCategoryOption {
  id: string
  name: string
  seedKey: string | null
  defaultBasis: CostBasis
  defaultRate: number | null
  showInIntake: boolean
  sortOrder: number
}

// ── ریز هزینه ──────────────────────────────────────────────────────────

/** یک مرحله در ریز هزینه — آینهٔ `StepCostDto`. */
export interface StepCost {
  stepId: string
  processStepTypeId: string
  stepNameFa: string
  orderIndex: number
  status: WorkflowStepStatus
  serviceProviderName: string | null
  appliedPricingModel: string | null
  appliedUnitRate: number | null
  pricedLength: number | null
  pricedWidth: number | null
  pricedQuantity: number | null
  /** مبلغ مؤثر مرحله (مبلغ دستی یا محاسبه‌شده + تعدیل)، با کف صفر */
  effectiveCost: number
  /** سهم واقعی در بها: برای انجام‌شده/در حال انجام همان مبلغ مؤثر، وگرنه ۰ */
  countedCost: number
}

/** سهم فرش از یک ثبت هزینه — آینهٔ `AllocationLineDto` (تا فاز هزینه‌ها همیشه خالی). */
export interface AllocationLine {
  entryId: string
  entryNumber: number
  category: string
  group: CostGroup
  /** yyyy-MM-dd */
  date: string
  amount: number
  weightUnits: number
  totalWeightUnits: number
  method: AllocationMethod
  isPinned: boolean
  entryBase: number
}

/** `GET /api/rugs/{id}/cost-breakdown` (Business) — آینهٔ `RugCostBreakdownDto`. */
export interface RugCostBreakdown {
  purchase: PurchaseLine[]
  steps: StepCost[]
  allocations: AllocationLine[]
  summary: RugCostSummary
}

// ── شماره ──────────────────────────────────────────────────────────────

/** `GET /api/rugs/next-serial`؛ در حالت «دستی» `next` = null. */
export interface NextSerial {
  next: number | null
}

/** `GET /api/rugs/by-serial/{serial}` (پیدا نشد → ۴۰۴). */
export interface RugBySerial {
  rug: RugListItem
  location: RugLocation
  matchType: SerialMatchType
}

// ── سلامت حساب‌ها (Configure) ─────────────────────────────────────────

/** کد بررسی در `CostMismatch.check`. */
export const CostMismatchCheck = {
  MissingTotal: 'missingTotal',
  Total: 'total',
  PurchaseLine: 'purchaseLine',
  EffectiveSize: 'effectiveSize',
  StaleVersion: 'staleVersion',
} as const
export type CostMismatchCheck = (typeof CostMismatchCheck)[keyof typeof CostMismatchCheck]

export interface CostMismatch {
  rugId: string
  sku: string
  serialNo: string | null
  /** یکی از `CostMismatchCheck`؛ کد ناشناختهٔ فاز بعد را هم نشان دهید */
  check: string
  stored: number | null
  expected: number | null
  detail: string | null
}

/** `GET /api/diagnostics/cost-consistency`؛ `mismatches` خالی یعنی سالم. */
export interface CostConsistencyReport {
  checked: number
  mismatches: CostMismatch[]
}

/** `POST /api/diagnostics/ledger-rebuild`. */
export interface LedgerRebuildResult {
  rugs: number
  entries: number
  lines: number
}
