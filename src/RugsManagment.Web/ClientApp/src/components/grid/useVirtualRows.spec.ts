import { describe, expect, it } from 'vitest'
import { renderedIndices, scrollToRow, visibleWindow } from './useVirtualRows'

/**
 * مجازی‌سازی ردیف‌ها (G.1): فقط ردیف‌های دیده‌شده و چند ردیف پیش و پس رندر می‌شوند، و خانهٔ فعال با اسکرول دیده
 * می‌ماند (زیر سرستون چسبان و بالای نوار لمسی گم نمی‌شود).
 */
describe('پنجرهٔ ردیف‌های دیده‌شده', () => {
  it('بالای جدول: از ردیف اول، به اندازهٔ صفحه به‌اضافهٔ حاشیه', () => {
    // ۴۴۰ پیکسل = ۱۰ ردیف ۴۴ پیکسلی، +۱ نیمه‌دیده، +۶ حاشیه
    expect(visibleWindow(0, 440, 44, 300, 6, 44)).toEqual({ start: 0, end: 17 })
  })

  it('وسط جدول: حاشیه دو طرف؛ ارتفاع سرستون از اسکرول کم می‌شود', () => {
    // scrollTop 44×100 + سرستون ۴۴ → اولین ردیف دیده‌شده ۱۰۰
    expect(visibleWindow(4400 + 44, 440, 44, 300, 6, 44)).toEqual({ start: 94, end: 117 })
  })

  it('ته جدول به تعداد ردیف‌ها محدود است؛ جدول خالی یا ارتفاع صفر هیچ', () => {
    expect(visibleWindow(44 * 295, 440, 44, 300, 6, 0)).toEqual({ start: 289, end: 300 })
    expect(visibleWindow(0, 440, 44, 0)).toEqual({ start: 0, end: 0 })
    expect(visibleWindow(0, 440, 0, 10)).toEqual({ start: 0, end: 0 })
  })
})

describe('ردیف فعال بیرون از پنجره', () => {
  it('ردیف فعال همیشه رندر می‌شود و فهرست به ترتیب می‌ماند (بالای پنجره اول، پایین آخر)', () => {
    expect(renderedIndices({ start: 58, end: 62 }, 57, 80)).toEqual([57, 58, 59, 60, 61])
    expect(renderedIndices({ start: 58, end: 62 }, 77, 80)).toEqual([58, 59, 60, 61, 77])
    expect(renderedIndices({ start: 58, end: 62 }, 60, 80)).toEqual([58, 59, 60, 61])
  })

  it('وقتی پنجره به ردیف فعال می‌رسد، ترتیب نسبی گره‌ها عوض نمی‌شود (گرهٔ فوکوس‌دار جابه‌جا نمی‌شود)', () => {
    // PageUp: ردیف ۵۷ فعال شد و هنوز بالای پنجره است؛ بعد اسکرول خودکار پنجره را تا ۴۹ می‌آورد
    const before = renderedIndices({ start: 58, end: 78 }, 57, 80)
    const after = renderedIndices({ start: 49, end: 78 }, 57, 80)
    const kept = after.filter((i) => before.includes(i))
    expect(kept).toEqual(before.filter((i) => after.includes(i)))
    expect(before[0]).toBe(57)
  })

  it('ردیف فعالِ نامعتبر چیزی اضافه نمی‌کند', () => {
    expect(renderedIndices({ start: 0, end: 3 }, null, 10)).toEqual([0, 1, 2])
    expect(renderedIndices({ start: 0, end: 3 }, 12, 10)).toEqual([0, 1, 2])
  })
})

describe('دیده شدن ردیف فعال', () => {
  it('ردیف دیده‌شده اسکرول را عوض نمی‌کند', () => {
    expect(scrollToRow(3, 0, 440, 44, 44)).toBe(0)
  })

  it('ردیف پایین‌تر: آن‌قدر پایین که ته ردیف دیده شود (با فاصلهٔ نوار پایین)', () => {
    // ردیف ۲۰: بالایش ۴۴ + ۸۸۰ = ۹۲۴، تهش ۹۶۸ → scrollTop = ۹۶۸ − ۴۴۰ = ۵۲۸
    expect(scrollToRow(20, 0, 440, 44, 44)).toBe(528)
    expect(scrollToRow(20, 0, 440, 44, 44, 56)).toBe(584)
  })

  it('ردیف بالاتر: زیر سرستون چسبان می‌نشیند، نه پشت آن', () => {
    // ردیف ۵ وقتی scrollTop ۸۸۰ است → بالای ردیف (۴۴ + ۲۲۰) منهای سرستون = ۲۲۰
    expect(scrollToRow(5, 880, 440, 44, 44)).toBe(220)
    expect(scrollToRow(0, 880, 440, 44, 44)).toBe(0)
  })
})
