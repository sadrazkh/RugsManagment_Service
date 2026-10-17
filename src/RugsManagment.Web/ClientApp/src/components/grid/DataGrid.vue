<script setup lang="ts">
/**
 * جدول اکسل‌مانند (G.1): سرستون و ستون اول یخ‌زده، فقط ردیف‌های دیده‌شده رندر می‌شوند، `role="grid"` با شمارهٔ ردیف و
 * ستون (H.7)، و `data-hotkeys-off` تا میان‌برهای «n» و «/» پوسته وسط تایپ کار نکنند. زیر ۶۴۰ پیکسل همان داده به‌صورت
 * کارت (`GridCardList`) دیده می‌شود.
 *
 * همهٔ رفتار در `useGridController` است؛ این‌جا فقط چیدمان: نوار ابزار («برگرد»، وضعیت ذخیره)، نوار پیش‌نویس، نوار
 * پرسش (دو دکمهٔ ۴۴ پیکسلی؛ Enter پاسخ اول، Esc پاسخ دوم)، نوار مشکل ردیف (تعارض، شمارهٔ آزاد)، سطر اطلاعات خانهٔ فعال
 * (مبلغ به حروف در `aria-live`)، منوی «⋮» هر ردیف، پیام‌های دکمه‌دار («حذف شد [برگردان]»)، و زیر انگشت (`pointer:
 * coarse`) نوار ۵۶ پیکسلی «قبلی · بعدی · برگرد · بستن».
 */
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import Button from '@/components/ui/Button.vue'
import { message } from '@/lib/messages'
import { faDigits } from './gridParse'
import GridCardList from './GridCardList.vue'
import GridCell from './GridCell.vue'
import type { GridRowState } from './gridModel'
import type { GridController, GridNotice } from './useGridController'
import { useVirtualRows } from './useVirtualRows'

const props = withDefaults(
  defineProps<{
    ctrl: GridController
    /** نام دسترس‌پذیر جدول */
    label: string
    /** فوکوس اولیه روی خانهٔ فعال (صفحهٔ ورود) */
    autofocus?: boolean
    /** بیشترین ارتفاع ناحیهٔ جدول */
    maxHeight?: string
  }>(),
  { autofocus: false, maxHeight: '70vh' },
)

const emit = defineEmits<{ nearEnd: [] }>()

const ctrl = props.ctrl
const uid = useId()
const root = ref<HTMLElement | null>(null)
const scroller = ref<HTMLElement | null>(null)
const header = ref<HTMLElement | null>(null)
const toolbarStart = ref<HTMLElement | null>(null)
const restoreButton = ref<HTMLElement | null>(null)
const keepButton = ref<HTMLElement | null>(null)
const headerHeight = ref(44)
const hasFocus = ref(false)
const confirmingDiscard = ref(false)

/** پهنای ستون کنار ردیف (منوی «⋮» و وضعیت) */
const GUTTER = '3rem'

// ── کارت زیر ۶۴۰ پیکسل ─────────────────────────────────────────────────

const cardMode = ref(false)
let media: MediaQueryList | null = null
function onMedia() {
  cardMode.value = media?.matches ?? false
  ctrl.layout = cardMode.value ? 'cards' : 'table'
}

// ── ستون‌ها و ردیف‌ها ───────────────────────────────────────────────────

const columns = computed(() => ctrl.columns)
/**
 * پهنای ستون به `ch` است و `ch` به اندازه و وزن نوشتهٔ همان ظرف grid بسته است؛ پس سرستون و ردیف‌ها هر دو `text-sm`
 * با وزن عادی‌اند (برچسب سرستون خودش پررنگ است)، وگرنه ستون‌های سرستون و خانه‌ها هم‌پهنا نمی‌شدند و از هم جدا می‌افتادند.
 */
const template = computed(() => [GUTTER, ...columns.value.map((c) => `${Math.max(4, Number(c.width) || 8)}ch`)].join(' '))
const rowCount = computed(() => ctrl.rows.length)

const virtual = useVirtualRows({
  container: scroller,
  count: rowCount,
  pinned: computed(() => ctrl.nav.row),
  headerHeight,
})

function rowAt(index: number): GridRowState {
  return ctrl.rows[index]
}

// ── فوکوس و دیده شدن خانهٔ فعال ─────────────────────────────────────────

function revealActive() {
  if (cardMode.value) return
  virtual.reveal(ctrl.nav.row)
  const el = scroller.value
  const cell = el?.querySelector<HTMLElement>('[data-active-input]')?.closest<HTMLElement>('[role="gridcell"]')
  if (!el || !cell || ctrl.nav.col === 0) return
  // ستون‌های یخ‌زده (کنار ردیف و ستون اول) در راست‌به‌چپ سمت راست‌اند
  const frozen = el.querySelector<HTMLElement>('[data-frozen-end]')
  const box = el.getBoundingClientRect()
  const visibleRight = frozen ? frozen.getBoundingClientRect().left : box.right
  const rect = cell.getBoundingClientRect()
  if (rect.right > visibleRight) el.scrollBy({ left: rect.right - visibleRight })
  else if (rect.left < box.left) el.scrollBy({ left: rect.left - box.left })
}

watch(
  () => ctrl.focusTick,
  () => void nextTick(revealActive),
  { flush: 'post' },
)

// ردیف‌های آخر دیده شدند (جدول انبار صفحهٔ بعد را بار کند)
watch(
  () => virtual.window.value.end,
  (end) => {
    if (end >= rowCount.value - 5) emit('nearEnd')
  },
)

// نوار پیش‌نویس: «برگرداندن ردیف‌ها» دکمهٔ اصلی و فوکوس‌دار است (G.7)
watch(
  () => ctrl.draftsPending?.length ?? 0,
  (count, before) => {
    if (count > 0 && !before) void nextTick(() => restoreButton.value?.focus())
    if (count === 0) confirmingDiscard.value = false
  },
  { flush: 'post' },
)

function askDiscard() {
  confirmingDiscard.value = true
  // «نه، نگه دار» فوکوس دارد: پاک کردن هرگز یک‌ضربه‌ای نیست
  void nextTick(() => keepButton.value?.focus())
}

function discard() {
  confirmingDiscard.value = false
  ctrl.discardDrafts()
  focusGrid()
}

/** «برگرداندن ردیف‌ها»: نوار می‌رود، پس فوکوس به خانهٔ فعال جدول برمی‌گردد (نه به body). */
function restoreDrafts() {
  ctrl.restoreDrafts()
  focusGrid()
}

function focusGrid() {
  void nextTick(() => ctrl.activate(ctrl.nav.row, ctrl.nav.col))
}

function keep() {
  confirmingDiscard.value = false
  void nextTick(() => restoreButton.value?.focus())
}

/** Esc در حالت انتخاب: بیرون آمدن از جدول به نوار ابزار (تله‌ی صفحه‌کلید نشود). */
function onRootKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && !event.defaultPrevented && (event.target as HTMLElement).matches('[data-active-input]')) {
    event.preventDefault()
    const target = toolbarStart.value?.querySelector<HTMLElement>('button:not([disabled]), a')
    ;(target ?? root.value)?.focus()
  }
}

function onFocusIn() {
  hasFocus.value = true
}

function onFocusOut(event: FocusEvent) {
  const next = event.relatedTarget as Node | null
  if (!next || !root.value?.contains(next)) hasFocus.value = false
}

// ── نوار لمسی ───────────────────────────────────────────────────────────

function touchKey(key: string, shiftKey = false) {
  ctrl.onKeydown({ key, shiftKey })
}

function closeKeyboard() {
  ctrl.onKeydown({ key: 'Escape' })
  ;(document.activeElement as HTMLElement | null)?.blur()
}

// ── منوی ردیف ───────────────────────────────────────────────────────────

const menuStyle = ref<Record<string, string>>({})
const menuRow = computed(() => (ctrl.menuRowId ? ctrl.rows.find((r) => r.id === ctrl.menuRowId) ?? null : null))
watch(() => ctrl.menuRowId, (id) => {
  if (!id) return
  void nextTick(() => {
    const button = root.value?.querySelector<HTMLElement>('[data-active-input]')
    const rect = button?.getBoundingClientRect()
    if (rect) menuStyle.value = { top: `${rect.bottom + 4}px`, right: `${window.innerWidth - rect.right}px` }
    document.getElementById(`${uid}-menu`)?.querySelector<HTMLElement>('[role="menuitem"]')?.focus()
  })
})

function openMenu(row: GridRowState, event: MouseEvent | KeyboardEvent) {
  const button = event.currentTarget as HTMLElement
  const rect = button.getBoundingClientRect()
  menuStyle.value = { top: `${Math.round(rect.bottom + 4)}px`, right: `${Math.round(window.innerWidth - rect.right)}px` }
  ctrl.menuRowId = ctrl.menuRowId === row.id ? null : row.id
  if (ctrl.menuRowId) void nextTick(() => document.getElementById(`${uid}-menu`)?.querySelector<HTMLElement>('[role="menuitem"]')?.focus())
}

function closeMenu() {
  ctrl.menuRowId = null
}

function onMenuKeydown(event: KeyboardEvent) {
  const items = [...(document.getElementById(`${uid}-menu`)?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? [])]
  const at = items.indexOf(document.activeElement as HTMLElement)
  if (event.key === 'Escape') {
    event.preventDefault()
    closeMenu()
    focusGrid()
  } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault()
    const next = (at + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length
    items[next]?.focus()
  }
}

function onDocumentDown(event: MouseEvent) {
  if (!ctrl.menuRowId) return
  const menu = document.getElementById(`${uid}-menu`)
  const target = event.target as Node
  if (menu?.contains(target) || (target as HTMLElement).closest?.('[data-row-menu]')) return
  closeMenu()
}

function runNotice(notice: GridNotice) {
  ctrl.dismissNotice(notice.id)
  void notice.action?.run()
  // دکمهٔ پیام («برگردان») با خود پیام می‌رود؛ فوکوس به خانهٔ فعال جدول برگردد، نه به body
  focusGrid()
}

function statusOf(row: GridRowState): { icon: string; label: string; tone: string } | null {
  if (row.placeholder) return null
  if (row.saving) return { icon: 'spinner', label: message('I_SAVING'), tone: 'text-on-surface-variant' }
  if (row.status === 'conflict') return { icon: 'warning', label: message('E_GRID_CONFLICT'), tone: 'text-warning' }
  if (row.status === 'invalid') return { icon: 'error', label: row.rowError ?? message('E_VALIDATION'), tone: 'text-error' }
  if (row.dirty.length > 0) return { icon: 'dot', label: message('I_SAVING'), tone: 'text-secondary' }
  return null
}

// ── نوار مشکل ───────────────────────────────────────────────────────────

/**
 * ردیفی که نوار تعارض/شمارهٔ آزاد برایش است: ردیف فعال، وگرنه اولین ردیف در تعارض (تعارض تصمیم می‌خواهد و کاربر معمولاً
 * با Enter از همان ردیف رفته)، وگرنه اولین ردیف دارای پیشنهاد شماره.
 */
const issueRow = computed<GridRowState | null>(() => {
  const active = ctrl.activeRow
  if (active && (active.conflict || active.nextFree !== null || active.rowError)) return active
  return ctrl.rows.find((r) => r.conflict !== null) ?? ctrl.rows.find((r) => r.nextFree !== null) ?? null
})

/** نام ردیف نوار وقتی ردیف فعال نیست («فرش ۳۷۷۸»)، تا معلوم باشد تصمیم دربارهٔ کدام فرش است. */
const issueLabel = computed(() => {
  const row = issueRow.value
  if (!row || row === ctrl.activeRow) return ''
  const serial = ctrl.serialLabel(row)
  return serial ? message('I_GRID_ROW_LABEL', { serial }) : ''
})

/** دکمه‌های نوار با خود نوار می‌روند؛ پس از تصمیم، فوکوس به خانهٔ فعال جدول برگردد (نه به body). */
function resolveIssue(choice: 'mine' | 'theirs') {
  if (!issueRow.value) return
  ctrl.resolveConflict(issueRow.value.id, choice)
  focusGrid()
}

function acceptIssueNextFree() {
  if (!issueRow.value) return
  ctrl.acceptNextFree(issueRow.value.id)
  focusGrid()
}

const otherProblems = computed(() => {
  const count = ctrl.counts.invalid + ctrl.counts.conflict
  const own = issueRow.value && (issueRow.value.status === 'invalid' || issueRow.value.status === 'conflict') ? 1 : 0
  return count - own
})

// ── وضعیت ذخیره ─────────────────────────────────────────────────────────

const saveText = computed(() => {
  const state = ctrl.saveState
  if (state.phase === 'saving') return message('I_SAVING')
  if (state.phase === 'retrying') return `${message('I_RETRYING')} · ${message('I_OFFLINE')}`
  if (ctrl.unsavedCount > 0) return ''
  if (state.phase === 'saved') return message('I_SAVED')
  return ''
})

onMounted(() => {
  media = window.matchMedia('(max-width: 640px)')
  onMedia()
  media.addEventListener('change', onMedia)
  document.addEventListener('mousedown', onDocumentDown)
  if (header.value) headerHeight.value = header.value.offsetHeight
  // پیش‌نویس جامانده‌ای که پیش از mount پیدا شده (بار اول صفحه): «برگرداندن ردیف‌ها» فوکوس می‌گیرد، نه جدول (G.7)
  if ((ctrl.draftsPending?.length ?? 0) > 0) void nextTick(() => restoreButton.value?.focus())
  else if (props.autofocus) void nextTick(() => ctrl.activate(ctrl.nav.row, ctrl.nav.col))
})

onBeforeUnmount(() => {
  media?.removeEventListener('change', onMedia)
  document.removeEventListener('mousedown', onDocumentDown)
})
</script>

<template>
  <section
    ref="root"
    tabindex="-1"
    data-grid-root
    data-hotkeys-off
    class="flex flex-col gap-2"
    @keydown="onRootKeydown"
    @focusin="onFocusIn"
    @focusout="onFocusOut"
  >
    <!-- نوار ابزار -->
    <div class="flex flex-wrap items-center gap-2">
      <div ref="toolbarStart" class="flex items-center gap-2">
        <Button icon="undo" :disabled="ctrl.undoCount === 0" @mousedown.prevent @click="ctrl.undo()">
          برگرد
          <kbd class="hidden rounded border border-current/30 px-1 text-xs opacity-70 pointer-fine:inline" dir="ltr" aria-hidden="true">Ctrl+Z</kbd>
        </Button>
      </div>
      <slot name="toolbar" />
      <p class="ms-auto flex min-h-11 items-center gap-1.5 text-sm text-on-surface-variant" role="status" aria-live="polite">
        <template v-if="ctrl.saveState.phase === 'saving'">
          <span class="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent" aria-hidden="true"></span>
          {{ saveText }}
        </template>
        <template v-else-if="saveText && ctrl.saveState.phase === 'saved'">
          <AppIcon name="check" class="h-4 w-4 text-success" />
          {{ saveText }}
        </template>
        <template v-else-if="ctrl.unsavedCount > 0 && ctrl.saveState.phase !== 'retrying'">
          <AppIcon name="edit" class="h-4 w-4" />
          {{ message('I_GRID_UNSAVED', { n: ctrl.unsavedCount }) }}
        </template>
      </p>
    </div>

    <!-- قطع شبکه (H.3) -->
    <div
      v-if="ctrl.saveState.phase === 'retrying'"
      class="flex flex-wrap items-center gap-3 rounded-xl border border-warning/40 bg-surface-container-low px-4 py-3 text-sm text-on-surface"
      role="alert"
    >
      <AppIcon name="warning" class="h-5 w-5 shrink-0 text-warning" />
      <p class="min-w-0 flex-1">
        {{ message('I_RETRYING') }} · {{ message('I_OFFLINE') }}
        <span v-if="ctrl.saveState.retryIn > 0" class="text-on-surface-variant">({{ faDigits(ctrl.saveState.retryIn) }} ثانیهٔ دیگر)</span>
      </p>
      <Button variant="primary" icon="upload" @click="ctrl.retryNow()">الان دوباره بفرست</Button>
    </div>

    <!-- پیش‌نویس جامانده (G.7) -->
    <div
      v-if="ctrl.draftsPending && ctrl.draftsPending.length > 0"
      class="flex flex-wrap items-center gap-3 rounded-xl border border-secondary/50 bg-secondary-container/40 px-4 py-3 text-sm text-on-surface"
      role="region"
      :aria-label="message('I_GRID_DRAFTS_FOUND', { n: ctrl.draftsPending.length })"
    >
      <template v-if="!confirmingDiscard">
        <AppIcon name="info" class="h-5 w-5 shrink-0" />
        <p class="min-w-0 flex-1">{{ message('I_GRID_DRAFTS_FOUND', { n: ctrl.draftsPending.length }) }}</p>
        <button
          ref="restoreButton"
          type="button"
          class="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 text-sm font-semibold text-on-primary hover:bg-primary-hover"
          @click="restoreDrafts"
        >
          <AppIcon name="undo" class="h-5 w-5" />
          برگرداندن ردیف‌ها
        </button>
        <Button variant="ghost" icon="trash" @click="askDiscard">پاک کردن</Button>
      </template>
      <template v-else>
        <AppIcon name="warning" class="h-5 w-5 shrink-0 text-error" />
        <p class="min-w-0 flex-1 font-medium" role="alert">{{ message('Q_GRID_DRAFTS_DISCARD', { n: ctrl.draftsPending.length }) }}</p>
        <Button variant="danger" icon="trash" @click="discard">پاک کن</Button>
        <button
          ref="keepButton"
          type="button"
          class="inline-flex min-h-11 items-center rounded-lg border border-outline-variant px-4 text-sm text-on-surface hover:bg-surface-container"
          @click="keep"
        >نه، نگه دار</button>
      </template>
    </div>

    <!-- پرسش (سانت؟ ۱۰٬۰۰۰؟ بازه/تعداد، یک پرسش برای چسباندن) -->
    <div
      v-if="ctrl.question"
      class="flex flex-wrap items-center gap-3 rounded-xl border border-secondary/50 bg-secondary-container/50 px-4 py-3"
      role="group"
      :aria-label="ctrl.question.message"
    >
      <AppIcon name="info" class="h-5 w-5 shrink-0 text-on-secondary-container" />
      <p class="min-w-0 flex-1 text-sm font-medium text-on-surface" aria-live="assertive">{{ ctrl.question.message }}</p>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="(choice, index) in ctrl.question.choices"
          :key="index"
          type="button"
          class="inline-flex min-h-11 items-center gap-1 rounded-lg px-4 text-sm"
          :class="index === 0 ? 'bg-primary font-semibold text-on-primary hover:bg-primary-hover' : 'border border-outline-variant bg-surface-container-lowest text-on-surface hover:bg-surface-container'"
          @mousedown.prevent
          @click="ctrl.answer(index === 0 ? 0 : 1)"
        >
          {{ choice.label }}
          <kbd class="ms-1 hidden rounded border border-current/30 px-1 text-xs opacity-70 pointer-fine:inline" dir="ltr" aria-hidden="true">{{ index === 0 ? 'Enter' : 'Esc' }}</kbd>
        </button>
      </div>
    </div>

    <!-- مشکل ردیف: تعارض، شمارهٔ آزاد، خطای ردیف -->
    <div
      v-if="issueRow && issueRow.conflict"
      class="flex flex-wrap items-center gap-3 rounded-xl border border-warning/50 bg-surface-container-low px-4 py-3 text-sm"
      role="alert"
    >
      <AppIcon name="warning" class="h-5 w-5 shrink-0 text-warning" />
      <div class="min-w-0 flex-1">
        <p v-if="issueLabel" class="text-on-surface-variant"><bdi>{{ issueLabel }}</bdi></p>
        <p class="font-medium text-on-surface">{{ message('E_GRID_CONFLICT') }}</p>
        <p v-for="line in ctrl.conflictLines(issueRow)" :key="line" class="text-on-surface-variant">{{ line }}</p>
      </div>
      <Button variant="primary" @click="resolveIssue('mine')">نوشتهٔ من بماند</Button>
      <Button @click="resolveIssue('theirs')">نوشتهٔ او را ببینم</Button>
    </div>
    <div
      v-else-if="issueRow && issueRow.nextFree !== null"
      class="flex flex-wrap items-center gap-3 rounded-xl border border-secondary/50 bg-secondary-container/40 px-4 py-3 text-sm"
      role="alert"
    >
      <AppIcon name="info" class="h-5 w-5 shrink-0" />
      <div class="min-w-0 flex-1">
        <p v-if="issueLabel" class="text-on-surface-variant"><bdi>{{ issueLabel }}</bdi></p>
        <p v-if="issueRow.errors['rug.serial']" class="text-error">{{ issueRow.errors['rug.serial'] }}</p>
        <p class="font-medium text-on-surface">{{ message('Q_SERIAL_NEXT_FREE', { serial: faDigits(issueRow.nextFree) }) }}</p>
      </div>
      <Button variant="primary" @click="acceptIssueNextFree">بله</Button>
      <Button @click="ctrl.editSerialOf(issueRow.id)">خودم می‌نویسم</Button>
    </div>
    <div
      v-else-if="issueRow && issueRow.rowError"
      class="flex items-center gap-3 rounded-xl border border-error/40 bg-error-container/40 px-4 py-3 text-sm text-on-surface"
      role="alert"
    >
      <AppIcon name="error" class="h-5 w-5 shrink-0 text-error" />
      <p class="min-w-0 flex-1">{{ issueRow.rowError }}</p>
    </div>
    <div
      v-if="otherProblems > 0"
      class="flex flex-wrap items-center gap-3 rounded-xl border border-error/40 bg-surface-container-low px-4 py-2 text-sm"
      role="status"
    >
      <AppIcon name="error" class="h-5 w-5 shrink-0 text-error" />
      <p class="min-w-0 flex-1">{{ message('E_GRID_ROWS_NOT_SAVED', { n: otherProblems }) }}</p>
      <Button icon="search" @click="ctrl.showProblem()">نشانم بده</Button>
    </div>

    <!-- سطر اطلاعات خانهٔ فعال -->
    <p class="flex min-h-6 flex-wrap items-center gap-x-2 text-sm text-on-surface-variant" aria-live="polite">
      <template v-if="ctrl.activeColumn && hasFocus">
        <span class="font-medium text-on-surface">{{ ctrl.activeColumn.label }}</span>
        <span v-if="ctrl.editError" class="flex items-center gap-1 text-error"><AppIcon name="error" class="h-4 w-4" />{{ ctrl.editError }}</span>
        <span v-else-if="ctrl.activeRow && ctrl.activeRow.errors[ctrl.activeColumn.key]" class="flex items-center gap-1 text-error">
          <AppIcon name="error" class="h-4 w-4" />{{ ctrl.activeRow.errors[ctrl.activeColumn.key] }}
        </span>
        <span v-else-if="ctrl.wordsLine">{{ ctrl.wordsLine }}</span>
        <span v-if="ctrl.activeRow && ctrl.activeRow.warning" class="flex items-center gap-1 text-warning">
          <AppIcon name="warning" class="h-4 w-4" />{{ ctrl.activeRow.warning }}
        </span>
      </template>
    </p>

    <!-- گوشی: کارت‌ها -->
    <GridCardList v-if="cardMode" :ctrl="ctrl" :label="label" @menu="openMenu" />

    <!-- جدول -->
    <div
      v-else
      ref="scroller"
      role="grid"
      :aria-label="label"
      :aria-rowcount="rowCount + 1"
      :aria-colcount="columns.length + 1"
      :aria-multiselectable="true"
      class="relative overflow-auto overscroll-contain rounded-xl border border-outline-variant bg-surface-container-lowest"
      :style="{ maxHeight }"
    >
      <div
        ref="header"
        role="row"
        aria-rowindex="1"
        class="sticky top-0 z-30 grid min-w-full border-b border-outline-variant bg-surface-container text-sm text-on-surface"
        :style="{ gridTemplateColumns: template, width: 'max-content' }"
      >
        <div role="columnheader" aria-colindex="1" class="sticky start-0 z-10 bg-surface-container">
          <span class="sr-only">کارهای ردیف</span>
        </div>
        <div
          v-for="(column, c) in columns"
          :key="column.key"
          role="columnheader"
          :aria-colindex="c + 2"
          :data-frozen-end="c === 0 ? '' : undefined"
          class="flex min-h-11 items-center gap-1 border-s border-outline-variant/60 px-2 py-1 leading-tight"
          :class="c === 0 ? 'sticky z-10 bg-surface-container' : ''"
          :style="c === 0 ? { insetInlineStart: GUTTER } : undefined"
        >
          <span class="truncate font-medium" :title="column.label">{{ column.label }}</span>
          <span v-if="column.required" class="text-error" aria-hidden="true">*</span>
        </div>
      </div>

      <div role="rowgroup" class="relative" :style="{ height: `${virtual.totalHeight.value}px`, width: 'max-content', minWidth: '100%' }">
        <div
          v-for="i in virtual.indices.value"
          :key="rowAt(i).id"
          role="row"
          data-grid-row
          :aria-rowindex="i + 2"
          :aria-invalid="rowAt(i).status === 'invalid' || undefined"
          class="absolute inset-x-0 grid h-11 text-sm pointer-coarse:h-12"
          :class="rowAt(i).placeholder ? 'text-on-surface-variant' : ''"
          :style="{ top: `${i * virtual.rowHeight.value}px`, gridTemplateColumns: template, width: 'max-content', minWidth: '100%' }"
        >
          <!-- کنار ردیف: وضعیت و منوی «⋮» -->
          <div
            role="gridcell"
            aria-colindex="1"
            class="sticky start-0 z-10 flex h-full items-center justify-center border-b border-outline-variant/50 bg-surface-container-low"
          >
            <template v-for="s in [statusOf(rowAt(i))]" :key="'status'">
              <template v-if="s">
              <span
                v-if="s.icon === 'spinner'"
                class="absolute top-1 end-1 h-2.5 w-2.5 animate-spin rounded-full border border-current border-t-transparent text-on-surface-variant"
                :title="s.label"
                aria-hidden="true"
              ></span>
              <span v-else-if="s.icon === 'dot'" class="absolute top-1.5 end-1.5 h-2 w-2 rounded-full bg-secondary" :title="s.label" aria-hidden="true"></span>
              <AppIcon v-else :name="s.icon" class="absolute top-0.5 end-0.5 h-3.5 w-3.5" :class="s.tone" />
              </template>
            </template>
            <button
              v-if="!rowAt(i).placeholder"
              type="button"
              tabindex="-1"
              data-row-menu
              class="grid h-11 w-11 place-items-center rounded-lg text-on-surface-variant hover:bg-surface-container"
              :aria-label="`کارهای ردیف ${ctrl.serialLabel(rowAt(i))}`"
              aria-haspopup="menu"
              :aria-expanded="ctrl.menuRowId === rowAt(i).id"
              @mousedown.prevent
              @click="openMenu(rowAt(i), $event)"
            >
              <AppIcon name="dots" class="h-5 w-5" />
            </button>
          </div>
          <GridCell
            v-for="(column, c) in columns"
            :key="column.key"
            :ctrl="ctrl"
            :row="rowAt(i)"
            :row-index="i"
            :column="column"
            :col-index="c"
            :frozen="c === 0"
            :style="c === 0 ? { insetInlineStart: GUTTER } : undefined"
          />
        </div>
      </div>
    </div>

    <p class="hidden text-xs text-on-surface-variant pointer-fine:block">
      {{ message('I_GRID_KEYS_HINT', { tab: 'Tab', enter: 'Enter', esc: 'Esc' }) }}
    </p>

    <slot name="footer" />

    <!-- منوی ردیف -->
    <Teleport to="body">
      <div
        v-if="menuRow"
        :id="`${uid}-menu`"
        role="menu"
        :aria-label="`کارهای ردیف ${ctrl.serialLabel(menuRow)}`"
        class="fixed z-[60] min-w-48 rounded-xl border border-outline-variant bg-surface-container-lowest py-1 shadow-lg"
        :style="menuStyle"
        @keydown="onMenuKeydown"
      >
        <a
          v-if="menuRow.rugId"
          role="menuitem"
          :href="`/Rugs/Details/${menuRow.rugId}`"
          class="flex min-h-11 items-center gap-2 px-4 text-sm text-on-surface hover:bg-surface-container focus:bg-surface-container focus:outline-none"
        >
          <AppIcon name="rug" class="h-5 w-5" />
          صفحهٔ فرش
        </a>
        <button
          v-if="ctrl.canDeleteRow(menuRow)"
          type="button"
          role="menuitem"
          class="flex min-h-11 w-full items-center gap-2 px-4 text-sm text-error hover:bg-error-container/40 focus:bg-error-container/40 focus:outline-none"
          @click="ctrl.deleteRow(menuRow.id)"
        >
          <AppIcon name="trash" class="h-5 w-5" />
          {{ menuRow.rugId ? 'حذف این فرش' : 'حذف این ردیف' }}
        </button>
        <p v-if="!menuRow.rugId && !ctrl.canDeleteRow(menuRow)" class="px-4 py-2 text-sm text-on-surface-variant">—</p>
      </div>
    </Teleport>

    <!-- پیام‌های دکمه‌دار («حذف شد [برگردان]»، «پاک شد [برگردان]») -->
    <Teleport to="body">
      <div class="pointer-events-none fixed bottom-20 end-4 z-[55] flex w-[min(24rem,calc(100vw-2rem))] flex-col gap-2" role="status" aria-live="polite">
        <div
          v-for="notice in ctrl.notices"
          :key="notice.id"
          class="pointer-events-auto flex items-center gap-3 rounded-xl border bg-surface-container-lowest px-4 py-2 text-sm text-on-surface shadow-lg"
          :class="{
            'border-error/50': notice.kind === 'error',
            'border-warning/50': notice.kind === 'warning',
            'border-success/50': notice.kind === 'success',
            'border-outline-variant': notice.kind === 'info',
          }"
        >
          <AppIcon
            :name="notice.kind === 'error' ? 'error' : notice.kind === 'warning' ? 'warning' : notice.kind === 'success' ? 'success' : 'info'"
            class="h-5 w-5 shrink-0"
            :class="notice.kind === 'error' ? 'text-error' : notice.kind === 'warning' ? 'text-warning' : notice.kind === 'success' ? 'text-success' : 'text-on-surface-variant'"
          />
          <p class="min-w-0 flex-1">{{ notice.text }}</p>
          <button
            v-if="notice.action"
            type="button"
            class="min-h-11 shrink-0 rounded-lg px-3 font-semibold text-primary hover:bg-primary/10"
            @click="runNotice(notice)"
          >{{ notice.action.label }}</button>
          <button
            type="button"
            class="grid h-11 w-11 shrink-0 place-items-center rounded-lg text-on-surface-variant hover:bg-surface-container"
            aria-label="بستن"
            @click="ctrl.dismissNotice(notice.id)"
          >
            <AppIcon name="close" class="h-4 w-4" />
          </button>
        </div>
      </div>
    </Teleport>

    <!-- زیر انگشت: نوار ۵۶ پیکسلی بالای صفحه‌کلید (H.6 ب) -->
    <div
      v-if="hasFocus"
      class="fixed inset-x-0 bottom-0 z-50 hidden gap-2 border-t border-outline-variant bg-surface-container px-3 py-1 pb-safe pointer-coarse:flex"
    >
      <slot name="touch-actions" />
      <button type="button" class="min-h-14 flex-1 rounded-lg bg-surface-container-lowest text-sm text-on-surface" @mousedown.prevent @click="touchKey('Tab', true)">
        قبلی
      </button>
      <button type="button" class="min-h-14 flex-1 rounded-lg bg-primary text-sm font-semibold text-on-primary" @mousedown.prevent @click="touchKey('Tab')">
        بعدی
      </button>
      <button type="button" class="min-h-14 flex-1 rounded-lg bg-surface-container-lowest text-sm text-on-surface" @mousedown.prevent @click="ctrl.undo()">
        برگرد
      </button>
      <button type="button" class="min-h-14 flex-1 rounded-lg bg-surface-container-lowest text-sm text-on-surface" @mousedown.prevent @click="closeKeyboard">
        بستن
      </button>
    </div>
  </section>
</template>
