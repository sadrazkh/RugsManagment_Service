import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, api, errorMessage, fileNameFromDisposition } from './api'
import { coreMessages } from './messages'

interface Call {
  url: string
  init: RequestInit
}

function mockFetch(status: number, body: unknown, headers: Record<string, string> = {}) {
  const calls: Call[] = []
  vi.stubGlobal('fetch', async (url: string, init: RequestInit) => {
    calls.push({ url, init })
    const text = body === undefined ? '' : typeof body === 'string' ? body : JSON.stringify(body)
    return new Response(status === 204 ? null : text, { status, headers })
  })
  return calls
}

afterEach(() => vi.unstubAllGlobals())

describe('api', () => {
  it('خطای اعتبارسنجی errors را نگه می‌دارد (کلیدها camelCase)', async () => {
    mockFetch(400, { message: 'چند خانه درست پر نشده', errors: { WidthMeters: ['عرض را بنویسید'], '$.lengthMeters': ['طول'] } })
    const error = await api.post('/api/rugs', {}).catch((e: unknown) => e)
    expect(error).toBeInstanceOf(ApiError)
    const e = error as ApiError
    expect(e.status).toBe(400)
    expect(e.message).toBe('چند خانه درست پر نشده')
    expect(e.errors).toEqual({ widthMeters: ['عرض را بنویسید'], lengthMeters: ['طول'] })
    expect(e.fieldError('WidthMeters')).toBe('عرض را بنویسید')
    expect(e.fieldError('lengthMeters')).toBe('طول')
    expect((error as Error).message).toBe('چند خانه درست پر نشده')
  })

  it('تعارض field و کلیدهای اضافه را نگه می‌دارد', async () => {
    mockFetch(409, { message: 'شمارهٔ ۳۷۷۸ تکراری است', field: 'rug.serial', nextFree: 4005 })
    const e = (await api.put('/api/rugs/1', {}).catch((x: unknown) => x)) as ApiError
    expect(e.field).toBe('rug.serial')
    expect(e.body?.nextFree).toBe(4005)
  })

  it('۴۰۳ بی‌بدنه پیام فارسی پیش‌فرض می‌گیرد', async () => {
    mockFetch(403, '')
    const e = (await api.get('/api/analytics').catch((x: unknown) => x)) as ApiError
    expect(e.status).toBe(403)
    expect(e.message).toBe(coreMessages.E_FORBIDDEN)
  })

  it('DELETE با بدنه', async () => {
    const calls = mockFetch(204, undefined)
    await api.del('/api/batches/1/rugs', { rugIds: ['a'] })
    expect(calls[0].init.method).toBe('DELETE')
    expect(calls[0].init.body).toBe(JSON.stringify({ rugIds: ['a'] }))
    expect((calls[0].init.headers as Record<string, string>)['Content-Type']).toBe('application/json')
  })

  it('postForm بدنهٔ multipart بی Content-Type دستی', async () => {
    const calls = mockFetch(200, { id: 'x' })
    const form = new FormData()
    form.append('file', new Blob(['a']), 'a.xlsx')
    const result = await api.postForm<{ id: string }>('/api/imports', form)
    expect(result).toEqual({ id: 'x' })
    expect(calls[0].init.body).toBe(form)
    expect((calls[0].init.headers as Record<string, string>)['Content-Type']).toBeUndefined()
  })

  it('قطعی شبکه همان TypeError می‌ماند (صف آفلاین روی آن تکیه دارد)', async () => {
    vi.stubGlobal('fetch', async () => {
      throw new TypeError('Failed to fetch')
    })
    const error = await api.get('/api/x').catch((e: unknown) => e)
    expect(error).toBeInstanceOf(TypeError)
    expect(errorMessage(error)).toBe(coreMessages.E_NETWORK)
  })

  it('errorMessage هرگز متن انگلیسی نمی‌دهد', () => {
    expect(errorMessage(new Error('boom'))).toBe(coreMessages.E_SERVER)
    expect(errorMessage(new ApiError(500, 'کار انجام نشد', null))).toBe('کار انجام نشد')
  })
})

describe('نام فایل دانلود', () => {
  it('RFC 5987 مقدم است', () => {
    expect(
      fileNameFromDisposition(
        "attachment; filename=\"export.xlsx\"; filename*=UTF-8''%D9%85%D8%AD%D9%85%D9%88%D9%84%D9%87.xlsx",
      ),
    ).toBe('محموله.xlsx')
  })

  it('filename ساده', () => {
    expect(fileNameFromDisposition('attachment; filename="sales.csv"')).toBe('sales.csv')
    expect(fileNameFromDisposition('attachment; filename=sales.csv')).toBe('sales.csv')
    expect(fileNameFromDisposition(null)).toBeNull()
  })
})
