/**
 * نوع‌های فاز سکو — آینهٔ دستی DTOهای `Application/Features/Platform/DTOs` و enumهای Domain.
 *
 * enumها در JSON عدد می‌آیند (سرور JsonStringEnumConverter ندارد)؛ هر enum این‌جا یک شیء ثابت
 * (برای مقدار) و یک نوع هم‌نام (برای عدد مجاز) دارد: `TextScale.Large` و `type TextScale = 0 | 1 | 2`.
 * عددها با enumهای C# یکی‌اند و فقط افزودنی‌اند؛ هرگز جابه‌جا نکنید.
 *
 * فقط app-context (`lib/context.ts`) حالت، اندازهٔ نوشته و جهت Enter را با نام متنی می‌دهد
 * («simple»، «large»، «down»)؛ تبدیل نام ↔ عدد با تابع‌های پایین همین فایل است.
 */

// ── enumها ─────────────────────────────────────────────────────────────

export const UserRole = { SystemAdmin: 0, TenantAdmin: 1, Operator: 2, Merchant: 3 } as const
export type UserRole = (typeof UserRole)[keyof typeof UserRole]

/** وضعیت فرش. `Exported` («صادر شده») را فقط قطعی شدن محموله می‌گذارد. */
export const RugStatus = { Draft: 0, InProgress: 1, ReadyForSale: 2, Sold: 3, Archived: 4, Exported: 5 } as const
export type RugStatus = (typeof RugStatus)[keyof typeof RugStatus]

/** واحد پول. `Usd` هرگز واحد پول کارگاه نیست. */
export const CurrencyUnit = { Toman: 0, Rial: 1, Usd: 2 } as const
export type CurrencyUnit = (typeof CurrencyUnit)[keyof typeof CurrencyUnit]

export const UiMode = { Simple: 0, Full: 1 } as const
export type UiMode = (typeof UiMode)[keyof typeof UiMode]

export const TextScale = { Normal: 0, Large: 1, XLarge: 2 } as const
export type TextScale = (typeof TextScale)[keyof typeof TextScale]

export const GridEnterDirection = { Down: 0, Right: 1 } as const
export type GridEnterDirection = (typeof GridEnterDirection)[keyof typeof GridEnterDirection]

export const BaleTerm = { Negleh: 0, Adl: 1 } as const
export type BaleTerm = (typeof BaleTerm)[keyof typeof BaleTerm]

export const SerialMode = { Manual: 0, SuggestNext: 1, Auto: 2 } as const
export type SerialMode = (typeof SerialMode)[keyof typeof SerialMode]

export const AllocationMethod = {
  ByArea: 0,
  ByCount: 1,
  Equal: 2,
  ByDeclaredValue: 3,
  ByPurchaseCost: 4,
  Manual: 5,
} as const
export type AllocationMethod = (typeof AllocationMethod)[keyof typeof AllocationMethod]

export const AreaBasis = { Intake: 0, Effective: 1 } as const
export type AreaBasis = (typeof AreaBasis)[keyof typeof AreaBasis]

// ── نام‌های متنی app-context ↔ عدد API ──────────────────────────────────

/** نام‌ها به ترتیب عدد enum: اندیس آرایه همان عدد است. */
export const UI_MODE_NAMES = ['simple', 'full'] as const
export type UiModeName = (typeof UI_MODE_NAMES)[number]

export const TEXT_SCALE_NAMES = ['normal', 'large', 'xlarge'] as const
export type TextScaleName = (typeof TEXT_SCALE_NAMES)[number]

export const GRID_ENTER_NAMES = ['down', 'right'] as const
export type GridEnterName = (typeof GRID_ENTER_NAMES)[number]

export const ROLE_NAMES = ['SystemAdmin', 'TenantAdmin', 'Operator', 'Merchant'] as const
export type RoleName = (typeof ROLE_NAMES)[number]

export function uiModeName(value: UiMode): UiModeName {
  return UI_MODE_NAMES[value]
}
export function uiModeValue(name: UiModeName): UiMode {
  return UI_MODE_NAMES.indexOf(name) as UiMode
}
export function textScaleName(value: TextScale): TextScaleName {
  return TEXT_SCALE_NAMES[value]
}
export function textScaleValue(name: TextScaleName): TextScale {
  return TEXT_SCALE_NAMES.indexOf(name) as TextScale
}
export function gridEnterName(value: GridEnterDirection): GridEnterName {
  return GRID_ENTER_NAMES[value]
}
export function gridEnterValue(name: GridEnterName): GridEnterDirection {
  return GRID_ENTER_NAMES.indexOf(name) as GridEnterDirection
}

// ── خطای API ───────────────────────────────────────────────────────────

/**
 * بدنهٔ پاسخ خطای API (`ApiExceptionFilter`): همیشه `message` فارسی؛ ۴۰۰ اعتبارسنجی `errors` (نام فیلد
 * camelCase → پیام‌ها)؛ ۴۰۹ تعارض `field` و شاید کلیدهای اضافه (مثل `nextFree`).
 */
export interface ApiErrorBody {
  message: string
  errors?: Record<string, string[]>
  field?: string | null
  [extension: string]: unknown
}

// ── ترجیحات ─────────────────────────────────────────────────────────────

/** GET/PUT `/api/settings/preferences` (فقط Configure). در PUT همان `version` خوانده‌شده را برگردانید. */
export interface TenantPreferences {
  skuPrefix: string
  serialMode: SerialMode
  serialStart: number | null
  /** تعداد رقم اعشار مبلغ‌ها (۰ تا ۲) */
  moneyScale: number
  defaultUsdFx: number | null
  defaultMethod: AllocationMethod
  defaultAreaBasis: AreaBasis
  declaredValueKeyField: string
  declaredValueFallbackUsd: number | null
  defaultBaleTargetAreaM2: number | null
  baleTargetTolerancePercent: number
  requireFinalSizeToFinalize: boolean
  baleTerm: BaleTerm
  defaultDeclarationTemplateId: string | null
  exporterName: string | null
  exporterCode: string | null
  exporterAddress: string | null
  exporterPhone: string | null
  customsOffice: string | null
  /** فقط خواندنی: مبلغی ثبت شده و moneyScale دیگر عوض نمی‌شود */
  moneyScaleLocked: boolean
  /** xmin ردیف */
  version: number
  /** ضلع بزرگ‌تر از این (متر) پرسش «متر یا سانت؟» می‌آورد */
  maxPlausibleSideM: number
}

export interface UserPreferences {
  uiMode: UiMode
  textScale: TextScale
  gridEnter: GridEnterDirection
  /** کلید راهنماهای شروعی که کاربر بسته است */
  coachDismissed: string[]
}

/**
 * بدنهٔ PUT `/api/me/preferences` (پاسخ ۲۰۴). هر عضو نفرستاده یا null یعنی «دست نزن».
 * `uiMode` فقط از تنظیم‌کننده پذیرفته می‌شود.
 */
export interface UpdateMyPreferencesRequest {
  textScale?: TextScale | null
  gridEnter?: GridEnterDirection | null
  uiMode?: UiMode | null
  /** کلید راهنمای شروعی که همین حالا بسته شد */
  dismissCoach?: string | null
}

/** GET `/api/me` */
export interface Me {
  userId: string
  tenantId: string | null
  tenantName: string | null
  fullName: string
  email: string
  role: UserRole
  roleLabel: string
  preferences: UserPreferences
}

// ── خانه ───────────────────────────────────────────────────────────────

/** یک رویداد تاریخچه — آینهٔ ActivityEntryDto. */
export interface ActivityEntry {
  id: string
  userName: string
  /** عدد AuditAction */
  action: number
  actionLabel: string
  entityType: string
  entityId: string | null
  entityLabel: string | null
  summary: string
  createdAt: string
}

/** یک محمولهٔ باز برای کاشی‌های خانه. دلار گمرکی برای همه دیده می‌شود. */
export interface HomeShipment {
  id: string
  number: number
  title: string
  /** تاریخ اظهار، ISO `yyyy-MM-dd` */
  documentDate: string
  bales: number
  lines: number
  /** تعداد تخته‌ها */
  pieces: number
  areaM2: number
  usdTotal: number
  readinessBlocking: number
  lastActivityAt: string | null
}

export interface HomeReadiness {
  code: string
  message: string
  count: number
  blocking: boolean
}

/** GET `/api/home/summary` (Floor). عضوهای پولی برای غیر Business null است. */
export interface HomeSummary {
  /** همهٔ محموله‌های باز، تازه‌ترین اول؛ تا فاز محموله همیشه خالی */
  openShipments: HomeShipment[]
  /** آمادگی اولین محمولهٔ باز */
  readiness: HomeReadiness[]
  stockPieces: number
  stockAreaM2: number
  openShipmentCostPerM2: number | null
  unallocatedEntries: number | null
  recent: ActivityEntry[]
}
