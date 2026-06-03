<script setup lang="ts">
/**
 * قالب اصلی بعد از ورود: سایدبار دسکتاپ + منوی پایین موبایل + محتوای RouterView
 */
import { computed, ref } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const route = useRoute()
const mobileNavOpen = ref(false)

const hideBottomNav = computed(() => !!route.meta.hideBottomNav)

const navItems = computed(() => {
  const items = [
    { to: '/', icon: 'dashboard', label: 'داشبورد' },
    { to: '/rugs', icon: 'inventory_2', label: 'فرش‌ها' },
    { to: '/workflows', icon: 'account_tree', label: 'فرایندها' },
    { to: '/labels/designer', icon: 'sell', label: 'برچسب' },
    { to: '/rugs/new', icon: 'add_circle', label: 'ثبت فرش' },
  ]
  if (auth.isSystemAdmin) {
    items.push({ to: '/admin/tenants', icon: 'admin_panel_settings', label: 'مشتریان' })
  }
  return items
})

function isActive(path: string) {
  if (path === '/') return route.path === '/'
  return route.path.startsWith(path)
}
</script>

<template>
  <div class="min-h-dvh flex flex-col lg:flex-row bg-background">
  <aside
    class="hidden lg:flex flex-col fixed inset-y-0 start-0 w-[280px] bg-surface-container-lowest border-e border-outline-variant z-40"
  >
    <div class="p-6 border-b border-outline-variant">
      <h1 class="text-xl font-black text-primary">سامانه مدیریت فرش</h1>
      <p class="text-sm text-on-surface-variant mt-1">{{ auth.user?.tenantName || 'پنل مدیریت' }}</p>
    </div>
    <nav class="flex-1 p-3 space-y-1 overflow-y-auto">
      <RouterLink
        v-for="item in navItems"
        :key="item.to"
        :to="item.to"
        class="flex items-center gap-3 px-4 py-3 rounded-full transition-all text-base"
        :class="isActive(item.to)
          ? 'bg-secondary-container text-on-secondary-container font-medium shadow-sm'
          : 'text-on-surface-variant hover:bg-surface-container-highest'"
      >
        <span class="material-symbols-outlined" :class="{ fill: isActive(item.to) }">{{ item.icon }}</span>
        {{ item.label }}
      </RouterLink>
    </nav>
    <div class="p-4 border-t border-outline-variant">
      <p class="text-sm font-medium truncate">{{ auth.user?.fullName }}</p>
      <p class="text-xs text-on-surface-variant truncate">{{ auth.user?.email }}</p>
      <button
        type="button"
        class="mt-3 w-full py-2 text-sm text-error border border-outline-variant rounded-lg hover:bg-error-container"
        @click="auth.logout(); $router.push('/login')"
      >
        خروج
      </button>
    </div>
  </aside>

  <header class="lg:hidden sticky top-0 z-30 bg-surface-container-lowest/95 backdrop-blur border-b border-outline-variant px-4 py-3 flex items-center justify-between">
    <div>
      <h1 class="font-bold text-primary text-lg">مدیریت فرش</h1>
      <p class="text-xs text-on-surface-variant">{{ auth.user?.fullName }}</p>
    </div>
    <button type="button" class="p-2 rounded-lg hover:bg-surface-container" @click="mobileNavOpen = !mobileNavOpen">
      <span class="material-symbols-outlined">{{ mobileNavOpen ? 'close' : 'menu' }}</span>
    </button>
  </header>

  <div
    v-if="mobileNavOpen"
    class="lg:hidden fixed inset-0 z-20 bg-black/40"
    @click="mobileNavOpen = false"
  />
  <nav
    v-show="mobileNavOpen"
    class="lg:hidden fixed top-[57px] end-0 bottom-0 w-72 max-w-[85vw] bg-surface-container-lowest z-30 border-s border-outline-variant p-4 space-y-1 overflow-y-auto"
  >
    <RouterLink
      v-for="item in navItems"
      :key="item.to"
      :to="item.to"
      class="flex items-center gap-3 px-4 py-3 rounded-xl"
      :class="isActive(item.to) ? 'bg-secondary-container text-on-secondary-container' : ''"
      @click="mobileNavOpen = false"
    >
      <span class="material-symbols-outlined">{{ item.icon }}</span>
      {{ item.label }}
    </RouterLink>
  </nav>

  <main class="flex-1 lg:ms-[280px] safe-bottom" :class="hideBottomNav ? 'pb-4 lg:pb-6' : 'pb-20 lg:pb-6'">
    <div class="max-w-6xl mx-auto px-4 sm:px-6 py-4 sm:py-6">
      <RouterView />
    </div>
  </main>

  <nav v-if="!hideBottomNav" class="lg:hidden fixed bottom-0 inset-x-0 z-30 bg-surface-container-lowest border-t border-outline-variant safe-bottom flex justify-around py-2">
    <RouterLink
      v-for="item in navItems.slice(0, 4)"
      :key="item.to"
      :to="item.to"
      class="flex flex-col items-center gap-0.5 px-2 py-1 text-[10px] min-w-[64px]"
      :class="isActive(item.to) ? 'text-primary' : 'text-on-surface-variant'"
    >
      <span class="material-symbols-outlined text-[22px]" :class="{ fill: isActive(item.to) }">{{ item.icon }}</span>
      <span>{{ item.label }}</span>
    </RouterLink>
  </nav>
  </div>
</template>
