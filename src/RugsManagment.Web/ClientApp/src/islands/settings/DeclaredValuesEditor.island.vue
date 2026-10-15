<script setup lang="ts">
/**
 * جدول ارزش گمرکی (`/settings/declared-values`، فقط تنظیم‌کننده): برای هر مقدار فیلد کلید (پیش‌فرض محل بافت) دلار هر
 * متر. ردیف محموله‌های باز بی نرخ دستی از همین جدول قیمت می‌گیرد (C.5)؛ نرخ پیش‌فرض و خودِ فیلد کلید در «ترجیحات»اند.
 *
 * داده از `GET /api/declared-values` می‌آید (جدول و مهر هم‌زمانی) و با `PUT /api/settings/declared-values` یکجا
 * جایگزین می‌شود، همراه همان مهر: اگر کس دیگری در این فاصله ذخیره کرده باشد (یا بازرگان از صفحهٔ نگله‌ها کلید تازه‌ای
 * افزوده باشد) سرور ۴۰۹ می‌دهد و جدول پیشنهاد بارگذاری دوباره می‌دهد، نه بازنویسی بی‌صدا.
 *
 * پیش از فرستادن: کلید خالی، کلید تکراری پس از یکسان‌سازی («کاشان» = «كاشان»، `declaredValueKeys.ts`) و دلار خالی یا
 * منفی همان‌جا زیر خانه گفته می‌شود. مقدارهایی که در فرش‌ها هست ولی نرخ ندارد (از `/api/rugs/values`) چیپ «+» دارند تا
 * با یک لمس ردیفشان ساخته شود.
 *
 * متن‌های دارای واژهٔ اظهار صادراتی از `lib/messages/shipments.ts` می‌آیند (واژه‌نامهٔ A.2: صفحه‌های تنظیمات آن را
 * مستقیم نمی‌نویسند).
 */
import { computed, nextTick, onMounted, reactive, ref } from 'vue'
import { ApiError, api, errorMessage } from '@/lib/api'
import { faDecimal } from '@/lib/format'
import { newId } from '@/lib/ids'
import { coreMessages, message } from '@/lib/messages'
import { confirmDialog, toast } from '@/lib/ui'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import NumberField from '@/components/ui/NumberField.vue'
import type {
  DeclaredValueRate,
  DeclaredValues,
  DeclaredValuesWriteResult,
  ReplaceDeclaredValuesRequest,
} from '@/lib/types/shipments'
import { duplicateKeyRows, keyNormalize, missingKeys } from './declaredValueKeys'

const props = withDefaults(
  defineProps<{
    /** کلید فیلدی که نرخ با مقدارش پیدا می‌شود (مثل `rug.origin`) */
    keyField?: string
    /** برچسب فارسی همان فیلد (مثل «محل بافت») */
    keyFieldLabel: string
    /** نرخ پیش‌فرض کارگاه؛ null یعنی ندارد */
    fallbackUsd?: number | null
  }>(),
  { keyField: 'rug.origin', fallbackUsd: null },
)

const READ_ENDPOINT = '/api/declared-values'
const WRITE_ENDPOINT = '/api/settings/declared-values'
/** چند مقدار پرتکرار فیلد کلید برای چیپ‌های «بی‌نرخ» */
const IN_USE_TAKE = 50
/** از این تعداد ردیف به بعد جعبهٔ جست‌وجو دیده می‌شود */
const FILTER_FROM = 10

interface Row {
  /** کلید پایدار v-for (ردیف تازه هم دارد) */
  uid: string
  id: string | null
  keyValue: string
  usdPerM2: number | null
}

const loading = ref(true)
const loadError = ref('')
const saving = ref(false)
const formError = ref('')
/** ۴۰۹ مهر کهنه: فقط بارگذاری دوباره */
const stale = ref(false)

const rows = ref<Row[]>([])
const stamp = ref(0)
/** آن‌چه آخرین بار از سرور آمد (بی uid)، برای «تغییری داده‌اید؟» */
const savedJson = ref('')
const inUse = ref<string[]>([])
const filter = ref('')

/** خطای هر ردیف که از سرور آمد یا پیش از فرستادن پیدا شد: uid → {key?, usd?} */
const rowErrors = reactive<Record<string, { key?: string; usd?: string }>>({})

const label = computed(() => props.keyFieldLabel || 'مقدار')

function snapshot(list: readonly Row[]): string {
  return JSON.stringify(list.map((r) => [r.id, r.keyValue.trim(), r.usdPerM2]))
}

const dirty = computed(() => !loading.value && snapshot(rows.value) !== savedJson.value)

const duplicates = computed(() => duplicateKeyRows(rows.value.map((r) => r.keyValue)))

const missing = computed(() => missingKeys(inUse.value, rows.value.map((r) => r.keyValue)))

const visibleRows = computed(() => {
  const needle = keyNormalize(filter.value)
  if (!needle) return rows.value
  return rows.value.filter((r) => !r.keyValue.trim() || keyNormalize(r.keyValue).includes(needle))
})

/** نرخ پیش‌فرض: از پاسخ جدول (تازه‌ترین)، وگرنه همان که صفحه داد. */
const fallback = ref<number | null>(props.fallbackUsd ?? null)

const fallbackText = computed(() =>
  fallback.value === null
    ? message('I_DECLARED_VALUES_NO_FALLBACK', { label: label.value })
    : message('I_DECLARED_VALUES_FALLBACK', { label: label.value, usd: faDecimal(fallback.value, 2) }),
)

function toRows(rates: readonly DeclaredValueRate[]): Row[] {
  return rates.map((r) => ({ uid: r.id ?? newId(), id: r.id, keyValue: r.keyValue, usdPerM2: r.usdPerM2 }))
}

function clearErrors() {
  formError.value = ''
  for (const key of Object.keys(rowErrors)) delete rowErrors[key]
}

function accept(data: DeclaredValues) {
  rows.value = toRows(data.rates)
  stamp.value = data.stamp
  if (data.fallbackUsdPerM2 !== undefined) fallback.value = data.fallbackUsdPerM2
  savedJson.value = snapshot(rows.value)
  stale.value = false
  clearErrors()
}

async function loadInUse() {
  try {
    const query = new URLSearchParams({ field: props.keyField, take: String(IN_USE_TAKE) })
    inUse.value = await api.get<string[]>(`/api/rugs/values?${query.toString()}`)
  } catch {
    // پیشنهادها فقط کمک‌اند؛ نبودنشان جدول را نمی‌شکند
    inUse.value = []
  }
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const [data] = await Promise.all([api.get<DeclaredValues>(READ_ENDPOINT), loadInUse()])
    accept(data)
  } catch (e) {
    loadError.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

// ── ویرایش ردیف‌ها ─────────────────────────────────────────────────────

const usdFields = new Map<string, { validate: () => string | null; focus: () => void }>()
function trackUsd(uid: string) {
  return (el: unknown) => {
    if (el && typeof (el as { validate?: unknown }).validate === 'function')
      usdFields.set(uid, el as { validate: () => string | null; focus: () => void })
    else usdFields.delete(uid)
  }
}

const keyInputs = new Map<string, HTMLInputElement>()
function trackKey(uid: string) {
  return (el: unknown) => {
    if (el instanceof HTMLInputElement) keyInputs.set(uid, el)
    else keyInputs.delete(uid)
  }
}

async function addRow(keyValue = '') {
  const row: Row = { uid: newId(), id: null, keyValue, usdPerM2: null }
  rows.value.push(row)
  filter.value = ''
  await nextTick()
  // ردیف با مقدار آماده (از چیپ) → مستقیم خانهٔ دلار؛ ردیف خالی → خانهٔ نام
  if (keyValue) usdFields.get(row.uid)?.focus()
  else keyInputs.get(row.uid)?.focus()
}

function removeRow(row: Row) {
  rows.value = rows.value.filter((r) => r.uid !== row.uid)
  delete rowErrors[row.uid]
}

async function revert() {
  if (!(await confirmDialog({ title: message('Q_DECLARED_VALUES_DISCARD'), confirmLabel: 'کنار بگذار', cancelLabel: 'نه' })))
    return
  const data = JSON.parse(savedJson.value) as [string | null, string, number | null][]
  rows.value = data.map(([id, keyValue, usdPerM2]) => ({ uid: id ?? newId(), id, keyValue, usdPerM2 }))
  clearErrors()
}

function keyError(row: Row, index: number): string | undefined {
  if (rowErrors[row.uid]?.key) return rowErrors[row.uid]?.key
  if (duplicates.value.has(index)) return message('E_DECLARED_KEY_DUPLICATE', { key: row.keyValue.trim() })
  return undefined
}

// ── ذخیره ──────────────────────────────────────────────────────────────

function validate(): boolean {
  clearErrors()
  let ok = true
  rows.value.forEach((row, index) => {
    const errors: { key?: string; usd?: string } = {}
    if (!row.keyValue.trim()) errors.key = message('E_REQUIRED', { label: label.value })
    else if (duplicates.value.has(index))
      errors.key = message('E_DECLARED_KEY_DUPLICATE', { key: row.keyValue.trim() })

    const usdProblem = usdFields.get(row.uid)?.validate() ?? null
    if (usdProblem) errors.usd = usdProblem
    else if (row.usdPerM2 === null) errors.usd = message('E_REQUIRED', { label: 'دلار هر متر' })

    if (errors.key || errors.usd) {
      rowErrors[row.uid] = errors
      ok = false
    }
  })
  return ok
}

/** خطای ۴۰۰ سرور با کلیدهایی مثل «rates[2].usdPerM2» یا «rates[2].keyValue» → زیر همان ردیف. */
function applyServerErrors(errors: Readonly<Record<string, string[]>>): boolean {
  let placed = false
  for (const [key, list] of Object.entries(errors)) {
    const match = /rates\[(\d+)\]\.?(\w+)?/i.exec(key)
    const row = match ? rows.value[Number(match[1])] : undefined
    if (!row || !list[0]) continue
    const field = (match?.[2] ?? '').toLowerCase()
    rowErrors[row.uid] = { ...rowErrors[row.uid], ...(field.startsWith('usd') ? { usd: list[0] } : { key: list[0] }) }
    placed = true
  }
  return placed
}

async function save() {
  if (saving.value || stale.value) return
  if (!validate()) {
    // ردیفِ خطادار ممکن است زیر جست‌وجو پنهان باشد؛ همه را نشان بده تا پیام زیر خانه دیده شود
    filter.value = ''
    formError.value = coreMessages.E_VALIDATION
    return
  }

  const body: ReplaceDeclaredValuesRequest = {
    rates: rows.value.map((r) => ({ id: r.id, keyValue: r.keyValue.trim(), usdPerM2: r.usdPerM2 ?? 0 })),
    stamp: stamp.value,
  }

  saving.value = true
  try {
    const result = await api.put<DeclaredValuesWriteResult>(WRITE_ENDPOINT, body)
    // ذخیره شد: مهر تازه همین حالا، تا اگر خواندن دوباره نشد ذخیرهٔ بعدی ۴۰۹ بی‌جا نگیرد
    if (result && typeof result.stamp === 'number') stamp.value = result.stamp
    savedJson.value = snapshot(rows.value)
    clearErrors()
    toast.success(
      result && result.affectedOpenLines > 0
        ? message('I_DECLARED_VALUES_SAVED', { n: result.affectedOpenLines })
        : message('I_DECLARED_VALUES_SAVED_NONE'),
    )
    try {
      // شناسهٔ ردیف‌های تازه و شکل یکسان‌شدهٔ کلیدها از سرور
      accept(await api.get<DeclaredValues>(READ_ENDPOINT))
    } catch {
      /* جدول ذخیره شده؛ بارگذاری بعدی همان را نشان می‌دهد */
    }
  } catch (e) {
    if (e instanceof ApiError && e.status === 409) {
      stale.value = true
      formError.value = e.message || message('E_DECLARED_STALE')
    } else if (e instanceof ApiError && Object.keys(e.errors).length > 0) {
      applyServerErrors(e.errors)
      formError.value = e.message || coreMessages.E_VALIDATION
    } else {
      formError.value = errorMessage(e)
    }
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <div v-if="loading" class="skeleton h-80 w-full" aria-hidden="true"></div>

    <EmptyState v-else-if="loadError" icon="warning" title="جدول ارزش گمرکی بارگذاری نشد" :text="loadError">
      <template #actions>
        <Button variant="primary" icon="undo" @click="load">دوباره امتحان کن</Button>
      </template>
    </EmptyState>

    <form v-else class="space-y-5" novalidate @submit.prevent="save">
      <div
        v-if="formError"
        class="flex flex-wrap items-center gap-3 rounded-lg bg-error-container px-4 py-3 text-sm text-on-error-container"
        role="alert"
      >
        <span class="flex-1">{{ formError }}</span>
        <Button v-if="stale" size="md" icon="undo" @click="load">بارگذاری دوباره</Button>
      </div>

      <section class="space-y-4 rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm">
        <p class="text-sm text-on-surface-variant">{{ message('I_DECLARED_VALUES_HINT', { label }) }}</p>

        <p class="flex items-start gap-2 rounded-lg bg-surface-container px-3 py-2 text-sm" data-numeric>
          <AppIcon name="info" class="mt-0.5 h-4 w-4 shrink-0 text-on-surface-variant" />
          <span class="flex-1">{{ fallbackText }}</span>
          <!-- هدف لمس ۴۴ پیکسل بی آنکه نوار راهنما بلندتر شود (حاشیهٔ منفی) -->
          <a
            href="/settings/preferences"
            class="-my-2 inline-flex min-h-11 shrink-0 items-center rounded-lg px-2 font-medium text-primary underline-offset-2 hover:underline"
          >ترجیحات</a>
        </p>

        <!-- مقدارهای بی‌نرخ: یک لمس، یک ردیف -->
        <div v-if="missing.length > 0" class="space-y-2">
          <p class="text-sm font-medium">{{ message('I_DECLARED_VALUES_MISSING') }}</p>
          <ul class="flex flex-wrap gap-2">
            <li v-for="value in missing" :key="value">
              <button
                type="button"
                class="inline-flex min-h-11 items-center gap-1.5 rounded-full border border-outline-variant px-4 text-sm hover:border-primary hover:bg-primary/5"
                :aria-label="`ردیف تازه برای «${value}»`"
                @click="addRow(value)"
              >
                <AppIcon name="plus" class="h-4 w-4" />
                {{ value }}
              </button>
            </li>
          </ul>
        </div>

        <label v-if="rows.length >= FILTER_FROM" class="relative block">
          <span class="sr-only">جست‌وجو در جدول</span>
          <AppIcon name="search" class="pointer-events-none absolute inset-y-0 start-3 my-auto h-4 w-4 text-on-surface-variant" />
          <input v-model="filter" type="search" :placeholder="`جست‌وجوی ${label}`" class="fld ps-9" />
        </label>

        <p v-if="rows.length === 0" class="rounded-lg border border-dashed border-outline-variant p-6 text-center text-on-surface-variant">
          {{ message('I_DECLARED_VALUES_EMPTY', { label }) }}
        </p>

        <div v-else>
          <!-- سرستون‌ها فقط روی صفحهٔ پهن؛ روی گوشی هر خانه برچسب خودش را دارد -->
          <div class="mb-2 hidden gap-3 px-1 text-sm font-medium text-on-surface-variant sm:grid sm:grid-cols-[minmax(0,1fr)_13rem_2.75rem]" aria-hidden="true">
            <span>{{ label }}</span>
            <span>دلار هر متر</span>
            <span></span>
          </div>

          <ul class="space-y-3 sm:space-y-2" :aria-label="`نرخ هر ${label}`">
            <li
              v-for="row in visibleRows"
              :key="row.uid"
              class="grid grid-cols-[minmax(0,1fr)_2.75rem] items-start gap-x-3 gap-y-2 rounded-lg border border-outline-variant p-3 sm:grid-cols-[minmax(0,1fr)_13rem_2.75rem] sm:border-0 sm:p-0"
            >
              <div class="min-w-0">
                <label :for="`dv-key-${row.uid}`" class="mb-1 block text-xs text-on-surface-variant sm:sr-only">{{ label }}</label>
                <input
                  :id="`dv-key-${row.uid}`"
                  :ref="trackKey(row.uid)"
                  v-model="row.keyValue"
                  maxlength="120"
                  autocomplete="off"
                  class="fld"
                  :aria-invalid="!!keyError(row, rows.indexOf(row)) || undefined"
                  :aria-describedby="keyError(row, rows.indexOf(row)) ? `dv-key-${row.uid}-error` : undefined"
                />
                <p v-if="keyError(row, rows.indexOf(row))" :id="`dv-key-${row.uid}-error`" class="mt-1 text-xs text-error">
                  {{ keyError(row, rows.indexOf(row)) }}
                </p>
              </div>

              <div class="col-start-1 row-start-2 sm:col-start-auto sm:row-start-auto">
                <label :for="`dv-usd-${row.uid}`" class="mb-1 block text-xs text-on-surface-variant sm:sr-only">دلار هر متر</label>
                <NumberField
                  :id="`dv-usd-${row.uid}`"
                  :ref="trackUsd(row.uid)"
                  v-model="row.usdPerM2"
                  kind="usd"
                  :min="0"
                  label="دلار هر متر"
                  :invalid="!!rowErrors[row.uid]?.usd"
                  :described-by="rowErrors[row.uid]?.usd ? `dv-usd-${row.uid}-error` : undefined"
                />
                <p v-if="rowErrors[row.uid]?.usd" :id="`dv-usd-${row.uid}-error`" class="mt-1 text-xs text-error">
                  {{ rowErrors[row.uid]?.usd }}
                </p>
              </div>

              <button
                type="button"
                class="row-span-2 grid h-11 w-11 place-items-center self-start rounded-lg text-on-surface-variant hover:bg-error-container hover:text-on-error-container sm:row-span-1 sm:mt-0"
                :aria-label="`حذف ${row.keyValue.trim() || 'این ردیف'}`"
                @click="removeRow(row)"
              >
                <AppIcon name="trash" class="h-5 w-5" />
              </button>
            </li>
          </ul>
        </div>

        <Button icon="plus" @click="addRow()">ردیف تازه</Button>
      </section>

      <!-- ذخیره (چسبان پایین) -->
      <div
        class="sticky bottom-0 z-10 -mx-4 flex flex-wrap items-center gap-3 border-t border-outline-variant bg-surface/95 px-4 py-3 backdrop-blur md:static md:mx-0 md:border-0 md:bg-transparent md:px-0 md:py-0"
      >
        <span v-if="dirty" class="flex-1 text-sm text-on-surface-variant" role="status">تغییرها هنوز ذخیره نشده‌اند.</span>
        <span v-else class="flex-1"></span>
        <Button :disabled="!dirty || saving" icon="undo" @click="revert">برگرداندن</Button>
        <Button type="submit" variant="primary" icon="check" :loading="saving" :disabled="!dirty || stale">ذخیره</Button>
      </div>
    </form>
  </div>
</template>

<style scoped>
.fld {
  min-height: 2.75rem;
  width: 100%;
  border-radius: 0.5rem;
  border: 1px solid var(--color-outline-variant);
  background: var(--color-surface-container-lowest);
  color: var(--color-on-surface);
  padding: 0 0.75rem;
  outline: none;
}
.fld:focus {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 1px var(--color-primary);
}
.fld[aria-invalid='true'] {
  border-color: var(--color-error);
}
</style>
