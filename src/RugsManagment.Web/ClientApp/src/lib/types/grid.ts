/**
 * نوع‌های جدول (فاز ۳) — آینهٔ دستی `Application/DTOs/Grid/*.cs` (spec I.3، G.6) و پاسخ‌های `/api/grid/*`،
 * `/api/settings/grid-layout/*` و `/api/batches/{today,recent}`.
 *
 * قراردادها (مثل `rugs.ts`):
 * - enumها در JSON عدد می‌آیند؛ هر enum یک شیء ثابت و یک نوع هم‌نام دارد. فقط افزودنی؛ هرگز جابه‌جا نکنید.
 * - `version` همان xmin ردیف است (عدد).
 * - مقدار هر خانه با کلید FieldCatalog می‌آید و می‌رود (`rug.serial`، `batch.seller`، `purchase.price`، …)؛ `null`
 *   یعنی «خالی»، کلیدی که نیامده یعنی «دست نزن».
 * - پول (تومان/ریال) برای غیر Business هرگز نمی‌آید: نه ستونش، نه مقدارش (A.5).
 *
 * شکل خانه‌های خرید (G.2 «Purchase columns») — قرارداد این جزیره با `RugGridService`:
 * - خواندن (`GridRowDto.values['purchase.price']`): `null` (سطری نیست)، `{ rate, basis, amount? }` (یک سطر از این نوع)،
 *   یا `{ rate: null, basis, amount, lines: n }` با n ≥ ۲ (چند سطر؛ خانه فقط‌خواندنی با چیپ «چند سطر»). عدد خالی هم
 *   پذیرفته است (نرخ با مبنای پیش‌فرض ستون).
 * - نوشتن (`GridOp.values`): همان چیزی که `RugFieldWriter` می‌خواند — `{ rate, basis }` برای خانهٔ «مبلغ + واحد»، عدد
 *   برای درصد، و `null` یعنی «سطر را حذف کن».
 */
import type { JsonValue } from './rugs'
import { CostBasis } from './rugs'

export { CostBasis }
export type { JsonValue }

// ── شناسه و ستون ───────────────────────────────────────────────────────

/** جدول‌های شناخته‌شده؛ `bales` را فاز نگله‌بندی اضافه می‌کند. */
export type GridId = 'intake' | 'rugs' | 'bales'

export const GRID_IDS: readonly GridId[] = ['intake', 'rugs', 'bales']

/** نوع خانه (`GridColumnDto.Type`). */
export type GridColumnType =
  | 'serial'
  | 'text'
  | 'combo'
  | 'select'
  | 'int'
  | 'dim'
  | 'money'
  | 'money+unit'
  | 'percent'
  | 'readonly'

export const GRID_COLUMN_TYPES: readonly GridColumnType[] = [
  'serial',
  'text',
  'combo',
  'select',
  'int',
  'dim',
  'money',
  'money+unit',
  'percent',
  'readonly',
]

/** یک ستون — آینهٔ `GridColumnDto`. */
export interface GridColumn {
  key: string
  label: string
  type: GridColumnType
  /** پهنا به `ch` تا با اندازهٔ نوشته بزرگ شود */
  width: number
  /** مقدار به ردیف تازهٔ بعدی می‌رسد («چسبان»، G.2) */
  sticky: boolean
  readOnly: boolean
  required: boolean
  /** گزینه‌های ثابت (select/combo)؛ برای «مبلغ + واحد» شاید نام مبناها با پیش‌فرض اول */
  options: string[] | null
  money: boolean
  /** false: Tab از روی این خانه می‌پرد (خانهٔ فقط‌خواندنی هم هرگز ایستگاه Tab نیست) */
  tabStop?: boolean
}

// ── داده ───────────────────────────────────────────────────────────────

/** یک ردیف — آینهٔ `GridRowDto`. */
export interface GridRow {
  rugId: string
  version: number
  sku: string | null
  values: Record<string, JsonValue | undefined>
  /**
   * مقدارهای حساب‌شده: `rug.areaM2`، `rug.totalAreaM2`، `rug.purchaseTotal`، و قرارداد این جزیره:
   * `row.canDelete` (آیا «حذف این فرش» به این کاربر نشان داده شود؛ G.3 «The menu item is hidden when the rule already
   * says no»).
   */
  derived: Record<string, JsonValue | undefined>
  lineId?: string | null
  lineVersion?: number | null
}

/** جمع یک ورودی (فروشندهٔ یک روز) در پانویس جدول ورود — آینهٔ `GridGroupTotalsDto` (E7). */
export interface GridGroupTotals {
  batchId: string | null
  batchNumber: number | null
  sellerName: string | null
  rows: number
  pieces: number
  areaM2: number
  purchase: number | null
}

/** جمع‌ها — آینهٔ `GridTotalsDto`. `purchase` برای غیر Business null؛ `groups` فقط در جدول ورود. */
export interface GridTotals {
  rows: number
  pieces: number
  areaM2: number
  purchase: number | null
  groups?: GridGroupTotals[] | null
}

/** `GET /api/grid/{gridId}` — آینهٔ `GridDataDto`. */
export interface GridData {
  columns: GridColumn[]
  rows: GridRow[]
  totals: GridTotals
  /** شمارهٔ پیشنهادی بعدی (G.4)؛ null در حالت «دستی» */
  nextSerial: number | null
}

// ── ذخیره (G.6) ────────────────────────────────────────────────────────

/** ورودیِ ردیف‌های جدول ورود: روز (و شاید فروشنده) — آینهٔ `GridBatchContext`. */
export interface GridBatchContext {
  id: string | null
  sellerName: string | null
  /** روز ایرانی، ISO `yyyy-MM-dd` */
  receivedAt: string | null
}

/** یک عمل روی یک ردیف — آینهٔ `GridOp`. */
export interface GridOp {
  clientRowId: string
  rugId: string | null
  version: number | null
  values: Record<string, JsonValue>
  /** مقدار پیشینِ همان کلیدها (پایهٔ ادغام سه‌طرفه) */
  baseValues: Record<string, JsonValue> | null
  delete: boolean
  lineVersion?: number | null
}

/** `POST /api/grid/rugs/batch` — آینهٔ `GridBatchRequest`. */
export interface GridBatchRequest {
  gridId: GridId
  batch: GridBatchContext | null
  shipmentId: string | null
  ops: GridOp[]
}

export const GridRowStatus = { Saved: 0, Invalid: 1, Conflict: 2, Deleted: 3 } as const
export type GridRowStatus = (typeof GridRowStatus)[keyof typeof GridRowStatus]

export interface FieldError {
  field: string
  message: string
}

/** نتیجهٔ یک عمل — آینهٔ `GridOpResult`. */
export interface GridOpResult {
  clientRowId: string
  rugId: string | null
  version: number | null
  sku: string | null
  status: GridRowStatus
  derived: Record<string, JsonValue | undefined> | null
  errors: FieldError[] | null
  /** مقدار فعلیِ همهٔ خانه‌های ردیف در سرور (پس از ذخیره، یا در تعارض) */
  current: Record<string, JsonValue | undefined> | null
  lineVersion?: number | null
  /** اولین شمارهٔ آزاد وقتی شماره همپوشانی داشت («شمارهٔ ۴۰۰۵ آزاد است؛ همان را بگذارم؟») */
  nextFree?: number | null
}

/** پاسخ `POST /api/grid/rugs/batch` — آینهٔ `GridBatchResultDto` (جزیره آرایهٔ خالیِ نتیجه‌ها را هم می‌پذیرد). */
export interface GridBatchResponse {
  results: GridOpResult[]
  totals?: GridTotals | null
  nextSerial?: number | null
}

// ── خانهٔ خرید ─────────────────────────────────────────────────────────

/** مقدار یک خانهٔ خرید در جدول (بالا را ببینید). */
export interface PurchaseCellValue {
  /** نرخ (درصد برای درصد)؛ null برای خانهٔ چندسطری یا وقتی فقط چیپ واحد عوض شده و نرخ هنوز نوشته نشده */
  rate: number | null
  basis: CostBasis
  /** مبلغ حساب‌شدهٔ سطر (یا جمع سطرها در چندسطری)، فقط خواندنی */
  amount?: number | null
  /** تعداد سطرهای این نوع؛ ۲ یا بیشتر یعنی فقط‌خواندنی */
  lines?: number
}

/** برچسب چیپ واحد (همان واژه‌های پنل هزینه، H.2). */
export const UNIT_LABELS: Readonly<Record<number, string>> = {
  [CostBasis.PerM2]: 'هر متر',
  [CostBasis.PerPiece]: 'هر تخته',
  [CostBasis.Total]: 'کل',
}

/** ترتیب چرخش چیپ با ضربه یا Alt+↓. */
export const UNIT_CYCLE: readonly CostBasis[] = [CostBasis.PerM2, CostBasis.PerPiece, CostBasis.Total]

// ── چیدمان (تنظیمات) ───────────────────────────────────────────────────

/**
 * `GET/PUT /api/settings/grid-layout/{gridId}` — آینهٔ `GridLayoutDto`. در PUT ترتیب، برچسب، پهنا، چسبان و ایستگاه Tab
 * خوانده می‌شود و ستونی که نیامده پنهان است؛ نسخهٔ کهنه → ۴۰۹. `version` صفر یعنی هنوز پیش‌فرض برنامه.
 */
export interface GridLayout {
  gridId: GridId
  columns: GridColumn[]
  version: number
  /** همهٔ ستون‌هایی که این جدول می‌تواند نشان دهد (برای برگرداندن ستون پنهان)؛ در PUT نادیده */
  available?: GridColumn[] | null
}

// ── ورودی‌ها ───────────────────────────────────────────────────────────

/**
 * یک «ورودی» (فرش‌های یک فروشنده در یک روز) برای نوار «ورودی‌های اخیر» — آینهٔ `IntakeBatchDto`
 * (`GET /api/batches/recent?days=7`، `POST /api/batches/today`).
 */
export interface IntakeBatch {
  id: string
  name: string
  description: string | null
  receivedAt: string | null
  /** تعداد ردیف‌ها (فرش‌ها) */
  rugCount: number
  currentStepSummary: string | null
  sellerName: string | null
  number: number
  /** روز ایران، ISO `yyyy-MM-dd` */
  receivedOn: string | null
  /** Σ تعداد تخته‌ها */
  pieces: number
  areaM2: number
  /** همان روز به شمسی با ارقام لاتین («1405/06/14») */
  receivedOnJalali: string | null
}

/** `POST /api/batches/today` — آینهٔ `TodayBatchRequest`. */
export interface TodayBatchRequest {
  sellerName: string | null
  /** روز ایران (`yyyy-MM-dd`) یا لحظهٔ ISO؛ خالی یعنی امروز */
  receivedAt: string | null
}

/** props جزیرهٔ `intake-grid` — آینهٔ `IntakePageDto` (I.5). */
export interface IntakePageProps {
  /** روز ایرانِ جدول، ISO */
  date: string
  /** «ورودی امروز» / «ورودی دیروز» / «ورودی ۱۴۰۵/۰۶/۱۰» (با عوض شدن روز، جزیره خودش می‌سازد) */
  title?: string | null
  defaultSeller: string | null
  serialMode: number
  maxPlausibleSideM: number
}
