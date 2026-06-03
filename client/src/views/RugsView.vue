<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import api from '@/api/client'
import StepAdvanceModal from '@/components/StepAdvanceModal.vue'
import type { AdvanceRugStepPayload, BulkOperationResult, Rug, RugBatch, RugStatus, RugWorkflowStep } from '@/types'
import { formatCurrency, statusLabel } from '@/utils/format'

const rugs = ref<Rug[]>([])
const batches = ref<RugBatch[]>([])
const filter = ref<RugStatus | ''>('')
const batchFilter = ref('')
const loading = ref(true)
const selected = ref<Set<string>>(new Set())
const showAdvance = ref(false)
const showBatchPanel = ref(false)
const newBatchName = ref('')
const bulkBusy = ref(false)
const lastResult = ref<BulkOperationResult | null>(null)
const providers = ref<{ id: string; name: string }[]>([])

const sampleStep = computed<RugWorkflowStep | null>(() => {
  const first = rugs.value.find((r) => selected.value.has(r.id) && r.workflowSteps.some((s) => s.status === 'InProgress'))
  return first?.workflowSteps.find((s) => s.status === 'InProgress') ?? null
})

const sampleArea = computed(() => {
  const first = rugs.value.find((r) => selected.value.has(r.id))
  return first?.areaSquareMeters ?? 0
})

async function load() {
  loading.value = true
  try {
    const params: Record<string, string> = {}
    if (filter.value) params.status = filter.value
    const [r, b, p] = await Promise.all([
      api.get<Rug[]>('/rugs', { params }),
      api.get<RugBatch[]>('/batches'),
      api.get<{ id: string; name: string }[]>('/workflows/providers'),
    ])
    providers.value = p.data
    let list = r.data
    if (batchFilter.value) list = list.filter((x) => x.batchId === batchFilter.value)
    rugs.value = list
    batches.value = b.data
  } finally {
    loading.value = false
  }
}

onMounted(load)

function toggle(id: string) {
  const s = new Set(selected.value)
  if (s.has(id)) s.delete(id)
  else s.add(id)
  selected.value = s
}

function toggleAll() {
  if (selected.value.size === rugs.value.length) selected.value = new Set()
  else selected.value = new Set(rugs.value.map((r) => r.id))
}

const selectedIds = computed(() => [...selected.value])

async function bulkAdvance(payload: AdvanceRugStepPayload) {
  if (!selectedIds.value.length) return
  bulkBusy.value = true
  try {
    const { data } = await api.post<BulkOperationResult>('/rugs/bulk/advance', {
      rugIds: selectedIds.value,
      step: payload,
    })
    lastResult.value = data
    selected.value = new Set()
    await load()
  } finally {
    bulkBusy.value = false
  }
}

async function createBatchAndAssign() {
  if (!newBatchName.value.trim() || !selectedIds.value.length) return
  const { data: batch } = await api.post<RugBatch>('/batches', { name: newBatchName.value })
  await api.post(`/batches/${batch.id}/rugs`, { rugIds: selectedIds.value })
  newBatchName.value = ''
  showBatchPanel.value = false
  selected.value = new Set()
  await load()
}

async function assignToExisting(batchId: string) {
  if (!batchId || !selectedIds.value.length) return
  await api.post(`/batches/${batchId}/rugs`, { rugIds: selectedIds.value })
  selected.value = new Set()
  await load()
}

const filters: { value: RugStatus | ''; label: string }[] = [
  { value: '', label: 'همه' },
  { value: 'InProgress', label: 'در جریان' },
  { value: 'ReadyForSale', label: 'آماده فروش' },
]
</script>

<template>
  <div class="space-y-4 pb-24">
    <header class="flex flex-col sm:flex-row justify-between gap-3">
      <div>
        <h2 class="text-2xl font-bold">فرش‌ها</h2>
        <p class="text-sm text-on-surface-variant">انتخاب گروهی و پیش بردن مسیر</p>
      </div>
      <RouterLink to="/rugs/new" class="inline-flex items-center gap-2 px-4 py-2.5 bg-primary text-on-primary rounded-full text-sm font-medium">
        <span class="material-symbols-outlined">add</span>
        ثبت جدید
      </RouterLink>
    </header>

    <div class="flex flex-wrap gap-2">
      <button v-for="f in filters" :key="f.value" type="button" class="px-3 py-1.5 rounded-full text-sm border" :class="filter === f.value ? 'bg-secondary-container' : ''" @click="filter = f.value; load()">{{ f.label }}</button>
      <select v-model="batchFilter" class="text-sm px-3 py-1.5 rounded-full border border-outline-variant" @change="load">
        <option value="">همه گروه‌ها</option>
        <option v-for="b in batches" :key="b.id" :value="b.id">{{ b.name }}</option>
      </select>
    </div>

    <div v-if="selected.size" class="sticky top-14 z-20 bg-surface-container-lowest border border-outline-variant rounded-xl p-3 flex flex-wrap gap-2 items-center shadow-sm">
      <span class="text-sm font-medium">{{ selected.size }} انتخاب</span>
      <button type="button" class="px-3 py-1.5 bg-primary text-on-primary rounded-full text-xs" @click="showAdvance = true">تکمیل مرحله (گروهی)</button>
      <button type="button" class="px-3 py-1.5 border rounded-full text-xs" @click="showBatchPanel = !showBatchPanel">گروه‌بندی</button>
      <button type="button" class="text-xs text-on-surface-variant" @click="selected = new Set()">لغو</button>
      <div v-if="showBatchPanel" class="w-full flex flex-wrap gap-2 pt-2 border-t">
        <input v-model="newBatchName" placeholder="نام گروه جدید" class="flex-1 min-w-[120px] px-2 py-1 border rounded-lg text-sm" />
        <button type="button" class="text-xs px-3 py-1.5 bg-secondary-container rounded-full" @click="createBatchAndAssign">ایجاد و افزودن</button>
        <select class="text-xs px-2 py-1 border rounded-lg" @change="(e) => assignToExisting((e.target as HTMLSelectElement).value)">
          <option value="">افزودن به گروه موجود...</option>
          <option v-for="b in batches" :key="b.id" :value="b.id">{{ b.name }}</option>
        </select>
      </div>
    </div>

    <p v-if="lastResult" class="text-sm px-3 py-2 rounded-lg bg-surface-container">
      گروهی: {{ lastResult.successCount }} موفق، {{ lastResult.failedCount }} خطا
    </p>

    <label class="flex items-center gap-2 text-sm">
      <input type="checkbox" :checked="selected.size === rugs.length && rugs.length > 0" @change="toggleAll" />
      انتخاب همه
    </label>

    <div v-if="loading" class="space-y-3">
      <div v-for="i in 4" :key="i" class="h-20 rounded-xl bg-surface-container animate-pulse" />
    </div>

    <div v-else class="grid gap-2">
      <div
        v-for="rug in rugs"
        :key="rug.id"
        class="flex gap-3 p-3 rounded-xl border border-outline-variant bg-surface-container-lowest"
      >
        <input type="checkbox" :checked="selected.has(rug.id)" class="mt-3 shrink-0" @change="toggle(rug.id)" />
        <RouterLink :to="`/rugs/${rug.id}`" class="flex-1 flex gap-3 min-w-0">
          <div class="flex-1 min-w-0">
            <p class="font-mono text-xs text-on-surface-variant">{{ rug.sku }}</p>
            <h3 class="font-semibold truncate">{{ rug.title || rug.origin || '—' }}</h3>
            <p class="text-xs mt-1">{{ rug.currentStepNameFa || statusLabel(rug.status) }} · {{ rug.widthMeters }}×{{ rug.lengthMeters }}m</p>
            <p v-if="rug.batchName" class="text-[10px] text-secondary mt-0.5">گروه: {{ rug.batchName }}</p>
          </div>
          <p class="text-sm font-medium text-primary shrink-0">{{ formatCurrency(rug.costs.totalInvestment) }}</p>
        </RouterLink>
      </div>
    </div>

    <StepAdvanceModal
      v-model:open="showAdvance"
      :step="sampleStep"
      :area-square-meters="sampleArea"
      :providers="providers"
      @confirm="bulkAdvance"
    />
  </div>
</template>
