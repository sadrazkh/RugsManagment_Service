/**
 * آزمون پوسته: فهرست جزیره‌ها (glob فایل‌های *.island.vue + جزیره‌های قدیمی) و میزبان پنل‌ها در lib/ui.ts
 * (L.3: «openPanel glob را پیدا می‌کند؛ نام ناشناخته بی‌خطا شکست می‌خورد»)، به‌اضافهٔ قفل تازه‌سازی و سطح
 * پیام‌ها. محیط node است (vitest.config.ts)؛ چیزی که window می‌خواهد آن را با vi.stubGlobal می‌سازد.
 */
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  blockReload,
  closePanel,
  hasPanel,
  isToastLevel,
  openPanel,
  panelNameFromPath,
  panelNames,
  panelState,
  reloadBlocked,
  toKebab,
  toast,
  toasts,
} from '@/lib/ui'
import { islandConflicts, islandNameFromPath, islands } from './registry'

afterEach(() => {
  closePanel(undefined)
  toasts.splice(0, toasts.length)
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

describe('نام‌ها', () => {
  it('kebab-case از نام فایل', () => {
    expect(toKebab('CostEntry')).toBe('cost-entry')
    expect(toKebab('QRScanner')).toBe('qr-scanner')
    expect(toKebab('HomeCoach')).toBe('home-coach')
    expect(panelNameFromPath('../panels/Stub.panel.vue')).toBe('stub')
    expect(panelNameFromPath('../panels/CostEntry.panel.vue')).toBe('cost-entry')
    expect(islandNameFromPath('./platform/HomeCoach.island.vue')).toBe('home-coach')
    expect(islandNameFromPath('./settings/PreferencesForm.island.vue')).toBe('preferences-form')
  })
})

describe('فهرست جزیره‌ها', () => {
  it('جزیره‌های glob و ۱۹ جزیرهٔ قدیمی با هم، بی نام تکراری', () => {
    const names = Object.keys(islands)
    expect(names).toEqual(expect.arrayContaining(['home-coach', 'preferences-form']))
    expect(names).toEqual(
      expect.arrayContaining([
        'rug-form', 'rug-workflow', 'rug-gallery', 'rug-sale', 'sales-report', 'analytics-dashboard',
        'aging-alert', 'step-type-manager', 'qr-scanner', 'workflow-template-editor', 'list-quick-advance',
        'rug-bulk-toolbar', 'group-detail', 'label-designer', 'label-print', 'pwa-install',
        'custom-fields-manager', 'providers-manager', 'provider-statement',
      ]),
    )
    expect(islandConflicts).toEqual([])
  })
})

describe('میزبان پنل (openPanel)', () => {
  it('glob پنل‌ها را پیدا می‌کند', () => {
    expect(panelNames()).toContain('stub')
    expect(hasPanel('stub')).toBe(true)
    // نام‌های شیء پایه پنل نیستند
    expect(hasPanel('constructor')).toBe(false)
    expect(hasPanel('toString')).toBe(false)
  })

  it('نام ناشناخته خطا پرتاب نمی‌کند: پیام فارسی و undefined', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    await expect(openPanel('no-such-panel', {})).resolves.toBeUndefined()
    expect(warn).toHaveBeenCalled()
    expect(toasts.some((t) => t.kind === 'error')).toBe(true)
    expect(panelState.name).toBeNull()
  })

  it('باز، بار شدن کامپوننت، بستن با نتیجه', async () => {
    const result = openPanel('stub', { a: 1 })
    expect(panelState.name).toBe('stub')
    expect(panelState.props).toEqual({ a: 1 })
    expect(panelState.loading).toBe(true)
    await vi.waitFor(() => expect(panelState.component).not.toBeNull())
    expect(panelState.loading).toBe(false)

    closePanel('ok')
    await expect(result).resolves.toBe('ok')
    expect(panelState.name).toBeNull()
    expect(panelState.component).toBeNull()
  })

  it('پنل تازه پنل قبلی را با undefined می‌بندد', async () => {
    const first = openPanel('stub', {})
    const second = openPanel('stub', { b: 2 })
    await expect(first).resolves.toBeUndefined()
    expect(panelState.props).toEqual({ b: 2 })
    closePanel(1)
    await expect(second).resolves.toBe(1)
  })

  it('props از بیرون کپی می‌شود (تغییر بعدیِ شیء صدازننده اثر ندارد)', () => {
    const props: Record<string, unknown> = { x: 1 }
    void openPanel('stub', props)
    props.x = 2
    expect(panelState.props).toEqual({ x: 1 })
  })
})

describe('قفل تازه‌سازی (blockReload)', () => {
  it('تا آخرین قفل باز نشده، بسته می‌ماند؛ آزاد کردن دوباره بی‌اثر است', () => {
    const listeners = { add: vi.fn(), remove: vi.fn() }
    vi.stubGlobal('window', { addEventListener: listeners.add, removeEventListener: listeners.remove })

    const a = blockReload('grid')
    const b = blockReload('panel')
    expect(reloadBlocked.value).toBe(true)
    expect(listeners.add).toHaveBeenCalledTimes(1)

    a()
    a()
    expect(reloadBlocked.value).toBe(true)

    b()
    expect(reloadBlocked.value).toBe(false)
    expect(listeners.remove).toHaveBeenCalledTimes(1)
  })
})

describe('سطح پیام‌ها', () => {
  it('فقط چهار سطح معتبر؛ سطح ناشناخته info می‌شود', () => {
    for (const level of ['success', 'error', 'warning', 'info']) expect(isToastLevel(level)).toBe(true)
    expect(isToastLevel('fatal')).toBe(false)
    expect(isToastLevel(undefined)).toBe(false)

    toast.show('پیام', 'fatal')
    toast.show('خطا', 'error')
    expect(toasts.map((t) => t.kind)).toEqual(['info', 'error'])
  })
})
