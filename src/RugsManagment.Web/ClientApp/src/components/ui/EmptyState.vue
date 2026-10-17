<script setup lang="ts">
/**
 * حالت خالی: یک جمله که بگوید چرا خالی است و یک دکمه که کار بعدی را شروع کند (H.3: «انبار خالی است.
 * [ثبت فرش تازه]»). متن‌ها را صدازننده می‌دهد؛ دکمه‌ها در slot `actions` می‌آیند.
 */
import AppIcon from '@/components/AppIcon.vue'

withDefaults(
  defineProps<{
    title: string
    text?: string
    icon?: string
    /** کوچک‌تر، برای داخل کارت */
    compact?: boolean
  }>(),
  { text: undefined, icon: 'info', compact: false },
)
</script>

<template>
  <div
    class="rounded-xl border border-dashed border-outline-variant bg-surface-container-lowest text-center"
    :class="compact ? 'p-6' : 'p-10 sm:p-12'"
  >
    <span
      class="mx-auto mb-3 grid place-items-center rounded-full bg-surface-container text-on-surface-variant"
      :class="compact ? 'h-11 w-11' : 'h-14 w-14'"
    >
      <AppIcon :name="icon" :class="compact ? 'h-5 w-5' : 'h-7 w-7'" />
    </span>
    <p class="font-medium text-on-surface">{{ title }}</p>
    <p v-if="text" class="mx-auto mt-1 max-w-md text-sm text-on-surface-variant">{{ text }}</p>
    <div v-if="$slots.actions" class="mt-5 flex flex-wrap items-center justify-center gap-3">
      <slot name="actions" />
    </div>
  </div>
</template>
