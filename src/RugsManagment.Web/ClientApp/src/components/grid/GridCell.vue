<script setup lang="ts">
/**
 * یک خانهٔ جدول (و یک خانهٔ کارت در نمای گوشی).
 *
 * خانهٔ فعال همیشه یک `<input>` واقعی دارد که فوکوس گرفته و متنش انتخاب شده است: پس تایپ کردن همان لحظه جای متن
 * می‌نشیند (مثل اکسل)، صفحه‌کلید لمسی با `inputmode` درست بالا می‌آید و `enterkeyhint="next"` دارد (G.1، H.6). بقیهٔ
 * خانه‌ها فقط متن‌اند تا جدول ۳۰۰ ردیفی سبک بماند.
 *
 * - combo: پیشنهاد هنگام تایپ (گزینه‌های ثابت، همین جدول و `/api/rugs/values`)؛ ↓/↑ و Enter؛ مقدار تازه هم پذیرفته است.
 * - مبلغ + واحد: چیپ «هر متر | هر تخته | کل» با ضربه (یا Alt+↓) عوض می‌شود؛ خانهٔ چندسطری فقط‌خواندنی با چیپ
 *   «چند سطر» که به بخش «خرید» صفحهٔ فرش می‌رود.
 * - خالی‌ها: شمارهٔ پیشنهادی، «خودکار» و مقدار چسبانِ ردیف بالا خاکستری دیده می‌شوند.
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { appContext } from '@/lib/context'
import { message } from '@/lib/messages'
import { CostBasis, type GridColumn } from '@/lib/types/grid'
import { SerialMode } from '@/lib/types/platform'
import {
  KEY_AREA,
  KEY_PURCHASE_TOTAL,
  KEY_QTY,
  KEY_SERIAL,
  displayText,
  faDigits,
  faNum,
  fold,
  isMultiLine,
  isPurchaseKey,
  purchaseDefaultBasis,
  readPurchaseCell,
  rowPurchase,
  rowQty,
  serialForLot,
  suggestionIndex,
  unitLabel,
} from './gridParse'
import type { GridRowState } from './gridModel'
import type { GridController } from './useGridController'
import { inRange } from './useGridSelection'

const props = withDefaults(
  defineProps<{
    ctrl: GridController
    row: GridRowState
    rowIndex: number
    column: GridColumn
    colIndex: number
    variant?: 'table' | 'card'
    /** خانهٔ ستون اول (یخ‌زده) */
    frozen?: boolean
  }>(),
  { variant: 'table', frozen: false },
)

const ctx = appContext()
const uid = useId()
const errorId = `${uid}-err`
const listId = `${uid}-list`
const input = ref<HTMLInputElement | null>(null)

const key = computed(() => props.column.key)
const active = computed(() => props.ctrl.nav.row === props.rowIndex && props.ctrl.nav.col === props.colIndex)
const selected = computed(() => inRange(props.ctrl.selection.range, props.rowIndex, props.colIndex))
const editable = computed(() => props.ctrl.isEditable(props.column, props.row))
const value = computed(() => props.row.values[key.value])
const isBlank = computed(() => value.value === null || value.value === undefined || value.value === '')

const error = computed(() => (active.value && props.ctrl.editError) || props.row.errors[key.value] || null)
const warning = computed(() => (key.value === KEY_AREA && props.row.warning) || null)

const numeric = computed(() => ['int', 'dim', 'money', 'money+unit', 'percent'].includes(props.column.type))
const inputMode = computed(() => {
  switch (props.column.type) {
    case 'dim':
    case 'money':
    case 'money+unit':
    case 'percent':
      return 'decimal'
    case 'int':
      return 'numeric'
    default:
      return 'text'
  }
})

// ── خانهٔ خرید ──────────────────────────────────────────────────────────

const purchase = computed(() =>
  props.column.type === 'money+unit' || (props.column.type === 'percent' && isPurchaseKey(key.value))
    ? readPurchaseCell(value.value, props.column)
    : null,
)
const multi = computed(() => isMultiLine(purchase.value))
const unitBasis = computed<CostBasis>(() => purchase.value?.basis ?? purchaseDefaultBasis(props.column))
const showUnit = computed(() => props.column.type === 'money+unit' && !props.row.placeholder && !multi.value)
/** دو سطر یا بیشتر از این نوع خرید (درصدی هم): فقط‌خواندنی با چیپ «چند سطر» که به بخش «خرید» فرش می‌رود */
const showMulti = computed(() => multi.value && !!props.row.rugId)

// ── متن نمایشی ──────────────────────────────────────────────────────────

const purchaseTotalText = computed(() => {
  const local = rowPurchase(props.row.values, props.ctrl.columns, ctx.moneyScale)
  const derived = props.row.derived[KEY_PURCHASE_TOTAL]
  const clean = props.row.dirty.length === 0
  if (clean && typeof derived === 'number') {
    if (local && Number(local.total.units) !== 0 && faNum(local.total, ctx.moneyScale) === faNum(derived, ctx.moneyScale)) return local.text
    return faNum(derived, ctx.moneyScale)
  }
  return local?.text ?? (typeof derived === 'number' ? faNum(derived, ctx.moneyScale) : '')
})

const shown = computed(() => {
  if (key.value === KEY_AREA) {
    const text = props.ctrl.areaOf(props.row)
    if (text) return text
    const derived = props.row.derived[KEY_AREA]
    return typeof derived === 'number' ? faNum(derived, 2, 2) : ''
  }
  if (key.value === KEY_PURCHASE_TOTAL) return purchaseTotalText.value
  if (props.column.type === 'readonly') {
    const derived = props.row.derived[key.value] ?? value.value
    return derived === null || derived === undefined ? '' : typeof derived === 'number' ? faNum(derived, 2) : String(derived)
  }
  return displayText(props.column, value.value, ctx.moneyScale)
})

/** متن خاکستری خانهٔ خالی: شمارهٔ پیشنهادی، «خودکار»، مقدار چسبان یا تعداد ۱. */
const hint = computed(() => {
  if (!isBlank.value) return ''
  const row = props.row
  if (key.value === KEY_SERIAL && row.rugId === null) {
    const mode = props.ctrl.serialMode
    if (mode === SerialMode.Auto) return message('I_GRID_AUTO_SERIAL')
    if (mode === SerialMode.SuggestNext && props.ctrl.suggestion !== null) {
      return faDigits(serialForLot(props.ctrl.suggestion, rowQty(row.values)))
    }
    return ''
  }
  if (key.value === KEY_QTY && row.rugId === null) return faDigits(1)
  if (row.placeholder && props.column.sticky) {
    const sticky = props.ctrl.stickyValue(props.rowIndex, props.column)
    return sticky === undefined ? '' : displayText(props.column, sticky, ctx.moneyScale)
  }
  return ''
})

const ariaLabel = computed(() => {
  const serial = props.ctrl.serialLabel(props.row)
  return serial ? `${props.column.label} — ${serial}` : props.column.label
})

const inputValue = computed(() => (editable.value ? props.ctrl.editText : shown.value))

// ── رویدادها ────────────────────────────────────────────────────────────

function onMouseDown(event: MouseEvent) {
  if (event.button !== 0) return
  if (active.value && !event.shiftKey) return
  event.preventDefault()
  props.ctrl.activate(props.rowIndex, props.colIndex, { extend: event.shiftKey })
}

function onDblClick() {
  props.ctrl.startEdit()
}

function onInput(event: Event) {
  props.ctrl.onInput((event.target as HTMLInputElement).value)
  scheduleSuggest()
}

function onPaste(event: ClipboardEvent) {
  const text = event.clipboardData?.getData('text/plain') ?? ''
  // متن چندخانه‌ای یا چسباندن روی خانهٔ انتخاب‌شده (نه وسط ویرایش): چسباندن جدولی
  if (props.ctrl.mode === 'nav' || /[\t\r\n]/.test(text.replace(/[\r\n]+$/, ''))) {
    event.preventDefault()
    props.ctrl.paste(text)
  }
}

function onCopy(event: ClipboardEvent) {
  if (props.ctrl.mode !== 'nav') return
  event.preventDefault()
  event.clipboardData?.setData('text/plain', props.ctrl.copyText())
}

function onBlur(event: FocusEvent) {
  closeList()
  const next = event.relatedTarget as Node | null
  // بیرون رفتن از جدول (نه به خانهٔ دیگر یا دکمهٔ پرسش): نوشته را ثبت کن
  if (next && (next as HTMLElement).closest?.('[data-grid-root]')) return
  if (props.ctrl.mode !== 'nav' && !props.ctrl.question) props.ctrl.commit()
}

function onUnit(event: Event) {
  event.stopPropagation()
  props.ctrl.cycleUnit(props.rowIndex, key.value)
}

// ── پیشنهادهای combo ────────────────────────────────────────────────────

const isCombo = computed(() => props.column.type === 'combo' || props.column.type === 'select')
const items = ref<string[]>([])
const listOpen = ref(false)
const highlighted = ref(-1)
const listStyle = ref<Record<string, string>>({})
let suggestTimer: ReturnType<typeof setTimeout> | null = null
let suggestSeq = 0

function scheduleSuggest(all = false) {
  if (!isCombo.value || !active.value) return
  if (suggestTimer) clearTimeout(suggestTimer)
  suggestTimer = setTimeout(() => void suggest(all), 150)
}

async function suggest(all: boolean) {
  const seq = ++suggestSeq
  const text = all ? '' : props.ctrl.editText
  const found = await props.ctrl.valuesFor(key.value, text)
  if (seq !== suggestSeq || !active.value) return
  const typed = fold(text)
  items.value = found
  highlighted.value = suggestionIndex(found, text)
  const only = found.length === 1 && fold(found[0]) === typed
  listOpen.value = found.length > 0 && !only
  if (listOpen.value) place()
}

function place() {
  const el = input.value
  if (!el) return
  const rect = el.getBoundingClientRect()
  listStyle.value = {
    top: `${Math.round(rect.bottom + 2)}px`,
    right: `${Math.round(window.innerWidth - rect.right)}px`,
    minWidth: `${Math.max(Math.round(rect.width), 192)}px`,
  }
}

function closeList() {
  listOpen.value = false
  highlighted.value = -1
}

function choose(index: number) {
  const item = items.value[index]
  if (item === undefined) return
  props.ctrl.onInput(item)
  closeList()
}

function onKeydown(event: KeyboardEvent) {
  if (listOpen.value && items.value.length > 0 && !props.ctrl.question) {
    const n = items.value.length
    if (event.key === 'ArrowDown' && !event.altKey) {
      highlighted.value = (highlighted.value + 1) % n
      event.preventDefault()
      return
    }
    if (event.key === 'ArrowUp') {
      highlighted.value = (highlighted.value - 1 + n) % n
      event.preventDefault()
      return
    }
    if (event.key === 'Escape') {
      closeList()
      event.preventDefault()
      event.stopPropagation()
      return
    }
    if ((event.key === 'Enter' || event.key === 'Tab') && highlighted.value >= 0) choose(highlighted.value)
    else if (event.key === 'Enter' || event.key === 'Tab') closeList()
  }
  if (isCombo.value && event.key === 'ArrowDown' && event.altKey && editable.value) {
    event.preventDefault()
    scheduleSuggest(true)
    return
  }
  if (props.ctrl.onKeydown(event)) {
    event.preventDefault()
    event.stopPropagation()
  }
}

// ── فوکوس ───────────────────────────────────────────────────────────────

function focusInput() {
  const el = input.value
  if (!el) return
  if (document.activeElement !== el) el.focus({ preventScroll: props.variant === 'table' })
  if (props.ctrl.mode === 'nav') el.select()
  else if (props.ctrl.mode === 'edit') {
    const end = el.value.length
    el.setSelectionRange(end, end)
  }
  if (props.variant === 'card') el.scrollIntoView({ block: 'nearest' })
}

watch(
  () => [active.value, props.ctrl.focusTick] as const,
  ([isActive]) => {
    if (!isActive) {
      closeList()
      return
    }
    void nextTick(focusInput)
  },
  { flush: 'post' },
)

// پس از ثبت با Enter، متن انتخاب‌شده بماند تا تایپ بعدی جایش بنشیند
watch(
  () => props.ctrl.mode,
  (mode) => {
    if (active.value && mode === 'nav') void nextTick(() => input.value?.select())
  },
  { flush: 'post' },
)

/**
 * خانه‌ای که از اول فعال ساخته می‌شود تغییر [active, focusTick] را نمی‌بیند: Ctrl+Home یا PageUp به ردیفی بیرون از
 * ردیف‌های رندرشده، که ردیفش تازه ساخته می‌شود و خانهٔ قبلی (با فوکوس) می‌رود. بی این، فوکوس به body می‌افتاد و تایپ
 * بعدی به هیچ‌جا نمی‌رفت. فقط وقتی فوکوس جای دیگری نیست (body) یا هنوز در همین جدول است؛ نوار پیش‌نویس یا دکمه‌ای
 * بیرون از جدول فوکوسش را نگه می‌دارد، و تا کاربر جدول را فوکوس نداده (focusTick صفر) چیزی دزدیده نمی‌شود.
 */
onMounted(() => {
  if (!active.value || props.ctrl.focusTick === 0) return
  const current = document.activeElement
  const grid = input.value?.closest('[data-grid-root]')
  if (!current || current === document.body || (grid && grid.contains(current))) void nextTick(focusInput)
})

onBeforeUnmount(() => {
  if (suggestTimer) clearTimeout(suggestTimer)
})

const cellClasses = computed(() => {
  const list: string[] = []
  if (props.variant === 'table') {
    list.push('relative flex h-full min-w-0 items-center gap-1 border-b border-s border-outline-variant/50 px-2 text-sm')
    if (props.frozen) list.push('sticky z-10 bg-surface-container-lowest')
    if (props.row.placeholder) list.push('bg-surface-container-low/60')
  } else {
    list.push('relative flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-base')
  }
  if (!editable.value && props.column.type === 'readonly') list.push('text-on-surface-variant')
  if (selected.value && !active.value) list.push('bg-primary/10')
  if (active.value) list.push('z-20 ring-2 ring-inset ring-primary')
  else if (error.value) list.push('ring-2 ring-inset ring-error')
  else if (warning.value) list.push('ring-2 ring-inset ring-warning')
  return list
})
</script>

<template>
  <div
    :role="variant === 'table' ? 'gridcell' : undefined"
    :aria-colindex="variant === 'table' ? colIndex + 2 : undefined"
    :aria-selected="variant === 'table' ? selected : undefined"
    :aria-readonly="variant === 'table' && !editable || undefined"
    :aria-invalid="variant === 'table' && !active && error ? true : undefined"
    :aria-describedby="variant === 'table' && !active && error ? errorId : undefined"
    :class="cellClasses"
    @mousedown="onMouseDown"
    @dblclick="onDblClick"
  >
    <!-- چیپ واحد خرید (پیش از عدد: در راست‌به‌چپ سمت راست) -->
    <button
      v-if="showUnit"
      type="button"
      tabindex="-1"
      class="touch-target shrink-0 rounded-full border border-outline-variant bg-surface-container px-2 py-0.5 text-xs text-on-surface-variant hover:bg-surface-container-high"
      :aria-label="`واحد ${column.label}: ${unitLabel(unitBasis)}`"
      :disabled="!editable"
      @mousedown.prevent.stop
      @click="onUnit"
    >{{ unitLabel(unitBasis) }}</button>
    <a
      v-else-if="showMulti"
      :href="`/Rugs/Details/${row.rugId}#purchase`"
      tabindex="-1"
      class="touch-target shrink-0 rounded-full border border-secondary bg-secondary-container px-2 py-0.5 text-xs text-on-secondary-container"
      @mousedown.stop
    >{{ message('I_GRID_MULTI_LINE') }}</a>

    <input
      v-if="active"
      ref="input"
      data-active-input
      type="text"
      autocomplete="off"
      spellcheck="false"
      enterkeyhint="next"
      :inputmode="inputMode"
      :dir="numeric || column.type === 'serial' ? 'ltr' : undefined"
      :value="inputValue"
      :placeholder="hint || undefined"
      :readonly="!editable || ctrl.question !== null"
      :aria-label="ariaLabel"
      :aria-invalid="error ? true : undefined"
      :aria-describedby="error ? errorId : undefined"
      :aria-autocomplete="isCombo ? 'list' : undefined"
      :aria-controls="isCombo && listOpen ? listId : undefined"
      :aria-expanded="isCombo ? listOpen : undefined"
      :role="isCombo ? 'combobox' : undefined"
      class="h-full min-w-0 flex-1 bg-transparent text-on-surface outline-none placeholder:text-on-surface-variant"
      :class="[numeric ? 'text-left tabular-nums' : 'text-start', variant === 'card' ? 'min-h-11' : '']"
      @keydown="onKeydown"
      @input="onInput"
      @paste="onPaste"
      @copy="onCopy"
      @blur="onBlur"
    />
    <span
      v-else
      class="min-w-0 flex-1 truncate"
      :class="[numeric || key === KEY_AREA || key === KEY_PURCHASE_TOTAL ? 'text-left tabular-nums' : 'text-start', isBlank && hint ? 'text-on-surface-variant' : '']"
      :title="shown || undefined"
    >
      <bdi v-if="column.type === 'serial' && !isBlank" dir="ltr">{{ shown }}</bdi>
      <!--
        حساب «متراژ» و «جمع خرید» («۸٬۵۰۰٬۰۰۰ × ۳٫۰۳ + ۲٪ = ۲۶٬۲۷۰٬۱۰۰») چپ‌به‌راست جدا می‌شود تا وقتی در خانه جا
        نمی‌شود، سرِ حساب بریده شود و حاصل سمت راست دیده بماند؛ بی این، حاصل (عددی که بازرگان دنبالش است) بریده می‌شد.
        متن کامل در title است.
      -->
      <bdi v-else-if="(key === KEY_AREA || key === KEY_PURCHASE_TOTAL) && shown" dir="ltr">{{ shown }}</bdi>
      <template v-else>{{ isBlank && column.type !== 'readonly' ? hint : shown }}</template>
    </span>

    <AppIcon v-if="error" name="error" class="h-4 w-4 shrink-0 text-error" />
    <AppIcon v-else-if="warning" name="warning" class="h-4 w-4 shrink-0 text-warning" />
    <span v-if="error" :id="errorId" class="sr-only">{{ error }}</span>

    <Teleport to="body">
      <ul
        v-if="active && listOpen"
        :id="listId"
        role="listbox"
        :aria-label="column.label"
        class="fixed z-[60] max-h-64 overflow-y-auto rounded-lg border border-outline-variant bg-surface-container-lowest py-1 text-sm shadow-lg"
        :style="listStyle"
      >
        <li
          v-for="(item, index) in items"
          :key="item"
          role="option"
          :aria-selected="index === highlighted"
          class="flex min-h-11 cursor-pointer items-center gap-2 px-3"
          :class="index === highlighted ? 'bg-primary/10 text-primary' : 'text-on-surface hover:bg-surface-container'"
          @mousedown.prevent="choose(index)"
          @mousemove="highlighted = index"
        >
          <AppIcon v-if="index === highlighted" name="check" class="h-4 w-4" />
          <span class="truncate">{{ item }}</span>
        </li>
      </ul>
    </Teleport>
  </div>
</template>
