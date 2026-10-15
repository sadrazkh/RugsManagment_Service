<script setup lang="ts">
/**
 * ویرایشگر سطرهای قیمت خرید یک فرش (B.6) — فقط Business. صفحهٔ جزئیات فرش (جزیرهٔ `purchase-lines-editor`) و فرم
 * فرش هر دو همین را دارند.
 *
 * هر سطر: نوع (قیمت خرید، دلالی، کرایه تا انبار، … یا سطر آزاد)، مبنا («کل» | «هر متر» | «هر تخته»، یا «درصد»)، نرخ
 * و مبلغ. مبلغ هنگام تایپ با همان قاعدهٔ سرور پیش‌نمایش می‌شود (`purchasePreview.ts`)؛ بعد از ذخیره عددی که خودِ سرور
 * (پاس سازگاری) نوشته نشان داده می‌شود. «هر متر» همیشه روی اندازهٔ اول است.
 *
 * ذخیره همهٔ سطرها را یک‌جا جایگزین می‌کند و نسخهٔ فرش را می‌فرستد؛ اگر کس دیگری در این فاصله فرش را عوض کرده باشد
 * ۴۰۹ می‌آید و ویرایشگر پیشنهاد «دوباره بخوان» می‌دهد (هیچ چیز بی‌صدا رونویسی نمی‌شود). نسخهٔ تازه با رویداد
 * `rugs:rug-changed` به جزیره‌های دیگر همین فرش می‌رسد.
 */
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { ApiError, api, errorMessage } from '@/lib/api'
import { appContext } from '@/lib/context'
import { faDecimal, faMoney, toPersianDigits } from '@/lib/format'
import { message } from '@/lib/messages'
import { toNumber } from '@/lib/num'
import { blockReload, confirmDialog } from '@/lib/ui'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import NumberField from '@/components/ui/NumberField.vue'
import {
  CostBasis,
  type PurchaseCategoryOption,
  type PurchaseLine,
  type PurchaseLinesResult,
  type RugCostSummary,
} from '@/lib/types/rugs'
import { announceRugChanged, onRugChanged } from '@/islands/rugs/rugEvents'
import { pieceAmount, purchaseAmounts } from '@/islands/rugs/purchasePreview'

const props = withDefaults(
  defineProps<{
    rugId: string
    version: number
    readOnly?: boolean
    /** مساحت یک تخته در اندازهٔ اول (پیش‌نمایش «هر متر»)؛ نبودش یعنی پیش‌نمایش فقط بعد از ذخیره */
    intakeAreaM2?: number | null
    quantity?: number
  }>(),
  { readOnly: false, intakeAreaM2: null, quantity: 1 },
)

const emit = defineEmits<{
  /**
   * ذخیرهٔ موفق با پاسخ سرور (سطرها، جمع، نسخهٔ تازه، خلاصهٔ بها) و نسخه‌ای که ذخیره رویش فرستاده شد. والدی که خودش فرم
   * همین فرش را دارد (RugForm) نسخهٔ تازه را فقط وقتی برمی‌دارد که این نسخهٔ پایه همان نسخهٔ فرم باشد؛ وگرنه ویرایشگر
   * تغییر کس دیگری را (با «دوباره بخوان») خوانده و فرمِ ساخته‌شده از دادهٔ کهنه باید ۴۰۹ بگیرد، نه رونویسی بی‌صدا.
   */
  saved: [result: PurchaseLinesResult, baseVersion: number]
}>()

const SOURCE = 'purchase-lines-editor'

interface Row {
  key: number
  id: string | null
  categoryId: string | null
  label: string
  basis: CostBasis
  rate: number | null
  /** مبلغی که سرور نوشته (بعد از ذخیره)؛ برای ردیف تازه null */
  savedAmount: number | null
  notes: string | null
}

const loading = ref(true)
const loadError = ref('')
const saving = ref(false)
const formError = ref('')
const stale = ref(false)
const dirty = ref(false)
const rows = ref<Row[]>([])
const categories = ref<PurchaseCategoryOption[]>([])
const rowErrors = reactive<Record<string, string>>({})
const version = ref(props.version)
const savedTotal = ref<number | null>(null)
const costs = ref<RugCostSummary | null>(null)
const addOpen = ref(false)
let nextKey = 1
let releaseReload: (() => void) | null = null

const moneyScale = computed(() => appContext().moneyScale)
const canPreview = computed(() => props.intakeAreaM2 != null && props.intakeAreaM2 > 0)

const BASIS_LABELS: Record<number, string> = {
  [CostBasis.Total]: 'کل',
  [CostBasis.PerM2]: 'هر متر',
  [CostBasis.PerPiece]: 'هر تخته',
  [CostBasis.Percent]: 'درصد',
}
const MONEY_BASES = [CostBasis.PerM2, CostBasis.PerPiece, CostBasis.Total] as const

const preview = computed(() =>
  canPreview.value
    ? purchaseAmounts(
        rows.value.map((r) => ({ basis: r.basis, rate: r.rate })),
        props.intakeAreaM2 ?? 0,
        props.quantity,
        moneyScale.value,
      ).map((d) => toNumber(d))
    : null,
)

const previewTotal = computed(() => (preview.value ? preview.value.reduce((sum, v) => sum + v, 0) : null))

function categoryOf(row: Row): PurchaseCategoryOption | undefined {
  return categories.value.find((c) => c.id === row.categoryId)
}

function isPercentRow(row: Row): boolean {
  return row.basis === CostBasis.Percent
}

/** «۳۰۰٬۰۰۰ × ۵۰ = ۱۵٬۰۰۰٬۰۰۰» برای هر متر/هر تخته روی دسته؛ وگرنه فقط مبلغ. */
function amountText(row: Row, index: number): string {
  const amount = dirty.value ? preview.value?.[index] ?? null : row.savedAmount
  if (amount === null || amount === undefined) return '—'
  if (props.quantity > 1 && row.rate !== null && canPreview.value) {
    const piece = pieceAmount(row.basis, row.rate, props.intakeAreaM2 ?? 0, moneyScale.value)
    if (piece) return `${faDecimal(toNumber(piece), moneyScale.value)} × ${toPersianDigits(props.quantity)} = ${faMoney(amount)}`
  }
  return faMoney(amount)
}

function toRow(line: PurchaseLine): Row {
  return {
    key: nextKey++,
    id: line.id,
    categoryId: line.categoryId,
    label: line.label,
    basis: line.basis,
    rate: line.rate,
    savedAmount: line.amount,
    notes: line.notes,
  }
}

function apply(result: PurchaseLinesResult) {
  rows.value = result.lines.map(toRow)
  version.value = result.version
  savedTotal.value = result.purchaseCost
  costs.value = result.costs
  markClean()
}

function markDirty() {
  dirty.value = true
  if (!releaseReload) releaseReload = blockReload('purchase-lines')
}

/**
 * نرخ سطر از خانهٔ عدد. فقط تغییر واقعی ویرایشگر را «تغییر کرده» می‌کند: فوکوس و خروجِ بی‌تغییر نباید تازه‌سازی صفحه را
 * قفل کند، «Leave site?» بیاورد و ذخیرهٔ جزیره‌های دیگر را پشت «بعد از ذخیرهٔ کارتان» نگه دارد.
 */
function setRate(row: Row, value: number | null) {
  if (row.rate === value) return
  row.rate = value
  markDirty()
}

function markClean() {
  dirty.value = false
  releaseReload?.()
  releaseReload = null
}

async function load() {
  loading.value = true
  loadError.value = ''
  stale.value = false
  try {
    const [result, options] = await Promise.all([
      api.get<PurchaseLinesResult>(`/api/rugs/${props.rugId}/purchase-lines`),
      api.get<PurchaseCategoryOption[]>('/api/rugs/purchase-categories'),
    ])
    categories.value = options
    apply(result)
  } catch (e) {
    loadError.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

function addRow(category: PurchaseCategoryOption | null) {
  addOpen.value = false
  rows.value.push({
    key: nextKey++,
    id: null,
    categoryId: category?.id ?? null,
    label: category?.name ?? '',
    basis: category?.defaultBasis ?? CostBasis.Total,
    rate: category?.defaultRate ?? null,
    savedAmount: null,
    notes: null,
  })
  markDirty()
}

async function removeRow(index: number) {
  const row = rows.value[index]
  if (row.id !== null && row.rate !== null) {
    const ok = await confirmDialog({
      title: `سطر «${row.label || 'بی‌نام'}» حذف شود؟`,
      message: 'با «ذخیره» از قیمت خرید این فرش برداشته می‌شود.',
      confirmLabel: 'حذف کن',
      cancelLabel: 'نه',
      danger: true,
    })
    if (!ok) return
  }
  rows.value.splice(index, 1)
  markDirty()
}

function setBasis(row: Row, basis: CostBasis) {
  if (row.basis === basis) return
  row.basis = basis
  markDirty()
}

function clearErrors() {
  formError.value = ''
  for (const key of Object.keys(rowErrors)) delete rowErrors[key]
}

async function save() {
  clearErrors()
  saving.value = true
  const baseVersion = version.value
  try {
    const result = await api.put<PurchaseLinesResult>(`/api/rugs/${props.rugId}/purchase-lines`, {
      version: baseVersion,
      lines: rows.value.map((row, index) => ({
        id: row.id,
        categoryId: row.categoryId,
        label: row.label.trim(),
        basis: row.basis,
        rate: row.rate ?? 0,
        amount: 0,
        sortOrder: (index + 1) * 10,
        notes: row.notes,
      })),
    })
    apply(result)
    announceRugChanged({ rugId: props.rugId, version: result.version, source: SOURCE })
    emit('saved', result, baseVersion)
  } catch (e) {
    formError.value = errorMessage(e)
    if (e instanceof ApiError) {
      if (e.status === 409) stale.value = true
      for (const [key, list] of Object.entries(e.errors)) if (list.length) rowErrors[key] = list[0]
    }
  } finally {
    saving.value = false
  }
}

function rowError(index: number, field: string): string | undefined {
  return rowErrors[`lines[${index}].${field}`]
}

let stopListening: (() => void) | null = null
onMounted(() => {
  void load()
  // ذخیرهٔ جزیرهٔ دیگری روی همین فرش (اندازهٔ نهایی، فرم): فقط نسخه جلو می‌رود؛ سطرها همان‌اند
  stopListening = onRugChanged(props.rugId, SOURCE, (detail) => {
    version.value = detail.version
  })
})
onUnmounted(() => {
  stopListening?.()
  releaseReload?.()
})
</script>

<template>
  <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm" aria-labelledby="purchase-lines-title">
    <div class="mb-3 flex items-center justify-between gap-2">
      <h3 id="purchase-lines-title" class="flex items-center gap-2 text-sm font-semibold text-primary">
        <AppIcon name="receipt" class="h-4 w-4" />
        قیمت خرید
      </h3>
      <span v-if="savedTotal !== null && !dirty" class="text-sm font-semibold" data-numeric>{{ faMoney(savedTotal) }}</span>
      <span v-else-if="dirty && previewTotal !== null" class="text-sm text-on-surface-variant" data-numeric>
        {{ faMoney(previewTotal) }}
      </span>
    </div>

    <p v-if="loading" class="text-sm text-on-surface-variant">در حال بارگذاری…</p>
    <div v-else-if="loadError" class="rounded-lg bg-error-container px-3 py-2 text-sm text-on-error-container" role="alert">
      {{ loadError }}
      <Button class="mt-2" icon="undo" @click="load">دوباره</Button>
    </div>

    <template v-else>
      <p v-if="rows.length === 0" class="mb-3 text-sm text-on-surface-variant">
        هنوز قیمت خریدی برای این فرش ثبت نشده.
      </p>

      <ul class="space-y-3">
        <li
          v-for="(row, index) in rows"
          :key="row.key"
          class="rounded-lg border border-outline-variant p-3"
        >
          <div class="flex flex-wrap items-start gap-2">
            <div class="min-w-36 flex-1">
              <label :for="`pl-label-${row.key}`" class="sr-only">نام سطر</label>
              <input
                v-if="!categoryOf(row)"
                :id="`pl-label-${row.key}`"
                v-model="row.label"
                :readonly="readOnly"
                class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-sm outline-none focus:border-primary"
                placeholder="نام سطر، مثل «کرایه»"
                :aria-invalid="!!rowError(index, 'label')"
                @input="markDirty"
              />
              <p v-else :id="`pl-label-${row.key}`" class="flex min-h-11 items-center text-sm font-medium">{{ row.label }}</p>
              <p v-if="rowError(index, 'label')" class="mt-1 text-xs text-error">{{ rowError(index, 'label') }}</p>
            </div>

            <button
              v-if="!readOnly"
              type="button"
              class="grid h-11 w-11 shrink-0 place-items-center rounded-lg text-error hover:bg-error-container/40"
              @click="removeRow(index)"
            >
              <AppIcon name="trash" :label="`حذف سطر ${row.label || ''}`" />
            </button>
          </div>

          <div class="mt-2 flex flex-wrap items-start gap-2">
            <!-- مبنا: کل | هر متر | هر تخته (درصد برای نوع درصدی ثابت است) -->
            <div
              v-if="!isPercentRow(row)"
              class="inline-flex rounded-lg border border-outline-variant p-0.5"
              role="radiogroup"
              :aria-label="`مبنای ${row.label || 'سطر'}`"
            >
              <button
                v-for="basis in MONEY_BASES"
                :key="basis"
                type="button"
                role="radio"
                :aria-checked="row.basis === basis"
                :disabled="readOnly"
                class="inline-flex min-h-10 items-center gap-1 rounded-md px-3 text-sm"
                :class="row.basis === basis ? 'bg-primary text-on-primary font-semibold' : 'text-on-surface-variant hover:bg-surface-container'"
                @click="setBasis(row, basis)"
              >
                <AppIcon v-if="row.basis === basis" name="check" class="h-4 w-4" />
                {{ BASIS_LABELS[basis] }}
              </button>
            </div>
            <span v-else class="inline-flex min-h-11 items-center rounded-lg bg-surface-container px-3 text-sm">درصد</span>

            <div class="min-w-40 flex-1">
              <NumberField
                :model-value="row.rate"
                :kind="isPercentRow(row) ? 'percent' : 'money'"
                :scale="row.basis === CostBasis.Total ? moneyScale : 4"
                :allow-negative="row.basis === CostBasis.Total"
                :readonly="readOnly"
                :invalid="!!rowError(index, 'rate')"
                :aria-label="`نرخ ${row.label || 'سطر'}`"
                :words="!isPercentRow(row)"
                @update:model-value="setRate(row, $event)"
              />
              <p v-if="rowError(index, 'rate')" class="mt-1 text-xs text-error">{{ rowError(index, 'rate') }}</p>
            </div>
          </div>

          <p class="mt-2 text-end text-sm" data-numeric>
            <span class="text-on-surface-variant">مبلغ: </span>
            <bdi>{{ amountText(row, index) }}</bdi>
          </p>
        </li>
      </ul>

      <div v-if="!readOnly" class="mt-3 flex flex-wrap items-center gap-2">
        <div class="relative">
          <Button icon="plus" :aria-expanded="addOpen" @click="addOpen = !addOpen">افزودن سطر</Button>
          <div
            v-if="addOpen"
            class="absolute start-0 z-20 mt-2 w-60 rounded-xl border border-outline-variant bg-surface-container-lowest p-2 shadow-lg"
            role="menu"
          >
            <button
              v-for="category in categories"
              :key="category.id"
              type="button"
              role="menuitem"
              class="flex min-h-11 w-full items-center gap-2 rounded-lg px-3 text-start text-sm hover:bg-surface-container"
              @click="addRow(category)"
            >
              <AppIcon name="receipt" class="h-4 w-4" />
              {{ category.name }}
              <span class="ms-auto text-xs text-on-surface-variant">{{ BASIS_LABELS[category.defaultBasis] }}</span>
            </button>
            <button
              type="button"
              role="menuitem"
              class="flex min-h-11 w-full items-center gap-2 rounded-lg px-3 text-start text-sm hover:bg-surface-container"
              @click="addRow(null)"
            >
              <AppIcon name="plus" class="h-4 w-4" />
              سطر آزاد
            </button>
          </div>
        </div>

        <Button v-if="dirty" variant="primary" icon="check" :loading="saving" @click="save">
          {{ saving ? message('I_SAVING') : 'ذخیرهٔ قیمت خرید' }}
        </Button>
        <Button v-if="dirty && !saving" variant="ghost" @click="load">انصراف</Button>
      </div>

      <div v-if="formError" class="mt-3 rounded-lg bg-error-container px-3 py-2 text-sm text-on-error-container" role="alert">
        {{ formError }}
        <div v-if="stale" class="mt-2">
          <Button icon="undo" @click="load">دوباره بخوان</Button>
        </div>
      </div>

      <p v-if="costs && !dirty" class="mt-3 border-t border-outline-variant pt-2 text-xs text-on-surface-variant" data-numeric>
        بهای تمام‌شده: {{ faMoney(costs.totalInvestment) }}
      </p>
    </template>
  </section>
</template>
