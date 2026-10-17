<script setup lang="ts">
import type { DocumentColumn, FooterCell } from '@/lib/types/documents'
import NumberField from '@/components/ui/NumberField.vue'
const cells = defineModel<FooterCell[]>({ required: true })
defineProps<{ columns: DocumentColumn[] }>()
function mode(cell: FooterCell) { return cell.agg ?? (cell.expr !== undefined ? 'expression' : 'text') }
function setMode(cell: FooterCell, value: string) {
  delete cell.text; delete cell.expr; delete cell.agg; delete cell.of
  if (value === 'text') cell.text = 'جمع'
  else if (value === 'expression') cell.expr = '0'
  else { cell.agg = value; if (value === 'sum') cell.of = cell.col }
}
</script>
<template><details class="rounded-lg border border-outline-variant p-3"><summary class="min-h-11 cursor-pointer font-bold">جمع پایین هر بسته</summary><div v-for="(cell, i) in cells" :key="cell.id" class="my-3 grid gap-2 rounded bg-surface-container p-3 sm:grid-cols-3"><label>شناسه<input v-model="cell.id" class="min-h-11 w-full rounded border border-outline-variant bg-surface p-2" /></label><label>ستون مقصد<select v-model="cell.col" class="min-h-11 w-full rounded border border-outline-variant bg-surface p-2"><option v-for="c in columns" :key="c.key" :value="c.key">{{ c.label }}</option></select></label><label>تعداد ستون‌های ادغام<NumberField :model-value="cell.span ?? 1" kind="integer" :min="1" :max="30" @update:model-value="cell.span = $event ?? 1" /></label><label>محتوا<select :value="mode(cell)" class="min-h-11 w-full rounded border border-outline-variant bg-surface p-2" @change="setMode(cell,($event.target as HTMLSelectElement).value)"><option value="text">متن</option><option value="expression">عبارت</option><option value="count">تعداد تخته</option><option value="sum">جمع ستون</option></select></label><label v-if="mode(cell) === 'text'">متن<input v-model="cell.text" class="min-h-11 w-full rounded border border-outline-variant bg-surface p-2" /></label><label v-if="mode(cell) === 'expression'">عبارت<input v-model="cell.expr" dir="ltr" class="min-h-11 w-full rounded border border-outline-variant bg-surface p-2" /></label><label v-if="cell.agg === 'sum'">ستون جمع<select v-model="cell.of" class="min-h-11 w-full rounded border border-outline-variant bg-surface p-2"><option v-for="c in columns" :key="c.key" :value="c.key">{{ c.label }}</option></select></label><button class="min-h-11 text-error" @click="cells.splice(i, 1)">حذف خانه</button></div><button class="min-h-11 px-3 text-primary" @click="cells.push({ id: `footer${Date.now()}`, col: columns[0]?.key ?? '', text: 'جمع' })">افزودن خانهٔ جمع</button><p class="text-sm text-on-surface-variant">ستون مقصد با نام ستون نگه داشته می‌شود؛ جابه‌جایی ستون‌ها فرمول را خراب نمی‌کند.</p></details></template>
