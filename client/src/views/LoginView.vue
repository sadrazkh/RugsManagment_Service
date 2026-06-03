<script setup lang="ts">
/** صفحهٔ ورود — توکن را در store و localStorage می‌گذارد */
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const email = ref('demo@rugsystem.local')
const password = ref('Demo@12345')
const error = ref('')
const loading = ref(false)

async function submit() {
  error.value = ''
  loading.value = true
  try {
    await auth.login(email.value, password.value)
    router.push('/')
  } catch {
    error.value = 'ایمیل یا رمز عبور نادرست است'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="min-h-dvh flex items-center justify-center p-4 bg-background card-texture">
    <div class="w-full max-w-md bg-surface-container-lowest border border-outline-variant rounded-2xl p-6 sm:p-8 shadow-lg">
      <div class="text-center mb-8">
        <span class="material-symbols-outlined text-5xl text-primary">texture</span>
        <h1 class="text-2xl font-black text-primary mt-3">سامانه مدیریت فرش</h1>
        <p class="text-on-surface-variant text-sm mt-1">از تولید تا آماده‌سازی برای فروش</p>
      </div>
      <form class="space-y-4" @submit.prevent="submit">
        <div>
          <label class="block text-sm font-medium mb-1">ایمیل</label>
          <input
            v-model="email"
            type="email"
            required
            class="w-full px-4 py-3 rounded-lg border border-outline-variant bg-surface-container-low focus:outline-none focus:ring-2 focus:ring-primary/30"
          />
        </div>
        <div>
          <label class="block text-sm font-medium mb-1">رمز عبور</label>
          <input
            v-model="password"
            type="password"
            required
            class="w-full px-4 py-3 rounded-lg border border-outline-variant bg-surface-container-low focus:outline-none focus:ring-2 focus:ring-primary/30"
          />
        </div>
        <p v-if="error" class="text-sm text-error">{{ error }}</p>
        <button
          type="submit"
          :disabled="loading"
          class="w-full py-3 bg-primary text-on-primary rounded-full font-semibold hover:bg-primary-container transition-colors disabled:opacity-60"
        >
          {{ loading ? 'در حال ورود...' : 'ورود' }}
        </button>
      </form>
      <p class="text-xs text-on-surface-variant text-center mt-6">
        دمو: demo@rugsystem.local / Demo@12345
        <br />
        ادمین: admin@rugsystem.local / Admin@12345
      </p>
    </div>
  </div>
</template>
