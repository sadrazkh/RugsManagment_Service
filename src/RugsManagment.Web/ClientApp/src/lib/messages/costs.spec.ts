import { describe, expect, it } from 'vitest'
import { resetAppContext } from '@/lib/context'
import { message, messages as all } from './index'
import { messages as costMessages } from './costs'

/** `Messages.fa.Costs.cs` سرور، اگر در مخزن هست. */
const serverFiles = import.meta.glob<string>('../../../../../RugsManagment.Application/Common/Messages.fa.Costs.cs', {
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

describe('پیام‌های هزینه (فاز ۵)', () => {
  const server = serverMessages()

  it('همهٔ پیام‌های هزینه در فهرست کل هستند', () => {
    for (const code of Object.keys(costMessages)) expect(all[code], code).toBe(costMessages[code])
  })

  it.skipIf(server.size === 0)('هر کد سرور در فایل کلاینت هست و متنش دقیقاً یکی است', () => {
    for (const [code, text] of server) {
      expect(costMessages[code], `${code} در lib/messages/costs.ts نیست`).toBeDefined()
      expect(costMessages[code], code).toBe(text)
    }
  })

  it('هر کد هشدار و خطای D.8 متن دارد', () => {
    const codes = [
      'W_STEP_DOUBLE', 'W_PURCHASE_DOUBLE', 'W_PIN_DROPPED', 'W_ZERO_WEIGHTS', 'W_NO_DECLARED_VALUE', 'W_EMPTY_TARGET',
      'W_TARGET_DELETED', 'W_LATE_COST', 'W_RATE_DRIFT', 'W_PINS_RESIDUAL', 'W_NEGATIVE_WEIGHT',
      'E_PINS_EXCEED', 'E_PINS_BASIS', 'E_CIRCULAR', 'E_FX_REQUIRED', 'E_SCOPE_EMPTY_SELECTION',
    ]
    for (const code of codes) expect(costMessages[code], code).toBeTruthy()
  })

  it('هر «نگله» با نشانه است و واحد پول ثابت نوشته نشده', () => {
    for (const [code, text] of Object.entries(costMessages)) {
      expect(text, code).not.toMatch(/نگله|عدل/)
      expect(text, code).not.toMatch(/\{(amount|rate)\} (تومان|ریال)/)
    }
  })

  it('جمله‌ها و toast همان نمونه‌های H.2', () => {
    resetAppContext({})
    expect(message('I_COST_SAVED', { amount: 10_000_000, currency: 'تومان', pieces: 52 })).toBe(
      'ثبت شد · ۱۰٬۰۰۰٬۰۰۰ تومان بین ۵۲ تخته تقسیم شد',
    )
    expect(message('I_COST_DELETED', { number: 12 })).toBe('هزینهٔ شمارهٔ ۱۲ حذف شد')
    expect(message('W_TARGET_DELETED')).toContain('(نگله، محموله یا ورودی)')
    resetAppContext({ terms: { bale: 'عدل', bales: 'عدل‌ها', baleOf: 'عدل', baleFooter: 'عدل شماره' } })
    expect(message('E_COST_BALE_REQUIRED')).toBe('عدل را مشخص کنید.')
    resetAppContext({})
  })
})
