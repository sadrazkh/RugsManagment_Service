<script setup lang="ts">
/** قبل از رفتن به مرحله بعد — یادداشت و هزینه */
import { ref, watch } from 'vue'
import MoneyInput from '@/components/MoneyInput.vue'
import type { AdvanceRugStepPayload, RugWorkflowStep, ServiceProvider, StepPricingModel } from '@/types'
import { formatCurrency } from '@/utils/format'

const open = defineModel<boolean>('open', { default: false })
const props = defineProps<{
  step: RugWorkflowStep | null
  areaSquareMeters: number
  providers: ServiceProvider[]
}>()
const emit = defineEmits<{ confirm: [payload: AdvanceRugStepPayload] }>()

const notes = ref('')
const manualCost = ref<number | undefined>()
const unitRate = ref<number | undefined>()
const pricingModel = ref<StepPricingModel>('PerSquareMeter')
const providerId = ref<string | undefined>()

watch(open, (v) => {
  if (v) {
    notes.value = ''
    manualCost.value = undefined
    unitRate.value = undefined
    providerId.value = undefined
  }
})

function submit() {
  emit('confirm', {
    notes: notes.value.trim() || undefined,
    manualCostOverride: manualCost.value,
    pricingModel: pricingModel.value,
    unitRate: unitRate.value,
    serviceProviderId: providerId.value,
    markCompleted: true,
  })
  open.value = false
}
</script>

<template>
  <Teleport to="body">
    <div v-if="open && step" class="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-0 sm:p-4">
      <div class="absolute inset-0 bg-black/50" @click="open = false" />
      <div class="relative w-full sm:max-w-md bg-surface-container-lowest rounded-t-2xl sm:rounded-2xl p-5 space-y-4 max-h-[90vh] overflow-y-auto safe-bottom shadow-xl">
        <h3 class="text-lg font-bold flex items-center gap-2">
          <span class="material-symbols-outlined text-primary">{{ step.icon }}</span>
          تکمیل: {{ step.stepNameFa }}
        </h3>
        <p class="text-sm text-on-surface-variant">
          قبل از رفتن به مرحله بعد، یادداشت و هزینه را وارد کنید (در صورت نیاز).
          مساحت: {{ areaSquareMeters.toFixed(2) }} m²
        </p>
        <div>
          <label class="text-sm font-medium">یادداشت / توضیح کار</label>
          <textarea v-model="notes" rows="3" class="w-full mt-1 px-3 py-2 rounded-lg border border-outline-variant bg-surface-container-low" placeholder="مثلاً نوع دارکشی، عیب، ..." />
        </div>
        <div>
          <label class="text-sm font-medium">ارائه‌دهنده</label>
          <select v-model="providerId" class="w-full mt-1 px-3 py-2 rounded-lg border border-outline-variant">
            <option :value="undefined">—</option>
            <option v-for="p in providers" :key="p.id" :value="p.id">{{ p.name }}</option>
          </select>
        </div>
        <div>
          <label class="text-sm font-medium">روش هزینه</label>
          <select v-model="pricingModel" class="w-full mt-1 px-3 py-2 rounded-lg border border-outline-variant">
            <option value="Fixed">ثابت</option>
            <option value="PerSquareMeter">بر اساس m²</option>
          </select>
        </div>
        <div>
          <label class="text-sm font-medium">نرخ واحد (ریال)</label>
          <MoneyInput v-model="unitRate" class="mt-1 px-3 py-2 rounded-lg border border-outline-variant" />
        </div>
        <div>
          <label class="text-sm font-medium">هزینه دستی (جایگزین)</label>
          <MoneyInput v-model="manualCost" class="mt-1 px-3 py-2 rounded-lg border border-outline-variant" />
        </div>
        <p v-if="step.effectiveCost > 0" class="text-xs text-on-surface-variant">
          هزینه فعلی مرحله: {{ formatCurrency(step.effectiveCost) }}
        </p>
        <div class="flex gap-2 pt-2">
          <button type="button" class="flex-1 py-3 border border-outline-variant rounded-full" @click="open = false">انصراف</button>
          <button type="button" class="flex-1 py-3 bg-primary text-on-primary rounded-full font-medium" @click="submit">تأیید و مرحله بعد</button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
