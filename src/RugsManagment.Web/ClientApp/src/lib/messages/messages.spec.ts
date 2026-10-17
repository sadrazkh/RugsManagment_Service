import { describe, expect, it } from 'vitest'
import { resetAppContext } from '@/lib/context'
import { coreMessages, duplicateMessageCodes, fillMessage, formatMessageNumber, message, messages } from './index'
import { messages as kitMessages } from './kit'

/** همهٔ نشانه‌ها را بردار تا فقط متن فارسی بماند. */
const withoutTokens = (text: string) => text.replace(/\{[A-Za-z][A-Za-z0-9]*\}/g, '')

describe('پیام‌ها', () => {
  it('هیچ کدی دو بار تعریف نشده (هستهٔ سکو + همهٔ فایل‌های فاز)', () => {
    expect(duplicateMessageCodes).toEqual([])
  })

  it('پیام‌های جعبه‌ابزار در فهرست کل هستند', () => {
    for (const code of Object.keys(kitMessages)) expect(messages[code], code).toBe(kitMessages[code])
    for (const code of Object.keys(coreMessages)) expect(messages[code], code).toBeTruthy()
  })

  it('هر کد متن دارد و کد شکل درست دارد', () => {
    for (const [code, text] of Object.entries(messages)) {
      expect(code, code).toMatch(/^[EWIQ]_[A-Z0-9_]+$/)
      expect(text.trim().length, code).toBeGreaterThan(0)
    }
  })

  it('بیرون از نشانه‌ها هیچ حرف لاتینی نیست', () => {
    for (const [code, text] of Object.entries(messages)) {
      expect(withoutTokens(text), `${code}: ${text}`).not.toMatch(/[A-Za-z]/)
    }
  })

  it('آکولادها جفت‌اند (نشانهٔ نیمه‌کاره نیست)', () => {
    for (const [code, text] of Object.entries(messages)) {
      expect(withoutTokens(text), code).not.toMatch(/[{}]/)
    }
  })
})

/** همهٔ `Messages.fa*.cs` سرور (کدهای پایه + فایل‌های هر فاز)، اگر در مخزن هستند. */
const serverFiles = import.meta.glob<string>('../../../../../RugsManagment.Application/Common/Messages.fa*.cs', {
  query: '?raw',
  import: 'default',
  eager: true,
})

function serverMessages(): Map<string, string> {
  const map = new Map<string, string>()
  for (const source of Object.values(serverFiles)) {
    for (const m of source.matchAll(/new\(\s*"([A-Z][A-Z0-9_]*)",\s*"((?:[^"\\]|\\.)*)"\s*\)/g)) map.set(m[1], m[2])
  }
  return map
}

describe('همان متن سرور (Messages.fa.cs)', () => {
  const server = serverMessages()

  it.skipIf(server.size === 0)('هر کد پایه در سرور هست و متنش دقیقاً یکی است', () => {
    for (const [code, text] of Object.entries(coreMessages)) {
      expect(server.has(code), `${code} در Messages.fa.cs نیست`).toBe(true)
      expect(server.get(code), code).toBe(text)
    }
  })

  it.skipIf(server.size === 0)('کد جعبه‌ابزار اگر در سرور هم آمد، همان متن را دارد', () => {
    for (const [code, text] of Object.entries(kitMessages)) {
      if (server.has(code)) expect(server.get(code), code).toBe(text)
    }
  })
})

describe('پر کردن نشانه‌ها', () => {
  it('عدد با ارقام فارسی و «٬»/«٫»', () => {
    expect(formatMessageNumber(10_000_000)).toBe('۱۰٬۰۰۰٬۰۰۰')
    expect(formatMessageNumber(2.5)).toBe('۲٫۵')
    expect(message('E_TOO_LONG', { label: 'نام', n: 120 })).toBe('«نام» حداکثر ۱۲۰ حرف است.')
  })

  it('پرسش سانت با متن آماده', () => {
    expect(message('Q_DIM_CM', { typed: '۲۰۸', metres: '۲٫۰۸' })).toBe('۲۰۸ ← ۲٫۰۸ متر؟')
  })

  it('نشانهٔ بی‌مقدار دست‌نخورده می‌ماند', () => {
    expect(fillMessage('«{label}» درست نیست.')).toBe('«{label}» درست نیست.')
  })

  it('واژهٔ نگله/عدل از app-context', () => {
    resetAppContext({ terms: { bale: 'عدل', bales: 'عدل‌ها', baleOf: 'عدل', baleFooter: 'عدل شماره' } })
    expect(fillMessage('{baleOf} ۵ خالی است')).toBe('عدل ۵ خالی است')
    resetAppContext({})
    expect(fillMessage('{baleOf} ۵ خالی است')).toBe('نگلهٔ ۵ خالی است')
  })

  it('کد ناشناخته پیام عمومی می‌شود، نه خودِ کد', () => {
    expect(message('X_UNKNOWN')).toBe(coreMessages.E_SERVER)
  })
})
