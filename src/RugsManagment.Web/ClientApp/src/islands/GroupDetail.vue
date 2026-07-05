<script setup lang="ts">
/** مدیریت یک گروه: فرش‌های داخل گروه، افزودن/حذف فرش، و پیشبرد گروهی مرحلهٔ جاری. */
import { computed, onMounted, ref } from 'vue'
import { api } from '@/lib/api'
import { formatThousands } from '@/lib/money'
import type { Rug } from '@/lib/types'

const props = defineProps<{ groupId: string; groupName: string }>()

const allRugs = ref<Rug[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const info = ref('')
const addPick = ref<Record<string, boolean>>({})

const statusLabel: Record<number, string> = { 0: 'پیش‌نویس', 1: 'در جریان', 2: 'آماده', 3: 'فروخته', 4: 'بایگانی' }

const inGroup = computed(() => allRugs.value.filter((r) => r.batchId === props.groupId))
const unassigned = computed(() => allRugs.value.filter((r) => !r.batchId))
const advanceable = computed(() => inGroup.value.filter((r) => r.workflowSteps.some((s) => s.status === 1)))

async function load() {
  loading.value = true
  try { allRugs.value = await api.get<Rug[]>('/api/rugs') }
  catch (e) { error.value = (e as Error).message }
  finally { loading.value = false }
}

async function addSelected() {
  const ids = Object.keys(addPick.value).filter((k) => addPick.value[k])
  if (ids.length === 0) return
  await run(async () => {
    await api.post(`/api/batches/${props.groupId}/rugs`, { rugIds: ids })
    addPick.value = {}
    info.value = `${ids.length} فرش به گروه اضافه شد.`
  })
}

async function removeOne(id: string) {
  await run(async () => {
    await fetchDelete(`/api/batches/${props.groupId}/rugs`, { rugIds: [id] })
    info.value = 'فرش از گروه خارج شد.'
  })
}

// DELETE با بدنه (api.del بدنه نمی‌فرستد)
async function fetchDelete(url: string, body: unknown) {
  const token = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') ?? ''
  const res = await fetch(url, { method: 'DELETE', headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token }, credentials: 'same-origin', body: JSON.stringify(body) })
  if (!res.ok) { let m = 'خطا'; try { m = (await res.json()).message ?? m } catch { /* */ } throw new Error(m) }
}

async function bulkAdvance() {
  const ids = advanceable.value.map((r) => r.id)
  if (ids.length === 0) return
  if (!confirm(`${ids.length} فرش که مرحلهٔ فعال دارند به مرحلهٔ بعد بروند؟`)) return
  await run(async () => {
    const result = await api.post<{ successCount: number; failedCount: number }>('/api/rugs/bulk/advance', {
      rugIds: ids,
      step: { serviceProviderId: null, manualCostOverride: null, pricingModel: null, unitRate: null, pricingConfigJson: null, fieldValuesJson: null, notes: null, markCompleted: true, adjustment: null },
    })
    info.value = `${result.successCount} فرش پیش رفت` + (result.failedCount ? `، ${result.failedCount} ناموفق` : '')
  })
}

async function run(fn: () => Promise<void>, _id?: string) {
  busy.value = true; error.value = ''; info.value = ''
  try { await fn(); await load() }
  catch (e) { error.value = (e as Error).message }
  finally { busy.value = false }
}

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <div v-if="error" class="rounded-lg bg-error-container px-4 py-3 text-sm text-error">{{ error }}</div>
    <div v-if="info" class="rounded-lg bg-success/10 px-4 py-3 text-sm text-success">{{ info }}</div>
    <div v-if="loading" class="rounded-xl border border-outline-variant bg-white p-8 text-center text-on-surface-variant">در حال بارگذاری…</div>

    <template v-else>
      <!-- نوار عملیات گروهی -->
      <div class="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-outline-variant bg-white p-4 shadow-sm">
        <div class="text-sm">
          <span class="font-bold">{{ inGroup.length }}</span> فرش در گروه ·
          <span class="text-on-surface-variant">{{ advanceable.length }} با مرحلهٔ فعال</span>
        </div>
        <button :disabled="busy || advanceable.length === 0" @click="bulkAdvance"
                class="rounded-lg bg-primary px-4 py-2.5 font-semibold text-on-primary hover:opacity-90 disabled:opacity-50">
          پیشبرد گروهی مرحلهٔ بعد ({{ advanceable.length }})
        </button>
      </div>

      <!-- فرش‌های گروه -->
      <section class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
        <h2 class="mb-3 text-sm font-semibold text-primary">فرش‌های گروه</h2>
        <div v-if="inGroup.length === 0" class="py-6 text-center text-on-surface-variant">هنوز فرشی در این گروه نیست.</div>
        <ul v-else class="divide-y divide-outline-variant">
          <li v-for="r in inGroup" :key="r.id" class="flex items-center justify-between gap-3 py-3">
            <div class="min-w-0">
              <a :href="`/Rugs/Details/${r.id}`" class="truncate font-medium hover:text-primary">{{ r.title || 'بدون عنوان' }}</a>
              <div class="text-xs text-on-surface-variant" dir="ltr">{{ r.sku }} · {{ statusLabel[r.status] }} · {{ formatThousands(r.costs.totalInvestment) }}</div>
            </div>
            <button :disabled="busy" @click="removeOne(r.id)" class="rounded-lg border border-outline-variant px-3 py-1.5 text-xs text-error hover:bg-error-container">خروج از گروه</button>
          </li>
        </ul>
      </section>

      <!-- افزودن فرش -->
      <section class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
        <div class="mb-3 flex items-center justify-between">
          <h2 class="text-sm font-semibold text-primary">افزودن فرش به گروه</h2>
          <button :disabled="busy" @click="addSelected" class="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-on-primary hover:opacity-90 disabled:opacity-50">افزودن انتخاب‌شده‌ها</button>
        </div>
        <div v-if="unassigned.length === 0" class="py-6 text-center text-on-surface-variant">فرش بدون گروهی موجود نیست.</div>
        <ul v-else class="max-h-72 divide-y divide-outline-variant overflow-y-auto">
          <li v-for="r in unassigned" :key="r.id" class="flex items-center gap-3 py-2.5">
            <input type="checkbox" v-model="addPick[r.id]" class="h-4 w-4 rounded border-outline-variant text-primary" />
            <div class="min-w-0">
              <div class="truncate font-medium">{{ r.title || 'بدون عنوان' }}</div>
              <div class="text-xs text-on-surface-variant" dir="ltr">{{ r.sku }}</div>
            </div>
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>
