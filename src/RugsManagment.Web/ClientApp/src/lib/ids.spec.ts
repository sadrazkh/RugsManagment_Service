import { describe, expect, it } from 'vitest'
import { isGuid, newId, uuidFromBytes } from './ids'

const V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/

describe('شناسهٔ یکتا', () => {
  it('با randomUUID', () => {
    expect(newId({ randomUUID: () => 'AAAAAAAA-BBBB-4CCC-8DDD-EEEEEEEEEEEE' })).toBe('aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee')
  })

  it('بدون randomUUID (زمینهٔ ناامن): getRandomValues', () => {
    let called = false
    const id = newId({
      getRandomValues: (array: Uint8Array) => {
        called = true
        array.fill(0xff)
        return array
      },
    })
    expect(called).toBe(true)
    expect(id).toMatch(V4)
    expect(id).toBe('ffffffff-ffff-4fff-bfff-ffffffffffff')
  })

  it('randomUUID که خطا می‌دهد هم راه بعد را می‌رود', () => {
    const id = newId({
      randomUUID: () => {
        throw new Error('insecure')
      },
      getRandomValues: (a: Uint8Array) => a,
    })
    expect(id).toBe('00000000-0000-4000-8000-000000000000')
  })

  it('بدون هیچ crypto: Math.random، باز هم GUID نسخهٔ ۴ و یکتا', () => {
    const ids = new Set(Array.from({ length: 200 }, () => newId(undefined)))
    expect(ids.size).toBe(200)
    for (const id of ids) expect(id).toMatch(V4)
  })

  it('crypto واقعی', () => {
    expect(newId()).toMatch(V4)
    expect(isGuid(newId())).toBe(true)
    expect(isGuid('x')).toBe(false)
  })

  it('uuidFromBytes بیت‌های نسخه را می‌گذارد', () => {
    expect(uuidFromBytes(new Uint8Array(16))).toBe('00000000-0000-4000-8000-000000000000')
  })
})
