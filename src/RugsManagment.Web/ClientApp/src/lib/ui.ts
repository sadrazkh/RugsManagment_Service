/**
 * سرویس رابط کاربری سراسری — Toast و دیالوگ تأیید.
 *
 * جایگزین alert()/confirm() بومی مرورگر که سبک ندارند، RTL نمی‌شوند،
 * ترد اصلی را قفل می‌کنند و روی موبایل زشت‌اند.
 *
 * چون هر جزیره (island) یک اپ Vue مستقل است، وضعیت در ماژول سطح‌بالا نگه‌داری می‌شود
 * تا همهٔ جزیره‌ها و حتی کد ساده در Razor به همین صف مشترک دسترسی داشته باشند.
 */
import { reactive } from 'vue'

export type ToastKind = 'success' | 'error' | 'warning' | 'info'

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
  toasts.push({ id, kind, message })
  window.setTimeout(() => dismissToast(id), durationFor(kind))
  return id
}

export const toast = {
  success: (message: string) => showToast(message, 'success'),
  error: (message: string) => showToast(message, 'error'),
  warning: (message: string) => showToast(message, 'warning'),
  info: (message: string) => showToast(message, 'info'),
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
    showToast(message, kind)
  } catch {
    /* محتوای نامعتبر — نادیده */
  }
}

/**
 * دیالوگ تأیید — Promise برمی‌گرداند تا دقیقاً مثل confirm() استفاده شود:
 *   if (!(await confirmDialog({ title: 'حذف شود؟' }))) return
 */
export function confirmDialog(options: ConfirmOptions): Promise<boolean> {
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

/**
 * در دسترس گذاشتن برای کدهای غیر-Vue (اسکریپت‌های ساده داخل viewهای رِیزور).
 * main.ts این را هنگام بالا آمدن صدا می‌زند.
 */
export function exposeGlobally(): void {
  ;(window as unknown as Record<string, unknown>).rugsUI = { toast, confirm: confirmDialog }
}
