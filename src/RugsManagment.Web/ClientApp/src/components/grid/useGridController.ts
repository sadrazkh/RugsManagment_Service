/**
 * مغز جدول: ردیف‌ها، خانهٔ فعال، ویرایش، پرسش‌ها، برگرد، چسباندن، ذخیره و پیش‌نویس — یک جا، تا نمای جدول
 * (`DataGrid.vue`) و نمای کارتی گوشی (`GridCardList.vue`) هر دو همین را برانند و رفتارشان یکی بماند.
 *
 * قاعده‌های اصلی (G.1–G.7):
 * - ردیف خالیِ ته جدول همیشه هست؛ اولین نوشته در آن «فرش تازه» می‌سازد و خانه‌های چسبان (فروشنده، رنگ‌ها، محل بافت،
 *   مواد، قیمت خرید و واحدش) از ردیف بالایی می‌آیند.
 * - شمارهٔ پیشنهادی (SuggestNext) خاکستری است و Enter یا Tab آن را می‌پذیرد؛ ردیفی که فقط تعداد گرفته دسته می‌شود.
 * - چیز مبهم (۲۰۸ سانت؟ ۲۵ متر یا سانت؟ ۱۰٫۰۰۰؟) با دو دکمه پرسیده می‌شود؛ Enter پاسخ اول و Esc پاسخ دوم. حرکتی که
 *   منتظر پاسخ ماند پس از پاسخ انجام می‌شود.
 * - هر کار (نوشتن، چسباندن، پر کردن، پاک کردن) یک قدم «برگرد» است (۵۰ قدم).
 */
import { computed, reactive, ref, shallowRef, watch } from 'vue'
import { api, ApiError, errorMessage } from '@/lib/api'
import { appContext } from '@/lib/context'
import type { DraftRow, DraftScope } from '@/lib/gridDrafts'
import { message } from '@/lib/messages'
import { isLargeArea, parseDecimal } from '@/lib/money'
import { toPlainString } from '@/lib/num'
import { moneyWords } from '@/lib/numWords'
import { blockReload, confirmDialog } from '@/lib/ui'
import { SerialMode } from '@/lib/types/platform'
import {
  GridRowStatus,
  UNIT_CYCLE,
  type GridBatchContext,
  type GridBatchRequest,
  type GridColumn,
  type GridData,
  type GridId,
  type GridOp,
  type GridOpResult,
  type GridRow,
  type GridTotals,
  type JsonValue,
} from '@/lib/types/grid'
import {
  dimensionTargets,
  KEY_LENGTH,
  KEY_QTY,
  KEY_SERIAL,
  KEY_WIDTH,
  areaText,
  displayText,
  editableText,
  faDigits,
  faNum,
  fold,
  isMultiLine,
  isPurchaseKey,
  nextSerialLocal,
  parseCellInput,
  pasteCmMessage,
  planPaste,
  purchaseDefaultBasis,
  readPurchaseCell,
  rowArea,
  rowQty,
  serialForLot,
  serialInfo,
  toTsv,
  type CellParse,
} from './gridParse'
import {
  applyOpResult,
  cloneJson,
  columnMap,
  deleteOp,
  emptyRow,
  hasUnsaved,
  keepMine,
  missingRequired,
  rowFromDraft,
  rowFromServer,
  rowToDraft,
  sameValue,
  takeTheirs,
  type GridRowState,
} from './gridModel'
import { useGridDrafts } from './useGridDrafts'
import { useGridSave } from './useGridSave'
import { cellsOf, fillDownPlan, useGridSelection } from './useGridSelection'
import {
  firstTabStop,
  keyAction,
  navArrow,
  navEdge,
  navEnter,
  navPage,
  navTab,
  useGridKeyboard,
  type CellMode,
  type EnterDirection,
  type NavState,
} from './useGridKeyboard'

// ── نوع‌ها ─────────────────────────────────────────────────────────────

export interface QuestionChoice {
  label: string
  run: () => void
}

export interface GridQuestion {
  message: string
  choices: [QuestionChoice, QuestionChoice]
  /** ردیف و خانه‌ای که پرسش دربارهٔ آن است */
  rowId: string | null
  key: string | null
  /** پرسش خطرناک (رنگ هشدار) */
  danger?: boolean
}

export interface GridNotice {
  id: number
  text: string
  kind: 'info' | 'success' | 'error' | 'warning'
  action?: { label: string; run: () => void | Promise<void> }
}

interface UndoChange {
  rowId: string
  key: string
  before: JsonValue
  after: JsonValue
  suggestedBefore: boolean
  suggestedAfter: boolean
}

interface SetOptions {
  /** مقدار از شمارهٔ پیشنهادی آمده */
  suggested?: boolean
  /** حتی اگر برابر بود بنویس (چیپ واحد بی‌نرخ) */
  force?: boolean
  /** قاعده‌های وابسته (تعداد از بازه، دسته از تعداد) اجرا نشوند */
  raw?: boolean
}

export interface GridControllerOptions {
  gridId: GridId
  /** ردیف تازه پذیرفته است (جدول ورود) */
  allowAdd: boolean
  /** منوی «حذف این فرش» */
  allowDelete: boolean
  serialMode: () => SerialMode
  maxPlausibleSideM: () => number
  enterDirection?: () => EnterDirection
  draftScope: () => DraftScope
  batchContext?: () => GridBatchContext | null
  shipmentId?: () => string | null
  /** آیا «حذف این فرش» برای این ردیف نشان داده شود (پیش‌فرض: `derived['row.canDelete']`، وگرنه Business) */
  canDelete?: (row: GridRowState) => boolean
  send?: (request: GridBatchRequest) => Promise<unknown>
  restoreRug?: (rugId: string) => Promise<unknown>
  fetchValues?: (key: string, q: string) => Promise<string[]>
  fetchNextSerial?: () => Promise<number | null>
  /** پس از هر ذخیرهٔ موفق (مثلاً تازه کردن «ورودی‌های اخیر») */
  onSaved?: () => void
  /** پس از برگرداندن فرش حذف‌شده (برای بار دوبارهٔ جدول) */
  onRestored?: () => void
  /** مقدار چسبانِ ردیف اولِ جدولِ خالی (مثلاً فروشندهٔ پیش‌فرض روز) */
  stickyDefaults?: () => Record<string, JsonValue>
  idleMs?: number
}

const UNDO_LIMIT = 50
const NOTICE_MS = 8000

export function useGridController(options: GridControllerOptions) {
  const ctx = appContext()
  /** نمای جدول یا کارت (گوشی)؛ در کارت Enter خانهٔ بعدی همان کارت است */
  const layout = ref<'table' | 'cards'>('table')
  const enterDirection =
    options.enterDirection ??
    ((): EnterDirection => (layout.value === 'cards' || ctx.gridEnter === 'right' ? 'right' : 'down'))

  // ── وضعیت ──────────────────────────────────────────────────────────

  const columns = shallowRef<GridColumn[]>([])
  const rows = ref<GridRowState[]>([])
  const serverNext = ref<number | null>(null)
  /** جمع‌های سرور (بار، و تازه‌شده پس از هر ذخیره) */
  const totals = shallowRef<GridTotals | null>(null)
  const loaded = ref(false)

  const { nav, set: setNav } = useGridKeyboard()
  const selection = useGridSelection()
  const mode = ref<CellMode>('nav')
  const editText = ref('')
  /** خطای خواندن متن خانهٔ فعال (هنوز ثبت نشده) */
  const editError = ref<string | null>(null)
  const question = ref<GridQuestion | null>(null)
  const notices = ref<GridNotice[]>([])
  /** هر بار بالا می‌رود که خانهٔ فعال باید فوکوس بگیرد */
  const focusTick = ref(0)
  /** منوی ردیف باز (شناسهٔ ردیف) */
  const menuRowId = ref<string | null>(null)
  const undoCount = ref(0)
  const undoStack: UndoChange[][] = []
  let pendingAfterAnswer: (() => void) | null = null
  let noticeId = 1

  const colMap = computed(() => columnMap(columns.value))
  const serialMode = computed(() => options.serialMode())

  function isEditable(column: GridColumn | undefined, row?: GridRowState): boolean {
    if (!column || column.readOnly || column.type === 'readonly') return false
    if (column.key === KEY_SERIAL && serialMode.value === SerialMode.Auto && row && row.rugId === null) return false
    if (row && isPurchaseKey(column.key) && isMultiLine(readPurchaseCell(row.values[column.key], column))) return false
    return true
  }

  const navCols = computed(() =>
    columns.value.map((c) => ({
      tabStop:
        !c.readOnly &&
        c.type !== 'readonly' &&
        c.tabStop !== false &&
        !(c.key === KEY_SERIAL && serialMode.value === SerialMode.Auto),
    })),
  )
  const shape = computed(() => ({ rows: rows.value.length, cols: navCols.value }))

  const activeRow = computed<GridRowState | null>(() => rows.value[nav.row] ?? null)
  const activeColumn = computed<GridColumn | null>(() => columns.value[nav.col] ?? null)

  /** شمارهٔ پیشنهادی ردیف تازه (G.4)؛ null در حالت دستی. */
  const suggestion = computed(() => {
    if (serialMode.value === SerialMode.Manual) return null
    return nextSerialLocal(serverNext.value, rows.value.map((r) => r.values[KEY_SERIAL]))
  })

  // ── پیش‌نویس و ذخیره ────────────────────────────────────────────────

  const drafts = useGridDrafts({
    scope: options.draftScope,
    collect: () => rows.value.filter(hasUnsaved).map(rowToDraft),
  })

  function isReady(row: GridRowState): boolean {
    if (row.rugId !== null) return true
    return missingRequired(row, columns.value, autoSkip()).length === 0
  }

  function autoSkip(): Set<string> {
    return serialMode.value === SerialMode.Auto ? new Set([KEY_SERIAL]) : new Set()
  }

  function buildRequest(ops: GridOp[]): GridBatchRequest {
    return {
      gridId: options.gridId,
      batch: options.batchContext?.() ?? null,
      shipmentId: options.shipmentId?.() ?? null,
      ops,
    }
  }

  const send = options.send ?? ((request: GridBatchRequest) => api.post<unknown>('/api/grid/rugs/batch', request))

  const save = useGridSave({
    rows: () => rows.value,
    columns: () => colMap.value,
    request: buildRequest,
    send,
    ready: isReady,
    idleMs: options.idleMs,
    onApplied: (row, outcome) => {
      if (outcome === 'deleted') removeRow(row.id)
      if (outcome === 'saved') {
        dedupe(row)
        options.onSaved?.()
      }
      if ((outcome === 'invalid' || outcome === 'error') && row.errors[KEY_SERIAL] && row.nextFree === null && row.serialSuggested) {
        void lookupNextFree(row)
      }
      drafts.schedule()
    },
    onRequestError: (error) => notify(error.message, 'error'),
    onResponse: (response) => {
      if (response?.totals) totals.value = response.totals
      if (response && response.nextSerial !== undefined) serverNext.value = response.nextSerial ?? null
    },
  })

  /** ردیفی که با پیش‌نویس برگشت و سرور گفت «همان فرش قبلی است»: نسخهٔ بارشدهٔ تکراری را بردار. */
  function dedupe(row: GridRowState) {
    if (!row.rugId) return
    const twins = rows.value.filter((r) => r !== row && r.rugId === row.rugId)
    if (twins.length === 0) return
    rows.value = rows.value.filter((r) => !twins.includes(r))
    clampNav()
  }

  async function lookupNextFree(row: GridRowState) {
    const fetcher = options.fetchNextSerial ?? (async () => (await api.get<{ next: number | null }>('/api/rugs/next-serial')).next)
    try {
      const next = await fetcher()
      if (next !== null && row.nextFree === null && row.errors[KEY_SERIAL]) row.nextFree = next
    } catch {
      /* پیشنهاد نشد؛ کاربر خودش می‌نویسد */
    }
  }

  // قفل تازه‌سازی تا وقتی چیزی ذخیره‌نشده هست (G.7)
  let release: (() => void) | null = null
  watch(
    () => save.unsavedCount.value > 0 || save.state.inFlight,
    (busy) => {
      if (busy && !release) release = blockReload(`grid:${options.gridId}`)
      else if (!busy && release) {
        release()
        release = null
      }
    },
    { immediate: true },
  )

  function changed() {
    drafts.schedule()
    save.schedule()
  }

  // ── بار کردن ────────────────────────────────────────────────────────

  function ensurePlaceholder() {
    if (!options.allowAdd) return
    const last = rows.value[rows.value.length - 1]
    if (!last || !last.placeholder) rows.value.push(emptyRow({ placeholder: true }))
  }

  function clampNav() {
    const maxRow = Math.max(0, rows.value.length - 1)
    const maxCol = Math.max(0, columns.value.length - 1)
    if (nav.row > maxRow || nav.col > maxCol) setNav({ row: Math.min(nav.row, maxRow), col: Math.min(nav.col, maxCol), runStart: null })
  }

  function visibleColumns(list: readonly GridColumn[]): GridColumn[] {
    return list.filter((c) => !c.money || ctx.moneyVisible)
  }

  /**
   * دادهٔ تازهٔ سرور. با `merge`، ردیف‌های تازه و ذخیره‌نشدهٔ همین صفحه می‌مانند (بار دوباره پس از «برگردان»)؛ بی آن
   * جدول از نو ساخته می‌شود (روز دیگر).
   */
  function load(data: GridData, opts: { merge?: boolean } = {}) {
    columns.value = visibleColumns(data.columns ?? [])
    serverNext.value = data.nextSerial ?? null
    totals.value = data.totals ?? null
    const map = columnMap(columns.value)
    const incoming = (data.rows ?? []).map((r: GridRow) => rowFromServer(r, map))

    if (opts.merge) {
      const local = rows.value.filter((r) => !r.placeholder)
      const byRug = new Map(local.filter((r) => r.rugId).map((r) => [r.rugId as string, r]))
      const merged = incoming.map((r) => {
        const mine = r.rugId ? byRug.get(r.rugId) : undefined
        return mine && (hasUnsaved(mine) || mine.conflict || mine.saving) ? mine : r
      })
      const kept = new Set(merged.map((r) => r.rugId))
      const extra = local.filter((r) => (r.rugId === null || (!kept.has(r.rugId) && hasUnsaved(r))) && !merged.includes(r))
      rows.value = [...merged, ...extra]
    } else {
      rows.value = incoming
      undoStack.length = 0
      undoCount.value = 0
      question.value = null
      pendingAfterAnswer = null
    }
    ensurePlaceholder()
    loaded.value = true

    if (!opts.merge) {
      const start = firstTabStop(navCols.value)
      const target = options.allowAdd ? Math.max(0, rows.value.length - 1) : 0
      moveTo({ row: target, col: start, runStart: null }, { focus: false })
      drafts.check(new Set(rows.value.map((r) => r.id)))
    } else {
      clampNav()
      syncEditText()
    }
  }

  /** صفحهٔ بعد (جدول انبار): ردیف‌های تازه ته جدول می‌آیند. */
  function append(data: GridData) {
    const map = colMap.value
    const present = new Set(rows.value.map((r) => r.rugId))
    const more = (data.rows ?? []).filter((r) => !present.has(r.rugId)).map((r: GridRow) => rowFromServer(r, map))
    const at = options.allowAdd && rows.value[rows.value.length - 1]?.placeholder ? rows.value.length - 1 : rows.value.length
    rows.value.splice(at, 0, ...more)
    if (data.totals) totals.value = data.totals
  }

  // ── خانه‌ها ────────────────────────────────────────────────────────

  function isBlank(value: JsonValue | undefined) {
    return value === null || value === undefined || value === ''
  }

  function markDirty(row: GridRowState, key: string) {
    const column = colMap.value.get(key)
    const dirty = new Set(row.dirty)
    if (row.rugId !== null && sameValue(key, column, row.values[key], row.base[key])) dirty.delete(key)
    else dirty.add(key)
    row.dirty = [...dirty]
    delete row.errors[key]
    if (row.status !== 'conflict') {
      row.status = row.dirty.length > 0 ? 'dirty' : Object.keys(row.errors).length > 0 ? 'invalid' : 'clean'
    }
  }

  /** ردیف بالایی که فرش است (برای خانه‌های چسبان). */
  function rowAbove(index: number): GridRowState | null {
    for (let i = index - 1; i >= 0; i--) if (!rows.value[i].placeholder) return rows.value[i]
    return null
  }

  /** مقدار چسبانی که ردیف خالی از بالایی می‌گیرد (برای نمایش خاکستری هم). */
  function stickyValue(index: number, column: GridColumn): JsonValue | undefined {
    if (!column.sticky) return undefined
    const above = rowAbove(index)
    const value = above ? above.values[column.key] : options.stickyDefaults?.()[column.key]
    if (isBlank(value)) return undefined
    if (isPurchaseKey(column.key) && isMultiLine(readPurchaseCell(value, column))) return undefined
    return value
  }

  /** ردیف خالی ته جدول با اولین نوشته فرش می‌شود و خانه‌های چسبان را می‌گیرد. */
  function materialize(index: number, group: UndoChange[], skipKey: string) {
    const row = rows.value[index]
    if (!row?.placeholder) return
    row.placeholder = false
    for (const column of columns.value) {
      if (column.key === skipKey || !column.sticky || !isEditable(column, row)) continue
      const value = stickyValue(index, column)
      if (value === undefined || !isBlank(row.values[column.key])) continue
      const copy = cloneJson(value)
      row.values[column.key] = isPurchaseKey(column.key) ? stripAmount(copy) : copy
      group.push({ rowId: row.id, key: column.key, before: null, after: cloneJson(row.values[column.key]), suggestedBefore: false, suggestedAfter: false })
      markDirty(row, column.key)
    }
    ensurePlaceholder()
  }

  /** خانهٔ خرید کپی‌شده فقط نرخ و مبنا را می‌برد. */
  function stripAmount(value: JsonValue): JsonValue {
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      const { rate, basis } = value as { rate?: JsonValue; basis?: JsonValue }
      return { rate: rate ?? null, basis: basis ?? null }
    }
    return value
  }

  /** نوشتن یک خانه (با قاعده‌های وابسته) و ثبت در قدم «برگرد». */
  function setValue(index: number, key: string, value: JsonValue, group: UndoChange[], opts: SetOptions = {}): boolean {
    const row = rows.value[index]
    if (!row) return false
    const column = colMap.value.get(key)
    if (row.placeholder) materialize(index, group, key)

    const before = cloneJson(row.values[key] ?? null)
    const suggestedBefore = row.serialSuggested
    if (!opts.force && sameValue(key, column, before, value) && !(key === KEY_SERIAL && opts.suggested !== undefined)) {
      return false
    }
    row.values[key] = cloneJson(value)
    if (key === KEY_SERIAL) {
      row.serialSuggested = opts.suggested ?? false
      row.nextFree = null
    }
    group.push({
      rowId: row.id,
      key,
      before,
      after: cloneJson(value),
      suggestedBefore,
      suggestedAfter: row.serialSuggested,
    })
    markDirty(row, key)

    if (!opts.raw) applyDependentRules(index, key, group)
    if (key === KEY_LENGTH || key === KEY_WIDTH || key === KEY_QTY) checkArea(row)
    return true
  }

  /** «تعداد از بازه» (G.2) و «دسته از تعداد» (G.4). */
  function applyDependentRules(index: number, key: string, group: UndoChange[]) {
    const row = rows.value[index]
    if (!colMap.value.has(KEY_QTY)) return
    if (key === KEY_SERIAL) {
      const info = serialInfo(row.values[KEY_SERIAL])
      const qtyValue = row.values[KEY_QTY]
      if (info && info.from !== null && info.count > 1 && (isBlank(qtyValue) || qtyValue === 1)) {
        setValue(index, KEY_QTY, info.count, group, { raw: true })
      }
    } else if (key === KEY_QTY && row.serialSuggested) {
      const info = serialInfo(row.values[KEY_SERIAL])
      if (info && info.from !== null) {
        setValue(index, KEY_SERIAL, serialForLot(info.from, rowQty(row.values)), group, { suggested: true, raw: true })
      }
    }
  }

  function checkArea(row: GridRowState) {
    const area = rowArea(row.values)
    row.warning = area && isLargeArea(area.piece) ? message('W_LARGE_AREA', { area: faNum(area.piece, 2) }) : null
  }

  function pushUndo(group: UndoChange[]) {
    if (group.length === 0) return
    undoStack.push(group)
    if (undoStack.length > UNDO_LIMIT) undoStack.shift()
    undoCount.value = undoStack.length
  }

  // ── ویرایش خانهٔ فعال ───────────────────────────────────────────────

  function parseContext(row: GridRowState | null, key: string | null) {
    return {
      moneyScale: ctx.moneyScale,
      currencyLabel: ctx.currency.label,
      maxPlausibleSideM: options.maxPlausibleSideM(),
      current: row && key ? row.values[key] : undefined,
    }
  }

  function syncEditText() {
    const row = activeRow.value
    const column = activeColumn.value
    editText.value = row && column ? editableText(column, row.values[column.key]) : ''
    editError.value = null
  }

  /** آیا خانهٔ فعال شمارهٔ پیشنهادی خالی دارد که Enter/Tab بپذیرد. */
  function canAcceptSuggestion(): boolean {
    const row = activeRow.value
    const column = activeColumn.value
    return (
      !!row &&
      !!column &&
      column.key === KEY_SERIAL &&
      serialMode.value === SerialMode.SuggestNext &&
      suggestion.value !== null &&
      row.rugId === null &&
      isBlank(row.values[KEY_SERIAL]) &&
      editText.value.trim() === ''
    )
  }

  function acceptSuggestion(): boolean {
    if (!canAcceptSuggestion()) return false
    const group: UndoChange[] = []
    const row = activeRow.value!
    const qty = rowQty(row.values)
    setValue(nav.row, KEY_SERIAL, serialForLot(suggestion.value!, qty), group, { suggested: true })
    pushUndo(group)
    changed()
    return true
  }

  /**
   * متن خانهٔ فعال را ثبت می‌کند. `ok`: ثبت شد (یا چیزی برای ثبت نبود)؛ `pending`: پرسش یا خطا مانده و حرکت نباید
   * انجام شود.
   */
  function commit(): 'ok' | 'pending' {
    if (question.value) return 'pending'
    const row = activeRow.value
    const column = activeColumn.value
    if (!row || !column) return 'ok'
    if (mode.value === 'nav') return 'ok'
    if (!isEditable(column, row)) {
      mode.value = 'nav'
      return 'ok'
    }

    const typed = editText.value
    const parsed: CellParse = parseCellInput(column, typed, parseContext(row, column.key))
    if (parsed.status === 'error') {
      editError.value = parsed.message
      mode.value = 'edit'
      return 'pending'
    }
    if (parsed.status === 'ask') {
      const index = nav.row
      question.value = {
        message: parsed.message,
        rowId: row.id,
        key: column.key,
        choices: parsed.choices.map((choice) => ({
          label: choice.label,
          run: () => {
            question.value = null
            if (choice.value === undefined) {
              editError.value = choice.error ?? message('E_DIMENSION')
              mode.value = 'edit'
              pendingAfterAnswer = null
              focusTick.value++
              return
            }
            const ok = writeParsed(index, column, { status: 'ok', value: choice.value, pair: choice.pair }, typed)
            if (ok) runPendingMove()
          },
        })) as [QuestionChoice, QuestionChoice],
      }
      return 'pending'
    }
    return writeParsed(nav.row, column, parsed, typed) ? 'ok' : 'pending'
  }

  /** مقدار خوانده‌شده را می‌نویسد (با جفت اندازه و پرسش «بازه/تعداد»). false اگر پرسش تازه‌ای آمد. */
  function writeParsed(index: number, column: GridColumn, parsed: Extract<CellParse, { status: 'ok' }>, typed: string): boolean {
    const group: UndoChange[] = []
    let value = parsed.value
    if (column.type === 'money+unit' && value !== null && typeof value === 'object') value = stripAmount(value)
    // «208×210» در خانهٔ طول یا عرض هر دو را پر می‌کند (عدد اول همیشه طول)
    const pairable = (key: string) => isEditable(colMap.value.get(key), rows.value[index])
    for (const target of dimensionTargets(column.key, value, parsed.pair, pairable)) {
      setValue(index, target.key, target.value, group, { suggested: target.key === KEY_SERIAL ? false : undefined })
    }
    pushUndo(group)
    mode.value = 'nav'
    editError.value = null
    syncEditText()
    changed()

    // بازه بلندتر از تعداد: بپرس (H.3 «Range/qty»)
    const row = rows.value[index]
    if (row && (column.key === KEY_SERIAL || column.key === KEY_QTY) && group.length > 0) {
      const info = serialInfo(row.values[KEY_SERIAL])
      const qty = rowQty(row.values)
      if (info && info.from !== null && info.to !== null && info.count > qty && qty > 1 && !row.serialSuggested) {
        question.value = {
          message: message('Q_GRID_RANGE_QTY', { from: faDigits(info.from), to: faDigits(info.to), count: info.count, qty }),
          rowId: row.id,
          key: column.key,
          choices: [
            {
              label: 'بله',
              run: () => {
                question.value = null
                runPendingMove()
              },
            },
            {
              label: 'درستش می‌کنم',
              run: () => {
                question.value = null
                pendingAfterAnswer = null
                undo({ silent: true })
                mode.value = 'edit'
                editText.value = typed
                focusTick.value++
              },
            },
          ],
        }
        return false
      }
    }
    return true
  }

  function runPendingMove() {
    const next = pendingAfterAnswer
    pendingAfterAnswer = null
    if (next) next()
    else focusTick.value++
  }

  /** پاسخ پرسش (Enter → ۰، Esc → ۱). */
  function answer(index: 0 | 1) {
    const q = question.value
    if (!q) return
    q.choices[index].run()
  }

  function cancelEdit() {
    mode.value = 'nav'
    syncEditText()
    focusTick.value++
  }

  // ── حرکت ───────────────────────────────────────────────────────────

  interface MoveOptions {
    focus?: boolean
    extend?: boolean
  }

  function moveTo(next: NavState, opts: MoveOptions = {}) {
    const previous = activeRow.value
    if (opts.extend) {
      selection.extend({ row: next.row, col: next.col })
      setNav(next)
    } else {
      setNav(next)
      selection.collapse({ row: next.row, col: next.col })
    }
    mode.value = 'nav'
    menuRowId.value = null
    syncEditText()
    if (previous && previous !== activeRow.value) leftRow(previous)
    if (opts.focus !== false) focusTick.value++
  }

  /** کاربر از ردیف بیرون رفت: همین حالا ذخیره (G.7)، و خانه‌های لازمِ خالیِ ردیف تازه را نشان بده. */
  function leftRow(row: GridRowState) {
    if (!hasUnsaved(row)) return
    if (row.rugId === null) {
      for (const column of missingRequired(row, columns.value, autoSkip())) {
        row.errors[column.key] = message('E_REQUIRED', { label: column.label })
      }
    }
    save.flushSoon()
  }

  /** بعد از ثبت، حرکت کن؛ اگر پرسشی آمد، حرکت پس از پاسخ انجام می‌شود. */
  function commitThen(move: () => void) {
    if (commit() === 'ok') move()
    else if (question.value) pendingAfterAnswer = move
  }

  function activate(row: number, col: number, opts: MoveOptions = {}) {
    if (row === nav.row && col === nav.col && !opts.extend) {
      if (opts.focus !== false) focusTick.value++
      return
    }
    commitThen(() => moveTo({ row, col, runStart: null }, opts))
  }

  // ── صفحه‌کلید ───────────────────────────────────────────────────────

  /** کلیدِ خانهٔ فعال؛ true یعنی کار شد و پیش‌فرض مرورگر باید گرفته شود. */
  function onKeydown(e: KeyboardEvent | (Partial<KeyboardEvent> & { key: string })): boolean {
    const action = keyAction(e, question.value ? 'ask' : mode.value)
    if (!action) return false

    switch (action.kind) {
      case 'answer':
        answer(action.index)
        return true
      case 'blocked':
        return true
      case 'tab':
      case 'enter': {
        if (!question.value && mode.value === 'nav' && canAcceptSuggestion()) acceptSuggestion()
        else if (mode.value !== 'nav' && canAcceptSuggestion()) {
          mode.value = 'nav'
          acceptSuggestion()
        }
        const target = () => {
          const next =
            action.kind === 'tab' ? navTab(nav, shape.value, action.shift) : navEnter(nav, shape.value, enterDirection(), action.shift)
          if (next) moveTo(next)
          else focusTick.value++
        }
        commitThen(target)
        return true
      }
      case 'arrow': {
        if (action.extend) {
          const focus = selection.state.focus
          const next = navArrow({ row: focus.row, col: focus.col, runStart: null }, shape.value, action.key)
          selection.extend({ row: next.row, col: next.col })
          return true
        }
        commitThen(() => moveTo(navArrow(nav, shape.value, action.key)))
        return true
      }
      case 'edge':
        commitThen(() => moveTo(navEdge(nav, shape.value, action.edge, action.ctrl)))
        return true
      case 'page':
        commitThen(() => moveTo(navPage(nav, shape.value, action.delta * 10)))
        return true
      case 'edit': {
        const row = activeRow.value
        if (!isEditable(activeColumn.value ?? undefined, row ?? undefined)) return true
        mode.value = 'edit'
        editError.value = null
        return true
      }
      case 'cancel':
        if (menuRowId.value) {
          menuRowId.value = null
          return true
        }
        if (mode.value !== 'nav' || editError.value) {
          cancelEdit()
          return true
        }
        if (selection.multi.value) {
          selection.collapse({ row: nav.row, col: nav.col })
          return true
        }
        return false
      case 'clear':
        clearSelection()
        return true
      case 'fillDown':
        fillDown()
        return true
      case 'undo':
        undo()
        return true
      case 'cycleUnit':
        cycleUnit(nav.row, activeColumn.value?.key ?? '')
        return true
      case 'menu':
        if (activeRow.value && !activeRow.value.placeholder) menuRowId.value = activeRow.value.id
        return true
    }
    return false
  }

  /** تایپ در خانهٔ فعال. */
  function onInput(text: string) {
    if (question.value) return
    editText.value = text
    editError.value = null
    if (mode.value === 'nav') mode.value = 'enter'
  }

  /** شروع ویرایش با دوبار کلیک یا F2. */
  function startEdit() {
    if (!isEditable(activeColumn.value ?? undefined, activeRow.value ?? undefined)) return
    mode.value = 'edit'
    focusTick.value++
  }

  // ── کارهای گروهی ────────────────────────────────────────────────────

  function clearSelection() {
    const group: UndoChange[] = []
    for (const cell of cellsOf(selection.range.value)) {
      const row = rows.value[cell.row]
      const column = columns.value[cell.col]
      if (!row || row.placeholder || !isEditable(column, row)) continue
      setValue(cell.row, column.key, null, group, { raw: true })
    }
    pushUndo(group)
    syncEditText()
    if (group.length > 0) changed()
  }

  function fillDown() {
    const group: UndoChange[] = []
    for (const step of fillDownPlan(selection.range.value)) {
      const column = columns.value[step.col]
      const source = rows.value[step.sourceRow]
      const target = rows.value[step.targetRow]
      if (!column || !source || !target || source.placeholder || !isEditable(column, target)) continue
      const value = source.values[column.key]
      if (isPurchaseKey(column.key) && isMultiLine(readPurchaseCell(value, column))) continue
      setValue(step.targetRow, column.key, isPurchaseKey(column.key) ? stripAmount(cloneJson(value ?? null)) : cloneJson(value ?? null), group)
    }
    pushUndo(group)
    syncEditText()
    if (group.length > 0) changed()
  }

  /** چیپ واحد: «هر متر» ← «هر تخته» ← «کل» (ضربه یا Alt+↓). */
  function cycleUnit(index: number, key: string) {
    const row = rows.value[index]
    const column = colMap.value.get(key)
    if (!row || !column || column.type !== 'money+unit' || !isEditable(column, row)) return
    const cell = readPurchaseCell(row.values[key], column) ?? { rate: null, basis: purchaseDefaultBasis(column) }
    const at = UNIT_CYCLE.indexOf(cell.basis)
    const basis = UNIT_CYCLE[(at + 1) % UNIT_CYCLE.length]
    const group: UndoChange[] = []
    setValue(index, key, { rate: cell.rate, basis }, group, { force: true })
    pushUndo(group)
    changed()
  }

  /** «برگرد» (Ctrl+Z یا دکمه): یک قدم، تا ۵۰ قدم. */
  function undo(opts: { silent?: boolean } = {}) {
    const group = undoStack.pop()
    undoCount.value = undoStack.length
    if (!group) {
      if (!opts.silent) notify(message('I_GRID_UNDO_EMPTY'), 'info')
      return
    }
    const touched = new Set<GridRowState>()
    for (const change of [...group].reverse()) {
      const row = rows.value.find((r) => r.id === change.rowId)
      if (!row) continue
      row.values[change.key] = cloneJson(change.before)
      row.serialSuggested = change.suggestedBefore
      markDirty(row, change.key)
      touched.add(row)
    }
    // ردیف تازه‌ای که دوباره کاملاً خالی شد ردیف خالیِ ته جدول می‌شود
    for (const row of touched) {
      if (row.rugId === null && Object.values(row.values).every((v) => isBlank(v))) {
        row.dirty = []
        row.errors = {}
        row.status = 'clean'
        const index = rows.value.indexOf(row)
        const isLastReal = index === rows.value.length - 2 && rows.value[rows.value.length - 1]?.placeholder
        if (isLastReal) {
          rows.value.splice(index + 1, 1)
          row.placeholder = true
        } else {
          rows.value.splice(index, 1)
        }
      }
    }
    ensurePlaceholder()
    clampNav()
    mode.value = 'nav'
    syncEditText()
    focusTick.value++
    changed()
  }

  // ── چسباندن و کپی ───────────────────────────────────────────────────

  /** متن TSV را از خانهٔ فعال (یا گوشهٔ گزینش) می‌چسباند؛ همهٔ اندازه‌های سانت‌نما یک پرسش می‌گیرند. */
  function paste(text: string) {
    if (question.value || !text) return
    const range = selection.range.value
    const startRow = range.top
    const startCol = range.left
    const plan = planPaste(text, columns.value, startCol, {
      ...parseContext(null, null),
      baleFooter: ctx.terms.baleFooter,
    })
    if (plan.rows === 0) return

    let maxRows = plan.rows
    if (!options.allowAdd) {
      const available = rows.value.length - startRow
      if (plan.rows > available) {
        notify(message('W_GRID_PASTE_TRUNCATED', { n: plan.rows - available }), 'warning')
        maxRows = available
      }
    }

    const group: UndoChange[] = []
    let errors = 0
    const cmCells: { index: number; cell: (typeof plan.cells)[number] }[] = []

    // سطرهای چسبانده به ترتیب روی ردیف‌های پشت‌سرهم می‌نشینند (سطری که هیچ خانه‌ای نداشت جا نمی‌گیرد)؛ در هر سطر
    // اول شماره و بعد بقیه، تا «تعداد از بازه» درست بنشیند
    const lineRows = [...new Set(plan.cells.map((c) => c.row))].sort((a, b) => a - b).slice(0, maxRows)
    lineRows.forEach((line, k) => {
      const index = startRow + k
      const cells = plan.cells
        .filter((c) => c.row === line)
        .sort((a, b) => (a.key === KEY_SERIAL ? -1 : b.key === KEY_SERIAL ? 1 : 0))
      for (const cell of cells) {
        const row = rows.value[index]
        if (!row) return
        const column = colMap.value.get(cell.key)
        if (!isEditable(column, row)) continue
        // اولین خانهٔ ردیف خالیِ ته جدول آن را فرش می‌کند و ردیف خالی تازه‌ای پایینش می‌آید
        if (row.placeholder) materialize(index, group, cell.key)
        if (cell.error) {
          row.errors[cell.key] = cell.error
          row.status = 'invalid'
          errors++
        } else if (cell.cm) {
          cmCells.push({ index, cell })
        } else {
          setValue(index, cell.key, cell.value ?? null, group)
        }
      }
    })
    maxRows = lineRows.length

    const finish = () => {
      pushUndo(group)
      syncEditText()
      changed()
      notify(message('I_GRID_PASTED', { n: maxRows }), 'success')
      if (errors > 0) notify(message('E_GRID_PASTE_CELLS', { n: errors }), 'error')
      focusTick.value++
    }

    if (cmCells.length === 0) {
      finish()
      return
    }

    const applyCm = (yes: boolean) => {
      question.value = null
      for (const { index, cell } of cmCells) {
        const row = rows.value[index]
        if (!row || !cell.cm) continue
        const value = yes ? cell.cm.yes : cell.cm.no
        if (value === undefined) {
          row.errors[cell.key] = cell.cm.noError ?? message('E_DIMENSION')
          row.status = 'invalid'
          errors++
        } else {
          setValue(index, cell.key, value, group)
        }
      }
      finish()
    }
    question.value = {
      message: pasteCmMessage(cmCells.length),
      rowId: null,
      key: null,
      choices: [
        { label: 'تبدیل کن', run: () => applyCm(true) },
        { label: 'نه', run: () => applyCm(false) },
      ],
    }
  }

  /** گزینش → TSV (عددها با ارقام لاتین تا اکسل عدد بخواند). */
  function copyText(): string {
    const range = selection.range.value
    const lines: string[][] = []
    for (let r = range.top; r <= range.bottom; r++) {
      const row = rows.value[r]
      if (!row || row.placeholder) continue
      const line: string[] = []
      for (let c = range.left; c <= range.right; c++) {
        const column = columns.value[c]
        const value = row.values[column?.key ?? '']
        line.push(column ? plainText(column, value, row) : '')
      }
      lines.push(line)
    }
    return toTsv(lines)
  }

  function plainText(column: GridColumn, value: JsonValue | undefined, row: GridRowState): string {
    if (column.key === 'rug.areaM2') {
      const area = rowArea(row.values)
      return area ? toPlainString(area.total, 2) : ''
    }
    if (isBlank(value)) return ''
    if (isPurchaseKey(column.key)) {
      const cell = readPurchaseCell(value, column)
      return cell?.rate !== null && cell?.rate !== undefined ? String(cell.rate) : ''
    }
    return typeof value === 'object' ? JSON.stringify(value) : String(value)
  }

  // ── ردیف: حذف، تعارض، شمارهٔ آزاد ───────────────────────────────────

  function removeRow(id: string) {
    const index = rows.value.findIndex((r) => r.id === id)
    if (index < 0) return
    rows.value.splice(index, 1)
    ensurePlaceholder()
    clampNav()
    syncEditText()
  }

  function canDeleteRow(row: GridRowState): boolean {
    if (!options.allowDelete || row.placeholder) return false
    if (row.rugId === null) return true
    if (options.canDelete) return options.canDelete(row)
    const flag = row.derived['row.canDelete']
    if (typeof flag === 'boolean') return flag
    return ctx.policies.business
  }

  /**
   * شمارهٔ ردیف برای متن‌ها («فرش ۳۹۱۱ - ۳۹۶۰ حذف شود؟»)، با ارقام فارسی و جداشده از متن راست‌به‌چپ (LRI…PDI، همان
   * `<bdi dir="ltr">` در H.7)؛ وگرنه دستهٔ «۳۹۱۱ - ۳۹۶۰» وسط جملهٔ فارسی برعکس دیده می‌شود.
   */
  function serialLabel(row: GridRowState): string {
    const serial = row.values[KEY_SERIAL]
    return isBlank(serial) ? '' : `⁦${faDigits(String(serial))}⁩`
  }

  /** «حذف این فرش» با تأیید؛ پس از حذف toast «حذف شد [برگردان]» تا ۸ ثانیه. */
  async function deleteRow(id: string) {
    menuRowId.value = null
    const index = rows.value.findIndex((r) => r.id === id)
    const row = rows.value[index]
    if (!row || !canDeleteRow(row)) return
    const serial = serialLabel(row)
    const ok = await confirmDialog({
      title: serial ? message('Q_GRID_DELETE_RUG', { serial }) : message('Q_GRID_DELETE_ROW'),
      confirmLabel: 'حذف کن',
      cancelLabel: 'نه',
      danger: true,
    })
    if (!ok) return

    if (row.rugId === null) {
      // هنوز ذخیره نشده: فقط از جدول (و پیش‌نویس) برداشته می‌شود
      removeRow(row.id)
      drafts.schedule()
      notify(message('I_DELETED'), 'info', {
        label: 'برگردان',
        run: () => {
          rows.value.splice(Math.min(index, rows.value.length - (options.allowAdd ? 1 : 0)), 0, row)
          changed()
        },
      })
      return
    }

    try {
      const response = await send(buildRequest([deleteOp(row)]))
      const results = Array.isArray(response) ? (response as GridOpResult[]) : ((response as { results?: GridOpResult[] })?.results ?? [])
      const result = results.find((r) => r.clientRowId === row.id)
      if (!result || result.status === GridRowStatus.Deleted) {
        removeRow(row.id)
        drafts.schedule()
        notify(message('I_DELETED'), 'info', { label: 'برگردان', run: () => restoreRow(row, index) })
        return
      }
      const reason = result.errors?.[0]?.message ?? message('E_RUG_DELETE_FORBIDDEN')
      notify(reason, 'error')
    } catch (error) {
      notify(errorMessage(error), 'error')
    }
  }

  async function restoreRow(row: GridRowState, index: number) {
    if (!row.rugId) return
    const restore =
      options.restoreRug ??
      ((rugId: string) => api.post<unknown>(`/api/grid/rugs/${rugId}/restore?gridId=${encodeURIComponent(options.gridId)}`))
    try {
      const response = await restore(row.rugId)
      // سرور ردیف تازهٔ همان فرش را می‌دهد (`GridRowDto`): نسخه و مقدارهای امروزش
      const fresh =
        response && typeof response === 'object' && 'values' in response && 'rugId' in response
          ? rowFromServer(response as GridRow, colMap.value)
          : null
      const back = fresh ?? row
      if (fresh) fresh.id = row.id
      back.dirty = []
      back.errors = {}
      back.status = 'clean'
      back.saving = false
      rows.value.splice(Math.min(index, rows.value.length - (options.allowAdd ? 1 : 0)), 0, back)
      notify(message('I_GRID_RESTORED'), 'success')
      options.onRestored?.()
    } catch (error) {
      notify(errorMessage(error), 'error')
    }
  }

  function resolveConflict(id: string, choice: 'mine' | 'theirs') {
    const row = rows.value.find((r) => r.id === id)
    if (!row) return
    if (choice === 'mine') keepMine(row)
    else takeTheirs(row)
    if (rows.value[nav.row] === row) syncEditText()
    changed()
  }

  /** خانه‌های درگیر تعارض: «رنگ متن: شما «کاشان» · او «تبریز»». */
  function conflictLines(row: GridRowState): string[] {
    if (!row.conflict) return []
    return row.conflict.fields.map((key) => {
      const column = colMap.value.get(key)
      const show = (v: JsonValue | undefined) => (column ? displayText(column, v, ctx.moneyScale) : String(v ?? '')) || '—'
      return message('I_GRID_CONFLICT_FIELD', {
        label: column?.label ?? key,
        mine: show(row.values[key]),
        theirs: show(row.conflict!.current[key]),
      })
    })
  }

  /** «شمارهٔ ۴۰۰۵ آزاد است؛ همان را بگذارم؟ [بله]». */
  function acceptNextFree(id: string) {
    const index = rows.value.findIndex((r) => r.id === id)
    const row = rows.value[index]
    if (!row || row.nextFree === null) return
    const group: UndoChange[] = []
    setValue(index, KEY_SERIAL, serialForLot(row.nextFree, rowQty(row.values)), group, { suggested: true })
    row.nextFree = null
    pushUndo(group)
    if (rows.value[nav.row] === row) syncEditText()
    changed()
  }

  /** «خودم می‌نویسم»: خانهٔ شماره برای ویرایش. */
  function editSerialOf(id: string) {
    const index = rows.value.findIndex((r) => r.id === id)
    const col = columns.value.findIndex((c) => c.key === KEY_SERIAL)
    if (index < 0 || col < 0) return
    rows.value[index].nextFree = null
    moveTo({ row: index, col, runStart: null })
    mode.value = 'edit'
  }

  // ── پیش‌نویس‌ها ─────────────────────────────────────────────────────

  function mergeDrafts(list: DraftRow[]) {
    for (const draft of list) {
      const existing =
        rows.value.find((r) => r.id === draft.clientRowId) ??
        (draft.rugId ? rows.value.find((r) => r.rugId === draft.rugId) : undefined) ??
        null
      const row = rowFromDraft(draft, existing)
      if (!existing) {
        const at = options.allowAdd && rows.value[rows.value.length - 1]?.placeholder ? rows.value.length - 1 : rows.value.length
        rows.value.splice(at, 0, row)
      }
    }
    ensurePlaceholder()
    syncEditText()
  }

  /** «برگرداندن ردیف‌ها». */
  function restoreDrafts() {
    const list = drafts.takePending()
    mergeDrafts(list)
    notify(message('I_GRID_DRAFTS_RESTORED', { n: list.length }), 'success')
    save.flushSoon()
  }

  /** «پاک کن» پس از تأیید: toast «پاک شد [برگردان]». */
  function discardDrafts() {
    const count = drafts.discardPending()
    if (count === 0) return
    notify(message('I_GRID_DRAFTS_DISCARDED'), 'info', {
      label: 'برگردان',
      run: () => {
        const back = drafts.undoDiscard()
        if (back) {
          mergeDrafts(back)
          save.flushSoon()
        }
      },
    })
  }

  // ── پیام‌های جدول (toast با دکمه) ───────────────────────────────────

  function notify(text: string, kind: GridNotice['kind'] = 'info', action?: GridNotice['action']) {
    // همان پیامِ بی‌دکمه دوباره روی هم نمی‌نشیند (مثلاً «نشست شما تمام شده» در چند ذخیرهٔ پشت‌سرهم): قبلی جایش را
    // به تازه می‌دهد تا زمانش از نو شمرده شود
    if (!action) notices.value = notices.value.filter((n) => n.action || n.kind !== kind || n.text !== text)
    const id = noticeId++
    notices.value.push({ id, text, kind, action })
    globalThis.setTimeout(() => dismissNotice(id), NOTICE_MS)
    return id
  }

  function dismissNotice(id: number) {
    notices.value = notices.value.filter((n) => n.id !== id)
  }

  // ── نمایش ──────────────────────────────────────────────────────────

  /** مبلغ زندهٔ خانهٔ فعال به حروف (H.4)، برای سطر اطلاعات. */
  const wordsLine = computed(() => {
    const column = activeColumn.value
    const row = activeRow.value
    if (!column || !row || (column.type !== 'money' && column.type !== 'money+unit')) return ''
    let amount: string | null = null
    if (mode.value !== 'nav') {
      const parsed = parseDecimal(editText.value.replace(/(هر\s*متر|هر\s*تخته|کل)$/u, ''), 'money', {
        scale: ctx.moneyScale,
        currencyLabel: ctx.currency.label,
      })
      amount = parsed.status === 'ok' ? parsed.value : null
    } else {
      const value = row.values[column.key]
      if (column.type === 'money+unit') {
        const cell = readPurchaseCell(value, column)
        amount = cell?.rate !== null && cell?.rate !== undefined ? String(cell.rate) : null
      } else if (typeof value === 'number') amount = String(value)
    }
    return amount ? moneyWords(amount, ctx.currency.label) : ''
  })

  /** متن خانهٔ «متراژ» یک ردیف. */
  function areaOf(row: GridRowState): string {
    return areaText(rowArea(row.values))
  }

  /** پیشنهادهای خانهٔ combo: گزینه‌های ثابت + مقدارهای همین جدول + مقدارهای قبلی سرور. */
  async function valuesFor(key: string, q: string): Promise<string[]> {
    const column = colMap.value.get(key)
    const needle = fold(q)
    const seen = new Map<string, string>()
    const addValue = (v: unknown) => {
      if (typeof v !== 'string' || !v.trim()) return
      const f = fold(v)
      if (!seen.has(f)) seen.set(f, v)
    }
    for (const option of column?.options ?? []) addValue(option)
    for (const row of rows.value) addValue(row.values[key])
    const fetcher =
      options.fetchValues ??
      (async (k: string, text: string) =>
        api.get<string[]>(`/api/rugs/values?field=${encodeURIComponent(k)}&q=${encodeURIComponent(text)}&take=20`))
    try {
      for (const v of await fetcher(key, q)) addValue(v)
    } catch {
      /* بی‌پیشنهاد سرور هم کار می‌کند */
    }
    const all = [...seen.values()]
    const starts = all.filter((v) => fold(v).startsWith(needle))
    const contains = all.filter((v) => !fold(v).startsWith(needle) && fold(v).includes(needle))
    return [...starts, ...contains].slice(0, 20)
  }

  const counts = computed(() => {
    const real = rows.value.filter((r) => !r.placeholder)
    return {
      rows: real.length,
      unsaved: real.filter(hasUnsaved).length,
      invalid: real.filter((r) => r.status === 'invalid').length,
      conflict: real.filter((r) => r.status === 'conflict').length,
    }
  })

  /** نخستین ردیف مشکل‌دار (برای نوار «ذخیره نشد» و «نشانم بده»). */
  function firstProblem(): number {
    return rows.value.findIndex((r) => r.status === 'invalid' || r.status === 'conflict')
  }

  function showProblem() {
    const index = firstProblem()
    if (index < 0) return
    const row = rows.value[index]
    const key = Object.keys(row.errors)[0] ?? row.conflict?.fields[0]
    const col = Math.max(0, columns.value.findIndex((c) => c.key === key))
    moveTo({ row: index, col, runStart: null })
  }

  function dispose() {
    drafts.flush()
    drafts.dispose()
    save.dispose()
    release?.()
    release = null
  }

  return reactive({
    // وضعیت
    columns,
    rows,
    nav,
    mode,
    editText,
    editError,
    question,
    notices,
    focusTick,
    menuRowId,
    undoCount,
    loaded,
    layout,
    totals,
    serialMode,
    serverNext,
    suggestion,
    activeRow,
    activeColumn,
    selection,
    counts,
    wordsLine,
    saveState: save.state,
    unsavedCount: save.unsavedCount,
    draftsPending: drafts.pending,
    // خواندن
    isEditable,
    stickyValue,
    areaOf,
    canDeleteRow,
    valuesFor,
    copyText,
    conflictLines,
    serialLabel,
    // کار
    load,
    append,
    activate,
    onKeydown,
    onInput,
    startEdit,
    commit,
    cancelEdit,
    answer,
    paste,
    undo,
    fillDown,
    clearSelection,
    cycleUnit,
    acceptSuggestion,
    deleteRow,
    resolveConflict,
    acceptNextFree,
    editSerialOf,
    restoreDrafts,
    discardDrafts,
    notify,
    dismissNotice,
    showProblem,
    flush: () => save.flush(),
    retryNow: () => save.retryNow(),
    flushDrafts: () => drafts.flush(),
    checkDrafts: () => drafts.check(new Set(rows.value.map((r) => r.id))),
    dispose,
  })
}

export type GridController = ReturnType<typeof useGridController>

/** برای آزمون: یک ردیف ساختگی. */
export { emptyRow }
export type { ApiError }
