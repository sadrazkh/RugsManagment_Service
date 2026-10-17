/**
 * گزینش چند خانه در جدول (Shift+پیکان، Shift+کلیک): لنگر و سرِ گزینش، و برنامهٔ «پر کردن رو به پایین» (Ctrl+D) و
 * پاک کردن گروهی. منطق خالص؛ خودِ نوشتن را کنترل‌گر جدول انجام می‌دهد تا «برگرد» یک قدم بماند.
 */
import { computed, reactive } from 'vue'

export interface CellPos {
  row: number
  col: number
}

export interface CellRange {
  top: number
  bottom: number
  left: number
  right: number
}

export function rangeOf(anchor: CellPos, focus: CellPos): CellRange {
  return {
    top: Math.min(anchor.row, focus.row),
    bottom: Math.max(anchor.row, focus.row),
    left: Math.min(anchor.col, focus.col),
    right: Math.max(anchor.col, focus.col),
  }
}

export function inRange(range: CellRange, row: number, col: number): boolean {
  return row >= range.top && row <= range.bottom && col >= range.left && col <= range.right
}

export function isSingle(range: CellRange): boolean {
  return range.top === range.bottom && range.left === range.right
}

export interface FillStep {
  /** ردیفی که مقدارش کپی می‌شود */
  sourceRow: number
  targetRow: number
  col: number
}

/**
 * Ctrl+D مثل اکسل: با گزینش چندردیفی، خانهٔ بالای هر ستون به بقیهٔ گزینش می‌رود؛ با یک خانه، مقدار خانهٔ بالایی به این
 * خانه می‌آید. ردیف اول جدول چیزی بالای سرش ندارد → برنامهٔ خالی.
 */
export function fillDownPlan(range: CellRange): FillStep[] {
  const steps: FillStep[] = []
  if (range.top === range.bottom) {
    if (range.top === 0) return steps
    for (let col = range.left; col <= range.right; col++) steps.push({ sourceRow: range.top - 1, targetRow: range.top, col })
    return steps
  }
  for (let col = range.left; col <= range.right; col++) {
    for (let row = range.top + 1; row <= range.bottom; row++) steps.push({ sourceRow: range.top, targetRow: row, col })
  }
  return steps
}

/** همهٔ خانه‌های یک گزینش (ردیف به ردیف). */
export function cellsOf(range: CellRange): CellPos[] {
  const cells: CellPos[] = []
  for (let row = range.top; row <= range.bottom; row++) {
    for (let col = range.left; col <= range.right; col++) cells.push({ row, col })
  }
  return cells
}

/** گزینش واکنش‌گرا: لنگر ثابت می‌ماند و سر با Shift جابه‌جا می‌شود. */
export function useGridSelection() {
  const state = reactive({ anchor: { row: 0, col: 0 } as CellPos, focus: { row: 0, col: 0 } as CellPos })

  const range = computed(() => rangeOf(state.anchor, state.focus))
  const multi = computed(() => !isSingle(range.value))

  /** گزینش یک‌خانه‌ای روی این خانه. */
  function collapse(pos: CellPos): void {
    state.anchor = { ...pos }
    state.focus = { ...pos }
  }

  /** سر گزینش را به این خانه ببر (لنگر سر جایش). */
  function extend(pos: CellPos): void {
    state.focus = { ...pos }
  }

  function contains(row: number, col: number): boolean {
    return inRange(range.value, row, col)
  }

  return { state, range, multi, collapse, extend, contains }
}
