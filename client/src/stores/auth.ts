/**
 * وضعیت ورود کاربر (Pinia) — توکن و پروفایل در localStorage هم ذخیره می‌شود
 * تا با refresh صفحه از بین نرود.
 */
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { authApi } from '@/api/client'
import type { User } from '@/types'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('auth_token'))
  const user = ref<User | null>(
    localStorage.getItem('auth_user') ? JSON.parse(localStorage.getItem('auth_user')!) : null,
  )

  const isAuthenticated = computed(() => !!token.value)
  /** ادمین کل — منوی «مشتریان» را می‌بیند */
  const isSystemAdmin = computed(() => user.value?.role === 'SystemAdmin')
  /** کاربر وابسته به یک کارگاه */
  const isTenantUser = computed(() => !!user.value?.tenantId)

  async function login(email: string, password: string) {
    const res = await authApi.login(email, password)
    token.value = res.token
    user.value = res.user
    localStorage.setItem('auth_token', res.token)
    localStorage.setItem('auth_user', JSON.stringify(res.user))
  }

  function logout() {
    token.value = null
    user.value = null
    localStorage.removeItem('auth_token')
    localStorage.removeItem('auth_user')
  }

  /** بعد از بارگذاری اپ: اعتبار توکن را با /auth/me چک می‌کند */
  async function hydrate() {
    if (!token.value) return
    try {
      const res = await authApi.me()
      user.value = res.user
      localStorage.setItem('auth_user', JSON.stringify(res.user))
    } catch {
      logout()
    }
  }

  return { token, user, isAuthenticated, isSystemAdmin, isTenantUser, login, logout, hydrate }
})
