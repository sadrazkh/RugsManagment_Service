<script setup lang="ts">
/**
 * فرم ثبت/ویرایش فرش (جزیرهٔ `rug-form`). اگر rugId داشته باشد حالت ویرایش است.
 *
 * از فاز ۲ فقط `values` (کلیدهای FieldCatalog: `rug.serial`، `rug.qty`، `rug.intakeLength`، …) و در ویرایش `version`
 * فرستاده می‌شود (I.3، E5): نه `purchaseCost`، نه `targetSalePrice` و نه طول/عرض مؤثرِ قدیمی. پس رفت‌وبرگشت فرم نه
 * سطرهای خرید چندگانه را خراب می‌کند و نه اندازهٔ اول را با اندازهٔ نهایی رونویسی. در ویرایش فقط خانه‌هایی که واقعاً
 * عوض شده‌اند فرستاده می‌شوند.
 *
 * - شماره و تعداد: «3778» یا دستهٔ «3911 - 3960» با تعداد؛ بازه هرگز کوتاه‌تر از تعداد نیست. هنگام ثبت شمارهٔ بعدی
 *   (G.4) کم‌رنگ پیشنهاد می‌شود و با Enter، Tab یا یک دکمه نوشته می‌شود؛ برای دسته پیشنهاد «۳۹۱۱ - ۳۹۶۰» می‌آید. در
 *   حالت «خودکار» خانهٔ شماره فقط «خودکار: ۴۰۰۴» را نشان می‌دهد، چون شماره را سرور هنگام ثبت می‌گذارد. صفحه‌کلید خانهٔ
 *   شماره متنی است (بازه خط تیره و شمارهٔ متنی حرف دارد).
 * - Enter در خانه‌های ویرایشگر سطرهای خرید (داخل همین فرم) فرم فرش را نمی‌فرستد؛ آن ویرایشگر ذخیرهٔ خودش را دارد.
 * - اندازهٔ اول (و در ویرایش اندازهٔ نهایی با «همان اندازهٔ اول»)، متراژ دقیق «۰٫۱۶ × ۵۰ = ۸٫۰۰».
 * - مبلغ‌ها فقط برای Business: هنگام ثبت خانه‌های خرید جدول ورود («قیمت خرید» با «هر متر | هر تخته | کل» و «دلالی»)،
 *   هنگام ویرایش ویرایشگر کامل سطرهای خرید؛ و «قیمت فروش هدف». انباردار هیچ کلید پولی نمی‌فرستد.
 * - همپوشانی شماره (۴۰۹ با `nextFree`): «شمارهٔ ۴۰۰۵ آزاد است؛ همان را بگذارم؟».
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ApiError, api, errorMessage } from '@/lib/api'
import { moneyVisible, appContext } from '@/lib/context'
import { faDecimal, faMoney, toPersianDigits } from '@/lib/format'
import { isLargeArea } from '@/lib/money'
import { message } from '@/lib/messages'
import { lineArea, pieceArea, toNumber, tryDec } from '@/lib/num'
import Button from '@/components/ui/Button.vue'
import Combobox from '@/components/ui/Combobox.vue'
import Field from '@/components/ui/Field.vue'
import JalaliDateField from '@/components/ui/JalaliDateField.vue'
import NumberField from '@/components/ui/NumberField.vue'
import PurchaseLinesEditor from '@/components/PurchaseLinesEditor.vue'
import type { CustomFieldDefinition, WorkflowTemplate } from '@/lib/types'
import {
  CostBasis,
  type FieldValues,
  type JsonValue,
  type NextSerial,
  type PurchaseCategoryOption,
  type PurchaseLinesResult,
  type Rug,
} from '@/lib/types/rugs'
import { announceRugChanged } from './rugs/rugEvents'
import { purchaseAmounts } from './rugs/purchasePreview'
import { acceptedSerialSuggestion, adoptSavedVersion, blocksImplicitSubmit, isAutoSerial } from './rugs/rugFormRules'
import type { SerialMode } from '@/lib/types/platform'

const props = defineProps<{ rugId?: string }>()
const isEdit = computed(() => !!props.rugId)
const canSeeMoney = moneyVisible()
const SOURCE = 'rug-form'

const form = reactive({
  serial: '',
  quantity: 1 as number | null,
  title: '',
  origin: '' as string | null,
  pattern: '' as string | null,
  material: '' as string | null,
  knotDensity: null as number | null,
  intakeLength: null as number | null,
  intakeWidth: null as number | null,
  finalLength: null as number | null,
  finalWidth: null as number | null,
  targetSalePrice: null as number | null,
  notes: '',
  workflowTemplateId: '' as string,
})

/** مقدارهای بار شده (حالت ویرایش) تا فقط خانه‌های عوض‌شده فرستاده شوند. */
let initial: Record<string, JsonValue> = {}
const version = ref(0)

const metadata = reactive<Record<string, string>>({})
let initialMetadata: Record<string, string> = {}
const templates = ref<WorkflowTemplate[]>([])
const customFields = ref<CustomFieldDefinition[]>([])
const suggestions = reactive<Record<string, string[]>>({ 'rug.origin': [], 'rug.pattern': [], 'rug.material': [], 'rug.title': [] })
const nextSerial = ref<number | null>(null)
/** حالت شمارهٔ کارگاه از `next-serial` (فقط هنگام ثبت). */
const serialMode = ref<SerialMode | null>(null)

/** خانه‌های خرید ثبت فرش (نوع‌های «خرید» با ShowInIntake)، مثل ستون‌های جدول ورود (G.2). */
interface PurchaseCell {
  category: PurchaseCategoryOption
  key: string
  basis: CostBasis
  rate: number | null
}
const purchaseCells = ref<PurchaseCell[]>([])

const loading = ref(true)
const saving = ref(false)
const error = ref('')
const stale = ref(false)
const nextFree = ref<number | null>(null)
const fieldErrors = reactive<Record<string, string>>({})

const lengthField = ref<InstanceType<typeof NumberField> | null>(null)
const widthField = ref<InstanceType<typeof NumberField> | null>(null)

const quantity = computed(() => (form.quantity && form.quantity >= 1 ? Math.floor(form.quantity) : 1))
const piece = computed(() => {
  const l = tryDec(form.intakeLength)
  const w = tryDec(form.intakeWidth)
  return l && w ? pieceArea(l, w) : null
})
const areaText = computed(() => {
  if (!piece.value) return '—'
  const one = faDecimal(toNumber(piece.value), 2, 2)
  if (quantity.value <= 1) return `${one} متر`
  return `${one} × ${toPersianDigits(quantity.value)} = ${faDecimal(toNumber(lineArea(piece.value, quantity.value)), 2, 2)} متر`
})
const largeArea = computed(() => piece.value !== null && isLargeArea(piece.value))

/** پیشنهاد شماره برای همین فرم: تک، یا برای دسته بازهٔ «از تا از + تعداد − ۱». */
const serialSuggestion = computed(() => {
  if (isEdit.value || nextSerial.value === null) return null
  return quantity.value > 1 ? `${nextSerial.value} - ${nextSerial.value + quantity.value - 1}` : String(nextSerial.value)
})

/** «خودکار»: خانهٔ شماره فقط نمایش است و سرور شماره را می‌گذارد. */
const autoSerial = computed(() => isAutoSerial(serialMode.value, isEdit.value))

/** شمارهٔ تکی که با تعداد بیش از یک نوشته شده: «3911» با ۵۰ → پیشنهاد «3911 - 3960». */
const serialAsLot = computed(() => {
  const text = form.serial.trim().replace(/[۰-۹]/g, (d) => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)))
  if (quantity.value <= 1 || !/^\d+$/.test(text)) return null
  const start = Number(text)
  return Number.isSafeInteger(start) ? `${start} - ${start + quantity.value - 1}` : null
})

const purchasePreview = computed(() => {
  if (!piece.value || purchaseCells.value.length === 0) return null
  const amounts = purchaseAmounts(
    purchaseCells.value.map((c) => ({ basis: c.basis, rate: c.rate })),
    piece.value,
    quantity.value,
    appContext().moneyScale,
  )
  return amounts.map((a) => toNumber(a))
})

const UNIT_LABELS: Record<number, string> = {
  [CostBasis.PerM2]: 'هر متر',
  [CostBasis.PerPiece]: 'هر تخته',
  [CostBasis.Total]: 'کل',
}
const UNIT_ORDER = [CostBasis.PerM2, CostBasis.PerPiece, CostBasis.Total] as const

function optionsOf(f: CustomFieldDefinition): string[] {
  if (!f.optionsJson) return []
  try {
    return JSON.parse(f.optionsJson) as string[]
  } catch {
    return []
  }
}

function onPair([length, width]: [number, number]) {
  form.intakeLength = length
  form.intakeWidth = width
  delete fieldErrors['rug.intakeLength']
  delete fieldErrors['rug.intakeWidth']
}

function onFinalPair([length, width]: [number, number]) {
  form.finalLength = length
  form.finalWidth = width
}

function finalSameAsIntake() {
  form.finalLength = form.intakeLength
  form.finalWidth = form.intakeWidth
}

function setMetaNumber(key: string, value: number | null) {
  metadata[key] = value === null ? '' : String(value)
}

async function loadSuggestions() {
  await Promise.all(
    Object.keys(suggestions).map(async (field) => {
      try {
        suggestions[field] = await api.get<string[]>(`/api/rugs/values?field=${encodeURIComponent(field)}&take=50`)
      } catch {
        suggestions[field] = []
      }
    }),
  )
}

onMounted(async () => {
  try {
    const requests: Promise<unknown>[] = [
      api.get<WorkflowTemplate[]>('/api/lookups/workflow-templates').then((t) => (templates.value = t)),
      api.get<CustomFieldDefinition[]>('/api/lookups/custom-fields').then((cf) => (customFields.value = cf)),
    ]
    if (!isEdit.value) {
      requests.push(
        api.get<NextSerial>('/api/rugs/next-serial').then((n) => {
          nextSerial.value = n.next
          serialMode.value = n.mode ?? null
        }),
      )
      if (canSeeMoney) {
        requests.push(
          api.get<PurchaseCategoryOption[]>('/api/rugs/purchase-categories').then((options) => {
            purchaseCells.value = options
              .filter((c) => c.showInIntake)
              .map((c) => ({
                category: c,
                key: c.seedKey?.startsWith('purchase.') ? c.seedKey : `purchase.cat:${c.id}`,
                basis: c.defaultBasis,
                rate: c.defaultBasis === CostBasis.Percent ? c.defaultRate : null,
              }))
          }),
        )
      }
    }
    await Promise.all(requests)
    void loadSuggestions()

    if (!isEdit.value) {
      form.workflowTemplateId = templates.value.find((t) => t.isDefault)?.id ?? templates.value[0]?.id ?? ''
    }
    if (props.rugId) {
      const rug = await api.get<Rug>(`/api/rugs/${props.rugId}`)
      Object.assign(form, {
        serial: rug.serialNo ?? '',
        quantity: rug.quantity,
        title: rug.title ?? '',
        origin: rug.origin ?? '',
        pattern: rug.pattern ?? '',
        material: rug.material ?? '',
        knotDensity: rug.knotDensity ?? null,
        intakeLength: rug.intakeLength,
        intakeWidth: rug.intakeWidth,
        finalLength: rug.finalLength,
        finalWidth: rug.finalWidth,
        targetSalePrice: rug.targetSalePrice ?? null,
        notes: rug.notes ?? '',
      })
      version.value = rug.version
      if (rug.metadataJson) {
        try {
          Object.assign(metadata, JSON.parse(rug.metadataJson))
        } catch {
          /* متادیتای خراب نادیده */
        }
      }
      initialMetadata = { ...metadata }
      initial = buildValues()
    }
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
})

/** خانه‌های عددی دیگر (رجشمار، مبلغ‌ها): خطای خودشان را نگه می‌دارند و پیش از ارسال پرسیده می‌شوند. */
const otherNumberFields = new Set<{ validate: () => string | null }>()
function track(el: unknown) {
  if (el && typeof (el as { validate?: unknown }).validate === 'function') {
    otherNumberFields.add(el as { validate: () => string | null })
  }
}

function clearErrors() {
  for (const key of Object.keys(fieldErrors)) delete fieldErrors[key]
}

function validate(): boolean {
  clearErrors()
  let ok = true
  for (const field of otherNumberFields) if (field.validate() !== null) ok = false
  const lengthProblem = lengthField.value?.validate() ?? null
  const widthProblem = widthField.value?.validate() ?? null
  if (lengthProblem) fieldErrors['rug.intakeLength'] = lengthProblem
  else if (!form.intakeLength || form.intakeLength <= 0) fieldErrors['rug.intakeLength'] = message('E_REQUIRED', { label: 'طول' })
  if (widthProblem) fieldErrors['rug.intakeWidth'] = widthProblem
  else if (!form.intakeWidth || form.intakeWidth <= 0) fieldErrors['rug.intakeWidth'] = message('E_REQUIRED', { label: 'عرض' })
  if ((form.finalLength === null) !== (form.finalWidth === null)) fieldErrors['rug.finalLength'] = message('E_SIZE_PAIR')
  if (!form.quantity || form.quantity < 1) fieldErrors['rug.qty'] = message('E_RUG_QUANTITY')
  for (const f of customFields.value) {
    if (f.isRequired && !metadata[f.key]) fieldErrors[`rug.custom.${f.key}`] = message('E_REQUIRED', { label: f.label })
  }
  if (!ok && Object.keys(fieldErrors).length === 0) error.value = message('E_VALIDATION')
  return ok && Object.keys(fieldErrors).length === 0
}

function text(value: string | null | undefined): JsonValue {
  const trimmed = (value ?? '').trim()
  return trimmed.length ? trimmed : null
}

/** همهٔ خانه‌های فرم به‌صورت مقدار FieldCatalog (بی پول برای غیر Business). */
function buildValues(): Record<string, JsonValue> {
  const values: Record<string, JsonValue> = {
    'rug.serial': text(form.serial),
    'rug.qty': quantity.value,
    'rug.title': text(form.title),
    'rug.origin': text(form.origin),
    'rug.pattern': text(form.pattern),
    'rug.material': text(form.material),
    'rug.knotDensity': form.knotDensity,
    'rug.intakeLength': form.intakeLength,
    'rug.intakeWidth': form.intakeWidth,
    'rug.notes': text(form.notes),
  }
  if (isEdit.value) {
    values['rug.finalLength'] = form.finalLength
    values['rug.finalWidth'] = form.finalWidth
  }
  if (canSeeMoney) values['rug.targetSalePrice'] = form.targetSalePrice
  for (const f of customFields.value) values[`rug.custom.${f.key}`] = text(metadata[f.key])
  return values
}

/** در ویرایش فقط خانه‌های عوض‌شده؛ اندازهٔ اول و نهایی جفتی می‌روند. */
function changedValues(all: Record<string, JsonValue>): FieldValues {
  if (!isEdit.value) {
    const created: FieldValues = {}
    for (const [key, value] of Object.entries(all)) {
      if (key.startsWith('rug.final')) continue
      if (value !== null || key === 'rug.serial') created[key] = value
    }
    for (const cell of purchaseCells.value) {
      if (cell.rate === null) continue
      created[cell.key] = cell.basis === CostBasis.Percent ? cell.rate : { rate: cell.rate, basis: cell.basis }
    }
    return created
  }

  const changed: FieldValues = {}
  for (const [key, value] of Object.entries(all)) {
    if (key.startsWith('rug.custom.')) {
      const customKey = key.slice('rug.custom.'.length)
      if ((initialMetadata[customKey] ?? '') !== (metadata[customKey] ?? '')) changed[key] = value
    } else if (JSON.stringify(initial[key] ?? null) !== JSON.stringify(value ?? null)) {
      changed[key] = value
    }
  }
  for (const pair of [['rug.intakeLength', 'rug.intakeWidth'], ['rug.finalLength', 'rug.finalWidth']] as const) {
    if (pair[0] in changed || pair[1] in changed) {
      changed[pair[0]] = all[pair[0]]
      changed[pair[1]] = all[pair[1]]
    }
  }
  if ('rug.serial' in changed || 'rug.qty' in changed) {
    changed['rug.serial'] = all['rug.serial']
    changed['rug.qty'] = all['rug.qty']
  }
  return changed
}

function reloadPage() {
  window.location.reload()
}

/** ذخیرهٔ ویرایشگر خرید: نسخهٔ تازه فقط اگر روی همان نسخهٔ فرم ذخیره شده بود (وگرنه ذخیرهٔ فرم ۴۰۹ می‌گیرد). */
function onPurchaseSaved(result: PurchaseLinesResult, baseVersion: number) {
  version.value = adoptSavedVersion(version.value, baseVersion, result.version)
}

/** Enter در خانه‌های ویرایشگر خرید فرم فرش را به‌طور ضمنی نمی‌فرستد. */
function onPurchaseEditorKeydown(event: KeyboardEvent) {
  if (blocksImplicitSubmit(event)) event.preventDefault()
}

/** «پیشنهاد بعدی» (G.4): Enter یا Tab در خانهٔ خالی پیشنهاد را می‌پذیرد؛ Enter فقط می‌پذیرد و فرم را نمی‌فرستد. */
function onSerialKeydown(event: KeyboardEvent) {
  const accepted = acceptedSerialSuggestion(event.key, event.shiftKey, form.serial, serialSuggestion.value, serialMode.value)
  if (accepted === null) return
  useSuggestion(accepted)
  if (event.key === 'Enter') event.preventDefault()
}

function useSuggestion(serial: string | null) {
  if (!serial) return
  form.serial = serial
  delete fieldErrors['rug.serial']
}

function useNextFree() {
  if (nextFree.value === null) return
  form.serial = quantity.value > 1 ? `${nextFree.value} - ${nextFree.value + quantity.value - 1}` : String(nextFree.value)
  nextFree.value = null
  error.value = ''
  delete fieldErrors['rug.serial']
}

async function submit() {
  error.value = ''
  stale.value = false
  nextFree.value = null
  if (!validate()) return
  saving.value = true
  try {
    const values = changedValues(buildValues())
    let rug: Rug
    if (isEdit.value) {
      rug = await api.put<Rug>(`/api/rugs/${props.rugId}`, { values, version: version.value })
      // اگر ترک صفحه با «Leave site?» (ویرایشگر خرید کار ذخیره‌نشده دارد) لغو شد، فرم باید با نسخهٔ تازه دوباره کار کند
      version.value = rug.version
      initial = buildValues()
      initialMetadata = { ...metadata }
      saving.value = false
      announceRugChanged({ rugId: rug.id, version: rug.version, source: SOURCE })
    } else {
      rug = await api.post<Rug>('/api/rugs', {
        values,
        workflowTemplateId: form.workflowTemplateId || null,
        skippedOptionalStepIds: null,
        customSteps: null,
      })
    }
    window.location.href = `/Rugs/Details/${rug.id}`
  } catch (e) {
    error.value = errorMessage(e)
    if (e instanceof ApiError) {
      for (const [key, list] of Object.entries(e.errors)) if (list.length) fieldErrors[key] = list[0]
      if (e.status === 409 && e.field === 'rug.serial') {
        fieldErrors['rug.serial'] = e.message
        const free = e.body?.nextFree
        nextFree.value = typeof free === 'number' ? free : null
      } else if (e.status === 409 && !e.field) {
        stale.value = true
      }
    }
    saving.value = false
  }
}
</script>

<template>
  <div v-if="loading" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-8 text-center text-on-surface-variant">
    در حال بارگذاری…
  </div>
  <form v-else class="space-y-6" novalidate @submit.prevent="submit">
    <div v-if="error" class="rounded-lg bg-error-container px-4 py-3 text-sm text-on-error-container" role="alert">
      {{ error }}
      <div v-if="nextFree !== null" class="mt-2 flex flex-wrap items-center gap-2">
        <span>{{ message('Q_SERIAL_NEXT_FREE', { serial: toPersianDigits(nextFree) }) }}</span>
        <Button variant="primary" @click="useNextFree">بله</Button>
        <Button variant="ghost" @click="nextFree = null">خودم می‌نویسم</Button>
      </div>
      <div v-if="stale" class="mt-2">
        <Button icon="undo" @click="reloadPage">صفحه را تازه کن</Button>
      </div>
    </div>

    <!-- شماره و تعداد -->
    <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">شماره و تعداد</h2>
      <div class="grid gap-4 sm:grid-cols-3">
        <Field
          label="شماره"
          :error="fieldErrors['rug.serial']"
          :hint="autoSerial ? message('I_SERIAL_AUTO_HINT') : 'مثل ۳۷۷۸، یا برای دسته ۳۹۱۱ - ۳۹۶۰'"
          v-slot="f"
          class="sm:col-span-2"
        >
          <output
            v-if="autoSerial"
            :id="f.id"
            class="input flex items-center justify-end bg-surface-container text-on-surface-variant"
            :aria-describedby="f.describedBy"
            data-numeric
          >
            <bdi>{{ message('I_SERIAL_AUTO', { serial: serialSuggestion ? toPersianDigits(serialSuggestion) : '—' }) }}</bdi>
          </output>
          <input
            v-else
            :id="f.id"
            v-model="form.serial"
            dir="ltr"
            inputmode="text"
            autocomplete="off"
            autocapitalize="off"
            spellcheck="false"
            :placeholder="serialSuggestion ? toPersianDigits(serialSuggestion) : ''"
            class="input text-end"
            :aria-describedby="f.describedBy"
            :aria-invalid="f.invalid"
            @input="delete fieldErrors['rug.serial']"
            @keydown="onSerialKeydown"
          />
          <div v-if="!autoSerial" class="mt-1 flex flex-wrap gap-2">
            <Button v-if="serialSuggestion && !form.serial.trim()" variant="ghost" @click="useSuggestion(serialSuggestion)">
              بنویس: <bdi dir="ltr">{{ toPersianDigits(serialSuggestion) }}</bdi>
            </Button>
            <Button v-if="serialAsLot" variant="ghost" @click="useSuggestion(serialAsLot)">
              دسته کن: <bdi dir="ltr">{{ toPersianDigits(serialAsLot) }}</bdi>
            </Button>
          </div>
        </Field>
        <Field label="تعداد (تخته)" :error="fieldErrors['rug.qty']" required v-slot="f">
          <NumberField :id="f.id" :ref="track" v-model="form.quantity" kind="integer" :min="1" label="تعداد" :described-by="f.describedBy" :invalid="f.invalid" />
        </Field>
      </div>
    </section>

    <!-- مشخصات پایه -->
    <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">مشخصات فرش</h2>
      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Field label="عنوان" :error="fieldErrors['rug.title']" v-slot="f">
          <Combobox :id="f.id" v-model="form.title" :options="suggestions['rug.title']" allow-create :described-by="f.describedBy" placeholder="مثلاً گبه" />
        </Field>
        <Field label="نقشه" :error="fieldErrors['rug.pattern']" v-slot="f">
          <Combobox :id="f.id" v-model="form.pattern" :options="suggestions['rug.pattern']" allow-create :described-by="f.describedBy" />
        </Field>
        <Field label="محل بافت" :error="fieldErrors['rug.origin']" v-slot="f">
          <Combobox :id="f.id" v-model="form.origin" :options="suggestions['rug.origin']" allow-create :described-by="f.describedBy" placeholder="کاشان، تبریز…" />
        </Field>
        <Field label="مواد مصرفی" :error="fieldErrors['rug.material']" v-slot="f">
          <Combobox :id="f.id" v-model="form.material" :options="suggestions['rug.material']" allow-create :described-by="f.describedBy" placeholder="پشم، ابریشم…" />
        </Field>
        <Field label="رجشمار" :error="fieldErrors['rug.knotDensity']" v-slot="f">
          <NumberField :id="f.id" :ref="track" v-model="form.knotDensity" kind="integer" :described-by="f.describedBy" label="رجشمار" />
        </Field>
      </div>
    </section>

    <!-- اندازه -->
    <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <h2 class="mb-1 text-sm font-semibold text-primary">اندازهٔ اول (متر)</h2>
      <p class="mb-4 text-xs text-on-surface-variant">به متر بنویسید، مثل ۲٫۰۸؛ «۲۰۸×۲۱۰» هر دو خانه را با هم پر می‌کند. برای دسته اندازهٔ یک تخته.</p>
      <div class="grid gap-4 sm:grid-cols-3">
        <Field label="طول" :error="fieldErrors['rug.intakeLength']" required v-slot="f">
          <NumberField
            :id="f.id"
            ref="lengthField"
            v-model="form.intakeLength"
            kind="dim"
            label="طول"
            required
            :described-by="f.describedBy"
            :invalid="f.invalid"
            @pair="onPair"
            @update:model-value="delete fieldErrors['rug.intakeLength']"
          />
        </Field>
        <Field label="عرض" :error="fieldErrors['rug.intakeWidth']" required v-slot="f">
          <NumberField
            :id="f.id"
            ref="widthField"
            v-model="form.intakeWidth"
            kind="dim"
            label="عرض"
            required
            :described-by="f.describedBy"
            :invalid="f.invalid"
            @pair="onPair"
            @update:model-value="delete fieldErrors['rug.intakeWidth']"
          />
        </Field>
        <div class="block">
          <span class="mb-1 block text-sm font-medium">متراژ</span>
          <div class="min-h-11 rounded-lg border border-dashed border-outline-variant bg-surface-container px-3 py-2.5 text-end font-semibold" data-numeric>
            <bdi>{{ areaText }}</bdi>
          </div>
          <p v-if="largeArea && piece" class="mt-1 flex items-start gap-1 text-xs text-warning" role="status">
            <svg class="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" focusable="false"><use :href="'/icons.svg#warning'" /></svg>
            {{ message('W_LARGE_AREA', { area: faDecimal(toNumber(piece), 2) }) }}
          </p>
        </div>
      </div>

      <!-- اندازهٔ نهایی (فقط ویرایش؛ هنگام نگله‌بندی هم ثبت می‌شود) -->
      <div v-if="isEdit" class="mt-5 border-t border-outline-variant pt-4">
        <h3 class="mb-3 text-sm font-semibold text-on-surface-variant">اندازهٔ نهایی (اختیاری)</h3>
        <div class="grid gap-4 sm:grid-cols-3">
          <Field label="طول نهایی" :error="fieldErrors['rug.finalLength']" v-slot="f">
            <NumberField :id="f.id" :ref="track" v-model="form.finalLength" kind="dim" label="طول نهایی" :described-by="f.describedBy" :invalid="f.invalid" @pair="onFinalPair" />
          </Field>
          <Field label="عرض نهایی" :error="fieldErrors['rug.finalWidth']" v-slot="f">
            <NumberField :id="f.id" :ref="track" v-model="form.finalWidth" kind="dim" label="عرض نهایی" :described-by="f.describedBy" :invalid="f.invalid" @pair="onFinalPair" />
          </Field>
          <div class="flex items-end gap-2">
            <Button icon="copy" @click="finalSameAsIntake">همان اندازهٔ اول</Button>
            <Button v-if="form.finalLength !== null || form.finalWidth !== null" variant="ghost" @click="form.finalLength = null; form.finalWidth = null">
              پاک کردن
            </Button>
          </div>
        </div>
      </div>
    </section>

    <!-- مبلغ‌ها (فقط بازرگان و مدیر) -->
    <section v-if="canSeeMoney && !isEdit && purchaseCells.length" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">قیمت خرید</h2>
      <div class="space-y-4">
        <div v-for="(cell, index) in purchaseCells" :key="cell.key" class="grid gap-2 sm:grid-cols-3 sm:items-start">
          <span class="text-sm font-medium sm:pt-3">{{ cell.category.name }}</span>
          <div class="space-y-2 sm:col-span-2">
            <div v-if="cell.basis !== CostBasis.Percent" class="inline-flex rounded-lg border border-outline-variant p-0.5" role="radiogroup" :aria-label="`واحد ${cell.category.name}`">
              <button
                v-for="unit in UNIT_ORDER"
                :key="unit"
                type="button"
                role="radio"
                :aria-checked="cell.basis === unit"
                class="inline-flex min-h-10 items-center gap-1 rounded-md px-3 text-sm"
                :class="cell.basis === unit ? 'bg-primary text-on-primary font-semibold' : 'text-on-surface-variant hover:bg-surface-container'"
                @click="cell.basis = unit"
              >
                {{ UNIT_LABELS[unit] }}
              </button>
            </div>
            <NumberField
              :ref="track"
              v-model="cell.rate"
              :kind="cell.basis === CostBasis.Percent ? 'percent' : 'money'"
              :scale="cell.basis === CostBasis.Total ? appContext().moneyScale : 4"
              :aria-label="cell.category.name"
              :invalid="!!fieldErrors[cell.key]"
            />
            <p v-if="fieldErrors[cell.key]" class="text-xs text-error">{{ fieldErrors[cell.key] }}</p>
            <p v-else-if="purchasePreview && cell.rate !== null" class="text-xs text-on-surface-variant" data-numeric>
              مبلغ: {{ faMoney(purchasePreview[index]) }}
            </p>
          </div>
        </div>
      </div>
      <p class="mt-3 text-xs text-on-surface-variant">سطرهای دیگر خرید را بعد از ثبت در صفحهٔ فرش اضافه کنید.</p>
    </section>

    <div v-if="canSeeMoney && isEdit && rugId" @keydown="onPurchaseEditorKeydown">
      <PurchaseLinesEditor
        :rug-id="rugId"
        :version="version"
        :intake-area-m2="piece ? toNumber(piece) : null"
        :quantity="quantity"
        @saved="onPurchaseSaved"
      />
    </div>

    <section v-if="canSeeMoney" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <div class="grid gap-4 sm:grid-cols-3">
        <Field label="قیمت فروش هدف" :error="fieldErrors['rug.targetSalePrice']" v-slot="f">
          <NumberField :id="f.id" :ref="track" v-model="form.targetSalePrice" kind="money" label="قیمت فروش هدف" :described-by="f.describedBy" :invalid="f.invalid" />
        </Field>
      </div>
    </section>

    <!-- فیلدهای سفارشی کارگاه -->
    <section v-if="customFields.length" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">مشخصات بیشتر</h2>
      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Field v-for="f in customFields" :key="f.id" :label="f.label" :required="f.isRequired" :error="fieldErrors['rug.custom.' + f.key]" v-slot="slot">
          <select v-if="f.fieldType === 3" :id="slot.id" v-model="metadata[f.key]" class="input" :aria-describedby="slot.describedBy">
            <option value="">—</option>
            <option v-for="o in optionsOf(f)" :key="o" :value="o">{{ o }}</option>
          </select>
          <label v-else-if="f.fieldType === 4" class="flex min-h-11 items-center gap-2">
            <input :id="slot.id" type="checkbox" :checked="metadata[f.key] === 'true'"
                   @change="metadata[f.key] = ($event.target as HTMLInputElement).checked ? 'true' : 'false'"
                   class="h-5 w-5 rounded border-outline-variant text-primary" />
            <span class="text-sm text-on-surface-variant">بله</span>
          </label>
          <JalaliDateField
            v-else-if="f.fieldType === 2"
            :id="slot.id"
            :model-value="metadata[f.key] || null"
            :described-by="slot.describedBy"
            :required="f.isRequired"
            @update:model-value="metadata[f.key] = $event ?? ''"
          />
          <NumberField
            v-else-if="f.fieldType === 1"
            :id="slot.id"
            :model-value="tryDec(metadata[f.key]) ? Number(metadata[f.key]) : null"
            kind="number"
            :scale="4"
            allow-negative
            :label="f.label"
            :described-by="slot.describedBy"
            @update:model-value="setMetaNumber(f.key, $event)"
          />
          <input v-else :id="slot.id" v-model="metadata[f.key]" class="input" :aria-describedby="slot.describedBy" />
        </Field>
      </div>
    </section>

    <!-- مسیر گردش کار (فقط هنگام ثبت) -->
    <section v-if="!isEdit" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <h2 class="mb-4 text-sm font-semibold text-primary">مسیر گردش کار</h2>
      <select v-model="form.workflowTemplateId" class="input max-w-md" aria-label="مسیر گردش کار">
        <option value="">بدون مسیر (بعداً تعریف می‌شود)</option>
        <option v-for="t in templates" :key="t.id" :value="t.id">{{ t.name }}</option>
      </select>
      <p class="mt-2 text-xs text-on-surface-variant">مسیر انتخابی برای این فرش کپی می‌شود و بعداً قابل ویرایش است.</p>
    </section>

    <!-- یادداشت -->
    <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
      <label class="block">
        <span class="mb-1 block text-sm font-semibold text-primary">یادداشت</span>
        <textarea v-model="form.notes" rows="3" class="input"></textarea>
      </label>
      <p v-if="fieldErrors['rug.notes']" class="mt-1 text-xs text-error">{{ fieldErrors['rug.notes'] }}</p>
    </section>

    <!-- اکشن‌ها (چسبان پایین در موبایل) -->
    <div class="sticky bottom-0 -mx-4 flex gap-3 border-t border-outline-variant bg-surface/95 px-4 py-3 backdrop-blur md:static md:mx-0 md:border-0 md:bg-transparent md:px-0 md:py-0">
      <a :href="isEdit ? `/Rugs/Details/${rugId}` : '/Rugs'" class="flex min-h-11 flex-1 items-center justify-center rounded-lg border border-outline-variant px-4 text-center hover:bg-surface-container md:flex-none">انصراف</a>
      <button type="submit" :disabled="saving"
              class="min-h-11 flex-1 rounded-lg bg-primary px-6 font-semibold text-on-primary hover:bg-primary-hover disabled:opacity-60 md:flex-none">
        {{ saving ? message('I_SAVING') : isEdit ? 'ذخیرهٔ تغییرات' : 'ثبت فرش' }}
      </button>
    </div>
  </form>
</template>

<style scoped>
.input {
  min-height: 2.75rem;
  width: 100%;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-lowest);
  color: var(--color-on-surface);
  padding: 0.625rem 0.75rem;
  outline: none;
}
.input:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 1px var(--color-primary);
}
</style>
