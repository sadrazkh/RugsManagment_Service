/**
 * نوع‌های محموله، نگله و ارزش گمرکی از فاز ۴a — آینهٔ دستی DTOهای `Application/DTOs/Shipments/*.cs` (spec I.3، با
 * عضوهای دنباله‌دار افزوده) و enumهای Domain. کد تازه مستقیم از همین فایل import کند:
 * `import type { Shipment } from '@/lib/types/shipments'`.
 *
 * قراردادها (مثل `rugs.ts`):
 * - enumها در JSON عدد می‌آیند؛ هر enum یک شیء ثابت (برای مقدار) و یک نوع هم‌نام دارد. عددها با C# یکی‌اند و فقط
 *   افزودنی‌اند؛ هرگز جابه‌جا نکنید.
 * - `version` همان xmin ردیف است (عدد)؛ هر نوشتنی آن را برمی‌گرداند و نسخهٔ کهنه ۴۰۹ می‌گیرد.
 * - دلار گمرکی پول داخلی نیست و برای همه (انباردار هم) دیده می‌شود؛ هیچ عضو این فایل مبلغ تومانی نیست.
 * - مساحت و دلار را سرور با قاعدهٔ «هر تخته، بعد × تعداد» گرد کرده (G.4)؛ در فرانت با `lib/num.ts` جمع بزنید، هرگز با
 *   ضرب float.
 * - تاریخ‌ها ISO‌اند: `documentDate` به شکل `yyyy-MM-dd` (روز ایران)، زمان‌ها با منطقهٔ زمانی.
 */

import type { RugListItem } from './rugs'

// ── enumها ─────────────────────────────────────────────────────────────

/** وضعیت محموله: «پیش‌نویس» (باز) یا «قطعی». */
export const ShipmentStatus = { Open: 0, Finalized: 1 } as const
export type ShipmentStatus = (typeof ShipmentStatus)[keyof typeof ShipmentStatus]

/** ارزش گمرکی یک ردیف از کجا آمد (C.5): دستی، از جدول، نرخ پیش‌فرض، یا ندارد. */
export const DeclaredRateSource = { Override: 0, Table: 1, Fallback: 2, Missing: 3 } as const
export type DeclaredRateSource = (typeof DeclaredRateSource)[keyof typeof DeclaredRateSource]

/** کدهای آمادگی (C.6)؛ `E_` جلوی قطعی شدن را می‌گیرد، `W_` فقط هشدار است. */
export const ReadinessCode = {
  EmptyShipment: 'E_EMPTY_SHIPMENT',
  EmptyBale: 'E_EMPTY_BALE',
  NoFinalSize: 'E_NO_FINAL_SIZE',
  NoDeclaredValue: 'E_NO_DECLARED_VALUE',
  BaleOverTarget: 'W_BALE_OVER_TARGET',
  NoSerial: 'W_NO_SERIAL',
  NoUsdRate: 'W_NO_USD_RATE',
  TemplateFieldEmpty: 'W_TEMPLATE_FIELD_EMPTY',
} as const
export type ReadinessCode = (typeof ReadinessCode)[keyof typeof ReadinessCode]

// ── محموله ─────────────────────────────────────────────────────────────

/**
 * بدنهٔ `POST /api/shipments` (Business). «محمولهٔ تازه» با یک لمس فقط `documentDate` را می‌فرستد (یا هیچ): عنوان خالی
 * «اظهار {شماره}»، تاریخ خالی امروزِ ایران، متراژ هدف و نرخ دلار خالی از ترجیحات کارگاه (C.1).
 */
export interface CreateShipmentRequest {
  title?: string | null
  /** `yyyy-MM-dd`؛ نبودن یعنی امروز */
  documentDate?: string | null
  destination?: string | null
  buyerName?: string | null
  declarationNo?: string | null
  customsOffice?: string | null
  targetBaleAreaM2?: number | null
  usdRate?: number | null
  notes?: string | null
}

/**
 * بدنهٔ `PUT /api/shipments/{id}` (Business): همهٔ خانه‌ها با هم جایگزین می‌شوند (خالی یعنی پاک)، جز عنوان خالی که
 * «اظهار {شماره}» می‌شود و تاریخ خالی که همان قبلی می‌ماند. پس از قطعی شدن هر تغییری جز یادداشت و متراژ هدف ۴۰۹ است.
 */
export interface UpdateShipmentRequest {
  title: string | null
  /** `yyyy-MM-dd` */
  documentDate: string | null
  destination: string | null
  buyerName: string | null
  declarationNo: string | null
  customsOffice: string | null
  targetBaleAreaM2: number | null
  usdRate: number | null
  notes: string | null
  version: number
}

/** یک ردیف `GET /api/shipments?status=`. */
export interface ShipmentListItem {
  id: string
  number: number
  title: string
  /** `yyyy-MM-dd` */
  documentDate: string
  status: ShipmentStatus
  bales: number
  /** ردیف‌ها (فرش یا دسته) */
  lines: number
  /** تخته‌ها (Σ تعداد) */
  pieces: number
  areaM2: number
  usdTotal: number
  /** تعداد کمبودهایی که جلوی قطعی شدن را می‌گیرند */
  readinessBlocking: number
}

/** جمع‌های محموله. */
export interface ShipmentTotals {
  bales: number
  lines: number
  pieces: number
  areaM2: number
  usdTotal: number
}

/**
 * یک کمبود آمادگی با پیام فارسی آماده و فرش‌های درگیر (برای «نشانم بده» و «بقیه همان اندازهٔ اول»). تراشهٔ یک نگله
 * نگله‌اش را دارد و تراشهٔ ارزش گمرکی (یکی برای هر مقدار فیلد کلید) همان مقدار را در `key`، برای فرم «نرخ بگذارید».
 */
export interface ReadinessIssue {
  code: ReadinessCode | string
  message: string
  count: number
  rugIds: string[]
  blocking: boolean
  key: string | null
  baleId: string | null
  baleNumber: number | null
}

/** خلاصهٔ یک نگله در سرِ محموله. */
export interface BaleSummary {
  id: string
  number: number
  version: number
  lines: number
  pieces: number
  areaM2: number
  usdTotal: number
  weightKg: number | null
}

/** `GET /api/shipments/{id}` — محموله با خلاصهٔ نگله‌ها، جمع‌ها و آمادگی. */
export interface Shipment {
  id: string
  number: number
  title: string
  /** `yyyy-MM-dd` */
  documentDate: string
  destination: string | null
  buyerName: string | null
  declarationNo: string | null
  customsOffice: string | null
  status: ShipmentStatus
  finalizeCount: number
  finalizedAt: string | null
  /** SHA-256 تصویر قطعی (۶۴ نویسهٔ hex)؛ فقط پس از قطعی شدن */
  snapshotHash: string | null
  /** متراژ هدفِ خودِ محموله (null یعنی از ترجیح کارگاه) */
  targetBaleAreaM2: number | null
  usdRate: number | null
  notes: string | null
  /** نسخهٔ محموله: برای ویرایش، قطعی کردن، باز کردن دوباره و شماره‌گذاری دوبارهٔ نگله‌ها */
  version: number
  bales: BaleSummary[]
  totals: ShipmentTotals
  readiness: ReadinessIssue[]
  /** متراژ هدف مؤثر (محموله ?? کارگاه)؛ null یعنی بی‌هدف */
  effectiveTargetAreaM2: number | null
  /** بازهٔ مجاز اطراف هدف به درصد */
  baleTargetTolerancePercent: number
}

/** بدنهٔ `POST /api/shipments/{id}/finalize` (Business). */
export interface FinalizeShipmentRequest {
  version: number
}

/** پاسخ قطعی کردن: `ok` = false یعنی کمبود بازدارنده هست و `issues` همه را دارد (محموله دست نخورد). */
export interface FinalizeShipmentResult {
  ok: boolean
  issues: ReadinessIssue[]
  shipment: Shipment
}

/** بدنهٔ `POST /api/shipments/{id}/reopen` (Business)؛ دلیل در تاریخچه ثبت می‌شود. */
export interface ReopenShipmentRequest {
  version: number
  reason?: string | null
}

// ── نگله ───────────────────────────────────────────────────────────────

/** یک ردیف نگله (یک فرش یا یک دسته) — آینهٔ `BaleRowDto`. */
export interface BaleRow {
  lineId: string
  lineVersion: number
  rugId: string
  rugVersion: number
  seq: number
  serial: string | null
  qty: number
  fieldColor: string | null
  borderColor: string | null
  pattern: string | null
  origin: string | null
  material: string | null
  intakeLength: number
  intakeWidth: number
  finalLength: number | null
  finalWidth: number | null
  /** تغییر بزرگ‌تر طول یا عرض به سانت، با علامت (منفی = کوچک‌تر شده، «↓۵ سانت»)؛ null بی اندازهٔ نهایی */
  sizeChangeCm: number | null
  /** مساحت یک تخته */
  areaM2: number
  /** مساحت × تعداد */
  lineAreaM2: number
  usdPerM2: number | null
  rateSource: DeclaredRateSource
  pieceUsd: number | null
  /** دلار یک تخته × تعداد */
  lineUsd: number | null
  baleId: string
  baleNumber: number
  /** نرخ دستیِ خط (فقط بازرگان می‌گذارد)؛ null یعنی از جدول یا پیش‌فرض */
  usdPerM2Override: number | null
  sku: string | null
  /** مقدار فیلد کلید ارزش گمرکی این فرش (برای فرم «نرخ بگذارید») */
  declaredKey: string | null
}

/** `GET /api/shipments/{id}/bales` — هر نگله با ردیف‌هایش. */
export interface Bale {
  id: string
  number: number
  version: number
  weightKg: number | null
  notes: string | null
  pieces: number
  areaM2: number
  usdTotal: number
  /** متراژ هدف (محموله ?? ترجیح کارگاه)؛ null یعنی بی‌هدف */
  targetAreaM2: number | null
  rows: BaleRow[]
}

/** بدنهٔ `POST /api/shipments/{id}/bales` (Floor)؛ بی شماره یعنی «بزرگ‌ترین + ۱»، شمارهٔ تکراری ۴۰۹. */
export interface CreateBaleRequest {
  number?: number | null
}

/** بدنهٔ `POST /api/shipments/{id}/bales/renumber` — نسخهٔ محموله. */
export interface RenumberBalesRequest {
  version: number
}

/** بدنهٔ `PUT /api/bales/{id}`: `number` null یعنی همان شماره؛ وزن و یادداشت جایگزین می‌شوند (null یعنی پاک). */
export interface UpdateBaleRequest {
  number: number | null
  weightKg: number | null
  notes: string | null
  version: number
}

// ── ردیف‌ها (یک درخواست، چند کار) ──────────────────────────────────────

/** نام کارها در `lines/batch`. */
export const LineOpKind = {
  Add: 'add',
  Move: 'move',
  Remove: 'remove',
  Measure: 'measure',
  Rate: 'rate',
  Order: 'order',
} as const
export type LineOpKind = (typeof LineOpKind)[keyof typeof LineOpKind]

/**
 * یک کار روی ردیف‌ها — آینهٔ `LineOp` (یک رکورد تخت؛ هر کار فقط عضوهای خودش را می‌خواند):
 * - `add`: `clientOpId`، `baleId` یا `baleNumber` (شمارهٔ تازه همان نگله را می‌سازد)، `rugId` یا `serial`؛ اختیاری `seq`
 *   (برای «برگردان» در همان جا) و `move: true` (تأیید انتقال فرشی که در نگلهٔ دیگری است).
 * - `move`: `lineId`، `lineVersion`، نگلهٔ مقصد، اختیاری `seq`.
 * - `remove`: `lineId`، `lineVersion`.
 * - `measure`: `rugId`، `rugVersion` و یکی از: `finalLength` + `finalWidth`، `sameAsIntake`، `clear`.
 * - `rate` (فقط Business): `lineId`، `lineVersion`، `usdPerM2Override` (null = برگشت به جدول).
 * - `order`: `baleId` و `lineIds` به ترتیب تازه.
 */
export interface LineOp {
  op: LineOpKind
  clientOpId?: string | null
  baleId?: string | null
  baleNumber?: number | null
  rugId?: string | null
  serial?: string | null
  lineId?: string | null
  lineVersion?: number | null
  seq?: number | null
  rugVersion?: number | null
  finalLength?: number | null
  finalWidth?: number | null
  sameAsIntake?: boolean | null
  clear?: boolean | null
  usdPerM2Override?: number | null
  lineIds?: string[] | null
  move?: boolean | null
}

/** بدنهٔ `POST /api/shipments/{id}/lines/batch`. */
export interface LinesBatchRequest {
  ops: LineOp[]
}

/** خطای یک کار: کد ثابت، پیام فارسی و خانهٔ درگیر اگر هست. */
export interface LineOpError {
  code: string
  message: string
  field: string | null
}

/**
 * پرسشی که «افزودن» پیش از انجام می‌پرسد (چیزی ذخیره نشده): `Q_LINE_MOVE` (فرش در نگلهٔ دیگری است؛ با `move: true`
 * دوباره بفرستید) یا `Q_LINE_SERIAL_IN_RANGE` (شماره داخل یک دسته است؛ «کل دسته را بگذار» با `rugId` دسته، یا «جدا کن»).
 */
export interface LineOpPrompt {
  code: string
  message: string
  rugId: string
  serial: string | null
  matchType: 'exact' | 'inRange' | string
  shipmentId: string | null
  shipmentNumber: number | null
  baleId: string | null
  baleNumber: number | null
  quantity: number
}

/**
 * نتیجهٔ یک کار. موفق: `row` ردیف تازه (برای «خارج کردن» همان ردیف پیش از خروج، تا «برگردان» نگله و جا را بداند؛ برای
 * «ترتیب» null). ناموفق: `errors`، یا `prompt` وقتی سرور پیش از انجام می‌پرسد.
 */
export interface LineOpResult {
  clientOpId: string | null
  ok: boolean
  errors: LineOpError[]
  row: BaleRow | null
  prompt: LineOpPrompt | null
  op: LineOpKind | null
}

/** پاسخ کارهای ردیف: نتیجهٔ هر کار به همان ترتیب، خلاصهٔ تازهٔ همهٔ نگله‌ها و جمع محموله. */
export interface LinesBatchResult {
  results: LineOpResult[]
  bales: BaleSummary[]
  totals: ShipmentTotals
}

/** فرش پیداشده کجاست. */
export interface FindRugInBale {
  shipmentId: string
  shipmentNo: number
  baleId: string
  baleNo: number
  lineId: string
  sameShipment: boolean
  status: ShipmentStatus
}

/** `GET /api/shipments/{id}/find-rug?serial=` — پیدا نشد → ۴۰۴. */
export interface FindRugResult {
  rug: RugListItem
  inBale: FindRugInBale | null
  matchType: 'exact' | 'inRange'
}

// ── ارزش گمرکی ─────────────────────────────────────────────────────────

/** یک نرخ جدول: مقدار کلید (مثلاً محل بافت «کاشان») → دلار هر متر. */
export interface DeclaredValueRate {
  /** null برای ردیف تازه */
  id: string | null
  keyValue: string
  usdPerM2: number
}

/** `GET /api/declared-values` (Floor) — جدول، مهر هم‌زمانی، فیلد کلید و نرخ پیش‌فرض کارگاه. */
export interface DeclaredValues {
  rates: DeclaredValueRate[]
  stamp: number
  /** کلید فیلدی که جدول بر اساسش نرخ می‌دهد (مثل `rug.origin`) */
  keyField: string | null
  /** نرخ پیش‌فرض برای مقدارهای بیرون از جدول؛ null یعنی ندارد */
  fallbackUsdPerM2: number | null
}

/** بدنهٔ `POST /api/declared-values` (Business): فقط کلیدِ تازه؛ کلید موجود ۴۰۹، نرخ منفی ۴۰۰، مهر کهنه ۴۰۹. */
export interface AddDeclaredValueRequest {
  keyValue: string
  usdPerM2: number
  stamp: number
}

/** بدنهٔ `PUT /api/settings/declared-values` (Configure): جایگزینی کل جدول؛ مهر کهنه ۴۰۹. */
export interface ReplaceDeclaredValuesRequest {
  rates: DeclaredValueRate[]
  stamp: number
}

/** پاسخ هر دو نوشتن: چند ردیفِ محموله‌های باز (بی نرخ دستی) دوباره ارزش‌گذاری شد، و مهر تازه. */
export interface DeclaredValuesWriteResult {
  affectedOpenLines: number
  stamp: number
}
