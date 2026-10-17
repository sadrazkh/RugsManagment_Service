<script setup lang="ts">
import NumberField from '@/components/ui/NumberField.vue'
import type { DocumentLayout } from '@/lib/types/documents'
const page = defineModel<DocumentLayout['page']>({ required: true })
</script>
<template><details class="rounded-lg border border-outline-variant p-3"><summary class="min-h-11 cursor-pointer font-bold">تنظیم صفحه</summary><div class="grid gap-3 sm:grid-cols-2"><label>کاغذ<select v-model="page.paper" class="min-h-11 w-full rounded border border-outline-variant bg-surface p-2"><option>A4</option><option>A3</option><option>A5</option><option>Letter</option></select></label><label>جهت<select v-model="page.orientation" class="min-h-11 w-full rounded border border-outline-variant bg-surface p-2"><option value="portrait">عمودی</option><option value="landscape">افقی</option></select></label><label>مقیاس<NumberField :model-value="page.scale" kind="integer" :min="10" :max="400" @update:model-value="page.scale = $event ?? 95" /></label><label>اندازهٔ قلم<NumberField :model-value="page.fontSize" kind="number" :min="6" :max="36" @update:model-value="page.fontSize = $event ?? 11" /></label></div></details></template>
