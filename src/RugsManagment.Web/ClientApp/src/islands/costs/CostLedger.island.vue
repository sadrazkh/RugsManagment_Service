<script setup lang="ts">
/**
 * جزیرهٔ `cost-ledger` (صفحهٔ «هزینه‌ها»، `/Costs`، Business؛ I.4، I.5): دفتر همهٔ ثبت‌های هزینه با فیلتر نوع، تاریخ و
 * تراشهٔ «تقسیم‌نشده»، و دکمهٔ «ثبت هزینه» که پنل `cost-entry` را باز می‌کند.
 *
 * props: `{ filters: { shipmentId?, batchId?, categoryId?, rugId? }, openNew, preset?, compact? }`. `openNew` (از
 * `/Costs?new=1`، مثلاً فرمان Ctrl+K «ثبت هزینه») پنل را همان لحظه باز می‌کند؛ `preset` «برای» آن پنل است. صفحهٔ فرش همین
 * جزیره را فشرده و با `rugId` نشان می‌دهد (بخش «هزینه‌ها»ی فرش، H.1).
 */
import { onMounted } from 'vue'
import CostList from '@/components/costs/CostList.vue'
import { CostScope, type CostPanelPreset } from '@/lib/types/costs'
import { openPanel } from '@/lib/ui'

const props = withDefaults(
  defineProps<{
    filters?: { shipmentId?: string | null; batchId?: string | null; categoryId?: string | null; rugId?: string | null }
    openNew?: boolean
    preset?: CostPanelPreset | null
    compact?: boolean
    unallocatedOnly?: boolean
  }>(),
  { filters: () => ({}), openNew: false, preset: null, compact: false, unallocatedOnly: false },
)

const panelPreset: CostPanelPreset = {
  ...(props.filters.shipmentId ? { scope: CostScope.Shipment, shipmentId: props.filters.shipmentId } : {}),
  ...(props.filters.batchId ? { scope: CostScope.IntakeBatch, batchId: props.filters.batchId } : {}),
  ...(props.filters.rugId ? { scope: CostScope.Selection, rugIds: [props.filters.rugId] } : {}),
  ...(props.preset ?? {}),
}

onMounted(() => {
  if (props.openNew) void openPanel('cost-entry', { ...panelPreset })
})
</script>

<template>
  <CostList
    :shipment-id="filters.shipmentId ?? null"
    :batch-id="filters.batchId ?? null"
    :category-id="filters.categoryId ?? null"
    :rug-id="filters.rugId ?? null"
    :preset="panelPreset"
    :show-filters="!compact"
    :compact="compact"
    :initial-unallocated="unallocatedOnly"
  />
</template>
