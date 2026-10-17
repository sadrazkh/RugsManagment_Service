<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { api, errorMessage } from '@/lib/api'
import { importLabels, importStatuses } from '@/lib/messages/import'
import type { ImportJob, ImportPreview, ImportResult, ImportUndo } from '@/lib/types/import'
import NumberField from '@/components/ui/NumberField.vue'
import { appContext } from '@/lib/context'
import { confirmDialog } from '@/lib/ui'
import { createPreviewGate } from '@/components/import/previewGate'
import ImportRowReview from '@/components/import/ImportRowReview.vue'
import { problemRows, restoreReview, reviewRows, setRowSkipped, sortedSkippedRows, useSheetDecisions, type ReviewFilter } from '@/components/import/importReview'
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
const { overrides, skippedRows, sheetNotice } = useSheetDecisions(sheet)
const filter = ref<ReviewFilter>('all')
const reviewButton = ref<HTMLButtonElement | null>(null)
const page = ref(1)
const blockers = ref<string[]>([])
const terms = computed(() => appContext().terms)
const fieldLabels = computed<Record<string, string>>(() => ({ ...importLabels, bale: terms.value.bale }))
const n = (value: number) => new Intl.NumberFormat('fa-IR', { maximumFractionDigits: 2 }).format(value)
const problems = computed(() => problemRows(preview.value?.rows ?? [], skippedRows.value))
const selectedSkips = computed(() => preview.value?.rows.filter(row => skippedRows.value.includes(row.sheetRow)) ?? [])
const warningSummary = computed(() => preview.value?.issuesByCode.filter(issue => issue.severity === 'warning') ?? [])
const filteredRows = computed(() => reviewRows(preview.value?.rows ?? [], filter.value, skippedRows.value))
const rows = computed(() => filteredRows.value.slice((page.value - 1) * 50, page.value * 50))
const totalPages = computed(() => Math.max(1, Math.ceil(filteredRows.value.length / 50)))
watch(filter, () => { page.value = 1 })
watch(totalPages, total => { page.value = Math.min(page.value, total) })
watch(sheet, () => { preview.value = null; showRows.value = false; page.value = 1 }, { flush: 'sync' })
const previewInput = computed(() => ({ sheet: sheet.value, headerRow: headerRow.value,
  mapping: Object.fromEntries(Object.entries(mapping.value).filter(([, column]) => Number.isInteger(column) && column > 0)),
  options: { mode: mode.value, existingSerialPolicy: policy.value, convertCentimeters: centimeters.value }, overrides: overrides.value, skippedRows: sortedSkippedRows(skippedRows.value) }))
const gate = createPreviewGate(previewInput)
const previewCurrent = gate.current
const canCommit = computed(() => !busy.value && previewCurrent.value && !!preview.value && preview.value.counts.errors === 0 && preview.value.counts.lines > 0)
const commitExplanation = computed(() => !previewCurrent.value ? 'تصمیم‌ها هنوز بازبینی نشده‌اند؛ «بازبینی و اعمال تصمیم‌ها» را بزنید.'
  : preview.value?.counts.errors ? 'ردیف‌های مشکل‌دار زیر را اصلاح کنید یا با تصمیم خودتان کنار بگذارید، سپس بازبینی کنید.'
  : !preview.value?.counts.lines ? 'ردیفی برای ورود باقی نمانده است.' : 'فقط ردیف‌های پذیرفته‌شدهٔ این بازبینی ثبت می‌شوند؛ هشدارها مانع ثبت نیستند.')
async function run(action: () => Promise<void>) {
  if (busy.value) return
  busy.value = true; error.value = ''
  try { await action() } catch (e) { error.value = errorMessage(e) } finally { busy.value = false }
}
async function prepare(j: ImportJob) {
  overrides.value = {}; skippedRows.value = []; preview.value = null; result.value = null; page.value = 1; showRows.value = false
  const saved = j.lastPreview
  const restored = restoreReview(j)
  job.value = j; sheet.value = restored.sheet; mapping.value = restored.mapping; mode.value = restored.options.mode; headerRow.value = restored.headerRow
  const url = new URL(window.location.href); url.searchParams.set('id', j.id)
  window.history.replaceState(window.history.state, '', url)
  policy.value = restored.options.existingSerialPolicy; centimeters.value = restored.options.convertCentimeters
  overrides.value = restored.overrides ?? {}; skippedRows.value = restored.skippedRows ?? []
  sheetNotice.value = ''
  if (j.status > 1) return
  if (saved || j.sheets.length === 1 && Object.keys(mapping.value).length >= 10) await updatePreview()
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
  const response = await api.post<ImportPreview>(`/api/imports/${job.value.id}/preview`, previewInput.value)
  if (gate.capture() !== sent) { error.value = 'برگه یا ورودی‌ها هنگام بازبینی تغییر کردند؛ نتیجهٔ قبلی اعمال نشد. دوباره بازبینی کنید.'; return }
  preview.value = response
  gate.accept(sent)
  job.value.status = 1; job.value.version = preview.value.version
}
async function commit() {
  if (!job.value || !preview.value || !canCommit.value) return
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
function edit(row: number, field: string, value: string | null) {
  overrides.value[row] ??= {}; overrides.value[row][field] = value
}
async function decideRow(row: number, skip: boolean) {
  skippedRows.value = setRowSkipped(skippedRows.value, row, skip)
  await nextTick()
  reviewButton.value?.focus({ preventScroll: true })
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
        <p v-if="sheetNotice" role="status" class="rounded-xl bg-secondary-container p-3 text-on-secondary-container">{{ sheetNotice }}</p>
        <button type="button" class="min-h-11 rounded-xl border border-outline px-4" :aria-expanded="showOptions" @click="showOptions = !showOptions">ستون‌ها و روش ورود</button>
        <div v-if="showOptions" class="grid gap-4 rounded-2xl border border-outline-variant p-4 sm:grid-cols-2">
          <label>برگه<select v-model="sheet" class="block min-h-11 w-full rounded-lg border border-outline bg-surface p-2"><option v-for="s in job.sheets" :key="s">{{ s }}</option></select></label>
          <label>ردیف سرستون<NumberField v-model="headerRow" kind="integer" :min="1" :max="10000" /></label>
          <label>روش ورود<select v-model="mode" class="block min-h-11 w-full rounded-lg border border-outline bg-surface p-2"><option :value="0">اظهار و {{ terms.bales }}</option><option :value="1">فقط ورود به انبار</option></select></label>
          <label>شمارهٔ موجود<select v-model="policy" class="block min-h-11 w-full rounded-lg border border-outline bg-surface p-2"><option :value="0">وصل کن و اندازهٔ نهایی را ثبت کن</option><option :value="1">کنار بگذار</option><option :value="2">خطا نشان بده</option></select></label>
          <label class="flex min-h-11 items-center gap-3"><input v-model="centimeters" type="checkbox" />اندازه‌های بزرگ‌تر از ۱۲، سانتی‌متر هستند</label>
          <label v-for="(label, key) in fieldLabels" :key="key">{{ label }}<NumberField :model-value="mapping[key]" kind="integer" :min="1" :max="512" placeholder="شمارهٔ ستون" @update:model-value="value => { if (value == null) delete mapping[key]; else mapping[key] = value }" /></label>
        </div>
        <button v-if="!preview" type="button" :disabled="busy" class="min-h-11 rounded-xl bg-primary px-5 text-on-primary disabled:opacity-50" @click="run(updatePreview)">بازبینی فایل</button>
        <div v-if="preview" class="space-y-4 rounded-2xl border border-outline-variant bg-surface-container p-5">
          <h2 class="text-xl font-bold">نتیجهٔ آخرین بازبینی: {{ n(preview.counts.pieces) }} تخته در {{ n(preview.counts.bales) }} {{ terms.bale }}</h2>
          <p class="text-lg">{{ n(preview.counts.areaM2) }} متر · {{ n(preview.counts.usdTotal) }} دلار</p>
          <p>{{ n(preview.counts.lines) }} ردیف · {{ n(preview.counts.attach) }} اتصال · {{ n(preview.counts.skip) }} کنار گذاشته · {{ n(preview.counts.warnings) }} هشدار</p>
          <div class="sticky top-20 z-10 space-y-2 rounded-xl border border-outline-variant bg-surface p-3 shadow-sm">
            <div class="flex flex-wrap gap-3">
              <button ref="reviewButton" type="button" :disabled="busy" class="min-h-11 rounded-xl bg-primary px-5 text-on-primary disabled:opacity-50" @click="run(updatePreview)">بازبینی و اعمال تصمیم‌ها</button>
              <button type="button" :disabled="!canCommit" class="min-h-11 rounded-xl border border-primary px-5 text-primary disabled:opacity-50" @click="commit">ثبت نهایی {{ n(preview.counts.lines) }} ردیف پذیرفته‌شده</button>
            </div>
            <p role="status" class="text-sm text-on-surface-variant">{{ commitExplanation }}</p>
          </div>
          <section v-if="problems.length" aria-label="ردیف‌های نیازمند تصمیم" class="space-y-3">
            <h2 class="text-lg font-bold text-error">{{ n(problems.length) }} ردیف نیاز به تصمیم شما دارد</h2>
            <p class="text-sm">همهٔ ردیف‌های مشکل‌دار اینجا هستند. شمارهٔ ردیف همان شمارهٔ ردیف اکسل است؛ اصلاح کنید یا هر ردیف را جداگانه کنار بگذارید، سپس بازبینی را بزنید.</p>
            <ImportRowReview v-for="row in problems" :key="row.sheetRow" :row="row" :overrides="overrides[row.sheetRow]" :labels="fieldLabels" :all-rows="preview.rows" :skipped="false" :busy="busy" context="problem" @edit="(field, value) => edit(row.sheetRow, field, value)" @skip="skip => decideRow(row.sheetRow, skip)" />
          </section>
          <section v-if="selectedSkips.length" aria-label="ردیف‌های کنار گذاشته‌شده با تصمیم شما" class="space-y-3">
            <h2 class="font-bold">{{ n(selectedSkips.length) }} ردیف را برای کنار گذاشتن انتخاب کرده‌اید</h2>
            <ImportRowReview v-for="row in selectedSkips" :key="row.sheetRow" :row="row" :overrides="overrides[row.sheetRow]" :labels="fieldLabels" :all-rows="preview.rows" :skipped="true" :busy="busy" context="decision" @edit="(field, value) => edit(row.sheetRow, field, value)" @skip="skip => decideRow(row.sheetRow, skip)" />
          </section>
          <details v-if="warningSummary.length" class="rounded-xl border border-outline-variant bg-surface p-3">
            <summary class="min-h-11 cursor-pointer py-3">{{ n(preview.counts.warnings) }} هشدار برای بررسی؛ مانع ثبت نیست</summary>
            <ul class="space-y-2 text-sm text-on-surface-variant"><li v-for="issue in warningSummary" :key="issue.code">{{ issue.message }} ({{ n(issue.count) }})</li></ul>
          </details>
          <button type="button" class="min-h-11 rounded-xl border border-outline px-4" :aria-expanded="showRows" @click="showRows = !showRows">{{ showRows ? 'بستن فهرست ردیف‌ها' : `دیدن همهٔ ${n(preview.rows.length)} ردیف و اصلاح دلخواه` }}</button>
          <div v-if="showRows" class="space-y-4">
            <label class="block">نمایش<select v-model="filter" class="block min-h-11 w-full rounded-lg border border-outline bg-surface p-2 sm:w-auto"><option value="all">همهٔ ردیف‌ها</option><option value="issues">خطاها و هشدارها</option><option value="skipped">کنار گذاشته‌شده‌ها</option></select></label>
            <p class="text-sm text-on-surface-variant">{{ n(filteredRows.length) }} ردیف در این نمایش</p>
            <ImportRowReview v-for="row in rows" :key="row.sheetRow" :row="row" :overrides="overrides[row.sheetRow]" :labels="fieldLabels" :all-rows="preview.rows" :skipped="skippedRows.includes(row.sheetRow)" :busy="busy" context="all" @edit="(field, value) => edit(row.sheetRow, field, value)" @skip="skip => decideRow(row.sheetRow, skip)" />
            <div class="flex gap-3"><button type="button" :disabled="page <= 1" class="min-h-11 px-4" @click="page--">قبلی</button><span class="self-center">{{ n(page) }} از {{ n(totalPages) }}</span><button type="button" :disabled="page >= totalPages" class="min-h-11 px-4" @click="page++">بعدی</button></div>
          </div>
          <details><summary class="min-h-11 cursor-pointer py-3">کنترل جمع {{ terms.bales }} و مقایسه با فایل</summary>
            <p class="mb-3 text-sm text-on-surface-variant">جمع محاسبه‌شده فقط از ردیف‌های پذیرفته‌شده است. ردیف‌های کنار گذاشته‌شده در آن نیستند؛ پس اختلاف با جمع قبلی فایل طبیعی است و به‌تنهایی مانع ثبت نیست.</p>
            <section v-for="r in preview.reconciliation" :key="r.bale ?? 0" class="mb-3 overflow-x-auto rounded-lg border border-outline-variant bg-surface p-3" tabindex="0" :aria-label="`کنترل جمع ${terms.bale} ${n(r.bale ?? 0)}`">
              <h3 class="mb-2 font-bold">{{ r.bale == null ? 'جمع کل فایل' : `${terms.baleOf} ${n(r.bale)}` }} · {{ r.ok ? 'جمع‌ها درست است' : 'اختلاف جمع؛ بررسی کنید' }}</h3>
              <table class="w-full min-w-max text-sm"><caption class="sr-only">مقایسهٔ جمع ردیف‌های پذیرفته‌شده با جمع فایل</caption><thead><tr><th class="p-2 text-start">مبنا</th><th class="p-2">تعداد</th><th class="p-2">متر</th><th class="p-2">دلار</th></tr></thead><tbody>
                <tr><th class="p-2 text-start">محاسبهٔ ردیف‌های پذیرفته‌شده</th><td class="p-2">{{ n(r.pieces) }}</td><td class="p-2">{{ n(r.area) }}</td><td class="p-2">{{ n(r.usd) }}</td></tr>
                <tr><th class="p-2 text-start">جمع زیر {{ terms.bale }} در فایل</th><td class="p-2">{{ r.footerPieces == null ? '—' : n(r.footerPieces) }}</td><td class="p-2">{{ r.footerArea == null ? '—' : n(r.footerArea) }}</td><td class="p-2">{{ r.footerUsd == null ? '—' : n(r.footerUsd) }}</td></tr>
                <tr><th class="p-2 text-start">خلاصهٔ فایل</th><td class="p-2">{{ r.summaryPieces == null ? '—' : n(r.summaryPieces) }}</td><td class="p-2">{{ r.summaryArea == null ? '—' : n(r.summaryArea) }}</td><td class="p-2">{{ r.summaryUsd == null ? '—' : n(r.summaryUsd) }}</td></tr>
              </tbody></table>
            </section>
          </details>
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
