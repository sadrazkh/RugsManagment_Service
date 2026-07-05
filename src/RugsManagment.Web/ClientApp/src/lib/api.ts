/**
 * کلاینت سبک برای فراخوانی APIهای هم‌مبدأ (same-origin).
 * توکن CSRF را از meta صفحه خوانده و در هدر X-CSRF-TOKEN می‌فرستد (هماهنگ با AutoValidateAntiforgeryToken بک‌اند).
 * کوکی احراز هویت خودکار همراه درخواست می‌رود.
 */
function csrfToken(): string {
  return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') ?? ''
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: {
      'Content-Type': 'application/json',
      'X-CSRF-TOKEN': csrfToken(),
    },
    credentials: 'same-origin',
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (!res.ok) {
    let message = 'خطا در ارتباط با سرور'
    try {
      const data = await res.json()
      if (data?.message) message = data.message
    } catch {
      /* پاسخ بدون بدنه */
    }
    throw new Error(message)
  }

  if (res.status === 204) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(url: string) => request<T>('GET', url),
  post: <T>(url: string, body?: unknown) => request<T>('POST', url, body),
  put: <T>(url: string, body?: unknown) => request<T>('PUT', url, body),
  del: <T>(url: string) => request<T>('DELETE', url),
}
