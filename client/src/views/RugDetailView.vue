<script setup lang="ts">
/**
 * جزئیات فرش: ویرایش مشخصات، مانیتور مسیر، هزینه هر مرحله (ثابت/طول/عرض/m²/ترکیبی)، جلو/عقب
 */
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import api from '@/api/client'
import MoneyInput from '@/components/MoneyInput.vue'
import StepPricingForm from '@/components/StepPricingForm.vue'
import WorkflowStepBuilder, { type BuilderStep } from '@/components/WorkflowStepBuilder.vue'
import WorkflowStepper from '@/components/WorkflowStepper.vue'
import type {
  AdvanceRugStepPayload,
  CombinedPricingItem,
  ProcessStepType,
  Rug,
  RugWorkflowStep,
  ServiceProvider,
  StepPricingModel,
} from '@/types'
import { formatCurrency, statusLabel } from '@/utils/format'

const route = useRoute()
const rug = ref<Rug | null>(null)
const stepTypes = ref<ProcessStepType[]>([])
const providers = ref<ServiceProvider[]>([])
const loading = ref(true)
const busy = ref(false)
const editRug = ref(false)
const editPath = ref(false)
const pathSteps = ref<BuilderStep[]>([])
const selectedStepId = ref<string | undefined>()

const notes = ref('')
const manualCost = ref<number | undefined>()
const providerId = ref<string | undefined>()
const pricingModel = ref<StepPricingModel>('PerSquareMeter')
const unitRate = ref<number | undefined>()
const combinedItems = ref<CombinedPricingItem[]>([{ model: 'Fixed', rate: undefined }])

const editForm = ref({
  title: '',
  origin: '',
  pattern: '',
  material: '',
  knotDensity: undefined as number | undefined,
  widthMeters: 1,
  lengthMeters: 1,
  purchaseCost: undefined as number | undefined,
  targetSalePrice: undefined as number | undefined,
  notes: '',
})

const activeStep = computed(() =>
  rug.value?.workflowSteps.find((s) => s.status === 'InProgress'),
)

const selectedStep = computed(() => {
  if (!rug.value) return undefined
  const id = selectedStepId.value ?? activeStep.value?.id
  return rug.value.workflowSteps.find((s) => s.id === id)
})

const isWorkingStep = computed(() => selectedStep.value?.status === 'InProgress')

const canEditStepPricing = computed(() => {
  const s = selectedStep.value?.status
  return s === 'InProgress' || s === 'Completed'
})

const canGoBack = computed(() =>
  rug.value?.workflowSteps.some((s) => s.status === 'InProgress' || s.status === 'Completed') ?? false,
)

function resolveImage(url: string) {
  if (url.startsWith('http')) return url
  const apiBase = import.meta.env.VITE_API_URL || '/api'
  const origin = apiBase.replace(/\/api\/?$/, '') || ''
  return `${origin}${url.startsWith('/') ? url : `/${url}`}`
}

function syncEditForm(r: Rug) {
  editForm.value = {
    title: r.title ?? '',
    origin: r.origin ?? '',
    pattern: r.pattern ?? '',
    material: r.material ?? '',
    knotDensity: r.knotDensity,
    widthMeters: r.widthMeters,
    lengthMeters: r.lengthMeters,
    purchaseCost: r.purchaseCost,
    targetSalePrice: r.targetSalePrice,
    notes: r.notes ?? '',
  }
}

function parseCombined(json?: string): CombinedPricingItem[] {
  if (!json) return [{ model: 'Fixed', rate: undefined }]
  try {
    const cfg = JSON.parse(json) as { items?: CombinedPricingItem[] }
    if (cfg.items?.length) return cfg.items
  } catch {
    /* ignore */
  }
  return [{ model: 'Fixed', rate: undefined }]
}

function loadStepForm(step: RugWorkflowStep) {
  notes.value = step.notes ?? ''
  manualCost.value = undefined
  providerId.value = step.serviceProviderId
  const st = stepTypes.value.find((t) => t.id === step.processStepTypeId)
  pricingModel.value =
    (step.appliedPricingModel as StepPricingModel | undefined)
    ?? (st?.defaultPricingModel as StepPricingModel | undefined)
    ?? 'PerSquareMeter'
  unitRate.value = step.appliedUnitRate ?? st?.defaultUnitRate
  combinedItems.value =
    pricingModel.value === 'Combined'
      ? parseCombined(step.pricingConfigJson)
      : [{ model: 'Fixed', rate: undefined }]
}

function buildPricingConfigJson(): string | undefined {
  if (pricingModel.value !== 'Combined') return undefined
  const items = combinedItems.value
    .filter((i) => i.rate != null && i.rate > 0)
    .map((i) => ({ model: i.model, rate: i.rate }))
  return items.length ? JSON.stringify({ items }) : undefined
}

function pendingToBuilder() {
  if (!rug.value) return
  pathSteps.value = rug.value.workflowSteps
    .filter((s) => s.status === 'Pending' || s.status === 'InProgress')
    .sort((a, b) => a.orderIndex - b.orderIndex)
    .map((s) => ({
      key: s.id,
      processStepTypeId: s.processStepTypeId,
      isOptional: s.isOptional,
    }))
}

watch(selectedStep, (step) => {
  if (step) loadStepForm(step)
}, { immediate: true })

onMounted(async () => {
  const id = route.params.id as string
  const [rugRes, st, prov] = await Promise.all([
    api.get<Rug>(`/rugs/${id}`),
    api.get<ProcessStepType[]>('/workflows/step-types'),
    api.get<ServiceProvider[]>('/workflows/providers'),
  ])
  rug.value = rugRes.data
  stepTypes.value = st.data
  providers.value = prov.data
  selectedStepId.value = rugRes.data.workflowSteps.find((s) => s.status === 'InProgress')?.id
    ?? rugRes.data.workflowSteps[0]?.id
  syncEditForm(rugRes.data)
  pendingToBuilder()
  loading.value = false
})

function buildStepPayload(markCompleted: boolean): AdvanceRugStepPayload {
  return {
    serviceProviderId: providerId.value,
    manualCostOverride: manualCost.value,
    pricingModel: pricingModel.value,
    unitRate: pricingModel.value === 'Combined' ? undefined : unitRate.value,
    pricingConfigJson: buildPricingConfigJson(),
    notes: notes.value.trim() || undefined,
    markCompleted,
  }
}

async function saveRug() {
  if (!rug.value) return
  busy.value = true
  try {
    const { data } = await api.put<Rug>(`/rugs/${rug.value.id}`, {
      ...editForm.value,
      imageUrl: rug.value.imageUrl,
    })
    rug.value = data
    syncEditForm(data)
    editRug.value = false
  } finally {
    busy.value = false
  }
}

async function saveStepPricing() {
  if (!rug.value || !selectedStep.value) return
  busy.value = true
  try {
    const { data } = await api.put<Rug>(
      `/rugs/${rug.value.id}/steps/${selectedStep.value.id}/pricing`,
      buildStepPayload(false),
    )
    rug.value = data
    const updated = data.workflowSteps.find((s) => s.id === selectedStep.value!.id)
    if (updated) loadStepForm(updated)
  } finally {
    busy.value = false
  }
}

async function advanceStep() {
  if (!rug.value || !selectedStep.value || selectedStep.value.status !== 'InProgress') return
  busy.value = true
  try {
    const { data } = await api.post<Rug>(
      `/rugs/${rug.value.id}/steps/${selectedStep.value.id}/advance`,
      buildStepPayload(true),
    )
    rug.value = data
    selectedStepId.value = data.workflowSteps.find((s) => s.status === 'InProgress')?.id
    notes.value = ''
    manualCost.value = undefined
    pendingToBuilder()
  } finally {
    busy.value = false
  }
}

async function goBack() {
  if (!rug.value) return
  busy.value = true
  try {
    const { data } = await api.post<Rug>(`/rugs/${rug.value.id}/workflow/back`)
    rug.value = data
    selectedStepId.value = data.workflowSteps.find((s) => s.status === 'InProgress')?.id
    pendingToBuilder()
  } finally {
    busy.value = false
  }
}

async function onSelectStep(step: RugWorkflowStep) {
  if (!rug.value || step.status === 'Skipped') return
  selectedStepId.value = step.id
  if (step.status === 'InProgress') return
  busy.value = true
  try {
    const { data } = await api.post<Rug>(`/rugs/${rug.value.id}/steps/${step.id}/activate`)
    rug.value = data
    selectedStepId.value = step.id
    pendingToBuilder()
  } finally {
    busy.value = false
  }
}

async function skipStep() {
  if (!rug.value || !selectedStep.value?.isOptional) return
  busy.value = true
  try {
    const { data } = await api.post<Rug>(`/rugs/${rug.value.id}/steps/${selectedStep.value.id}/skip`)
    rug.value = data
    selectedStepId.value = data.workflowSteps.find((s) => s.status === 'InProgress')?.id
    pendingToBuilder()
  } finally {
    busy.value = false
  }
}

async function savePath() {
  if (!rug.value || !pathSteps.value.length) return
  busy.value = true
  try {
    const { data } = await api.put<Rug>(`/rugs/${rug.value.id}/workflow`, {
      pendingSteps: pathSteps.value.map((s) => ({
        processStepTypeId: s.processStepTypeId,
        isOptional: s.isOptional,
      })),
    })
    rug.value = data
    editPath.value = false
    pendingToBuilder()
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div v-if="loading" class="h-64 animate-pulse bg-surface-container rounded-xl" />
  <div v-else-if="rug" class="space-y-6 pb-28 lg:pb-8">
    <header class="flex flex-col sm:flex-row gap-4 justify-between">
      <div class="flex gap-4">
        <img
          v-if="rug.imageUrl"
          :src="resolveImage(rug.imageUrl)"
          alt=""
          class="w-20 h-20 sm:w-24 sm:h-24 object-cover rounded-xl border border-outline-variant shrink-0"
        />
        <div>
          <p class="font-mono text-sm text-on-surface-variant">{{ rug.sku }}</p>
          <h2 class="text-2xl font-bold">{{ rug.title || 'جزئیات فرش' }}</h2>
          <span class="inline-block mt-2 px-3 py-1 text-xs rounded-full bg-secondary-container">
            {{ statusLabel(rug.status) }}
          </span>
          <p v-if="rug.batchName" class="text-xs mt-1 text-on-surface-variant">گروه: {{ rug.batchName }}</p>
          <p v-if="rug.currentStepNameFa" class="text-sm mt-2 text-primary font-medium">
            الان در مرحله: {{ rug.currentStepNameFa }}
          </p>
        </div>
      </div>
      <button
        type="button"
        class="self-start px-4 py-2 text-sm border border-outline-variant rounded-full hover:bg-surface-container"
        @click="editRug = !editRug"
      >
        {{ editRug ? 'بستن ویرایش' : 'ویرایش فرش' }}
      </button>
    </header>

    <section v-if="editRug" class="border border-outline-variant rounded-xl p-4 space-y-3 bg-surface-container-lowest">
      <h3 class="font-semibold">مشخصات فرش</h3>
      <div class="grid sm:grid-cols-2 gap-3">
        <div class="sm:col-span-2">
          <label class="text-xs font-medium">عنوان</label>
          <input v-model="editForm.title" class="field" />
        </div>
        <div>
          <label class="text-xs font-medium">منشأ</label>
          <input v-model="editForm.origin" class="field" />
        </div>
        <div>
          <label class="text-xs font-medium">طرح</label>
          <input v-model="editForm.pattern" class="field" />
        </div>
        <div>
          <label class="text-xs font-medium">جنس</label>
          <input v-model="editForm.material" class="field" />
        </div>
        <div>
          <label class="text-xs font-medium">عرض (متر)</label>
          <input v-model.number="editForm.widthMeters" type="number" step="0.01" class="field" />
        </div>
        <div>
          <label class="text-xs font-medium">طول (متر)</label>
          <input v-model.number="editForm.lengthMeters" type="number" step="0.01" class="field" />
        </div>
        <div>
          <label class="text-xs font-medium">قیمت خرید (ریال)</label>
          <MoneyInput v-model="editForm.purchaseCost" class="field" />
        </div>
        <div>
          <label class="text-xs font-medium">قیمت فروش هدف (ریال)</label>
          <MoneyInput v-model="editForm.targetSalePrice" class="field" />
        </div>
        <div class="sm:col-span-2">
          <label class="text-xs font-medium">یادداشت فرش</label>
          <textarea v-model="editForm.notes" rows="2" class="field" />
        </div>
      </div>
      <button type="button" class="px-5 py-2.5 bg-primary text-on-primary rounded-full text-sm font-medium" :disabled="busy" @click="saveRug">
        ذخیره مشخصات
      </button>
    </section>

    <section class="border border-outline-variant rounded-xl p-4 space-y-3">
      <h3 class="font-semibold flex items-center gap-2">
        <span class="material-symbols-outlined text-primary">route</span>
        مسیر و وضعیت مراحل
      </h3>
      <p class="text-xs text-on-surface-variant">روی هر مرحله بزنید؛ مرحله جاری برجسته است.</p>
      <WorkflowStepper :steps="rug.workflowSteps" :selected-id="selectedStepId" @select="onSelectStep" />
    </section>

    <div class="grid sm:grid-cols-2 lg:grid-cols-4 gap-3">
      <div class="p-4 rounded-xl border border-outline-variant bg-surface-container-lowest">
        <p class="text-xs text-on-surface-variant">هزینه فرایند</p>
        <p class="text-lg font-bold">{{ formatCurrency(rug.costs.totalProcessCost) }}</p>
      </div>
      <div class="p-4 rounded-xl border border-outline-variant bg-surface-container-lowest">
        <p class="text-xs text-on-surface-variant">خرید</p>
        <p class="text-lg font-bold">{{ formatCurrency(rug.costs.purchaseCost) }}</p>
      </div>
      <div class="p-4 rounded-xl border border-outline-variant bg-surface-container-lowest">
        <p class="text-xs text-on-surface-variant">کل سرمایه</p>
        <p class="text-lg font-bold text-primary">{{ formatCurrency(rug.costs.totalInvestment) }}</p>
      </div>
      <div v-if="rug.costs.estimatedMargin != null" class="p-4 rounded-xl border border-outline-variant bg-surface-container-lowest">
        <p class="text-xs text-on-surface-variant">حاشیه تخمینی</p>
        <p class="text-lg font-bold">{{ formatCurrency(rug.costs.estimatedMargin) }}</p>
      </div>
    </div>

    <section
      v-if="selectedStep"
      class="border-2 rounded-xl p-4 sm:p-5 space-y-4"
      :class="isWorkingStep ? 'border-primary bg-primary/5' : 'border-outline-variant bg-surface-container-lowest'"
    >
      <h3 class="font-semibold flex items-center gap-2 flex-wrap">
        <span class="material-symbols-outlined text-primary">{{ selectedStep.icon }}</span>
        {{ selectedStep.stepNameFa }}
        <span class="text-xs font-normal px-2 py-0.5 rounded-full bg-surface-container">{{ statusLabel(selectedStep.status) }}</span>
      </h3>

      <p v-if="selectedStep.status === 'Completed'" class="text-sm">
        هزینه ثبت‌شده: <strong>{{ formatCurrency(selectedStep.effectiveCost) }}</strong>
        <span v-if="selectedStep.appliedPricingModel" class="text-on-surface-variant">
          — {{ selectedStep.appliedPricingModel }}
        </span>
      </p>

      <template v-if="canEditStepPricing && rug">
        <div>
          <label class="text-sm font-medium">یادداشت این مرحله</label>
          <textarea v-model="notes" rows="2" class="field mt-1" placeholder="توضیح کار انجام‌شده..." />
        </div>

        <StepPricingForm
          v-model:pricing-model="pricingModel"
          v-model:unit-rate="unitRate"
          v-model:manual-cost="manualCost"
          v-model:combined-items="combinedItems"
          :rug="rug"
        />

        <div>
          <label class="text-sm font-medium">ارائه‌دهنده</label>
          <select v-model="providerId" class="field mt-1">
            <option :value="undefined">—</option>
            <option v-for="p in providers" :key="p.id" :value="p.id">{{ p.name }}</option>
          </select>
        </div>

        <div class="flex flex-wrap gap-2 pt-1">
          <button
            type="button"
            :disabled="busy"
            class="px-5 py-2.5 border border-primary text-primary rounded-full text-sm font-medium disabled:opacity-60"
            @click="saveStepPricing"
          >
            ذخیره هزینه مرحله
          </button>
          <button
            v-if="isWorkingStep"
            type="button"
            :disabled="busy"
            class="px-5 py-2.5 bg-primary text-on-primary rounded-full text-sm font-medium disabled:opacity-60"
            @click="advanceStep"
          >
            تکمیل و مرحله بعد
          </button>
          <button
            v-if="canGoBack"
            type="button"
            :disabled="busy"
            class="px-5 py-2.5 border border-secondary text-secondary rounded-full text-sm"
            @click="goBack"
          >
            برگشت به مرحله قبل
          </button>
          <button
            v-if="isWorkingStep && selectedStep.isOptional"
            type="button"
            :disabled="busy"
            class="px-5 py-2.5 border border-outline-variant rounded-full text-sm"
            @click="skipStep"
          >
            رد (اختیاری)
          </button>
        </div>
      </template>

      <p v-else-if="selectedStep.status === 'Pending'" class="text-sm text-on-surface-variant">
        این مرحله هنوز شروع نشده — برای ثبت هزینه ابتدا روی آن در مسیر بالا کلیک کنید تا فعال شود.
      </p>
    </section>

    <section class="border border-outline-variant rounded-xl p-4 space-y-3">
      <div class="flex justify-between items-center gap-2">
        <h3 class="font-semibold text-sm">ویرایش مراحل باقی‌مانده مسیر</h3>
        <button
          type="button"
          class="text-sm text-primary"
          @click="editPath = !editPath"
        >
          {{ editPath ? 'بستن' : 'ویرایش' }}
        </button>
      </div>
      <div v-if="editPath" class="space-y-3">
        <WorkflowStepBuilder v-model="pathSteps" :step-types="stepTypes" compact />
        <button type="button" class="px-4 py-2 bg-secondary-container rounded-full text-sm" :disabled="busy" @click="savePath">
          ذخیره مسیر
        </button>
      </div>
      <p v-else class="text-xs text-on-surface-variant">مراحل انجام‌شده حفظ می‌شوند؛ فقط مراحل آینده قابل تغییر است.</p>
    </section>

    <section v-if="!editRug" class="text-sm text-on-surface-variant space-y-1">
      <p v-if="rug.origin"><strong>منشأ:</strong> {{ rug.origin }}</p>
      <p v-if="rug.pattern"><strong>طرح:</strong> {{ rug.pattern }}</p>
      <p><strong>ابعاد:</strong> {{ rug.widthMeters }} × {{ rug.lengthMeters }} متر ({{ rug.areaSquareMeters.toFixed(2) }} m²)</p>
    </section>

    <div class="lg:hidden fixed inset-x-0 bottom-0 z-40 px-3 py-3 safe-bottom bg-surface-container-lowest/98 border-t border-outline-variant flex gap-2 shadow-lg">
      <button v-if="canGoBack" type="button" class="px-4 py-2.5 border border-secondary text-secondary rounded-full text-sm" :disabled="busy" @click="goBack">
        قبل
      </button>
      <button
        v-if="canEditStepPricing"
        type="button"
        class="px-3 py-2.5 border border-primary text-primary rounded-full text-xs"
        :disabled="busy"
        @click="saveStepPricing"
      >
        ذخیره هزینه
      </button>
      <button v-if="isWorkingStep" type="button" class="flex-1 py-2.5 bg-primary text-on-primary rounded-full text-sm font-medium" :disabled="busy" @click="advanceStep">
        مرحله بعد
      </button>
    </div>
  </div>
</template>

<style scoped>
.field {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-low);
}
</style>
