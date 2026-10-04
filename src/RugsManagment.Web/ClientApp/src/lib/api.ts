/**
 * کلاینت سبک برای فراخوانی APIهای هم‌مبدأ (same-origin).
 * توکن CSRF را از meta صفحه خوانده و در هدر X-CSRF-TOKEN می‌فرستد (هماهنگ با AutoValidateAntiforgeryToken بک‌اند).
 * کوکی احراز هویت خودکار همراه درخواست می‌رود.
 *
 * خطا: پاسخ ناموفق `ApiError` می‌شود که `message` فارسی سرور را دارد (پس `(e as Error).message` در
 * جزیره‌های قدیمی همان کار قبل را می‌کند) و علاوه بر آن `status`، `errors` (خطای هر خانه، ۴۰۰) و
 * `field` (۴۰۹). قطعی شبکه همان `TypeError` خودِ fetch می‌ماند؛ `sendOrQueue` روی همین تکیه دارد.
 */
import { coreMessages } from '@/lib/messages'
import type { ApiErrorBody } from '@/lib/types/platform'

function csrfToken(): string {
  if (typeof document === 'undefined') return ''
  return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') ?? ''
}

/** خطای پاسخ ناموفق سرور. */
export class ApiError extends Error {
  readonly status: number
  /** خطای هر خانه (۴۰۰)، کلید camelCase → پیام‌ها؛ همیشه شیء (شاید خالی) */
  readonly errors: Readonly<Record<string, string[]>>
  /** خانهٔ درگیر در تعارض (۴۰۹)، مثل «rug.serial» */
  readonly field: string | null
  /** بدنهٔ کامل پاسخ (کلیدهای اضافه مثل `nextFree` همین‌جاست) */
  readonly body: ApiErrorBody | null

  constructor(status: number, message: string, body: ApiErrorBody | null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.body = body
    this.errors = normalizeErrors(body?.errors)
    this.field = typeof body?.field === 'string' ? body.field : null
  }

  /** نخستین پیام یک خانه؛ نام خانه بی‌حساسیت به بزرگی حرف و با/بی‌پیشوند «$.» مقایسه می‌شود. */
  fieldError(name: string): string | undefined {
    const wanted = errorKey(name)
    for (const [key, list] of Object.entries(this.errors)) {
      if (errorKey(key) === wanted && list.length > 0) return list[0]
    }
    return undefined
  }
}

function errorKey(name: string): string {
  return name.replace(/^\$\./, '').toLowerCase()
}

/** ProblemDetails خودِ ASP.NET کلید «WidthMeters» یا «$.widthMeters» می‌دهد؛ ما camelCase یکدست می‌خواهیم. */
function normalizeErrors(raw: unknown): Record<string, string[]> {
  const out: Record<string, string[]> = {}
  if (typeof raw !== 'object' || raw === null || Array.isArray(raw)) return out
  for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
    const clean = key.replace(/^\$\./, '')
    const camel = clean ? clean[0].toLowerCase() + clean.slice(1) : clean
    const list = Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : typeof value === 'string' ? [value] : []
    out[camel] = [...(out[camel] ?? []), ...list]
  }
  return out
}

/** پیام پیش‌فرض هر کد وضعیت، وقتی سرور پیامی نفرستاده (مثلاً ۴۰۳ خودِ سیاست دسترسی بدنه ندارد). */
function fallbackMessage(status: number): string {
  if (status === 401 || status === 403) return coreMessages.E_FORBIDDEN
  if (status === 404) return coreMessages.E_NOT_FOUND
  if (status === 409) return coreMessages.E_CONFLICT
  if (status === 400 || status === 422) return coreMessages.E_VALIDATION
  return coreMessages.E_SERVER
}

async function readError(res: Response): Promise<ApiError> {
  let body: ApiErrorBody | null = null
  try {
    const text = await res.text()
    if (text) {
      const parsed = JSON.parse(text) as Record<string, unknown>
      if (parsed && typeof parsed === 'object') {
        // ProblemDetails («title») هم پذیرفته است، ولی پیام فارسی سرور (`message`) مقدم است
        const message = typeof parsed.message === 'string' ? parsed.message : ''
        body = { ...parsed, message } as ApiErrorBody
      }
    }
  } catch {
    /* پاسخ بدون بدنه یا غیر JSON */
  }
  const message = body?.message || fallbackMessage(res.status)
  return new ApiError(res.status, message, body ?? null)
}

interface RequestOptions {
  body?: unknown
  /** بدنهٔ multipart؛ Content-Type را مرورگر با boundary می‌گذارد */
  form?: FormData
  signal?: AbortSignal
}

async function send(method: string, url: string, options: RequestOptions = {}): Promise<Response> {
  const headers: Record<string, string> = { 'X-CSRF-TOKEN': csrfToken(), Accept: 'application/json' }
  let body: BodyInit | undefined
  if (options.form) {
    body = options.form
  } else {
    // مثل قبل: درخواست‌های JSON همیشه این سربرگ را دارند، حتی بی‌بدنه (برخی اکشن‌ها بدون آن ۴۱۵ می‌دهند)
    headers['Content-Type'] = 'application/json'
    if (options.body !== undefined) body = JSON.stringify(options.body)
  }

  const res = await fetch(url, { method, headers, credentials: 'same-origin', body, signal: options.signal })
  if (!res.ok) throw await readError(res)
  return res
}

async function request<T>(method: string, url: string, options: RequestOptions = {}): Promise<T> {
  const res = await send(method, url, options)
  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

/** نام فایل از Content-Disposition (RFC 5987 «filename*=UTF-8''…» مقدم بر «filename=»). */
export function fileNameFromDisposition(header: string | null | undefined): string | null {
  if (!header) return null
  const star = /filename\*\s*=\s*([^']*)'[^']*'([^;]+)/i.exec(header)
  if (star) {
    try {
      return decodeURIComponent(star[2].trim().replace(/^"|"$/g, ''))
    } catch {
      /* کدگذاری خراب؛ می‌رویم سراغ filename ساده */
    }
  }
  const plain = /filename\s*=\s*("([^"]*)"|[^;]+)/i.exec(header)
  if (plain) return (plain[2] ?? plain[1]).trim()
  return null
}

export interface DownloadOptions {
  method?: 'GET' | 'POST'
  /** بدنهٔ JSON (برای POST) */
  body?: unknown
  /** بدنهٔ فرم (برای POST) */
  form?: FormData
  /** نام فایل اگر سرور نام نداد */
  fileName?: string
  signal?: AbortSignal
}

/**
 * دریافت فایل (اکسل، CSV) و ذخیره با `a[download]` — بی‌آنکه صفحه عوض شود یا خطای سرور به‌صورت
 * صفحهٔ خام باز شود. نام فایل از سرور (Content-Disposition) گرفته می‌شود. نام ذخیره‌شده برمی‌گردد.
 */
async function download(url: string, options: DownloadOptions = {}): Promise<string> {
  const res = await send(options.method ?? 'GET', url, { body: options.body, form: options.form, signal: options.signal })
  const blob = await res.blob()
  const name = fileNameFromDisposition(res.headers.get('Content-Disposition')) ?? options.fileName ?? 'download'

  const href = URL.createObjectURL(blob)
  try {
    const a = document.createElement('a')
    a.href = href
    a.download = name
    a.rel = 'noopener'
    a.style.display = 'none'
    document.body.appendChild(a)
    a.click()
    a.remove()
  } finally {
    // پیش از پایان دانلود آزاد نشود (فایرفاکس)
    window.setTimeout(() => URL.revokeObjectURL(href), 60_000)
  }
  return name
}

export const api = {
  get: <T>(url: string, signal?: AbortSignal) => request<T>('GET', url, { signal }),
  post: <T>(url: string, body?: unknown) => request<T>('POST', url, { body }),
  put: <T>(url: string, body?: unknown) => request<T>('PUT', url, { body }),
  /** DELETE، با بدنهٔ اختیاری (مثل `{ rugIds }` برای بیرون بردن از ورودی) */
  del: <T>(url: string, body?: unknown) => request<T>('DELETE', url, { body }),
  /** ارسال multipart (بارگذاری فایل) */
  postForm: <T>(url: string, form: FormData) => request<T>('POST', url, { form }),
  download,
}

/**
 * پیام فارسی برای نمایش هر خطا: پیام سرور، «اینترنت را نگاه کنید» برای قطعی شبکه، وگرنه پیام عمومی.
 * هرگز متن انگلیسی خطای مرورگر به بازرگان نشان داده نمی‌شود.
 */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof TypeError) return coreMessages.E_NETWORK
  if (error instanceof DOMException && error.name === 'AbortError') return ''
  return coreMessages.E_SERVER
}

/** آیا خطا قطعی شبکه است (نه پاسخ سرور)؟ */
export function isNetworkError(error: unknown): boolean {
  return error instanceof TypeError
}

/** نتیجهٔ ارسالِ تاب‌آور در برابر قطعی شبکه. */
export type SendOutcome =
  /** به سرور رسید */
  | { kind: 'sent' }
  /** شبکه قطع بود؛ در صف ماند تا بعداً ارسال شود */
  | { kind: 'queued' }

/**
 * عملیات را می‌فرستد و اگر شبکه قطع بود در صف آفلاین می‌گذارد.
 *
 * تمایز مهم: خطای شبکه صف می‌شود، ولی خطای سرور (۴xx/۵xx) پرتاب می‌شود —
 * چون تکرار درخواستی که سرور رد کرده بی‌فایده است و کاربر باید همان لحظه بداند.
 */
export async function sendOrQueue(
  method: 'POST' | 'PUT' | 'DELETE',
  url: string,
  body: unknown,
  label: string,
): Promise<SendOutcome> {
  const { enqueue } = await import('./offlineQueue')

  if (!navigator.onLine) {
    enqueue({ url, method, body, label })
    return { kind: 'queued' }
  }

  try {
    await request(method, url, { body })
    return { kind: 'sent' }
  } catch (error) {
    // request فقط برای پاسخ‌های ناموفق ApiError پرتاب می‌کند؛ خطای شبکه TypeError است
    if (error instanceof TypeError) {
      enqueue({ url, method, body, label })
      return { kind: 'queued' }
    }
    throw error
  }
}
