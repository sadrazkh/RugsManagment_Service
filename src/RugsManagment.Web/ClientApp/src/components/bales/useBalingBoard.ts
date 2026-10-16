/**
 * مغز تختهٔ نگله‌بندی — یک جا برای جدول، کارت‌ها (زبانهٔ «نگله‌ها») و حالت اسکن گوشی، تا هر سه یک رفتار داشته باشند.
 *
 * - **نگلهٔ جاری** (C.2) برای هر کاربر و هر محموله در localStorage؛ «نگلهٔ بعد» فقط شماره را بالا می‌برد و چیزی
 *   نمی‌سازد. نگله را اولین فرشش می‌سازد (`add` با `baleNumber` تازه)، پس هرگز نگلهٔ خالی جا نمی‌ماند.
 * - **افزودن با شماره یا اسکن** (C.3): پرسش‌های سرور پیش از انجام کار («این فرش در نگلهٔ ۲ است؛ منتقل شود؟»، «این شماره
 *   داخل دستهٔ ۳۹۱۱ تا ۳۹۶۰ است: [کل دسته را بگذار] [جدا کن]») دو یا سه دکمه می‌شوند.
 * - **هر کار یک قدم «برگرد»** (انتقال، خروج، اندازه، افزودن)، و خروج از نگله پیام «۳۷۷۸ به انبار برگشت [برگردان]»
 *   دارد که فرش را در همان نگله و همان Seq برمی‌گرداند.
 * - **قفل و تعارض:** محمولهٔ قطعی فقط‌خواندنی است (پیام قفل فارسی سرور)؛ نسخهٔ کهنه (کس دیگری همان ردیف را عوض کرد)
 *   نگله‌ها را دوباره بار می‌کند و پیام می‌دهد، نه این‌که چیزی را بی‌صدا بنویسد.
 * - **قطع اینترنت** (G.5، G.7): افزودنی که نرسید روی همین دستگاه می‌ماند و فقط با «الان دوباره بفرست» دوباره می‌رود؛
 *   فرستادن خودکار در پس‌زمینه نیست. همان `clientOpId` دوباره می‌رود و «افزودن» با عضویت یکتاست، پس دوبار نمی‌شود.
 * - **همگامی با جزیره‌های دیگر** (سربرگ محموله): هر تغییر `rugs:shipment-changed` می‌فرستد؛ فرمان‌های سربرگ
 *   (`rugs:bales-command`، مثلاً «بقیه همان اندازهٔ اول») را می‌پذیرد.
 */
import { computed, onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue'
import { api, ApiError, errorMessage, isNetworkError } from '@/lib/api'
import { appContext } from '@/lib/context'
import { draftKey, safeStorage } from '@/lib/gridDrafts'
import { newId } from '@/lib/ids'
import { coreMessages, message } from '@/lib/messages'
import { blockReload, confirmDialog, toast } from '@/lib/ui'
import {
  BALES_COMMAND_EVENT,
  SHIPMENT_CHANGED_EVENT,
  type BalesCommandDetail,
  type FinalSizeInput,
  type MoveTarget,
  type PendingAdd,
  type RecentAdd,
  type ShipmentChangedDetail,
} from '@/lib/types/bales'
import type { GridBatchRequest, GridBatchResponse, GridColumn, GridData } from '@/lib/types/grid'
import {
  ShipmentStatus,
  type AddDeclaredValueRequest,
  type Bale,
  type BaleRow,
  type DeclaredValues,
  type DeclaredValuesWriteResult,
  type LineOp,
  type LineOpPrompt,
  type LineOpResult,
  type LinesBatchResult,
  type ReadinessIssue,
} from '@/lib/types/shipments'
import {
  allRows,
  applyBatchResult,
  currentBaleKey,
  descriptiveValue,
  DESCRIPTIVE_FIELD,
  faDigitsOf,
  finalSizeOf,
  inverseOps,
  maxBaleNumber,
  measureOp,
  readCurrentBale,
  reorderSeq,
  resolveCurrent,
  serialText,
  shiftsOthers,
  withDescriptive,
  writeCurrentBale,
  type BoardColumnKey,
  type UndoEntry,
} from './baleModel'

// ── نوع‌ها ─────────────────────────────────────────────────────────────

export interface BoardChoice {
  label: string
  /** دکمهٔ اصلی (Enter) */
  primary?: boolean
  icon?: string
  run: () => void | Promise<void>
}

/** پرسشی که پیش از انجام کار باید جواب بگیرد (دکمه‌های ۴۴ پیکسلی؛ Enter اولی، Esc آخری). */
export interface BoardQuestion {
  id: number
  message: string
  choices: BoardChoice[]
}

/** پیام کوتاه با دکمهٔ اختیاری («۳۷۷۸ به انبار برگشت [برگردان]»). */
export interface BoardNotice {
  id: number
  text: string
  kind: 'info' | 'success' | 'warning' | 'error'
  action?: { label: string; run: () => void | Promise<void> }
}

/** فرشی که باید جدا شود (پاسخ «جدا کن» به «داخل دسته»). */
export interface SplitRequest {
  rugId: string
  serial: string | null
  quantity: number
  /** شماره‌ای که کاربر زد؛ پس از جدا شدن دوباره فرستاده می‌شود */
  retrySerial: string | null
}

export interface BoardOptions {
  shipmentId: () => string
  locked: () => boolean
  targetAreaM2: () => number | null
  /** نام جزیره برای رویدادها (تا خودش دوباره واکنش نشان ندهد) */
  source: string
  /** نگله‌ای که پس از بار «جاری» شود (حالت اسکن `?bale=`) */
  initialBaleId?: () => string | null
  /** پس از هر افزودن موفق (فوکوس در «طول» آن ردیف، یا پرسیدن اندازه) */
  onAdded?: (row: BaleRow, via: 'serial' | 'rug' | 'undo') => void
  /** «جدا کن» برای دسته‌ای که شماره داخلش بود */
  onSplitRequested?: (request: SplitRequest) => void
  /** چیدمان ستون‌های جدول را هم بار کن (فقط زبانهٔ نگله‌ها) */
  loadLayout?: boolean
  /** شمارهٔ محموله برای پیام قفل */
  shipmentNumber?: () => number | null
}

const NOTICE_MS = 8000
const RECENT_LIMIT = 5
const UNDO_LIMIT = 50

/** پاسخ سرور برای کاری که فقط همین درخواست را داشت. */
interface SendOutcome {
  result: LinesBatchResult | null
  error: unknown
}

export function useBalingBoard(options: BoardOptions) {
  const ctx = appContext()
  const shipmentId = computed(() => options.shipmentId())

  // ── وضعیت ──────────────────────────────────────────────────────────

  const bales = ref<Bale[]>([])
  const loaded = ref(false)
  const loading = ref(false)
  const loadError = ref('')
  const lockedLocal = ref<boolean | null>(null)
  const locked = computed(() => lockedLocal.value ?? options.locked())
  const busy = ref(0)
  const readiness = ref<ReadinessIssue[]>([])
  const layout = shallowRef<GridColumn[] | null>(null)
  const current = ref(1)
  const question = ref<BoardQuestion | null>(null)
  const notices = ref<BoardNotice[]>([])
  const pending = ref<PendingAdd[]>([])
  const recent = ref<RecentAdd[]>([])
  const lastError = ref('')
  /** «نشانم بده»: فقط این فرش‌ها؛ null یعنی همه */
  const onlyRugIds = shallowRef<ReadonlySet<string> | null>(null)
  const undoCount = ref(0)
  const undoStack: UndoEntry[] = []
  let noticeId = 1
  let questionId = 1

  const rows = computed(() => allRows(bales.value))
  const maxNumber = computed(() => maxBaleNumber(bales.value))
  const currentBale = computed(() => bales.value.find((b) => b.number === current.value) ?? null)
  const totals = computed(() => ({
    bales: bales.value.length,
    lines: rows.value.length,
    pieces: bales.value.reduce((n, b) => n + b.pieces, 0),
  }))

  const storageKey = computed(() => currentBaleKey(ctx.tenantId, ctx.userId, shipmentId.value))
  const pendingKey = computed(() =>
    draftKey({ tenantId: ctx.tenantId, userId: ctx.userId, gridId: 'bales', contextId: shipmentId.value }),
  )

  function setCurrent(number: number) {
    if (!Number.isSafeInteger(number) || number < 1) return
    current.value = number
    writeCurrentBale(storageKey.value, number)
  }

  /** «نگلهٔ بعد»: فقط شماره (بزرگ‌ترین + ۱)؛ نگله را اولین فرشش می‌سازد. */
  function nextBale() {
    setCurrent(maxNumber.value + 1)
  }

  // ── پیام‌ها ─────────────────────────────────────────────────────────

  function notify(text: string, kind: BoardNotice['kind'] = 'info', action?: BoardNotice['action']) {
    const id = noticeId++
    notices.value = [...notices.value.slice(-2), { id, text, kind, action }]
    globalThis.setTimeout(() => dismissNotice(id), action ? NOTICE_MS : kind === 'error' ? NOTICE_MS : 4000)
    return id
  }

  function dismissNotice(id: number) {
    notices.value = notices.value.filter((n) => n.id !== id)
  }

  function ask(text: string, choices: BoardChoice[]) {
    question.value = { id: questionId++, message: text, choices }
  }

  /** جواب پرسش (دکمه، Enter = اولی، Esc = آخری). */
  async function answer(index: number) {
    const q = question.value
    if (!q) return
    question.value = null
    const choice = q.choices[index < 0 ? q.choices.length - 1 : index]
    await choice?.run()
  }

  /**
   * خطای یک کار: خطای «افزودن» زیر همان جعبهٔ شماره می‌نشیند (`lastError`، با `role="alert"`)، بقیه پیام کوتاه
   * می‌شوند. هیچ‌کدام بی‌صدا گم نمی‌شود.
   */
  function fail(text: string, inline = false) {
    if (inline) {
      lastError.value = text
      return
    }
    notify(text, 'error')
  }

  // ── بار ─────────────────────────────────────────────────────────────

  async function load() {
    loading.value = true
    loadError.value = ''
    try {
      const [list, issues, grid] = await Promise.all([
        api.get<Bale[]>(`/api/shipments/${shipmentId.value}/bales`),
        api.get<ReadinessIssue[]>(`/api/shipments/${shipmentId.value}/readiness`).catch(() => null),
        options.loadLayout
          ? api.get<GridData>(`/api/grid/bales?shipmentId=${shipmentId.value}&take=5`).catch(() => null)
          : Promise.resolve(null),
      ])
      bales.value = list.map((b) => ({ ...b, rows: [...b.rows].sort((x, y) => x.seq - y.seq) }))
      if (issues) readiness.value = issues
      if (grid?.columns) layout.value = grid.columns
      if (!loaded.value) {
        const wanted = options.initialBaleId?.()
        const fromProp = wanted ? bales.value.find((b) => b.id === wanted)?.number ?? null : null
        current.value = resolveCurrent(fromProp ?? readCurrentBale(storageKey.value), bales.value)
        if (fromProp) writeCurrentBale(storageKey.value, fromProp)
      }
      loaded.value = true
    } catch (error) {
      loadError.value = error instanceof ApiError && error.status !== 404 ? error.message : message('E_BOARD_LOAD')
      if (error instanceof ApiError && error.status === 404) loadError.value = coreMessages.E_NOT_FOUND
    } finally {
      loading.value = false
    }
  }

  let readinessTimer: ReturnType<typeof setTimeout> | null = null
  function refreshReadiness(delay = 500) {
    if (readinessTimer) clearTimeout(readinessTimer)
    readinessTimer = setTimeout(async () => {
      try {
        readiness.value = await api.get<ReadinessIssue[]>(`/api/shipments/${shipmentId.value}/readiness`)
      } catch {
        /* تراشه‌ها اختیاری‌اند؛ بار بعد درست می‌شوند */
      }
    }, delay)
  }

  /** خبر به جزیره‌های دیگر (سربرگ: جمع‌ها و آمادگی). */
  function announce() {
    if (typeof window === 'undefined') return
    const detail: ShipmentChangedDetail = {
      shipmentId: shipmentId.value,
      source: options.source,
      bales: bales.value.map((b) => ({
        id: b.id,
        number: b.number,
        version: b.version,
        lines: b.rows.length,
        pieces: b.pieces,
        areaM2: b.areaM2,
        usdTotal: b.usdTotal,
        weightKg: b.weightKg,
      })),
    }
    window.dispatchEvent(new CustomEvent<ShipmentChangedDetail>(SHIPMENT_CHANGED_EVENT, { detail }))
  }

  // ── فرستادن ─────────────────────────────────────────────────────────

  function pushUndo(entry: UndoEntry) {
    undoStack.push(entry)
    if (undoStack.length > UNDO_LIMIT) undoStack.shift()
    undoCount.value = undoStack.length
  }

  function takeUndo(entry: UndoEntry): boolean {
    const index = undoStack.lastIndexOf(entry)
    if (index < 0) return false
    undoStack.splice(index, 1)
    undoCount.value = undoStack.length
    return true
  }

  /** یک درخواست `lines/batch`؛ نتیجه روی نگله‌ها نشانده می‌شود. خطای کل درخواست برمی‌گردد (پرتاب نمی‌شود). */
  async function send(ops: LineOp[]): Promise<SendOutcome> {
    if (locked.value) return { result: null, error: new ApiError(409, lockMessage(), null) }
    busy.value++
    const release = blockReload('bales')
    try {
      const result = await api.post<LinesBatchResult>(`/api/shipments/${shipmentId.value}/lines/batch`, { ops })
      bales.value = applyBatchResult(bales.value, result, ops, options.targetAreaM2())
      if (shiftsOthers(ops) && result.results.some((r) => r.ok)) await reloadQuietly()
      handleOpErrors(result.results)
      if (result.results.some((r) => r.ok)) {
        refreshReadiness()
        announce()
      }
      return { result, error: null }
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        if (isLockError(error)) lockedLocal.value = true
        else await reloadQuietly()
      }
      return { result: null, error }
    } finally {
      release()
      busy.value--
    }
  }

  function isLockError(error: ApiError): boolean {
    return error.body?.code === 'E_SHIPMENT_LOCKED' || /قطعی/.test(error.message)
  }

  /** خطای هر کار: قفل → فقط‌خواندنی؛ تعارض نسخه → بار دوباره. */
  function handleOpErrors(results: LineOpResult[]) {
    if (results.some((r) => r.errors.some((e) => e.code === 'E_SHIPMENT_LOCKED'))) lockedLocal.value = true
    if (results.some((r) => r.errors.some((e) => e.code === 'E_CONFLICT' || e.code === 'E_LINE_NOT_IN_SHIPMENT'))) {
      void reloadQuietly()
    }
  }

  async function reloadQuietly() {
    try {
      const list = await api.get<Bale[]>(`/api/shipments/${shipmentId.value}/bales`)
      bales.value = list.map((b) => ({ ...b, rows: [...b.rows].sort((x, y) => x.seq - y.seq) }))
    } catch {
      /* همان نگله‌های فعلی می‌مانند */
    }
  }

  /** پیام قفل برای این بیننده (C.8): بازرگان «باز کردن دوباره» را می‌بیند، انباردار «به بازرگان بگویید». */
  function lockMessage(): string {
    const number = options.shipmentNumber?.() ?? null
    return ctx.policies.business && number !== null
      ? message('E_SHIPMENT_LOCKED_HEADER', { number })
      : message('E_SHIPMENT_LOCKED_OPERATOR')
  }

  /** پیام یک کار ناموفق، یا خطای کل درخواست. */
  function errorText(result: LineOpResult | null, error: unknown): string {
    if (result && result.errors.length > 0) return result.errors[0].message
    if (error) return errorMessage(error) || coreMessages.E_SERVER
    return coreMessages.E_SERVER
  }

  // ── افزودن ─────────────────────────────────────────────────────────

  interface AddTarget {
    serial?: string | null
    rugId?: string | null
    baleNumber?: number
    baleId?: string | null
    seq?: number | null
    move?: boolean
    clientOpId?: string
    via?: 'serial' | 'rug' | 'undo'
  }

  /**
   * افزودن یک فرش به نگلهٔ جاری (یا نگلهٔ داده‌شده). نتیجه: ردیف تازه، یا null (پرسش آمد، خطا، یا قطع اینترنت).
   */
  async function addRug(target: AddTarget): Promise<BaleRow | null> {
    lastError.value = ''
    const baleNumber = target.baleNumber ?? current.value
    const op: LineOp = {
      op: 'add',
      clientOpId: target.clientOpId ?? newId(),
      ...(target.baleId ? { baleId: target.baleId } : { baleNumber }),
      ...(target.rugId ? { rugId: target.rugId } : { serial: target.serial ?? null }),
      ...(target.seq ? { seq: target.seq } : {}),
      ...(target.move ? { move: true } : {}),
    }

    const { result, error } = await send([op])
    if (!result) {
      if (isNetworkError(error) && target.via !== 'undo') {
        keepPending({
          clientOpId: op.clientOpId as string,
          serial: target.serial ?? null,
          rugId: target.rugId ?? null,
          baleNumber,
          at: Date.now(),
        })
        return null
      }
      fail(errorText(null, error), target.via !== 'undo')
      return null
    }

    const r = result.results[0]
    dropPending(op.clientOpId as string)
    if (r?.ok && r.row) {
      const row = r.row
      if (target.via !== 'undo') {
        pushUndo({ kind: 'add', lineId: row.lineId, serial: row.serial })
        recent.value = [
          { lineId: row.lineId, rugId: row.rugId, serial: row.serial, baleNumber: row.baleNumber, at: Date.now() },
          ...recent.value.filter((x) => x.lineId !== row.lineId),
        ].slice(0, RECENT_LIMIT)
      }
      // افزودن با شمارهٔ تازه همان نگله را ساخت؛ جاری همان می‌ماند
      if (!target.baleId && row.baleNumber !== current.value && target.via !== 'undo') setCurrent(row.baleNumber)
      options.onAdded?.(row, target.via ?? (target.rugId ? 'rug' : 'serial'))
      return row
    }

    if (r?.prompt) {
      handlePrompt(r.prompt, target, baleNumber)
      return null
    }

    fail(errorText(r ?? null, error), target.via !== 'undo')
    return null
  }

  /** پرسش‌های «افزودن» (C.3): منتقل شود؟ / داخل دسته است. */
  function handlePrompt(prompt: LineOpPrompt, target: AddTarget, baleNumber: number) {
    if (prompt.code === 'Q_LINE_SERIAL_IN_RANGE') {
      const choices: BoardChoice[] = [
        {
          label: 'کل دسته را بگذار',
          primary: true,
          icon: 'bale',
          run: () => void addRug({ ...target, serial: null, rugId: prompt.rugId, baleNumber, clientOpId: newId(), via: 'rug' }),
        },
      ]
      if (prompt.quantity > 1 && options.onSplitRequested) {
        choices.push({
          label: 'جدا کن',
          icon: 'split',
          run: () =>
            options.onSplitRequested?.({
              rugId: prompt.rugId,
              serial: prompt.serial,
              quantity: prompt.quantity,
              retrySerial: target.serial ?? null,
            }),
        })
      }
      choices.push({ label: 'نه', run: () => {} })
      ask(prompt.message, choices)
      return
    }

    // Q_LINE_MOVE / Q_LINE_MOVE_SAME: فرش جای دیگری است
    ask(prompt.message, [
      {
        label: 'منتقل کن',
        primary: true,
        icon: 'move',
        run: () => void addRug({ ...target, serial: null, rugId: prompt.rugId, baleNumber, move: true, clientOpId: newId(), via: target.via === 'undo' ? 'undo' : 'rug' }),
      },
      { label: 'نه', run: () => {} },
    ])
  }

  /** افزودن با متن جعبهٔ شماره یا اسکن. نشانی صفحهٔ فرش (برچسب‌های قدیمی) همان فرش را می‌گذارد. */
  async function addBySerial(raw: string): Promise<BaleRow | null> {
    const text = raw.trim()
    if (!text) return null
    const rugId = rugIdFromUrl(text)
    return rugId ? addRug({ rugId, via: 'rug' }) : addRug({ serial: text, via: 'serial' })
  }

  // ── قطع اینترنت ─────────────────────────────────────────────────────

  function keepPending(item: PendingAdd) {
    pending.value = [...pending.value.filter((p) => p.clientOpId !== item.clientOpId), item]
    savePending()
  }

  function dropPending(clientOpId: string) {
    if (!pending.value.some((p) => p.clientOpId === clientOpId)) return
    pending.value = pending.value.filter((p) => p.clientOpId !== clientOpId)
    savePending()
  }

  function savePending() {
    const key = pendingKey.value
    const storage = safeStorage()
    if (!key || !storage) return
    try {
      if (pending.value.length === 0) storage.removeItem(key)
      else storage.setItem(key, JSON.stringify({ v: 1, savedAt: Date.now(), adds: pending.value }))
    } catch {
      /* حالت خصوصی: فقط تا بسته شدن صفحه می‌ماند */
    }
  }

  function restorePending() {
    const key = pendingKey.value
    const storage = safeStorage()
    if (!key || !storage) return
    try {
      const raw = storage.getItem(key)
      const parsed = raw ? (JSON.parse(raw) as { adds?: PendingAdd[] }) : null
      if (Array.isArray(parsed?.adds)) pending.value = parsed.adds.filter((p) => typeof p?.clientOpId === 'string')
    } catch {
      /* پیش‌نویس خراب: نادیده */
    }
  }

  /** «الان دوباره بفرست»: همه با همان `clientOpId`. */
  async function retryPending() {
    const items = [...pending.value]
    for (const item of items) {
      const row = await addRug({
        serial: item.serial,
        rugId: item.rugId,
        baleNumber: item.baleNumber,
        clientOpId: item.clientOpId,
        via: item.rugId ? 'rug' : 'serial',
      })
      if (!row && pending.value.some((p) => p.clientOpId === item.clientOpId)) break // هنوز قطع است
    }
  }

  function discardPending(clientOpId: string) {
    dropPending(clientOpId)
  }

  // ── انتقال، خروج، ترتیب ────────────────────────────────────────────

  function findRow(lineId: string): BaleRow | null {
    return rows.value.find((r) => r.lineId === lineId) ?? null
  }

  function findRowOfRug(rugId: string): BaleRow | null {
    return rows.value.find((r) => r.rugId === rugId) ?? null
  }

  /** انتقال به نگلهٔ دیگر (شمارهٔ تازه همان نگله را می‌سازد)، برگشت به انبار، یا بالا/پایین در همان نگله. */
  async function moveRow(row: BaleRow, target: MoveTarget): Promise<boolean> {
    if (target.kind === 'stock') return removeRow(row)
    lastError.value = ''

    let op: LineOp
    if (target.kind === 'up' || target.kind === 'down') {
      const bale = bales.value.find((b) => b.id === row.baleId)
      const seq = bale ? reorderSeq(bale.rows, row.lineId, target.kind) : null
      if (seq === null) return false
      op = { op: 'move', clientOpId: newId(), lineId: row.lineId, lineVersion: row.lineVersion, baleId: row.baleId, seq }
    } else {
      if (target.number === row.baleNumber) return false
      const bale = bales.value.find((b) => b.number === target.number)
      op = {
        op: 'move',
        clientOpId: newId(),
        lineId: row.lineId,
        lineVersion: row.lineVersion,
        ...(bale ? { baleId: bale.id } : { baleNumber: target.number }),
      }
    }

    const before = { baleId: row.baleId, baleNumber: row.baleNumber, seq: row.seq }
    const { result, error } = await send([op])
    const r = result?.results[0] ?? null
    if (!r?.ok) {
      fail(errorText(r, error))
      return false
    }
    const entry: UndoEntry = { kind: 'move', lineId: row.lineId, ...before, serial: row.serial }
    pushUndo(entry)
    if (target.kind === 'bale') {
      notify(message('I_BOARD_MOVED', { serial: serialText(row.serial), n: target.number }), 'success', {
        label: 'برگردان',
        run: () => void undoEntry(entry),
      })
    }
    return true
  }

  /** انتقال چند ردیف با هم (نوار گروهی) به یک نگله؛ یک درخواست و یک «برگرد». خروجی: تعداد انجام‌شده. */
  async function moveMany(targets: readonly BaleRow[], number: number): Promise<number> {
    const moving = targets.filter((r) => r.baleNumber !== number)
    if (moving.length === 0) return 0
    const bale = bales.value.find((b) => b.number === number)
    const ops: LineOp[] = moving.map((r) => ({
      op: 'move',
      clientOpId: newId(),
      lineId: r.lineId,
      lineVersion: r.lineVersion,
      ...(bale ? { baleId: bale.id } : { baleNumber: number }),
    }))
    const befores = moving.map((r) => ({ lineId: r.lineId, baleId: r.baleId, baleNumber: r.baleNumber, seq: r.seq, serial: r.serial }))
    const { result, error } = await send(ops)
    if (!result) {
      fail(errorText(null, error))
      return 0
    }
    const done = befores.filter((_, i) => result.results[i]?.ok)
    if (done.length > 0) {
      const entry: UndoEntry = { kind: 'group', entries: done.map((b) => ({ kind: 'move', ...b })) }
      pushUndo(entry)
      notify(message('I_BOARD_MOVED', { serial: faDigitsOf(done.length) + ' فرش', n: number }), 'success', {
        label: 'برگردان',
        run: () => void undoEntry(entry),
      })
    }
    const failed = result.results.find((r) => !r.ok)
    if (failed) fail(errorText(failed, null))
    return done.length
  }

  /** «خارج کردن از نگله»: فرش به انبار برمی‌گردد؛ «برگردان» همان نگله و همان Seq. */
  async function removeRow(row: BaleRow): Promise<boolean> {
    lastError.value = ''
    const op: LineOp = { op: 'remove', clientOpId: newId(), lineId: row.lineId, lineVersion: row.lineVersion }
    const { result, error } = await send([op])
    const r = result?.results[0] ?? null
    if (!r?.ok) {
      fail(errorText(r, error))
      return false
    }
    const before = r.row ?? row
    const entry: UndoEntry = {
      kind: 'remove',
      rugId: before.rugId,
      baleId: before.baleId,
      baleNumber: before.baleNumber,
      seq: before.seq,
      serial: before.serial,
    }
    pushUndo(entry)
    recent.value = recent.value.filter((x) => x.lineId !== row.lineId)
    notify(message('I_BOARD_REMOVED', { serial: serialText(before.serial) }), 'info', {
      label: 'برگردان',
      run: () => void undoEntry(entry),
    })
    return true
  }

  // ── اندازهٔ نهایی ───────────────────────────────────────────────────

  /** ثبت اندازهٔ نهایی یک ردیف (C.4). نتیجه: ردیف تازه یا null. */
  async function measure(row: BaleRow, size: FinalSizeInput, options2: { quiet?: boolean } = {}): Promise<BaleRow | null> {
    lastError.value = ''
    const before = finalSizeOf(row)
    const op = measureOp(row, size, newId())
    const { result, error } = await send([op])
    const r = result?.results[0] ?? null
    if (!r?.ok || !r.row) {
      fail(errorText(r, error))
      return null
    }
    const entry: UndoEntry = { kind: 'measure', rugId: row.rugId, before, serial: row.serial }
    pushUndo(entry)
    if (!options2.quiet) {
      notify(message(size.kind === 'clear' ? 'I_BOARD_MEASURE_CLEARED' : 'I_BOARD_MEASURED', { serial: serialText(row.serial) }), 'success', {
        label: 'برگردان',
        run: () => void undoEntry(entry),
      })
    }
    return r.row
  }

  /** «بقیه همان اندازهٔ اول»: یک تأیید و یک درخواست برای همه (C.4). */
  async function sameAsIntakeMany(rugIds: readonly string[], confirmFirst = true): Promise<number> {
    const targets = rugIds.map((id) => findRowOfRug(id)).filter((r): r is BaleRow => r !== null)
    if (targets.length === 0) return 0
    if (confirmFirst) {
      const ok = await confirmDialog({
        title: message('Q_BOARD_SAME_AS_INTAKE', { n: targets.length }),
        confirmLabel: 'ثبت کن',
        cancelLabel: 'نه',
      })
      if (!ok) return 0
    }
    const befores = new Map(targets.map((r) => [r.rugId, finalSizeOf(r)]))
    const ops = targets.map((r) => measureOp(r, { kind: 'same' }, newId()))
    const { result, error } = await send(ops)
    if (!result) {
      fail(errorText(null, error))
      return 0
    }
    const done = result.results
      .map((r, i) => (r.ok ? targets[i] : null))
      .filter((r): r is BaleRow => r !== null)
    if (done.length > 0) {
      const entry: UndoEntry = {
        kind: 'group',
        entries: done.map((r) => ({ kind: 'measure', rugId: r.rugId, before: befores.get(r.rugId) ?? { kind: 'clear' }, serial: r.serial })),
      }
      pushUndo(entry)
      notify(message('I_BOARD_SAME_AS_INTAKE_DONE', { n: done.length }), 'success', {
        label: 'برگردان',
        run: () => void undoEntry(entry),
      })
    }
    const failed = result.results.find((r) => !r.ok)
    if (failed) fail(errorText(failed, null))
    return done.length
  }

  // ── «برگرد» ────────────────────────────────────────────────────────

  async function undoEntry(entry: UndoEntry): Promise<boolean> {
    takeUndo(entry)
    const ops = inverseOps(entry, rows.value, (id) => bales.value.some((b) => b.id === id), newId)
    if (ops.length === 0) {
      notify(message('I_BOARD_UNDO_EMPTY'))
      return false
    }
    const { result, error } = await send(ops)
    if (!result) {
      fail(errorText(null, error))
      return false
    }
    const prompt = result.results.find((r) => r.prompt)?.prompt
    if (prompt && entry.kind === 'remove') {
      handlePrompt(prompt, { rugId: entry.rugId, baleId: null, seq: entry.seq, via: 'undo' }, entry.baleNumber)
      return false
    }
    const failed = result.results.find((r) => !r.ok)
    if (failed) {
      fail(errorText(failed, null))
      return false
    }
    const added = result.results.find((r) => r.op === 'add' && r.row)?.row
    if (added) options.onAdded?.(added, 'undo')
    notify(message('I_BOARD_UNDONE'), 'success')
    return true
  }

  /** «برگرد» (Ctrl+Z، دکمهٔ نوار): آخرین کار. */
  async function undo(): Promise<boolean> {
    const entry = undoStack[undoStack.length - 1]
    if (!entry) {
      notify(message('I_BOARD_UNDO_EMPTY'))
      return false
    }
    return undoEntry(entry)
  }

  // ── خانه‌های توصیفی (رنگ، نقشه، محل بافت، مواد) ─────────────────────

  /**
   * ویرایش یک خانهٔ توصیفی از راه جدول فرش‌ها (`POST /api/grid/rugs/batch`، G.6) با ادغام سه‌طرفه: فقط همین کلید با
   * مقدار پیشینش. محل بافت ارزش گمرکی را عوض می‌کند، پس پس از ذخیره نگله‌ها دوباره بار می‌شوند.
   */
  async function editDescriptive(row: BaleRow, column: BoardColumnKey, value: string | null): Promise<boolean> {
    const field = DESCRIPTIVE_FIELD[column]
    if (!field || locked.value) return false
    const before = descriptiveValue(column, row)
    const next = value && value.trim() ? value.trim() : null
    if ((before ?? '') === (next ?? '')) return true

    const request = (gridId: 'bales' | 'rugs'): GridBatchRequest => ({
      gridId,
      batch: null,
      shipmentId: shipmentId.value,
      ops: [
        {
          clientRowId: newId(),
          rugId: row.rugId,
          version: row.rugVersion,
          values: { [field]: next },
          baseValues: { [field]: before },
          delete: false,
        },
      ],
    })

    busy.value++
    try {
      let response: GridBatchResponse
      try {
        response = await api.post<GridBatchResponse>('/api/grid/rugs/batch', request('bales'))
      } catch (error) {
        // جدول «bales» هنوز ثبت نشده باشد: همان نویسندهٔ فیلد از راه جدول فرش‌ها
        if (error instanceof ApiError && error.status === 404) response = await api.post<GridBatchResponse>('/api/grid/rugs/batch', request('rugs'))
        else throw error
      }
      const result = response.results?.[0]
      if (!result || result.status === 1 /* Invalid */) {
        fail(result?.errors?.[0]?.message ?? coreMessages.E_VALIDATION)
        if (result?.errors?.some((e) => /قطعی/.test(e.message))) lockedLocal.value = true
        return false
      }
      if (result.status === 2 /* Conflict */) {
        notify(message('E_GRID_CONFLICT'), 'warning')
        await reloadQuietly()
        return false
      }
      // ذخیره شد: نسخهٔ تازه و مقدار سرور؛ ارزش گمرکی شاید عوض شده باشد
      const updated = withDescriptive({ ...row, rugVersion: result.version ?? row.rugVersion }, column, next)
      for (const bale of bales.value) {
        const index = bale.rows.findIndex((r) => r.lineId === row.lineId)
        if (index >= 0) bale.rows.splice(index, 1, updated)
      }
      if (column === 'origin') {
        await reloadQuietly()
        refreshReadiness()
        announce()
      }
      return true
    } catch (error) {
      if (error instanceof ApiError && error.status === 409 && /قطعی/.test(error.message)) lockedLocal.value = true
      fail(errorMessage(error) || coreMessages.E_SERVER)
      return false
    } finally {
      busy.value--
    }
  }

  // ── نرخ دستی و ارزش گمرکی (Business) ───────────────────────────────

  /** نرخ دستی یک ردیف؛ null یعنی برگشت به جدول. */
  async function setRate(row: BaleRow, usdPerM2: number | null): Promise<boolean> {
    const op: LineOp = { op: 'rate', clientOpId: newId(), lineId: row.lineId, lineVersion: row.lineVersion, usdPerM2Override: usdPerM2 }
    const { result, error } = await send([op])
    const r = result?.results[0] ?? null
    if (!r?.ok) {
      fail(errorText(r, error))
      return false
    }
    if (usdPerM2 === null) notify(message('I_BOARD_RATE_CLEAR'), 'success')
    return true
  }

  /** «ارزش گمرکی «کاشان» برای هر متر: [___] دلار [ثبت]» — فقط کلید تازه (C.5). */
  async function addDeclaredValue(keyValue: string, usdPerM2: number): Promise<boolean> {
    busy.value++
    try {
      const table = await api.get<DeclaredValues>('/api/declared-values')
      const body: AddDeclaredValueRequest = { keyValue, usdPerM2, stamp: table.stamp }
      const result = await api.post<DeclaredValuesWriteResult>('/api/declared-values', body)
      toast.success(message('I_DECLARED_ADDED', { key: keyValue, n: result.affectedOpenLines }))
      await reloadQuietly()
      refreshReadiness(0)
      announce()
      return true
    } catch (error) {
      fail(errorMessage(error) || coreMessages.E_SERVER)
      return false
    } finally {
      busy.value--
    }
  }

  // ── نگلهٔ خالی ─────────────────────────────────────────────────────

  async function deleteBale(baleId: string): Promise<boolean> {
    const bale = bales.value.find((b) => b.id === baleId)
    if (!bale || locked.value) return false
    busy.value++
    try {
      await api.del(`/api/bales/${bale.id}?version=${bale.version}`)
      bales.value = bales.value.filter((b) => b.id !== bale.id)
      notify(message('I_BOARD_BALE_DELETED', { n: bale.number }), 'success')
      refreshReadiness(0)
      announce()
      return true
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) await reloadQuietly()
      fail(errorMessage(error) || coreMessages.E_SERVER)
      return false
    } finally {
      busy.value--
    }
  }

  // ── «نشانم بده» ─────────────────────────────────────────────────────

  function showOnly(rugIds: readonly string[] | null) {
    onlyRugIds.value = rugIds && rugIds.length > 0 ? new Set(rugIds) : null
  }

  // ── رویدادهای جزیره‌های دیگر ───────────────────────────────────────

  let reloadTimer: ReturnType<typeof setTimeout> | null = null
  function onShipmentChanged(event: Event) {
    const detail = (event as CustomEvent<ShipmentChangedDetail>).detail
    if (!detail || detail.shipmentId !== shipmentId.value || detail.source === options.source) return
    if (detail.status !== undefined && detail.status !== null) lockedLocal.value = detail.status === ShipmentStatus.Finalized
    if (reloadTimer) clearTimeout(reloadTimer)
    reloadTimer = setTimeout(() => {
      void reloadQuietly()
      refreshReadiness(0)
    }, 300)
  }

  /** فرمانی که برای این تخته آمده؛ جزیره خودش اجرا می‌کند (فوکوس، تأیید). */
  const command = shallowRef<BalesCommandDetail | null>(null)
  function onCommand(event: Event) {
    const detail = (event as CustomEvent<BalesCommandDetail>).detail
    if (!detail || detail.shipmentId !== shipmentId.value) return
    command.value = detail
  }

  onMounted(() => {
    restorePending()
    window.addEventListener(SHIPMENT_CHANGED_EVENT, onShipmentChanged)
    window.addEventListener(BALES_COMMAND_EVENT, onCommand)
  })

  onBeforeUnmount(() => {
    window.removeEventListener(SHIPMENT_CHANGED_EVENT, onShipmentChanged)
    window.removeEventListener(BALES_COMMAND_EVENT, onCommand)
    if (readinessTimer) clearTimeout(readinessTimer)
    if (reloadTimer) clearTimeout(reloadTimer)
  })

  // جاری نباید از «بزرگ‌ترین + ۱» جلوتر برود (مثلاً پس از حذف نگلهٔ خالی)
  watch(maxNumber, (max) => {
    if (loaded.value && current.value > max + 1 && !bales.value.some((b) => b.number === current.value)) setCurrent(Math.max(1, max + 1))
  })

  return {
    // وضعیت
    bales,
    rows,
    loaded,
    loading,
    loadError,
    locked,
    busy,
    readiness,
    layout,
    current,
    currentBale,
    maxNumber,
    totals,
    question,
    notices,
    pending,
    recent,
    lastError,
    onlyRugIds,
    undoCount,
    command,
    // کارها
    load,
    setCurrent,
    nextBale,
    addRug,
    addBySerial,
    retryPending,
    discardPending,
    moveRow,
    moveMany,
    removeRow,
    measure,
    sameAsIntakeMany,
    undo,
    editDescriptive,
    setRate,
    addDeclaredValue,
    deleteBale,
    showOnly,
    findRow,
    findRowOfRug,
    answer,
    ask,
    notify,
    dismissNotice,
    refreshReadiness,
    reload: reloadQuietly,
  }
}

export type BalingBoard = ReturnType<typeof useBalingBoard>

/** شناسهٔ فرش از نشانی صفحهٔ فرش همین سایت (برچسب‌های QR قدیمی): «…/Rugs/Details/{guid}». */
export function rugIdFromUrl(text: string, origin: string | null = typeof window === 'undefined' ? null : window.location.origin): string | null {
  if (!/^https?:\/\//i.test(text)) return null
  try {
    const url = new URL(text)
    if (origin && url.origin !== origin) return null
    const match = /\/Rugs\/Details\/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/i.exec(url.pathname)
    return match ? match[1] : null
  } catch {
    return null
  }
}
