import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { draftKey, discardedKey, type DraftRow, type DraftScope, type StorageLike } from '@/lib/gridDrafts'
import { useGridDrafts } from './useGridDrafts'

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

const scope: DraftScope = { tenantId: 't', userId: 'u', gridId: 'intake', contextId: '2026-09-06' }
const row = (id: string): DraftRow => ({ clientRowId: id, rugId: null, version: null, values: { 'rug.serial': id }, base: {}, dirty: ['rug.serial'] })
const ids = (storage: MemoryStorage) =>
  (JSON.parse(storage.getItem(draftKey(scope)!) ?? '{"rows":[]}') as { rows: DraftRow[] }).rows.map((r) => r.clientRowId)

describe('useGridDrafts (G.7)', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('هر تغییر با ۳۰۰ میلی‌ثانیه تأخیر نوشته می‌شود', () => {
    const storage = new MemoryStorage()
    let current = [row('a')]
    const drafts = useGridDrafts({ scope: () => scope, collect: () => current, storage: () => storage })
    drafts.schedule()
    current = [row('a'), row('b')]
    drafts.schedule()
    vi.advanceTimersByTime(299)
    expect(storage.data.size).toBe(0)
    vi.advanceTimersByTime(1)
    expect(ids(storage)).toEqual(['a', 'b'])
  })

  it('storage ای که خطا می‌دهد جدول را نمی‌شکند', () => {
    const throwing: StorageLike = {
      getItem: () => {
        throw new Error('x')
      },
      setItem: () => {
        throw new Error('x')
      },
      removeItem: () => {
        throw new Error('x')
      },
    }
    const drafts = useGridDrafts({ scope: () => scope, collect: () => [row('a')], storage: () => throwing })
    expect(drafts.check()).toBe(0)
    expect(() => drafts.flush()).not.toThrow()
    expect(() => drafts.discardPending()).not.toThrow()
    expect(drafts.undoDiscard()).toBeNull()
  })

  it('پیش‌نویس جامانده پیشنهاد می‌شود و تا تصمیم کنار نوشته‌های تازه می‌ماند', () => {
    const storage = new MemoryStorage()
    storage.setItem(draftKey(scope)!, JSON.stringify({ v: 1, savedAt: 1, rows: [row('old1'), row('old2')] }))
    let current: DraftRow[] = []
    const drafts = useGridDrafts({ scope: () => scope, collect: () => current, storage: () => storage })
    expect(drafts.check()).toBe(2)
    current = [row('new')]
    drafts.flush()
    expect(ids(storage)).toEqual(['new', 'old1', 'old2'])
  })

  it('ردیفی که همین حالا در جدول است دوباره پیشنهاد نمی‌شود', () => {
    const storage = new MemoryStorage()
    storage.setItem(draftKey(scope)!, JSON.stringify({ v: 1, savedAt: 1, rows: [row('a'), row('b')] }))
    const drafts = useGridDrafts({ scope: () => scope, collect: () => [], storage: () => storage })
    expect(drafts.check(new Set(['a']))).toBe(1)
    expect(drafts.pending.value?.map((r) => r.clientRowId)).toEqual(['b'])
  })

  it('«برگرداندن ردیف‌ها» ردیف‌ها را می‌دهد', () => {
    const storage = new MemoryStorage()
    storage.setItem(draftKey(scope)!, JSON.stringify({ v: 1, savedAt: 1, rows: [row('a')] }))
    const drafts = useGridDrafts({ scope: () => scope, collect: () => [], storage: () => storage })
    drafts.check()
    expect(drafts.takePending().map((r) => r.clientRowId)).toEqual(['a'])
    expect(drafts.pending.value).toBeNull()
  })

  it('«پاک کردن» محموله را به rugs:draft-discarded:* می‌برد و «برگردان» برش می‌گرداند', () => {
    const storage = new MemoryStorage()
    storage.setItem(draftKey(scope)!, JSON.stringify({ v: 1, savedAt: 1, rows: [row('a'), row('b')] }))
    let current: DraftRow[] = [row('fresh')]
    const drafts = useGridDrafts({ scope: () => scope, collect: () => current, storage: () => storage })
    drafts.check()
    expect(drafts.discardPending()).toBe(2)
    expect(ids(storage)).toEqual(['fresh'])
    expect(storage.data.has(discardedKey(scope)!)).toBe(true)

    const back = drafts.undoDiscard()
    expect(back?.map((r) => r.clientRowId)).toEqual(['a', 'b'])
    expect(ids(storage)).toEqual(['fresh', 'a', 'b'])
    expect(storage.data.has(discardedKey(scope)!)).toBe(false)
    current = []
  })
})
