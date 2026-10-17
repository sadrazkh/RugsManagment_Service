<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import DocumentPreview from '@/components/documents/DocumentPreview.vue'
import { api, errorMessage } from '@/lib/api'
import { toPersianDigits } from '@/lib/format'
import { documentMessages as m } from '@/lib/messages/documents'
import type { DocumentTemplate, RenderDocument, ExportRun } from '@/lib/types/documents'
const props = defineProps<{ shipmentId: string; locked: boolean }>()
const templates = ref<DocumentTemplate[]>([]), selected = ref(''), preview = ref<RenderDocument | null>(null), history = ref<ExportRun[]>([]), error = ref(''), busy = ref(false)
const readiness = ref<{ code: string; message: string; href?: string }[]>([])
let sequence = 0
async function load() {
  const current = ++sequence
  busy.value = true; error.value = ''
  try {
    const result = await api.get<RenderDocument>(`/api/shipments/${props.shipmentId}/documents/preview?template=${selected.value}`)
    if (current === sequence) {
      preview.value = result; history.value = await api.get<ExportRun[]>(`/api/shipments/${props.shipmentId}/exports`)
      readiness.value = await api.get(`/api/shipments/${props.shipmentId}/readiness?templateId=${selected.value}`)
    }
  } catch (e) { if (current === sequence) error.value = errorMessage(e) } finally { if (current === sequence) busy.value = false }
}
onMounted(async () => {
  try {
    templates.value = (await api.get<DocumentTemplate[]>('/api/document-templates')).filter(t => t.kind !== 2)
    selected.value = templates.value.find(t => t.isDefault && t.kind === 0)?.id ?? templates.value[0]?.id ?? ''
  } catch (e) { error.value = errorMessage(e) }
})
watch(selected, () => { if (selected.value) void load() })
</script>
<template>
  <section class="space-y-4" :aria-busy="busy">
    <p v-if="!locked" class="rounded-lg bg-surface-container p-3 font-bold">{{ m.draft }} — تا قطعی شدن، عددها با تغییر فرش‌ها به‌روز می‌شوند.</p>
    <label class="block">قالب خروجی<select v-model="selected" class="mt-2 block min-h-11 w-full rounded-lg border border-outline-variant bg-surface p-2"><option v-for="t in templates" :key="t.id" :value="t.id">{{ t.name }}</option></select></label>
    <p v-if="!templates.length">{{ m.empty }}</p><p v-if="error" role="alert" class="text-error">{{ error }}</p>
    <ul v-if="readiness.length" class="space-y-2"><li v-for="issue in readiness" :key="issue.code + issue.message"><a class="inline-flex min-h-11 items-center text-primary" :href="issue.href ?? `/Shipments/${shipmentId}?tab=bales`">{{ issue.message }}</a></li></ul>
    <div v-if="selected" class="flex flex-wrap gap-3"><a class="inline-flex min-h-11 items-center rounded-lg bg-primary px-5 text-on-primary" :href="`/shipments/${shipmentId}/export.xlsx?template=${selected}`">{{ m.download }}</a><a class="inline-flex min-h-11 items-center rounded-lg border border-outline-variant px-5" :href="`/shipments/${shipmentId}/print?template=${selected}`" target="_blank" rel="noopener">{{ m.print }}</a></div>
    <DocumentPreview :document="preview" />
    <details><summary class="min-h-11 cursor-pointer py-3">{{ m.history }}</summary><ul><li v-for="run in history" :key="run.id" class="border-b border-outline-variant py-3">{{ run.template }} — نسخهٔ {{ toPersianDigits(String(run.revision)) }} — {{ new Date(run.at).toLocaleString('fa-IR') }} — {{ run.user }} <span v-if="run.isDraft">(پیش‌نویس)</span></li></ul></details>
  </section>
</template>
