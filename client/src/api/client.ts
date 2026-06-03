/**
 * کلاینت HTTP مرکزی — همهٔ صفحات از همین axios استفاده می‌کنند.
 * - آدرس پایه: /api (در dev از vite proxy به بک‌اند می‌رود)
 * - توکن JWT از localStorage روی هر درخواست چسبانده می‌شود
 * - اگر 401 بیاید کاربر به صفحه login هدایت می‌شود
 */
import axios from 'axios'
import type { AuthResponse } from '@/types'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  headers: { 'Content-Type': 'application/json' },
})

// قبل از هر درخواست: هدر Authorization
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('auth_token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// اگر توکن منقضی یا نامعتبر باشد
api.interceptors.response.use(
  (r) => r,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('auth_token')
      localStorage.removeItem('auth_user')
      if (!window.location.pathname.startsWith('/login')) {
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  },
)

/** فقط endpointهای ورود — بقیه از default export استفاده کنید */
export const authApi = {
  login: (email: string, password: string) =>
    api.post<AuthResponse>('/auth/login', { email, password }).then((r) => r.data),
  me: () => api.get<AuthResponse>('/auth/me').then((r) => r.data),
}

export default api
