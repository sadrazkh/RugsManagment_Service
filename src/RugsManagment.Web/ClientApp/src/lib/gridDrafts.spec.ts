import { describe, expect, it } from 'vitest'
import {
  DISCARD_TTL_MS,
  clearDraft,
  discardDraft,
  discardedKey,
  draftKey,
  readDiscarded,
  readDraft,
  restoreDiscarded,
  writeDraft,
  type DraftRow,
  type DraftScope,
  type StorageLike,
} from './gridDrafts'

class MemoryStorage implements StorageLike {
  data = new Map<string, string>()
  getItem(key: string) {
    return this.data.get(key) ?? null
  }
  setItem(key: string, value: string) {
    this.data.set(key, value)
  }
  removeItem(key: string) {
    this.data.delete(key)
  }
}

/** storage ای که هر کاری کنی خطا می‌دهد (حالت خصوصی سافاری، سهمیهٔ پر). */
const throwing: StorageLike = {
  getItem: () => {
    throw new Error('SecurityError')
  },
  setItem: () => {
    throw new Error('QuotaExceededError')
  },
  removeItem: () => {
    throw new Error('SecurityError')
  },
}

const scope: DraftScope = { tenantId: 't1', userId: 'u1', gridId: 'intake', contextId: '2026-09-06' }

function row(id: string, values: Record<string, string | number | null> = { 'rug.serial': id }): DraftRow {
  return { clientRowId: id, rugId: null, version: null, values, base: {}, dirty: Object.keys(values) }
}

describe('کلیدهای پیش‌نویس (G.7)', () => {
  it('کارگاه، کاربر، جدول و زمینه', () => {
    expect(draftKey(scope)).toBe('rugs:draft:t1:u1:intake:2026-09-06')
    expect(discardedKey(scope)).toBe('rugs:draft-discarded:t1:u1:intake:2026-09-06')
  })

  it('بی کاربر چیزی نوشته نمی‌شود', () => {
    const storage = new MemoryStorage()
    expect(writeDraft({ ...scope, userId: null }, [row('a')], storage)).toBe(false)
    expect(storage.data.size).toBe(0)
  })
})

describe('خواندن و نوشتن', () => {
  it('رفت و برگشت؛ فهرست خالی کلید را پاک می‌کند', () => {
    const storage = new MemoryStorage()
    writeDraft(scope, [row('a'), row('b')], storage, 1000)
    expect(readDraft(scope, storage)?.rows.map((r) => r.clientRowId)).toEqual(['a', 'b'])
    writeDraft(scope, [], storage)
    expect(readDraft(scope, storage)).toBeNull()
    expect(storage.data.size).toBe(0)
  })

  it('JSON خراب یا شکل ناجور نادیده گرفته می‌شود', () => {
    const storage = new MemoryStorage()
    storage.setItem(draftKey(scope)!, '{not json')
    expect(readDraft(scope, storage)).toBeNull()
    storage.setItem(draftKey(scope)!, JSON.stringify({ rows: [{ nope: 1 }, row('ok')] }))
    expect(readDraft(scope, storage)?.rows.map((r) => r.clientRowId)).toEqual(['ok'])
  })

  it('storage ای که خطا می‌دهد هیچ‌چیز را نمی‌شکند', () => {
    expect(() => writeDraft(scope, [row('a')], throwing)).not.toThrow()
    expect(writeDraft(scope, [row('a')], throwing)).toBe(false)
    expect(readDraft(scope, throwing)).toBeNull()
    expect(() => clearDraft(scope, throwing)).not.toThrow()
    expect(discardDraft(scope, undefined, throwing)).toBeNull()
    expect(restoreDiscarded(scope, throwing)).toBeNull()
  })

  it('بی storage (null) هم بی‌خطا', () => {
    expect(writeDraft(scope, [row('a')], null)).toBe(false)
    expect(readDraft(scope, null)).toBeNull()
  })
})

describe('«پاک کردن» و «برگردان»', () => {
  it('محموله پیش از حذف به rugs:draft-discarded:* می‌رود و «برگردان» برش می‌گرداند', () => {
    const storage = new MemoryStorage()
    writeDraft(scope, [row('a'), row('b')], storage, 1000)
    const moved = discardDraft(scope, undefined, storage, 2000)
    expect(moved?.rows).toHaveLength(2)
    expect(moved?.expiresAt).toBe(2000 + DISCARD_TTL_MS)
    expect(readDraft(scope, storage)).toBeNull()
    expect(storage.data.has(discardedKey(scope)!)).toBe(true)

    const back = restoreDiscarded(scope, storage, 3000)
    expect(back?.map((r) => r.clientRowId)).toEqual(['a', 'b'])
    expect(readDraft(scope, storage)?.rows).toHaveLength(2)
    expect(storage.data.has(discardedKey(scope)!)).toBe(false)
  })

  it('فقط ردیف‌های نام‌برده پاک می‌شوند؛ نوشته‌های تازهٔ همین نشست می‌مانند', () => {
    const storage = new MemoryStorage()
    writeDraft(scope, [row('old1'), row('old2'), row('new')], storage, 1000)
    discardDraft(scope, [row('old1'), row('old2')], storage, 2000)
    expect(readDraft(scope, storage)?.rows.map((r) => r.clientRowId)).toEqual(['new'])
    restoreDiscarded(scope, storage, 2500)
    expect(readDraft(scope, storage)?.rows.map((r) => r.clientRowId)).toEqual(['new', 'old1', 'old2'])
  })

  it('پس از ۲۴ ساعت برگشتی نیست و کلید پاک می‌شود', () => {
    const storage = new MemoryStorage()
    writeDraft(scope, [row('a')], storage, 0)
    discardDraft(scope, undefined, storage, 0)
    expect(readDiscarded(scope, storage, DISCARD_TTL_MS + 1)).toBeNull()
    expect(storage.data.has(discardedKey(scope)!)).toBe(false)
    expect(restoreDiscarded(scope, storage, DISCARD_TTL_MS + 1)).toBeNull()
  })
})
