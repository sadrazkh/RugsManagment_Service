/**
 * شناسهٔ یکتا در مرورگر — برای ردیف تازهٔ جدول (`ClientRowId`)، کلید یکتای ثبت هزینه (`ClientKey`) و
 * هر جایی که ارسال دوباره نباید دو رکورد بسازد.
 *
 * چرا fallback: `crypto.randomUUID` فقط در «زمینهٔ امن» (https یا localhost) هست؛ تبلت انبار که با
 * نشانی شبکهٔ داخلی (http://192.168…) باز شده آن را ندارد. پس اول randomUUID، بعد getRandomValues، و
 * فقط در آخر Math.random. خروجی همیشه GUID نسخهٔ ۴ با حروف کوچک است (همان شکلی که سرور `Guid` می‌خواند).
 */

interface CryptoLike {
  randomUUID?: () => string
  getRandomValues?: (array: Uint8Array) => Uint8Array
}

function randomBytes(source: CryptoLike | undefined): Uint8Array {
  const bytes = new Uint8Array(16)
  if (source && typeof source.getRandomValues === 'function') {
    source.getRandomValues(bytes)
  } else {
    for (let i = 0; i < bytes.length; i++) bytes[i] = Math.floor(Math.random() * 256)
  }
  return bytes
}

/** GUID نسخهٔ ۴ از ۱۶ بایت تصادفی (بیت‌های نسخه و گونه درست گذاشته می‌شوند). */
export function uuidFromBytes(bytes: Uint8Array): string {
  const b = Uint8Array.from(bytes.subarray(0, 16))
  b[6] = (b[6] & 0x0f) | 0x40
  b[8] = (b[8] & 0x3f) | 0x80
  const hex = Array.from(b, (x) => x.toString(16).padStart(2, '0')).join('')
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`
}

/**
 * شناسهٔ تازه. `source` فقط برای آزمون است؛ پیش‌فرض `globalThis.crypto`.
 */
export function newId(source: CryptoLike | undefined = (globalThis as { crypto?: CryptoLike }).crypto): string {
  if (source && typeof source.randomUUID === 'function') {
    try {
      return source.randomUUID().toLowerCase()
    } catch {
      /* زمینهٔ ناامن در برخی مرورگرها خطا می‌دهد؛ می‌رویم سراغ راه بعد */
    }
  }
  return uuidFromBytes(randomBytes(source))
}

/** نام خواناتر برای کلید یکتای یک ارسال («ثبت هزینه» دوبار ثبت نشود). */
export const newClientKey = newId

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

export function isGuid(value: unknown): value is string {
  return typeof value === 'string' && GUID.test(value)
}

/** GUID تهی سرور (`Guid.Empty`) — مثلاً «بیرون بردن از ورودی» در ویرایش گروهی. */
export const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'
