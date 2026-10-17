/**
 * app-context: آنچه هر جزیره دربارهٔ بینندهٔ جاری و کارگاه می‌داند، بی‌آنکه در props تکرار شود.
 *
 * layout (`_Layout.cshtml`) این JSON را با `@Json.Serialize(...)` داخل
 * `<script type="application/json" id="app-context">` می‌نویسد. این داده است نه کد، پس با CSP مشکلی ندارد
 * و nonce نمی‌خواهد. شکل دقیق (کلیدها camelCase؛ همهٔ کلیدها همیشه حاضرند):
 *
 * ```json
 * {
 *   "tenantId": "3f2c…" | null,            // null برای مدیر سامانه / صفحهٔ بی‌کاربر
 *   "userId": "9a1b…" | null,
 *   "role": "Merchant",                     // "SystemAdmin" | "TenantAdmin" | "Merchant" | "Operator" | null
 *   "policies": { "floor": true, "business": true, "configure": false },
 *   "moneyVisible": true,                   // = policies.business؛ مبلغ تومانی دیده/نوشته شود یا نه
 *   "uiMode": "simple",                     // "simple" | "full" — حالت ذخیره‌شدهٔ کاربر
 *   "previewSimple": false,                 // پیش‌نمایش «نمای ساده»ی تنظیم‌کننده (فقط پوسته)
 *   "textScale": "large",                   // "normal" | "large" | "xlarge"
 *   "gridEnter": "down",                    // "down" | "right"
 *   "currency": { "code": "IRT", "label": "تومان", "scale": 0 },   // code: "IRT" (تومان) | "IRR" (ریال)
 *   "moneyScale": 0,                        // = currency.scale (۰ تا ۲)
 *   "terms": { "bale": "نگله", "bales": "نگله‌ها", "baleOf": "نگلهٔ", "baleFooter": "نگله شماره" },
 *   "today": "2026-09-06",                  // تاریخ میلادی امروز به وقت ایران، ISO
 *   "todayJalali": "1405/06/15",            // برای نمایش (ارقام لاتین؛ نمایش فارسی‌شان می‌کند)
 *   "commands": [ { "label": "ثبت فرش تازه", "href": "/Intake", "icon": "plus" } ],
 *   "maxPlausibleSideM": 12                 // ضلع بزرگ‌تر از این «متر یا سانت؟» می‌پرسد (TenantPreferences)
 * }
 * ```
 *
 * با «عدل»: `{ "bale": "عدل", "bales": "عدل‌ها", "baleOf": "عدل", "baleFooter": "عدل شماره" }`.
 *
 * ویژگی‌های ریشه: layout روی `<html>` هم `data-ui="simple|full"` (حالت مؤثر: ساده یا پیش‌نمایش ساده) و
 * `data-text-scale="normal|large|xlarge"` می‌گذارد؛ `updateAppContext` همین دو را همگام نگه می‌دارد.
 *
 * نبودِ عنصر (صفحه‌های بی‌layout، آزمون‌ها) یا JSON خراب خطا نمی‌دهد: هر کلید جداگانه با پیش‌فرضِ
 * کم‌دسترسی پر می‌شود (بدون پول، بدون سیاست). دسترسی واقعی همیشه روی سرور سنجیده می‌شود؛ این فقط برای
 * پنهان کردن کنترل‌هایی است که بیننده نمی‌تواند به کار ببرد.
 */
import { reactive, readonly } from 'vue'
import {
  GRID_ENTER_NAMES,
  ROLE_NAMES,
  TEXT_SCALE_NAMES,
  UI_MODE_NAMES,
  type GridEnterName,
  type RoleName,
  type TextScaleName,
  type UiModeName,
} from '@/lib/types/platform'

export interface AppPolicies {
  floor: boolean
  business: boolean
  configure: boolean
}

export interface AppCurrency {
  /** «IRT» تومان، «IRR» ریال */
  code: 'IRT' | 'IRR'
  /** «تومان» یا «ریال» — هرگز ثابت ننویسید، همیشه از این بخوانید */
  label: string
  /** تعداد رقم اعشار مبلغ‌ها */
  scale: number
}

/** واژه‌های بستهٔ فرش کارگاه (نشانه‌های `{bale}`، `{bales}`، `{baleOf}`، `{baleFooter}` پیام‌ها). */
export interface AppTerms {
  bale: string
  bales: string
  baleOf: string
  baleFooter: string
}

export interface AppCommand {
  label: string
  href: string
  icon: string
}

export interface AppContext {
  tenantId: string | null
  userId: string | null
  role: RoleName | null
  policies: AppPolicies
  moneyVisible: boolean
  uiMode: UiModeName
  previewSimple: boolean
  textScale: TextScaleName
  gridEnter: GridEnterName
  currency: AppCurrency
  moneyScale: number
  terms: AppTerms
  today: string
  todayJalali: string
  commands: AppCommand[]
  /**
   * «بزرگ‌ترین ضلع معمول» کارگاه (TenantPreferences.MaxPlausibleSideM، ۱ تا ۳۰ متر): ضلع عدد صحیح بزرگ‌تر از این
   * تا ۳۰ «متر یا سانت؟» پرسیده می‌شود. خانه‌های اندازه و جدول همین را می‌خوانند، نه عدد ثابت ۱۲.
   */
  maxPlausibleSideM: number
}

export const APP_CONTEXT_ELEMENT_ID = 'app-context'

const NEGLEH_TERMS: AppTerms = { bale: 'نگله', bales: 'نگله‌ها', baleOf: 'نگلهٔ', baleFooter: 'نگله شماره' }

/** پیش‌فرض و بازهٔ «بزرگ‌ترین ضلع معمول» (همان TenantPreferences.DefaultMaxPlausibleSideM و بازهٔ سرور). */
export const DEFAULT_MAX_PLAUSIBLE_SIDE_M = 12
const MIN_MAX_PLAUSIBLE_SIDE_M = 1
const MAX_MAX_PLAUSIBLE_SIDE_M = 30

/** امروز به وقت ایران، ISO — فقط وقتی layout تاریخ نداده باشد. */
function iranToday(): string {
  try {
    // en-CA تاریخ را yyyy-MM-dd می‌نویسد
    return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Tehran' }).format(new Date())
  } catch {
    return new Date().toISOString().slice(0, 10)
  }
}

function defaults(): AppContext {
  const today = iranToday()
  return {
    tenantId: null,
    userId: null,
    role: null,
    policies: { floor: false, business: false, configure: false },
    moneyVisible: false,
    uiMode: 'full',
    previewSimple: false,
    textScale: 'normal',
    gridEnter: 'down',
    currency: { code: 'IRT', label: 'تومان', scale: 0 },
    moneyScale: 0,
    terms: { ...NEGLEH_TERMS },
    today,
    todayJalali: '',
    commands: [],
    maxPlausibleSideM: DEFAULT_MAX_PLAUSIBLE_SIDE_M,
  }
}

// ── خواندن امن هر کلید ──────────────────────────────────────────────────

type Json = Record<string, unknown>

function isObject(value: unknown): value is Json {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function str(value: unknown, fallback: string): string {
  return typeof value === 'string' ? value : fallback
}

function strOrNull(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null
}

function bool(value: unknown, fallback: boolean): boolean {
  return typeof value === 'boolean' ? value : fallback
}

function int(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isInteger(value) ? value : fallback
}

/** عدد متناهی در بازه؛ وگرنه پیش‌فرض (سرور «12.00» را عدد JSON می‌نویسد). */
function numberIn(value: unknown, min: number, max: number, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) && value >= min && value <= max ? value : fallback
}

function oneOf<T extends string>(value: unknown, allowed: readonly T[], fallback: T): T {
  return typeof value === 'string' && (allowed as readonly string[]).includes(value) ? (value as T) : fallback
}

function parse(raw: unknown): AppContext {
  const base = defaults()
  if (!isObject(raw)) return base

  const policies = isObject(raw.policies) ? raw.policies : {}
  const currency = isObject(raw.currency) ? raw.currency : {}
  const terms = isObject(raw.terms) ? raw.terms : {}
  const commands = Array.isArray(raw.commands) ? raw.commands : []
  const role = typeof raw.role === 'string' && (ROLE_NAMES as readonly string[]).includes(raw.role)
    ? (raw.role as RoleName)
    : null

  return {
    tenantId: strOrNull(raw.tenantId),
    userId: strOrNull(raw.userId),
    role,
    policies: {
      floor: bool(policies.floor, false),
      business: bool(policies.business, false),
      configure: bool(policies.configure, false),
    },
    moneyVisible: bool(raw.moneyVisible, false),
    uiMode: oneOf(raw.uiMode, UI_MODE_NAMES, base.uiMode),
    previewSimple: bool(raw.previewSimple, false),
    textScale: oneOf(raw.textScale, TEXT_SCALE_NAMES, base.textScale),
    gridEnter: oneOf(raw.gridEnter, GRID_ENTER_NAMES, base.gridEnter),
    currency: {
      code: oneOf(currency.code, ['IRT', 'IRR'] as const, base.currency.code),
      label: str(currency.label, base.currency.label),
      scale: int(currency.scale, base.currency.scale),
    },
    moneyScale: int(raw.moneyScale, int(currency.scale, base.moneyScale)),
    terms: {
      bale: str(terms.bale, NEGLEH_TERMS.bale),
      bales: str(terms.bales, NEGLEH_TERMS.bales),
      baleOf: str(terms.baleOf, NEGLEH_TERMS.baleOf),
      baleFooter: str(terms.baleFooter, NEGLEH_TERMS.baleFooter),
    },
    today: str(raw.today, base.today),
    todayJalali: str(raw.todayJalali, base.todayJalali),
    commands: commands
      .filter(isObject)
      .filter((c) => typeof c.label === 'string' && typeof c.href === 'string')
      .map((c) => ({ label: c.label as string, href: c.href as string, icon: str(c.icon, 'arrow-left') })),
    maxPlausibleSideM: numberIn(
      raw.maxPlausibleSideM,
      MIN_MAX_PLAUSIBLE_SIDE_M,
      MAX_MAX_PLAUSIBLE_SIDE_M,
      base.maxPlausibleSideM,
    ),
  }
}

function readRaw(): unknown {
  if (typeof document === 'undefined') return null
  const text = document.getElementById(APP_CONTEXT_ELEMENT_ID)?.textContent
  if (!text) return null
  try {
    return JSON.parse(text)
  } catch (err) {
    console.warn('[app-context] JSON نامعتبر؛ پیش‌فرض‌ها به کار می‌روند', err)
    return null
  }
}

// ── یک نمونهٔ مشترک و واکنش‌گرا برای همهٔ جزیره‌ها ──────────────────────

let state: AppContext | null = null

function ensure(): AppContext {
  if (state === null) state = reactive(parse(readRaw())) as AppContext
  return state
}

/**
 * app-context صفحه (یک بار خوانده و برای همهٔ جزیره‌ها مشترک). واکنش‌گراست: اگر کنترل اندازهٔ نوشته
 * با `updateAppContext` چیزی را عوض کند، جزیره‌های دیگر هم همان لحظه می‌بینند. فقط خواندنی؛ برای تغییر
 * از `updateAppContext` استفاده کنید.
 */
export function appContext(): Readonly<AppContext> {
  return readonly(ensure()) as Readonly<AppContext>
}

/** حالت مؤثر پوسته: ساده یا پیش‌نمایش «نمای ساده»ی تنظیم‌کننده. */
export function isSimpleUi(ctx: Readonly<AppContext> = appContext()): boolean {
  return ctx.uiMode === 'simple' || ctx.previewSimple
}

/** آیا مبلغ تومانی نشان داده و نوشته شود (= سیاست Business). */
export function moneyVisible(ctx: Readonly<AppContext> = appContext()): boolean {
  return ctx.moneyVisible
}

/**
 * تغییر ترجیحات همین نشست پس از ذخیرهٔ موفق در سرور (مثلاً اندازهٔ نوشته): مقدار مشترک را عوض می‌کند و
 * `data-ui` / `data-text-scale` روی `<html>` را همگام می‌کند تا اندازه همان لحظه عوض شود.
 */
export function updateAppContext(
  patch: Partial<Pick<AppContext, 'uiMode' | 'previewSimple' | 'textScale' | 'gridEnter'>>,
): void {
  const ctx = ensure()
  Object.assign(ctx, patch)
  if (typeof document === 'undefined') return
  const root = document.documentElement
  root.dataset.ui = isSimpleUi(ctx) ? 'simple' : 'full'
  root.dataset.textScale = ctx.textScale
}

/** فقط برای آزمون‌ها: خواندن دوبارهٔ app-context از DOM (یا از شیء داده‌شده). */
export function resetAppContext(raw?: unknown): void {
  state = raw === undefined ? null : (reactive(parse(raw)) as AppContext)
}
