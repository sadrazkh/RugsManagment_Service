<script setup lang="ts">
import { ref } from 'vue'
import NumberField from '@/components/ui/NumberField.vue'
import type { DocumentColumn } from '@/lib/types/documents'
const columns = defineModel<DocumentColumn[]>({ required: true })
defineProps<{ selected: string }>()
const emit = defineEmits<{ select: [key: string] }>(), dragging = ref(-1)
function move(from: number, to: number) { if (to < 0 || to >= columns.value.length) return; const copy = [...columns.value]; const [item] = copy.splice(from, 1); copy.splice(to, 0, item); columns.value = copy }
</script>
<template><ol class="flex flex-wrap gap-2" aria-label="ستون‌های سند"><li v-for="(c, i) in columns" :key="c.key" draggable="true" class="w-56 space-y-2 rounded-lg border border-outline-variant bg-surface-container p-3" :class="selected === c.key ? 'ring-2 ring-primary' : ''" @dragstart="dragging = i" @dragover.prevent @drop.prevent="move(dragging, i)"><button class="min-h-11 w-full text-start font-bold" @click="emit('select', c.key)">{{ c.label }}</button><label class="block">عنوان<input v-model="c.label" class="min-h-11 w-full rounded border border-outline-variant bg-surface p-2" /></label><label class="block">پهنای ستون<NumberField :model-value="c.width" kind="number" :min="1" :max="100" @update:model-value="c.width = $event ?? undefined" /></label><label class="block">نمایش<select v-model="c.visibleWhen" class="min-h-11 w-full rounded border border-outline-variant bg-surface p-2"><option value="always">همیشه</option><option value="hasLots">وقتی دسته داریم</option><option value="never">پنهان</option></select></label><div class="flex gap-2"><button :disabled="i === 0" class="min-h-11 flex-1 rounded border border-outline-variant" @click="move(i, i - 1)">قبلی</button><button :disabled="i === columns.length - 1" class="min-h-11 flex-1 rounded border border-outline-variant" @click="move(i, i + 1)">بعدی</button></div></li></ol></template>
