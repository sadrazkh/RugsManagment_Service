<script setup lang="ts">
/** ورودی مبلغ با جداکنندهٔ هزارگان و پذیرش ارقام فارسی. */
import { computed } from 'vue'
import { formatThousands, parseNumber } from '@/lib/money'

const model = defineModel<number | undefined>()
withDefaults(defineProps<{ placeholder?: string; suffix?: string }>(), { placeholder: '۰' })

const display = computed({
  get: () => formatThousands(model.value ?? undefined),
  set: (v: string) => { model.value = parseNumber(v) },
})
</script>

<template>
  <div class="relative">
    <input
      v-model="display"
      type="text"
      inputmode="numeric"
      dir="ltr"
      :placeholder="placeholder"
      class="w-full rounded-lg border border-outline-variant px-3 py-2.5 text-end outline-none focus:border-primary focus:ring-1 focus:ring-primary"
      :class="{ 'pe-14': suffix }"
    />
    <span v-if="suffix" class="pointer-events-none absolute inset-y-0 left-3 flex items-center text-xs text-on-surface-variant">{{ suffix }}</span>
  </div>
</template>
