/**
 * کلیدهای جدول ارزش گمرکی در مرورگر — همان یکسان‌سازی سرور (`KeyNormalize`، C.5: `PersianText.Normalize`، ارقام لاتین،
 * فاصله‌های جمع‌شده)، تا ویرایشگر پیش از فرستادن بگوید «کاشان» و «كاشان» یکی‌اند و کدام محل بافتِ فرش‌ها هنوز نرخ ندارد.
 *
 * داوری نهایی با سرور است (کلید تکراری را او هم رد می‌کند)؛ این فقط برای اینکه تنظیم‌کننده خطا را همان‌جا ببیند.
 */

/** نویسه‌هایی که سرور یکی می‌گیرد (`PersianText.Normalize`). */
const CHAR_MAP: Readonly<Record<string, string>> = {
  'ي': 'ی', // ی عربی → ی فارسی
  'ك': 'ک', // ک عربی → ک فارسی
  'ة': 'ه', // ة → ه
  'ۀ': 'ه', // هٔ → ه
  '‌': ' ', // نیم‌فاصله → فاصله
}

const FA_ZERO = 0x06f0 // ۰
const AR_ZERO = 0x0660 // ٠

/** یکسان‌سازی یک کلید: «نائين ۹  لا» → «نائین 9 لا». */
export function keyNormalize(value: string | null | undefined): string {
  if (!value) return ''
  let out = ''
  for (const ch of value) {
    const code = ch.codePointAt(0) ?? 0
    if (code >= FA_ZERO && code <= FA_ZERO + 9) out += String(code - FA_ZERO)
    else if (code >= AR_ZERO && code <= AR_ZERO + 9) out += String(code - AR_ZERO)
    else out += CHAR_MAP[ch] ?? ch
  }
  return out.replace(/\s+/g, ' ').trim()
}

/**
 * ردیف‌هایی که کلیدشان (پس از یکسان‌سازی) پیش‌تر در فهرست آمده: اندیس ردیف تکراری → اندیس اولین ردیف هم‌کلید.
 * کلید خالی تکراری شمرده نمی‌شود (خطای «بنویسید» جداست).
 */
export function duplicateKeyRows(keys: readonly string[]): Map<number, number> {
  const first = new Map<string, number>()
  const duplicates = new Map<number, number>()
  keys.forEach((key, index) => {
    const normalized = keyNormalize(key)
    if (!normalized) return
    const seen = first.get(normalized)
    if (seen === undefined) first.set(normalized, index)
    else duplicates.set(index, seen)
  })
  return duplicates
}

/**
 * مقدارهایی که در فرش‌ها هست ولی هیچ ردیف جدول ندارد (به ترتیب ورودی، بی تکرار). همان‌هایی که در محموله «ندارد» یا
 * «نرخ پیش‌فرض» می‌گیرند.
 */
export function missingKeys(inUse: readonly string[], tableKeys: readonly string[]): string[] {
  const covered = new Set(tableKeys.map(keyNormalize).filter((k) => k.length > 0))
  const seen = new Set<string>()
  const missing: string[] = []
  for (const value of inUse) {
    const normalized = keyNormalize(value)
    if (!normalized || covered.has(normalized) || seen.has(normalized)) continue
    seen.add(normalized)
    missing.push(value.trim())
  }
  return missing
}
