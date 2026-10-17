<script setup lang="ts">
/**
 * سربرگ کارگاه محموله (`/Shipments/{id}`، Floor؛ props I.5 `{ shipmentId, canBusiness }`) — H.1:
 * شماره، عنوان، تاریخ، نشان وضعیت («پیش‌نویس» / «قطعی»)، جمع‌ها (نگله، تخته، متر، دلار) و تراشه‌های آمادگی با دکمه‌های
 * درست کردنشان (H.3b)، «بیشتر» برای فیلدهای اختیاری (C.1)، و دکمهٔ اصلی بازرگان «قطعی کن» / «باز کردن دوباره».
 *
 * <b>قطعی کن</b> (Business): تا کمبود بازدارنده هست غیرفعال است و زیرش «اول این‌ها را درست کنید: …» با پیام تراشه‌ها؛ با
 * پرسش «محمولهٔ ۸ قطعی شود؟ ۲۴۲ تخته «صادر شده» می‌شوند…» و نسخهٔ محموله فرستاده می‌شود. پس از «قطعی» یا «باز کردن
 * دوباره» صفحه تازه می‌شود، چون زبانه‌ها (نگله‌ها فقط‌خواندنی، خروجی بی «پیش‌نویس») props «قفل» را از سرور می‌گیرند.
 * نسخهٔ کهنه (۴۰۹) پیام فارسی سرور را نشان می‌دهد و محموله را دوباره می‌خواند.
 *
 * <b>تراشه‌ها</b> دکمه‌اند با `aria-describedby` به پیام کامل (H.7): «[افزودن فرش]»، «[حذف کن]» (نگلهٔ خالی)، «[نشانم بده]»،
 * «[بقیه همان اندازهٔ اول]» (یک پرسش، بعد عمل‌های `measure` با `sameAsIntake` و نسخهٔ هر فرش، C.4) و «[نرخ بگذارید]»
 * (فقط Business: فرم «ارزش گمرکی «کاشان» برای هر متر: [___] دلار [ثبت]» → `POST /api/declared-values`، C.5).
 *
 * <b>هم‌گامی با صفحهٔ نگله‌بندی:</b> دو جزیرهٔ جدا روی یک صفحه‌اند؛ هر کدام پس از نوشتن رویداد `rugs:shipment-changed` می‌فرستد
 * و دیگری دوباره می‌خواند. وقتی تخته همین‌جاست دکمه‌های تراشه فرمان `rugs:bales-command` می‌فرستند (تخته خودش می‌پرسد و «برگرد»
 * دارد)؛ وگرنه «نشانم بده» و «افزودن فرش» به زبانهٔ نگله‌ها می‌روند و بقیه را خودِ سربرگ انجام می‌دهد. دکمه‌ای که بیننده اجازه‌اش را ندارد هرگز نشان داده نمی‌شود (انباردار نه «قطعی
 * کن» دارد نه «نرخ بگذارید»، و متن قفلش بازرگان را نام می‌برد). هیچ مبلغ تومانی در محموله نیست.
 */
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, useId } from 'vue'
import { ApiError, api, errorMessage } from '@/lib/api'
import { appContext } from '@/lib/context'
import { faJalali } from '@/lib/jalali'
import { coreMessages, message } from '@/lib/messages'
import { confirmDialog, toast, toastAfterReload } from '@/lib/ui'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import Field from '@/components/ui/Field.vue'
import JalaliDateField from '@/components/ui/JalaliDateField.vue'
import Modal from '@/components/ui/Modal.vue'
import NumberField from '@/components/ui/NumberField.vue'
import {
  ShipmentStatus,
  type Bale,
  type DeclaredValues,
  type DeclaredValuesWriteResult,
  type FinalizeShipmentResult,
  type LinesBatchResult,
  type ReadinessIssue,
  type Shipment,
  type UpdateShipmentRequest,
} from '@/lib/types/shipments'
import {
  BALES_COMMAND_EVENT,
  SHIPMENT_CHANGED_EVENT,
  actionLabel,
  balesCommand,
  baleVersion,
  blockingIssues,
  copy,
  isReady,
  num,
  orderIssues,
  readinessActions,
  sameSizeOps,
  shipmentName,
  type BalesCommandDetail,
  type ReadinessAction,
  type ShipmentChangedDetail,
} from './shipmentShell'

const props = defineProps<{
  shipmentId: string
  /** بیننده Business است: «قطعی کن»، «باز کردن دوباره»، «نرخ بگذارید» و ویرایش «بیشتر» */
  canBusiness: boolean
}>()

const SOURCE = 'shipment-header'
const ctx = appContext()
const uid = useId()

const shipment = ref<Shipment | null>(null)
const loading = ref(true)
const loadError = ref('')
/** کار در جریان: «قطعی کن»، «باز کردن دوباره»، یا کد تراشه‌ای که دکمه‌اش زده شد */
const busy = ref<string | null>(null)

const locked = computed(() => shipment.value?.status === ShipmentStatus.Finalized)
const issues = computed(() => orderIssues(shipment.value?.readiness ?? []))
const blocking = computed(() => blockingIssues(issues.value))
const ready = computed(() => (shipment.value ? isReady(shipment.value.totals.lines, issues.value) : false))
const number = computed(() => shipment.value?.number ?? 0)

const finalizeHint = computed(() =>
  blocking.value.length > 0
    ? message('E_SHIPMENT_NOT_READY', { issues: blocking.value.map((i) => i.message).join('؛ ') })
    : '',
)
const hintId = `${uid}-finalize-hint`

const lockText = computed(() =>
  props.canBusiness
    ? message('E_SHIPMENT_LOCKED_HEADER', { number: number.value })
    : message('E_SHIPMENT_LOCKED_OPERATOR'),
)

/** عنوانی که خودِ بازرگان نوشته (نه «اظهار ۸» پیش‌فرض) زیر شماره می‌آید. */
const ownTitle = computed(() => {
  const s = shipment.value
  if (!s || !s.title.trim()) return ''
  return s.title === message('I_SHIPMENT_TITLE', { number: s.number }) ? '' : s.title
})

const moreItems = computed(() => {
  const s = shipment.value
  if (!s) return []
  return [
    { label: 'مقصد', value: s.destination },
    { label: 'خریدار', value: s.buyerName },
    { label: 'شمارهٔ اظهار', value: s.declarationNo },
    { label: 'گمرک', value: s.customsOffice },
    { label: 'نرخ دلار', value: s.usdRate !== null ? num(s.usdRate) : null },
    { label: `متراژ هدف هر ${ctx.terms.bale}`, value: s.targetBaleAreaM2 !== null ? `${num(s.targetBaleAreaM2)} متر` : null },
    { label: 'یادداشت', value: s.notes },
  ].filter((item) => item.value !== null && item.value !== '')
})

function issueKey(issue: ReadinessIssue): string {
  return `${issue.code}:${issue.key ?? ''}:${issue.baleId ?? ''}`
}

function messageId(index: number): string {
  return `${uid}-issue-${index}`
}

// ── خواندن و هم‌گامی ────────────────────────────────────────────────────────

async function load() {
  try {
    shipment.value = await api.get<Shipment>(`/api/shipments/${props.shipmentId}`)
    loadError.value = ''
  } catch (error) {
    loadError.value = errorMessage(error) || copy('loadFailed')
  } finally {
    loading.value = false
  }
}

function notifyChanged() {
  window.dispatchEvent(
    new CustomEvent<ShipmentChangedDetail>(SHIPMENT_CHANGED_EVENT, { detail: { shipmentId: props.shipmentId, source: SOURCE } }),
  )
}

function onChanged(event: Event) {
  const detail = (event as CustomEvent<Partial<ShipmentChangedDetail>>).detail
  if (detail?.source === SOURCE) return
  if (detail?.shipmentId && detail.shipmentId !== props.shipmentId) return
  void load()
}

function onVisibility() {
  if (document.visibilityState === 'visible') void load()
}

onMounted(() => {
  void load()
  window.addEventListener(SHIPMENT_CHANGED_EVENT, onChanged)
  document.addEventListener('visibilitychange', onVisibility)
})

onBeforeUnmount(() => {
  window.removeEventListener(SHIPMENT_CHANGED_EVENT, onChanged)
  document.removeEventListener('visibilitychange', onVisibility)
})

/** خطای نوشتن: پیام فارسی سرور؛ با ۴۰۹ (نسخهٔ کهنه، قفل) محموله دوباره خوانده می‌شود. */
async function failed(error: unknown) {
  toast.error(errorMessage(error) || coreMessages.E_SERVER)
  if (error instanceof ApiError && (error.status === 409 || error.status === 404)) await load()
}

// ── «قطعی کن» و «باز کردن دوباره» ───────────────────────────────────────────

async function finalize() {
  const s = shipment.value
  if (!s || busy.value || blocking.value.length > 0) return
  const ok = await confirmDialog({
    title: copy('finalizeConfirmTitle', { number: s.number }),
    message: copy('finalizeConfirm', { pieces: s.totals.pieces }),
    confirmLabel: copy('finalizeYes'),
    cancelLabel: copy('finalizeNo'),
  })
  if (!ok) return

  busy.value = 'finalize'
  try {
    const result = await api.post<FinalizeShipmentResult>(`/api/shipments/${s.id}/finalize`, { version: s.version })
    if (result.ok) {
      toastAfterReload(copy('finalized', { number: s.number }))
      window.location.reload()
      return
    }
    // کمبودی همین حالا پیدا شد (کس دیگری چیزی عوض کرد): فهرست کامل سرور
    shipment.value = { ...result.shipment, readiness: result.issues }
    toast.error(finalizeHint.value || coreMessages.E_CONFLICT)
  } catch (error) {
    await failed(error)
  } finally {
    busy.value = null
  }
}

async function reopen() {
  const s = shipment.value
  if (!s || busy.value) return
  const ok = await confirmDialog({
    title: copy('reopenConfirmTitle', { number: s.number }),
    message: copy('reopenConfirm'),
    confirmLabel: copy('reopenYes'),
    cancelLabel: copy('finalizeNo'),
  })
  if (!ok) return

  busy.value = 'reopen'
  try {
    await api.post<Shipment>(`/api/shipments/${s.id}/reopen`, { version: s.version, reason: null })
    toastAfterReload(copy('reopened', { number: s.number }))
    window.location.reload()
  } catch (error) {
    await failed(error)
    busy.value = null
  }
}

// ── دکمه‌های تراشه‌ها ────────────────────────────────────────────────────────

/** زبانهٔ نگله‌ها همین صفحه است؟ (جزیرهٔ جدول نگله‌ها سوار است) */
function boardHere(): boolean {
  return document.querySelector('[data-island="baling-board"]') !== null
}

function goToBales() {
  window.location.href = `/Shipments/${props.shipmentId}?tab=bales`
}

/**
 * دکمهٔ یک تراشه. وقتی صفحهٔ نگله‌بندی همین‌جاست فرمان به آن می‌رود (تخته خودش می‌پرسد، «برگرد» دارد و جدول را صاف
 * می‌کند)؛ وگرنه «نشانم بده» و «افزودن فرش» به زبانهٔ نگله‌ها می‌روند و «بقیه همان اندازهٔ اول» و «حذف کن» را خودِ سربرگ
 * انجام می‌دهد.
 */
async function runAction(issue: ReadinessIssue, action: ReadinessAction) {
  const command = balesCommand(issue, action)
  if (command && boardHere()) {
    window.dispatchEvent(
      new CustomEvent<BalesCommandDetail>(BALES_COMMAND_EVENT, { detail: { ...command, shipmentId: props.shipmentId } }),
    )
    return
  }

  switch (action) {
    case 'addRug':
    case 'showMe':
      goToBales()
      return
    case 'deleteBale':
      return deleteBale(issue)
    case 'sameSize':
      return sameSize(issue)
    case 'setRate':
      rateForm.key = issue.key
      rateForm.value = null
      rateForm.error = ''
      // فوکوس در خانهٔ دلار، تا بازرگان همان لحظه عدد را بنویسد
      await nextTick()
      document.getElementById(rateFieldId)?.focus()
      return
  }
}

async function deleteBale(issue: ReadinessIssue) {
  const s = shipment.value
  if (!s || busy.value || !issue.baleId) return
  const version = baleVersion(s.bales, issue.baleId)
  if (version === null) {
    await load()
    return
  }

  busy.value = issueKey(issue)
  try {
    await api.del(`/api/bales/${issue.baleId}?version=${version}`)
    toast.success(copy('baleDeleted', { n: issue.baleNumber ?? 0 }))
    await load()
    notifyChanged()
  } catch (error) {
    await failed(error)
  } finally {
    busy.value = null
  }
}

/** «[بقیه همان اندازهٔ اول]»: یک پرسش، بعد یک درخواست با یک عمل `measure` برای هر فرش (C.4). */
async function sameSize(issue: ReadinessIssue) {
  const s = shipment.value
  if (!s || busy.value) return
  const ok = await confirmDialog({
    title: copy('sameSizeConfirmTitle', { n: issue.count }),
    confirmLabel: copy('sameSizeYes'),
    cancelLabel: copy('no'),
  })
  if (!ok) return

  busy.value = issueKey(issue)
  try {
    const bales = await api.get<Bale[]>(`/api/shipments/${s.id}/bales`)
    const ops = sameSizeOps(bales, issue.rugIds)
    if (ops.length > 0) {
      const result = await api.post<LinesBatchResult>(`/api/shipments/${s.id}/lines/batch`, { ops })
      const done = result.results.filter((r) => r.ok).length
      const failedCount = result.results.length - done
      if (failedCount === 0) toast.success(copy('sameSizeDone', { n: done }))
      else toast.warning(copy('sameSizePartial', { done, failed: failedCount }))
      notifyChanged()
    }
    await load()
  } catch (error) {
    await failed(error)
  } finally {
    busy.value = null
  }
}

// ── «نرخ بگذارید» (C.5) ─────────────────────────────────────────────────────

const rateForm = reactive<{ key: string | null; value: number | null; error: string; saving: boolean }>({
  key: null,
  value: null,
  error: '',
  saving: false,
})
const rateFieldId = `${uid}-rate`

async function saveRate() {
  if (!rateForm.key || rateForm.saving) return
  if (rateForm.value === null) {
    rateForm.error = message('E_REQUIRED', { label: 'ارزش گمرکی' })
    return
  }

  rateForm.saving = true
  rateForm.error = ''
  try {
    const { stamp } = await api.get<DeclaredValues>('/api/declared-values')
    const result = await api.post<DeclaredValuesWriteResult>('/api/declared-values', {
      keyValue: rateForm.key,
      usdPerM2: rateForm.value,
      stamp,
    })
    toast.success(message('I_DECLARED_ADDED', { key: rateForm.key, n: result.affectedOpenLines }))
    rateForm.key = null
    await load()
    notifyChanged()
  } catch (error) {
    rateForm.error = (error instanceof ApiError && error.fieldError('usdPerM2')) || errorMessage(error)
  } finally {
    rateForm.saving = false
  }
}

// ── «بیشتر»: ویرایش فیلدهای سربرگ (Business) ────────────────────────────────

const editOpen = ref(false)
const saving = ref(false)
const form = reactive({
  title: '',
  documentDate: null as string | null,
  destination: '',
  buyerName: '',
  declarationNo: '',
  customsOffice: '',
  usdRate: null as number | null,
  targetBaleAreaM2: null as number | null,
  notes: '',
})
const formErrors = reactive<Record<string, string>>({})
const formError = ref('')

function openEdit() {
  const s = shipment.value
  if (!s) return
  form.title = s.title
  form.documentDate = s.documentDate
  form.destination = s.destination ?? ''
  form.buyerName = s.buyerName ?? ''
  form.declarationNo = s.declarationNo ?? ''
  form.customsOffice = s.customsOffice ?? ''
  form.usdRate = s.usdRate
  form.targetBaleAreaM2 = s.targetBaleAreaM2
  form.notes = s.notes ?? ''
  for (const key of Object.keys(formErrors)) delete formErrors[key]
  formError.value = ''
  editOpen.value = true
}

function blankToNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed ? trimmed : null
}

async function saveEdit() {
  const s = shipment.value
  if (!s || saving.value) return
  saving.value = true
  formError.value = ''
  for (const key of Object.keys(formErrors)) delete formErrors[key]

  const body: UpdateShipmentRequest = {
    title: blankToNull(form.title),
    // سرور تاریخ خالی را «همان قبلی» می‌داند، ولی DateOnly خالی (null) را نمی‌پذیرد
    documentDate: form.documentDate ?? s.documentDate,
    destination: blankToNull(form.destination),
    buyerName: blankToNull(form.buyerName),
    declarationNo: blankToNull(form.declarationNo),
    customsOffice: blankToNull(form.customsOffice),
    targetBaleAreaM2: form.targetBaleAreaM2,
    usdRate: form.usdRate,
    notes: blankToNull(form.notes),
    version: s.version,
  }

  try {
    shipment.value = await api.put<Shipment>(`/api/shipments/${s.id}`, body)
    editOpen.value = false
    toast.success(copy('saved'))
    notifyChanged()
  } catch (error) {
    if (error instanceof ApiError && error.status === 400) {
      for (const key of Object.keys(form)) {
        const text = error.fieldError(key)
        if (text) formErrors[key] = text
      }
      formError.value = Object.keys(formErrors).length > 0 ? coreMessages.E_VALIDATION : error.message
    } else if (error instanceof ApiError && error.status === 409) {
      formError.value = error.message
      await load()
    } else {
      formError.value = errorMessage(error)
    }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <header class="space-y-4" :aria-busy="loading">
    <div v-if="!shipment && loading" class="space-y-3" aria-hidden="true">
      <div class="skeleton h-12 w-64 rounded-lg" />
      <div class="skeleton h-20 w-full rounded-xl" />
    </div>

    <div
      v-else-if="!shipment"
      class="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-error/30 bg-error-container px-4 py-3 text-on-error-container"
      role="alert"
    >
      <span class="flex items-center gap-2">
        <AppIcon name="error" class="h-5 w-5 shrink-0" />
        {{ loadError || copy('loadFailed') }}
      </span>
      <Button icon="undo" @click="load">{{ copy('retry') }}</Button>
    </div>

    <template v-else>
      <div class="flex flex-wrap items-start gap-3">
        <!-- در RTL «بازگشت» به سمت راست اشاره می‌کند -->
        <a
          href="/Shipments"
          class="grid h-11 w-11 shrink-0 place-items-center rounded-lg border border-outline-variant hover:bg-surface-container"
        >
          <AppIcon name="arrow-right" :label="copy('back')" />
        </a>
        <div class="min-w-0 flex-1">
          <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
            <h1 class="text-2xl font-bold" data-numeric>{{ shipmentName(shipment.number) }}</h1>
            <span
              v-if="locked"
              class="inline-flex items-center gap-1 rounded-full bg-surface-container-highest px-3 py-1 text-sm font-medium text-on-surface"
              data-shipment-status="finalized"
            >
              <AppIcon name="lock" class="h-4 w-4" />
              {{ copy('final') }}
            </span>
            <span
              v-else
              class="inline-flex items-center gap-1 rounded-full bg-secondary-container px-3 py-1 text-sm font-medium text-on-secondary-container"
              data-shipment-status="open"
            >
              <AppIcon name="edit" class="h-4 w-4" />
              {{ copy('draft') }}
            </span>
          </div>
          <p class="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-on-surface-variant" data-numeric>
            <span v-if="ownTitle" class="font-medium text-on-surface">{{ ownTitle }}</span>
            <span class="inline-flex items-center gap-1">
              <AppIcon name="calendar" class="h-4 w-4" />
              {{ faJalali(shipment.documentDate) }}
            </span>
          </p>
        </div>

        <div v-if="canBusiness" class="flex flex-wrap items-center gap-2">
          <Button
            v-if="!locked"
            variant="primary"
            size="lg"
            icon="lock"
            :loading="busy === 'finalize'"
            :disabled="blocking.length > 0 || busy !== null"
            :aria-describedby="blocking.length > 0 ? hintId : undefined"
            data-shipment-action="finalize"
            @click="finalize"
          >
            {{ copy('finalize') }}
          </Button>
          <Button
            v-else
            variant="outline"
            size="lg"
            icon="unlock"
            :loading="busy === 'reopen'"
            :disabled="busy !== null"
            data-shipment-action="reopen"
            @click="reopen"
          >
            {{ copy('reopen') }}
          </Button>
        </div>
      </div>

      <p
        v-if="canBusiness && !locked && blocking.length > 0"
        :id="hintId"
        class="flex items-start gap-2 text-sm text-on-surface-variant"
        data-numeric
      >
        <AppIcon name="info" class="mt-0.5 h-4 w-4 shrink-0" />
        <span>{{ finalizeHint }}</span>
      </p>

      <dl
        class="flex flex-wrap gap-x-8 gap-y-3 rounded-xl border border-outline-variant bg-surface-container-lowest px-5 py-4"
        data-numeric
      >
        <div>
          <dt class="text-sm text-on-surface-variant">{{ ctx.terms.bale }}</dt>
          <dd class="text-2xl font-bold">{{ num(shipment.totals.bales) }}</dd>
        </div>
        <div>
          <dt class="text-sm text-on-surface-variant">تخته</dt>
          <dd class="text-2xl font-bold">
            {{ num(shipment.totals.pieces) }}
            <!-- با دسته، شمار ردیف پشت تخته‌ها (واژه‌نامهٔ A.2: «۵۲ تخته (۳ ردیف)») -->
            <span v-if="shipment.totals.lines !== shipment.totals.pieces" class="text-sm font-normal text-on-surface-variant">
              {{ copy('rowsSuffix', { rows: shipment.totals.lines }) }}
            </span>
          </dd>
        </div>
        <div>
          <dt class="text-sm text-on-surface-variant">متر</dt>
          <dd class="text-2xl font-bold">{{ num(shipment.totals.areaM2) }}</dd>
        </div>
        <div>
          <dt class="text-sm text-on-surface-variant">دلار</dt>
          <dd class="text-2xl font-bold">{{ num(shipment.totals.usdTotal) }}</dd>
        </div>
      </dl>

      <div v-if="locked" class="space-y-2">
        <p class="flex items-start gap-2 rounded-lg bg-surface-container-high px-4 py-3 text-sm" role="status" data-numeric>
          <AppIcon name="lock" class="mt-0.5 h-4 w-4 shrink-0" />
          <span>{{ lockText }}</span>
        </p>
        <p v-if="canBusiness" class="flex items-start gap-2 px-1 text-sm text-on-surface-variant">
          <AppIcon name="info" class="mt-0.5 h-4 w-4 shrink-0" />
          <span>{{ copy('lateCost') }}</span>
        </p>
      </div>

      <ul
        v-else-if="issues.length > 0 || shipment.totals.lines > 0"
        class="flex flex-wrap gap-2"
        :aria-label="copy('readinessLabel')"
        data-numeric
      >
        <li
          v-if="ready"
          class="inline-flex min-h-11 items-center gap-1.5 rounded-full bg-success/12 px-3 py-1 text-sm font-medium text-success"
          data-readiness-state="ready"
        >
          <AppIcon name="check" class="h-4 w-4 shrink-0" />
          {{ copy('readyToFinalize') }}
        </li>
        <li
          v-for="(issue, index) in issues"
          :key="issueKey(issue)"
          class="flex max-w-full flex-wrap items-center gap-2 rounded-2xl px-3 py-1.5 text-sm"
          :class="issue.blocking ? 'bg-error-container text-on-error-container' : 'bg-warning/12 text-warning'"
          :data-readiness="issue.code"
        >
          <AppIcon :name="issue.blocking ? 'error' : 'warning'" class="h-4 w-4 shrink-0" />
          <span :id="messageId(index)">{{ issue.message }}</span>
          <button
            v-for="action in readinessActions(issue, canBusiness, locked)"
            :key="action"
            type="button"
            class="inline-flex min-h-11 items-center rounded-full border border-current px-3 font-medium hover:bg-surface-container-lowest/40 disabled:opacity-60"
            :aria-describedby="messageId(index)"
            :disabled="busy !== null"
            :data-readiness-action="action"
            @click="runAction(issue, action)"
          >
            {{ actionLabel(action) }}
          </button>

          <form
            v-if="rateForm.key !== null && issue.key === rateForm.key && readinessActions(issue, canBusiness, locked).includes('setRate')"
            class="flex w-full flex-wrap items-end gap-2 pt-1 text-on-surface"
            @submit.prevent="saveRate"
          >
            <Field :label="copy('rateFormLabel', { key: rateForm.key })" :error="rateForm.error || null" :id="rateFieldId" v-slot="f">
              <NumberField
                :id="f.id"
                v-model="rateForm.value"
                kind="usd"
                :described-by="f.describedBy"
                :invalid="f.invalid"
              />
            </Field>
            <Button type="submit" variant="primary" icon="check" :loading="rateForm.saving">{{ copy('rateSubmit') }}</Button>
            <Button variant="ghost" @click="rateForm.key = null">{{ copy('cancel') }}</Button>
          </form>
        </li>
      </ul>

      <details
        v-if="moreItems.length > 0 || canBusiness"
        class="group rounded-xl border border-outline-variant bg-surface-container-lowest"
      >
        <summary class="flex min-h-11 cursor-pointer list-none items-center justify-between gap-2 px-4 font-medium">
          {{ copy('more') }}
          <AppIcon name="chevron-down" class="h-5 w-5 transition-transform group-open:rotate-180" />
        </summary>
        <div class="border-t border-outline-variant px-4 py-3">
          <dl v-if="moreItems.length > 0" class="grid gap-x-6 gap-y-3 sm:grid-cols-2" data-numeric>
            <div v-for="item in moreItems" :key="item.label">
              <dt class="text-sm text-on-surface-variant">{{ item.label }}</dt>
              <dd class="break-words">{{ item.value }}</dd>
            </div>
          </dl>
          <div v-if="canBusiness" class="mt-3">
            <Button icon="edit" data-shipment-action="edit" @click="openEdit">{{ copy('edit') }}</Button>
          </div>
        </div>
      </details>
    </template>

    <Modal
      v-if="canBusiness && shipment"
      v-model:open="editOpen"
      :title="copy('editTitle', { number: shipment.number })"
      :description="locked ? copy('lockedFieldsHint') : undefined"
      size="lg"
      :close-on-backdrop="false"
    >
      <form class="grid gap-4 sm:grid-cols-2" @submit.prevent="saveEdit">
        <Field label="عنوان" :error="formErrors.title || null" v-slot="f">
          <input
            :id="f.id"
            v-model="form.title"
            type="text"
            :disabled="locked"
            :aria-describedby="f.describedBy"
            :aria-invalid="f.invalid || undefined"
            class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 outline-none focus:border-primary disabled:opacity-60"
          />
        </Field>
        <Field label="تاریخ اظهار" :error="formErrors.documentDate || null" v-slot="f">
          <JalaliDateField
            :id="f.id"
            v-model="form.documentDate"
            :disabled="locked"
            :described-by="f.describedBy"
            :invalid="f.invalid"
          />
        </Field>
        <Field label="مقصد" :error="formErrors.destination || null" v-slot="f">
          <input
            :id="f.id"
            v-model="form.destination"
            type="text"
            :disabled="locked"
            :aria-describedby="f.describedBy"
            class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 outline-none focus:border-primary disabled:opacity-60"
          />
        </Field>
        <Field label="خریدار" :error="formErrors.buyerName || null" v-slot="f">
          <input
            :id="f.id"
            v-model="form.buyerName"
            type="text"
            :disabled="locked"
            :aria-describedby="f.describedBy"
            class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 outline-none focus:border-primary disabled:opacity-60"
          />
        </Field>
        <Field label="شمارهٔ اظهار" :error="formErrors.declarationNo || null" v-slot="f">
          <input
            :id="f.id"
            v-model="form.declarationNo"
            type="text"
            dir="ltr"
            :disabled="locked"
            :aria-describedby="f.describedBy"
            class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-end outline-none focus:border-primary disabled:opacity-60"
          />
        </Field>
        <Field label="گمرک" :error="formErrors.customsOffice || null" v-slot="f">
          <input
            :id="f.id"
            v-model="form.customsOffice"
            type="text"
            :disabled="locked"
            :aria-describedby="f.describedBy"
            class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 outline-none focus:border-primary disabled:opacity-60"
          />
        </Field>
        <Field label="نرخ دلار" :error="formErrors.usdRate || null" v-slot="f">
          <NumberField
            :id="f.id"
            v-model="form.usdRate"
            kind="number"
            :scale="6"
            :suffix="ctx.currency.label"
            :disabled="locked"
            :described-by="f.describedBy"
            :invalid="f.invalid"
          />
        </Field>
        <Field :label="`متراژ هدف هر ${ctx.terms.bale}`" :error="formErrors.targetBaleAreaM2 || null" v-slot="f">
          <NumberField
            :id="f.id"
            v-model="form.targetBaleAreaM2"
            kind="number"
            :scale="2"
            suffix="متر"
            :described-by="f.describedBy"
            :invalid="f.invalid"
          />
        </Field>
        <Field class="sm:col-span-2" label="یادداشت" :error="formErrors.notes || null" v-slot="f">
          <textarea
            :id="f.id"
            v-model="form.notes"
            rows="3"
            :aria-describedby="f.describedBy"
            class="w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 py-2 outline-none focus:border-primary"
          />
        </Field>
        <p v-if="formError" class="flex items-start gap-2 text-sm text-error sm:col-span-2" role="alert">
          <AppIcon name="error" class="mt-0.5 h-4 w-4 shrink-0" />
          <span>{{ formError }}</span>
        </p>
        <div class="flex flex-wrap justify-end gap-3 sm:col-span-2">
          <Button @click="editOpen = false">{{ copy('cancel') }}</Button>
          <Button type="submit" variant="primary" icon="check" :loading="saving">{{ copy('save') }}</Button>
        </div>
      </form>
    </Modal>
  </header>
</template>
