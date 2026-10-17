<script setup lang="ts">
/**
 * سرِ «نگلهٔ جاری» (C.2، H.1، G.5): «نگلهٔ جاری: ۳ ▾ [نگلهٔ بعد]»، جمع همین نگله و نوار هدفش. یک مدل برای جدول،
 * کارت‌ها و حالت اسکن.
 *
 * - انتخاب نگله یک `<select>` بومی است (۴۴ پیکسل، صفحه‌خوان و صفحه‌کلید بی دردسر)، با گزینهٔ «نگلهٔ تازه» در ته.
 * - «نگلهٔ بعد» فقط جاری را «بزرگ‌ترین + ۱» می‌کند؛ نگله را اولین فرشش می‌سازد، پس نگلهٔ خالی جا نمی‌ماند.
 * - جمع‌ها در ناحیهٔ `aria-live="polite"` خوانده می‌شوند: «نگلهٔ ۳: ۴ تخته، ۱۳٫۵۱ متر» (H.6).
 */
import { computed, useId } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { appContext } from '@/lib/context'
import { message } from '@/lib/messages'
import type { BaleSummary } from '@/lib/types/shipments'
import BaleTargetBar from './BaleTargetBar.vue'
import { announceText, currentSummaryText, faDigitsOf, maxBaleNumber } from './baleModel'

type BaleLike = Pick<BaleSummary, 'id' | 'number' | 'pieces' | 'areaM2' | 'usdTotal'>

const props = withDefaults(
  defineProps<{
    bales: readonly BaleLike[]
    current: number
    target: number | null | undefined
    tolerancePercent: number
    locked?: boolean
    /** بزرگ (حالت اسکن گوشی) */
    large?: boolean
  }>(),
  { locked: false, large: false },
)

const emit = defineEmits<{ 'update:current': [value: number]; next: [] }>()

const terms = appContext().terms
const uid = useId()
const selectId = `${uid}-current`
const max = computed(() => maxBaleNumber(props.bales))
const bale = computed(() => props.bales.find((b) => b.number === props.current) ?? null)
const isNew = computed(() => !bale.value)

const options = computed(() => {
  const list = [...props.bales]
    .sort((a, b) => a.number - b.number)
    .map((b) => ({ value: b.number, label: faDigitsOf(b.number) }))
  // شمارهٔ جاری که هنوز فرشی ندارد (پس از «نگلهٔ بعد») و «نگلهٔ تازه»
  const fresh = Math.max(max.value + 1, 1)
  if (!list.some((o) => o.value === props.current)) list.push({ value: props.current, label: `${faDigitsOf(props.current)} (${message('I_BOARD_NEW_BALE')})` })
  if (!list.some((o) => o.value === fresh)) list.push({ value: fresh, label: `${faDigitsOf(fresh)} (${message('I_BOARD_NEW_BALE')})` })
  return list.sort((a, b) => a.value - b.value)
})

const summary = computed(() => currentSummaryText(bale.value, props.current))
const announcement = computed(() =>
  bale.value ? announceText(bale.value) : message('I_BOARD_CURRENT_EMPTY', { n: props.current }),
)

function onSelect(event: Event) {
  const value = Number((event.target as HTMLSelectElement).value)
  if (Number.isSafeInteger(value) && value >= 1) emit('update:current', value)
}

/** «نگلهٔ بعد» وقتی جاری خودش هنوز خالی است کاری ندارد (همان «بزرگ‌ترین + ۱» است). */
const nextDisabled = computed(() => props.locked || (isNew.value && props.current === max.value + 1))
</script>

<template>
  <section
    class="rounded-xl border border-outline-variant bg-surface-container-lowest"
    :class="large ? 'p-4' : 'px-4 py-3'"
    :aria-labelledby="`${uid}-title`"
  >
    <div class="flex flex-wrap items-center gap-x-3 gap-y-2">
      <label :id="`${uid}-title`" :for="selectId" class="font-semibold" :class="large ? 'text-lg' : 'text-base'">
        {{ message('I_BOARD_CURRENT') }}:
      </label>
      <div class="relative">
        <select
          :id="selectId"
          :value="current"
          :disabled="locked"
          class="min-h-11 appearance-none rounded-lg border border-outline-variant bg-surface-container-lowest pe-9 ps-3 text-lg font-bold text-on-surface focus:border-primary"
          :class="large ? 'min-h-14 text-2xl' : ''"
          data-numeric
          @change="onSelect"
        >
          <option v-for="o in options" :key="o.value" :value="o.value">{{ o.label }}</option>
        </select>
        <AppIcon name="chevron-down" class="pointer-events-none absolute inset-y-0 end-2.5 my-auto h-5 w-5 text-on-surface-variant" />
      </div>
      <button
        type="button"
        class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-4 text-sm font-semibold text-on-surface hover:bg-surface-container"
        :class="large ? 'min-h-14 text-base' : ''"
        :disabled="nextDisabled"
        @click="emit('next')"
      >
        <AppIcon name="plus" class="h-5 w-5" />
        {{ message('I_BOARD_NEXT') }}
      </button>
      <slot name="actions" />
    </div>

    <div class="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1">
      <p class="text-sm" :class="isNew ? 'text-on-surface-variant' : 'font-medium text-on-surface'" data-numeric>
        <span class="sr-only">{{ terms.baleOf }} {{ faDigitsOf(current) }}: </span>{{ summary }}
      </p>
      <BaleTargetBar
        v-if="bale"
        class="min-w-48 flex-1"
        :number="bale.number"
        :area-m2="bale.areaM2"
        :pieces="bale.pieces"
        :usd-total="bale.usdTotal"
        :target="target"
        :tolerance-percent="tolerancePercent"
      />
    </div>

    <!-- جمع نگلهٔ جاری برای صفحه‌خوان؛ با هر افزودن، خروج و اندازه خوانده می‌شود -->
    <p class="sr-only" role="status" aria-live="polite" aria-atomic="true">{{ announcement }}</p>
  </section>
</template>
