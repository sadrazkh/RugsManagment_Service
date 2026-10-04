<script setup lang="ts">
/**
 * ورودی مبلغ قدیمی با جداکنندهٔ هزارگان و پذیرش ارقام فارسی — فقط عدد صحیح.
 *
 * کد تازه از `components/ui/NumberField.vue` استفاده کند (اعشار، «۱۰م»، سطر «به حروف»، پرسش «10.000»)؛
 * این یکی تا فاز آخر روی صفحه‌های قدیمی می‌ماند. واحد پول را ثابت ننویسید: `suffix` را از
 * `currencyLabel()` (`lib/format`) بدهید تا کارگاه ریالی «ریال» ببیند.
 */
import { computed } from 'vue'
import { formatThousands, parseNumber } from '@/lib/money'

const model = defineModel<number | null | undefined>()
withDefaults(defineProps<{ placeholder?: string; suffix?: string }>(), { placeholder: '۰', suffix: undefined })

const display = computed({
  get: () => formatThousands(model.value ?? undefined),
  set: (v: string) => {
    model.value = parseNumber(v)
  },
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
      class="w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 py-2.5 text-right text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary"
      :class="{ 'pl-16': suffix }"
    />
    <span v-if="suffix" class="pointer-events-none absolute inset-y-0 left-3 flex items-center text-xs text-on-surface-variant">{{ suffix }}</span>
  </div>
</template>
