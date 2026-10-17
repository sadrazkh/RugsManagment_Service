import { describe, expect, it } from 'vitest'
import { resetAppContext } from '@/lib/context'
import { message, messages as all } from './index'
import { messages as shipmentMessages } from './shipments'

/** `Messages.fa.Shipments.cs` سرور، اگر در مخزن هست. */
const serverFiles = import.meta.glob<string>('../../../../../RugsManagment.Application/Common/Messages.fa.Shipments.cs', {
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

describe('پیام‌های محموله (فاز ۴a)', () => {
  const server = serverMessages()

  it('همهٔ پیام‌های محموله در فهرست کل هستند', () => {
    for (const code of Object.keys(shipmentMessages)) expect(all[code], code).toBe(shipmentMessages[code])
  })

  it.skipIf(server.size === 0)('هر کد سرور در فایل کلاینت هست و متنش دقیقاً یکی است', () => {
    for (const [code, text] of server) {
      expect(shipmentMessages[code], `${code} در lib/messages/shipments.ts نیست`).toBeDefined()
      expect(shipmentMessages[code], code).toBe(text)
    }
  })

  it('هر «نگله» با نشانه است، نه متن ثابت', () => {
    for (const [code, text] of Object.entries(shipmentMessages)) expect(text, code).not.toMatch(/نگله|عدل/)
  })

  it('واژهٔ کارگاه «عدل» در پیام‌ها می‌نشیند', () => {
    resetAppContext({ terms: { bale: 'عدل', bales: 'عدل‌ها', baleOf: 'عدل', baleFooter: 'عدل شماره' } })
    expect(message('E_EMPTY_BALE', { n: 5 })).toBe('عدل ۵ خالی است؛ یا فرش در آن بگذارید یا حذفش کنید.')
    resetAppContext({})
    expect(message('W_BALE_OVER_TARGET', { n: 3, area: 15.1, target: 13 })).toBe('نگلهٔ ۳ شد ۱۵٫۱ متر؛ از ۱۳ متر هدف بیشتر است.')
  })

  it('جدول ارزش گمرکی: پیام ذخیره با تعداد ردیف‌ها', () => {
    expect(message('I_DECLARED_VALUES_SAVED', { n: 20 })).toBe('جدول ارزش گمرکی ذخیره شد؛ ارزش ۲۰ ردیف محمولهٔ باز دوباره حساب شد.')
    expect(message('E_DECLARED_KEY_DUPLICATE', { key: 'کاشان' })).toBe('«کاشان» دو بار در جدول آمده.')
  })
})
