<script setup lang="ts">
import { VueDraggable } from 'vue-draggable-plus'
import type { ProcessStepType } from '@/types'

export interface BuilderStep {
  key: string
  processStepTypeId: string
  isOptional: boolean
}

const steps = defineModel<BuilderStep[]>({ required: true })
const props = defineProps<{ stepTypes: ProcessStepType[]; compact?: boolean }>()

function addStep(typeId: string) {
  steps.value = [...steps.value, { key: crypto.randomUUID(), processStepTypeId: typeId, isOptional: false }]
}

function removeAt(i: number) {
  const c = [...steps.value]
  c.splice(i, 1)
  steps.value = c
}

function nameOf(id: string) {
  return props.stepTypes.find((t) => t.id === id)?.nameFa ?? '—'
}

function iconOf(id: string) {
  return props.stepTypes.find((t) => t.id === id)?.icon ?? 'circle'
}
</script>

<template>
  <div class="space-y-3">
    <p v-if="!compact" class="text-xs text-on-surface-variant">
      بکشید و رها کنید. برای تکرار مرحله (مثلاً دارکشی دوباره) همان مرحله را دوباره اضافه کنید.
    </p>
    <VueDraggable v-model="steps" :animation="180" handle=".drag-handle" class="space-y-2" item-key="key">
      <div
        v-for="(item, index) in steps"
        :key="item.key"
        class="flex items-center gap-2 p-3 rounded-xl border border-outline-variant bg-surface-container-lowest"
      >
        <span class="drag-handle material-symbols-outlined cursor-grab text-on-surface-variant">drag_indicator</span>
        <span class="text-xs font-mono w-5">{{ index + 1 }}</span>
        <span class="material-symbols-outlined text-primary">{{ iconOf(item.processStepTypeId) }}</span>
        <span class="flex-1 text-sm font-medium">{{ nameOf(item.processStepTypeId) }}</span>
        <label class="text-xs flex gap-1"><input v-model="item.isOptional" type="checkbox" />اختیاری</label>
        <button type="button" class="text-error" @click="removeAt(index)"><span class="material-symbols-outlined">close</span></button>
      </div>
    </VueDraggable>
    <div class="flex flex-wrap gap-1 max-h-32 overflow-y-auto">
      <button
        v-for="st in stepTypes"
        :key="st.id"
        type="button"
        class="text-xs px-2 py-1 rounded-full border border-outline-variant"
        @click="addStep(st.id)"
      >
        + {{ st.nameFa }}
      </button>
    </div>
  </div>
</template>
