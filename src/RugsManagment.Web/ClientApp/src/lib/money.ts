/**
 * قالب‌بندی و تجزیهٔ مبالغ با جداکنندهٔ هزارگان.
 * ارقام فارسی/عربی هنگام تایپ به انگلیسی نرمال می‌شوند.
 */
const FA_DIGITS = '۰۱۲۳۴۵۶۷۸۹'
const AR_DIGITS = '٠١٢٣٤٥٦٧٨٩'

export function normalizeDigits(input: string): string {
  let out = ''
  for (const ch of input) {
    const fa = FA_DIGITS.indexOf(ch)
    const ar = AR_DIGITS.indexOf(ch)
    if (fa > -1) out += String(fa)
    else if (ar > -1) out += String(ar)
    else out += ch
  }
  return out
}

/** نمایش عدد با جداکنندهٔ هزارگان (بدون اعشار). */
export function formatThousands(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return ''
  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(value)
}

/** تبدیل ورودی متنی کاربر به عدد (ارقام فارسی و جداکننده‌ها را می‌پذیرد). */
export function parseNumber(raw: string): number | undefined {
  const digits = normalizeDigits(raw).replace(/[^\d.]/g, '')
  if (!digits) return undefined
  const n = Number(digits)
  return Number.isFinite(n) ? n : undefined
}
