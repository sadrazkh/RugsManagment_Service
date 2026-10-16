<script setup lang="ts">
/**
 * انتخاب روش تقسیم یک هزینهٔ «کل» (H.2 «[تغییر]»): «به نسبت متراژ»، «به تعداد تخته»، «به نسبت ارزش گمرکی»، «به نسبت
 * قیمت خرید»، «خودم سهم هر فرش را می‌نویسم» و — فقط وقتی دسته‌ای در فرش‌هاست — «مساوی برای هر ردیف».
 *
 * گروه رادیویی با کلیدهای جهت در RTL (H.7)؛ گزینهٔ انتخاب‌شده نشانهٔ تیک و نوشته دارد، نه فقط رنگ؛ هر گزینه ۴۴ پیکسل.
 * پایهٔ متراژ (اندازهٔ اول / نهایی) فقط در نمای کامل و فقط برای روش متراژی دیده می‌شود (A.3: در نمای ساده هرگز).
 */
import { computed, useId } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { AllocationMethod, AreaBasis, type CostGroup } from '@/lib/types/costs'
import { AREA_BASIS_LABELS, availableMethods, METHOD_LABELS, radioKeyTarget } from './costText'

const props = withDefaults(
  defineProps<{
    modelValue: AllocationMethod
    areaBasis: AreaBasis
    group: CostGroup | null
    /** فرش‌ها دسته دارند (تخته ≠ ردیف) */
    hasLot: boolean
    /** نمای کامل: کنترل پایهٔ متراژ هم دیده شود */
    showAreaBasis?: boolean
  }>(),
  { showAreaBasis: false },
)

const emit = defineEmits<{
  'update:modelValue': [value: AllocationMethod]
  'update:areaBasis': [value: AreaBasis]
}>()

const uid = useId()

const methods = computed(() => {
  const list = availableMethods({ group: props.group, hasLot: props.hasLot })
  // روشِ ثبتِ موجود حتی اگر دیگر پیشنهاد نمی‌شود (مثلاً «مساوی» بی‌دسته) دیده بماند
  if (!list.includes(props.modelValue)) list.push(props.modelValue)
  return list
})

const areaOptions = [AreaBasis.Intake, AreaBasis.Effective] as const

function onKeydown(event: KeyboardEvent, index: number, count: number, pick: (i: number) => void) {
  const next = radioKeyTarget(event.key, index, count)
  if (next === null) return
  event.preventDefault()
  pick(next)
  const group = (event.currentTarget as HTMLElement).closest('[role="radiogroup"]')
  group?.querySelectorAll<HTMLElement>('[role="radio"]')[next]?.focus()
}

function pickMethod(i: number) {
  const m = methods.value[i]
  if (m !== undefined && m !== props.modelValue) emit('update:modelValue', m)
}

function pickArea(i: number) {
  const a = areaOptions[i]
  if (a !== undefined && a !== props.areaBasis) emit('update:areaBasis', a)
}
</script>

<template>
  <div class="space-y-3">
    <div role="radiogroup" :aria-labelledby="`${uid}-method`" class="grid gap-2">
      <p :id="`${uid}-method`" class="text-sm font-semibold text-on-surface">روش تقسیم</p>
      <button
        v-for="(m, i) in methods"
        :key="m"
        type="button"
        role="radio"
        :aria-checked="m === modelValue"
        :tabindex="m === modelValue ? 0 : -1"
        class="flex min-h-11 items-center gap-2 rounded-lg border px-3 text-start text-sm"
        :class="m === modelValue
          ? 'border-primary bg-primary/10 font-semibold text-on-surface'
          : 'border-outline-variant bg-surface-container-lowest text-on-surface hover:bg-surface-container'"
        :data-method="m"
        @click="pickMethod(i)"
        @keydown="onKeydown($event, i, methods.length, pickMethod)"
      >
        <AppIcon v-if="m === modelValue" name="check" class="h-4 w-4 shrink-0 text-primary" />
        <span v-else class="h-4 w-4 shrink-0" aria-hidden="true" />
        {{ METHOD_LABELS[m] }}
      </button>
    </div>

    <div
      v-if="showAreaBasis && modelValue === AllocationMethod.ByArea"
      role="radiogroup"
      :aria-labelledby="`${uid}-area`"
      class="space-y-2"
    >
      <p :id="`${uid}-area`" class="text-sm font-semibold text-on-surface">متراژ کدام اندازه؟</p>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="(a, i) in areaOptions"
          :key="a"
          type="button"
          role="radio"
          :aria-checked="a === areaBasis"
          :tabindex="a === areaBasis ? 0 : -1"
          class="inline-flex min-h-11 items-center gap-2 rounded-lg border px-3 text-sm"
          :class="a === areaBasis
            ? 'border-primary bg-primary/10 font-semibold'
            : 'border-outline-variant bg-surface-container-lowest hover:bg-surface-container'"
          @click="pickArea(i)"
          @keydown="onKeydown($event, i, areaOptions.length, pickArea)"
        >
          <AppIcon v-if="a === areaBasis" name="check" class="h-4 w-4 shrink-0 text-primary" />
          {{ AREA_BASIS_LABELS[a] }}
        </button>
      </div>
    </div>
  </div>
</template>
