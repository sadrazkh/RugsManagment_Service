import { describe, expect, it } from 'vitest'
import { message, messages as all } from '@/lib/messages'
import { messages as gridMessages } from '@/lib/messages/grid'

/**
 * پیام‌های جدول (فاز ۳): هر کدی که سرور هم دارد (`Messages.fa.Grid.cs`) دقیقاً همان متن را دارد، و هر کدی که خودِ
 * جدول با `message('…')` صدا می‌زند واقعاً در فهرست کل هست (وگرنه بازرگان پیام عمومی «کار انجام نشد» می‌دید).
 */
const serverFiles = import.meta.glob<string>('../../../../../RugsManagment.Application/Common/Messages.fa.Grid.cs', {
  query: '?raw',
  import: 'default',
  eager: true,
})

function serverMessages(): Map<string, string> {
  const map = new Map<string, string>()
  for (const source of Object.values(serverFiles)) {
    for (const m of source.matchAll(/new\(\s*"([A-Z][A-Z0-9_]*)",\s*(?:\r?\n\s*)?"((?:[^"\\]|\\.)*)"\s*\)/g)) map.set(m[1], m[2])
  }
  return map
}

/** متن منبع کد جدول و جزیره‌هایش (برای پیدا کردن کدهای صدازده‌شده). */
const sources = import.meta.glob<string>(
  [
    './*.ts',
    './*.vue',
    '!./*.spec.ts',
    '../../islands/intake/*.vue',
    '../../islands/rugs/RugsGrid.island.vue',
    '../../islands/settings/GridColumnsEditor.island.vue',
  ],
  { query: '?raw', import: 'default', eager: true },
)

describe('پیام‌های جدول', () => {
  const server = serverMessages()

  it('همهٔ پیام‌های جدول در فهرست کل هستند', () => {
    for (const code of Object.keys(gridMessages)) expect(all[code], code).toBe(gridMessages[code])
  })

  it.skipIf(server.size === 0)('هر کد سرور در فایل کلاینت هست و متنش دقیقاً یکی است', () => {
    for (const [code, text] of server) {
      expect(gridMessages[code], `${code} در lib/messages/grid.ts نیست`).toBeDefined()
      expect(gridMessages[code], code).toBe(text)
    }
  })

  it('هر کدی که جدول صدا می‌زند تعریف شده', () => {
    const used = new Set<string>()
    for (const source of Object.values(sources)) {
      for (const m of source.matchAll(/message\(\s*'([EWIQ]_[A-Z0-9_]+)'/g)) used.add(m[1])
    }
    expect(used.size).toBeGreaterThan(20)
    for (const code of used) expect(all[code], `${code} تعریف نشده`).toBeTruthy()
  })

  it('متن‌های H.3 با نشانه‌های پرشده', () => {
    expect(message('I_GRID_DRAFTS_FOUND', { n: 5 })).toBe('۵ ردیفِ نوشته‌شده از دفعهٔ قبل ذخیره نشده بود.')
    expect(message('Q_GRID_DRAFTS_DISCARD', { n: 5 })).toBe('این ۵ ردیف پاک شود؟ دیگر برنمی‌گردد.')
    expect(message('Q_GRID_PASTE_CM', { n: 20 })).toBe('۲۰ اندازه به سانت نوشته شده؛ همه به متر تبدیل شود؟')
    expect(message('Q_GRID_DELETE_RUG', { serial: '۳۷۷۸' })).toBe('فرش ۳۷۷۸ حذف شود؟')
    expect(message('Q_GRID_RANGE_QTY', { from: '۳۹۸۵', to: '۴۰۰۳', count: 19, qty: 10 })).toBe(
      'شماره‌ها از ۳۹۸۵ تا ۴۰۰۳ است (۱۹ شماره) ولی تعداد را ۱۰ نوشته‌اید؛ درست است؟',
    )
    expect(message('I_GRID_RECENT_CHIP', { date: '۱۴۰۵/۰۶/۱۴', seller: 'حاج رضا', pieces: 8 })).toBe('۱۴۰۵/۰۶/۱۴ · حاج رضا · ۸ تخته')
    expect(message('I_GRID_PIECES_ROWS', { n: 52, rows: 3 })).toBe('۵۲ تخته (۳ ردیف)')
  })
})
