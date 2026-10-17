/**
 * ذخیرهٔ خودکار جدول (G.6، G.7):
 * - ۱٫۵ ثانیه پس از آخرین تغییر، یا همان لحظه که کاربر از ردیف بیرون می‌رود؛ همهٔ ردیف‌های آماده در یک درخواست
 *   (`POST /api/grid/rugs/batch`). در هر لحظه فقط یک درخواست در راه است؛ تغییرهای این فاصله در نوبت بعد می‌روند.
 * - ردیف تازه فقط وقتی فرستاده می‌شود که خانه‌های لازمش پر است (وگرنه پیش‌نویس می‌ماند).
 * - قطع شبکه یا خطای ۵xx: دوباره پس از ۲، ۴، ۸، ۱۶ و ۳۰ ثانیه (و بعد هر ۳۰ ثانیه)، با شمارش معکوس و «الان دوباره
 *   بفرست». پیش‌نویس‌ها روی دستگاه می‌مانند.
 * - خطای کل درخواست (۴۰۰/۴۰۹) با چند ردیف: هر ردیف جدا فرستاده می‌شود تا ردیف خراب جدا شود؛ با یک ردیف، خطا زیر
 *   همان ردیف می‌نشیند (همپوشانی شماره با `nextFree`).
 * - خطای کل درخواست که به ردیفی نمی‌خورد (۴۰۱ نشست تمام‌شده، ۴۰۳): خودبه‌خود دوباره فرستاده نمی‌شود. همان درخواست
 *   باز همان خطا را می‌گیرد؛ پیش‌تر هر ۱٫۵ ثانیه دوباره می‌رفت و هر بار یک پیام تازه روی هم می‌نشست. ردیف‌ها ذخیره‌نشده
 *   و در پیش‌نویس می‌مانند؛ تغییر بعدی کاربر یا «الان دوباره بفرست» دوباره امتحان می‌کند.
 */
import { computed, reactive } from 'vue'
import { ApiError } from '@/lib/api'
import type { GridBatchRequest, GridBatchResponse, GridOp, GridOpResult, JsonValue } from '@/lib/types/grid'
import {
  applyOpResult,
  applyRequestError,
  buildOp,
  hasUnsaved,
  type ApplyOutcome,
  type ColumnMap,
  type GridRowState,
} from './gridModel'

export const SAVE_IDLE_MS = 1500
export const RETRY_SCHEDULE_S = [2, 4, 8, 16, 30] as const

export type SavePhase = 'idle' | 'saving' | 'saved' | 'retrying' | 'error'

export interface SaveTimers {
  setTimeout: (fn: () => void, ms: number) => unknown
  clearTimeout: (handle: unknown) => void
}

export interface GridSaveOptions {
  rows: () => GridRowState[]
  columns: () => ColumnMap
  /** درخواست کامل برای این عمل‌ها (gridId، ورودی، محموله) */
  request: (ops: GridOp[]) => GridBatchRequest
  send: (request: GridBatchRequest) => Promise<unknown>
  /** آیا ردیف آمادهٔ ارسال است (ردیف تازه با خانه‌های لازم) */
  ready: (row: GridRowState) => boolean
  /** پس از نشاندن نتیجهٔ هر ردیف */
  onApplied?: (row: GridRowState, outcome: ApplyOutcome | 'error') => void
  /** خطای کل درخواست که به ردیف خاصی نمی‌خورد (۴۰۳ پول، …) */
  onRequestError?: (error: ApiError) => void
  /** پاسخ کامل هر ذخیرهٔ موفق (جمع‌های تازه و شمارهٔ بعدی) */
  onResponse?: (response: GridBatchResponse | null) => void
  idleMs?: number
  retrySchedule?: readonly number[]
  timers?: SaveTimers
}

/** نتیجه‌های پاسخ، چه آرایه چه `{ results }`. */
export function resultsOf(response: unknown): GridOpResult[] {
  if (Array.isArray(response)) return response as GridOpResult[]
  if (response && typeof response === 'object' && Array.isArray((response as GridBatchResponse).results)) {
    return (response as GridBatchResponse).results
  }
  return []
}

/** آیا خطا گذراست و باید دوباره امتحان شود (قطع شبکه یا خطای سرور). */
export function isTransient(error: unknown): boolean {
  if (error instanceof TypeError) return true
  if (error instanceof ApiError) return error.status >= 500 || error.status === 408 || error.status === 429
  return !(error instanceof ApiError)
}

/** چند ثانیه تا تلاش شمارهٔ `attempt` (از ۱). */
export function retryDelay(attempt: number, schedule: readonly number[] = RETRY_SCHEDULE_S): number {
  if (schedule.length === 0) return 30
  return schedule[Math.min(Math.max(attempt, 1), schedule.length) - 1]
}

export function useGridSave(options: GridSaveOptions) {
  const timers: SaveTimers = options.timers ?? {
    setTimeout: (fn, ms) => globalThis.setTimeout(fn, ms),
    clearTimeout: (h) => globalThis.clearTimeout(h as ReturnType<typeof setTimeout>),
  }
  const idleMs = options.idleMs ?? SAVE_IDLE_MS
  const schedule_ = options.retrySchedule ?? RETRY_SCHEDULE_S

  const state = reactive({
    phase: 'idle' as SavePhase,
    /** ثانیه تا تلاش بعدی */
    retryIn: 0,
    attempt: 0,
    lastError: '',
    inFlight: false,
  })

  let idleTimer: unknown = null
  let retryTimer: unknown = null
  let tickTimer: unknown = null
  let again = false
  let disposed = false
  const sentRows = new WeakSet<GridRowState>()
  const idleWaiters: (() => void)[] = []

  function waitForIdle(): Promise<void> {
    return state.inFlight ? new Promise((resolve) => idleWaiters.push(resolve)) : Promise.resolve()
  }

  const unsavedCount = computed(() => options.rows().filter(hasUnsaved).length)

  function clear(handle: unknown) {
    if (handle !== null) timers.clearTimeout(handle)
  }

  /** ۱٫۵ ثانیه پس از آخرین تغییر. */
  function schedule(): void {
    if (disposed) return
    clear(idleTimer)
    idleTimer = timers.setTimeout(() => {
      idleTimer = null
      void flush()
    }, idleMs)
  }

  /** همین حالا (مثلاً کاربر از ردیف بیرون رفت). */
  function flushSoon(): void {
    if (disposed) return
    clear(idleTimer)
    idleTimer = timers.setTimeout(() => {
      idleTimer = null
      void flush()
    }, 0)
  }

  function stopRetry() {
    clear(retryTimer)
    clear(tickTimer)
    retryTimer = null
    tickTimer = null
    state.retryIn = 0
  }

  function scheduleRetry() {
    state.attempt++
    const seconds = retryDelay(state.attempt, schedule_)
    state.retryIn = seconds
    state.phase = 'retrying'
    clear(retryTimer)
    clear(tickTimer)
    retryTimer = timers.setTimeout(() => {
      retryTimer = null
      clear(tickTimer)
      tickTimer = null
      void flush()
    }, seconds * 1000)
    const tick = () => {
      if (state.retryIn > 1) {
        state.retryIn--
        tickTimer = timers.setTimeout(tick, 1000)
      }
    }
    tickTimer = timers.setTimeout(tick, 1000)
  }

  /** «الان دوباره بفرست». */
  function retryNow(): Promise<void> {
    stopRetry()
    for (const row of options.rows()) if (row.status === 'invalid') row.status = 'dirty'
    return flush()
  }

  interface Pending {
    row: GridRowState
    op: GridOp
    sent: Record<string, JsonValue>
  }

  function collect(): Pending[] {
    const columns = options.columns()
    const list: Pending[] = []
    for (const row of options.rows()) {
      if (!hasUnsaved(row) || row.saving || row.conflict || row.status === 'invalid' || !options.ready(row)) continue
      const built = buildOp(row, columns)
      if (built) list.push({ row, op: built.op, sent: built.sent })
    }
    return list
  }

  function applyAll(batch: Pending[], results: GridOpResult[]) {
    const columns = options.columns()
    const byId = new Map(results.map((r) => [r.clientRowId, r]))
    for (const item of batch) {
      const result = byId.get(item.row.id)
      if (!result) {
        // سرور دربارهٔ این ردیف چیزی نگفت: دوباره فرستاده می‌شود
        item.row.saving = false
        continue
      }
      const outcome = applyOpResult(item.row, result, item.sent, columns)
      options.onApplied?.(item.row, outcome)
    }
  }

  async function sendBatch(batch: Pending[]): Promise<void> {
    for (const item of batch) sentRows.add(item.row)
    const response = await options.send(options.request(batch.map((b) => b.op)))
    applyAll(batch, resultsOf(response))
    options.onResponse?.(Array.isArray(response) || !response || typeof response !== 'object' ? null : (response as GridBatchResponse))
  }

  /** همهٔ ردیف‌های آماده را بفرست. */
  async function flush(): Promise<void> {
    if (disposed) return
    if (state.inFlight) {
      again = true
      return
    }
    const batch = collect()
    if (batch.length === 0) {
      if (state.phase === 'saving') state.phase = 'idle'
      return
    }

    stopRetry()
    state.inFlight = true
    state.phase = 'saving'
    for (const item of batch) item.row.saving = true
    /** خطای کل درخواست (۴۰۱/۴۰۳): نوبت خودکار بعدی نمی‌آید */
    let halted = false

    try {
      await sendBatch(batch)
      state.attempt = 0
      state.lastError = ''
      state.phase = 'saved'
    } catch (error) {
      for (const item of batch) item.row.saving = false
      if (isTransient(error)) {
        state.lastError = error instanceof ApiError ? error.message : ''
        state.inFlight = false
        scheduleRetry()
        return
      }
      const apiError = error as ApiError
      if ((apiError.status === 400 || apiError.status === 409 || apiError.status === 422) && batch.length > 1) {
        // ردیف خراب را جدا کن: یکی‌یکی
        await isolate(batch)
      } else if (apiError.status === 400 || apiError.status === 409 || apiError.status === 422 || apiError.status === 404) {
        const columns = options.columns()
        for (const item of batch) {
          applyRequestError(item.row, apiError, columns)
          options.onApplied?.(item.row, 'error')
        }
        state.phase = 'error'
      } else {
        // ۴۰۱/۴۰۳ و مانند آن: به هیچ ردیفی نمی‌خورد و تکرارش همان را می‌گیرد
        state.lastError = apiError.message
        state.phase = 'error'
        halted = true
        options.onRequestError?.(apiError)
      }
    } finally {
      state.inFlight = false
      for (const resolve of idleWaiters.splice(0)) resolve()
    }

    if (halted) {
      again = false
      return
    }
    if (again || collect().length > 0) {
      again = false
      schedule()
    }
  }

  async function isolate(batch: Pending[]): Promise<void> {
    const columns = options.columns()
    let failed = false
    for (const item of batch) {
      item.row.saving = true
      try {
        await sendBatch([item])
      } catch (error) {
        item.row.saving = false
        if (isTransient(error)) {
          failed = true
          continue
        }
        applyRequestError(item.row, error as ApiError, columns)
        options.onApplied?.(item.row, 'error')
      }
    }
    state.phase = failed ? 'error' : 'saved'
    if (failed) {
      state.inFlight = false
      scheduleRetry()
    }
  }

  function dispose(): void {
    disposed = true
    clear(idleTimer)
    stopRetry()
  }

  return { state, unsavedCount, schedule, flushSoon, flush, retryNow, dispose, waitForIdle, wasSent: (row: GridRowState) => sentRows.has(row) }
}
