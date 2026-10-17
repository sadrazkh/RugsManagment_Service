import { describe, expect, it } from 'vitest'
import { duplicateKeyRows, keyNormalize, missingKeys } from './declaredValueKeys'

describe('کلیدهای ارزش گمرکی (KeyNormalize)', () => {
  it('ی و ک عربی، ارقام فارسی و فاصله‌های اضافه یکی می‌شوند', () => {
    // «نائین ۹ لا» = «نائين 9 لا» (C.5)
    expect(keyNormalize('نائین ۹ لا')).toBe(keyNormalize('نائين 9 لا'))
    expect(keyNormalize('  كاشان  ')).toBe('کاشان')
    expect(keyNormalize('نائین   ۹‌لا')).toBe('نائین 9 لا')
    expect(keyNormalize('٣٤')).toBe('34')
  })

  it('خالی و null خالی می‌مانند', () => {
    expect(keyNormalize('')).toBe('')
    expect(keyNormalize(null)).toBe('')
    expect(keyNormalize('   ')).toBe('')
  })

  it('ردیف تکراری به اولین ردیف هم‌کلید اشاره می‌کند', () => {
    const duplicates = duplicateKeyRows(['کاشان', 'تبریز', 'كاشان', '', '', 'تبریز '])
    expect([...duplicates.entries()]).toEqual([
      [2, 0],
      [5, 1],
    ])
  })

  it('مقدارهای بی‌نرخ: یکسان‌سازی‌شده، بی تکرار، به ترتیب', () => {
    expect(missingKeys(['كاشان', 'قم', 'نائین ۹ لا', 'قم', ' '], ['کاشان', 'نائين 9 لا'])).toEqual(['قم'])
    expect(missingKeys([], ['کاشان'])).toEqual([])
  })
})
