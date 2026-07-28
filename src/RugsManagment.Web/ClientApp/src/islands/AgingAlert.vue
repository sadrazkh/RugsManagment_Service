<script setup lang="ts">
/**
 * هشدار کوتاه فرش‌های گیرکرده، برای داشبورد.
 *
 * فقط وقتی چیزی برای گفتن هست رندر می‌شود — کارت خالیِ «۰ مورد» فضای
 * داشبورد را بی‌دلیل می‌گیرد و چشم را عادت می‌دهد نادیده بگیردش.
 */
import { computed, onMounted, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { api } from '@/lib/api'
import { faNumber } from '@/lib/format'

interface AgingItem {
  rugId: string
  sku: string
  title?: string
  stepName: string
  daysInStep: number
  severity: number
}

const items = ref<AgingItem[]>([])
const criticalCount = ref(0)
const seriousCount = ref(0)
const ready = ref(false)

/** فقط چند مورد بدترین؛ بقیه در صفحهٔ گزارش‌ها */
const top = computed(() => items.value.slice(0, 4))
const rest = computed(() => Math.max(0, items.value.length - top.value.length))

const severityCss = (s: number) =>
  s >= 3 ? 'text-on-error-container bg-error-container' : 'text-warning bg-warning/15'

onMounted(async () => {
  try {
    const data = await api.get<{ items: AgingItem[]; criticalCount: number; seriousCount: number }>(
      '/api/analytics/aging',
    )
    items.value = data.items
    criticalCount.value = data.criticalCount
    seriousCount.value = data.seriousCount
  } catch {
    /* داشبورد نباید به‌خاطر یک ویجت جانبی بشکند */
  } finally {
    ready.value = true
  }
})
</script>

<template>
  <section
    v-if="ready && items.length > 0"
    class="rounded-xl border p-5 shadow-sm"
    :class="criticalCount > 0 ? 'border-error/40 bg-error-container/40' : 'border-warning/40 bg-warning/10'"
  >
    <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
      <h2 class="flex items-center gap-2 text-sm font-semibold"
          :class="criticalCount > 0 ? 'text-on-error-container' : 'text-warning'">
        <AppIcon :name="criticalCount > 0 ? 'error' : 'warning'" class="h-4 w-4" />
        {{ faNumber(items.length) }} فرش گیر کرده است
      </h2>
      <a href="/Reports" class="inline-flex min-h-11 items-center gap-1 text-xs text-primary hover:underline">
        گزارش کامل
        <AppIcon name="arrow-left" class="h-3.5 w-3.5" />
      </a>
    </div>

    <ul class="space-y-1">
      <li v-for="item in top" :key="item.rugId">
        <a :href="`/Rugs/Details/${item.rugId}`"
           class="-mx-2 flex items-center gap-2 rounded-lg px-2 py-1.5 text-sm hover:bg-surface-container/60">
          <span class="shrink-0 rounded-full px-2 py-0.5 text-xs" :class="severityCss(item.severity)" data-numeric>
            {{ faNumber(item.daysInStep) }} روز
          </span>
          <span class="min-w-0 flex-1 truncate">{{ item.title || item.sku }}</span>
          <span class="shrink-0 truncate text-xs text-on-surface-variant">{{ item.stepName }}</span>
        </a>
      </li>
    </ul>

    <p v-if="rest > 0" class="mt-2 text-xs text-on-surface-variant" data-numeric>
      و {{ faNumber(rest) }} مورد دیگر…
    </p>
  </section>
</template>
