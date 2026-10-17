<script setup lang="ts">
/**
 * گام‌شمار — مثلاً نوار چسبان محموله «ورود · نگله‌بندی · هزینه · خروجی» با شمار هر گام.
 *
 * گام جاری `aria-current="step"` دارد و گام انجام‌شده آیکون تیک + نوشتهٔ «انجام شد» (نه فقط رنگ). با
 * `href` هر گام پیوند است، وگرنه دکمه‌ای که `select` می‌فرستد. روی صفحهٔ باریک افقی اسکرول می‌خورد.
 */
import AppIcon from '@/components/AppIcon.vue'
import { toPersianDigits } from '@/lib/format'

export interface StepperStep {
  key: string
  label: string
  /** شمار یا خلاصهٔ کوتاه زیر برچسب، مثل «۲۴۲ تخته» */
  note?: string
  done?: boolean
  disabled?: boolean
  href?: string
}

withDefaults(
  defineProps<{
    steps: StepperStep[]
    current: string
    /** برچسب صفحه‌خوان کل نوار */
    label?: string
    sticky?: boolean
  }>(),
  { label: 'گام‌ها', sticky: false },
)

const emit = defineEmits<{ select: [key: string] }>()
</script>

<template>
  <nav
    :aria-label="label"
    class="overflow-x-auto rounded-xl border border-outline-variant bg-surface-container-lowest"
    :class="{ 'sticky top-0 z-30 shadow-sm': sticky }"
  >
    <ol class="flex min-w-max items-stretch">
      <li v-for="(step, index) in steps" :key="step.key" class="flex items-stretch">
        <component
          :is="step.href && !step.disabled ? 'a' : 'button'"
          :href="step.href && !step.disabled ? step.href : undefined"
          :type="step.href && !step.disabled ? undefined : 'button'"
          :disabled="!step.href && step.disabled ? true : undefined"
          :aria-current="step.key === current ? 'step' : undefined"
          class="flex min-h-14 items-center gap-3 px-4 py-2 text-start"
          :class="step.key === current ? 'bg-primary/8 text-primary' : 'text-on-surface hover:bg-surface-container'"
          @click="!step.href && !step.disabled && emit('select', step.key)"
        >
          <span
            class="grid h-8 w-8 shrink-0 place-items-center rounded-full text-sm font-semibold"
            :class="step.done
              ? 'bg-success/12 text-success'
              : step.key === current
                ? 'bg-primary text-on-primary'
                : 'bg-surface-container text-on-surface-variant'"
            data-numeric
          >
            <AppIcon v-if="step.done" name="check" class="h-4 w-4" />
            <template v-else>{{ toPersianDigits(index + 1) }}</template>
          </span>
          <span class="min-w-0">
            <span class="block font-medium">{{ step.label }}</span>
            <span v-if="step.note || step.done" class="block text-xs text-on-surface-variant">
              <template v-if="step.done">انجام شد</template><template v-if="step.done && step.note"> · </template>{{ step.note }}
            </span>
          </span>
        </component>
        <span v-if="index < steps.length - 1" class="my-3 w-px bg-outline-variant" aria-hidden="true"></span>
      </li>
    </ol>
  </nav>
</template>
