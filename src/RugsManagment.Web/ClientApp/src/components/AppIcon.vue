<script setup lang="ts">
/**
 * آیکون SVG از اسپرایت /icons.svg — معادل Vue برای تگ‌هلپر <icon> در رِیزور.
 * بدون label تزئینی است و از صفحه‌خوان پنهان می‌شود.
 */
withDefaults(
  defineProps<{
    name: string
    label?: string
  }>(),
  { label: undefined },
)
</script>

<template>
  <svg
    class="h-5 w-5 shrink-0"
    :aria-hidden="label ? undefined : 'true'"
    :focusable="label ? undefined : 'false'"
    :role="label ? 'img' : undefined"
    :aria-label="label"
  >
    <use :href="`/icons.svg#${name}`" />
  </svg>
</template>
