<script setup lang="ts">
/**
 * جزیرهٔ `shipment-costs` — زبانهٔ «هزینه‌ها»ی کارگاه محموله (H.1، I.5؛ Business): هزینه‌هایی که به این محموله وصل‌اند
 * یا فرشی از آن را دارند، با «ثبت هزینه» که پنل را با «برای: این محموله» باز می‌کند.
 *
 * محمولهٔ قطعی هم هزینهٔ تازه می‌پذیرد (D.7 #12)؛ فقط نوار «این محموله قطعی شده؛ هزینهٔ تازه فقط سود را تغییر می‌دهد، نه
 * اظهار را.» بالای فهرست می‌آید. محمولهٔ بی‌فرش هزینه‌اش را «تقسیم‌نشده» نشان می‌دهد (تراشهٔ وضعیت هر ردیف).
 */
import AppIcon from '@/components/AppIcon.vue'
import CostList from '@/components/costs/CostList.vue'
import { message } from '@/lib/messages'
import { CostScope } from '@/lib/types/costs'

const props = withDefaults(defineProps<{ shipmentId: string; locked?: boolean }>(), { locked: false })

const preset = { scope: CostScope.Shipment, shipmentId: props.shipmentId }
</script>

<template>
  <div class="space-y-4" :data-shipment-costs="shipmentId">
    <p v-if="locked" class="flex items-start gap-2 rounded-lg bg-surface-container-high px-4 py-3 text-sm" role="status" data-late-cost>
      <AppIcon name="lock" class="mt-0.5 h-4 w-4 shrink-0" />
      {{ message('W_LATE_COST') }}
    </p>
    <CostList :shipment-id="shipmentId" :preset="preset" />
  </div>
</template>
