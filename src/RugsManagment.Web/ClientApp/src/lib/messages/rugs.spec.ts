import { describe, expect, it } from 'vitest'
import { message, messages as all } from './index'
import { messages as rugMessages } from './rugs'

/** `Messages.fa.Rugs.cs` سرور، اگر در مخزن هست. */
const serverFiles = import.meta.glob<string>('../../../../../RugsManagment.Application/Common/Messages.fa.Rugs.cs', {
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

describe('پیام‌های فرش (فاز ۲)', () => {
  const server = serverMessages()

  it('همهٔ پیام‌های فرش در فهرست کل هستند', () => {
    for (const code of Object.keys(rugMessages)) expect(all[code], code).toBe(rugMessages[code])
  })

  it.skipIf(server.size === 0)('هر کد سرور در فایل کلاینت هست و متنش دقیقاً یکی است', () => {
    for (const [code, text] of server) {
      expect(rugMessages[code], `${code} در lib/messages/rugs.ts نیست`).toBeDefined()
      expect(rugMessages[code], code).toBe(text)
    }
  })

  it('پیش‌نمایش برش دسته (D.9)', () => {
    expect(
      message('I_SPLIT_PREVIEW', { total: 50, kept: 30, taken: 20, keptSerial: '۳۹۱۱ تا ۳۹۴۰', takenSerial: '۳۹۴۱ تا ۳۹۶۰' }),
    ).toBe('۵۰ تخته ← ۳۰ + ۲۰ · شماره‌ها ۳۹۱۱ تا ۳۹۴۰ و ۳۹۴۱ تا ۳۹۶۰')
  })

  it('بازهٔ کوتاه‌تر از تعداد با نشانه‌های پرشده', () => {
    expect(message('E_SERIAL_QTY', { serial: '۳۹۱۱', count: 1, qty: 50 })).toContain('فقط ۱ شماره دارد ولی تعداد ۵۰ تخته است')
  })
})
