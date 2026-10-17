<script setup lang="ts">
import { computed } from 'vue'
import type { ImportRow } from '@/lib/types/import'
import { importActions } from '@/lib/messages/import'
import NumberField from '@/components/ui/NumberField.vue'
import { relatedRows, rowFieldValue } from './importReview'
const props = defineProps<{ row: ImportRow; overrides?: Record<string, string | null>; labels: Record<string, string>; skipped: boolean; busy: boolean; context: string; allRows: ImportRow[] }>()
const emit = defineEmits<{ edit: [field: string, value: string | null]; skip: [skip: boolean] }>()
const n = (value: number) => new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 2 }).format(value)
const fields = ['serial', 'bale', 'length', 'width', 'qty', 'usdPerM2', 'origin', 'material', 'pattern', 'bg_color', 'border_color']
const numeric = ['length', 'width', 'qty', 'bale', 'usdPerM2']
const primaryFields = computed(() => [...new Set(['serial', 'bale', ...props.row.issues.map(issue => issue.field).filter((field): field is string => !!field && fields.includes(field))])])
const extraFields = computed(() => fields.filter(field => !primaryFields.value.includes(field)))
const controlRow = computed(() => props.row.action === 'skip' && !Object.hasOwn(props.row.values, 'serial'))
const serial = computed(() => controlRow.value ? 'ردیف کنترل جمع فایل' : String(rowFieldValue(props.row, props.overrides, 'serial')).trim() || 'شماره خالی است')
const references = computed(() => relatedRows(props.row).map(number => ({ number, row: props.allRows.find(row => row.sheetRow === number) })))
const policySkipped = computed(() => !controlRow.value && props.row.action === 'skip' && !props.skipped && !props.row.issues.some(issue => issue.code === 'I_ROW_SKIPPED'))
function numberValue(field: string) { const value = rowFieldValue(props.row, props.overrides, field); return value === '' || !Number.isFinite(Number(value)) ? null : Number(value) }
</script>
<template>
  <section :id="`${context}-row-${row.sheetRow}`" :aria-label="`ردیف اکسل ${n(row.sheetRow)}`" class="min-w-0 space-y-3 rounded-xl border border-outline-variant bg-surface p-4">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div class="min-w-0 space-y-1">
        <h3 class="font-bold">ردیف اکسل {{ n(row.sheetRow) }} · <bdi dir="ltr" class="break-all">{{ serial }}</bdi></h3>
        <p class="text-sm text-on-surface-variant">{{ labels.bale }}: {{ rowFieldValue(row, overrides, 'bale') || 'مشخص نشده' }} · {{ skipped ? 'کنار گذاشتن انتخاب شده' : importActions[row.action] }}</p>
      </div>
      <button v-if="!policySkipped && !controlRow" type="button" :disabled="busy" :aria-label="`${skipped ? 'لغو کنار گذاشتن' : 'کنار گذاشتن'} ردیف اکسل ${n(row.sheetRow)}`" class="min-h-11 rounded-lg border border-outline px-4 disabled:opacity-50" @click="emit('skip', !skipped)">{{ skipped ? 'لغو کنار گذاشتن این ردیف' : 'این ردیف را کنار بگذار' }}</button>
    </div>
    <template v-if="!skipped">
      <p v-if="policySkipped" class="text-sm text-on-surface-variant">این ردیف طبق روش ورود کنار گذاشته شده است. برای ورود آن، «شمارهٔ موجود» را در بخش «ستون‌ها و روش ورود» تغییر دهید و دوباره بازبینی کنید.</p>
      <p v-if="controlRow" class="text-sm text-on-surface-variant">این ردیف برای کنترل جمع فایل است و به‌عنوان فرش وارد نمی‌شود.</p>
      <ul v-if="row.issues.length" class="space-y-2 text-sm">
        <li v-for="(issue, index) in row.issues" :key="index" :class="issue.severity === 'error' ? 'text-error' : 'text-on-surface-variant'">
          <strong>{{ issue.field ? labels[issue.field] ?? issue.field : 'ردیف' }}:</strong> {{ issue.message }}
          <span v-if="issue.relatedRows?.length" class="block">ردیف‌های مرتبط در اکسل: {{ issue.relatedRows.map(n).join('، ') }}</span>
        </li>
      </ul>
      <div v-if="references.length" class="space-y-1 rounded-lg bg-surface-container p-3 text-sm">
        <p v-for="reference in references" :key="reference.number">ردیف اکسل {{ n(reference.number) }} · شماره <bdi dir="ltr">{{ reference.row?.values.serial || 'خالی' }}</bdi> · {{ labels.bale }} {{ reference.row?.bale == null ? 'مشخص نشده' : n(reference.row.bale) }}</p>
      </div>
      <div v-if="!controlRow && !policySkipped" class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        <label v-for="field in primaryFields" :key="field" class="min-w-0 text-sm">{{ labels[field] }}
          <NumberField v-if="numeric.includes(field)" :model-value="numberValue(field)" :kind="field === 'qty' || field === 'bale' ? 'integer' : field === 'usdPerM2' || field === 'usd' ? 'usd' : 'dim'" :disabled="busy" @update:model-value="value => emit('edit', field, value === null ? null : String(value))" />
          <input v-else :value="rowFieldValue(row, overrides, field)" :disabled="busy" class="block min-h-11 w-full rounded-lg border border-outline bg-surface p-2" @input="emit('edit', field, ($event.target as HTMLInputElement).value)" />
        </label>
      </div>
      <details v-if="!controlRow && !policySkipped">
        <summary class="min-h-11 cursor-pointer py-3 text-primary">اصلاح اندازه، تعداد و سایر ستون‌های این ردیف</summary>
        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <label v-for="field in extraFields" :key="field" class="min-w-0 text-sm">{{ labels[field] }}
            <NumberField v-if="numeric.includes(field)" :model-value="numberValue(field)" :kind="field === 'qty' || field === 'bale' ? 'integer' : field === 'usdPerM2' || field === 'usd' ? 'usd' : 'dim'" :disabled="busy" @update:model-value="value => emit('edit', field, value === null ? null : String(value))" />
            <input v-else :value="rowFieldValue(row, overrides, field)" :disabled="busy" class="block min-h-11 w-full rounded-lg border border-outline bg-surface p-2" @input="emit('edit', field, ($event.target as HTMLInputElement).value)" />
          </label>
        </div>
      </details>
    </template>
    <p v-else class="text-sm text-on-surface-variant">{{ row.action === 'skip' ? 'این ردیف در آخرین بازبینی کنار گذاشته شد و وارد نمی‌شود؛ تا پیش از ثبت نهایی می‌توانید این تصمیم را لغو و دوباره بازبینی کنید.' : 'این تصمیم در بازبینی بعدی اعمال می‌شود؛ تا پیش از ثبت نهایی قابل برگشت است.' }}</p>
  </section>
</template>
