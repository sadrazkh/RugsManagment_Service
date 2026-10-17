import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { resetAppContext } from '@/lib/context'
import { FakeNode, buttonWithText, click, domGlobals, fakeDocument, find, focus, mount, press } from '@/testing/miniDom'
import JalaliDateField from './JalaliDateField.vue'

/**
 * یافته‌های بازبینی فاز ۱ در خانهٔ تاریخ شمسی:
 * (۱) Esc فقط روی جدول روزها کار می‌کرد؛ روی «ماه قبل/بعد» یا «امروز/بستن» تقویم باز می‌ماند و همان Esc به تلهٔ فوکوس
 *     Modal/Drawer می‌رسید و کل دیالوگ را با فرمش می‌بست.
 * (۲) Tab که فوکوس را از تقویم بیرون می‌برد تقویم را باز می‌گذاشت.
 * (۳) تاریخ کهنهٔ بیرون از بازه («0001-01-01») خانه را می‌شکست.
 */
describe('JalaliDateField: تقویم با صفحه‌کلید', () => {
  beforeEach(() => {
    for (const [name, value] of Object.entries(domGlobals())) vi.stubGlobal(name, value)
    resetAppContext({ today: '2026-09-06' })
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    resetAppContext({})
  })

  const popoverOf = (root: FakeNode) => root.querySelector('div[role="dialog"]')
  const toggleOf = (root: FakeNode) => buttonWithText(root, 'تقویم')
  /** دکمهٔ «ماه قبل/بعد» (برچسبش روی آیکون است) */
  const navButton = (root: FakeNode, label: string) => find(root, `svg[aria-label="${label}"]`).parentNode!

  async function openField() {
    const field = await mount(JalaliDateField, { modelValue: '2026-09-06' })
    // مثل Modal/Drawer: هر keydownی که از خانه بیرون بیاید این‌جا دیده می‌شود
    const outer: string[] = []
    field.root.addEventListener('keydown', (e) => outer.push(e.key))
    await click(toggleOf(field.root))
    expect(popoverOf(field.root)).not.toBeNull()
    return { field, outer }
  }

  it.each(['ماه قبل', 'ماه بعد'])('Esc روی «%s» تقویم را می‌بندد و به دیالوگ بیرونی نمی‌رسد', async (label) => {
    const { field, outer } = await openField()
    const nav = navButton(field.root, label)
    await focus(nav)

    const esc = await press(nav, 'Escape')

    expect(esc.defaultPrevented).toBe(true)
    expect(outer).toEqual([])
    expect(popoverOf(field.root)).toBeNull()
    expect(fakeDocument.activeElement).toBe(toggleOf(field.root)) // فوکوس به دکمهٔ «تقویم» برگشت
  })

  it.each(['امروز', 'بستن'])('Esc روی «%s» پایین تقویم هم همین', async (text) => {
    const { field, outer } = await openField()
    const popover = popoverOf(field.root)!
    const button = buttonWithText(popover, text)
    await focus(button)

    await press(button, 'Escape')

    expect(outer).toEqual([])
    expect(popoverOf(field.root)).toBeNull()
  })

  it('Esc روی روزها همچنان فقط تقویم را می‌بندد', async () => {
    const { field, outer } = await openField()
    const day = find(popoverOf(field.root)!, 'button[data-active="true"]')
    expect(fakeDocument.activeElement).toBe(day)

    await press(day, 'Escape')

    expect(outer).toEqual([])
    expect(popoverOf(field.root)).toBeNull()
  })

  it('Tab از «بستن» به بیرون تقویم را می‌بندد، بی آن‌که فوکوس را پس بگیرد', async () => {
    const { field } = await openField()
    const after = new FakeNode('button')
    fakeDocument.body.children.push(after)
    after.parentNode = fakeDocument.body

    await focus(buttonWithText(popoverOf(field.root)!, 'بستن'))
    await focus(after) // همان کاری که Tab مرورگر می‌کند

    expect(popoverOf(field.root)).toBeNull()
    expect(fakeDocument.activeElement).toBe(after)
  })

  it('جابه‌جایی فوکوس درون تقویم آن را نمی‌بندد', async () => {
    const { field } = await openField()
    const popover = popoverOf(field.root)!

    await focus(navButton(field.root, 'ماه بعد'))
    await focus(buttonWithText(popover, 'امروز'))

    expect(popoverOf(field.root)).not.toBeNull()
  })

  it('Shift+Tab به دکمهٔ «تقویم» تقویم را نمی‌بندد (کلیک همان دکمه خودش می‌بندد)', async () => {
    const { field } = await openField()
    await focus(navButton(field.root, 'ماه قبل'))
    await focus(toggleOf(field.root))
    expect(popoverOf(field.root)).not.toBeNull()
  })
})

describe('JalaliDateField: تاریخ بیرون از بازه', () => {
  beforeEach(() => {
    for (const [name, value] of Object.entries(domGlobals())) vi.stubGlobal(name, value)
    resetAppContext({ today: '2026-09-06' })
  })
  afterEach(() => {
    vi.unstubAllGlobals()
    resetAppContext({})
  })

  it.each(['0001-01-01', '0024-05-01', '9999-12-31'])('«%s» خانه را نمی‌شکند و خالی نشان داده می‌شود', async (iso) => {
    const field = await mount(JalaliDateField, { modelValue: iso })
    expect(find(field.root, 'input').value).toBe('')

    // باز کردن تقویم هم از امروز شروع می‌کند، نه از سال نامعتبر
    await click(buttonWithText(field.root, 'تقویم'))
    expect(field.root.querySelector('div[role="dialog"]')).not.toBeNull()
    expect(field.values('update:modelValue')).toEqual([])
  })

  it('تقویم از سال ۱۲۰۰ عقب‌تر نمی‌رود (PageUp و «ماه قبل»)', async () => {
    const field = await mount(JalaliDateField, { modelValue: '1821-03-21' }) // ۱۲۰۰/۰۱/۰۱
    await click(buttonWithText(field.root, 'تقویم'))
    const popover = field.root.querySelector('div[role="dialog"]')!
    const title = () => popover.textContent

    const before = title()
    expect(before).toContain('۱۲۰۰')
    const day = find(popover, 'button[data-active="true"]')
    await press(day, 'PageUp')
    await click(find(field.root, 'svg[aria-label="ماه قبل"]').parentNode!)

    expect(title()).toBe(before)
    expect(field.values('update:modelValue')).toEqual([])
  })
})
