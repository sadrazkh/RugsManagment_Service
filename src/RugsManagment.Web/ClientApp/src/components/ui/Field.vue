<script setup lang="ts">
/**
 * یک خانهٔ فرم: برچسب، کنترل، راهنما و پیام خطا — با شناسه‌های درست تا صفحه‌خوان هر سه را بخواند.
 *
 * کنترل از slot می‌آید و `id`، `describedBy` و `invalid` را می‌گیرد:
 *
 *   <Field label="طول" :error="errors.length" hint="به متر، مثل ۲٫۰۸" v-slot="f">
 *     <NumberField :id="f.id" :described-by="f.describedBy" :invalid="f.invalid" kind="dim" v-model="length" />
 *   </Field>
 */
import { computed, useId } from 'vue'

const props = withDefaults(
  defineProps<{
    label: string
    hint?: string
    error?: string | null
    required?: boolean
    /** شناسهٔ کنترل؛ پیش‌فرض خودکار */
    id?: string
    /** برچسب فقط برای صفحه‌خوان (وقتی ظاهر صفحه برچسب دیگری دارد) */
    hideLabel?: boolean
  }>(),
  { hint: undefined, error: null, required: false, id: undefined, hideLabel: false },
)

const auto = useId()
const controlId = computed(() => props.id ?? `${auto}-control`)
const hintId = computed(() => `${controlId.value}-hint`)
const errorId = computed(() => `${controlId.value}-error`)
// راهنما وقتی خطا هست پنهان است؛ پس فقط چیزی که دیده می‌شود به کنترل وصل می‌شود
const describedBy = computed(() => (props.error ? errorId.value : props.hint ? hintId.value : undefined))
</script>

<template>
  <div class="block">
    <label :for="controlId" class="mb-1 block text-sm font-medium text-on-surface" :class="{ 'sr-only': hideLabel }">
      {{ label }}<span v-if="required" class="text-error" aria-hidden="true"> *</span>
    </label>
    <slot :id="controlId" :describedBy="describedBy" :invalid="!!error" />
    <p v-if="hint && !error" :id="hintId" class="mt-1 text-xs text-on-surface-variant">{{ hint }}</p>
    <p v-if="error" :id="errorId" class="mt-1 flex items-start gap-1 text-xs text-error">
      <svg class="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" focusable="false"><use :href="'/icons.svg#error'" /></svg>
      <span>{{ error }}</span>
    </p>
  </div>
</template>
