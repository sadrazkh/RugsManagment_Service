import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  blockReload,
  closePanel,
  hasPanel,
  openPanel,
  panelNameFromPath,
  panelNames,
  panelState,
  reloadBlocked,
  requestReload,
  toKebab,
  toasts,
} from './ui'

afterEach(() => {
  closePanel(undefined)
  vi.restoreAllMocks()
})

describe('میزبان پنل‌ها (panels/*.panel.vue)', () => {
  it('نام پنل kebab-case نام فایل است', () => {
    expect(panelNameFromPath('../panels/CostEntry.panel.vue')).toBe('cost-entry')
    expect(panelNameFromPath('../panels/Stub.panel.vue')).toBe('stub')
    expect(toKebab('QRScanner')).toBe('qr-scanner')
    expect(toKebab('HomeCoach')).toBe('home-coach')
  })

  it('glob پنل‌ها را پیدا می‌کند و کلیدهای پیش‌نمونه پنل نیستند', () => {
    expect(panelNames()).toEqual([...panelNames()].sort())
    expect(hasPanel('constructor')).toBe(false)
    expect(hasPanel('toString')).toBe(false)
  })

  it('نام ناشناخته خطا پرتاب نمی‌کند: پیام فارسی و حل با undefined', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined)
    const before = toasts.length
    await expect(openPanel('no-such-panel', { x: 1 })).resolves.toBeUndefined()
    expect(warn).toHaveBeenCalled()
    expect(toasts.length).toBe(before + 1)
    const last = toasts[toasts.length - 1]
    expect(last.kind).toBe('error')
    expect(last.message).not.toMatch(/[A-Za-z]/)
    expect(panelState.name).toBeNull()
  })

  it.skipIf(!hasPanel('stub'))('پنل موجود بار می‌شود و با نتیجهٔ بستن حل می‌شود', async () => {
    const result = openPanel('stub', { shipmentId: 'a' })
    expect(panelState.name).toBe('stub')
    expect(panelState.props).toEqual({ shipmentId: 'a' })
    expect(panelState.loading).toBe(true)
    await vi.waitFor(() => expect(panelState.component).not.toBeNull())
    expect(panelState.loading).toBe(false)

    closePanel('done')
    await expect(result).resolves.toBe('done')
    expect(panelState.name).toBeNull()
    expect(panelState.component).toBeNull()
  })

  it.skipIf(!hasPanel('stub'))('باز کردن پنل دوم اولی را با undefined می‌بندد', async () => {
    const first = openPanel('stub', { n: 1 })
    const second = openPanel('stub', { n: 2 })
    await expect(first).resolves.toBeUndefined()
    expect(panelState.props).toEqual({ n: 2 })
    closePanel(42)
    await expect(second).resolves.toBe(42)
  })
})

describe('قفل تازه‌سازی (blockReload)', () => {
  it('تا آزاد نشده، تازه‌سازی خودکار انجام نمی‌شود و پیام می‌دهد', () => {
    const release = blockReload('ذخیرهٔ جدول')
    expect(reloadBlocked.value).toBe(true)
    const before = toasts.length
    expect(requestReload()).toBe(false)
    expect(toasts.length).toBe(before + 1)

    release()
    release() // دوباره صدا زدن بی‌اثر است
    expect(reloadBlocked.value).toBe(false)
  })

  it('چند قفل هم‌زمان: تا آخرین آزاد نشود قفل می‌ماند', () => {
    const a = blockReload('a')
    const b = blockReload('b')
    a()
    expect(reloadBlocked.value).toBe(true)
    b()
    expect(reloadBlocked.value).toBe(false)
  })
})
