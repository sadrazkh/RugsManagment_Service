/**
 * حرکت در جدول با صفحه‌کلید، مثل اکسل (G.3) — منطق خالص و آزمون‌پذیر؛ DataGrid فقط رویداد را به این‌جا می‌دهد.
 *
 * - راست‌به‌چپ: ← ستون بعد، → ستون قبل؛ ↑/↓ ردیف.
 * - Tab خانهٔ بعد و Shift+Tab خانهٔ قبل؛ خانهٔ فقط‌خواندنی و ستونی که `tabStop: false` دارد ایستگاه Tab نیست. ته
 *   ردیف، Tab به اولین ایستگاه ردیف بعد می‌رود.
 * - Enter ثبت می‌کند و پایین می‌رود (یا با ترجیح «راست» به خانهٔ بعد). **بازگشت پس از Tab:** اگر کاربر با چند Tab در
 *   ردیف جلو رفته، Enter به ردیف بعد و همان ستونی می‌رود که Tab زدن از آن شروع شد.
 * - حالت خانه: `nav` (خانه انتخاب است؛ تایپ کردن ویرایش را شروع می‌کند)، `enter` (با تایپ شروع شده؛ پیکان‌ها ثبت و جابه‌جا
 *   می‌کنند)، `edit` (با F2 یا دوبار کلیک؛ پیکان چپ/راست مکان‌نما را جابه‌جا می‌کند)، `ask` (پرسشی بی‌جواب مانده؛ Enter
 *   پاسخ اول، Esc پاسخ دوم، بقیه بسته).
 */
import { reactive } from 'vue'

export interface NavState {
  row: number
  col: number
  /** ستونی که رشتهٔ Tab از آن شروع شد؛ null یعنی رشته‌ای در کار نیست */
  runStart: number | null
}

export interface NavColumn {
  /** ایستگاه Tab است (نه فقط‌خواندنی، نه `tabStop: false`) */
  tabStop: boolean
}

export interface NavShape {
  /** تعداد ردیف‌ها (با ردیف خالیِ ته جدول، اگر هست) */
  rows: number
  cols: readonly NavColumn[]
}

export type EnterDirection = 'down' | 'right'

export type CellMode = 'nav' | 'enter' | 'edit' | 'ask'

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max)
}

export function firstTabStop(cols: readonly NavColumn[]): number {
  const i = cols.findIndex((c) => c.tabStop)
  return i >= 0 ? i : 0
}

export function lastTabStop(cols: readonly NavColumn[]): number {
  for (let i = cols.length - 1; i >= 0; i--) if (cols[i].tabStop) return i
  return Math.max(0, cols.length - 1)
}

/** اولین ایستگاه Tab بعد از `col` در همین ردیف، یا null. */
export function nextTabStop(cols: readonly NavColumn[], col: number): number | null {
  for (let i = col + 1; i < cols.length; i++) if (cols[i].tabStop) return i
  return null
}

/** اولین ایستگاه Tab پیش از `col` در همین ردیف، یا null. */
export function prevTabStop(cols: readonly NavColumn[], col: number): number | null {
  for (let i = col - 1; i >= 0; i--) if (cols[i].tabStop) return i
  return null
}

/**
 * Tab / Shift+Tab. ته ردیف به اولین ایستگاه ردیف بعد (و سر ردیف با Shift+Tab به آخرین ایستگاه ردیف قبل) می‌رود؛ اگر
 * ردیفی نمانده، `null` (صدازننده شاید ردیف تازه بسازد). رشتهٔ Tab از خانهٔ فعلی شروع می‌شود و با رفتن به ردیف دیگر تمام.
 */
export function navTab(state: NavState, shape: NavShape, shift: boolean): NavState | null {
  if (!shift) {
    const next = nextTabStop(shape.cols, state.col)
    if (next !== null) return { row: state.row, col: next, runStart: state.runStart ?? state.col }
    if (state.row + 1 >= shape.rows) return null
    return { row: state.row + 1, col: firstTabStop(shape.cols), runStart: null }
  }
  const prev = prevTabStop(shape.cols, state.col)
  if (prev !== null) return { row: state.row, col: prev, runStart: state.runStart }
  if (state.row === 0) return null
  return { row: state.row - 1, col: lastTabStop(shape.cols), runStart: null }
}

/**
 * Enter / Shift+Enter. پایین: اگر رشتهٔ Tab در کار است به ستون شروع رشته در ردیف بعد، وگرنه همان ستون. راست: مثل Tab،
 * ولی ته ردیف به ردیف بعد و ستون شروع (رشتهٔ Enter یا Tab) برمی‌گردد. ته جدول → `null`.
 */
export function navEnter(state: NavState, shape: NavShape, direction: EnterDirection, shift: boolean): NavState | null {
  if (shift) {
    if (state.row === 0) return null
    return { row: state.row - 1, col: state.col, runStart: null }
  }
  if (direction === 'right') {
    const next = nextTabStop(shape.cols, state.col)
    if (next !== null) return { row: state.row, col: next, runStart: state.runStart ?? state.col }
    if (state.row + 1 >= shape.rows) return null
    return { row: state.row + 1, col: state.runStart ?? firstTabStop(shape.cols), runStart: null }
  }
  if (state.row + 1 >= shape.rows) return null
  return { row: state.row + 1, col: state.runStart ?? state.col, runStart: null }
}

export type ArrowKey = 'ArrowLeft' | 'ArrowRight' | 'ArrowUp' | 'ArrowDown'

/** پیکان‌ها در راست‌به‌چپ: ← ستون بعد، → ستون قبل. رشتهٔ Tab تمام می‌شود. */
export function navArrow(state: NavState, shape: NavShape, key: ArrowKey): NavState {
  const lastCol = Math.max(0, shape.cols.length - 1)
  const lastRow = Math.max(0, shape.rows - 1)
  switch (key) {
    case 'ArrowLeft':
      return { row: state.row, col: clamp(state.col + 1, 0, lastCol), runStart: null }
    case 'ArrowRight':
      return { row: state.row, col: clamp(state.col - 1, 0, lastCol), runStart: null }
    case 'ArrowUp':
      return { row: clamp(state.row - 1, 0, lastRow), col: state.col, runStart: null }
    case 'ArrowDown':
      return { row: clamp(state.row + 1, 0, lastRow), col: state.col, runStart: null }
  }
}

/** Home/End (و با Ctrl اول/آخر جدول). */
export function navEdge(state: NavState, shape: NavShape, edge: 'home' | 'end', ctrl: boolean): NavState {
  const col = edge === 'home' ? 0 : Math.max(0, shape.cols.length - 1)
  const row = ctrl ? (edge === 'home' ? 0 : Math.max(0, shape.rows - 1)) : state.row
  return { row, col, runStart: null }
}

/** PageUp/PageDown. */
export function navPage(state: NavState, shape: NavShape, delta: number): NavState {
  return { row: clamp(state.row + delta, 0, Math.max(0, shape.rows - 1)), col: state.col, runStart: null }
}

// ── کلید → کار ─────────────────────────────────────────────────────────

export interface KeyLike {
  key: string
  /** جای فیزیکی کلید («KeyD»)؛ با چیدمان فارسی `key` حرف فارسی است */
  code?: string
  shiftKey?: boolean
  ctrlKey?: boolean
  metaKey?: boolean
  altKey?: boolean
}

export type GridKeyAction =
  | { kind: 'arrow'; key: ArrowKey; extend: boolean }
  | { kind: 'tab'; shift: boolean }
  | { kind: 'enter'; shift: boolean }
  | { kind: 'edge'; edge: 'home' | 'end'; ctrl: boolean }
  | { kind: 'page'; delta: 1 | -1 }
  | { kind: 'edit' }
  | { kind: 'cancel' }
  | { kind: 'clear' }
  | { kind: 'fillDown' }
  | { kind: 'undo' }
  | { kind: 'cycleUnit' }
  | { kind: 'menu' }
  | { kind: 'answer'; index: 0 | 1 }
  /** کلید در حالت پرسش بسته است */
  | { kind: 'blocked' }

const ARROWS = new Set(['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'])

/**
 * کلید فشرده‌شده در این حالت خانه چه می‌کند. null یعنی «کار پیش‌فرض مرورگر» (تایپ حرف، جابه‌جایی مکان‌نما).
 */
export function keyAction(e: KeyLike, mode: CellMode): GridKeyAction | null {
  const ctrl = !!(e.ctrlKey || e.metaKey)
  const key = e.key

  if (mode === 'ask') {
    if (key === 'Enter') return { kind: 'answer', index: 0 }
    if (key === 'Escape') return { kind: 'answer', index: 1 }
    if (key === 'Tab') return { kind: 'blocked' }
    if (ctrl || e.altKey) return { kind: 'blocked' }
    return key.length === 1 || ARROWS.has(key) || key === 'Backspace' || key === 'Delete' ? { kind: 'blocked' } : null
  }

  if (key === 'Tab') return { kind: 'tab', shift: !!e.shiftKey }
  if (key === 'Enter') return { kind: 'enter', shift: !!e.shiftKey }
  if (key === 'Escape') return { kind: 'cancel' }
  if (key === 'ArrowDown' && e.altKey) return mode === 'edit' ? null : { kind: 'cycleUnit' }
  if (key === 'F10' && e.shiftKey) return { kind: 'menu' }
  if (key === 'ContextMenu') return { kind: 'menu' }

  if (ARROWS.has(key)) {
    const arrow = key as ArrowKey
    // ویرایش با F2: چپ و راست مکان‌نمای متن‌اند
    if (mode === 'edit' && (arrow === 'ArrowLeft' || arrow === 'ArrowRight')) return null
    return { kind: 'arrow', key: arrow, extend: mode === 'nav' && !!e.shiftKey }
  }

  if (mode !== 'nav') {
    // داخل متن: Home/End، Delete و Ctrl+Z کار خودِ خانهٔ متن‌اند
    return null
  }

  if (key === 'F2') return { kind: 'edit' }
  if (key === 'Home' || key === 'End') return { kind: 'edge', edge: key === 'Home' ? 'home' : 'end', ctrl }
  if (key === 'PageDown') return { kind: 'page', delta: 1 }
  if (key === 'PageUp') return { kind: 'page', delta: -1 }
  if (key === 'Delete' || key === 'Backspace') return { kind: 'clear' }
  // با چیدمان فارسی Ctrl+D حرف «ی» می‌دهد؛ پس جای کلید (`code`) هم دیده می‌شود
  if (ctrl && !e.altKey && (e.code === 'KeyD' || key === 'd' || key === 'D')) return { kind: 'fillDown' }
  if (ctrl && !e.altKey && !e.shiftKey && (e.code === 'KeyZ' || key === 'z' || key === 'Z')) return { kind: 'undo' }
  return null
}

/** وضعیت واکنش‌گرای حرکت برای یک جدول. */
export function useGridKeyboard(initial: Partial<NavState> = {}) {
  const nav = reactive<NavState>({ row: initial.row ?? 0, col: initial.col ?? 0, runStart: initial.runStart ?? null })

  function set(next: NavState): void {
    nav.row = next.row
    nav.col = next.col
    nav.runStart = next.runStart
  }

  return { nav, set }
}
