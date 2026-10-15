import { describe, expect, it } from 'vitest'
import { HELP_PAGES, helpFor } from './help'

/**
 * یافتهٔ بازبینی فاز ۱: کشوی «؟ راهنما» به انباردار گام‌هایی می‌گفت که دکمه یا خانه‌اش را ندارد («ثبت هزینه»، نوشتن
 * مبلغ با «م»، سطر حروف زیر مبلغ، «قطعی کن»). H.3: متن از سیاست بیننده پیروی می‌کند.
 */
describe('راهنما به نقش بیننده', () => {
  const operator = { business: false }
  const merchant = { business: true }
  const texts = (page: string, viewer?: { business: boolean }) => helpFor(page, viewer).steps.map((s) => s.text)

  it('انباردار «ثبت هزینه» خانه را نمی‌بیند؛ بازرگان می‌بیند', () => {
    expect(texts('home', operator).some((t) => t.includes('«ثبت هزینه»'))).toBe(false)
    expect(texts('home', merchant).some((t) => t.includes('«ثبت هزینه»'))).toBe(true)
  })

  it('در فرم فرش، گام‌های مبلغ فقط برای Business', () => {
    const forOperator = texts('rug-form', operator)
    expect(forOperator.some((t) => t.includes('مبلغ'))).toBe(false)
    expect(forOperator.some((t) => t.includes('طول و عرض'))).toBe(true)
    expect(texts('rug-form', merchant).filter((t) => t.includes('مبلغ'))).toHaveLength(2)
  })

  it('«قطعی کن» محموله فقط برای Business', () => {
    expect(texts('shipments', operator).some((t) => t.includes('قطعی کن'))).toBe(false)
    expect(texts('shipments', merchant).some((t) => t.includes('قطعی کن'))).toBe(true)
  })

  it('بی بیننده (کارت چاپی بازرگان) همهٔ گام‌ها؛ بازرگان همان', () => {
    for (const page of HELP_PAGES) expect(helpFor(page, merchant)).toEqual(helpFor(page))
  })

  it('هر صفحه برای انباردار هم دست‌کم ۳ گام دارد', () => {
    for (const page of HELP_PAGES) expect(helpFor(page, operator).steps.length, page).toBeGreaterThanOrEqual(3)
  })
})
