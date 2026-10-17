<script setup lang="ts">
/**
 * نوار هدف یک نگله (C.2، H.7): `role="meter"` با `aria-valuetext` «۱۲٫۳۷ از ۱۳ متر». سه حالت با نشانه و نوشته، هرگز
 * فقط رنگ: کمتر از هدف (`outline-variant`)، در بازه (`success`)، بیشتر (`warning`، با همان پیامی که هدف را نام می‌برد:
 * «نگلهٔ ۳ شد ۱۵٫۱ متر؛ از ۱۳ متر هدف بیشتر است.»). بی هدف چیزی نشان نمی‌دهد.
 */
import { computed } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { message } from '@/lib/messages'
import { faLoose, targetFill, targetState, targetText, upperBound } from './baleModel'
import { toFixedString } from '@/lib/num'

const props = withDefaults(
  defineProps<{
    number: number
    areaM2: number
    pieces?: number
    usdTotal?: number
    target: number | null | undefined
    tolerancePercent: number
    /** کوچک (پانویس جدول): فقط نوار و نشانه، نوشته برای صفحه‌خوان */
    compact?: boolean
  }>(),
  { pieces: 0, usdTotal: 0, compact: false },
)

const state = computed(() => targetState(props.areaM2, props.target, props.tolerancePercent))
const fill = computed(() => targetFill(props.areaM2, props.target, props.tolerancePercent))
const text = computed(() =>
  targetText({ number: props.number, areaM2: props.areaM2, pieces: props.pieces, usdTotal: props.usdTotal }, props.target, props.tolerancePercent),
)
const valueText = computed(() =>
  props.target ? message('I_BOARD_TARGET_VALUE', { area: faLoose(props.areaM2), target: faLoose(props.target) }) : '',
)
const max = computed(() => (props.target ? Number(toFixedString(upperBound(props.target, props.tolerancePercent), 2)) : 0))

const tone = computed(() => {
  switch (state.value) {
    case 'above':
      return { bar: 'bg-warning', text: 'text-warning', icon: 'warning' }
    case 'within':
      return { bar: 'bg-success', text: 'text-success', icon: 'check' }
    default:
      return { bar: 'bg-outline-variant', text: 'text-on-surface-variant', icon: 'info' }
  }
})
</script>

<template>
  <div v-if="state !== 'none'" class="flex min-w-0 items-center gap-2" :data-target-state="state">
    <div
      role="meter"
      :aria-valuenow="areaM2"
      aria-valuemin="0"
      :aria-valuemax="max"
      :aria-valuetext="valueText"
      :aria-label="text"
      class="relative h-2.5 min-w-16 flex-1 overflow-hidden rounded-full bg-surface-container-highest"
      :class="compact ? 'max-w-32' : ''"
    >
      <div class="absolute inset-y-0 start-0 rounded-full" :class="tone.bar" :style="{ width: `${fill}%` }"></div>
    </div>
    <span class="inline-flex min-w-0 items-center gap-1 text-sm font-medium" data-numeric>
      <AppIcon :name="tone.icon" class="h-4 w-4" :class="tone.text" />
      <!--
        در پانویس فقط «بیشتر از هدف» نوشته می‌شود (هشداری که باید خوانده شود)، به رنگ متن همان زمینه تا کنتراست AA
        بماند (نارنجی روی زمینهٔ کهربایی کم‌رنگ است)؛ بقیه فقط برای صفحه‌خوان.
      -->
      <span :class="compact ? (state === 'above' ? '' : 'sr-only') : tone.text">{{ text }}</span>
    </span>
  </div>
</template>
