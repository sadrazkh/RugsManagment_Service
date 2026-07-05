<script setup lang="ts">
/**
 * پنل گردش کار یک فرش: تایم‌لاین مراحل، حرکت جلو/عقب با مودال تأیید (هزینه/انجام‌دهنده/تاریخ/توضیح)،
 * رد کردن مرحلهٔ اختیاری، و ویرایش مسیرِ مراحل باقی‌مانده با drag & drop.
 * محاسبهٔ هزینه در بک‌اند انجام می‌شود.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { VueDraggable } from 'vue-draggable-plus'
import { api } from '@/lib/api'
import { formatThousands } from '@/lib/money'
import MoneyInput from '@/components/MoneyInput.vue'
import type { Rug } from '@/lib/types'

const props = defineProps<{ rugId: string }>()

interface Provider { id: string; name: string }
interface StepType { id: string; nameFa: string }
interface Row { uid: number; processStepTypeId: string; stepNameFa: string; isOptional: boolean }

const rug = ref<Rug | null>(null)
const providers = ref<Provider[]>([])
const stepTypes = ref<StepType[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref('')

const statusLabel: Record<number, string> = { 0: 'در صف', 1: 'در حال انجام', 2: 'تکمیل', 3: 'رد‌شده', 4: 'لغو' }
const statusCss: Record<number, string> = {
  0: 'bg-surface-container text-on-surface-variant', 1: 'bg-secondary-container text-on-secondary-container',
  2: 'bg-success/10 text-success', 3: 'bg-surface-container text-on-surface-variant line-through', 4: 'bg-error-container text-error',
}

const orderedSteps = computed(() => [...(rug.value?.workflowSteps ?? [])].sort((a, b) => a.orderIndex - b.orderIndex))
const currentStep = computed(() => orderedSteps.value.find((s) => s.status === 1) ?? null)

// ── مودال حرکت ──
const modal = reactive({
  open: false, kind: 'forward' as 'forward' | 'back',
  manual: false, model: 1, unitRate: undefined as number | undefined,
  manualCost: undefined as number | undefined, providerId: '', date: '', notes: '',
})
const estimate = computed(() => {
  if (!rug.value) return 0
  if (modal.manual) return modal.manualCost ?? 0
  const r = modal.unitRate ?? 0
  switch (modal.model) {
    case 0: return r
    case 1: return Math.round(r * rug.value.areaSquareMeters * 100) / 100
    case 4: return Math.round(r * rug.value.lengthMeters * 100) / 100
    case 5: return Math.round(r * rug.value.widthMeters * 100) / 100
    default: return r
  }
})

// ── ویرایش مسیر ──
const editingPath = ref(false)
const pathRows = ref<Row[]>([])
let counter = 0

async function load() {
  loading.value = true
  try {
    const [r, p, st] = await Promise.all([
      api.get<Rug>(`/api/rugs/${props.rugId}`),
      api.get<Provider[]>('/api/lookups/service-providers'),
      api.get<StepType[]>('/api/lookups/step-types'),
    ])
    rug.value = r; providers.value = p; stepTypes.value = st
  } catch (e) { error.value = (e as Error).message } finally { loading.value = false }
}

function openForward() {
  Object.assign(modal, { open: true, kind: 'forward', manual: false, model: 1, unitRate: undefined, manualCost: undefined, providerId: currentStep.value ? '' : '', date: new Date().toISOString().slice(0, 10), notes: '' })
}
function openBack() {
  Object.assign(modal, { open: true, kind: 'back', notes: '' })
}

function fieldValues() {
  const obj: Record<string, string> = {}
  if (modal.date) obj.date = modal.date
  return Object.keys(obj).length ? JSON.stringify(obj) : null
}
function movePayload(markCompleted: boolean) {
  return {
    serviceProviderId: modal.providerId || null,
    manualCostOverride: modal.manual ? (modal.manualCost ?? 0) : null,
    pricingModel: modal.manual ? null : modal.model,
    unitRate: modal.manual ? null : (modal.unitRate ?? null),
    pricingConfigJson: null, fieldValuesJson: fieldValues(),
    notes: modal.notes || null, markCompleted,
  }
}

async function doForward(markCompleted: boolean) {
  if (!currentStep.value) return
  busy.value = true; error.value = ''
  try {
    if (markCompleted) {
      await api.post(`/api/rugs/${props.rugId}/steps/${currentStep.value.id}/advance`, movePayload(true))
    } else {
      const updated = await api.put<Rug>(`/api/rugs/${props.rugId}/steps/${currentStep.value.id}/pricing`, movePayload(false))
      rug.value = updated; modal.open = false; busy.value = false; return
    }
    window.location.reload()
  } catch (e) { error.value = (e as Error).message; busy.value = false }
}

async function doBack() {
  busy.value = true; error.value = ''
  try { await api.post(`/api/rugs/${props.rugId}/workflow/back`, undefined); window.location.reload() }
  catch (e) { error.value = (e as Error).message; busy.value = false }
}

async function skipCurrent() {
  if (!currentStep.value) return
  busy.value = true; error.value = ''
  try { await api.post(`/api/rugs/${props.rugId}/steps/${currentStep.value.id}/skip`, undefined); window.location.reload() }
  catch (e) { error.value = (e as Error).message; busy.value = false }
}

function startEditPath() {
  pathRows.value = orderedSteps.value
    .filter((s) => s.status === 0 || s.status === 1)
    .map((s) => ({ uid: ++counter, processStepTypeId: s.processStepTypeId, stepNameFa: s.stepNameFa, isOptional: s.isOptional }))
  editingPath.value = true
}
function addPathStep(t: StepType) { pathRows.value.push({ uid: ++counter, processStepTypeId: t.id, stepNameFa: t.nameFa, isOptional: false }) }
function removePathStep(uid: number) { pathRows.value = pathRows.value.filter((r) => r.uid !== uid) }

async function savePath() {
  busy.value = true; error.value = ''
  try {
    await api.put(`/api/workflows/rugs/${props.rugId}/path`, {
      pendingSteps: pathRows.value.map((r) => ({ processStepTypeId: r.processStepTypeId, isOptional: r.isOptional, serviceProviderId: null })),
    })
    window.location.reload()
  } catch (e) { error.value = (e as Error).message; busy.value = false }
}

onMounted(load)
</script>

<template>
  <div class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
    <div class="mb-4 flex items-center justify-between">
      <h2 class="text-sm font-semibold text-primary">گردش کار</h2>
      <button v-if="!editingPath && !loading" @click="startEditPath" class="text-xs text-primary hover:underline">ویرایش مسیر</button>
    </div>

    <div v-if="loading" class="py-6 text-center text-on-surface-variant">در حال بارگذاری…</div>
    <div v-else-if="error && !rug" class="rounded-lg bg-error-container px-4 py-3 text-sm text-error">{{ error }}</div>

    <template v-else>
      <div v-if="error" class="mb-3 rounded-lg bg-error-container px-4 py-2 text-sm text-error">{{ error }}</div>

      <!-- ویرایش مسیر (drag & drop) -->
      <div v-if="editingPath" class="space-y-3">
        <p class="text-xs text-on-surface-variant">مراحل تمام‌شده حفظ می‌شوند؛ فقط مراحل باقی‌مانده را بازچینی کنید.</p>
        <VueDraggable v-model="pathRows" :animation="150" handle=".dh" class="space-y-2">
          <div v-for="(r, i) in pathRows" :key="r.uid" class="flex items-center gap-2 rounded-lg border border-outline-variant px-3 py-2">
            <span class="dh cursor-grab text-on-surface-variant">⠿</span>
            <span class="flex-1">{{ i + 1 }}. {{ r.stepNameFa }}</span>
            <label class="flex items-center gap-1 text-xs"><input v-model="r.isOptional" type="checkbox" class="h-3.5 w-3.5" /> اختیاری</label>
            <button @click="removePathStep(r.uid)" class="text-error">✕</button>
          </div>
        </VueDraggable>
        <div class="flex flex-wrap gap-1.5">
          <button v-for="t in stepTypes" :key="t.id" @click="addPathStep(t)" class="rounded-lg border border-outline-variant px-2.5 py-1 text-xs hover:bg-surface-container">+ {{ t.nameFa }}</button>
        </div>
        <div class="flex gap-2 pt-2">
          <button @click="editingPath = false" class="flex-1 rounded-lg border border-outline-variant px-4 py-2.5 hover:bg-surface-container">انصراف</button>
          <button :disabled="busy" @click="savePath" class="flex-1 rounded-lg bg-primary px-4 py-2.5 font-semibold text-on-primary disabled:opacity-60">ذخیرهٔ مسیر</button>
        </div>
      </div>

      <!-- تایم‌لاین -->
      <template v-else>
        <ol class="space-y-2">
          <li v-for="s in orderedSteps" :key="s.id"
              class="flex items-center justify-between gap-2 rounded-lg border px-3 py-2.5"
              :class="s.status === 1 ? 'border-primary bg-primary/5' : 'border-outline-variant'">
            <div class="flex items-center gap-2">
              <span class="grid h-7 w-7 place-items-center rounded-full bg-surface-container text-sm">{{ s.orderIndex + 1 }}</span>
              <span class="font-medium">{{ s.stepNameFa }}<span v-if="s.isOptional" class="text-xs text-on-surface-variant"> (اختیاری)</span></span>
            </div>
            <div class="flex items-center gap-2">
              <span v-if="s.effectiveCost > 0" class="text-xs" dir="ltr">{{ formatThousands(s.effectiveCost) }}</span>
              <span class="rounded-full px-2 py-0.5 text-xs" :class="statusCss[s.status]">{{ statusLabel[s.status] }}</span>
            </div>
          </li>
        </ol>

        <div v-if="currentStep" class="mt-4 flex flex-wrap gap-2">
          <button :disabled="busy" @click="openForward" class="flex-1 rounded-lg bg-primary px-4 py-2.5 font-semibold text-on-primary hover:opacity-90 disabled:opacity-60">تکمیل مرحله ←</button>
          <button v-if="currentStep.isOptional" :disabled="busy" @click="skipCurrent" class="rounded-lg border border-outline-variant px-4 py-2.5 hover:bg-surface-container">رد کردن</button>
          <button :disabled="busy" @click="openBack" class="rounded-lg border border-outline-variant px-4 py-2.5 text-on-surface-variant hover:bg-surface-container">→ مرحلهٔ قبل</button>
        </div>
        <div v-else class="mt-4 rounded-lg bg-success/10 px-4 py-3 text-center text-sm text-success">همهٔ مراحل تمام شده است.</div>
      </template>
    </template>
  </div>

  <!-- مودال حرکت -->
  <div v-if="modal.open" class="fixed inset-0 z-50 grid place-items-center bg-black/40 p-4" @click.self="modal.open = false">
    <div class="w-full max-w-md rounded-2xl bg-white p-5 shadow-lg">
      <template v-if="modal.kind === 'forward'">
        <h3 class="mb-1 text-lg font-bold">تکمیل مرحله: {{ currentStep?.stepNameFa }}</h3>
        <p class="mb-4 text-sm text-on-surface-variant">جزئیات این مرحله را وارد کنید سپس به مرحلهٔ بعد بروید.</p>
        <div v-if="error" class="mb-3 rounded-lg bg-error-container px-3 py-2 text-sm text-error">{{ error }}</div>
        <div class="space-y-3">
          <label class="flex items-center gap-2 text-sm"><input v-model="modal.manual" type="checkbox" class="h-4 w-4" /> مبلغ دستی</label>
          <div v-if="!modal.manual" class="grid grid-cols-2 gap-3">
            <label class="block"><span class="mb-1 block text-sm">روش</span>
              <select v-model.number="modal.model" class="fld"><option :value="0">ثابت</option><option :value="1">× مساحت</option><option :value="4">× طول</option><option :value="5">× عرض</option></select>
            </label>
            <label class="block"><span class="mb-1 block text-sm">نرخ</span><MoneyInput v-model="modal.unitRate" suffix="تومان" /></label>
          </div>
          <label v-else class="block"><span class="mb-1 block text-sm">مبلغ</span><MoneyInput v-model="modal.manualCost" suffix="تومان" /></label>
          <div class="grid grid-cols-2 gap-3">
            <label v-if="providers.length" class="block"><span class="mb-1 block text-sm">انجام‌دهنده</span>
              <select v-model="modal.providerId" class="fld"><option value="">—</option><option v-for="p in providers" :key="p.id" :value="p.id">{{ p.name }}</option></select>
            </label>
            <label class="block"><span class="mb-1 block text-sm">تاریخ</span><input v-model="modal.date" type="date" dir="ltr" class="fld" /></label>
          </div>
          <label class="block"><span class="mb-1 block text-sm">توضیح</span><input v-model="modal.notes" class="fld" /></label>
          <div class="flex items-center justify-between rounded-lg border border-dashed border-outline-variant px-3 py-2 text-sm">
            <span class="text-on-surface-variant">هزینه (تخمین)</span><span class="font-bold" dir="ltr">{{ formatThousands(estimate) }}</span>
          </div>
        </div>
        <div class="mt-5 flex flex-wrap gap-2">
          <button @click="modal.open = false" class="rounded-lg border border-outline-variant px-4 py-2.5 hover:bg-surface-container">انصراف</button>
          <button :disabled="busy" @click="doForward(false)" class="flex-1 rounded-lg border border-outline-variant px-4 py-2.5 hover:bg-surface-container disabled:opacity-60">فقط ذخیره</button>
          <button :disabled="busy" @click="doForward(true)" class="flex-1 rounded-lg bg-primary px-4 py-2.5 font-semibold text-on-primary disabled:opacity-60">تکمیل و بعدی</button>
        </div>
      </template>

      <template v-else>
        <h3 class="mb-1 text-lg font-bold">بازگشت به مرحلهٔ قبل</h3>
        <p class="mb-4 text-sm text-on-surface-variant">آیا مطمئن هستید؟ مرحلهٔ جاری به حالت «در صف» برمی‌گردد و مرحلهٔ قبلی دوباره فعال می‌شود.</p>
        <div v-if="error" class="mb-3 rounded-lg bg-error-container px-3 py-2 text-sm text-error">{{ error }}</div>
        <div class="flex gap-2">
          <button @click="modal.open = false" class="flex-1 rounded-lg border border-outline-variant px-4 py-2.5 hover:bg-surface-container">انصراف</button>
          <button :disabled="busy" @click="doBack" class="flex-1 rounded-lg bg-primary px-4 py-2.5 font-semibold text-on-primary disabled:opacity-60">بله، بازگشت</button>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.fld { width: 100%; border-radius: 0.5rem; border: 1px solid var(--color-outline-variant); padding: 0.5rem 0.75rem; outline: none; }
.fld:focus { border-color: var(--color-primary); box-shadow: 0 0 0 1px var(--color-primary); }
</style>
