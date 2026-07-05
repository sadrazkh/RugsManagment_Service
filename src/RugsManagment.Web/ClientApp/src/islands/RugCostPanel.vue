<script setup lang="ts">
/**
 * ثبت هزینهٔ مرحلهٔ جاری فرش + تکمیل مرحله.
 * محاسبهٔ نهایی در بک‌اند انجام می‌شود؛ عدد «تخمین» صرفاً برای پیش‌نمایش فوری است و
 * پس از ذخیره، مقدار واقعیِ محاسبه‌شدهٔ بک‌اند نمایش داده می‌شود.
 */
import { computed, onMounted, ref } from 'vue'
import { api } from '@/lib/api'
import { formatThousands } from '@/lib/money'
import MoneyInput from '@/components/MoneyInput.vue'
import type { Rug } from '@/lib/types'

const props = defineProps<{ rugId: string }>()

interface Provider { id: string; name: string }

const rug = ref<Rug | null>(null)
const providers = ref<Provider[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const savedCost = ref<number | null>(null)

const model = ref<number>(1) // پیش‌فرض: متری مربع
const manual = ref(false)
const unitRate = ref<number | undefined>()
const manualCost = ref<number | undefined>()
const notes = ref('')
const providerId = ref('')

const currentStep = computed(() =>
  rug.value?.workflowSteps.find((s) => s.status === 1) ?? null)

const estimate = computed(() => {
  if (!rug.value) return 0
  if (manual.value) return manualCost.value ?? 0
  const rate = unitRate.value ?? 0
  switch (model.value) {
    case 0: return rate
    case 1: return Math.round(rate * rug.value.areaSquareMeters * 100) / 100
    case 4: return Math.round(rate * rug.value.lengthMeters * 100) / 100
    case 5: return Math.round(rate * rug.value.widthMeters * 100) / 100
    default: return rate
  }
})

async function load() {
  loading.value = true
  try {
    const [r, p] = await Promise.all([
      api.get<Rug>(`/api/rugs/${props.rugId}`),
      api.get<Provider[]>('/api/lookups/service-providers'),
    ])
    rug.value = r
    providers.value = p
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

function payload(markCompleted: boolean) {
  return {
    serviceProviderId: providerId.value || null,
    manualCostOverride: manual.value ? (manualCost.value ?? 0) : null,
    pricingModel: manual.value ? null : model.value,
    unitRate: manual.value ? null : (unitRate.value ?? null),
    pricingConfigJson: null,
    fieldValuesJson: null,
    notes: notes.value || null,
    markCompleted,
  }
}

async function saveCost() {
  if (!currentStep.value) return
  error.value = ''
  busy.value = true
  try {
    const updated = await api.put<Rug>(
      `/api/rugs/${props.rugId}/steps/${currentStep.value.id}/pricing`, payload(false))
    rug.value = updated
    savedCost.value = updated.workflowSteps.find((s) => s.id === currentStep.value!.id)?.effectiveCost ?? null
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busy.value = false
  }
}

async function complete() {
  if (!currentStep.value) return
  error.value = ''
  busy.value = true
  try {
    await api.post<Rug>(
      `/api/rugs/${props.rugId}/steps/${currentStep.value.id}/advance`, payload(true))
    window.location.reload() // صفحهٔ جزئیات سرور-رندر تازه شود
  } catch (e) {
    error.value = (e as Error).message
    busy.value = false
  }
}

async function goBack() {
  error.value = ''
  busy.value = true
  try {
    await api.post<Rug>(`/api/rugs/${props.rugId}/workflow/back`, undefined)
    window.location.reload()
  } catch (e) {
    error.value = (e as Error).message
    busy.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="rounded-xl border border-outline-variant bg-white p-5 shadow-sm">
    <h2 class="mb-4 flex items-center justify-between text-sm font-semibold text-primary">
      ثبت هزینهٔ مرحله
    </h2>

    <div v-if="loading" class="py-6 text-center text-on-surface-variant">در حال بارگذاری…</div>
    <div v-else-if="error && !rug" class="rounded-lg bg-error-container px-4 py-3 text-sm text-error">{{ error }}</div>
    <div v-else-if="!currentStep" class="rounded-lg bg-surface-container px-4 py-6 text-center text-on-surface-variant">
      مرحلهٔ فعالی برای ثبت هزینه وجود ندارد.
    </div>

    <div v-else class="space-y-4">
      <div class="rounded-lg bg-primary/5 px-4 py-3 text-sm">
        مرحلهٔ جاری: <strong class="text-primary">{{ currentStep.stepNameFa }}</strong>
      </div>

      <div v-if="error" class="rounded-lg bg-error-container px-4 py-2 text-sm text-error">{{ error }}</div>

      <label class="flex items-center gap-2 text-sm">
        <input v-model="manual" type="checkbox" class="h-4 w-4 rounded border-outline-variant text-primary" />
        مبلغ دستی (بدون فرمول)
      </label>

      <div v-if="!manual" class="grid gap-3 sm:grid-cols-2">
        <label class="block">
          <span class="mb-1 block text-sm">روش محاسبه</span>
          <select v-model.number="model" class="w-full rounded-lg border border-outline-variant px-3 py-2.5 outline-none focus:border-primary">
            <option :value="0">ثابت</option>
            <option :value="1">نرخ × مساحت</option>
            <option :value="4">نرخ × طول</option>
            <option :value="5">نرخ × عرض</option>
          </select>
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">نرخ واحد (تومان)</span>
          <MoneyInput v-model="unitRate" suffix="تومان" />
        </label>
      </div>
      <div v-else>
        <label class="block">
          <span class="mb-1 block text-sm">مبلغ هزینه (تومان)</span>
          <MoneyInput v-model="manualCost" suffix="تومان" />
        </label>
      </div>

      <div class="grid gap-3 sm:grid-cols-2">
        <label v-if="providers.length" class="block">
          <span class="mb-1 block text-sm">انجام‌دهنده</span>
          <select v-model="providerId" class="w-full rounded-lg border border-outline-variant px-3 py-2.5 outline-none focus:border-primary">
            <option value="">—</option>
            <option v-for="p in providers" :key="p.id" :value="p.id">{{ p.name }}</option>
          </select>
        </label>
        <label class="block">
          <span class="mb-1 block text-sm">توضیح</span>
          <input v-model="notes" class="w-full rounded-lg border border-outline-variant px-3 py-2.5 outline-none focus:border-primary" />
        </label>
      </div>

      <!-- پیش‌نمایش -->
      <div class="flex items-center justify-between rounded-lg border border-dashed border-outline-variant px-4 py-3">
        <span class="text-sm text-on-surface-variant">{{ savedCost !== null ? 'هزینهٔ ثبت‌شده (محاسبهٔ بک‌اند)' : 'تخمین' }}</span>
        <span class="font-bold" dir="ltr">{{ formatThousands(savedCost ?? estimate) }} تومان</span>
      </div>

      <div class="flex flex-wrap gap-2">
        <button :disabled="busy" @click="saveCost"
                class="flex-1 rounded-lg border border-outline-variant px-4 py-2.5 hover:bg-surface-container disabled:opacity-60">
          ذخیرهٔ هزینه
        </button>
        <button :disabled="busy" @click="complete"
                class="flex-1 rounded-lg bg-primary px-4 py-2.5 font-semibold text-on-primary hover:opacity-90 disabled:opacity-60">
          تکمیل و مرحلهٔ بعد
        </button>
        <button :disabled="busy" @click="goBack"
                class="rounded-lg border border-outline-variant px-4 py-2.5 text-on-surface-variant hover:bg-surface-container disabled:opacity-60">
          مرحلهٔ قبل
        </button>
      </div>
    </div>
  </div>
</template>
