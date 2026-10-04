<script setup lang="ts">
/**
 * خانهٔ تاریخ شمسی (H.4) — جای `<input type="date">` میلادی.
 *
 * - تایپ آزاد: «1405/6/15»، «1405.06.15»، «14050615»، با ارقام لاتین، فارسی یا عربی و جداکننده‌های / . - ٫.
 *   تاریخ ناممکن «۱۴۰۵/۰۶/۳۲ وجود ندارد» و سال میلادی «سال را شمسی بنویسید» می‌گیرد.
 * - دکمه‌های «امروز» و «دیروز» (امروز به وقت ایران، از app-context).
 * - تقویم ماهانه با نام ماه‌های فارسی، کاملاً با صفحه‌کلید: در راست‌به‌چپ ← روز بعد و → روز قبل،
 *   ↑/↓ هفتهٔ قبل/بعد، PageUp/PageDown ماه قبل/بعد، Home/End اول/آخر هفته، Enter انتخاب، Esc بستن.
 * - مقدار `v-model` تاریخ میلادی ISO («2026-09-06») است، همان که `DateOnly` سرور می‌خواهد.
 */
import { computed, nextTick, onBeforeUnmount, ref, useId, watch } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import { appContext } from '@/lib/context'
import { toPersianDigits } from '@/lib/format'
import {
  JALALI_MONTHS,
  JALALI_WEEKDAYS,
  JALALI_WEEKDAYS_SHORT,
  addIsoDays,
  addJalaliDays,
  addJalaliMonths,
  faJalali,
  isoToJalali,
  jalaliMonthLength,
  jalaliToIso,
  jalaliWeekday,
  parseJalali,
  type JalaliDate,
} from '@/lib/jalali'
import { message } from '@/lib/messages'

const props = withDefaults(
  defineProps<{
    modelValue: string | null | undefined
    id?: string
    describedBy?: string
    invalid?: boolean
    disabled?: boolean
    readonly?: boolean
    required?: boolean
    placeholder?: string
    /** دکمه‌های «امروز» و «دیروز» */
    chips?: boolean
    /** کمترین و بیشترین تاریخ مجاز (ISO) */
    min?: string
    max?: string
    ariaLabel?: string
    /** نام خانه برای پیام ««…» را بنویسید» (نمایش داده نمی‌شود؛ برچسب دیدنی کار Field است) */
    label?: string
  }>(),
  {
    id: undefined,
    describedBy: undefined,
    invalid: false,
    disabled: false,
    readonly: false,
    required: false,
    placeholder: '۱۴۰۵/۰۶/۱۵',
    chips: true,
    min: undefined,
    max: undefined,
    ariaLabel: undefined,
    label: undefined,
  },
)

const emit = defineEmits<{ 'update:modelValue': [value: string | null]; commit: [value: string | null] }>()

const ctx = appContext()
const uid = useId()
const inputId = computed(() => props.id ?? `${uid}-input`)
const errorId = `${uid}-error`
const popoverId = `${uid}-calendar`
const monthLabelId = `${uid}-month`

const root = ref<HTMLElement | null>(null)
const input = ref<HTMLInputElement | null>(null)
const toggle = ref<HTMLButtonElement | null>(null)
const grid = ref<HTMLElement | null>(null)

const focused = ref(false)
const text = ref(display(props.modelValue))
const errorText = ref<string | null>(null)

function display(iso: string | null | undefined): string {
  return iso ? (isoToJalali(iso) ? faJalali(iso) : '') : ''
}

/** آخرین مقداری که خودمان فرستادیم؛ تغییر بیرونی را از پژواک خودمان جدا می‌کند */
let lastEmitted: string | null = props.modelValue ?? null

function emitValue(iso: string | null) {
  if (iso === lastEmitted) return
  lastEmitted = iso
  emit('update:modelValue', iso)
}

watch(
  () => props.modelValue,
  (iso) => {
    // پژواک مقدار خودمان (مثلاً null پس از تاریخ ناممکن) نباید پیام خطا را پاک کند
    if ((iso ?? null) === lastEmitted) return
    lastEmitted = iso ?? null
    errorText.value = null
    if (!focused.value) text.value = display(iso)
  },
)

const today = computed(() => ctx.today)
const yesterday = computed(() => addIsoDays(ctx.today, -1))

function outOfRange(iso: string): string | null {
  if (props.min && iso < props.min) return message('E_DATE_BEFORE', { date: faJalali(props.min) })
  if (props.max && iso > props.max) return message('E_DATE_AFTER', { date: faJalali(props.max) })
  return null
}

function set(iso: string | null) {
  if (iso !== null) {
    const problem = outOfRange(iso)
    if (problem) {
      errorText.value = problem
      emitValue(null)
      emit('commit', null)
      return
    }
  }
  errorText.value = null
  text.value = display(iso)
  emitValue(iso)
  emit('commit', iso)
}

/** ثبت متن تایپ‌شده. true اگر خطایی ماند. */
function commit(): boolean {
  const r = parseJalali(text.value)
  if (r.status === 'empty') {
    set(null)
    return false
  }
  if (r.status === 'ok') {
    set(r.iso)
    return errorText.value !== null
  }
  errorText.value = message(r.code, r.date ? { date: r.date } : undefined)
  emitValue(null)
  emit('commit', null)
  return true
}

function onFocus() {
  focused.value = true
}

function onBlur() {
  focused.value = false
  commit()
}

function onInput(event: Event) {
  text.value = (event.target as HTMLInputElement).value
  errorText.value = null
}

function onInputKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter') {
    if (commit()) event.preventDefault()
  } else if (event.key === 'ArrowDown' && event.altKey) {
    event.preventDefault()
    void openCalendar()
  }
}

// ── تقویم ────────────────────────────────────────────────────────────

const open = ref(false)
const view = ref<{ jy: number; jm: number }>({ jy: 1405, jm: 1 })
const active = ref<JalaliDate>({ jy: 1405, jm: 1, jd: 1 })

const selected = computed(() => isoToJalali(props.modelValue ?? null))
const todayJ = computed(() => isoToJalali(ctx.today))

const monthTitle = computed(() => toPersianDigits(`${JALALI_MONTHS[view.value.jm - 1]} ${view.value.jy}`))

const weeks = computed(() => {
  const { jy, jm } = view.value
  const offset = jalaliWeekday({ jy, jm, jd: 1 })
  const length = jalaliMonthLength(jy, jm)
  const cells: (JalaliDate | null)[] = Array.from({ length: offset }, () => null)
  for (let jd = 1; jd <= length; jd++) cells.push({ jy, jm, jd })
  while (cells.length % 7 !== 0) cells.push(null)
  const rows: (JalaliDate | null)[][] = []
  for (let i = 0; i < cells.length; i += 7) rows.push(cells.slice(i, i + 7))
  return rows
})

const same = (a: JalaliDate | null | undefined, b: JalaliDate | null | undefined) =>
  !!a && !!b && a.jy === b.jy && a.jm === b.jm && a.jd === b.jd

function dayDisabled(day: JalaliDate): boolean {
  return outOfRange(jalaliToIso(day)) !== null
}

async function focusActive() {
  await nextTick()
  grid.value?.querySelector<HTMLButtonElement>('button[data-active="true"]')?.focus()
}

async function openCalendar() {
  if (props.disabled || props.readonly) return
  const start = selected.value ?? todayJ.value ?? { jy: 1405, jm: 1, jd: 1 }
  view.value = { jy: start.jy, jm: start.jm }
  active.value = start
  open.value = true
  await focusActive()
}

function closeCalendar(returnFocus = true) {
  open.value = false
  if (returnFocus) toggle.value?.focus()
}

function moveActive(next: JalaliDate) {
  active.value = next
  view.value = { jy: next.jy, jm: next.jm }
  void focusActive()
}

function pick(day: JalaliDate) {
  if (dayDisabled(day)) return
  set(jalaliToIso(day))
  closeCalendar()
}

function shiftMonth(delta: number) {
  const next = addJalaliMonths({ ...view.value, jd: 1 }, delta)
  view.value = { jy: next.jy, jm: next.jm }
  active.value = addJalaliMonths(active.value, delta)
}

function onGridKeydown(event: KeyboardEvent) {
  const a = active.value
  let next: JalaliDate | null = null
  switch (event.key) {
    case 'ArrowLeft':
      next = addJalaliDays(a, 1) // راست‌به‌چپ: چپ یعنی بعد
      break
    case 'ArrowRight':
      next = addJalaliDays(a, -1)
      break
    case 'ArrowDown':
      next = addJalaliDays(a, 7)
      break
    case 'ArrowUp':
      next = addJalaliDays(a, -7)
      break
    case 'PageDown':
      next = addJalaliMonths(a, 1)
      break
    case 'PageUp':
      next = addJalaliMonths(a, -1)
      break
    case 'Home':
      next = addJalaliDays(a, -jalaliWeekday(a))
      break
    case 'End':
      next = addJalaliDays(a, 6 - jalaliWeekday(a))
      break
    case 'Escape':
      event.preventDefault()
      event.stopPropagation()
      closeCalendar()
      return
    default:
      return
  }
  event.preventDefault()
  moveActive(next)
}

function onDocumentPointerDown(event: PointerEvent) {
  if (root.value && event.target instanceof Node && !root.value.contains(event.target)) closeCalendar(false)
}

watch(open, (isOpen) => {
  if (isOpen) document.addEventListener('pointerdown', onDocumentPointerDown)
  else document.removeEventListener('pointerdown', onDocumentPointerDown)
})

onBeforeUnmount(() => document.removeEventListener('pointerdown', onDocumentPointerDown))

const ariaDescribedBy = computed(
  () => [props.describedBy, errorText.value ? errorId : null].filter(Boolean).join(' ') || undefined,
)

/** پیام مانع ثبت یا null (والد پیش از ارسال صدا می‌زند). */
function validate(): string | null {
  if (focused.value) commit()
  if (errorText.value) return errorText.value
  if (props.required && !props.modelValue) {
    const label = props.label ?? props.ariaLabel
    return label ? message('E_REQUIRED', { label }) : message('E_VALIDATION')
  }
  return null
}

defineExpose({ validate, focus: () => input.value?.focus() })
</script>

<template>
  <div ref="root" class="relative">
    <div class="flex flex-wrap items-stretch gap-2">
      <div class="relative min-w-[9rem] flex-1">
        <input
          :id="inputId"
          ref="input"
          type="text"
          inputmode="numeric"
          dir="ltr"
          autocomplete="off"
          spellcheck="false"
          :value="text"
          :placeholder="placeholder"
          :disabled="disabled"
          :readonly="readonly"
          :required="required"
          :aria-label="ariaLabel"
          :aria-invalid="invalid || errorText !== null || undefined"
          :aria-describedby="ariaDescribedBy"
          data-numeric
          class="min-h-11 w-full rounded-lg border bg-surface-container-lowest px-3 text-end text-on-surface outline-none focus:border-primary focus:ring-1 focus:ring-primary disabled:opacity-60"
          :class="invalid || errorText ? 'border-error' : 'border-outline-variant'"
          @focus="onFocus"
          @blur="onBlur"
          @input="onInput"
          @keydown="onInputKeydown"
        />
      </div>

      <button
        ref="toggle"
        type="button"
        :disabled="disabled || readonly"
        :aria-expanded="open"
        :aria-controls="popoverId"
        class="inline-flex min-h-11 items-center gap-1 rounded-lg border border-outline-variant px-3 text-sm text-on-surface hover:bg-surface-container"
        @click="open ? closeCalendar() : openCalendar()"
      >
        تقویم
        <AppIcon :name="open ? 'chevron-up' : 'chevron-down'" class="h-4 w-4" />
      </button>

      <template v-if="chips && !readonly">
        <button
          type="button"
          :disabled="disabled"
          class="min-h-11 rounded-full border px-3 text-sm"
          :class="modelValue === today ? 'border-primary bg-primary/10 text-primary' : 'border-outline-variant text-on-surface hover:bg-surface-container'"
          :aria-pressed="modelValue === today"
          @click="set(today)"
        >امروز</button>
        <button
          v-if="yesterday"
          type="button"
          :disabled="disabled"
          class="min-h-11 rounded-full border px-3 text-sm"
          :class="modelValue === yesterday ? 'border-primary bg-primary/10 text-primary' : 'border-outline-variant text-on-surface hover:bg-surface-container'"
          :aria-pressed="modelValue === yesterday"
          @click="set(yesterday)"
        >دیروز</button>
      </template>
    </div>

    <p v-if="errorText" :id="errorId" class="mt-1 flex items-start gap-1 text-xs text-error">
      <svg class="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" focusable="false"><use :href="'/icons.svg#error'" /></svg>
      <span>{{ errorText }}</span>
    </p>

    <!-- تقویم ماهانه -->
    <div
      v-if="open"
      :id="popoverId"
      role="dialog"
      aria-modal="false"
      :aria-labelledby="monthLabelId"
      class="absolute top-full z-40 mt-1 w-[21rem] max-w-[calc(100vw-2rem)] rounded-xl border border-outline-variant bg-surface-container-lowest p-3 text-on-surface shadow-lg"
      data-no-print
    >
      <div class="mb-2 flex items-center justify-between gap-2">
        <button
          type="button"
          class="grid h-11 w-11 place-items-center rounded-lg hover:bg-surface-container"
          @click="shiftMonth(-1)"
        >
          <AppIcon name="arrow-right" class="h-4 w-4" label="ماه قبل" />
        </button>
        <span :id="monthLabelId" class="font-semibold" aria-live="polite">{{ monthTitle }}</span>
        <button
          type="button"
          class="grid h-11 w-11 place-items-center rounded-lg hover:bg-surface-container"
          @click="shiftMonth(1)"
        >
          <AppIcon name="arrow-left" class="h-4 w-4" label="ماه بعد" />
        </button>
      </div>

      <table ref="grid" role="grid" :aria-labelledby="monthLabelId" class="w-full table-fixed border-collapse text-center" @keydown="onGridKeydown">
        <thead>
          <tr>
            <th v-for="(d, i) in JALALI_WEEKDAYS_SHORT" :key="d" scope="col" class="pb-1 text-xs font-medium text-on-surface-variant">
              <abbr :title="JALALI_WEEKDAYS[i]" class="no-underline">{{ d }}</abbr>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(week, w) in weeks" :key="w">
            <td
              v-for="(day, i) in week"
              :key="i"
              role="gridcell"
              :aria-selected="day ? same(day, selected) : undefined"
              class="p-0.5"
            >
              <button
                v-if="day"
                type="button"
                :tabindex="same(day, active) ? 0 : -1"
                :data-active="same(day, active)"
                :disabled="dayDisabled(day)"
                :aria-current="same(day, todayJ) ? 'date' : undefined"
                :aria-label="toPersianDigits(`${day.jd} ${JALALI_MONTHS[day.jm - 1]} ${day.jy}`)"
                class="h-11 w-full min-w-10 rounded-lg text-sm"
                :class="same(day, selected)
                  ? 'bg-primary font-semibold text-on-primary'
                  : same(day, todayJ)
                    ? 'border border-primary text-primary hover:bg-primary/10'
                    : 'hover:bg-surface-container'"
                data-numeric
                @click="pick(day)"
                @focus="active = day"
              >{{ toPersianDigits(day.jd) }}</button>
            </td>
          </tr>
        </tbody>
      </table>

      <div class="mt-2 flex justify-between gap-2 border-t border-outline-variant pt-2">
        <button type="button" class="min-h-11 rounded-lg px-3 text-sm text-primary hover:bg-primary/10" @click="pick(todayJ ?? active)">امروز</button>
        <button type="button" class="min-h-11 rounded-lg px-3 text-sm text-on-surface-variant hover:bg-surface-container" @click="closeCalendar()">بستن</button>
      </div>
    </div>
  </div>
</template>
