import { describe, expect, it } from 'vitest'
import { resetAppContext } from './context'
import { visibleBulkActions, type BulkAction } from './bulkActions'
import { HELP_PAGES, helpFor, helpPageFor } from './help'
import { MANUAL_RUG_STATUSES, roleLabel, rugStatus, rugStatusLabel, stepStatus } from './labels'
import { RugStatus, UserRole } from './types/platform'

describe('برچسب وضعیت فرش (واژه‌نامهٔ A.2)', () => {
  it('فرش تازه «ثبت‌شده» است، هرگز «پیش‌نویس»', () => {
    expect(rugStatusLabel(RugStatus.Draft, false)).toBe('ثبت‌شده')
    for (const status of Object.values(RugStatus)) {
      expect(rugStatusLabel(status, false)).not.toContain('پیش‌نویس')
      expect(rugStatusLabel(status, true)).not.toContain('پیش‌نویس')
    }
  })

  it('«در کارگاه» فقط در حالت کامل؛ نمای ساده محل فرش را می‌گوید (همان DisplayHelpers)', () => {
    expect(rugStatusLabel(RugStatus.InProgress, false)).toBe('در کارگاه')
    expect(rugStatusLabel(RugStatus.InProgress, true)).toBe('در انبار')
    expect(rugStatusLabel(RugStatus.Draft, true)).toBe('در انبار')
    expect(rugStatusLabel(RugStatus.ReadyForSale, true)).toBe('در انبار')
    expect(rugStatus(RugStatus.Draft, true).icon).toBe('warehouse')
    expect(rugStatusLabel(RugStatus.Sold, true)).toBe('فروخته‌شده')
    expect(rugStatusLabel(RugStatus.Exported, true)).toBe('صادر شده')
    expect(rugStatusLabel(RugStatus.Archived, true)).toBe('بایگانی')
    expect(rugStatusLabel(99, true)).toBe('نامشخص')
  })

  it('«صادر شده» با آیکون کامیون و رنگ توکن', () => {
    const view = rugStatus(RugStatus.Exported, false)
    expect(view.label).toBe('صادر شده')
    expect(view.icon).toBe('truck')
    expect(view.css).not.toMatch(/#|bg-white/)
  })

  it('وضعیت‌های دستی: فروخته و صادرشده نه', () => {
    expect(MANUAL_RUG_STATUSES).not.toContain(RugStatus.Sold)
    expect(MANUAL_RUG_STATUSES).not.toContain(RugStatus.Exported)
  })

  it('مرحله و نقش', () => {
    expect(stepStatus(2).label).toBe('تکمیل‌شده')
    expect(roleLabel(UserRole.Merchant)).toBe('بازرگان')
    expect(roleLabel('TenantAdmin')).toBe('تنظیم‌کننده (مدیر)')
    expect(roleLabel('Operator')).toBe('کاربر / انباردار')
    expect(roleLabel('toString')).toBe('نقش ناشناخته')
    expect(roleLabel(null)).toBe('نقش ناشناخته')
  })
})

/** متن C# سرور (اگر در مخزن هست) تا برچسب‌های دو طرف از هم جدا نشوند. */
const serverSources = import.meta.glob<string>(
  ['../../../Support/DisplayHelpers.cs', '../../../../RugsManagment.Application/Common/RoleLabels.cs'],
  { query: '?raw', import: 'default', eager: true },
)
const displayHelpers = Object.entries(serverSources).find(([path]) => path.endsWith('DisplayHelpers.cs'))?.[1]
const appRoleLabels = Object.entries(serverSources).find(([path]) => path.endsWith('RoleLabels.cs'))?.[1]

describe('همان برچسب‌های سرور', () => {
  /** «(label, css, icon)» یک بازوی switch سرور: `=> ("ثبت‌شده", NeutralChip, "edit")`. */
  function serverTriple(source: string, label: string): { css: string; icon: string } | null {
    const at = source.indexOf(`("${label}",`)
    if (at < 0) return null
    const line = source.slice(at, source.indexOf('\n', at))
    const m = /\("[^"]*",\s*(NeutralChip|"[^"]*"),\s*"([^"]*)"\)/.exec(line)
    if (!m) return null
    const neutral = /NeutralChip = "([^"]*)"/.exec(source)?.[1] ?? ''
    return { css: m[1] === 'NeutralChip' ? neutral : m[1].slice(1, -1), icon: m[2] }
  }

  it.skipIf(!displayHelpers)('وضعیت فرش و مرحله همان DisplayHelpers است (برچسب، رنگ، آیکون)', () => {
    const source = displayHelpers as string
    const views = [
      ...Object.values(RugStatus).map((s) => rugStatus(s, false)),
      rugStatus(RugStatus.Draft, true),
      ...[0, 1, 2, 3, 4].map((s) => stepStatus(s)),
    ]
    for (const view of views) {
      const server = serverTriple(source, view.label)
      expect(server, `«${view.label}» در DisplayHelpers.cs نیست`).not.toBeNull()
      expect(server!.css, view.label).toBe(view.css)
      expect(server!.icon, view.label).toBe(view.icon)
    }
  })

  it.skipIf(!appRoleLabels)('برچسب نقش‌ها همان RoleLabels.cs است', () => {
    for (const role of Object.values(UserRole)) {
      expect(appRoleLabels, String(role)).toContain(`"${roleLabel(role)}"`)
    }
    expect(appRoleLabels).toContain(`"${roleLabel(null)}"`)
  })
})

describe('راهنما', () => {
  it('هر صفحه ۳ تا ۵ گام دارد', () => {
    for (const page of HELP_PAGES) {
      const steps = helpFor(page).steps.length
      expect(steps, page).toBeGreaterThanOrEqual(3)
      expect(steps, page).toBeLessThanOrEqual(5)
    }
  })

  const sprite = Object.values(
    import.meta.glob<string>('../../../wwwroot/icons.svg', { query: '?raw', import: 'default', eager: true }),
  )[0]

  it.skipIf(!sprite)('هر آیکون راهنما و وضعیت در /icons.svg هست', () => {
    const ids = new Set([...(sprite as string).matchAll(/<symbol\s+id="([^"]+)"/g)].map((m) => m[1]))
    const used = new Set<string>()
    for (const page of HELP_PAGES) for (const step of helpFor(page).steps) if (step.icon) used.add(step.icon)
    for (const status of Object.values(RugStatus)) {
      used.add(rugStatus(status, false).icon)
      used.add(rugStatus(status, true).icon)
    }
    for (const s of [0, 1, 2, 3, 4]) used.add(stepStatus(s).icon)
    for (const icon of used) expect(ids.has(icon), `آیکون «${icon}» در icons.svg نیست`).toBe(true)
  })

  it('کلید صفحه از مسیر', () => {
    expect(helpPageFor('/')).toBe('home')
    expect(helpPageFor('/Rugs')).toBe('rugs')
    expect(helpPageFor('/Rugs/Details/abc')).toBe('rug-details')
    expect(helpPageFor('/Rugs/Create')).toBe('rug-form')
    expect(helpPageFor('/Groups/Details/1')).toBe('batches')
    expect(helpPageFor('/settings/preferences')).toBe('settings')
    expect(helpPageFor('/whatever')).toBe('default')
    expect(helpFor('constructor').title).toBe(helpFor('default').title)
  })
})

describe('درگاه نوار گروهی', () => {
  const action = (key: string, extra: Partial<BulkAction> = {}): { default: BulkAction } => ({
    default: { key, label: key, icon: 'plus', order: 100, run: () => undefined, ...extra },
  })

  it('مرتب، بی‌تکرار، و به سیاست و حالت بیننده', () => {
    const source = {
      '../bulk-actions/b.action.ts': action('b', { order: 200 }),
      '../bulk-actions/a.action.ts': action('a', { order: 100, policy: 'business' }),
      '../bulk-actions/c.action.ts': action('c', { order: 50, simple: false }),
      '../bulk-actions/dup.action.ts': action('b', { order: 1 }),
    }
    resetAppContext({ policies: { floor: true, business: true, configure: false }, uiMode: 'full' })
    expect(visibleBulkActions(undefined, source).map((a) => a.key)).toEqual(['c', 'a', 'b'])

    resetAppContext({ policies: { floor: true, business: false, configure: false }, uiMode: 'simple' })
    expect(visibleBulkActions(undefined, source).map((a) => a.key)).toEqual(['b'])
    resetAppContext({})
  })
})
