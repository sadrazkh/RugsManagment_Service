/**
 * صف عملیات آفلاین.
 *
 * چرا لازم است: اپراتور کارگاه با موبایل بین سالن‌ها می‌چرخد و اینترنت قطع و وصل می‌شود.
 * بدون صف، «تکمیل مرحله» در لحظهٔ قطعی گم می‌شود و کاربر نمی‌داند ثبت شده یا نه.
 *
 * راهبرد:
 *   • فقط خطای شبکه صف می‌شود.
 *   • عملیات به ترتیب و یکی‌یکی بازپخش می‌شود تا ترتیب مراحل به‌هم نریزد.
 *   • هر پاسخ سرور یکی از چهار حالت است (`classifyReplay`):
 *     - ۲xx: رسید، از صف برداشته می‌شود.
 *     - ۴۰۱ / ۴۰۳ / ۴۰۰ بی‌پیام (رد توکن ضد CSRF): مشکل از نشست است نه از کار — نشست تمام شده یا در تب دیگری کس
 *       دیگری وارد شده. بازپخش می‌ایستد و همهٔ صف می‌ماند تا بعد از ورود دوباره فرستاده شود («دوباره وارد شوید»).
 *       پیش‌تر این‌ها «سرور تصمیم گرفته» حساب می‌شدند و کارِ گوشی‌ای که بیش از عمر کوکی آفلاین مانده بود پاک می‌شد.
 *     - ۴۰۸ / ۴۲۹ / ۵xx: سرور الان نمی‌تواند؛ مثل قطعی شبکه می‌ایستیم و بعداً دوباره.
 *     - بقیهٔ ۴xx (اعتبارسنجی با پیام، ۴۰۴، ۴۰۹ «این مرحله قبلاً بسته شده»): هرگز موفق نمی‌شود؛ با پیام دیدنی از صف
 *       حذف می‌شود.
 *   • پیش از بازپخش، `/api/me` باید همان کاربرِ صاحب صف را نشان دهد؛ وگرنه کار نفر قبل به نام نفر بعد نمی‌رود.
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
  /** عملیاتی که سرور رد کرد (هرگز موفق نمی‌شد) و از صف حذف شدند */
  rejected: { label: string; message: string }[]
  /** هنوز در صف مانده (شبکه قطع است، سرور در دسترس نیست، یا باید دوباره وارد شد) */
  remaining: number
  /**
   * بازپخش ایستاد چون نشست این کاربر معتبر نیست (۴۰۱/۴۰۳/رد توکن، یا کاربر دیگری در همین مرورگر وارد شده). صف
   * دست‌نخورده مانده؛ رابط کاربری «دوباره وارد شوید» را نشان می‌دهد.
   */
  signInRequired: boolean
}

/** نتیجهٔ یک پاسخ هنگام بازپخش. */
export type ReplayOutcome = 'sent' | 'drop' | 'stop-sign-in' | 'stop-retry'

/**
 * پاسخ سرور به یک کار بازپخش‌شده چه معنایی دارد. `body` بدنهٔ JSON خوانده‌شده است (یا null).
 *
 * ۴۰۰ با `message` همان خطای اعتبارسنجی برنامه است (ApiExceptionFilter و InvalidModelStateResponseFactory همیشه
 * پیام فارسی می‌گذارند)؛ ۴۰۰ بی‌پیام فقط از رد توکن ضد CSRF می‌آید، که یعنی کوکی و توکن این تب به هم نمی‌خورند.
 */
export function classifyReplay(status: number, body: unknown): ReplayOutcome {
  if (status >= 200 && status < 300) return 'sent'
  if (status === 401 || status === 403) return 'stop-sign-in'
  if (status === 400 && !hasMessage(body)) return 'stop-sign-in'
  if (status === 408 || status === 429 || status >= 500) return 'stop-retry'
  return 'drop'
}

function hasMessage(body: unknown): body is { message: string } {
  return typeof body === 'object' && body !== null && typeof (body as { message?: unknown }).message === 'string'
    && (body as { message: string }).message.length > 0
}

async function readBody(res: Response): Promise<unknown> {
  try {
    const text = await res.text()
    return text ? (JSON.parse(text) as unknown) : null
  } catch {
    return null
  }
}

/**
 * آیا کوکیِ همین مرورگر هنوز مال صاحب صف است؟ «sign-in» اگر نشست تمام شده یا کس دیگری وارد شده، «retry» اگر
 * سرور جواب نداد. صف بی‌کاربر (فقط در حافظه) چیزی برای سنجیدن ندارد.
 */
async function verifyOwner(): Promise<'ok' | 'sign-in' | 'retry'> {
  const { tenantId, userId } = appContext()
  if (!tenantId || !userId) return 'ok'
  try {
    const res = await fetch('/api/me', { headers: { Accept: 'application/json' }, credentials: 'same-origin' })
    if (res.status === 401 || res.status === 403) return 'sign-in'
    if (!res.ok) return 'retry'
    const me = (await readBody(res)) as { userId?: unknown; tenantId?: unknown } | null
    const same = (a: unknown, b: string) => typeof a === 'string' && a.toLowerCase() === b.toLowerCase()
    return me && same(me.userId, userId) && same(me.tenantId, tenantId) ? 'ok' : 'sign-in'
  } catch {
    return 'retry'
  }
}

/**
 * بازپخش صف. اگر شبکه قطع باشد، سرور در دسترس نباشد یا نشست معتبر نباشد، از همان کار می‌ایستد و بقیه
 * دست‌نخورده می‌مانند تا ترتیب حفظ شود.
 */
export async function flush(): Promise<FlushResult> {
  const result: FlushResult = { sent: 0, rejected: [], remaining: queue.value.length, signInRequired: false }

  if (flushing.value || queue.value.length === 0 || !navigator.onLine) return result
  if (!acquireLock()) return result

  flushing.value = true
  try {
    const owner = await verifyOwner()
    if (owner !== 'ok') {
      result.signInRequired = owner === 'sign-in'
      return result
    }

    // کپی می‌گیریم چون در حین ارسال از صف حذف می‌کنیم
    for (const action of [...queue.value]) {
      let res: Response
      try {
        res = await fetch(action.url, {
          method: action.method,
          headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken() },
          credentials: 'same-origin',
          body: action.body === undefined ? undefined : JSON.stringify(action.body),
        })
      } catch {
        // خطای شبکه: همین‌جا می‌ایستیم تا ترتیب به‌هم نریزد
        action.attempts++
        persist()
        break
      }

      const body = res.ok ? null : await readBody(res)
      const outcome = classifyReplay(res.status, body)

      if (outcome === 'sent') {
        result.sent++
        remove(action.id)
        continue
      }

      if (outcome === 'stop-sign-in') {
        // کار به سرور نرسیده؛ نگه می‌داریم تا بعد از ورود دوباره
        result.signInRequired = true
        break
      }

      if (outcome === 'stop-retry') {
        action.attempts++
        persist()
        break
      }

      // سرور تصمیم گرفته و تکرار فایده ندارد؛ با پیامش از صف حذف می‌شود
      const message = hasMessage(body) ? body.message : 'سرور این عملیات را نپذیرفت.'
      result.rejected.push({ label: action.label, message })
      remove(action.id)
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
    if (result.sent > 0 || result.rejected.length > 0 || result.signInRequired) onFlushed(result)
  }

  window.addEventListener('online', () => void sync())
  window.addEventListener('offline', () => (isOnline.value = false))

  // صف بازمانده از نشست قبلیِ همین کاربر
  if (queue.value.length > 0) void sync()
}
