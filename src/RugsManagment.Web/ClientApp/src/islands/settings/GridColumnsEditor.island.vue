<script setup lang="ts">
/**
 * جزیرهٔ `grid-columns-editor` (I.5، Configure): چیدمان ستون‌های یک جدول برای همهٔ کاربران کارگاه —
 * `GET/PUT /api/settings/grid-layout/{gridId}`.
 *
 * - ترتیب با دکمه‌های «بالا / پایین» (نه فقط کشیدن؛ WCAG 2.5.7)، نام، پهنا (به `ch`، تا با اندازهٔ نوشته بزرگ شود)،
 *   «چسبان» (مقدار به ردیف تازهٔ بعد می‌رسد) و «با Tab» (ایستگاه Tab).
 * - ستونی که برداشته شود پنهان است و از «ستون‌های پنهان» دوباره برمی‌گردد. ستون لازم (ردیف تازه بی آن ذخیره نمی‌شود)
 *   پنهان‌شدنی نیست.
 * - نسخه همراه ذخیره می‌رود؛ اگر کس دیگری همین حالا عوضش کرده باشد سرور ۴۰۹ می‌دهد و صفحه پیشنهاد بار دوباره دارد.
 * - «برگرداندن پیش‌فرض» چیدمان کارگاه را پاک می‌کند (`DELETE …?version=`).
 */
import { computed, onMounted, ref } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import NumberField from '@/components/ui/NumberField.vue'
import { ApiError, api, errorMessage } from '@/lib/api'
import { message } from '@/lib/messages'
import { confirmDialog, toast } from '@/lib/ui'
import type { GridColumn, GridId, GridLayout } from '@/lib/types/grid'

const props = defineProps<{ gridId: GridId }>()

/** حدهای پهنا (همان GridLayoutService). */
const MIN_WIDTH = 4
const MAX_WIDTH = 60

const TYPE_LABELS: Record<string, string> = {
  serial: 'شماره',
  text: 'متن',
  combo: 'متن با پیشنهاد',
  select: 'گزینه',
  int: 'عدد',
  dim: 'اندازه',
  money: 'مبلغ',
  'money+unit': 'مبلغ با واحد',
  percent: 'درصد',
  readonly: 'حساب‌شده',
}

const loading = ref(true)
const loadError = ref('')
const saving = ref(false)
const formError = ref('')
const stale = ref(false)
const version = ref(0)
const columns = ref<GridColumn[]>([])
const available = ref<GridColumn[]>([])
const savedJson = ref('')

const hidden = computed(() => available.value.filter((a) => !columns.value.some((c) => c.key === a.key)))
const dirty = computed(() => JSON.stringify(columns.value) !== savedJson.value)

function apply(layout: GridLayout) {
  version.value = layout.version
  columns.value = layout.columns.map((c) => ({ ...c }))
  available.value = (layout.available ?? layout.columns).map((c) => ({ ...c }))
  savedJson.value = JSON.stringify(columns.value)
}

async function load() {
  loading.value = true
  loadError.value = ''
  formError.value = ''
  stale.value = false
  try {
    apply(await api.get<GridLayout>(`/api/settings/grid-layout/${encodeURIComponent(props.gridId)}`))
  } catch (error) {
    loadError.value = errorMessage(error)
  } finally {
    loading.value = false
  }
}

function move(index: number, delta: -1 | 1) {
  const target = index + delta
  if (target < 0 || target >= columns.value.length) return
  const list = [...columns.value]
  ;[list[index], list[target]] = [list[target], list[index]]
  columns.value = list
}

function hide(index: number) {
  columns.value = columns.value.filter((_, i) => i !== index)
}

function show(column: GridColumn) {
  columns.value = [...columns.value, { ...column }]
}

function setWidth(column: GridColumn, value: number | null) {
  column.width = value === null ? column.width : Math.min(MAX_WIDTH, Math.max(MIN_WIDTH, Math.round(value)))
}

function handleError(error: unknown) {
  formError.value = errorMessage(error)
  stale.value = error instanceof ApiError && error.status === 409
  if (stale.value && !formError.value) formError.value = message('E_GRID_LAYOUT_STALE')
}

async function save() {
  if (columns.value.length === 0) {
    formError.value = message('E_GRID_LAYOUT_EMPTY')
    return
  }
  saving.value = true
  formError.value = ''
  stale.value = false
  try {
    apply(
      await api.put<GridLayout>(`/api/settings/grid-layout/${encodeURIComponent(props.gridId)}`, {
        gridId: props.gridId,
        columns: columns.value,
        version: version.value,
      }),
    )
    toast.success(message('I_GRID_LAYOUT_SAVED'))
  } catch (error) {
    handleError(error)
  } finally {
    saving.value = false
  }
}

async function reset() {
  const ok = await confirmDialog({
    title: 'ستون‌ها به چیدمان پیش‌فرض برگردد؟',
    message: 'نام، ترتیب و پهنایی که این‌جا گذاشته‌اید پاک می‌شود.',
    confirmLabel: 'برگردان',
    cancelLabel: 'نه',
    danger: true,
  })
  if (!ok) return
  saving.value = true
  formError.value = ''
  try {
    apply(await api.del<GridLayout>(`/api/settings/grid-layout/${encodeURIComponent(props.gridId)}?version=${version.value}`))
    toast.success(message('I_GRID_LAYOUT_SAVED'))
  } catch (error) {
    handleError(error)
  } finally {
    saving.value = false
  }
}

onMounted(() => void load())
</script>

<template>
  <div class="space-y-5">
    <div v-if="loading" class="skeleton h-96 w-full" aria-hidden="true"></div>

    <EmptyState v-else-if="loadError" icon="warning" title="ستون‌ها بار نشد" :text="loadError">
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
        <Button v-if="stale" icon="undo" @click="load">بارگذاری دوباره</Button>
      </div>

      <section class="rounded-xl border border-outline-variant bg-surface-container-lowest p-4 shadow-sm sm:p-5">
        <h2 class="mb-1 font-semibold text-primary">ستون‌های پیدا</h2>
        <p class="mb-4 text-sm text-on-surface-variant">
          از راست به چپ همین ترتیب در جدول دیده می‌شود. پهنا به اندازهٔ چند حرف است و با بزرگ شدن نوشته بزرگ می‌شود.
        </p>

        <ol class="flex flex-col gap-3">
          <li
            v-for="(column, index) in columns"
            :key="column.key"
            class="grid gap-3 rounded-lg border border-outline-variant p-3 sm:grid-cols-[auto_minmax(0,1fr)_7rem_auto] sm:items-center"
          >
            <div class="flex items-center gap-1">
              <span class="w-6 text-center text-sm text-on-surface-variant tabular-nums">{{ (index + 1).toLocaleString('fa-IR') }}</span>
              <button
                type="button"
                class="grid h-11 w-11 place-items-center rounded-lg border border-outline-variant hover:bg-surface-container"
                :disabled="index === 0"
                :aria-label="`${column.label}: یکی جلوتر`"
                @click="move(index, -1)"
              >
                <AppIcon name="chevron-up" class="h-5 w-5" />
              </button>
              <button
                type="button"
                class="grid h-11 w-11 place-items-center rounded-lg border border-outline-variant hover:bg-surface-container"
                :disabled="index === columns.length - 1"
                :aria-label="`${column.label}: یکی عقب‌تر`"
                @click="move(index, 1)"
              >
                <AppIcon name="chevron-down" class="h-5 w-5" />
              </button>
            </div>

            <div class="min-w-0">
              <label :for="`col-label-${index}`" class="sr-only">نام ستون</label>
              <input
                :id="`col-label-${index}`"
                v-model.trim="column.label"
                maxlength="60"
                autocomplete="off"
                class="min-h-11 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary"
              />
              <p class="mt-1 flex flex-wrap items-center gap-x-2 text-xs text-on-surface-variant">
                <span>{{ TYPE_LABELS[column.type] ?? column.type }}</span>
                <span v-if="column.required" class="text-error">لازم</span>
                <span v-if="column.money" class="inline-flex items-center gap-0.5"><AppIcon name="coins" class="h-3.5 w-3.5" />مبلغ</span>
              </p>
            </div>

            <div>
              <NumberField
                :model-value="column.width"
                kind="integer"
                :min="MIN_WIDTH"
                :max="MAX_WIDTH"
                suffix="حرف"
                :aria-label="`پهنای ${column.label}`"
                :words="false"
                @update:model-value="(v) => setWidth(column, v)"
              />
            </div>

            <div class="flex flex-wrap items-center gap-2">
              <label
                class="flex min-h-11 cursor-pointer items-center gap-2 rounded-lg border px-3 text-sm"
                :class="column.sticky ? 'border-primary bg-primary/5' : 'border-outline-variant'"
              >
                <input v-model="column.sticky" type="checkbox" class="h-4 w-4 accent-primary" :disabled="column.readOnly" />
                چسبان
              </label>
              <label
                class="flex min-h-11 cursor-pointer items-center gap-2 rounded-lg border px-3 text-sm"
                :class="column.tabStop !== false ? 'border-primary bg-primary/5' : 'border-outline-variant'"
              >
                <input
                  type="checkbox"
                  class="h-4 w-4 accent-primary"
                  :checked="column.tabStop !== false && !column.readOnly"
                  :disabled="column.readOnly"
                  @change="column.tabStop = ($event.target as HTMLInputElement).checked"
                />
                با Tab
              </label>
              <button
                type="button"
                class="grid h-11 w-11 place-items-center rounded-lg text-on-surface-variant hover:bg-surface-container disabled:opacity-40"
                :disabled="column.required"
                :aria-label="`پنهان کردن ${column.label}`"
                :title="column.required ? message('E_GRID_LAYOUT_REQUIRED', { label: column.label }) : undefined"
                @click="hide(index)"
              >
                <AppIcon name="close" class="h-5 w-5" />
              </button>
            </div>
          </li>
        </ol>
      </section>

      <section v-if="hidden.length > 0" class="rounded-xl border border-outline-variant bg-surface-container-lowest p-4 shadow-sm sm:p-5">
        <h2 class="mb-3 font-semibold text-primary">ستون‌های پنهان</h2>
        <ul class="flex flex-wrap gap-2">
          <li v-for="column in hidden" :key="column.key">
            <button
              type="button"
              class="inline-flex min-h-11 items-center gap-2 rounded-full border border-outline-variant px-4 text-sm hover:bg-surface-container"
              @click="show(column)"
            >
              <AppIcon name="plus" class="h-4 w-4" />
              {{ column.label }}
            </button>
          </li>
        </ul>
      </section>

      <div class="flex flex-wrap items-center gap-3">
        <Button type="submit" variant="primary" size="lg" icon="check" :loading="saving" :disabled="!dirty">ذخیرهٔ ستون‌ها</Button>
        <Button icon="undo" :disabled="saving || version === 0" @click="reset">برگرداندن پیش‌فرض</Button>
        <span v-if="dirty" class="text-sm text-on-surface-variant">تغییرها هنوز ذخیره نشده.</span>
      </div>
    </form>
  </div>
</template>
