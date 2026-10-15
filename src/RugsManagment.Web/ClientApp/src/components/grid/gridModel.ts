/**
 * مدل ردیف جدول و قاعده‌های ذخیره (G.6) — خالص و آزمون‌پذیر.
 *
 * هر ردیف سه لایه دارد: `values` (آنچه کاربر می‌بیند)، `base` (آخرین مقدار شناخته‌شدهٔ سرور، پایهٔ ادغام سه‌طرفه) و
 * `dirty` (کلیدهایی که هنوز ذخیره نشده‌اند). هر عمل فقط کلیدهای تغییرکرده را با مقدار پایه‌شان می‌فرستد؛ پس اگر کس دیگری
 * خانهٔ دیگری از همان فرش را عوض کرده باشد، سرور خودش ادغام می‌کند و فقط وقتی هر دو یک خانه را عوض کرده‌اند «تعارض»
 * برمی‌گردد. ردیف تازه با `ClientRowId` خودش ساخته می‌شود تا ارسال دوباره (پس از قطع شبکه) فرش دوم نسازد.
 */
import type { ApiError } from '@/lib/api'
import type { DraftRow } from '@/lib/gridDrafts'
import { newId } from '@/lib/ids'
import { cmp, tryDec } from '@/lib/num'
import {
  GridRowStatus,
  type GridColumn,
  type GridOp,
  type GridOpResult,
  type GridRow,
  type JsonValue,
} from '@/lib/types/grid'
import { isMultiLine, isPurchaseKey, KEY_SERIAL, readPurchaseCell, writePurchaseCell } from './gridParse'

export type RowStatus = 'clean' | 'dirty' | 'invalid' | 'conflict'

export interface RowConflict {
  /** خانه‌هایی که هر دو طرف عوض کرده‌اند */
  fields: string[]
  /** مقدار فعلی سرور */
  current: Record<string, JsonValue>
  /** نسخهٔ فعلی سرور */
  version: number | null
}

export interface GridRowState {
  /** `ClientRowId` (برای ردیف‌های بارشده از سرور هم یکی ساخته می‌شود تا نتیجه‌ها جفت شوند) */
  id: string
  rugId: string | null
  version: number | null
  sku: string | null
  values: Record<string, JsonValue>
  base: Record<string, JsonValue>
  derived: Record<string, JsonValue>
  dirty: string[]
  /** خطای هر خانه (کلید ستون) */
  errors: Record<string, string>
  /** خطایی که به خانهٔ دیده‌شده‌ای نمی‌خورد */
  rowError: string | null
  /** هشدار غیرمانع (مساحت بیش از ۶۰ متر) */
  warning: string | null
  status: RowStatus
  /** همین حالا در راه سرور است */
  saving: boolean
  conflict: RowConflict | null
  /** شماره از پیشنهاد آمده؛ پس با تغییر تعداد دسته می‌شود (G.4) */
  serialSuggested: boolean
  /** ردیف خالیِ ته جدول (هنوز فرشی نیست) */
  placeholder: boolean
  /** شمارهٔ آزاد پیشنهادی سرور پس از همپوشانی */
  nextFree: number | null
  lineId: string | null
  lineVersion: number | null
}

export function emptyRow(partial: Partial<GridRowState> = {}): GridRowState {
  return {
    id: newId(),
    rugId: null,
    version: null,
    sku: null,
    values: {},
    base: {},
    derived: {},
    dirty: [],
    errors: {},
    rowError: null,
    warning: null,
    status: 'clean',
    saving: false,
    conflict: null,
    serialSuggested: false,
    placeholder: false,
    nextFree: null,
    lineId: null,
    lineVersion: null,
    ...partial,
  }
}

// ── مقدارها ────────────────────────────────────────────────────────────

/** کپی عمیق یک مقدار JSON. */
export function cloneJson<T extends JsonValue | undefined>(value: T): T {
  if (value === null || value === undefined || typeof value !== 'object') return value
  return JSON.parse(JSON.stringify(value)) as T
}

function isPurchaseColumn(column: GridColumn | undefined, key: string): boolean {
  return isPurchaseKey(key) && (column?.type === 'money+unit' || column?.type === 'percent' || column === undefined)
}

/** مقدار سرور → شکل درونی (خانهٔ خرید یکسان می‌شود؛ undefined → null). */
export function normalizeValue(key: string, column: GridColumn | undefined, raw: JsonValue | undefined): JsonValue {
  if (raw === undefined) return null
  if (isPurchaseColumn(column, key)) {
    const cell = readPurchaseCell(raw, column ?? { type: 'money+unit', options: null })
    return cell ? (cell as unknown as JsonValue) : null
  }
  return raw
}

function isBlank(value: JsonValue | undefined): boolean {
  return value === null || value === undefined || value === ''
}

/** برابری مقدار دو خانه: عددها با مقدار، خانهٔ خرید با نرخ و مبنا (و چندسطری بودن)، متن دقیق. */
export function sameValue(key: string, column: GridColumn | undefined, a: JsonValue | undefined, b: JsonValue | undefined): boolean {
  if (isBlank(a) && isBlank(b)) return true
  if (isBlank(a) || isBlank(b)) {
    if (isPurchaseColumn(column, key)) {
      const ca = readPurchaseCell(a, column ?? { type: 'money+unit', options: null })
      const cb = readPurchaseCell(b, column ?? { type: 'money+unit', options: null })
      return (ca?.rate ?? null) === null && (cb?.rate ?? null) === null && !isMultiLine(ca) && !isMultiLine(cb)
    }
    return false
  }
  if (isPurchaseColumn(column, key)) {
    const kind = column ?? { type: 'money+unit' as const, options: null }
    const ca = readPurchaseCell(a, kind)
    const cb = readPurchaseCell(b, kind)
    if (!ca || !cb) return !ca && !cb
    if (isMultiLine(ca) || isMultiLine(cb)) return isMultiLine(ca) && isMultiLine(cb) && sameNumber(ca.amount, cb.amount)
    return sameNumber(ca.rate, cb.rate) && (column?.type === 'percent' || ca.basis === cb.basis)
  }
  if (typeof a === 'number' || typeof b === 'number') return sameNumber(a, b)
  if (typeof a === 'string' && typeof b === 'string') return a.trim() === b.trim()
  return JSON.stringify(a) === JSON.stringify(b)
}

function sameNumber(a: unknown, b: unknown): boolean {
  const x = tryDec(a as never)
  const y = tryDec(b as never)
  if (!x || !y) return !x && !y
  return cmp(x, y) === 0
}

/** مقدار درونی → مقدار ارسالی (`RugFieldWriter`): خانهٔ خرید `{rate, basis}` یا عدد یا null. */
export function wireValue(key: string, column: GridColumn | undefined, value: JsonValue | undefined): JsonValue {
  if (value === undefined) return null
  if (isPurchaseColumn(column, key)) {
    const kind = column ?? { type: 'money+unit' as const, options: null }
    return writePurchaseCell(readPurchaseCell(value, kind), kind.type)
  }
  return value
}

// ── ساختن ردیف ─────────────────────────────────────────────────────────

export type ColumnMap = ReadonlyMap<string, GridColumn>

export function columnMap(columns: readonly GridColumn[]): Map<string, GridColumn> {
  return new Map(columns.map((c) => [c.key, c]))
}

/** ردیف سرور → وضعیت جدول. */
export function rowFromServer(row: GridRow, columns: ColumnMap): GridRowState {
  const values: Record<string, JsonValue> = {}
  for (const [key, raw] of Object.entries(row.values ?? {})) values[key] = normalizeValue(key, columns.get(key), raw)
  const derived: Record<string, JsonValue> = {}
  for (const [key, raw] of Object.entries(row.derived ?? {})) derived[key] = raw ?? null
  return emptyRow({
    rugId: row.rugId,
    version: row.version,
    sku: row.sku,
    values,
    base: cloneJson(values as JsonValue) as Record<string, JsonValue>,
    derived,
    lineId: row.lineId ?? null,
    lineVersion: row.lineVersion ?? null,
  })
}

/** ردیف → پیش‌نویس (فقط کلیدهای تغییرکرده؛ ردیف تازه همهٔ کلیدهای پر). */
export function rowToDraft(row: GridRowState): DraftRow {
  const keys = row.rugId === null ? Object.keys(row.values).filter((k) => !isBlank(row.values[k])) : row.dirty
  const values: Record<string, JsonValue> = {}
  const base: Record<string, JsonValue> = {}
  for (const key of new Set([...keys, ...row.dirty])) {
    values[key] = cloneJson(row.values[key] ?? null)
    base[key] = cloneJson(row.base[key] ?? null)
  }
  return {
    clientRowId: row.id,
    rugId: row.rugId,
    version: row.version,
    values,
    base,
    dirty: [...row.dirty],
    serialSuggested: row.serialSuggested,
  }
}

/**
 * پیش‌نویس را روی ردیف بارشدهٔ همان فرش (اگر هست) می‌نشاند، وگرنه ردیف تازه می‌سازد. پایه و نسخه همان پیش‌نویس‌اند تا
 * ادغام سه‌طرفهٔ سرور ببیند چه چیزی واقعاً عوض شده بود.
 */
export function rowFromDraft(draft: DraftRow, existing: GridRowState | null): GridRowState {
  const row = existing ?? emptyRow({ id: draft.clientRowId, rugId: draft.rugId, version: draft.version })
  row.id = draft.clientRowId
  if (draft.rugId) row.rugId = draft.rugId
  if (draft.version !== null) row.version = draft.version
  const dirty = new Set(row.dirty)
  for (const [key, value] of Object.entries(draft.values)) {
    row.values[key] = cloneJson(value)
    if (key in draft.base) row.base[key] = cloneJson(draft.base[key])
  }
  for (const key of draft.dirty) dirty.add(key)
  if (row.rugId === null) for (const key of Object.keys(draft.values)) dirty.add(key)
  row.dirty = [...dirty]
  row.serialSuggested = draft.serialSuggested ?? false
  row.placeholder = false
  row.status = row.dirty.length > 0 ? 'dirty' : 'clean'
  return row
}

// ── ارسال ──────────────────────────────────────────────────────────────

/** آیا ردیف چیزی برای ذخیره دارد. */
export function hasUnsaved(row: GridRowState): boolean {
  return !row.placeholder && row.dirty.length > 0
}

/** ستون‌های لازمی که ردیف تازه هنوز ندارد. */
export function missingRequired(row: GridRowState, columns: readonly GridColumn[], skip: ReadonlySet<string> = new Set()): GridColumn[] {
  if (row.rugId !== null) return []
  return columns.filter((c) => c.required && !c.readOnly && c.type !== 'readonly' && !skip.has(c.key) && isBlank(row.values[c.key]))
}

export interface BuiltOp {
  op: GridOp
  /** مقدارهای درونیِ فرستاده‌شده (برای جدا کردن تغییرهای پس از ارسال) */
  sent: Record<string, JsonValue>
}

/**
 * عمل ذخیرهٔ یک ردیف: ردیف تازه همهٔ خانه‌های پرش را می‌فرستد (بی پایه)؛ ردیف موجود فقط کلیدهای تغییرکرده با
 * مقدار پایه‌شان. چیزی برای فرستادن نیست → null.
 */
export function buildOp(row: GridRowState, columns: ColumnMap): BuiltOp | null {
  if (row.placeholder) return null
  const isNew = row.rugId === null
  const keys = isNew
    ? [...new Set([...Object.keys(row.values).filter((k) => !isBlank(row.values[k])), ...row.dirty])]
    : [...row.dirty]
  if (keys.length === 0 || (!isNew && row.dirty.length === 0)) return null

  const values: Record<string, JsonValue> = {}
  const baseValues: Record<string, JsonValue> = {}
  const sent: Record<string, JsonValue> = {}
  for (const key of keys) {
    const column = columns.get(key)
    if (column && (column.readOnly || column.type === 'readonly')) continue
    // خانهٔ خرید چندسطری فقط‌خواندنی است
    if (isPurchaseKey(key) && isMultiLine(readPurchaseCell(row.values[key], column ?? { type: 'money+unit', options: null }))) continue
    values[key] = wireValue(key, column, row.values[key])
    sent[key] = cloneJson(row.values[key] ?? null)
    if (!isNew) baseValues[key] = wireValue(key, column, row.base[key])
  }
  if (Object.keys(values).length === 0) return null

  return {
    op: {
      clientRowId: row.id,
      rugId: row.rugId,
      version: row.version,
      values,
      baseValues: isNew ? null : baseValues,
      delete: false,
      lineVersion: row.lineVersion,
    },
    sent,
  }
}

/** عمل حذف یک فرش. */
export function deleteOp(row: GridRowState): GridOp {
  return {
    clientRowId: row.id,
    rugId: row.rugId,
    version: row.version,
    values: {},
    baseValues: null,
    delete: true,
    lineVersion: row.lineVersion,
  }
}

// ── نتیجه ──────────────────────────────────────────────────────────────

/** خطاهای سرور → خانه‌ها؛ خطای کلیدی که ستون دیده‌شده ندارد خطای ردیف می‌شود. */
function placeErrors(row: GridRowState, errors: { field: string; message: string }[], columns: ColumnMap): void {
  const rowLevel: string[] = []
  for (const error of errors) {
    const field = normalizeField(error.field)
    if (field && columns.has(field)) row.errors[field] = error.message
    else rowLevel.push(error.message)
  }
  row.rowError = rowLevel.length > 0 ? rowLevel.join(' ') : null
}

/** «rug.quantity» → «rug.qty»؛ «$.values.rug.serial» → «rug.serial». */
export function normalizeField(field: string | null | undefined): string {
  if (!field) return ''
  let f = field.replace(/^\$\./, '').replace(/^values\./i, '')
  if (f === 'rug.quantity') f = 'rug.qty'
  return f
}

function readNextFree(...sources: (Record<string, JsonValue | undefined> | null | undefined)[]): number | null {
  for (const source of sources) {
    const raw = source?.nextFree ?? source?.['rug.nextFree']
    if (typeof raw === 'number' && Number.isSafeInteger(raw)) return raw
  }
  return null
}

export type ApplyOutcome = 'saved' | 'invalid' | 'conflict' | 'deleted'

/**
 * نتیجهٔ یک عمل را روی ردیف می‌نشاند.
 * - ذخیره: نسخه، شناسهٔ فرش، مقدارهای حساب‌شده؛ کلیدهای فرستاده پایه می‌شوند و اگر کاربر در این فاصله دوباره عوضشان
 *   نکرده، از «تغییرکرده» درمی‌آیند. شمارهٔ خودکار سرور (`derived['rug.serial']`) هم نشانده می‌شود.
 * - نادرست: خطای هر خانه زیر همان خانه؛ نوشته‌ها می‌مانند.
 * - تعارض: خانه‌هایی که فقط آن طرف عوض کرده بی‌صدا ادغام می‌شوند؛ اگر خانه‌ای را هر دو عوض کرده‌اند، ردیف منتظر
 *   «نوشتهٔ من بماند / نوشتهٔ او را ببینم» می‌ماند؛ وگرنه با نسخهٔ تازه دوباره فرستاده می‌شود.
 */
export function applyOpResult(row: GridRowState, result: GridOpResult, sent: Record<string, JsonValue>, columns: ColumnMap): ApplyOutcome {
  row.saving = false
  switch (result.status) {
    case GridRowStatus.Deleted:
      return 'deleted'

    case GridRowStatus.Saved: {
      if (result.rugId) row.rugId = result.rugId
      if (result.version !== null && result.version !== undefined) row.version = result.version
      if (result.sku !== undefined && result.sku !== null) row.sku = result.sku
      if (result.lineVersion !== undefined && result.lineVersion !== null) row.lineVersion = result.lineVersion
      for (const [key, value] of Object.entries(result.derived ?? {})) row.derived[key] = value ?? null

      const dirty = new Set(row.dirty)
      for (const [key, value] of Object.entries(sent)) {
        row.base[key] = cloneJson(value)
        if (sameValue(key, columns.get(key), row.values[key], value)) dirty.delete(key)
        delete row.errors[key]
      }
      // شمارهٔ خودکار (حالت «خودکار») و هر مقدار تازهٔ سرور برای خانه‌ای که کاربر در این فاصله دست نزده
      const serial = result.derived?.[KEY_SERIAL]
      if (typeof serial === 'string' && !dirty.has(KEY_SERIAL)) {
        row.values[KEY_SERIAL] = serial
        row.base[KEY_SERIAL] = serial
      }
      for (const [key, raw] of Object.entries(result.current ?? {})) {
        if (dirty.has(key)) continue
        const value = normalizeValue(key, columns.get(key), raw)
        row.values[key] = value
        row.base[key] = cloneJson(value)
      }
      row.dirty = [...dirty]
      row.rowError = null
      row.conflict = null
      row.nextFree = null
      row.status = row.dirty.length > 0 ? 'dirty' : 'clean'
      return 'saved'
    }

    case GridRowStatus.Invalid: {
      if (result.rugId) row.rugId = result.rugId
      if (result.version !== null && result.version !== undefined) row.version = result.version
      row.errors = {}
      placeErrors(row, result.errors ?? [], columns)
      row.nextFree = typeof result.nextFree === 'number' ? result.nextFree : readNextFree(result.derived, result.current)
      row.status = 'invalid'
      return 'invalid'
    }

    case GridRowStatus.Conflict:
    default: {
      const current: Record<string, JsonValue> = {}
      for (const [key, raw] of Object.entries(result.current ?? {})) current[key] = normalizeValue(key, columns.get(key), raw)
      if (result.version !== null && result.version !== undefined) row.version = result.version

      const dirty = new Set(row.dirty)
      const fields: string[] = []
      for (const [key, value] of Object.entries(current)) {
        const column = columns.get(key)
        if (!dirty.has(key)) {
          // فقط آن طرف عوض کرده: ادغام بی‌صدا
          row.values[key] = value
          row.base[key] = cloneJson(value)
          continue
        }
        if (sameValue(key, column, value, row.values[key])) {
          // هر دو یکی نوشته‌اند
          row.base[key] = cloneJson(value)
          dirty.delete(key)
        } else if (!sameValue(key, column, value, row.base[key])) {
          fields.push(key)
        }
      }
      row.dirty = [...dirty]
      if (fields.length === 0 && Object.keys(current).length === 0) fields.push(...row.dirty)
      if (fields.length === 0) {
        row.conflict = null
        row.status = row.dirty.length > 0 ? 'dirty' : 'clean'
        return 'saved'
      }
      row.conflict = { fields, current, version: result.version ?? null }
      row.status = 'conflict'
      return 'conflict'
    }
  }
}

/** «نوشتهٔ من بماند»: پایهٔ خانه‌های درگیر همان مقدار فعلی سرور می‌شود تا ارسال بعدی بنشیند. */
export function keepMine(row: GridRowState): void {
  if (!row.conflict) return
  for (const key of row.conflict.fields) row.base[key] = cloneJson(row.conflict.current[key] ?? null)
  if (row.conflict.version !== null) row.version = row.conflict.version
  row.conflict = null
  row.status = row.dirty.length > 0 ? 'dirty' : 'clean'
}

/** «نوشتهٔ او را ببینم»: خانه‌های درگیر مقدار سرور را می‌گیرند و از «تغییرکرده» درمی‌آیند. */
export function takeTheirs(row: GridRowState): void {
  if (!row.conflict) return
  const dirty = new Set(row.dirty)
  for (const key of row.conflict.fields) {
    const value = cloneJson(row.conflict.current[key] ?? null)
    row.values[key] = value
    row.base[key] = cloneJson(value)
    dirty.delete(key)
    delete row.errors[key]
  }
  if (row.conflict.version !== null) row.version = row.conflict.version
  row.dirty = [...dirty]
  row.conflict = null
  row.status = row.dirty.length > 0 ? 'dirty' : 'clean'
}

/**
 * خطای کل درخواست (وقتی فقط همین یک عمل در آن بود) → خطای ردیف: ۴۰۰ خطای هر خانه، ۴۰۹ خانهٔ درگیر (و `nextFree`
 * همپوشانی شماره)، بقیه خطای ردیف.
 */
export function applyRequestError(row: GridRowState, error: Pick<ApiError, 'status' | 'message' | 'errors' | 'field' | 'body'>, columns: ColumnMap): void {
  row.saving = false
  row.errors = {}
  const list: { field: string; message: string }[] = []
  for (const [field, messages] of Object.entries(error.errors ?? {})) {
    if (messages.length > 0) list.push({ field, message: messages[0] })
  }
  if (error.status === 409 && error.field) list.push({ field: error.field, message: error.message })
  if (list.length === 0) list.push({ field: '', message: error.message })
  placeErrors(row, list, columns)
  row.nextFree = readNextFree(error.body as Record<string, JsonValue> | null)
  row.status = 'invalid'
}
