<script setup lang="ts">
/**
 * نشانگر وضعیت اتصال و صف عملیات معلق.
 *
 * فقط وقتی چیزی برای گفتن هست دیده می‌شود — نوار دائمیِ «آنلاین» فضا می‌گیرد
 * و چشم را عادت می‌دهد نادیده بگیرد.
 */
import { computed, ref } from 'vue'
import AppIcon from './AppIcon.vue'
import { faNumber } from '@/lib/format'
import { clear, flush, isOnline, queue } from '@/lib/offlineQueue'
import { toast } from '@/lib/ui'

const expanded = ref(false)
const sending = ref(false)

const count = computed(() => queue.value.length)
const visible = computed(() => !isOnline.value || count.value > 0)

async function retryNow() {
  if (!navigator.onLine) {
    toast.warning('هنوز اینترنت وصل نیست.')
    return
  }

  sending.value = true
  try {
    const result = await flush()
    if (result.sent > 0) toast.success(`${faNumber(result.sent)} عملیات ارسال شد.`)
    for (const r of result.rejected) toast.error(`${r.label}: ${r.message}`)
    if (result.sent > 0) window.setTimeout(() => window.location.reload(), 900)
  } finally {
    sending.value = false
  }
}

async function discard() {
  clear()
  toast.info('صف پاک شد.')
  expanded.value = false
}
</script>

<template>
  <div v-if="visible" class="fixed bottom-4 left-4 z-[55] max-w-sm" data-no-print>
    <div class="overflow-hidden rounded-xl border shadow-lg"
         :class="isOnline ? 'border-warning/40 bg-warning/10' : 'border-outline-variant bg-surface-container-highest'">
      <button
        type="button"
        class="flex min-h-11 w-full items-center gap-2 px-4 text-start text-sm"
        :aria-expanded="expanded"
        @click="expanded = !expanded"
      >
        <AppIcon :name="isOnline ? 'warning' : 'info'" class="h-5 w-5 shrink-0"
                 :class="isOnline ? 'text-warning' : 'text-on-surface-variant'" />

        <span class="flex-1" data-numeric>
          <template v-if="!isOnline && count === 0">اینترنت قطع است</template>
          <template v-else-if="!isOnline">اینترنت قطع — {{ faNumber(count) }} کار در صف</template>
          <template v-else>{{ faNumber(count) }} کار در انتظار ارسال</template>
        </span>

        <AppIcon :name="expanded ? 'chevron-down' : 'chevron-up'" class="h-4 w-4 shrink-0 text-on-surface-variant" />
      </button>

      <div v-if="expanded" class="border-t border-outline-variant px-4 py-3">
        <p v-if="count === 0" class="text-xs text-on-surface-variant">
          کاری در صف نیست. کارهایی که در حالت آفلاین انجام دهید اینجا می‌مانند.
        </p>

        <template v-else>
          <ul class="mb-3 space-y-1 text-xs text-on-surface-variant">
            <li v-for="a in queue" :key="a.id" class="flex items-center gap-2">
              <AppIcon name="info" class="h-3.5 w-3.5 shrink-0" />
              <span class="truncate">{{ a.label }}</span>
            </li>
          </ul>

          <div class="flex flex-wrap gap-2">
            <button type="button" :disabled="sending || !isOnline"
                    class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-3 text-xs font-semibold text-on-primary hover:bg-primary-hover"
                    @click="retryNow">
              <AppIcon name="check" class="h-4 w-4" />
              ارسال حالا
            </button>
            <button type="button"
                    class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-3 text-xs text-error hover:bg-error-container"
                    @click="discard">
              <AppIcon name="trash" class="h-4 w-4" />
              دور انداختن
            </button>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>
