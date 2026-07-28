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
 * ذخیره‌سازی در localStorage است نه IndexedDB: صف حداکثر چند ده آیتم کوچک دارد،
 * و سادگی کد اینجا از مزیت‌های IndexedDB مهم‌تر است.
 */
import { ref } from 'vue'

const STORAGE_KEY = 'rugs-offline-queue'
/** قفل بین تب‌ها تا دو تب هم‌زمان یک عملیات را دو بار نفرستند. */
const LOCK_KEY = 'rugs-offline-flushing'
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

/** صف زنده — رابط کاربری روی همین واکنش نشان می‌دهد. */
export const queue = ref<QueuedAction[]>(load())
export const isOnline = ref(navigator.onLine)
export const flushing = ref(false)

function load(): QueuedAction[] {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]') as QueuedAction[]
  } catch {
    return []
  }
}

function persist() {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(queue.value))
  } catch {
    /* حالت خصوصی یا پر بودن فضا — صف در همین نشست می‌ماند */
  }
}

function csrfToken(): string {
  return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') ?? ''
}

export function enqueue(action: Omit<QueuedAction, 'id' | 'createdAt' | 'attempts'>): QueuedAction {
  const item: QueuedAction = {
    ...action,
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
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
  try {
    const held = Number(localStorage.getItem(LOCK_KEY) ?? 0)
    if (held && Date.now() - held < LOCK_TTL_MS) return false
    localStorage.setItem(LOCK_KEY, String(Date.now()))
    return true
  } catch {
    return true
  }
}

function releaseLock() {
  try {
    localStorage.removeItem(LOCK_KEY)
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

/** اتصال به رویدادهای شبکه — یک بار از main.ts صدا زده می‌شود. */
export function watchConnectivity(onFlushed: (r: FlushResult) => void) {
  const sync = async () => {
    isOnline.value = navigator.onLine
    if (!navigator.onLine) return

    const result = await flush()
    if (result.sent > 0 || result.rejected.length > 0) onFlushed(result)
  }

  window.addEventListener('online', () => void sync())
  window.addEventListener('offline', () => (isOnline.value = false))

  // صف بازمانده از نشست قبلی
  if (queue.value.length > 0) void sync()
}
