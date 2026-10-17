<script setup lang="ts">
import { computed } from 'vue'
const props = defineProps<{ fields: { key: string; label: string; scope: string }[] }>()
const emit = defineEmits<{ select: [key: string] }>()
const groups = computed(() => [...new Set(props.fields.map(f => f.scope))])
</script>
<template><label class="block">فیلد ستون<select class="mt-2 min-h-11 w-full rounded border border-outline-variant bg-surface p-2" @change="emit('select', ($event.target as HTMLSelectElement).value)"><option value="">انتخاب فیلد…</option><optgroup v-for="g in groups" :key="g" :label="g"><option v-for="f in fields.filter(f => f.scope === g)" :key="f.key" :value="f.key">{{ f.label }}</option></optgroup></select></label></template>
