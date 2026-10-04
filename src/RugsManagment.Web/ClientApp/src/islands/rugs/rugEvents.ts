/**
 * پیام میان جزیره‌های یک فرش در یک صفحه (اندازهٔ نهایی، سطرهای خرید، ریز هزینه، فرم فرش).
 *
 * چرا: هر جزیره نسخهٔ فرش (xmin) را از props گرفته است. وقتی یکی ذخیره می‌کند نسخه جلو می‌رود و بقیه با نسخهٔ کهنه
 * ۴۰۹ می‌گرفتند. پس هر ذخیرهٔ موفق نسخهٔ تازه را با این رویداد خبر می‌دهد و بقیه نسخه‌شان را عوض می‌کنند (و ریز هزینه
 * اگر باز است دوباره می‌خواند). رویداد روی `window` است، چون هر جزیره اپ Vue جداست (I.5).
 */
export const RUG_CHANGED_EVENT = 'rugs:rug-changed'

export interface RugChangedDetail {
  rugId: string
  /** نسخهٔ تازهٔ فرش بعد از ذخیره */
  version: number
  /** کدام جزیره ذخیره کرد (تا خودش دوباره واکنش نشان ندهد) */
  source: string
}

/** خبر دادن ذخیرهٔ موفق. */
export function announceRugChanged(detail: RugChangedDetail): void {
  if (typeof window === 'undefined') return
  window.dispatchEvent(new CustomEvent<RugChangedDetail>(RUG_CHANGED_EVENT, { detail }))
}

/** گوش دادن به ذخیره‌های همین فرش از جزیره‌های دیگر؛ تابع برگشتی گوش دادن را برمی‌دارد. */
export function onRugChanged(rugId: string, source: string, handler: (detail: RugChangedDetail) => void): () => void {
  if (typeof window === 'undefined') return () => {}
  const listener = (event: Event) => {
    const detail = (event as CustomEvent<RugChangedDetail>).detail
    if (!detail || detail.rugId !== rugId || detail.source === source) return
    handler(detail)
  }
  window.addEventListener(RUG_CHANGED_EVENT, listener)
  return () => window.removeEventListener(RUG_CHANGED_EVENT, listener)
}
