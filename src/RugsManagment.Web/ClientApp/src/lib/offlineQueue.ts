/**
 * صف عملیات آفلاین.
 *
 * چرا لازم است: اپراتور کارگاه با موبایل بین سالن‌ها می‌چرخد و اینترنت قطع و وصل می‌شود.
 * بدون صف، «تکمیل مرحله» در لحظهٔ قطعی گم می‌شود و کاربر نمی‌داند ثبت شده یا نه.
 *
 * راهبرد:
 *   • فقط خطای شبکه صف می‌شود. پاسخ ۴xx یعنی سرور تصمیم گرفته — تکرارش بی‌فایده است.
 *   • عملیات به ترتیب و یکی‌یکی بازپخش می‌شود تا ترتیب مراحل به‌هم نریزد.
 *   • «این مرحله قبلاً بسته شده» یعنی درخواست قبلی رسیده بوده؛ از صف حذف می‌شود نه اینکه خطا بماند.
 *
 * جداسازی به ازای کارگاه و کاربر: کلید ذخیره `rugs-offline-queue:{tenantId}:{userId}` از app-context است.
 * قبلاً یک کلید مشترک بود و اگر روی یک گوشی کاربر دیگری وارد می‌شد، کارهای معلقِ نفر قبل به نام او
 * فرستاده می‌شد. صف قدیمیِ بی‌نام (پیش از این تغییر) معلوم نیست مال کیست، پس دور ریخته می‌شود و
 * هرگز بازپخش نمی‌شود. بی‌کاربر (صفحهٔ بی‌layout، مدیر سامانه) صف فقط در حافظهٔ همان صفحه می‌ماند.
 *
 * ذخیره‌سازی در localStorage است نه IndexedDB: صف حداکثر چند ده آیتم کوچک دارد،
 * و سادگی کد اینجا از مزیت‌های IndexedDB مهم‌تر است.
 */
import { ref } from 'vue'
import { appContext } from '@/lib/context'
import { newId } from '@/lib/ids'

const STORAGE_PREFIX = 'rugs-offline-queue'
const LOCK_PREFIX = 'rugs-offline-flushing'
/** کلید مشترکِ پیش از جداسازی — فقط برای پاک کردن */
const LEGACY_STORAGE_KEY = STORAGE_PREFIX
/** قفل بین تب‌ها تا دو تب هم‌زمان یک عملیات را دو بار نفرستند. */
const LOCK_TTL_MS = 30_000

export interface QueuedAction {
  id: string
  url: string
  method: 'POST' | 'PUT' | 'DELETE'
  body: unknown
  /** متن فارسی برای نمایش در فهرست انتظار */
  label: string
  createdAt: number
  attempts: number
}

/** «tenantId:userId» کاربر جاری، یا null اگر صفحه کاربر کارگاه ندارد. */
function scope(): string | null {
  const { tenantId, userId } = appContext()
  return tenantId && userId ? `${tenantId}:${userId}` : null
}

/** کلید localStorage صف کاربر جاری (برای آزمون و OfflineIndicator هم export شده). */
export function queueStorageKey(): string | null {
  const s = scope()
  return s ? `${STORAGE_PREFIX}:${s}` : null
}

function lockKey(): string | null {
  const s = scope()
  return s ? `${LOCK_PREFIX}:${s}` : null
}

function storage(): Storage | null {
  try {
    return typeof localStorage === 'undefined' ? null : localStorage
  } catch {
    return null
  }
}

function load(): QueuedAction[] {
  const store = storage()
  if (!store) return []
  try {
    // صف بی‌نامِ قدیمی را هرگز بازپخش نکن؛ صاحبش معلوم نیست
    store.removeItem(LEGACY_STORAGE_KEY)
    const key = queueStorageKey()
    if (!key) return []
    const parsed = JSON.parse(store.getItem(key) ?? '[]') as unknown
    return Array.isArray(parsed) ? (parsed as QueuedAction[]) : []
  } catch {
    return []
  }
}

function persist() {
  const store = storage()
  const key = queueStorageKey()
  if (!store || !key) return
  try {
    if (queue.value.length === 0) store.removeItem(key)
    else store.setItem(key, JSON.stringify(queue.value))
  } catch {
    /* حالت خصوصی یا پر بودن فضا — صف در همین نشست می‌ماند */
  }
}

/** صف زنده — رابط کاربری روی همین واکنش نشان می‌دهد. */
export const queue = ref<QueuedAction[]>(load())
export const isOnline = ref(typeof navigator === 'undefined' ? true : navigator.onLine)
export const flushing = ref(false)

/** فقط برای آزمون: خواندن دوبارهٔ صف از localStorage (پس از عوض شدن app-context). */
export function reloadQueue(): void {
  queue.value = load()
}

function csrfToken(): string {
  return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') ?? ''
}

export function enqueue(action: Omit<QueuedAction, 'id' | 'createdAt' | 'attempts'>): QueuedAction {
  const item: QueuedAction = {
    ...action,
    id: newId(),
    createdAt: Date.now(),
    attempts: 0,
  }
  queue.value.push(item)
  persist()
  return item
}

export function remove(id: string) {
  queue.value = queue.value.filter((a) => a.id !== id)
  persist()
}

export function clear() {
  queue.value = []
  persist()
}

/** قفل ساده مبتنی بر زمان — تب دیگری که همین حالا در حال ارسال است را محترم می‌شمارد. */
function acquireLock(): boolean {
  const store = storage()
  const key = lockKey()
  if (!store || !key) return true
  try {
    const held = Number(store.getItem(key) ?? 0)
    if (held && Date.now() - held < LOCK_TTL_MS) return false
    store.setItem(key, String(Date.now()))
    return true
  } catch {
    return true
  }
}

function releaseLock() {
  const store = storage()
  const key = lockKey()
  if (!store || !key) return
  try {
    store.removeItem(key)
  } catch {
    /* بی‌صدا */
  }
}

export interface FlushResult {
  sent: number
  /** عملیاتی که سرور رد کرد و از صف حذف شدند */
  rejected: { label: string; message: string }[]
  /** هنوز در صف مانده (شبکه قطع است) */
  remaining: number
}

/**
 * بازپخش صف. اگر شبکه قطع باشد یا اولین درخواست به خطای شبکه بخورد،
 * بقیه دست‌نخورده می‌مانند تا ترتیب حفظ شود.
 */
export async function flush(): Promise<FlushResult> {
  const result: FlushResult = { sent: 0, rejected: [], remaining: queue.value.length }

  if (flushing.value || queue.value.length === 0 || !navigator.onLine) return result
  if (!acquireLock()) return result

  flushing.value = true
  try {
    // کپی می‌گیریم چون در حین ارسال از صف حذف می‌کنیم
    for (const action of [...queue.value]) {
      try {
        const res = await fetch(action.url, {
          method: action.method,
          headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken() },
          credentials: 'same-origin',
          body: action.body === undefined ? undefined : JSON.stringify(action.body),
        })

        if (res.ok) {
          result.sent++
          remove(action.id)
          continue
        }

        // سرور تصمیم گرفته — تکرار فایده ندارد
        let message = 'سرور این عملیات را نپذیرفت.'
        try {
          message = (await res.json())?.message ?? message
        } catch {
          /* پاسخ بدون بدنه */
        }

        result.rejected.push({ label: action.label, message })
        remove(action.id)
      } catch {
        // خطای شبکه: همین‌جا می‌ایستیم تا ترتیب به‌هم نریزد
        action.attempts++
        persist()
        break
      }
    }
  } finally {
    flushing.value = false
    releaseLock()
    result.remaining = queue.value.length
  }

  return result
}

/** اتصال به رویدادهای شبکه — یک بار از پوستهٔ UI صدا زده می‌شود. */
export function watchConnectivity(onFlushed: (r: FlushResult) => void) {
  const sync = async () => {
    isOnline.value = navigator.onLine
    if (!navigator.onLine) return

    const result = await flush()
    if (result.sent > 0 || result.rejected.length > 0) onFlushed(result)
  }

  window.addEventListener('online', () => void sync())
  window.addEventListener('offline', () => (isOnline.value = false))

  // صف بازمانده از نشست قبلیِ همین کاربر
  if (queue.value.length > 0) void sync()
}
