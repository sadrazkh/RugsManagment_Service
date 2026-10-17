<script setup lang="ts">
import { ref, watch, onBeforeUnmount } from 'vue'
import { api, ApiError, errorMessage } from '@/lib/api'
import type { DocumentLayout } from '@/lib/types/documents'
const expr = defineModel<string>({ required: true })
const props = defineProps<{ layout: DocumentLayout; columnKey: string; shipmentId: string }>()
const result = ref<{ type: string; formula: string | null; samples: string[] } | null>(null), error = ref('')
let timer: ReturnType<typeof setTimeout> | undefined, sequence = 0
watch(() => [expr.value, props.shipmentId, props.columnKey], () => {
  clearTimeout(timer); const n = ++sequence; error.value = ''; result.value = null
  if (!expr.value) return
  timer = setTimeout(async () => { try {
    const value = await api.post<{ type: string; formula: string | null; samples: string[] }>('/api/document-templates/validate-expression', { expression: expr.value, layout: props.layout, columnKey: props.columnKey, shipmentId: props.shipmentId || null })
    if (n === sequence) result.value = value
  } catch (e) { if (n === sequence) { const errors = e instanceof ApiError ? e.body?.errors : null; error.value = Array.isArray(errors) ? errors.map((x: { path: string; message: string }) => `${x.path}: ${x.message}`).join(' — ') : errorMessage(e) } } }, 400)
}, { immediate: true })
onBeforeUnmount(() => clearTimeout(timer))
</script>
<template><div class="space-y-2"><label class="block">عبارت محاسبه<textarea v-model="expr" dir="ltr" class="min-h-24 w-full rounded-lg border border-outline-variant bg-surface p-3 font-mono" placeholder="round(col.area * col.rate, 2)" /></label><p v-if="error" role="alert" class="text-error">{{ error }}</p><div v-if="result" aria-live="polite"><p>نوع: {{ result.type }}</p><p v-if="result.formula" dir="ltr" class="overflow-auto font-mono">={{ result.formula }}</p><p>نمونه‌ها: {{ result.samples.join('، ') }}</p></div></div></template>
