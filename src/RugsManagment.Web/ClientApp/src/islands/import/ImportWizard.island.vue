<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api, errorMessage } from '@/lib/api'
import { importActions, importLabels, importStatuses } from '@/lib/messages/import'
import type { ImportJob, ImportPreview, ImportResult, ImportUndo } from '@/lib/types/import'
import NumberField from '@/components/ui/NumberField.vue'
import { appContext } from '@/lib/context'
import { confirmDialog } from '@/lib/ui'
import { createPreviewGate } from '@/components/import/previewGate'
const props = defineProps<{ jobId?: string | null }>()
const job = ref<ImportJob | null>(null)
const preview = ref<ImportPreview | null>(null)
const result = ref<ImportResult | null>(null)
const busy = ref(false)
const error = ref('')
const sheet = ref('')
const mapping = ref<Record<string, number>>({})
const mode = ref(0)
const headerRow = ref<number | null>(null)
const policy = ref(0)
const centimeters = ref(false)
const showRows = ref(false)
const showOptions = ref(false)
const overrides = ref<Record<number, Record<string, string | null>>>({})
const page = ref(1)
const blockers = ref<string[]>([])
const terms = computed(() => appContext().terms)
const fieldLabels = computed<Record<string, string>>(() => ({ ...importLabels, bale: terms.value.bale }))
const n = (value: number) => new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 2 }).format(value)
const rows = computed(() => preview.value?.rows.filter(r => r.values.serial).slice((page.value - 1) * 50, page.value * 50) ?? [])
const totalPages = computed(() => Math.max(1, Math.ceil((preview.value?.rows.filter(r => r.values.serial).length ?? 0) / 50)))
const previewInput = computed(() => ({ sheet: sheet.value, headerRow: headerRow.value,
  mapping: Object.fromEntries(Object.entries(mapping.value).filter(([, column]) => Number.isInteger(column) && column > 0)),
  options: { mode: mode.value, existingSerialPolicy: policy.value, convertCentimeters: centimeters.value }, overrides: overrides.value }))
const gate = createPreviewGate(previewInput)
const previewCurrent = gate.current
async function run(action: () => Promise<void>) {
  if (busy.value) return
  busy.value = true; error.value = ''
  try { await action() } catch (e) { error.value = errorMessage(e) } finally { busy.value = false }
}
async function prepare(j: ImportJob) {
  job.value = j; sheet.value = j.sheets[0] ?? ''; mapping.value = { ...j.mapping }; mode.value = j.mode; headerRow.value = j.headerRow
  if (j.status > 1) return
  if (j.sheets.length === 1 && Object.keys(mapping.value).length >= 10) await updatePreview()
  else showOptions.value = true
}
async function upload(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  if (file.size > 10_485_760) { error.value = 'حجم فایل باید کمتر از ۱۰ مگابایت باشد.'; return }
  await run(async () => { const form = new FormData(); form.append('file', file); await prepare(await api.postForm<ImportJob>('/api/imports', form)) })
}
async function updatePreview() {
  if (!job.value) return
  const sent = gate.capture()
  preview.value = await api.post<ImportPreview>(`/api/imports/${job.value.id}/preview`, previewInput.value)
  gate.accept(sent)
  job.value.status = 1; job.value.version = preview.value.version
}
async function commit() {
  if (!job.value || !preview.value || !previewCurrent.value) return
  await run(async () => {
    result.value = await api.post<ImportResult>(`/api/imports/${job.value!.id}/commit`, { saveMapping: true, version: preview.value!.version })
    job.value!.status = 2
  })
}
async function undo() {
  if (!job.value || !await confirmDialog({ title: 'برگرداندن ورود', message: 'این ورود برگردانده شود؟', confirmLabel: 'برگردان' })) return
  await run(async () => {
    const response = await api.post<ImportUndo>(`/api/imports/${job.value!.id}/undo`, {})
    blockers.value = response.blockers
    if (response.ok) { job.value!.status = 3; result.value = null; preview.value = null }
  })
}
function edit(row: number, field: string, event: Event) {
  overrides.value[row] ??= {}; overrides.value[row][field] = (event.target as HTMLInputElement).value
}
function editNumber(row: number, field: string, value: number | null) {
  overrides.value[row] ??= {}; overrides.value[row][field] = value === null ? null : String(value)
}
onMounted(() => { if (props.jobId) void run(async () => { await prepare(await api.get<ImportJob>(`/api/imports/${props.jobId}`)) }) })
</script>
<template>
  <section class="space-y-5" :aria-busy="busy">
    <div v-if="error" role="alert" class="rounded-xl bg-error-container p-4 text-on-error-container">{{ error }}</div>
    <p v-if="busy" role="status" class="text-on-surface-variant">در حال انجام…</p>
    <label v-if="!job" class="block rounded-2xl border border-outline-variant bg-surface-container p-6">
      <span class="mb-3 block font-bold">فایل اکسل را انتخاب کنید</span>
      <input type="file" accept=".xlsx" :disabled="busy" class="min-h-11 max-w-full" @change="upload" />
      <span class="mt-2 block text-sm text-on-surface-variant">فقط xlsx، حداکثر ۱۰ مگابایت</span>
    </label>
    <template v-if="job">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div><h2 class="font-bold break-all">{{ job.fileName }}</h2><p>{{ importStatuses[job.status] }}</p></div>
        <a href="/Import" class="inline-flex min-h-11 items-center text-primary">فایل دیگری وارد کنید</a>
      </div>
      <p v-if="job.duplicateOf" role="status" class="rounded-xl bg-secondary-container p-4 text-on-secondary-container">این فایل قبلاً بارگذاری شده است؛ ورود شمارهٔ {{ n(job.duplicateOf.number) }} را بررسی کنید.</p>
      <template v-if="job.status <= 1">
        <button type="button" class="min-h-11 rounded-xl border border-outline px-4" :aria-expanded="showOptions" @click="showOptions = !showOptions">ستون‌ها و روش ورود</button>
        <div v-if="showOptions" class="grid gap-4 rounded-2xl border border-outline-variant p-4 sm:grid-cols-2">
          <label>برگه<select v-model="sheet" class="block min-h-11 w-full rounded-lg border border-outline bg-surface p-2"><option v-for="s in job.sheets" :key="s">{{ s }}</option></select></label>
          <label>ردیف سرستون<NumberField v-model="headerRow" kind="integer" :min="1" :max="10000" /></label>
          <label>روش ورود<select v-model="mode" class="block min-h-11 w-full rounded-lg border border-outline bg-surface p-2"><option :value="0">اظهار و {{ terms.bales }}</option><option :value="1">فقط ورود به انبار</option></select></label>
          <label>شمارهٔ موجود<select v-model="policy" class="block min-h-11 w-full rounded-lg border border-outline bg-surface p-2"><option :value="0">وصل کن و اندازهٔ نهایی را ثبت کن</option><option :value="1">کنار بگذار</option><option :value="2">خطا نشان بده</option></select></label>
          <label class="flex min-h-11 items-center gap-3"><input v-model="centimeters" type="checkbox" />اندازه‌های بزرگ‌تر از ۱۲، سانتی‌متر هستند</label>
          <label v-for="(label, key) in fieldLabels" :key="key">{{ label }}<NumberField :model-value="mapping[key]" kind="integer" :min="1" :max="512" placeholder="شمارهٔ ستون" @update:model-value="value => { if (value == null) delete mapping[key]; else mapping[key] = value }" /></label>
          <button type="button" :disabled="busy" class="min-h-11 rounded-xl bg-primary px-5 text-on-primary" @click="run(updatePreview)">بازبینی فایل</button>
        </div>
        <div v-if="preview" class="space-y-4 rounded-2xl border border-outline-variant bg-surface-container p-5">
          <h2 class="text-xl font-bold">{{ n(preview.counts.pieces) }} تخته در {{ n(preview.counts.bales) }} {{ terms.bale }}</h2>
          <p class="text-lg">{{ n(preview.counts.areaM2) }} متر · {{ n(preview.counts.usdTotal) }} دلار</p>
          <p>{{ n(preview.counts.lines) }} ردیف · {{ n(preview.counts.attach) }} اتصال · {{ n(preview.counts.skip) }} کنار گذاشته · {{ n(preview.counts.warnings) }} هشدار</p>
          <p v-if="preview.counts.errors" role="alert" class="text-error">{{ n(preview.counts.errors) }} ردیف نیاز به اصلاح دارد.</p>
          <div class="flex flex-wrap gap-3">
            <button type="button" :disabled="busy || !previewCurrent || preview.counts.errors > 0 || preview.counts.lines === 0" class="min-h-11 rounded-xl bg-primary px-6 text-on-primary disabled:opacity-50" @click="commit">وارد کن</button>
            <button type="button" class="min-h-11 rounded-xl border border-outline px-4" :aria-expanded="showRows" @click="showRows = !showRows">دیدن ردیف‌ها و هشدارها</button>
          </div>
          <p v-if="!previewCurrent" role="status" class="text-on-surface-variant">ردیف‌ها یا روش ورود تغییر کرده‌اند؛ پیش از ثبت، اصلاح‌ها را دوباره بازبینی کنید.</p>
          <div v-if="showRows" class="space-y-4">
            <ul class="space-y-2"><li v-for="issue in preview.issuesByCode" :key="issue.code">{{ issue.message }} ({{ n(issue.count) }})</li></ul>
            <div v-for="row in rows" :key="row.sheetRow" class="rounded-xl border border-outline-variant bg-surface p-4">
              <p class="mb-2 font-bold">ردیف {{ n(row.sheetRow) }} · {{ importActions[row.action] }}</p>
              <div class="grid grid-cols-2 gap-3 sm:grid-cols-4">
                <label v-for="field in ['serial', 'bale', 'length', 'width', 'qty', 'usdPerM2', 'origin', 'material', 'pattern', 'bg_color', 'border_color']" :key="field" class="text-sm">{{ fieldLabels[field] }}<NumberField v-if="['length', 'width', 'qty', 'bale', 'usdPerM2'].includes(field)" :model-value="Number(overrides[row.sheetRow]?.[field] ?? (field === 'bale' ? row.bale : row.values[field])) || null" :kind="field === 'qty' || field === 'bale' ? 'integer' : field === 'usdPerM2' ? 'usd' : 'dim'" @update:model-value="value => editNumber(row.sheetRow, field, value)" /><input v-else :value="overrides[row.sheetRow]?.[field] ?? row.values[field] ?? ''" class="block min-h-11 w-full rounded-lg border border-outline bg-surface p-2" @input="edit(row.sheetRow, field, $event)" /></label>
              </div>
              <ul class="mt-3 text-sm"><li v-for="(issue, index) in row.issues" :key="index" :class="issue.severity === 'error' ? 'text-error' : 'text-on-surface-variant'">{{ issue.message }}</li></ul>
            </div>
            <div class="flex gap-3"><button type="button" :disabled="page <= 1" class="min-h-11 px-4" @click="page--">قبلی</button><span class="self-center">{{ n(page) }} از {{ n(totalPages) }}</span><button type="button" :disabled="page >= totalPages" class="min-h-11 px-4" @click="page++">بعدی</button></div>
            <button type="button" :disabled="busy" class="min-h-11 rounded-xl bg-primary px-5 text-on-primary" @click="run(updatePreview)">اصلاح‌ها را بازبینی کن</button>
          </div>
          <details><summary class="min-h-11 cursor-pointer py-3">کنترل جمع {{ terms.bales }}</summary><ul class="space-y-2"><li v-for="r in preview.reconciliation" :key="r.bale ?? 0">{{ terms.baleOf }} {{ n(r.bale ?? 0) }}: {{ n(r.pieces) }} تخته · {{ n(r.area) }} متر · {{ n(r.usd) }} دلار · {{ r.ok ? 'جمع‌ها درست است' : 'جمع‌ها را بررسی کنید' }}</li></ul></details>
        </div>
      </template>
      <div v-if="result" role="status" class="space-y-3 rounded-2xl bg-secondary-container p-5 text-on-secondary-container">
        <p>ثبت شد: {{ n(result.createdRugs) }} فرش تازه و {{ n(result.attachedRugs) }} اتصال.</p>
        <a v-if="result.shipmentId" :href="`/Shipments/${result.shipmentId}`" class="inline-flex min-h-11 items-center underline">محموله را باز کن</a>
      </div>
      <button v-if="job.status === 2" type="button" :disabled="busy" class="min-h-11 rounded-xl border border-outline px-5" @click="undo">برگردان این ورود</button>
      <ul v-if="blockers.length" role="alert" class="rounded-xl bg-error-container p-4 text-on-error-container"><li v-for="blocker in blockers" :key="blocker">{{ blocker }}</li></ul>
    </template>
  </section>
</template>
