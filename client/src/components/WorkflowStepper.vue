<script setup lang="ts">
/** مسیر گردش — کلیک برای دیدن/فعال‌سازی مرحله */
import type { RugWorkflowStep } from '@/types'
import { formatCurrency, statusLabel } from '@/utils/format'

defineProps<{
  steps: RugWorkflowStep[]
  selectedId?: string
}>()

const emit = defineEmits<{ select: [step: RugWorkflowStep] }>()
</script>

<template>
  <div class="overflow-x-auto pb-2 -mx-1 px-1">
    <ol class="flex items-start gap-0 min-w-max">
      <li v-for="(step, i) in steps" :key="step.id" class="flex items-center">
        <button
          type="button"
          class="flex flex-col items-center w-24 sm:w-28 rounded-lg p-1 transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
          :class="selectedId === step.id ? 'bg-primary/10' : 'hover:bg-surface-container'"
          :disabled="step.status === 'Skipped'"
          @click="emit('select', step)"
        >
          <div
            class="w-9 h-9 rounded-full flex items-center justify-center border-2"
            :class="{
              'bg-primary border-primary text-on-primary': step.status === 'InProgress',
              'bg-secondary-container border-secondary text-on-secondary-container': step.status === 'Completed',
              'bg-surface-container border-outline-variant text-on-surface-variant': step.status === 'Pending',
              'bg-surface-container-high border-dashed border-outline opacity-60': step.status === 'Skipped',
              'ring-2 ring-primary ring-offset-2': selectedId === step.id,
            }"
          >
            <span v-if="step.status === 'Completed'" class="material-symbols-outlined text-lg">check</span>
            <span v-else class="material-symbols-outlined text-lg">{{ step.icon }}</span>
          </div>
          <p class="text-[11px] sm:text-xs text-center mt-2 font-medium leading-tight">{{ step.stepNameFa }}</p>
          <p class="text-[10px] text-on-surface-variant">{{ statusLabel(step.status) }}</p>
          <p v-if="step.effectiveCost > 0" class="text-[10px] text-secondary mt-0.5">{{ formatCurrency(step.effectiveCost) }}</p>
        </button>
        <div
          v-if="i < steps.length - 1"
          class="h-0.5 w-6 sm:w-10 mt-4 shrink-0"
          :class="step.status === 'Completed' ? 'bg-secondary' : 'bg-outline-variant'"
        />
      </li>
    </ol>
  </div>
</template>
