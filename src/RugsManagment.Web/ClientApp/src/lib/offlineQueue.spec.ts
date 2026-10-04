import { beforeEach, describe, expect, it, vi } from 'vitest'
import { resetAppContext } from './context'

/** localStorage ساده در حافظه (محیط آزمون node است). */
class MemoryStorage implements Storage {
  private map = new Map<string, string>()
  get length() {
    return this.map.size
  }
  clear() {
    this.map.clear()
  }
  getItem(key: string) {
    return this.map.has(key) ? (this.map.get(key) as string) : null
  }
  key(index: number) {
    return [...this.map.keys()][index] ?? null
  }
  removeItem(key: string) {
    this.map.delete(key)
  }
  setItem(key: string, value: string) {
    this.map.set(key, String(value))
  }
}

const store = new MemoryStorage()
vi.stubGlobal('localStorage', store)
vi.stubGlobal('navigator', { onLine: true })

const TENANT_A = '11111111-1111-4111-8111-111111111111'
const TENANT_B = '22222222-2222-4222-8222-222222222222'
const USER_1 = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
const USER_2 = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'

function signIn(tenantId: string | null, userId: string | null) {
  resetAppContext({ tenantId, userId })
}

describe('صف آفلاین به ازای کارگاه و کاربر', () => {
  beforeEach(() => store.clear())

  it('کلید از app-context ساخته می‌شود', async () => {
    const q = await import('./offlineQueue')
    signIn(TENANT_A, USER_1)
    expect(q.queueStorageKey()).toBe(`rugs-offline-queue:${TENANT_A}:${USER_1}`)
    signIn(null, null)
    expect(q.queueStorageKey()).toBeNull()
  })

  it('کار معلق یک کاربر به کاربر دیگر (یا کارگاه دیگر) نمی‌رسد', async () => {
    const q = await import('./offlineQueue')
    signIn(TENANT_A, USER_1)
    q.reloadQueue()
    q.enqueue({ url: '/api/x', method: 'POST', body: { a: 1 }, label: 'یک کار' })
    expect(q.queue.value).toHaveLength(1)

    signIn(TENANT_A, USER_2)
    q.reloadQueue()
    expect(q.queue.value).toHaveLength(0)

    signIn(TENANT_B, USER_1)
    q.reloadQueue()
    expect(q.queue.value).toHaveLength(0)

    signIn(TENANT_A, USER_1)
    q.reloadQueue()
    expect(q.queue.value).toHaveLength(1)
    expect(q.queue.value[0].label).toBe('یک کار')
  })

  it('صف قدیمیِ بی‌نام دور ریخته می‌شود و بازپخش نمی‌شود', async () => {
    const q = await import('./offlineQueue')
    store.setItem('rugs-offline-queue', JSON.stringify([{ id: 'x', url: '/api/old', method: 'POST', body: null, label: 'قدیمی', createdAt: 0, attempts: 0 }]))
    signIn(TENANT_A, USER_1)
    q.reloadQueue()
    expect(q.queue.value).toHaveLength(0)
    expect(store.getItem('rugs-offline-queue')).toBeNull()
  })

  it('بی‌کاربر فقط در حافظه', async () => {
    const q = await import('./offlineQueue')
    signIn(null, null)
    q.reloadQueue()
    q.enqueue({ url: '/api/x', method: 'PUT', body: null, label: 'موقت' })
    expect(q.queue.value).toHaveLength(1)
    expect(store.length).toBe(0)
    q.clear()
  })

  it('خالی شدن صف کلید را پاک می‌کند', async () => {
    const q = await import('./offlineQueue')
    signIn(TENANT_A, USER_1)
    q.reloadQueue()
    const item = q.enqueue({ url: '/api/x', method: 'DELETE', body: null, label: 'حذف' })
    expect(store.getItem(q.queueStorageKey()!)).not.toBeNull()
    q.remove(item.id)
    expect(store.getItem(q.queueStorageKey()!)).toBeNull()
  })
})
