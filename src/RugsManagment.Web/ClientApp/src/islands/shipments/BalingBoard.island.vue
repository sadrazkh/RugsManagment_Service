<script setup lang="ts">
/**
 * جزیرهٔ `baling-board` (I.5): زبانهٔ «نگله‌ها»ی کارگاه محموله (H.1).
 *
 * - سر: «نگلهٔ جاری: ۳ ▾ [نگلهٔ بعد]» با جمع و نوار هدف، پیوند «حالت اسکن (گوشی)» و QR همان نشانی (G.5).
 * - «شماره را بزنید یا اسکن کنید»: به نگلهٔ جاری می‌رود و فوکوس به «طول» نهایی همان ردیف می‌رود؛ Tab به «عرض» و Enter
 *   به جعبهٔ شماره (C.4). پس نگله‌بندی و اندازه‌گیری با صفحه‌کلید: شماره → طول → عرض، بی Tab اضافه؛ ستون‌های توصیفی در
 *   حالت ساده ایستگاه Tab نیستند (با لمس، دوبار کلیک یا F2 ویرایش می‌شوند، G.2).
 * - دو نما: جدول مثل اکسل با پانویس هر نگله («نگلهٔ ۱ · شامل ۴ تخته · ۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار») و نوار هدف، یا کارت‌ها
 *   (تبلت) با «انتقال» (کلید M) و تراشهٔ «اندازه». زیر ۱۱۰۰ پیکسل جدول با پیش‌نمایش «اندازه‌گیری» باز می‌شود (H.6).
 * - انتقال بی کشیدن (انتخاب‌گر نگله)، «خارج کردن از نگله» با «برگردان» در همان جا، «همان اندازه» (کلید =، دکمهٔ
 *   «همان»، نوار گروهی)، «بقیه همان اندازهٔ اول» روی تراشهٔ آمادگی، جدا کردن دسته با پیش‌نمایش، افزودن از انبار.
 * - محمولهٔ قطعی فقط‌خواندنی است، با پیام قفل همان بیننده؛ تعارض نسخه نگله‌ها را تازه می‌کند و پیام فارسی می‌دهد.
 *
 * همهٔ رفتار در `useBalingBoard` است (مشترک با حالت اسکن)؛ این‌جا چیدمان و صفحه‌کلید جدول.
 */
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, useId, watch } from 'vue'
import AppIcon from '@/components/AppIcon.vue'
import QrImage from '@/components/QrImage.vue'
import BaleCard from '@/components/bales/BaleCard.vue'
import BaleFooterRow from '@/components/bales/BaleFooterRow.vue'
import BaleMovePicker from '@/components/bales/BaleMovePicker.vue'
import CurrentBaleHeader from '@/components/bales/CurrentBaleHeader.vue'
import FinalSizeSheet from '@/components/bales/FinalSizeSheet.vue'
import LotSplitDialog from '@/components/bales/LotSplitDialog.vue'
import StockPicker from '@/components/bales/StockPicker.vue'
import {
  boardColumns,
  cellText,
  descriptiveValue,
  faDigitsOf,
  faFixed,
  faLoose,
  parseSizeText,
  rateSourceText,
  reorderSeq,
  serialText,
  sizeChangeBadge,
  type BoardColumn,
  type SizeChoice,
} from '@/components/bales/baleModel'
import { useBalingBoard, type SplitRequest } from '@/components/bales/useBalingBoard'
import BarcodeScan from '@/components/scan/BarcodeScan.vue'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import NumberField from '@/components/ui/NumberField.vue'
import { api } from '@/lib/api'
import { appContext, isSimpleUi } from '@/lib/context'
import { message } from '@/lib/messages'
import { parseDecimal } from '@/lib/money'
import { toNumber } from '@/lib/num'
import type { BoardView, FinalSizeInput, MoveTarget } from '@/lib/types/bales'
import type { RugListItem, SplitRugResult } from '@/lib/types/rugs'
import { DeclaredRateSource, ReadinessCode, type BaleRow, type ReadinessIssue } from '@/lib/types/shipments'

const props = withDefaults(
  defineProps<{
    shipmentId: string
    view?: BoardView
    targetAreaM2?: number | null
    tolerancePercent?: number
    locked?: boolean
    shipmentNumber?: number | null
    packUrl?: string | null
  }>(),
  { view: 'table', targetAreaM2: null, tolerancePercent: 10, locked: false, shipmentNumber: null, packUrl: null },
)

const ctx = appContext()
const terms = ctx.terms
const simple = computed(() => isSimpleUi(ctx))
const canBusiness = computed(() => ctx.policies.business)
const uid = useId()
const SOURCE = 'baling-board'

// ── مغز ─────────────────────────────────────────────────────────────

/** افزودن از جعبهٔ شماره فوکوس را به «طول» همان ردیف می‌برد؛ اسکن نه (پنجرهٔ دوربین باز است). */
let focusAfterAdd = false
/** ردیفی که همین حالا با شماره آمد: Enter در «عرض» به جعبهٔ شماره برمی‌گردد (C.4). */
const addFlowLine = ref<string | null>(null)

const board = useBalingBoard({
  shipmentId: () => props.shipmentId,
  locked: () => props.locked,
  targetAreaM2: () => props.targetAreaM2,
  shipmentNumber: () => props.shipmentNumber,
  source: SOURCE,
  loadLayout: true,
  onAdded: (row, via) => {
    if (via === 'undo') return
    if (board.onlyRugIds.value && !board.onlyRugIds.value.has(row.rugId)) board.showOnly(null)
    if (!focusAfterAdd) return
    focusAfterAdd = false
    addFlowLine.value = row.lineId
    void nextTick(() => {
      if (view.value === 'table') focusSize(row.lineId, 'L')
      else document.querySelector<HTMLElement>(`[data-line-id="${row.lineId}"]`)?.focus()
    })
  },
  onSplitRequested: (request) => openSplit(request),
})

const locked = computed(() => board.locked.value)
const target = computed(() => props.targetAreaM2)
const tolerance = computed(() => props.tolerancePercent ?? 10)

const lockText = computed(() =>
  canBusiness.value && props.shipmentNumber !== null
    ? message('E_SHIPMENT_LOCKED_HEADER', { number: props.shipmentNumber })
    : message('E_SHIPMENT_LOCKED_OPERATOR'),
)

// ── نما و ستون‌ها ──────────────────────────────────────────────────────

/** نما از `?view=` می‌آید (پیوندهای «جدول / کارت‌ها» بالای زبانه، که بی‌اسکریپت هم کار می‌کنند و نشانک‌پذیرند). */
const view = computed<BoardView>(() => (props.view === 'cards' ? 'cards' : 'table'))

/** پیش‌نمایش «اندازه‌گیری»: پیش‌فرض زیر ۱۱۰۰ پیکسل (G.2، H.6). */
const measuring = ref(false)
let wideQuery: MediaQueryList | null = null

const columns = computed<BoardColumn[]>(() =>
  boardColumns(board.layout.value, { terms, simple: simple.value, canRate: canBusiness.value, measuring: measuring.value }),
)

const visibleBales = computed(() => {
  const only = board.onlyRugIds.value
  return board.bales.value
    .map((bale) => ({ bale, rows: only ? bale.rows.filter((r) => only.has(r.rugId)) : bale.rows }))
    .filter((g) => !only || g.rows.length > 0)
})

const visibleRows = computed(() => visibleBales.value.flatMap((g) => g.rows))
const shownCount = computed(() => visibleRows.value.length)

// ── جعبهٔ شماره و اسکن ─────────────────────────────────────────────────

const serialInput = ref<HTMLInputElement | null>(null)
const serial = ref('')
const adding = ref(false)

async function submitSerial() {
  const text = serial.value.trim()
  if (!text || adding.value) return
  adding.value = true
  focusAfterAdd = true
  try {
    const row = await board.addBySerial(text)
    if (row || board.question.value) serial.value = ''
    if (!row && !board.question.value) {
      focusAfterAdd = false
      void nextTick(() => serialInput.value?.select())
    }
  } finally {
    adding.value = false
  }
}

function onScanned(code: string) {
  focusAfterAdd = false
  void board.addBySerial(code)
}

function focusSerial() {
  void nextTick(() => serialInput.value?.focus())
}

// ── پرسش (Enter اولی، Esc آخری) ────────────────────────────────────────

const questionBox = ref<HTMLElement | null>(null)
watch(
  () => board.question.value?.id,
  (id) => {
    // اگر پرسش از جعبهٔ شماره آمد، فوکوس به دکمهٔ اول؛ پرسش خانهٔ اندازه فوکوس را در همان خانه نگه می‌دارد
    if (id && !activeSize.value) void nextTick(() => questionBox.value?.querySelector<HTMLElement>('button')?.focus())
  },
)

async function answer(index: number) {
  const fromSerial = !activeSize.value
  await board.answer(index)
  if (fromSerial && !board.question.value && !addFlowLine.value) focusSerial()
}

function onQuestionKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape') {
    event.preventDefault()
    void answer(-1)
  }
}

// ── خانه‌های اندازه (جدول) ─────────────────────────────────────────────

type Side = 'L' | 'W'
const sizeTexts = reactive<Record<string, string>>({})
const sizeErrors = reactive<Record<string, string>>({})
/** مقدار خوانده‌شدهٔ هر خانه (متر، لاتین)؛ null یعنی خالی */
const sizeValues = reactive<Record<string, string | null>>({})
const activeSize = ref<{ lineId: string; side: Side } | null>(null)
const savingRows = reactive<Record<string, boolean>>({})

function cellKey(lineId: string, side: Side) {
  return `${lineId}:${side}`
}

function finalOf(row: BaleRow, side: Side): number | null {
  return side === 'L' ? row.finalLength : row.finalWidth
}

function sizeDisplay(row: BaleRow, side: Side): string {
  const key = cellKey(row.lineId, side)
  if (key in sizeTexts) return sizeTexts[key]
  const v = finalOf(row, side)
  return v === null ? '' : faFixed(v)
}

function sizeInputId(lineId: string, side: Side) {
  return `${uid}-size-${lineId}-${side}`
}

function focusSize(lineId: string, side: Side) {
  const el = document.getElementById(sizeInputId(lineId, side)) as HTMLInputElement | null
  el?.focus()
  el?.select()
}

function onSizeFocus(row: BaleRow, side: Side, event: FocusEvent) {
  activeSize.value = { lineId: row.lineId, side }
  ;(event.target as HTMLInputElement).select()
}

function onSizeInput(row: BaleRow, side: Side, event: Event) {
  const key = cellKey(row.lineId, side)
  sizeTexts[key] = (event.target as HTMLInputElement).value
  delete sizeErrors[key]
  delete sizeValues[key]
}

function clearRowEdits(lineId: string) {
  for (const side of ['L', 'W'] as Side[]) {
    const key = cellKey(lineId, side)
    delete sizeTexts[key]
    delete sizeErrors[key]
    delete sizeValues[key]
  }
}

type CellOutcome = 'clean' | 'ok' | 'ask' | 'error'

/**
 * متن یک خانه را می‌خواند: درست → مقدار؛ مبهم → پرسش (دکمه‌ها `then` را پس از جواب ادامه می‌دهند)؛ نادرست → خطای خانه.
 */
function readCell(row: BaleRow, side: Side, then: () => void): CellOutcome {
  const key = cellKey(row.lineId, side)
  if (!(key in sizeTexts)) return 'clean'
  if (key in sizeValues) return 'ok'
  const parsed = parseSizeText(sizeTexts[key], side === 'L')
  switch (parsed.status) {
    case 'empty':
      sizeValues[key] = null
      return 'ok'
    case 'ok':
      sizeValues[key] = parsed.value
      sizeTexts[key] = faFixed(parsed.value)
      return 'ok'
    case 'pair':
      setPair(row, parsed.values)
      return 'ok'
    case 'error':
      sizeErrors[key] = parsed.message
      return 'error'
    case 'ask':
      board.ask(
        parsed.message,
        parsed.choices.map((choice: SizeChoice, i) => ({
          label: choice.label,
          primary: i === 0,
          run: () => {
            if (choice.values) setPair(row, choice.values)
            else if (choice.value !== null) {
              sizeValues[key] = choice.value
              sizeTexts[key] = faFixed(choice.value)
            } else {
              sizeErrors[key] = message('E_DIMENSION')
              return
            }
            then()
          },
        })),
      )
      return 'ask'
  }
}

function setPair(row: BaleRow, values: [string, string]) {
  sizeValues[cellKey(row.lineId, 'L')] = values[0]
  sizeTexts[cellKey(row.lineId, 'L')] = faFixed(values[0])
  sizeValues[cellKey(row.lineId, 'W')] = values[1]
  sizeTexts[cellKey(row.lineId, 'W')] = faFixed(values[1])
  delete sizeErrors[cellKey(row.lineId, 'L')]
  delete sizeErrors[cellKey(row.lineId, 'W')]
}

/** طول و عرض هر دو خوانده شدند و با نهایی فعلی فرق دارند → ثبت (یک کار `measure`). */
async function saveRowIfReady(row: BaleRow): Promise<'saved' | 'nothing' | 'incomplete' | 'failed'> {
  const lKey = cellKey(row.lineId, 'L')
  const wKey = cellKey(row.lineId, 'W')
  if (!(lKey in sizeTexts) && !(wKey in sizeTexts)) return 'nothing'
  const length = lKey in sizeValues ? sizeValues[lKey] : row.finalLength === null ? null : String(row.finalLength)
  const width = wKey in sizeValues ? sizeValues[wKey] : row.finalWidth === null ? null : String(row.finalWidth)
  if (length === null && width === null) {
    // هر دو پاک شد: برگشت به اندازهٔ اول
    if (row.finalLength === null) {
      clearRowEdits(row.lineId)
      return 'nothing'
    }
    return (await saveSize(row, { kind: 'clear' })) ? 'saved' : 'failed'
  }
  if (length === null || width === null) return 'incomplete'
  if (row.finalLength !== null && Number(length) === row.finalLength && row.finalWidth !== null && Number(width) === row.finalWidth) {
    clearRowEdits(row.lineId)
    return 'nothing'
  }
  return (await saveSize(row, { kind: 'pair', length: toNumber(length), width: toNumber(width) })) ? 'saved' : 'failed'
}

async function saveSize(row: BaleRow, size: FinalSizeInput, quiet = true): Promise<boolean> {
  savingRows[row.lineId] = true
  try {
    const saved = await board.measure(row, size, { quiet })
    if (saved) clearRowEdits(row.lineId)
    return saved !== null
  } finally {
    delete savingRows[row.lineId]
  }
}

/** ردیف بعد/قبلِ دیده‌شده. */
function neighbour(lineId: string, step: 1 | -1): BaleRow | null {
  const list = visibleRows.value
  const index = list.findIndex((r) => r.lineId === lineId)
  return index < 0 ? null : list[index + step] ?? null
}

/** پس از ثبتِ «عرض» با Enter: ردیفِ تازه از جعبهٔ شماره → جعبهٔ شماره؛ وگرنه «طول» ردیف بعد (مثل اکسل). */
function afterRowDone(row: BaleRow) {
  if (addFlowLine.value === row.lineId || simpleFlowToSerial()) {
    addFlowLine.value = null
    focusSerial()
    return
  }
  const next = neighbour(row.lineId, 1)
  if (next) void nextTick(() => focusSize(next.lineId, 'L'))
  else focusSerial()
}

/** فقط یک ردیف (جدول تازه): Enter همیشه به جعبهٔ شماره. */
function simpleFlowToSerial() {
  return visibleRows.value.length <= 1
}

async function onSizeKeydown(row: BaleRow, side: Side, event: KeyboardEvent) {
  // پرسش باز: Enter پاسخ اول، Esc پاسخ دوم (همان جدول ورود)
  if (board.question.value && (event.key === 'Enter' || event.key === 'Escape')) {
    event.preventDefault()
    await board.answer(event.key === 'Enter' ? 0 : -1)
    return
  }
  if ((event.ctrlKey || event.metaKey) && (event.key === 'z' || event.key === 'Z')) {
    event.preventDefault()
    await board.undo()
    return
  }
  if (event.key === '=' && !event.ctrlKey && !event.altKey && !event.metaKey) {
    // «همان اندازه» (C.4)
    event.preventDefault()
    clearRowEdits(row.lineId)
    if (await saveSize(row, { kind: 'same' })) afterRowDone(board.findRow(row.lineId) ?? row)
    return
  }
  if (event.key === 'Escape') {
    event.preventDefault()
    clearRowEdits(row.lineId)
    addFlowLine.value = null
    focusSerial()
    return
  }
  if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault()
    const other = neighbour(row.lineId, event.key === 'ArrowDown' ? 1 : -1)
    if (other) focusSize(other.lineId, side)
    return
  }
  if (event.key !== 'Enter') return
  event.preventDefault()

  if (side === 'L') {
    const outcome = readCell(row, 'L', () => {
      // جفت «208×210» یا جواب پرسش: اگر هر دو هست ثبت، وگرنه عرض
      void continueAfterLength(row)
    })
    if (outcome === 'ok' || outcome === 'clean') void continueAfterLength(row)
    return
  }

  const outcome = readCell(row, 'W', () => void finishRow(row))
  if (outcome === 'ok' || outcome === 'clean') void finishRow(row)
}

async function continueAfterLength(row: BaleRow) {
  const wKey = cellKey(row.lineId, 'W')
  if (wKey in sizeValues) {
    // «208×210» هر دو را پر کرد
    await finishRow(row)
    return
  }
  focusSize(row.lineId, 'W')
}

async function finishRow(row: BaleRow) {
  const lOutcome = readCell(row, 'L', () => void finishRow(row))
  if (lOutcome === 'ask' || lOutcome === 'error') return
  const result = await saveRowIfReady(row)
  if (result === 'incomplete') {
    const lKey = cellKey(row.lineId, 'L')
    const missingLength = !(lKey in sizeValues) && row.finalLength === null
    sizeErrors[cellKey(row.lineId, missingLength ? 'L' : 'W')] = message('E_SIZE_PAIR')
    focusSize(row.lineId, missingLength ? 'L' : 'W')
    return
  }
  if (result === 'failed') return
  afterRowDone(board.findRow(row.lineId) ?? row)
}

/** خروج از خانه: خواندن؛ خروج از ردیف با هر دو مقدار → ثبت. */
function onSizeBlur(row: BaleRow, side: Side, event: FocusEvent) {
  const next = event.relatedTarget as HTMLElement | null
  const stayingInRow = next?.dataset?.sizeLine === row.lineId
  const inTouchBar = !!next?.closest?.('[data-touch-bar]')
  const inQuestion = !!next?.closest?.('[data-board-question]')
  if (!inTouchBar) activeSize.value = null
  if (inQuestion) return
  const outcome = readCell(row, side, () => {
    if (!stayingInRow) void saveRowIfReady(row)
  })
  if ((outcome === 'ok' || outcome === 'clean') && !stayingInRow && !inTouchBar) void saveRowIfReady(row)
}

// ── نوار لمسی (H.6 ب) ─────────────────────────────────────────────────

const sizeOrder = computed(() => visibleRows.value.flatMap((r) => [cellKey(r.lineId, 'L'), cellKey(r.lineId, 'W')]))

function touchMove(step: 1 | -1) {
  const active = activeSize.value
  if (!active) return
  const keys = sizeOrder.value
  const at = keys.indexOf(cellKey(active.lineId, active.side))
  const next = keys[at + step]
  if (!next) return
  const [lineId, side] = next.split(':') as [string, Side]
  focusSize(lineId, side)
}

async function touchSame() {
  const active = activeSize.value
  const row = active ? board.findRow(active.lineId) : null
  if (!row) return
  clearRowEdits(row.lineId)
  if (await saveSize(row, { kind: 'same' })) afterRowDone(board.findRow(row.lineId) ?? row)
}

function touchClose() {
  ;(document.activeElement as HTMLElement | null)?.blur()
  activeSize.value = null
}

// ── خانه‌های توصیفی (حالت ساده: لمس، دوبار کلیک، F2) ────────────────────

const editing = ref<{ lineId: string; key: BoardColumn['key'] } | null>(null)
const editText = ref('')
const suggestions = ref<string[]>([])

const SUGGEST_FIELD: Partial<Record<BoardColumn['key'], string>> = {
  fieldColor: 'rug.custom.bg_color',
  borderColor: 'rug.custom.border_color',
  pattern: 'rug.pattern',
  origin: 'rug.origin',
  material: 'rug.material',
}

async function startEdit(row: BaleRow, column: BoardColumn) {
  if (locked.value || column.edit !== 'text') return
  editing.value = { lineId: row.lineId, key: column.key }
  editText.value = descriptiveValue(column.key, row) ?? ''
  void nextTick(() => {
    const el = document.getElementById(`${uid}-edit`) as HTMLInputElement | null
    el?.focus()
    el?.select()
  })
  suggestions.value = []
  const field = SUGGEST_FIELD[column.key]
  if (field) {
    try {
      suggestions.value = await api.get<string[]>(`/api/rugs/values?field=${encodeURIComponent(field)}&q=`)
    } catch {
      /* پیشنهاد اختیاری است */
    }
  }
}

async function commitEdit(row: BaleRow) {
  const current = editing.value
  if (!current || current.lineId !== row.lineId) return
  editing.value = null
  await board.editDescriptive(row, current.key, editText.value)
}

function onEditKeydown(row: BaleRow, event: KeyboardEvent) {
  if (event.key === 'Enter') {
    event.preventDefault()
    void commitEdit(row)
  } else if (event.key === 'Escape') {
    event.preventDefault()
    editing.value = null
  }
}

function onTextCellKeydown(row: BaleRow, column: BoardColumn, event: KeyboardEvent) {
  if (event.key === 'F2' || event.key === 'Enter') {
    event.preventDefault()
    void startEdit(row, column)
  }
}

// ── نرخ دستی (Business) و «ندارد» ────────────────────────────────────

const rateEditing = ref<string | null>(null)
const rateText = ref('')

function startRate(row: BaleRow) {
  if (locked.value || !canBusiness.value) return
  rateEditing.value = row.lineId
  rateText.value = row.usdPerM2Override === null ? '' : faLoose(row.usdPerM2Override)
  void nextTick(() => (document.getElementById(`${uid}-rate`) as HTMLInputElement | null)?.select())
}

async function commitRate(row: BaleRow) {
  if (rateEditing.value !== row.lineId) return
  rateEditing.value = null
  const parsed = parseDecimal(rateText.value, 'number', { scale: 2 })
  if (parsed.status === 'empty') {
    if (row.usdPerM2Override !== null) await board.setRate(row, null)
    return
  }
  if (parsed.status !== 'ok') {
    board.notify(message('E_DECLARED_NEGATIVE'), 'error')
    return
  }
  const value = toNumber(parsed.value)
  if (value === row.usdPerM2Override) return
  await board.setRate(row, value)
}

function onRateKeydown(row: BaleRow, event: KeyboardEvent) {
  if (event.key === 'Enter') {
    event.preventDefault()
    void commitRate(row)
  } else if (event.key === 'Escape') {
    event.preventDefault()
    rateEditing.value = null
  }
}

/** فرم «ارزش گمرکی «کاشان» برای هر متر: [___] دلار [ثبت]» (C.5؛ فقط Business). */
const rateForm = ref<{ key: string } | null>(null)
const rateFormValue = ref<number | null>(null)
const rateFormSaving = ref(false)

function openRateForm(key: string | null) {
  if (!key || !canBusiness.value || locked.value) return
  rateForm.value = { key }
  rateFormValue.value = null
  void nextTick(() => document.getElementById(`${uid}-rate-form`)?.focus())
}

async function submitRateForm() {
  const form = rateForm.value
  if (!form || rateFormValue.value === null) return
  rateFormSaving.value = true
  try {
    if (await board.addDeclaredValue(form.key, rateFormValue.value)) rateForm.value = null
  } finally {
    rateFormSaving.value = false
  }
}

// ── انتخاب و نوار گروهی ──────────────────────────────────────────────

const selected = ref<Set<string>>(new Set())
const selectedRows = computed(() => board.rows.value.filter((r) => selected.value.has(r.lineId)))

function toggleSelect(row: BaleRow) {
  const next = new Set(selected.value)
  if (next.has(row.lineId)) next.delete(row.lineId)
  else next.add(row.lineId)
  selected.value = next
}

function clearSelection() {
  selected.value = new Set()
}

async function bulkSame() {
  const n = await board.sameAsIntakeMany(selectedRows.value.map((r) => r.rugId))
  if (n > 0) clearSelection()
}

// ── انتقال، منوی ردیف، اندازه، جدا کردن ───────────────────────────────

const moveOpen = ref(false)
const moveRows = ref<BaleRow[]>([])

function openMove(row: BaleRow | BaleRow[]) {
  if (locked.value) return
  moveRows.value = Array.isArray(row) ? row : [row]
  moveOpen.value = moveRows.value.length > 0
  closeMenu()
}

const moveTitle = computed(() =>
  moveRows.value.length === 1
    ? message('I_BOARD_MOVE_TITLE', { serial: serialText(moveRows.value[0].serial) })
    : message('I_BOARD_ADD_MANY_TITLE', { n: moveRows.value.length }),
)
const moveDescription = computed(() =>
  moveRows.value.length === 1 ? message('I_BOARD_MOVE_NOW', { n: moveRows.value[0].baleNumber }) : undefined,
)
const moveFrom = computed(() => (moveRows.value.length === 1 ? moveRows.value[0].baleNumber : null))
const moveCanUp = computed(() => {
  const row = moveRows.value.length === 1 ? moveRows.value[0] : null
  const bale = row ? board.bales.value.find((b) => b.id === row.baleId) : null
  return !!(row && bale && reorderSeq(bale.rows, row.lineId, 'up') !== null)
})
const moveCanDown = computed(() => {
  const row = moveRows.value.length === 1 ? moveRows.value[0] : null
  const bale = row ? board.bales.value.find((b) => b.id === row.baleId) : null
  return !!(row && bale && reorderSeq(bale.rows, row.lineId, 'down') !== null)
})

async function onMovePick(targetPick: MoveTarget) {
  const list = moveRows.value
  if (list.length === 1) {
    const row = board.findRow(list[0].lineId) ?? list[0]
    await board.moveRow(row, targetPick)
    void nextTick(() => document.querySelector<HTMLElement>(`[data-line-id="${row.lineId}"]`)?.focus())
  } else if (targetPick.kind === 'bale') {
    const n = await board.moveMany(list, targetPick.number)
    if (n > 0) clearSelection()
  } else if (targetPick.kind === 'stock') {
    for (const row of list) await board.removeRow(board.findRow(row.lineId) ?? row)
    clearSelection()
  }
}

// منوی «⋮» ردیف جدول
const menuLine = ref<string | null>(null)
const menuStyle = ref<Record<string, string>>({})
const menuRow = computed(() => (menuLine.value ? board.findRow(menuLine.value) : null))

function openMenu(row: BaleRow, event: MouseEvent) {
  const button = event.currentTarget as HTMLElement
  const rect = button.getBoundingClientRect()
  menuStyle.value = { top: `${Math.round(rect.bottom + 4)}px`, left: `${Math.round(rect.left)}px` }
  menuLine.value = menuLine.value === row.lineId ? null : row.lineId
  if (menuLine.value) void nextTick(() => document.getElementById(`${uid}-menu`)?.querySelector<HTMLElement>('[role="menuitem"]')?.focus())
}

function closeMenu() {
  menuLine.value = null
}

function onMenuKeydown(event: KeyboardEvent) {
  const items = [...(document.getElementById(`${uid}-menu`)?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? [])]
  const at = items.indexOf(document.activeElement as HTMLElement)
  if (event.key === 'Escape') {
    event.preventDefault()
    closeMenu()
  } else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
    event.preventDefault()
    items[(at + (event.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length]?.focus()
  }
}

function onDocumentDown(event: MouseEvent) {
  if (!menuLine.value) return
  const target = event.target as HTMLElement
  if (document.getElementById(`${uid}-menu`)?.contains(target) || target.closest?.('[data-row-menu]')) return
  closeMenu()
}

async function menuRemove(row: BaleRow) {
  closeMenu()
  await board.removeRow(row)
}

// برگهٔ «اندازه»
const sheetOpen = ref(false)
const sheetLine = ref<string | null>(null)
const sheetRow = computed(() => (sheetLine.value ? board.findRow(sheetLine.value) : null))
const sheetIndex = computed(() => visibleRows.value.findIndex((r) => r.lineId === sheetLine.value))

function openSheet(row: BaleRow) {
  sheetLine.value = row.lineId
  sheetOpen.value = true
}

async function sheetSave(size: FinalSizeInput): Promise<boolean> {
  const row = sheetRow.value
  if (!row) return false
  return (await board.measure(row, size, { quiet: true })) !== null
}

function sheetNavigate(step: -1 | 1) {
  const next = visibleRows.value[sheetIndex.value + step]
  if (next) sheetLine.value = next.lineId
}

async function sameFor(row: BaleRow) {
  await saveSize(row, { kind: 'same' }, false)
}

// جدا کردن دسته
const splitOpen = ref(false)
const split = ref<SplitRequest | null>(null)

function openSplit(request: SplitRequest) {
  split.value = request
  splitOpen.value = true
  closeMenu()
}

function splitRow(row: BaleRow) {
  openSplit({ rugId: row.rugId, serial: row.serial, quantity: row.qty, retrySerial: null })
}

async function onSplitDone(result: SplitRugResult) {
  board.notify(message('I_SPLIT_DONE', { taken: result.created.quantity }), 'success')
  await board.reload()
  board.refreshReadiness(0)
  // شماره‌ای که داخل دسته بود: حالا شاید یک فرش تکی است؛ دوباره افزوده می‌شود
  const retry = split.value?.retrySerial
  split.value = null
  if (retry) {
    focusAfterAdd = true
    await board.addBySerial(retry)
  }
}

// افزودن از انبار
const stockOpen = ref(false)
async function addFromStock(rug: RugListItem): Promise<boolean> {
  focusAfterAdd = false
  return (await board.addRug({ rugId: rug.id, via: 'rug' })) !== null
}

// ── تراشه‌های آمادگی (کارهای همین صفحه) ───────────────────────────────

const BOARD_CODES = new Set<string>([
  ReadinessCode.NoFinalSize,
  ReadinessCode.EmptyBale,
  ReadinessCode.NoDeclaredValue,
  'E_NO_DECLARED_KEY',
  ReadinessCode.BaleOverTarget,
  ReadinessCode.NoSerial,
])

const chips = computed(() => (locked.value ? [] : board.readiness.value.filter((i) => BOARD_CODES.has(i.code))))

function showIssue(issue: ReadinessIssue) {
  if (issue.rugIds.length > 0) board.showOnly(issue.rugIds)
  else if (issue.baleId) {
    const bale = board.bales.value.find((b) => b.id === issue.baleId)
    if (bale) board.showOnly(bale.rows.map((r) => r.rugId))
  }
}

// ── «برگرد» سراسری و فرمان‌های سربرگ ─────────────────────────────────

function onRootKeydown(event: KeyboardEvent) {
  if (!(event.ctrlKey || event.metaKey) || (event.key !== 'z' && event.key !== 'Z')) return
  const target = event.target as HTMLElement
  if (target.matches('input, textarea, select') && !target.dataset.sizeLine) return
  if (target.dataset.sizeLine) return // خانهٔ اندازه خودش می‌گیرد
  event.preventDefault()
  void board.undo()
}

watch(
  () => board.command.value,
  async (detail) => {
    if (!detail) return
    board.command.value = null
    switch (detail.command) {
      case 'show':
        board.showOnly(detail.rugIds)
        break
      case 'same-as-intake':
        await board.sameAsIntakeMany(detail.rugIds)
        break
      case 'focus-serial':
        focusSerial()
        break
      case 'delete-bale':
        await board.deleteBale(detail.baleId)
        break
    }
  },
)

// ── QR حالت اسکن ─────────────────────────────────────────────────────

const packHref = computed(() => props.packUrl || `/Shipments/${props.shipmentId}/Pack`)
const packAbsolute = computed(() => (typeof window === 'undefined' ? packHref.value : new URL(packHref.value, window.location.origin).toString()))
const qrOpen = ref(false)

// ── چرخهٔ عمر ────────────────────────────────────────────────────────

function onWide() {
  measuring.value = !!wideQuery?.matches
}

onMounted(async () => {
  wideQuery = window.matchMedia('(max-width: 1099px)')
  onWide()
  wideQuery.addEventListener('change', onWide)
  document.addEventListener('mousedown', onDocumentDown)
  await board.load()
  if (!locked.value) focusSerial()
})

onBeforeUnmount(() => {
  wideQuery?.removeEventListener('change', onWide)
  document.removeEventListener('mousedown', onDocumentDown)
})

// ── کمک‌های قالب ─────────────────────────────────────────────────────

function rowLabel(row: BaleRow) {
  return `${terms.baleOf} ${faDigitsOf(row.baleNumber)}، ${serialText(row.serial)}`
}

function isCurrentBale(number: number) {
  return number === board.current.value
}

const colCount = computed(() => columns.value.length + 2)
const keysHint = computed(() => message('I_BOARD_KEYS_HINT', { enter: 'Enter', tab: 'Tab', same: '=' }))
</script>

<template>
  <div class="space-y-4" data-baling-board @keydown="onRootKeydown">
    <nav :aria-label="`نمای ${terms.bales}`" class="flex flex-wrap items-center gap-2">
      <a :href="`/Shipments/${props.shipmentId}?tab=bales&view=table`" :aria-current="view === 'table' ? 'page' : undefined"
        class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container">
        <AppIcon name="spreadsheet" class="h-5 w-5" />جدول
      </a>
      <a :href="`/Shipments/${props.shipmentId}?tab=bales&view=cards`" :aria-current="view === 'cards' ? 'page' : undefined"
        class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container">
        <AppIcon name="bale" class="h-5 w-5" />کارت‌ها
      </a>
      <a :href="packHref" class="inline-flex min-h-11 items-center gap-2 rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container">
        <AppIcon name="scan" class="h-5 w-5" />حالت اسکن (گوشی)
      </a>
    </nav>
    <!--
      محمولهٔ قطعی: فقط‌خواندنی (C.8). پیام قفلِ بار اول را سربرگ محموله نشان می‌دهد؛ این‌جا فقط وقتی که همین حالا (از
      دستگاه دیگر) قطعی شد و کار بعدی ۴۰۹ گرفت.
    -->
    <p v-if="locked && !props.locked" class="flex items-start gap-2 rounded-lg bg-surface-container-high px-4 py-3 text-sm" role="alert" data-numeric>
      <AppIcon name="lock" class="mt-0.5 h-4 w-4 shrink-0" />
      <span>{{ lockText }}</span>
    </p>

    <template v-if="!locked">
      <CurrentBaleHeader
        :bales="board.bales.value"
        :current="board.current.value"
        :target="target"
        :tolerance-percent="tolerance"
        @update:current="board.setCurrent"
        @next="
          () => {
            board.nextBale()
            focusSerial()
          }
        "
      >
        <template #actions>
          <!-- پیوند «حالت اسکن (گوشی)» بالای زبانه است (بی‌اسکریپت هم کار می‌کند)؛ این‌جا QR همان نشانی تا گوشی بخواندش (G.5) -->
          <button
            type="button"
            class="inline-flex min-h-11 items-center gap-2 rounded-lg px-3 text-sm text-on-surface-variant hover:bg-surface-container sm:ms-auto"
            :aria-expanded="qrOpen"
            :aria-controls="`${uid}-qr`"
            @click="qrOpen = !qrOpen"
          >
            <AppIcon name="qr" class="h-5 w-5" />
            کد QR برای گوشی
          </button>
        </template>
      </CurrentBaleHeader>

      <div v-if="qrOpen" :id="`${uid}-qr`" class="flex flex-wrap items-center gap-4 rounded-xl border border-outline-variant bg-surface-container-lowest p-4">
        <!-- QR روی سطح سفید چاپی تا هر دوربینی بخواند -->
        <div class="rounded-lg bg-white p-2"><QrImage :value="packAbsolute" :size="144" /></div>
        <p class="max-w-sm text-sm text-on-surface-variant">{{ message('I_BOARD_PACK_QR') }}</p>
      </div>

      <!-- جعبهٔ شماره -->
      <form class="flex flex-wrap items-end gap-2" @submit.prevent="submitSerial">
        <div class="min-w-48 flex-1">
          <label :for="`${uid}-serial`" class="mb-1 block text-sm font-medium">{{ message('I_BOARD_SERIAL_HINT') }}</label>
          <input
            :id="`${uid}-serial`"
            ref="serialInput"
            v-model="serial"
            dir="ltr"
            inputmode="text"
            enterkeyhint="go"
            autocomplete="off"
            autocapitalize="off"
            spellcheck="false"
            data-hotkeys-off
            class="min-h-12 w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-3 text-end text-lg outline-none focus:border-primary focus:ring-1 focus:ring-primary"
            :aria-describedby="board.lastError.value ? `${uid}-serial-error` : undefined"
            :aria-invalid="board.lastError.value ? 'true' : undefined"
            data-numeric
          />
        </div>
        <Button variant="primary" type="submit" icon="plus" :loading="adding" :disabled="board.busy.value > 0 && !adding">
          {{ message('I_BOARD_ADD_TO', { n: board.current.value }) }}
        </Button>
        <BarcodeScan @scanned="onScanned" />
        <Button icon="warehouse" @click="stockOpen = true">از انبار</Button>
      </form>
      <p v-if="board.lastError.value" :id="`${uid}-serial-error`" class="-mt-2 flex items-start gap-1 text-sm text-error" role="alert">
        <AppIcon name="error" class="mt-0.5 h-4 w-4 shrink-0" />
        {{ board.lastError.value }}
      </p>
    </template>

    <!-- پرسش (منتقل شود؟ / داخل دسته / سانت؟) -->
    <div
      v-if="board.question.value"
      ref="questionBox"
      data-board-question
      class="flex flex-wrap items-center gap-3 rounded-xl border border-secondary/50 bg-secondary-container/50 px-4 py-3"
      role="group"
      :aria-label="board.question.value.message"
      @keydown="onQuestionKeydown"
    >
      <AppIcon name="info" class="h-5 w-5 shrink-0 text-on-secondary-container" />
      <p class="min-w-0 flex-1 text-sm font-medium text-on-surface" aria-live="assertive" data-numeric>{{ board.question.value.message }}</p>
      <div class="flex flex-wrap gap-2">
        <button
          v-for="(choice, index) in board.question.value.choices"
          :key="index"
          type="button"
          class="inline-flex min-h-11 items-center gap-1.5 rounded-lg px-4 text-sm"
          :class="choice.primary ? 'bg-primary font-semibold text-on-primary hover:bg-primary-hover' : 'border border-outline-variant bg-surface-container-lowest text-on-surface hover:bg-surface-container'"
          @mousedown.prevent
          @click="answer(index)"
        >
          <AppIcon v-if="choice.icon" :name="choice.icon" class="h-5 w-5" />
          {{ choice.label }}
          <kbd
            v-if="index === 0 || index === board.question.value.choices.length - 1"
            class="ms-1 hidden rounded border border-current/30 px-1 text-xs opacity-70 pointer-fine:inline"
            dir="ltr"
            aria-hidden="true"
          >{{ index === 0 ? 'Enter' : 'Esc' }}</kbd>
        </button>
      </div>
    </div>

    <!-- قطع اینترنت: افزودن‌هایی که نرسیدند (G.5، G.7) -->
    <div
      v-if="board.pending.value.length > 0"
      class="flex flex-wrap items-center gap-3 rounded-xl border border-warning/40 bg-surface-container-low px-4 py-3 text-sm"
      role="alert"
    >
      <AppIcon name="warning" class="h-5 w-5 shrink-0 text-warning" />
      <p class="min-w-0 flex-1">{{ message('I_BOARD_PENDING', { n: board.pending.value.length }) }}</p>
      <Button variant="primary" icon="upload" :loading="board.busy.value > 0" @click="board.retryPending()">الان دوباره بفرست</Button>
    </div>

    <!-- تراشه‌های آمادگی با کار همین صفحه -->
    <ul v-if="chips.length > 0" class="space-y-2" aria-label="کارهای مانده تا قطعی شدن">
      <li
        v-for="(issue, i) in chips"
        :key="`${issue.code}-${issue.key ?? issue.baleId ?? i}`"
        class="flex flex-wrap items-center gap-2 rounded-xl border px-3 py-2 text-sm"
        :class="issue.blocking ? 'border-error/30 bg-error-container/40' : 'border-warning/30 bg-surface-container-low'"
        :data-readiness="issue.code"
      >
        <AppIcon :name="issue.blocking ? 'error' : 'warning'" class="h-5 w-5 shrink-0" :class="issue.blocking ? 'text-error' : 'text-warning'" />
        <span :id="`${uid}-issue-${i}`" class="min-w-0 flex-1" data-numeric>{{ issue.message }}</span>
        <button
          v-if="issue.code !== ReadinessCode.EmptyBale"
          type="button"
          class="inline-flex min-h-11 items-center gap-1.5 rounded-lg border border-outline-variant bg-surface-container-lowest px-3 hover:bg-surface-container"
          :aria-describedby="`${uid}-issue-${i}`"
          @click="showIssue(issue)"
        >
          <AppIcon name="eye" class="h-5 w-5" />
          نشانم بده
        </button>
        <button
          v-if="issue.code === ReadinessCode.NoFinalSize"
          type="button"
          class="inline-flex min-h-11 items-center gap-1.5 rounded-lg bg-primary px-3 font-semibold text-on-primary hover:bg-primary-hover"
          :aria-describedby="`${uid}-issue-${i}`"
          @click="board.sameAsIntakeMany(issue.rugIds)"
        >
          <AppIcon name="ruler" class="h-5 w-5" />
          بقیه همان اندازهٔ اول
        </button>
        <button
          v-if="issue.code === ReadinessCode.EmptyBale && issue.baleId"
          type="button"
          class="inline-flex min-h-11 items-center gap-1.5 rounded-lg border border-outline-variant bg-surface-container-lowest px-3 hover:bg-surface-container"
          :aria-describedby="`${uid}-issue-${i}`"
          @click="board.deleteBale(issue.baleId)"
        >
          <AppIcon name="trash" class="h-5 w-5" />
          حذف کن
        </button>
        <button
          v-if="issue.code === ReadinessCode.NoDeclaredValue && canBusiness && issue.key"
          type="button"
          class="inline-flex min-h-11 items-center gap-1.5 rounded-lg bg-primary px-3 font-semibold text-on-primary hover:bg-primary-hover"
          :aria-describedby="`${uid}-issue-${i}`"
          @click="openRateForm(issue.key)"
        >
          <AppIcon name="dollar" class="h-5 w-5" />
          نرخ بگذارید
        </button>
      </li>
    </ul>

    <!-- «ارزش گمرکی «کاشان» برای هر متر: [___] دلار [ثبت]» -->
    <form
      v-if="rateForm"
      class="flex flex-wrap items-end gap-2 rounded-xl border border-outline-variant bg-surface-container-lowest px-4 py-3"
      @submit.prevent="submitRateForm"
    >
      <div class="min-w-48 flex-1">
        <label :for="`${uid}-rate-form`" class="mb-1 block text-sm font-medium">{{ message('I_BOARD_RATE_FORM', { key: rateForm.key }) }}</label>
        <NumberField :id="`${uid}-rate-form`" v-model="rateFormValue" kind="usd" label="دلار هر متر" />
      </div>
      <Button variant="primary" type="submit" icon="check" :loading="rateFormSaving" :disabled="rateFormValue === null">ثبت</Button>
      <Button variant="ghost" @click="rateForm = null">نه</Button>
    </form>

    <!-- نوار ابزار: نما، ستون‌ها، برگرد، نوار گروهی -->
    <div v-if="board.loaded.value && board.rows.value.length > 0" class="flex flex-wrap items-center gap-2">
      <button
        v-if="view === 'table'"
        type="button"
        class="inline-flex min-h-11 items-center gap-1.5 rounded-lg border border-outline-variant px-3 text-sm hover:bg-surface-container"
        :aria-pressed="measuring"
        @click="measuring = !measuring"
      >
        <AppIcon name="ruler" class="h-5 w-5" />
        {{ measuring ? 'همهٔ ستون‌ها' : 'فقط اندازه‌گیری' }}
      </button>
      <Button v-if="!locked" icon="undo" :disabled="board.undoCount.value === 0" @mousedown.prevent @click="board.undo()">
        برگرد
        <kbd class="hidden rounded border border-current/30 px-1 text-xs opacity-70 pointer-fine:inline" dir="ltr" aria-hidden="true">Ctrl+Z</kbd>
      </Button>

      <template v-if="!locked && selected.size > 0">
        <span class="ms-2 text-sm font-medium" data-numeric>{{ faDigitsOf(selected.size) }} فرش انتخاب شده</span>
        <Button icon="ruler" @click="bulkSame">همان اندازه</Button>
        <Button icon="move" @click="openMove(selectedRows)">انتقال</Button>
        <Button variant="ghost" icon="close" @click="clearSelection">لغو انتخاب</Button>
      </template>

      <p class="ms-auto flex min-h-11 items-center gap-1.5 text-sm text-on-surface-variant" role="status" aria-live="polite">
        <template v-if="board.busy.value > 0">
          <span class="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent" aria-hidden="true"></span>
          {{ message('I_SAVING') }}
        </template>
      </p>
    </div>

    <!-- «نشانم بده» -->
    <div v-if="board.onlyRugIds.value" class="flex flex-wrap items-center gap-3 rounded-xl bg-surface-container px-4 py-2 text-sm" role="status">
      <AppIcon name="filter" class="h-5 w-5 shrink-0" />
      <p class="min-w-0 flex-1" data-numeric>{{ message('I_BOARD_SHOWING', { n: shownCount }) }}</p>
      <Button variant="ghost" icon="close" @click="board.showOnly(null)">همه را نشان بده</Button>
    </div>

    <!-- بار -->
    <p v-if="board.loading.value && !board.loaded.value" class="skeleton h-48 w-full rounded-xl" aria-hidden="true"></p>
    <p v-if="board.loadError.value" class="rounded-lg bg-error-container px-4 py-3 text-sm text-on-error-container" role="alert">{{ board.loadError.value }}</p>

    <!-- خالی -->
    <EmptyState
      v-else-if="board.loaded.value && board.bales.value.length === 0"
      icon="bale"
      :title="message('I_BOARD_EMPTY', { n: board.current.value })"
    />

    <!-- جدول -->
    <div
      v-else-if="board.loaded.value && view === 'table'"
      class="relative overflow-x-auto rounded-xl border border-outline-variant bg-surface-container-lowest"
      data-hotkeys-off
    >
      <table
        role="grid"
        :aria-label="terms.bales"
        :aria-rowcount="visibleRows.length + visibleBales.length + 1"
        :aria-colcount="colCount"
        class="w-full min-w-max border-collapse text-sm"
      >
        <thead class="sticky top-0 z-10 bg-surface-container">
          <tr aria-rowindex="1">
            <th scope="col" class="w-11 px-1"><span class="sr-only">انتخاب</span></th>
            <th
              v-for="column in columns"
              :key="column.key"
              scope="col"
              class="border-s border-outline-variant/60 px-2 py-2 text-start font-medium"
              :style="{ minWidth: `${column.width}ch` }"
            >
              {{ column.label }}
            </th>
            <th scope="col" class="w-24 px-1"><span class="sr-only">کارهای ردیف</span></th>
          </tr>
        </thead>
        <tbody v-for="group in visibleBales" :key="group.bale.id" :data-bale-number="group.bale.number">
          <tr
            v-for="row in group.rows"
            :key="row.lineId"
            :data-line-id="row.lineId"
            :aria-label="rowLabel(row)"
            class="border-b border-outline-variant/50"
            :class="[
              isCurrentBale(row.baleNumber) ? 'bg-primary/[0.03]' : '',
              savingRows[row.lineId] ? 'opacity-70' : '',
              addFlowLine === row.lineId ? 'outline outline-2 -outline-offset-2 outline-primary/40' : '',
            ]"
          >
            <td role="gridcell" class="px-1 text-center">
              <input
                v-if="!locked"
                type="checkbox"
                tabindex="-1"
                class="h-5 w-5 accent-primary"
                :checked="selected.has(row.lineId)"
                :aria-label="`انتخاب ${serialText(row.serial)}`"
                @change="toggleSelect(row)"
              />
            </td>
            <td
              v-for="column in columns"
              :key="column.key"
              role="gridcell"
              class="border-s border-outline-variant/40 px-1 py-0.5 align-middle"
              :class="column.key === 'serial' ? 'font-semibold' : ''"
              data-numeric
            >
              <!-- اندازهٔ نهایی -->
              <template v-if="column.edit === 'size' && !locked">
                <input
                  :id="sizeInputId(row.lineId, column.key === 'finalLength' ? 'L' : 'W')"
                  type="text"
                  dir="ltr"
                  inputmode="decimal"
                  enterkeyhint="next"
                  autocomplete="off"
                  spellcheck="false"
                  :data-size-line="row.lineId"
                  :value="sizeDisplay(row, column.key === 'finalLength' ? 'L' : 'W')"
                  :placeholder="faFixed(column.key === 'finalLength' ? row.intakeLength : row.intakeWidth)"
                  :aria-label="`${column.label} ${serialText(row.serial)}`"
                  :aria-invalid="sizeErrors[cellKey(row.lineId, column.key === 'finalLength' ? 'L' : 'W')] ? 'true' : undefined"
                  :aria-describedby="sizeErrors[cellKey(row.lineId, column.key === 'finalLength' ? 'L' : 'W')] ? `${uid}-size-error` : undefined"
                  aria-keyshortcuts="="
                  class="min-h-11 w-full rounded-md border bg-transparent px-2 text-end outline-none placeholder:text-on-surface-variant/50 focus:border-primary focus:bg-surface-container-lowest focus:ring-1 focus:ring-primary"
                  :class="sizeErrors[cellKey(row.lineId, column.key === 'finalLength' ? 'L' : 'W')] ? 'border-error' : 'border-transparent'"
                  @focus="onSizeFocus(row, column.key === 'finalLength' ? 'L' : 'W', $event)"
                  @input="onSizeInput(row, column.key === 'finalLength' ? 'L' : 'W', $event)"
                  @blur="onSizeBlur(row, column.key === 'finalLength' ? 'L' : 'W', $event)"
                  @keydown="onSizeKeydown(row, column.key === 'finalLength' ? 'L' : 'W', $event)"
                />
              </template>

              <!-- خانهٔ توصیفی -->
              <template v-else-if="column.edit === 'text' && !locked">
                <input
                  v-if="editing && editing.lineId === row.lineId && editing.key === column.key"
                  :id="`${uid}-edit`"
                  v-model="editText"
                  :list="`${uid}-suggest`"
                  autocomplete="off"
                  class="min-h-11 w-full rounded-md border border-primary bg-surface-container-lowest px-2 outline-none ring-1 ring-primary"
                  :aria-label="`${column.label} ${serialText(row.serial)}`"
                  @keydown="onEditKeydown(row, $event)"
                  @blur="commitEdit(row)"
                />
                <div
                  v-else
                  role="button"
                  :tabindex="column.tabStop ? 0 : -1"
                  class="flex min-h-11 items-center rounded-md px-2 hover:bg-surface-container"
                  :aria-label="`${column.label}: ${cellText(column.key, row, simple) || '—'}`"
                  @click="startEdit(row, column)"
                  @dblclick="startEdit(row, column)"
                  @keydown="onTextCellKeydown(row, column, $event)"
                >{{ cellText(column.key, row, simple) }}</div>
              </template>

              <!-- نرخ (Business ویرایش می‌کند) -->
              <template v-else-if="column.key === 'rate'">
                <input
                  v-if="rateEditing === row.lineId"
                  :id="`${uid}-rate`"
                  v-model="rateText"
                  dir="ltr"
                  inputmode="decimal"
                  autocomplete="off"
                  class="min-h-11 w-24 rounded-md border border-primary bg-surface-container-lowest px-2 text-end outline-none ring-1 ring-primary"
                  :aria-label="`${column.label} ${serialText(row.serial)}`"
                  @keydown="onRateKeydown(row, $event)"
                  @blur="commitRate(row)"
                />
                <div v-else class="flex min-h-11 flex-wrap items-center gap-1 px-1">
                  <button
                    v-if="column.edit === 'rate' && !locked"
                    type="button"
                    tabindex="-1"
                    class="min-h-9 rounded-md px-1.5 hover:bg-surface-container"
                    :aria-label="`${column.label} ${serialText(row.serial)}: ${cellText('rate', row, simple) || '—'}`"
                    @click="startRate(row)"
                  >{{ cellText('rate', row, simple) || '—' }}</button>
                  <span v-else>{{ cellText('rate', row, simple) }}</span>
                  <button
                    v-if="row.rateSource === DeclaredRateSource.Missing && canBusiness && !locked && row.declaredKey"
                    type="button"
                    tabindex="-1"
                    class="min-h-9 rounded-full bg-error-container px-2 text-xs font-medium text-on-error-container"
                    @click="openRateForm(row.declaredKey)"
                  >{{ rateSourceText(row.rateSource) }}</button>
                  <span
                    v-else
                    class="rounded-full px-2 text-xs"
                    :class="row.rateSource === DeclaredRateSource.Missing ? 'bg-error-container text-on-error-container' : 'bg-surface-container-high text-on-surface-variant'"
                  >{{ rateSourceText(row.rateSource) }}</span>
                </div>
              </template>

              <!-- نگله: لمس → انتقال -->
              <template v-else-if="column.key === 'bale' && !locked">
                <button
                  type="button"
                  tabindex="-1"
                  class="min-h-11 w-full rounded-md px-2 text-center font-semibold hover:bg-surface-container"
                  :aria-label="`انتقال ${serialText(row.serial)} از ${terms.baleOf} ${faDigitsOf(row.baleNumber)}`"
                  @click="openMove(row)"
                >{{ faDigitsOf(row.baleNumber) }}</button>
              </template>

              <!-- شماره -->
              <template v-else-if="column.key === 'serial'">
                <span class="px-2"><bdi dir="ltr">{{ cellText('serial', row, simple) }}</bdi></span>
                <span
                  v-if="sizeChangeBadge(row.sizeChangeCm)"
                  class="ms-1 rounded-full bg-surface-container-high px-1.5 text-xs font-medium"
                  :aria-label="sizeChangeBadge(row.sizeChangeCm)?.label"
                >{{ sizeChangeBadge(row.sizeChangeCm)?.text }}</span>
              </template>

              <!-- بقیه فقط‌خواندنی -->
              <span v-else class="block px-2" :class="column.key === 'intake' ? 'text-on-surface-variant' : ''">
                <bdi v-if="column.key === 'intake' || column.key === 'area'" dir="ltr">{{ cellText(column.key, row, simple) }}</bdi>
                <template v-else>{{ cellText(column.key, row, simple) }}</template>
              </span>
            </td>
            <td role="gridcell" class="whitespace-nowrap px-1">
              <div v-if="!locked" class="flex items-center justify-end gap-1">
                <button
                  v-if="row.finalLength === null"
                  type="button"
                  tabindex="-1"
                  class="min-h-11 rounded-lg border border-outline-variant px-2 text-xs font-semibold hover:bg-surface-container"
                  :aria-label="`همان اندازهٔ اول برای ${serialText(row.serial)}`"
                  @click="sameFor(row)"
                >همان</button>
                <button
                  type="button"
                  tabindex="-1"
                  data-row-menu
                  class="grid h-11 w-11 place-items-center rounded-lg text-on-surface-variant hover:bg-surface-container"
                  :aria-label="`کارهای ${serialText(row.serial)}`"
                  aria-haspopup="menu"
                  :aria-expanded="menuLine === row.lineId"
                  @click="openMenu(row, $event)"
                >
                  <AppIcon name="dots" class="h-5 w-5" />
                </button>
              </div>
            </td>
          </tr>
          <BaleFooterRow :bale="group.bale" :target="target" :tolerance-percent="tolerance" :colspan="colCount" />
        </tbody>
      </table>
      <datalist :id="`${uid}-suggest`">
        <option v-for="s in suggestions" :key="s" :value="s" />
      </datalist>
    </div>

    <!-- کارت‌ها -->
    <div v-else-if="board.loaded.value" class="space-y-5">
      <section v-for="group in visibleBales" :key="group.bale.id" :aria-labelledby="`${uid}-bale-${group.bale.id}`" class="space-y-2">
        <h3 :id="`${uid}-bale-${group.bale.id}`" class="flex items-center gap-2 text-base font-bold" data-numeric>
          <AppIcon name="bale" class="h-5 w-5" />
          {{ terms.baleOf }} {{ faDigitsOf(group.bale.number) }}
          <span v-if="isCurrentBale(group.bale.number)" class="rounded-full bg-primary/10 px-2 text-xs font-medium text-primary">جاری</span>
        </h3>
        <div class="grid gap-3 md:grid-cols-2">
          <BaleCard
            v-for="row in group.rows"
            :key="row.lineId"
            :row="row"
            :simple="simple"
            :locked="locked"
            @move="openMove"
            @size="openSheet"
            @same="sameFor"
            @remove="(r) => board.removeRow(r)"
            @split="splitRow"
          />
        </div>
        <BaleFooterRow as="block" :bale="group.bale" :target="target" :tolerance-percent="tolerance" />
      </section>
    </div>

    <p v-if="sizeErrors && activeSize && sizeErrors[cellKey(activeSize.lineId, activeSize.side)]" :id="`${uid}-size-error`" class="text-sm text-error" role="alert">
      {{ sizeErrors[cellKey(activeSize.lineId, activeSize.side)] }}
    </p>
    <p v-if="view === 'table' && !locked && board.rows.value.length > 0" class="hidden text-xs text-on-surface-variant pointer-fine:block">{{ keysHint }}</p>

    <!-- منوی ردیف -->
    <Teleport to="body">
      <div
        v-if="menuRow"
        :id="`${uid}-menu`"
        role="menu"
        :aria-label="`کارهای ${serialText(menuRow.serial)}`"
        class="fixed z-[60] min-w-52 rounded-xl border border-outline-variant bg-surface-container-lowest py-1 shadow-lg"
        :style="menuStyle"
        @keydown="onMenuKeydown"
      >
        <button type="button" role="menuitem" class="flex min-h-11 w-full items-center gap-2 px-4 text-sm hover:bg-surface-container focus:bg-surface-container focus:outline-none" @click="openMove(menuRow)">
          <AppIcon name="move" class="h-5 w-5" />
          انتقال
        </button>
        <button type="button" role="menuitem" class="flex min-h-11 w-full items-center gap-2 px-4 text-sm hover:bg-surface-container focus:bg-surface-container focus:outline-none" @click="menuRemove(menuRow)">
          <AppIcon name="warehouse" class="h-5 w-5" />
          خارج کردن از {{ terms.bale }}
        </button>
        <button
          type="button"
          role="menuitem"
          class="flex min-h-11 w-full items-center gap-2 px-4 text-sm hover:bg-surface-container focus:bg-surface-container focus:outline-none"
          @click="() => { const r = menuRow; closeMenu(); if (r) openSheet(r) }"
        >
          <AppIcon name="ruler" class="h-5 w-5" />
          اندازه
        </button>
        <button
          v-if="menuRow.qty > 1"
          type="button"
          role="menuitem"
          class="flex min-h-11 w-full items-center gap-2 px-4 text-sm hover:bg-surface-container focus:bg-surface-container focus:outline-none"
          @click="splitRow(menuRow)"
        >
          <AppIcon name="split" class="h-5 w-5" />
          جدا کردن از دسته
        </button>
        <a role="menuitem" :href="`/Rugs/Details/${menuRow.rugId}`" class="flex min-h-11 items-center gap-2 px-4 text-sm hover:bg-surface-container focus:bg-surface-container focus:outline-none">
          <AppIcon name="rug" class="h-5 w-5" />
          صفحهٔ فرش
        </a>
      </div>
    </Teleport>

    <!-- پیام‌های دکمه‌دار («۳۷۷۸ به انبار برگشت [برگردان]») -->
    <Teleport to="body">
      <div class="pointer-events-none fixed bottom-20 end-4 z-[55] flex w-[min(24rem,calc(100vw-2rem))] flex-col gap-2" role="status" aria-live="polite">
        <div
          v-for="notice in board.notices.value"
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
          <p class="min-w-0 flex-1" data-numeric>{{ notice.text }}</p>
          <button
            v-if="notice.action"
            type="button"
            class="min-h-11 shrink-0 rounded-lg px-3 font-semibold text-primary hover:bg-primary/10"
            @click="
              () => {
                board.dismissNotice(notice.id)
                void notice.action?.run()
              }
            "
          >{{ notice.action.label }}</button>
          <button
            type="button"
            class="grid h-11 w-11 shrink-0 place-items-center rounded-lg text-on-surface-variant hover:bg-surface-container"
            aria-label="بستن"
            @click="board.dismissNotice(notice.id)"
          >
            <AppIcon name="close" class="h-4 w-4" />
          </button>
        </div>
      </div>
    </Teleport>

    <!-- زیر انگشت: نوار ۵۶ پیکسلی بالای صفحه‌کلید (H.6 ب) -->
    <div
      v-if="activeSize && !locked"
      data-touch-bar
      class="fixed inset-x-0 bottom-0 z-50 hidden gap-2 border-t border-outline-variant bg-surface-container px-3 py-1 pb-safe pointer-coarse:flex"
    >
      <button type="button" class="min-h-14 flex-1 rounded-lg bg-surface-container-lowest text-sm font-semibold text-on-surface" @mousedown.prevent @click="touchSame">همان اندازه</button>
      <button type="button" class="min-h-14 flex-1 rounded-lg bg-surface-container-lowest text-sm text-on-surface" @mousedown.prevent @click="touchMove(-1)">قبلی</button>
      <button type="button" class="min-h-14 flex-1 rounded-lg bg-primary text-sm font-semibold text-on-primary" @mousedown.prevent @click="touchMove(1)">بعدی</button>
      <button type="button" class="min-h-14 flex-1 rounded-lg bg-surface-container-lowest text-sm text-on-surface" @mousedown.prevent @click="board.undo()">برگرد</button>
      <button type="button" class="min-h-14 flex-1 rounded-lg bg-surface-container-lowest text-sm text-on-surface" @mousedown.prevent @click="touchClose">بستن</button>
    </div>

    <BaleMovePicker
      v-model:open="moveOpen"
      :title="moveTitle"
      :description="moveDescription"
      :bales="board.bales.value"
      :current="board.current.value"
      :from="moveFrom"
      :can-up="moveCanUp"
      :can-down="moveCanDown"
      @pick="onMovePick"
    />
    <FinalSizeSheet
      v-model:open="sheetOpen"
      :row="sheetRow"
      :save="sheetSave"
      :has-prev="sheetIndex > 0"
      :has-next="sheetIndex >= 0 && sheetIndex < visibleRows.length - 1"
      :can-undo="board.undoCount.value > 0"
      :locked="locked"
      @navigate="sheetNavigate"
      @undo="board.undo()"
    />
    <StockPicker v-model:open="stockOpen" :bale-number="board.current.value" :add="addFromStock" />
    <LotSplitDialog
      v-model:open="splitOpen"
      :rug-id="split?.rugId ?? null"
      :serial="split?.serial ?? null"
      :quantity="split?.quantity ?? 0"
      @done="onSplitDone"
    />
  </div>
</template>
