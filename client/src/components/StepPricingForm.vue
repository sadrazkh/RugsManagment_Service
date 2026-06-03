<script setup lang="ts">
/**
 * فرم هزینهٔ یک مرحله — ثابت، طول، عرض، m²، یا ترکیبی
 */
import { computed } from 'vue'
import MoneyInput from '@/components/MoneyInput.vue'
import type { CombinedPricingItem, Rug, StepPricingModel } from '@/types'
import { formatCurrency } from '@/utils/format'

const pricingModel = defineModel<StepPricingModel>('pricingModel', { required: true })
const unitRate = defineModel<number | undefined>('unitRate')
const manualCost = defineModel<number | undefined>('manualCost')
const combinedItems = defineModel<CombinedPricingItem[]>('combinedItems', { required: true })

const props = defineProps<{
  rug: Rug
  disabled?: boolean
}>()

const preview = computed(() => {
  if (manualCost.value != null) return manualCost.value
  const r = props.rug
  const rate = unitRate.value ?? 0
  const line = (model: StepPricingModel, rt: number) => {
    switch (model) {
      case 'Fixed':
        return rt
      case 'PerSquareMeter':
        return rt * r.areaSquareMeters
      case 'PerLength':
        return rt * r.lengthMeters
      case 'PerWidth':
        return rt * r.widthMeters
      default:
        return 0
    }
  }
  if (pricingModel.value === 'Combined') {
    return combinedItems.value.reduce((sum, item) => sum + line(item.model, item.rate ?? 0), 0)
  }
  return line(pricingModel.value, rate)
})

function addCombinedLine() {
  combinedItems.value = [...combinedItems.value, { model: 'Fixed', rate: undefined }]
}

function removeCombinedLine(index: number) {
  if (combinedItems.value.length <= 1) return
  combinedItems.value = combinedItems.value.filter((_, i) => i !== index)
}
</script>

<template>
  <div class="space-y-3" :class="{ 'opacity-60 pointer-events-none': disabled }">
    <p class="text-sm text-on-surface-variant">
      ابعاد: {{ rug.widthMeters }} × {{ rug.lengthMeters }} متر — مساحت {{ rug.areaSquareMeters.toFixed(2) }} m²
    </p>

    <div>
      <label class="text-sm font-medium">روش محاسبه هزینه</label>
      <select v-model="pricingModel" class="field mt-1">
        <option value="Fixed">مبلغ ثابت</option>
        <option value="PerLength">نرخ × طول (متر)</option>
        <option value="PerWidth">نرخ × عرض (متر)</option>
        <option value="PerSquareMeter">نرخ × مساحت (m²)</option>
        <option value="Combined">ترکیبی (چند بند)</option>
      </select>
    </div>

    <div v-if="pricingModel !== 'Combined'">
      <label class="text-sm font-medium">نرخ واحد (ریال)</label>
      <MoneyInput v-model="unitRate" class="field mt-1" />
      <p class="text-xs text-on-surface-variant mt-1">
        پیش‌نمایش: {{ formatCurrency(Math.round(preview)) }}
      </p>
    </div>

    <div v-else class="space-y-2">
      <p class="text-xs text-on-surface-variant">هر بند جدا محاسبه و جمع می‌شود.</p>
      <div
        v-for="(item, idx) in combinedItems"
        :key="idx"
        class="grid grid-cols-[1fr_1fr_auto] gap-2 items-end p-2 rounded-lg bg-surface-container"
      >
        <div>
          <label class="text-xs">نوع</label>
          <select v-model="item.model" class="field mt-0.5 text-sm">
            <option value="Fixed">ثابت</option>
            <option value="PerLength">× طول</option>
            <option value="PerWidth">× عرض</option>
            <option value="PerSquareMeter">× m²</option>
          </select>
        </div>
        <div>
          <label class="text-xs">نرخ (ریال)</label>
          <MoneyInput v-model="item.rate" class="field mt-0.5" />
        </div>
        <button
          type="button"
          class="p-2 text-error rounded-full hover:bg-error/10"
          :disabled="combinedItems.length <= 1"
          title="حذف بند"
          @click="removeCombinedLine(idx)"
        >
          <span class="material-symbols-outlined text-lg">close</span>
        </button>
      </div>
      <button
        type="button"
        class="text-sm text-primary font-medium"
        @click="addCombinedLine"
      >
        + بند هزینه
      </button>
      <p class="text-xs text-on-surface-variant">
        جمع پیش‌نمایش: {{ formatCurrency(Math.round(preview)) }}
      </p>
    </div>

    <div>
      <label class="text-sm font-medium">هزینه دستی (جایگزین محاسبه)</label>
      <MoneyInput v-model="manualCost" class="field mt-1" />
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
