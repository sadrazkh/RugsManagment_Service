/**
 * فرمت نمایش عدد و پول — محاسبات مالی در بک‌اند است؛ اینجا فقط نمایش.
 */

export function formatCurrency(value: number, locale = 'fa-IR') {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency: 'IRR',
    maximumFractionDigits: 0,
  }).format(value)
}

export function formatNumber(value: number, locale = 'fa-IR') {
  return new Intl.NumberFormat(locale).format(value)
}

/** ترجمهٔ وضعیت enum انگلیسی API به فارسی UI */
export const statusLabels: Record<string, string> = {
  Draft: 'پیش‌نویس',
  InProgress: 'در جریان',
  ReadyForSale: 'آماده فروش',
  Sold: 'فروخته شده',
  Archived: 'بایگانی',
  Pending: 'در انتظار',
  Completed: 'تکمیل',
  Skipped: 'رد شده',
  InProgressStep: 'در حال انجام',
}

export function statusLabel(status: string) {
  return statusLabels[status] ?? status
}
