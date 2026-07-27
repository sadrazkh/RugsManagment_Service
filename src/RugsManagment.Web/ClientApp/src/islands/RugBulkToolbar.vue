<script setup lang="ts">
/**
 * نوار عملیات دسته‌ای روی لیست فرش‌ها.
 * چک‌باکس‌های [data-rug-select] در Razor رندر می‌شوند؛ این جزیره انتخاب‌ها را جمع کرده،
 * و امکان «پیشبرد گروهیِ مرحلهٔ بعد» و «افزودن به گروه» را می‌دهد.
 */
import { computed, onMounted, ref } from 'vue'
import { api } from '@/lib/api'
import { confirmDialog, toast, toastAfterReload } from '@/lib/ui'
import AppIcon from '@/components/AppIcon.vue'

interface Group { id: string; name: string }

const selected = ref<Set<string>>(new Set())
const count = computed(() => selected.value.size)
const groups = ref<Group[]>([])
const targetGroup = ref('')
const busy = ref(false)

function refreshSelection() {
  const set = new Set<string>()
  document.querySelectorAll<HTMLInputElement>('[data-rug-select]:checked').forEach((c) => set.add(c.dataset.rugSelect!))
  selected.value = set
}

function clearAll() {
  document.querySelectorAll<HTMLInputElement>('[data-rug-select]').forEach((c) => (c.checked = false))
  refreshSelection()
}

async function bulkAdvance() {
  if (count.value === 0) return

  const ok = await confirmDialog({
    title: `${count.value} فرش به مرحلهٔ بعد بروند؟`,
    message: 'فرش‌هایی که مرحلهٔ فعالی ندارند نادیده گرفته می‌شوند.',
    confirmLabel: 'پیشبرد همه',
  })
  if (!ok) return

  await run(async () => {
    const res = await api.post<{ successCount: number; failedCount: number }>('/api/rugs/bulk/advance', {
      rugIds: [...selected.value],
      step: {
        serviceProviderId: null, manualCostOverride: null, pricingModel: null, unitRate: null,
        pricingConfigJson: null, fieldValuesJson: null, notes: null, markCompleted: true, adjustment: null,
      },
    })

    if (res.failedCount) {
      toastAfterReload(`${res.successCount} فرش پیش رفت، ${res.failedCount} ناموفق بود.`, 'warning')
    } else {
      toastAfterReload(`${res.successCount} فرش پیش رفت.`)
    }
  })
}

async function addToGroup() {
  if (count.value === 0 || !targetGroup.value) return
  const moved = count.value
  await run(async () => {
    await api.post(`/api/batches/${targetGroup.value}/rugs`, { rugIds: [...selected.value] })
    toastAfterReload(`${moved} فرش به گروه اضافه شد.`)
  })
}

/** رفتن به صفحهٔ چاپ با فرش‌های انتخاب‌شده — بدون تغییر داده، پس تأیید لازم نیست. */
function printLabels() {
  if (count.value === 0) return
  const params = new URLSearchParams()
  for (const id of selected.value) params.append('rugIds', id)
  window.location.href = `/Labels/Print?${params}`
}

async function run(fn: () => Promise<void>) {
  busy.value = true
  try {
    await fn()
    // پیام موفقیت در sessionStorage است و بعد از بارگذاری روی دادهٔ تازه دیده می‌شود
    window.location.reload()
  } catch (e) {
    toast.error((e as Error).message)
    busy.value = false
  }
}

onMounted(async () => {
  document
    .querySelectorAll<HTMLInputElement>('[data-rug-select]')
    .forEach((c) => c.addEventListener('change', refreshSelection))
  try {
    groups.value = await api.get<Group[]>('/api/batches')
  } catch {
    /* اگر گروهی نبود، انتخابگر خالی می‌ماند */
  }
})
</script>

<template>
  <Transition
    enter-active-class="transition duration-200 ease-out"
    enter-from-class="translate-y-full"
    leave-active-class="transition duration-150 ease-in"
    leave-to-class="translate-y-full"
  >
    <div
      v-if="count > 0"
      class="pb-safe fixed inset-x-0 bottom-0 z-40 border-t border-outline-variant bg-surface-container-lowest/95 px-4 pt-3 shadow-lg backdrop-blur"
      role="region"
      aria-label="عملیات دسته‌ای"
      data-no-print
    >
      <div class="mx-auto flex max-w-[1440px] flex-wrap items-center gap-3">
        <span class="font-bold text-primary" aria-live="polite">{{ count }} انتخاب‌شده</span>
        <button
          type="button"
          class="inline-flex min-h-11 items-center gap-1 rounded-lg px-2 text-sm text-on-surface-variant hover:bg-surface-container"
          @click="clearAll"
        >
          <AppIcon name="close" class="h-4 w-4" />
          لغو انتخاب
        </button>

        <div class="flex-1"></div>

        <label class="sr-only" for="bulk-group">گروه مقصد</label>
        <select
          id="bulk-group"
          v-model="targetGroup"
          class="min-h-11 rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-sm outline-none focus:border-primary"
        >
          <option value="">— گروه —</option>
          <option v-for="g in groups" :key="g.id" :value="g.id">{{ g.name }}</option>
        </select>

        <button
          type="button"
          :disabled="busy || !targetGroup"
          class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-4 text-sm hover:bg-surface-container"
          @click="addToGroup"
        >
          <AppIcon name="package" class="h-4 w-4" />
          افزودن به گروه
        </button>

        <button
          type="button"
          class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-4 text-sm hover:bg-surface-container"
          @click="printLabels"
        >
          <AppIcon name="printer" class="h-4 w-4" />
          چاپ برچسب
        </button>

        <button
          type="button"
          :disabled="busy"
          class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover"
          @click="bulkAdvance"
        >
          <AppIcon name="check" class="h-4 w-4" />
          پیشبرد مرحلهٔ بعد
        </button>
      </div>
    </div>
  </Transition>
</template>
