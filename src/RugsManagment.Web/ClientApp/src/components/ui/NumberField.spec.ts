import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { resetAppContext } from '@/lib/context'
import { buttonWithText, click, domGlobals, find, focus, mount, press, typeInto } from '@/testing/miniDom'
import NumberField from './NumberField.vue'

/**
 * یافتهٔ بازبینی فاز ۱ (G.4): در خانهٔ اندازه، جواب «۲۵ متر» با خروج بعدی دوباره پرسیده و پاک می‌شد (فرم هرگز ثبت
 * نمی‌شد)، ضلع ۱۵ متریِ ذخیره‌شده با یک فوکوس و خروج پاک می‌شد، و «بزرگ‌ترین ضلع معمول» کارگاه نادیده بود.
 */
describe('NumberField اندازه: «متر یا سانت؟» یک بار', () => {
  beforeEach(() => {
    for (const [name, value] of Object.entries(domGlobals())) vi.stubGlobal(name, value)
    resetAppContext({})
  })
  afterEach(() => vi.unstubAllGlobals())

  interface Exposed {
    validate: () => string | null
  }

  it('جواب «۲۵ متر» با خروج و Enter بعدی پاک نمی‌شود و فرم ثبت می‌شود', async () => {
    const field = await mount<Exposed>(NumberField, { modelValue: null, kind: 'dim', label: 'طول' })
    const input = find(field.root, 'input')

    await focus(input)
    await typeInto(input, '25')
    await focus(null) // خروج: پرسش
    await click(buttonWithText(field.root, '۲۵ متر'))
    await focus(null)
    expect(field.values('update:modelValue').at(-1)).toBe(25)

    const before = field.emitted.length
    // دوباره فوکوس و خروج (مثل کلیک روی «ثبت فرش»)
    await focus(input)
    await focus(null)
    expect(field.emitted.length).toBe(before)
    expect(field.exposed.validate()).toBeNull()

    // Enter در همان خانه فرم را می‌فرستد (preventDefault نمی‌شود) و چیزی پاک نمی‌شود
    await focus(input)
    const enter = await press(input, 'Enter')
    expect(enter.defaultPrevented).toBe(false)
    expect(field.values('update:modelValue').at(-1)).toBe(25)
    expect(() => buttonWithText(field.root, 'سانت')).toThrow()
  })

  it('همان عدد دوباره تایپ شود، جواب قبلی بی پرسش به کار می‌رود', async () => {
    const field = await mount<Exposed>(NumberField, { modelValue: null, kind: 'dim' })
    const input = find(field.root, 'input')

    await focus(input)
    await typeInto(input, '25')
    await focus(null)
    await click(buttonWithText(field.root, '۲۵ متر'))

    await focus(input)
    await typeInto(input, '25')
    await focus(null)
    expect(() => buttonWithText(field.root, '۲۵ متر')).toThrow() // پرسشی نیامد
    expect(field.values('update:modelValue').at(-1)).toBe(25)
  })

  it('ضلع ۱۵ متریِ ذخیره‌شده با فوکوس و خروج بی‌تایپ پاک نمی‌شود', async () => {
    const field = await mount<Exposed>(NumberField, { modelValue: 15, kind: 'dim', label: 'طول' })
    const input = find(field.root, 'input')

    await focus(input)
    await focus(null)
    await focus(input)
    expect(field.exposed.validate()).toBeNull()
    await focus(null)

    expect(field.values('update:modelValue')).toEqual([])
    expect(() => buttonWithText(field.root, 'سانت')).toThrow()
  })

  it('«بزرگ‌ترین ضلع معمول» از app-context: ۸ متر یعنی «۱۰» پرسیده می‌شود', async () => {
    resetAppContext({ maxPlausibleSideM: 8 })
    const field = await mount(NumberField, { modelValue: null, kind: 'dim' })
    const input = find(field.root, 'input')

    await focus(input)
    await typeInto(input, '10')
    await focus(null)
    expect(buttonWithText(field.root, '۱۰ متر')).toBeTruthy()
  })

  it('با ۳۰ متر، «۲۵» بی پرسش متر است', async () => {
    resetAppContext({ maxPlausibleSideM: 30 })
    const field = await mount(NumberField, { modelValue: null, kind: 'dim' })
    const input = find(field.root, 'input')

    await focus(input)
    await typeInto(input, '25')
    await focus(null)
    expect(field.values('update:modelValue').at(-1)).toBe(25)
  })

  it('نوشتهٔ تازه همچنان خوانده و پرسیده می‌شود (رفتار درست عوض نشده)', async () => {
    const field = await mount(NumberField, { modelValue: 2.5, kind: 'dim' })
    const input = find(field.root, 'input')

    await focus(input)
    await typeInto(input, '208')
    await focus(null)
    expect(buttonWithText(field.root, '۲٫۰۸ متر')).toBeTruthy()
    expect(field.values('update:modelValue').at(-1)).toBeNull()
  })
})
