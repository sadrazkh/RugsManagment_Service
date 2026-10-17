<script setup lang="ts">
/**
 * پانویس یک نگله، مثل اکسل بازرگان (H.1): «نگلهٔ ۱ · شامل ۴ تخته · ۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار» با نوار هدف. رنگ از
 * توکن‌های `bg-secondary-container` / `text-on-secondary-container` است، هرگز زرد ثابت (حالت تاریک هم درست می‌ماند).
 *
 * دو شکل: ردیف جدول (`as="row"`، یک خانه به پهنای همهٔ ستون‌ها) یا بلوک زیر کارت‌ها (`as="block"`). واژهٔ نگله از
 * `terms` است («عدل ۳» در کارگاهی که «عدل» می‌گوید).
 */
import { computed } from 'vue'
import BaleTargetBar from './BaleTargetBar.vue'
import { footerText, type BaleFigures } from './baleModel'

const props = withDefaults(
  defineProps<{
    bale: BaleFigures & { lines?: number }
    target: number | null | undefined
    tolerancePercent: number
    as?: 'row' | 'block'
    /** تعداد ستون‌های جدول (برای `colspan`) */
    colspan?: number
    /** شمارهٔ ردیف برای `aria-rowindex` */
    rowIndex?: number
  }>(),
  { as: 'row', colspan: 1, rowIndex: undefined },
)

const text = computed(() => footerText(props.bale))
</script>

<template>
  <tr v-if="as === 'row'" :aria-rowindex="rowIndex" data-bale-footer class="bg-secondary-container text-on-secondary-container">
    <td role="gridcell" :colspan="colspan" class="px-3 py-2">
      <div class="flex flex-wrap items-center gap-x-4 gap-y-1">
        <p class="font-semibold" data-numeric>{{ text }}</p>
        <BaleTargetBar
          class="min-w-48 flex-1"
          :number="bale.number"
          :area-m2="bale.areaM2"
          :pieces="bale.pieces"
          :usd-total="bale.usdTotal"
          :target="target"
          :tolerance-percent="tolerancePercent"
          compact
        />
      </div>
    </td>
  </tr>
  <div v-else data-bale-footer class="flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg bg-secondary-container px-3 py-2 text-on-secondary-container">
    <p class="font-semibold" data-numeric>{{ text }}</p>
    <BaleTargetBar
      class="min-w-48 flex-1"
      :number="bale.number"
      :area-m2="bale.areaM2"
      :pieces="bale.pieces"
      :usd-total="bale.usdTotal"
      :target="target"
      :tolerance-percent="tolerancePercent"
      compact
    />
  </div>
</template>
