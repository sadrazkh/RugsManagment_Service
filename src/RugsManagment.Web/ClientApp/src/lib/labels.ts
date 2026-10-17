/**
 * برچسب‌های فارسی وضعیت‌ها، نقش‌ها و گزینه‌ها — یک جا برای همهٔ جزیره‌ها.
 *
 * چرا یک جا: «پیش‌نویس» پیش از این در چهار جزیره جدا نوشته شده بود و با تغییر واژه‌نامه (A.2) فقط
 * بعضی‌شان عوض می‌شد. آینهٔ سرور: `Web/Support/DisplayHelpers` (وضعیت فرش و مرحله) و
 * `App/Common/RoleLabels` (نقش). کلاس‌های رنگ همان توکن‌های Tailwind‌اند (هرگز رنگ ثابت).
 *
 * قاعدهٔ واژه‌نامه: «پیش‌نویس» فقط مال محموله و خروجی است؛ فرش تازه «ثبت‌شده» است. «در کارگاه» فقط در
 * حالت کامل دیده می‌شود (حالت ساده کارهای کارگاه را پنهان می‌کند و جای وضعیت، محل فرش را می‌گوید:
 * «در انبار»). «ورودی» نام گروه قدیمی است و «محموله» هرگز به آن گفته نمی‌شود.
 *
 * جایی که کاربر وضعیت را «انتخاب» می‌کند یا برچسب چاپ می‌شود، همیشه برچسب کامل (`simple = false`) به کار
 * برود؛ نمای ساده فقط برای نمایش است.
 */
import { appContext, isSimpleUi } from '@/lib/context'
import {
  AllocationMethod,
  AreaBasis,
  BaleTerm,
  GridEnterDirection,
  RugStatus,
  SerialMode,
  TextScale,
  UserRole,
  type RoleName,
} from '@/lib/types/platform'

export interface StatusView {
  label: string
  /** کلاس‌های چیپ (پس‌زمینه + رنگ نوشته)، سازگار با حالت تاریک */
  css: string
  /** نام آیکون در /icons.svg — وضعیت همیشه آیکون + نوشته است، نه فقط رنگ */
  icon: string
}

// ── وضعیت فرش ─────────────────────────────────────────────────────────

const RUG_STATUS: Record<number, StatusView> = {
  [RugStatus.Draft]: { label: 'ثبت‌شده', css: 'bg-surface-container-high text-on-surface-variant', icon: 'edit' },
  [RugStatus.InProgress]: { label: 'در کارگاه', css: 'bg-secondary-container text-on-secondary-container', icon: 'workflow' },
  [RugStatus.ReadyForSale]: { label: 'آمادهٔ فروش', css: 'bg-success/12 text-success', icon: 'success' },
  [RugStatus.Sold]: { label: 'فروخته‌شده', css: 'bg-primary/12 text-primary', icon: 'check' },
  [RugStatus.Archived]: { label: 'بایگانی', css: 'bg-surface-container-high text-on-surface-variant', icon: 'package' },
  [RugStatus.Exported]: { label: 'صادر شده', css: 'bg-tertiary/12 text-tertiary', icon: 'truck' },
}

const UNKNOWN_STATUS: StatusView = { label: 'نامشخص', css: 'bg-surface-container-high text-on-surface-variant', icon: 'info' }

/** «در انبار»: هر فرشی که فروخته، صادر یا بایگانی نشده (نمای ساده). */
const IN_STOCK: StatusView = { label: 'در انبار', css: 'bg-surface-container-high text-on-surface-variant', icon: 'warehouse' }

/**
 * برچسب، رنگ و آیکون وضعیت فرش — همان `DisplayHelpers.RugStatus(status, simple)` سرور.
 *
 * نمای ساده مرحله‌های کارگاه را نشان نمی‌دهد، فقط اینکه فرش کجاست: «فروخته‌شده»، «صادر شده» و «بایگانی» همان
 * می‌مانند و بقیه (ثبت‌شده، در کارگاه، آمادهٔ فروش) «در انبار» می‌شوند. تا وقتی محل واقعی فرش («در نگله»)
 * در فاز محموله بیاید، هر فرش فروخته‌نشده و صادرنشده در انبار است.
 */
export function rugStatus(status: number, simple: boolean = isSimpleUi()): StatusView {
  const view = RUG_STATUS[status]
  if (!view) return UNKNOWN_STATUS
  if (!simple) return view
  return status === RugStatus.Sold || status === RugStatus.Exported || status === RugStatus.Archived ? view : IN_STOCK
}

export function rugStatusLabel(status: number, simple?: boolean): string {
  return rugStatus(status, simple).label
}

/**
 * وضعیت‌هایی که دستی (ویرایش گروهی) گذاشتنی‌اند. «فروخته‌شده» را فقط ثبت فروش و «صادر شده» را فقط قطعی
 * شدن محموله می‌گذارد.
 */
export const MANUAL_RUG_STATUSES: readonly number[] = [
  RugStatus.Draft,
  RugStatus.InProgress,
  RugStatus.ReadyForSale,
  RugStatus.Archived,
]

// ── وضعیت مرحلهٔ کارگاه ───────────────────────────────────────────────

/** همان `DisplayHelpers.StepStatus` سرور (عدد = WorkflowStepStatus). */
const STEP_STATUS: Record<number, StatusView> = {
  0: { label: 'در صف', css: 'bg-surface-container-high text-on-surface-variant', icon: 'info' },
  1: { label: 'در حال انجام', css: 'bg-secondary-container text-on-secondary-container', icon: 'workflow' },
  2: { label: 'تکمیل‌شده', css: 'bg-success/12 text-success', icon: 'success' },
  3: { label: 'رد‌شده', css: 'bg-surface-container-high text-on-surface-variant line-through', icon: 'close' },
  4: { label: 'لغو‌شده', css: 'bg-error-container text-error', icon: 'error' },
}

export function stepStatus(status: number): StatusView {
  return STEP_STATUS[status] ?? UNKNOWN_STATUS
}

// ── نقش ───────────────────────────────────────────────────────────────

const ROLE_LABELS: Record<number, string> = {
  [UserRole.SystemAdmin]: 'مدیر سامانه',
  [UserRole.TenantAdmin]: 'تنظیم‌کننده (مدیر)',
  [UserRole.Merchant]: 'بازرگان',
  [UserRole.Operator]: 'کاربر / انباردار',
}

/** برچسب نقش از عدد یا نام («Merchant»)؛ ناشناخته «نقش ناشناخته». */
export function roleLabel(role: number | RoleName | string | null | undefined): string {
  if (typeof role === 'number') return ROLE_LABELS[role] ?? 'نقش ناشناخته'
  if (typeof role === 'string' && Object.prototype.hasOwnProperty.call(UserRole, role)) {
    return ROLE_LABELS[UserRole[role as RoleName]]
  }
  return 'نقش ناشناخته'
}

/** برچسب نقش کاربر جاری (از app-context). */
export function currentRoleLabel(): string {
  return roleLabel(appContext().role)
}

// ── واژه‌های ثابت ──────────────────────────────────────────────────────

/** نام گروه قدیمی (`RugBatch`) در همهٔ صفحه‌ها. */
export const BATCH_TERMS = {
  batch: 'ورودی',
  batches: 'ورودی‌ها',
  addTo: 'افزودن به ورودی',
  removeFrom: 'بیرون بردن از ورودی',
  newFromSelection: 'ورودی تازه از انتخاب‌شده‌ها',
  moveTo: 'انتقال به ورودی دیگر',
  none: 'بدون ورودی',
} as const

/** نحوهٔ پرداخت فروش (ترتیب = عدد enum). */
export const PAYMENT_METHOD_LABELS: readonly string[] = ['نقدی', 'کارت', 'حواله', 'چک', 'اقساطی']

export function paymentMethodLabel(method: number): string {
  return PAYMENT_METHOD_LABELS[method] ?? '—'
}

/** نوع فیلد سفارشی (ترتیب = عدد enum). */
export const CUSTOM_FIELD_TYPE_LABELS: Readonly<Record<number, string>> = {
  0: 'متن',
  1: 'عدد',
  2: 'تاریخ',
  3: 'انتخابی',
  4: 'بله/خیر',
}

// ── ترجیحات (صفحهٔ ترجیحات کارگاه و کنترل اندازهٔ نوشته) ───────────────

export const SERIAL_MODE_LABELS: Readonly<Record<number, { label: string; hint: string }>> = {
  [SerialMode.Manual]: { label: 'دستی', hint: 'شماره را خودتان می‌نویسید؛ چیزی پیشنهاد نمی‌شود.' },
  [SerialMode.SuggestNext]: { label: 'پیشنهاد شمارهٔ بعد', hint: 'شمارهٔ بعدی کم‌رنگ پیشنهاد می‌شود؛ با Enter پذیرفته و قابل تغییر است.' },
  [SerialMode.Auto]: { label: 'خودکار', hint: 'سرور هنگام ذخیره شماره می‌گذارد و نوشتهٔ دستی نادیده گرفته می‌شود.' },
}

export const BALE_TERM_LABELS: Readonly<Record<number, { label: string; example: string }>> = {
  [BaleTerm.Negleh]: { label: 'نگله', example: 'نگلهٔ ۳ · نگله شماره' },
  [BaleTerm.Adl]: { label: 'عدل', example: 'عدل ۳ · عدل شماره' },
}

export const ALLOCATION_METHOD_LABELS: Readonly<Record<number, string>> = {
  [AllocationMethod.ByArea]: 'به نسبت متراژ',
  [AllocationMethod.ByCount]: 'به تعداد تخته',
  [AllocationMethod.Equal]: 'مساوی برای هر ردیف',
  [AllocationMethod.ByDeclaredValue]: 'به نسبت ارزش گمرکی',
  [AllocationMethod.ByPurchaseCost]: 'به نسبت قیمت خرید',
  [AllocationMethod.Manual]: 'خودم سهم هر فرش را می‌نویسم',
}

export const AREA_BASIS_LABELS: Readonly<Record<number, string>> = {
  [AreaBasis.Intake]: 'اندازهٔ اول',
  [AreaBasis.Effective]: 'اندازهٔ نهایی (اگر نیست، اندازهٔ اول)',
}

/** دکمه‌های کنترل «اندازهٔ نوشته: [آ] [آ+] [آ++]». */
export const TEXT_SCALE_OPTIONS: readonly { value: TextScale; short: string; label: string }[] = [
  { value: TextScale.Normal, short: 'آ', label: 'معمولی' },
  { value: TextScale.Large, short: 'آ+', label: 'درشت' },
  { value: TextScale.XLarge, short: 'آ++', label: 'خیلی درشت' },
]

export const GRID_ENTER_LABELS: Readonly<Record<number, string>> = {
  [GridEnterDirection.Down]: 'Enter به خانهٔ پایین می‌رود (مثل اکسل)',
  [GridEnterDirection.Right]: 'Enter به خانهٔ بعدیِ همان ردیف می‌رود',
}

/** کلیدهای فیلدی که ارزش گمرکی بر اساسش خوانده می‌شود (بدون فیلدهای سفارشی). */
export const DECLARED_VALUE_KEY_FIELDS: readonly { key: string; label: string }[] = [
  { key: 'rug.origin', label: 'محل بافت' },
  { key: 'rug.pattern', label: 'نقشه' },
  { key: 'rug.material', label: 'مواد مصرفی' },
]
