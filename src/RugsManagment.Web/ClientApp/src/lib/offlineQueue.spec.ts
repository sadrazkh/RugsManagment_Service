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

describe('بازپخش: نشست نامعتبر صف را پاک نمی‌کند (یافتهٔ بازبینی فاز ۱)', () => {
  vi.stubGlobal('document', { querySelector: () => null, getElementById: () => null })

  const json = (status: number, body: unknown) =>
    new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

  /** fetch ساختگی: `/api/me` همان کاربر (یا `me`)، بقیه به ترتیب از `replies`. */
  function serve(replies: Response[], me: unknown = { userId: USER_1, tenantId: TENANT_A }) {
    const sent: string[] = []
    const fetchMock = vi.fn(async (url: string) => {
      if (url === '/api/me') return me instanceof Response ? me : json(200, me)
      sent.push(url)
      const next = replies.shift()
      if (!next) throw new TypeError('Failed to fetch')
      return next
    })
    vi.stubGlobal('fetch', fetchMock)
    return sent
  }

  async function queueOf(...urls: string[]) {
    const q = await import('./offlineQueue')
    store.clear()
    signIn(TENANT_A, USER_1)
    q.reloadQueue()
    for (const url of urls) q.enqueue({ url, method: 'POST', body: { x: 1 }, label: `کار ${url}` })
    return q
  }

  it('۴۰۱ (کوکی منقضی): می‌ایستد، هیچ کاری حذف نمی‌شود و «دوباره وارد شوید»', async () => {
    const q = await queueOf('/api/a', '/api/b')
    const sent = serve([json(401, undefined)])

    const result = await q.flush()

    expect(result.signInRequired).toBe(true)
    expect(result.rejected).toEqual([])
    expect(result.remaining).toBe(2)
    expect(q.queue.value.map((a) => a.url)).toEqual(['/api/a', '/api/b'])
    expect(sent).toEqual(['/api/a']) // دومی اصلاً فرستاده نشد
    expect(JSON.parse(store.getItem(q.queueStorageKey()!)!)).toHaveLength(2)
  })

  it('۴۰۳ هم صف را نگه می‌دارد', async () => {
    const q = await queueOf('/api/a')
    serve([json(403, undefined)])
    const result = await q.flush()
    expect(result.signInRequired).toBe(true)
    expect(q.queue.value).toHaveLength(1)
  })

  it.each([
    ['بی‌بدنه', undefined],
    ['ProblemDetails بی‌پیام', { type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1', title: 'Bad Request', status: 400 }],
  ])('۴۰۰ رد توکن ضد CSRF (%s) صف را نگه می‌دارد', async (_name, body) => {
    const q = await queueOf('/api/a')
    serve([json(400, body)])
    const result = await q.flush()
    expect(result.signInRequired).toBe(true)
    expect(result.rejected).toEqual([])
    expect(q.queue.value).toHaveLength(1)
  })

  it('کاربر دیگری در همین مرورگر وارد شده: هیچ کاری به نام او فرستاده نمی‌شود', async () => {
    const q = await queueOf('/api/a')
    const sent = serve([json(200, {})], { userId: USER_2, tenantId: TENANT_A })
    const result = await q.flush()
    expect(sent).toEqual([])
    expect(result.signInRequired).toBe(true)
    expect(q.queue.value).toHaveLength(1)
  })

  it('خطای اعتبارسنجی با پیام: فقط همان کار با پیام دیدنی حذف می‌شود و بقیه فرستاده می‌شوند', async () => {
    const q = await queueOf('/api/a', '/api/b')
    serve([json(400, { message: 'این مرحله قبلاً بسته شده است.' }), json(200, {})])
    const result = await q.flush()
    expect(result.rejected).toEqual([{ label: 'کار /api/a', message: 'این مرحله قبلاً بسته شده است.' }])
    expect(result.sent).toBe(1)
    expect(result.signInRequired).toBe(false)
    expect(q.queue.value).toHaveLength(0)
  })

  it('۵۰۰: مثل قطعی شبکه می‌ایستد و صف می‌ماند (بی پیام ورود)', async () => {
    const q = await queueOf('/api/a', '/api/b')
    serve([json(503, undefined)])
    const result = await q.flush()
    expect(result.signInRequired).toBe(false)
    expect(result.rejected).toEqual([])
    expect(q.queue.value).toHaveLength(2)
    expect(q.queue.value[0].attempts).toBe(1)
  })

  it.each([
    [200, null, 'sent'],
    [204, null, 'sent'],
    [401, null, 'stop-sign-in'],
    [403, { message: 'این کار برای شما مجاز نیست.' }, 'stop-sign-in'],
    [400, null, 'stop-sign-in'],
    [400, { title: 'Bad Request' }, 'stop-sign-in'],
    [400, { message: 'چند خانه درست پر نشده.', errors: { a: ['x'] } }, 'drop'],
    [404, { message: 'پیدا نشد.' }, 'drop'],
    [409, { message: 'قبلاً بسته شده.' }, 'drop'],
    [429, null, 'stop-retry'],
    [500, null, 'stop-retry'],
  ] as const)('classifyReplay(%i, %j) → %s', async (status, body, expected) => {
    const q = await import('./offlineQueue')
    expect(q.classifyReplay(status, body)).toBe(expected)
  })
})
