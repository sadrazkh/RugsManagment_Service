/**
 * سرویس رابط کاربری سراسری — Toast، دیالوگ تأیید، میزبان پنل‌ها، راهنما، اندازهٔ نوشته و قفل تازه‌سازی.
 *
 * جایگزین alert()/confirm() بومی مرورگر که سبک ندارند، RTL نمی‌شوند،
 * ترد اصلی را قفل می‌کنند و روی موبایل زشت‌اند.
 *
 * چون هر جزیره (island) یک اپ Vue مستقل است، وضعیت در ماژول سطح‌بالا نگه‌داری می‌شود
 * تا همهٔ جزیره‌ها و حتی کد ساده در Razor (از راه `window.rugsUI` و site.js) به همین صف مشترک دسترسی داشته باشند.
 * پوستهٔ UiShell همهٔ این‌ها را رندر می‌کند؛ این فایل فقط وضعیت و فرمان است.
 */
import { markRaw, reactive, ref, shallowReactive, type Component } from 'vue'
import { appContext, updateAppContext } from './context'
import { TEXT_SCALE_NAMES, textScaleValue, type TextScaleName } from './types/platform'

// ═════════════════════════════════════════════════════════════════════
// Toast
// ═════════════════════════════════════════════════════════════════════

/** سطح پیام. همان چهار مقداری که سرور در TempData["ToastLevel"] می‌گذارد. */
export type ToastKind = 'success' | 'error' | 'warning' | 'info'
export type ToastLevel = ToastKind

export const TOAST_LEVELS: readonly ToastKind[] = ['success', 'error', 'warning', 'info']

/** آیا مقدار یک سطح معتبر پیام است (برای ورودی‌های بیرونی مثل data-server-toast-kind). */
export function isToastLevel(value: unknown): value is ToastKind {
  return typeof value === 'string' && (TOAST_LEVELS as readonly string[]).includes(value)
}

export interface Toast {
  id: number
  kind: ToastKind
  message: string
}

export interface ConfirmOptions {
  title: string
  message?: string
  confirmLabel?: string
  cancelLabel?: string
  /** ظاهر خطرناک (قرمز) برای عملیات حذف */
  danger?: boolean
}

interface ConfirmState extends ConfirmOptions {
  open: boolean
  resolve: ((value: boolean) => void) | null
}

let nextId = 1

export const toasts = reactive<Toast[]>([])

export const confirmState = reactive<ConfirmState>({
  open: false,
  title: '',
  message: '',
  confirmLabel: 'تأیید',
  cancelLabel: 'انصراف',
  danger: false,
  resolve: null,
})

/** مدت نمایش: پیام خطا بیشتر می‌ماند چون کاربر باید بخواند و تصمیم بگیرد. */
function durationFor(kind: ToastKind): number {
  return kind === 'error' ? 8000 : kind === 'warning' ? 6000 : 4000
}

export function dismissToast(id: number): void {
  const index = toasts.findIndex((t) => t.id === id)
  if (index !== -1) toasts.splice(index, 1)
}

export function showToast(message: string, kind: ToastKind = 'info'): number {
  const id = nextId++
  toasts.push({ id, kind: isToastLevel(kind) ? kind : 'info', message })
  // globalThis نه window: در آزمون‌های بی‌DOM (vitest با محیط node) هم کار کند
  globalThis.setTimeout(() => dismissToast(id), durationFor(kind))
  return id
}

export const toast = {
  success: (message: string) => showToast(message, 'success'),
  error: (message: string) => showToast(message, 'error'),
  warning: (message: string) => showToast(message, 'warning'),
  info: (message: string) => showToast(message, 'info'),
  /** با سطح متغیر (مثلاً از سرور)؛ سطح ناشناخته «info» می‌شود. */
  show: (message: string, level: string = 'info') => showToast(message, isToastLevel(level) ? level : 'info'),
}

const PENDING_TOAST_KEY = 'rugs-pending-toast'

/**
 * Toastی که باید *بعد از* بارگذاری مجدد صفحه دیده شود.
 *
 * چرا لازم است: بعد از یک عملیات موفق معمولاً صفحه reload می‌شود تا دادهٔ تازه بیاید؛
 * اگر Toast را قبل از reload نشان دهیم کاربر هرگز آن را نمی‌بیند. این تابع پیام را
 * در sessionStorage می‌گذارد و پوستهٔ UI بعد از بارگذاری آن را نمایش می‌دهد.
 */
export function toastAfterReload(message: string, kind: ToastKind = 'success'): void {
  try {
    sessionStorage.setItem(PENDING_TOAST_KEY, JSON.stringify({ message, kind }))
  } catch {
    /* حالت خصوصی مرورگر — پیام از دست می‌رود ولی عملیات انجام شده است */
  }
}

/** پیام معلق را (اگر هست) نشان داده و پاک می‌کند. پوستهٔ UI هنگام mount صدا می‌زند. */
export function drainPendingToast(): void {
  try {
    const raw = sessionStorage.getItem(PENDING_TOAST_KEY)
    if (!raw) return
    sessionStorage.removeItem(PENDING_TOAST_KEY)
    const { message, kind } = JSON.parse(raw) as { message: string; kind: ToastKind }
    if (typeof message === 'string' && message) showToast(message, isToastLevel(kind) ? kind : 'success')
  } catch {
    /* محتوای نامعتبر — نادیده */
  }
}

// ═════════════════════════════════════════════════════════════════════
// دیالوگ تأیید
// ═════════════════════════════════════════════════════════════════════

/**
 * دیالوگ تأیید — Promise برمی‌گرداند تا دقیقاً مثل confirm() استفاده شود:
 *   if (!(await confirmDialog({ title: 'حذف شود؟' }))) return
 */
export function confirmDialog(options: ConfirmOptions): Promise<boolean> {
  // دیالوگ قبلیِ بی‌جواب «نه» حساب می‌شود تا Promiseاش آویزان نماند
  confirmState.resolve?.(false)
  return new Promise((resolve) => {
    confirmState.open = true
    confirmState.title = options.title
    confirmState.message = options.message ?? ''
    confirmState.confirmLabel = options.confirmLabel ?? 'تأیید'
    confirmState.cancelLabel = options.cancelLabel ?? 'انصراف'
    confirmState.danger = options.danger ?? false
    confirmState.resolve = resolve
  })
}

export function settleConfirm(result: boolean): void {
  confirmState.resolve?.(result)
  confirmState.open = false
  confirmState.resolve = null
}

// ═════════════════════════════════════════════════════════════════════
// فوکوس (مشترک دیالوگ، پنل و هر Modal/Drawer دیگر)
// ═════════════════════════════════════════════════════════════════════

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), ' +
  'textarea:not([disabled]), [tabindex]:not([tabindex="-1"]), [contenteditable="true"]'

/** عنصرهای فوکوس‌پذیرِ دیده‌شدنی داخل یک ظرف، به ترتیب سند. */
export function focusableElements(container: HTMLElement): HTMLElement[] {
  return [...container.querySelectorAll<HTMLElement>(FOCUSABLE)].filter(
    (el) => el.offsetParent !== null || el === document.activeElement,
  )
}

/** حبس Tab داخل ظرف (focus trap) — در keydown ظرف صدا بزنید. */
export function trapFocus(event: KeyboardEvent, container: HTMLElement): void {
  if (event.key !== 'Tab') return
  const items = focusableElements(container)
  if (items.length === 0) {
    event.preventDefault()
    container.focus()
    return
  }

  const first = items[0]
  const last = items[items.length - 1]
  const active = document.activeElement

  if (event.shiftKey && (active === first || !container.contains(active))) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && (active === last || !container.contains(active))) {
    event.preventDefault()
    first.focus()
  }
}

// ═════════════════════════════════════════════════════════════════════
// قفل تازه‌سازی (blockReload)
// ═════════════════════════════════════════════════════════════════════
//
// چرا: پوسته پس از ارسال صف آفلاین صفحه را خودکار تازه می‌کند تا دادهٔ تازه بیاید. اگر همان لحظه
// جدولی نوشتهٔ ذخیره‌نشده دارد یا پنل هزینه منتظر پاسخ سرور است، آن تازه‌سازی کار کاربر را می‌برد.
// هر بخشی که کارِ در جریان دارد blockReload() را صدا می‌زند و تابعی که برمی‌گردد را بعد از تمام شدن
// کار صدا می‌زند. تا قفلی باز است، تازه‌سازی خودکار انجام نمی‌شود و ترک صفحه هشدار مرورگر را می‌دهد.

const reloadBlockers = new Map<number, string>()
let nextBlockId = 1

/** آیا الان کاری در جریان است که نباید با تازه‌سازی صفحه از دست برود (واکنش‌گرا). */
export const reloadBlocked = ref(false)

function onBeforeUnload(event: BeforeUnloadEvent): void {
  event.preventDefault()
  // مرورگرهای قدیمی‌تر فقط با returnValue هشدار می‌دهند
  event.returnValue = ''
}

function syncReloadGuard(): void {
  const blocked = reloadBlockers.size > 0
  if (blocked === reloadBlocked.value) return
  reloadBlocked.value = blocked
  if (typeof window === 'undefined') return
  if (blocked) window.addEventListener('beforeunload', onBeforeUnload)
  else window.removeEventListener('beforeunload', onBeforeUnload)
}

/**
 * تا وقتی تابع برگشتی صدا زده نشده، صفحه خودکار تازه نمی‌شود و ترک صفحه هشدار می‌دهد.
 *   const release = blockReload('ذخیرهٔ هزینه'); try { await save() } finally { release() }
 * صدا زدن دوبارهٔ تابع برگشتی بی‌اثر است.
 */
export function blockReload(reason = 'save'): () => void {
  const id = nextBlockId++
  reloadBlockers.set(id, reason)
  syncReloadGuard()
  let released = false
  return () => {
    if (released) return
    released = true
    reloadBlockers.delete(id)
    syncReloadGuard()
  }
}

/** دلیل قفل‌های باز (برای عیب‌یابی). */
export function reloadBlockReasons(): string[] {
  return [...reloadBlockers.values()]
}

/**
 * تازه‌سازی خودکار صفحه، مگر کاری در جریان باشد. در آن حالت صفحه دست نمی‌خورد و یک پیام می‌گوید
 * بعداً تازه کنید. خروجی: آیا تازه‌سازی زمان‌بندی شد.
 */
export function requestReload(options: { delayMs?: number } = {}): boolean {
  if (reloadBlocked.value) {
    toast.info('تغییرهای تازه رسید؛ بعد از ذخیرهٔ کارتان صفحه را تازه کنید.')
    return false
  }
  window.setTimeout(() => {
    // اگر در این فاصله کاری شروع شد، باز هم تازه نکن
    if (!reloadBlocked.value) window.location.reload()
  }, options.delayMs ?? 0)
  return true
}

// ═════════════════════════════════════════════════════════════════════
// میزبان پنل‌ها (panels/*.panel.vue)
// ═════════════════════════════════════════════════════════════════════
//
// پنل یک کشوی کناری (روی گوشی برگهٔ پایین) است که بی‌رفتن از صفحه باز می‌شود، مثل «ثبت هزینه». هر فاز
// فقط یک فایل panels/Name.panel.vue اضافه می‌کند؛ نامش kebab-case همان فایل است («cost-entry»). Razor با
// <button data-open-panel="cost-entry" data-panel-props='{…}'> بازش می‌کند (site.js، بدون اسکریپت درون‌خطی)
// و جزیره‌ها با openPanel('cost-entry', {…}).
//
// قرارداد کامپوننت پنل: propsها همان شیء داده‌شده‌اند؛ برای بستن emit('close', نتیجه?) — Promise
// openPanel با همان نتیجه حل می‌شود (بستن با Esc، پس‌زمینه یا دکمهٔ ✕ یعنی undefined). اولین h1/h2/h3
// پنل عنوان دیالوگ (aria-labelledby) می‌شود و اولین [autofocus] یا [data-autofocus] فوکوس می‌گیرد.

interface PanelModule {
  default: Component
}

type PanelLoader = () => Promise<PanelModule>

/** نام پنل از مسیر فایل: «../panels/CostEntry.panel.vue» → «cost-entry». */
export function panelNameFromPath(path: string): string {
  const file = path.split('/').pop() ?? path
  const stem = file.replace(/\.panel\.vue$/, '')
  return toKebab(stem)
}

/** «CostEntry» → «cost-entry»، «QRScanner» → «qr-scanner». */
export function toKebab(name: string): string {
  return name
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1-$2')
    .replace(/[\s_]+/g, '-')
    .toLowerCase()
}

const panelLoaders: Readonly<Record<string, PanelLoader>> = Object.freeze(
  Object.fromEntries(
    Object.entries(import.meta.glob<PanelModule>('../panels/*.panel.vue')).map(([path, loader]) => [
      panelNameFromPath(path),
      loader,
    ]),
  ),
)

/** نام همهٔ پنل‌های موجود (مرتب). */
export function panelNames(): string[] {
  return Object.keys(panelLoaders).sort()
}

/** آیا پنلی با این نام هست. */
export function hasPanel(name: string): boolean {
  return Object.prototype.hasOwnProperty.call(panelLoaders, name)
}

export interface PanelState {
  /** نام پنل باز، یا null */
  name: string | null
  /** کامپوننت بارشده (markRaw) */
  component: Component | null
  props: Record<string, unknown>
  /** chunk پنل در حال دانلود است */
  loading: boolean
}

export const panelState = shallowReactive<PanelState>({
  name: null,
  component: null,
  props: {},
  loading: false,
})

let panelResolve: ((value: unknown) => void) | null = null
let panelOpener: HTMLElement | null = null
let panelRequest = 0

/**
 * پنلی را باز می‌کند و وقتی بسته شد با نتیجه‌اش حل می‌شود. نام ناشناخته (یا chunkی که بار نشد) خطا
 * پرتاب نمی‌کند: پیام فارسی نشان می‌دهد، در کنسول هشدار می‌گذارد و با undefined حل می‌شود. پنل باز دیگری
 * (اگر بود) اول بسته می‌شود.
 */
export function openPanel(name: string, props: Record<string, unknown> = {}): Promise<unknown> {
  const loader = hasPanel(name) ? panelLoaders[name] : undefined
  if (!loader) {
    console.warn(`[panel] پنل ثبت‌نشده: "${name}". موجود: ${panelNames().join('، ') || '—'}`)
    toast.error('این بخش هنوز در دسترس نیست؛ صفحه را تازه کنید یا بعداً دوباره امتحان کنید.')
    return Promise.resolve(undefined)
  }

  if (panelState.name !== null) closePanel(undefined)

  const request = ++panelRequest
  panelOpener = typeof document === 'undefined' ? null : (document.activeElement as HTMLElement | null)
  panelState.name = name
  panelState.props = { ...props }
  panelState.component = null
  panelState.loading = true

  const result = new Promise<unknown>((resolve) => {
    panelResolve = resolve
  })

  loader()
    .then((mod) => {
      if (request !== panelRequest || panelState.name !== name) return // در این فاصله بسته یا جایگزین شد
      // هر دو در یک تیک، تا پوسته کامپوننت را همان بار رندر کند که اسکلت برمی‌دارد
      panelState.component = markRaw(mod.default)
      panelState.loading = false
    })
    .catch((err: unknown) => {
      if (request !== panelRequest) return
      console.error(`[panel] بارگذاری "${name}" ناموفق`, err)
      toast.error('بارگذاری این بخش ناموفق بود. صفحه را تازه کنید.')
      closePanel(undefined)
    })

  return result
}

/** پنل باز را می‌بندد؛ Promise همان openPanel با <paramref>result</paramref> حل می‌شود و فوکوس برمی‌گردد. */
export function closePanel(result?: unknown): void {
  if (panelState.name === null && panelResolve === null) return

  const resolve = panelResolve
  const opener = panelOpener
  panelResolve = null
  panelOpener = null
  panelRequest++
  panelState.name = null
  panelState.component = null
  panelState.props = {}
  panelState.loading = false

  resolve?.(result)
  if (opener && typeof document !== 'undefined' && document.contains(opener)) opener.focus()
}

// ═════════════════════════════════════════════════════════════════════
// راهنما («؟ راهنما» → components/ui/HelpDrawer.vue)
// ═════════════════════════════════════════════════════════════════════

/**
 * کشوی راهنما اگر در build هست (components/ui/HelpDrawer.vue). با glob بارش می‌کنیم تا نبودنش build را
 * نشکند؛ UiShell آن را پس از اولین «؟ راهنما» با props ‎{ open, page? }‎ مانت می‌کند و به update:open و
 * close گوش می‌دهد.
 */
export const helpDrawerLoader: (() => Promise<{ default: Component }>) | null =
  Object.values(import.meta.glob<{ default: Component }>('../components/ui/HelpDrawer.vue'))[0] ?? null

/** page خالی یعنی «کشوی راهنما کلید را خودش بسازد» (data-help-page روی body، وگرنه مسیر نشانی). */
export const helpState = reactive({ open: false, page: '' })

/** راهنمای صفحه را باز می‌کند؛ بدون کشوی راهنما به «کارت راهنما» می‌رود. */
export function openHelp(page?: string): void {
  if (!helpDrawerLoader) {
    window.location.href = '/Help/Card'
    return
  }
  helpState.page = page ?? ''
  helpState.open = true
}

export function closeHelp(): void {
  helpState.open = false
}

// ═════════════════════════════════════════════════════════════════════
// اندازهٔ نوشته
// ═════════════════════════════════════════════════════════════════════

/**
 * اندازهٔ نوشتهٔ کاربر را همان لحظه عوض می‌کند (data-text-scale روی <html>) و در سرور ذخیره می‌کند
 * (PUT /api/me/preferences). اگر ذخیره نشد به اندازهٔ قبلی برمی‌گردد و پیام می‌دهد. منوی کاربر (site.js) و
 * هر جزیره‌ای که اندازه را عوض می‌کند از همین استفاده کند تا همه با هم همگام بمانند.
 */
export async function setTextScale(name: TextScaleName): Promise<boolean> {
  if (!(TEXT_SCALE_NAMES as readonly string[]).includes(name)) return false

  const previous = appContext().textScale
  if (previous === name) return true

  updateAppContext({ textScale: name })
  try {
    const { api } = await import('./api')
    await api.put('/api/me/preferences', { textScale: textScaleValue(name) })
    return true
  } catch (err) {
    updateAppContext({ textScale: previous })
    const detail = err instanceof Error && err.message ? err.message : ''
    toast.error(detail ? `اندازهٔ نوشته ذخیره نشد: ${detail}` : 'اندازهٔ نوشته ذخیره نشد؛ دوباره امتحان کنید.')
    return false
  }
}

// ═════════════════════════════════════════════════════════════════════
// دسترسی سراسری
// ═════════════════════════════════════════════════════════════════════

/** همهٔ فرمان‌های پوسته در یک شیء (جزیره‌ها import می‌کنند؛ site.js از window.rugsUI). */
export const ui = {
  toast,
  confirm: confirmDialog,
  toastAfterReload,
  openPanel,
  closePanel,
  hasPanel,
  openHelp,
  closeHelp,
  blockReload,
  requestReload,
  setTextScale,
}

export type RugsUI = typeof ui

declare global {
  interface Window {
    rugsUI?: RugsUI
  }
}

/** رویدادی که پس از آماده شدن window.rugsUI روی window فرستاده می‌شود (site.js منتظرش می‌ماند). */
export const UI_READY_EVENT = 'rugs:ui-ready'

/**
 * در دسترس گذاشتن برای کدهای غیر-Vue (site.js و viewهای رِیزور).
 * main.ts این را پس از مانت شدن UiShell صدا می‌زند.
 */
export function exposeGlobally(): void {
  window.rugsUI = ui
  window.dispatchEvent(new CustomEvent(UI_READY_EVENT))
}
