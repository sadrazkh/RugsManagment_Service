<script setup lang="ts">
/** کارت آماری داشبورد */
defineProps<{
  label: string
  value: string | number
  icon: string
  accent?: 'primary' | 'secondary' | 'tertiary'
}>()
</script>

<template>
  <div class="card-texture bg-surface-container-lowest border border-outline-variant rounded-xl p-4 sm:p-5 shadow-sm">
    <div class="flex items-start justify-between gap-3">
      <div>
        <p class="text-sm text-on-surface-variant">{{ label }}</p>
        <p class="text-2xl sm:text-3xl font-bold mt-1 tabular-nums">{{ value }}</p>
      </div>
      <span
        class="material-symbols-outlined text-3xl p-2 rounded-lg"
        :class="{
          'bg-primary/10 text-primary': accent === 'primary' || !accent,
          'bg-secondary-container text-on-secondary-container': accent === 'secondary',
          'bg-tertiary-container text-on-tertiary-container': accent === 'tertiary',
        }"
      >{{ icon }}</span>
    </div>
  </div>
</template>
