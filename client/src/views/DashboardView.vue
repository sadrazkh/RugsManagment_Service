<script setup lang="ts">
/** داشبورد — آمار از GET /api/dashboard (محاسبات در بک‌اند) */
import { onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import api from '@/api/client'
import StatCard from '@/components/StatCard.vue'
import type { DashboardStats } from '@/types'
import { formatCurrency, formatNumber, statusLabel } from '@/utils/format'

const stats = ref<DashboardStats | null>(null)
const loading = ref(true)

onMounted(async () => {
  try {
    const { data } = await api.get<DashboardStats>('/dashboard')
    stats.value = data
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-6">
    <header class="flex flex-col sm:flex-row sm:items-end justify-between gap-4">
      <div>
        <h2 class="text-2xl sm:text-3xl font-bold text-on-surface">داشبورد</h2>
        <p class="text-on-surface-variant text-sm mt-1">نمای کلی کارگاه و گردش فرش‌ها</p>
      </div>
      <RouterLink
        to="/rugs/new"
        class="inline-flex items-center justify-center gap-2 px-5 py-2.5 bg-primary text-on-primary rounded-full font-medium text-sm hover:bg-primary-container transition-colors"
      >
        <span class="material-symbols-outlined text-lg">add</span>
        ثبت فرش جدید
      </RouterLink>
    </header>

    <div v-if="loading" class="grid grid-cols-2 lg:grid-cols-4 gap-3">
      <div v-for="i in 4" :key="i" class="h-28 rounded-xl bg-surface-container animate-pulse" />
    </div>

    <div v-else-if="stats" class="grid grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
      <StatCard label="کل فرش‌ها" :value="formatNumber(stats.totalRugs)" icon="inventory_2" />
      <StatCard label="در جریان" :value="formatNumber(stats.inProgress)" icon="sync" accent="secondary" />
      <StatCard label="آماده فروش" :value="formatNumber(stats.readyForSale)" icon="sell" accent="tertiary" />
      <StatCard label="سرمایه در جریان" :value="formatCurrency(stats.pipelineValue)" icon="payments" />
    </div>

    <div v-if="stats" class="grid lg:grid-cols-2 gap-4">
      <section class="bg-surface-container-lowest border border-outline-variant rounded-xl p-4 sm:p-5">
        <h3 class="font-semibold mb-4 flex items-center gap-2">
          <span class="material-symbols-outlined text-secondary">history</span>
          آخرین فرش‌ها
        </h3>
        <ul class="space-y-3">
          <li v-for="rug in stats.recentRugs" :key="rug.id">
            <RouterLink
              :to="`/rugs/${rug.id}`"
              class="flex items-center justify-between p-3 rounded-lg hover:bg-surface-container-low transition-colors"
            >
              <div>
                <p class="font-mono text-sm text-on-surface-variant">{{ rug.sku }}</p>
                <p class="font-medium">{{ rug.title || 'بدون عنوان' }}</p>
                <p class="text-xs text-on-surface-variant">{{ rug.currentStepName || statusLabel(rug.status) }}</p>
              </div>
              <span class="text-sm font-medium">{{ formatCurrency(rug.totalInvestment) }}</span>
            </RouterLink>
          </li>
          <li v-if="!stats.recentRugs.length" class="text-sm text-on-surface-variant text-center py-6">
            هنوز فرشی ثبت نشده
          </li>
        </ul>
      </section>

      <section class="bg-surface-container-lowest border border-outline-variant rounded-xl p-4 sm:p-5">
        <h3 class="font-semibold mb-4 flex items-center gap-2">
          <span class="material-symbols-outlined text-primary">donut_large</span>
          توزیع مراحل فعال
        </h3>
        <ul class="space-y-3">
          <li v-for="item in stats.stepDistribution" :key="item.stepName" class="flex items-center gap-3">
            <span class="text-sm flex-1">{{ item.stepName }}</span>
            <div class="flex-1 h-2 bg-surface-container rounded-full overflow-hidden max-w-[140px]">
              <div
                class="h-full bg-secondary rounded-full"
                :style="{ width: `${Math.min(100, item.count * 25)}%` }"
              />
            </div>
            <span class="text-sm font-mono w-6 text-end">{{ item.count }}</span>
          </li>
          <li v-if="!stats.stepDistribution.length" class="text-sm text-on-surface-variant text-center py-6">
            مرحله فعالی وجود ندارد
          </li>
        </ul>
      </section>
    </div>
  </div>
</template>
