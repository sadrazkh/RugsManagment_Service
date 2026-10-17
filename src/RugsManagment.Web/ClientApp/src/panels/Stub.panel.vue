<script setup lang="ts">
/**
 * پنل آزمایشی (نام: «stub») — فقط برای پذیرش میزبان پنل‌ها: یک دکمهٔ Razor با
 * `data-open-panel="stub" data-panel-props='{…}'` آن را بی‌اسکریپت درون‌خطی باز می‌کند (site.js → openPanel)
 * و props رسیده را نشان می‌دهد. پنل‌های واقعی (مثل `CostEntry.panel.vue`) همین قرارداد را دارند: props از
 * openPanel، و emit('close', نتیجه?) برای بستن.
 *
 * props آزاد است (هر کلیدی)، پس به‌جای defineProps از attrs خوانده می‌شود.
 */
import { computed, useAttrs } from 'vue'

defineOptions({ inheritAttrs: false })

const emit = defineEmits<{ close: [result?: unknown] }>()
const attrs = useAttrs()

const received = computed(() => JSON.stringify(attrs, null, 2))
</script>

<template>
  <div class="space-y-4">
    <h2 class="text-xl font-bold">پنل آزمایشی</h2>
    <p class="text-on-surface-variant">
      این پنل فقط برای آزمودن میزبان پنل‌هاست. اگر آن را می‌بینید، دکمهٔ صفحه بدون اسکریپت درون‌خطی کار کرده است.
    </p>

    <section aria-labelledby="stub-props-title">
      <h3 id="stub-props-title" class="mb-2 text-sm font-semibold text-on-surface-variant">داده‌ای که به پنل رسید</h3>
      <pre
        class="overflow-x-auto rounded-lg border border-outline-variant bg-surface-container-low p-3 font-mono text-sm"
        dir="ltr"
      >{{ received }}</pre>
    </section>

    <div class="flex flex-wrap gap-3">
      <button
        type="button"
        class="min-h-11 rounded-lg bg-primary px-5 font-semibold text-on-primary hover:bg-primary-hover"
        data-autofocus
        @click="emit('close', 'ok')"
      >
        بستن
      </button>
    </div>
  </div>
</template>
