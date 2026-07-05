<script setup lang="ts">
/**
 * نوار عملیات دسته‌ای روی لیست فرش‌ها.
 * چک‌باکس‌های [data-rug-select] در Razor رندر می‌شوند؛ این جزیره انتخاب‌ها را جمع کرده،
 * و امکان «پیشبرد گروهیِ مرحلهٔ بعد» و «افزودن به گروه» را می‌دهد.
 */
import { computed, onMounted, ref } from 'vue'
import { api } from '@/lib/api'

interface Group { id: string; name: string }

const selected = ref<Set<string>>(new Set())
const count = computed(() => selected.value.size)
const groups = ref<Group[]>([])
const targetGroup = ref('')
const busy = ref(false)
const error = ref('')

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
  if (!confirm(`${count.value} فرش به مرحلهٔ بعد بروند؟ (فرش‌های بدون مرحلهٔ فعال نادیده گرفته می‌شوند)`)) return
  await run(async () => {
    const res = await api.post<{ successCount: number; failedCount: number }>('/api/rugs/bulk/advance', {
      rugIds: [...selected.value],
      step: { serviceProviderId: null, manualCostOverride: null, pricingModel: null, unitRate: null, pricingConfigJson: null, fieldValuesJson: null, notes: null, markCompleted: true, adjustment: null },
    })
    alert(`${res.successCount} فرش پیش رفت` + (res.failedCount ? `، ${res.failedCount} ناموفق` : ''))
  })
}

async function addToGroup() {
  if (count.value === 0 || !targetGroup.value) return
  await run(async () => {
    await api.post(`/api/batches/${targetGroup.value}/rugs`, { rugIds: [...selected.value] })
    alert(`${count.value} فرش به گروه اضافه شد.`)
  })
}

async function run(fn: () => Promise<void>) {
  busy.value = true; error.value = ''
  try { await fn(); window.location.reload() }
  catch (e) { error.value = (e as Error).message; alert(error.value); busy.value = false }
}

onMounted(async () => {
  document.querySelectorAll<HTMLInputElement>('[data-rug-select]').forEach((c) => c.addEventListener('change', refreshSelection))
  try { groups.value = await api.get<Group[]>('/api/batches') } catch { /* */ }
})
</script>

<template>
  <transition name="slide-up">
    <div v-if="count > 0"
         class="fixed inset-x-0 bottom-0 z-40 border-t border-outline-variant bg-white/95 px-4 py-3 shadow-lg backdrop-blur">
      <div class="mx-auto flex max-w-[1440px] flex-wrap items-center gap-3">
        <span class="font-bold text-primary">{{ count }} انتخاب‌شده</span>
        <button @click="clearAll" class="text-sm text-on-surface-variant hover:underline">لغو</button>
        <div class="flex-1"></div>
        <select v-model="targetGroup" class="rounded-lg border border-outline-variant px-3 py-2 text-sm outline-none focus:border-primary">
          <option value="">— گروه —</option>
          <option v-for="g in groups" :key="g.id" :value="g.id">{{ g.name }}</option>
        </select>
        <button :disabled="busy || !targetGroup" @click="addToGroup"
                class="rounded-lg border border-outline-variant px-4 py-2 text-sm hover:bg-surface-container disabled:opacity-50">افزودن به گروه</button>
        <button :disabled="busy" @click="bulkAdvance"
                class="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-on-primary hover:opacity-90 disabled:opacity-50">پیشبرد مرحلهٔ بعد</button>
      </div>
    </div>
  </transition>
</template>

<style scoped>
.slide-up-enter-active, .slide-up-leave-active { transition: transform 0.2s ease; }
.slide-up-enter-from, .slide-up-leave-to { transform: translateY(100%); }
</style>
