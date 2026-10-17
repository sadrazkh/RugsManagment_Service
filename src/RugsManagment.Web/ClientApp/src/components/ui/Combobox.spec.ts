import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { domGlobals, find, focus, mount, press, typeInto } from '@/testing/miniDom'
import Combobox from './Combobox.vue'

/**
 * یافته‌های بازبینی فاز ۱: (۱) گزینه‌هایی که بعد از سوار شدن می‌رسند، شناسهٔ خام (مثلاً GUID) را در خانه جا می‌گذاشتند؛
 * (۲) Enter در خانهٔ خالیِ دست‌نخورده بی‌صدا گزینهٔ اول را انتخاب می‌کرد و فرم را نمی‌فرستاد.
 */
describe('Combobox', () => {
  beforeEach(() => {
    for (const [name, value] of Object.entries(domGlobals())) vi.stubGlobal(name, value)
  })
  afterEach(() => vi.unstubAllGlobals())

  it('گزینه‌هایی که دیر می‌رسند برچسب مقدار را جای شناسه می‌نشانند', async () => {
    const box = await mount(Combobox, { modelValue: 'id-1', options: [] })
    const input = find(box.root, 'input')
    expect(input.value).toBe('id-1')

    await box.setProps({ options: [{ value: 'id-1', label: 'کاشان' }, { value: 'id-2', label: 'تبریز' }] })

    expect(input.value).toBe('کاشان')
    expect(box.values('update:modelValue')).toEqual([])
  })

  it('نوشتهٔ کاربر با رسیدن گزینه‌ها پاک نمی‌شود', async () => {
    const box = await mount(Combobox, { modelValue: 'id-1', options: [], allowCreate: true })
    const input = find(box.root, 'input')
    await focus(input)
    await typeInto(input, 'حاج رضا')
    await press(input, 'Escape')

    await box.setProps({ options: [{ value: 'id-1', label: 'کاشان' }] })

    expect(input.value).toBe('حاج رضا')
  })

  it('Enter در خانهٔ خالیِ دست‌نخورده چیزی انتخاب نمی‌کند و فرم را می‌فرستد', async () => {
    const box = await mount(Combobox, { modelValue: null, options: ['الف', 'ب'] })
    const input = find(box.root, 'input')

    await focus(input)
    const enter = await press(input, 'Enter')

    expect(enter.defaultPrevented).toBe(false)
    expect(box.values('update:modelValue')).toEqual([])
    expect(input.getAttribute('aria-activedescendant')).toBeNull()
  })

  it('↓ سپس Enter همچنان اولی را انتخاب می‌کند', async () => {
    const box = await mount(Combobox, { modelValue: null, options: ['الف', 'ب'] })
    const input = find(box.root, 'input')

    await focus(input)
    await press(input, 'ArrowDown')
    const enter = await press(input, 'Enter')

    expect(enter.defaultPrevented).toBe(true)
    expect(box.values('update:modelValue')).toEqual(['الف'])
  })

  it('تایپ سپس Enter نزدیک‌ترین گزینه را انتخاب می‌کند', async () => {
    const box = await mount(Combobox, { modelValue: null, options: ['الف', 'ب'] })
    const input = find(box.root, 'input')

    await focus(input)
    await typeInto(input, 'ب')
    await press(input, 'Enter')

    expect(box.values('update:modelValue')).toEqual(['ب'])
  })

  it('تایپ و پاک کردن همهٔ نوشته، سپس Enter: چیزی انتخاب نمی‌شود و فرم می‌رود', async () => {
    const box = await mount(Combobox, { modelValue: null, options: ['الف', 'ب'] })
    const input = find(box.root, 'input')

    await focus(input)
    await typeInto(input, 'ب')
    await typeInto(input, '')
    const enter = await press(input, 'Enter')

    expect(enter.defaultPrevented).toBe(false)
    expect(box.values('update:modelValue')).toEqual([])
  })

  it('مقدار فعلی هنگام فوکوس برجسته است (Enter همان را نگه می‌دارد)', async () => {
    const box = await mount(Combobox, { modelValue: 'ب', options: ['الف', 'ب'] })
    const input = find(box.root, 'input')

    await focus(input)
    await press(input, 'Enter')

    expect(box.values('update:modelValue')).toEqual(['ب'])
  })
})
