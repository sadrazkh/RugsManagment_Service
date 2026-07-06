<script setup lang="ts">
/**
 * مدیریت گرافیکیِ یک گروه: فرش‌ها بر اساس «مرحلهٔ جاری» دسته‌بندی می‌شوند
 * (مثلاً ۱۰ فرش در قالیشویی، ۵ در دارکشی). می‌توان زیرمجموعه‌ای را انتخاب کرد و:
 *   • با هم پیش برد یا به مرحلهٔ قبل برگرداند
 *   • از انتخاب یک «گروه جدید» ساخت یا به گروه دیگری منتقل کرد (زیرگروه‌بندی)
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { api } from '@/lib/api'
import { formatThousands } from '@/lib/money'
import type { Rug } from '@/lib/types'

const props = defineProps<{ groupId: string; groupName: string }>()

interface Group { id: string; name: string }

const allRugs = ref<Rug[]>([])
const groups = ref<Group[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const info = ref('')
const sel = reactive<Record<string, boolean>>({})
const addPick = reactive<Record<string, boolean>>({})
const moveTarget = ref('')

const statusName: Record<number, string> = { 0: 'پیش‌نویس', 1: 'در جریان', 2: 'آماده', 3: 'فروخته', 4: 'بایگانی' }

const inGroup = computed(() => allRugs.value.filter((r) => r.batchId === props.groupId))
const unassigned = computed(() => allRugs.value.filter((r) => !r.batchId))
const otherGroups = computed(() => groups.value.filter((g) => g.id !== props.groupId))
const selectedIds = computed(() => inGroup.value.filter((r) => sel[r.id]).map((r) => r.id))

function currentStage(r: Rug): string {
  const step = r.workflowSteps.find((s) => s.status === 1)
  return step ? step.stepNameFa : `بدون مرحلهٔ فعال (${statusName[r.status] ?? '—'})`
}
function progress(r: Rug): { done: number; total: number } {
  const total = r.workflowSteps.length
  const done = r.workflowSteps.filter((s) => s.status === 2 || s.status === 3).length
  return { done, total }
}

// دسته‌بندی بر اساس مرحلهٔ جاری
const stageGroups = computed(() => {
  const map = new Map<string, Rug[]>()
  for (const r of inGroup.value) {
    const k = currentStage(r)
    if (!map.has(k)) map.set(k, [])
    map.get(k)!.push(r)
  }
  return [...map.entries()].map(([name, rugs]) => ({ name, rugs }))
})

function toggleStage(rugs: Rug[], checked: boolean) {
  for (const r of rugs) sel[r.id] = checked
}
function clearSel() { for (const k of Object.keys(sel)) sel[k] = false }

async function load() {
  loading.value = true
  try {
    const [rugList, gs] = await Promise.all([api.get<Rug[]>('/api/rugs'), api.get<Group[]>('/api/batches')])
    allRugs.value = rugList; groups.value = gs
  } catch (e) { error.value = (e as Error).message } finally { loading.value = false }
}

async function run(fn: () => Promise<void>) {
  busy.value = true; error.value = ''; info.value = ''
  try { await fn(); clearSel(); await load() }
  catch (e) { error.value = (e as Error).message }
  finally { busy.value = false }
}

async function fetchJson(method: string, url: string, body?: unknown) {
  const token = document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') ?? ''
  const res = await fetch(url, { method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token }, credentials: 'same-origin', body: body === undefined ? undefined : JSON.stringify(body) })
  if (!res.ok) { let m = 'خطا'; try { m = (await res.json()).message ?? m } catch { /* */ } throw new Error(m) }
  return res.status === 204 ? null : res.json()
}

async function advanceSel() {
  if (selectedIds.value.length === 0) return
  await run(async () => {
    const r = await api.post<{ successCount: number; failedCount: number }>('/api/rugs/bulk/advance', {
      rugIds: selectedIds.value,
      step: { serviceProviderId: null, manualCostOverride: null, pricingModel: null, unitRate: null, pricingConfigJson: null, fieldValuesJson: null, notes: null, markCompleted: true, adjustment: null },
    })
    info.value = `${r.successCount} فرش پیش رفت` + (r.failedCount ? `، ${r.failedCount} ناموفق` : '')
  })
}
async function backSel() {
  if (selectedIds.value.length === 0) return
  await run(async () => {
    const r = await api.post<{ successCount: number; failedCount: number }>('/api/rugs/bulk/back', { rugIds: selectedIds.value })
    info.value = `${r.successCount} فرش به مرحلهٔ قبل رفت`
  })
}
async function newSubGroup() {
  if (selectedIds.value.length === 0) return
  const name = prompt('نام گروه جدید برای فرش‌های انتخاب‌شده:')
  if (!name) return
  await run(async () => {
    const g = await api.post<Group>('/api/batches', { name, description: `جدا شده از «${props.groupName}»`, receivedAt: null })
    await api.post(`/api/batches/${g.id}/rugs`, { rugIds: selectedIds.value })
    info.value = `${selectedIds.value.length} فرش به گروه «${name}» منتقل شد`
  })
}
async function moveToGroup() {
  if (selectedIds.value.length === 0 || !moveTarget.value) return
  const ids = selectedIds.value
  await run(async () => {
    await api.post(`/api/batches/${moveTarget.value}/rugs`, { rugIds: ids })
    info.value = `${ids.length} فرش منتقل شد`
  })
}
async function addSelected() {
  const ids = Object.keys(addPick).filter((k) => addPick[k])
  if (ids.length === 0) return
  await run(async () => {
    await api.post(`/api/batches/${props.groupId}/rugs`, { rugIds: ids })
    for (const k of Object.keys(addPick)) addPick[k] = false
    info.value = `${ids.length} فرش اضافه شد`
  })
}
async function removeOne(id: string) {
  await run(async () => { await fetchJson('DELETE', `/api/batches/${props.groupId}/rugs`, { rugIds: [id] }); info.value = 'از گروه خارج شد' })
}

onMounted(load)
</script>

<template>
  <div class="space-y-5 pb-24">
    <div v-if="error" class="rounded-lg bg-error-container px-4 py-3 text-sm text-error">{{ error }}</div>
    <div v-if="info" class="rounded-lg bg-success/10 px-4 py-3 text-sm text-success">{{ info }}</div>
    <div v-if="loading" class="rounded-xl border border-outline-variant bg-white p-8 text-center text-on-surface-variant">در حال بارگذاری…</div>

    <template v-else>
      <div class="text-sm text-on-surface-variant"><span class="font-bold text-on-surface">{{ inGroup.length }}</span> فرش در این گروه، در {{ stageGroups.length }} مرحله</div>

      <div v-if="inGroup.length === 0" class="rounded-xl border border-dashed border-outline-variant bg-white p-8 text-center text-on-surface-variant">
        هنوز فرشی در این گروه نیست. از پایین اضافه کنید.
      </div>

      <!-- ستون‌های مرحله‌محور -->
      <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <section v-for="sg in stageGroups" :key="sg.name" class="rounded-xl border border-outline-variant bg-white p-4 shadow-sm">
          <div class="mb-3 flex items-center justify-between gap-2">
            <h2 class="flex items-center gap-2 font-semibold text-primary">
              {{ sg.name }}
              <span class="rounded-full bg-primary/10 px-2 py-0.5 text-xs">{{ sg.rugs.length }}</span>
            </h2>
            <label class="flex items-center gap-1 text-xs text-on-surface-variant">
              <input type="checkbox" @change="toggleStage(sg.rugs, ($event.target as HTMLInputElement).checked)" class="h-4 w-4 rounded border-outline-variant text-primary" />
              همه
            </label>
          </div>
          <ul class="space-y-2">
            <li v-for="r in sg.rugs" :key="r.id" class="flex items-center gap-2 rounded-lg border px-3 py-2"
                :class="sel[r.id] ? 'border-primary bg-primary/5' : 'border-outline-variant'">
              <input type="checkbox" v-model="sel[r.id]" class="h-4 w-4 shrink-0 rounded border-outline-variant text-primary" />
              <a :href="`/Rugs/Details/${r.id}`" class="min-w-0 flex-1">
                <div class="truncate text-sm font-medium hover:text-primary">{{ r.title || 'بدون عنوان' }}</div>
                <div class="text-xs text-on-surface-variant" dir="ltr">{{ r.sku }}</div>
              </a>
              <div class="shrink-0 text-left">
                <div class="text-xs text-on-surface-variant" dir="ltr">{{ progress(r).done }}/{{ progress(r).total }}</div>
                <div class="mt-0.5 h-1.5 w-12 overflow-hidden rounded-full bg-surface-container">
                  <div class="h-full rounded-full bg-success" :style="{ width: (progress(r).total ? progress(r).done / progress(r).total * 100 : 0) + '%' }"></div>
                </div>
              </div>
              <button @click="removeOne(r.id)" :disabled="busy" class="shrink-0 text-xs text-error hover:opacity-70" title="خروج از گروه">✕</button>
            </li>
          </ul>
        </section>
      </div>

      <!-- افزودن فرش بدون گروه -->
      <section class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
        <div class="mb-3 flex items-center justify-between">
          <h2 class="text-sm font-semibold text-primary">افزودن فرش به گروه</h2>
          <button :disabled="busy" @click="addSelected" class="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-on-primary hover:opacity-90 disabled:opacity-50">افزودن</button>
        </div>
        <div v-if="unassigned.length === 0" class="py-4 text-center text-sm text-on-surface-variant">فرش بدون گروهی موجود نیست.</div>
        <ul v-else class="max-h-60 divide-y divide-outline-variant overflow-y-auto">
          <li v-for="r in unassigned" :key="r.id" class="flex items-center gap-3 py-2">
            <input type="checkbox" v-model="addPick[r.id]" class="h-4 w-4 rounded border-outline-variant text-primary" />
            <div class="min-w-0"><div class="truncate text-sm font-medium">{{ r.title || 'بدون عنوان' }}</div><div class="text-xs text-on-surface-variant" dir="ltr">{{ r.sku }}</div></div>
          </li>
        </ul>
      </section>
    </template>

    <!-- نوار عملیات زیرمجموعه (چسبان پایین) -->
    <transition name="fade">
      <div v-if="selectedIds.length > 0" class="pb-safe fixed inset-x-0 bottom-0 z-40 border-t border-outline-variant bg-white/95 px-4 pt-3 shadow-lg backdrop-blur">
        <div class="mx-auto flex max-w-[1440px] flex-wrap items-center gap-2">
          <span class="font-bold text-primary">{{ selectedIds.length }} انتخاب</span>
          <button @click="clearSel" class="text-sm text-on-surface-variant hover:underline">لغو</button>
          <div class="flex-1"></div>
          <button :disabled="busy" @click="backSel" class="rounded-lg border border-outline-variant px-3 py-2 text-sm hover:bg-surface-container disabled:opacity-50">→ مرحلهٔ قبل</button>
          <button :disabled="busy" @click="advanceSel" class="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-on-primary hover:opacity-90 disabled:opacity-50">مرحلهٔ بعد ←</button>
          <span class="mx-1 h-6 w-px bg-outline-variant"></span>
          <button :disabled="busy" @click="newSubGroup" class="rounded-lg border border-primary px-3 py-2 text-sm text-primary hover:bg-primary/10 disabled:opacity-50">گروه جدید از انتخاب</button>
          <select v-if="otherGroups.length" v-model="moveTarget" class="rounded-lg border border-outline-variant px-2 py-2 text-sm">
            <option value="">انتقال به…</option>
            <option v-for="g in otherGroups" :key="g.id" :value="g.id">{{ g.name }}</option>
          </select>
          <button v-if="otherGroups.length" :disabled="busy || !moveTarget" @click="moveToGroup" class="rounded-lg border border-outline-variant px-3 py-2 text-sm hover:bg-surface-container disabled:opacity-50">انتقال</button>
        </div>
      </div>
    </transition>
  </div>
</template>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.15s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
