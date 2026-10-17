/**
 * منطق خالص تختهٔ نگله‌بندی (فاز ۴b) — آزمون‌پذیر و بی DOM؛ جزیره‌ها و کامپوننت‌های `components/bales` همین را
 * صدا می‌زنند تا جدول، کارت‌ها و حالت اسکن یک قاعده داشته باشند:
 *
 * - قالب عددها: متراژ و دلار پانویس با دو رقم اعشار و «٬»/«٫» مثل اکسل («۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار»). جمع‌ها را سرور
 *   با قاعدهٔ «هر تخته، بعد × تعداد» گرد کرده (G.4)؛ این‌جا فقط نمایش است و هرگز ضرب float نیست (`lib/num`).
 * - نوار هدف (C.2): کمتر از هدف × (۱ − t)، در بازه، بیشتر از هدف × (۱ + t). ۱۳ متر با ۱۰٪ بالای ۱۴٫۳ هشدار می‌شود.
 * - نشاندن نتیجهٔ `lines/batch` روی نگله‌های محلی (ردیف تازه، انتقال، خروج، خلاصهٔ نگله‌ها) و ساختن کار وارونه برای
 *   «برگرد».
 * - «نگلهٔ جاری» برای هر کاربر و هر محموله در localStorage (با try/catch؛ نبودِ storage فقط یادآوری را از دست می‌دهد).
 * - ستون‌های جدول از چیدمان کارگاه (`GET /api/grid/bales`) یا پیش‌فرض G.2، با قاعدهٔ ایستگاه Tab حالت ساده.
 */
import { message } from '@/lib/messages'
import { looksLikeDimensionPair, parseDecimal, parseDimensionPair, type ParseDecimalOptions } from '@/lib/money'
import { add, cmp, dec, mul, round, sub, toFixedString, toPlainString, tryDec, type Dec } from '@/lib/num'
import { DeclaredRateSource, type Bale, type BaleRow, type BaleSummary, type LineOp, type LinesBatchResult } from '@/lib/types/shipments'
import type { GridColumn } from '@/lib/types/grid'
import type { AppTerms } from '@/lib/context'
import type { FinalSizeInput, TargetState } from '@/lib/types/bales'

// ═══════════════════════════════════════════════════════════════════
// قالب عدد
// ═══════════════════════════════════════════════════════════════════

const FA_DIGITS = '۰۱۲۳۴۵۶۷۸۹'

/** رقم‌های لاتین → فارسی. */
export function faDigitsOf(value: string | number): string {
  return String(value).replace(/\d/g, (d) => FA_DIGITS[Number(d)])
}

/**
 * عدد با دقیقاً `decimals` رقم اعشار، جداکنندهٔ «٬» و ممیز «٫» (همان شکل پیام‌های سرور): ۱۳٫۵ → «۱۳٫۵۰»،
 * ۶۰۷٫۹۵ → «۶۰۷٫۹۵»، ۱۲۳۴۵ → «۱۲٬۳۴۵٫۰۰». خالی یا نامعتبر → «—».
 */
export function faFixed(value: unknown, decimals = 2): string {
  const d = tryDec(value)
  if (!d) return '—'
  const text = toFixedString(d, decimals)
  const negative = text.startsWith('-')
  const [intPart, fracPart] = text.replace('-', '').split('.')
  const grouped = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, '٬')
  return faDigitsOf((negative ? '-' : '') + grouped + (fracPart ? `٫${fracPart}` : ''))
}

/** عدد با حداکثر `maxDecimals` رقم اعشار و بی صفر انتهایی («۴۵»، «۱۵٫۱»). */
export function faLoose(value: unknown, maxDecimals = 2): string {
  const d = tryDec(value)
  if (!d) return '—'
  let text = toFixedString(d, maxDecimals)
  if (text.includes('.')) text = text.replace(/0+$/, '').replace(/\.$/, '')
  const negative = text.startsWith('-')
  const [intPart, fracPart] = text.replace('-', '').split('.')
  const grouped = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, '٬')
  return faDigitsOf((negative ? '-' : '') + grouped + (fracPart ? `٫${fracPart}` : ''))
}

/** شمارهٔ فرش برای نمایش: «۳۷۷۸» یا «۳۹۱۱ - ۳۹۶۰»؛ بی‌شماره → «—». */
export function serialText(serial: string | null | undefined): string {
  return serial && serial.trim() ? faDigitsOf(serial.trim()) : '—'
}

/** «۲٫۰۸ × ۲٫۱۰». */
export function sizeText(length: unknown, width: unknown): string {
  if (tryDec(length) === null || tryDec(width) === null) return '—'
  return `${faFixed(length)} × ${faFixed(width)}`
}

/** اندازهٔ مؤثر ردیف (نهایی ?? اول). */
export function effectiveSize(row: Pick<BaleRow, 'finalLength' | 'finalWidth' | 'intakeLength' | 'intakeWidth'>): [number, number] {
  return row.finalLength !== null && row.finalWidth !== null
    ? [row.finalLength, row.finalWidth]
    : [row.intakeLength, row.intakeWidth]
}

/** اندازهٔ نهایی با اندازهٔ اول فرق دارد؟ (در حالت ساده «اندازهٔ اول» فقط آن وقت دیده می‌شود، A.3.) */
export function sizeDiffers(row: Pick<BaleRow, 'finalLength' | 'finalWidth' | 'intakeLength' | 'intakeWidth'>): boolean {
  if (row.finalLength === null || row.finalWidth === null) return false
  return cmp(row.finalLength, row.intakeLength) !== 0 || cmp(row.finalWidth, row.intakeWidth) !== 0
}

/** مساحت ردیف برای خانهٔ «مساحت»: تکی «۴٫۳۷»، دسته «۰٫۱۶ × ۵۰ = ۸٫۰۰». */
export function areaCellText(row: Pick<BaleRow, 'areaM2' | 'lineAreaM2' | 'qty'>): string {
  if (row.qty > 1) return `${faFixed(row.areaM2)} × ${faDigitsOf(row.qty)} = ${faFixed(row.lineAreaM2)}`
  return faFixed(row.lineAreaM2)
}

/** نشان تغییر اندازه (C.4): «↓۵ سانت» با برچسب صفحه‌خوان «۵ سانت کوچک‌تر شده»؛ بی تغییر → null. */
export function sizeChangeBadge(cm: number | null | undefined): { text: string; label: string; down: boolean } | null {
  if (cm === null || cm === undefined || cm === 0 || !Number.isFinite(cm)) return null
  const n = Math.abs(cm)
  const down = cm < 0
  return {
    text: `${down ? '↓' : '↑'}${faDigitsOf(n)} سانت`,
    label: message(down ? 'I_BOARD_SIZE_DOWN' : 'I_BOARD_SIZE_UP', { n }),
    down,
  }
}

/** متن منبع نرخ («دستی» / «از جدول» / «نرخ پیش‌فرض» / «ندارد»). */
export function rateSourceText(source: DeclaredRateSource): string {
  switch (source) {
    case DeclaredRateSource.Override:
      return message('I_RATE_OVERRIDE')
    case DeclaredRateSource.Table:
      return message('I_RATE_TABLE')
    case DeclaredRateSource.Fallback:
      return message('I_RATE_FALLBACK')
    default:
      return message('I_RATE_MISSING')
  }
}

// ═══════════════════════════════════════════════════════════════════
// پانویس و نوار هدف (C.2)
// ═══════════════════════════════════════════════════════════════════

/** آنچه پانویس و نوار هدف از یک نگله لازم دارند (هم `Bale` و هم `BaleSummary`). */
export type BaleFigures = Pick<BaleSummary, 'number' | 'pieces' | 'areaM2' | 'usdTotal'>

/** «نگلهٔ ۱ · شامل ۴ تخته · ۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار». */
export function footerText(bale: BaleFigures, terms?: Partial<AppTerms>): string {
  return message('I_BOARD_FOOTER', {
    ...terms,
    n: bale.number,
    pieces: bale.pieces,
    area: faFixed(bale.areaM2),
    usd: faFixed(bale.usdTotal),
  })
}

/** «شامل ۴ تخته · ۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار» (سرِ «نگلهٔ جاری»). */
export function totalsText(bale: Pick<BaleSummary, 'pieces' | 'areaM2' | 'usdTotal'>): string {
  return message('I_BOARD_TOTALS', { pieces: bale.pieces, area: faFixed(bale.areaM2), usd: faFixed(bale.usdTotal) })
}

/** «نگلهٔ ۳: ۴ تخته، ۱۳٫۵۱ متر» برای `aria-live`. */
export function announceText(bale: BaleFigures): string {
  return message('I_BOARD_ANNOUNCE', { n: bale.number, pieces: bale.pieces, area: faFixed(bale.areaM2) })
}

/** درصد صحیح ۰ تا ۱۰۰ → کسر دقیق («10» → 0.10). */
function percentFraction(tolerancePercent: number): Dec {
  const t = Math.min(100, Math.max(0, Math.trunc(Number.isFinite(tolerancePercent) ? tolerancePercent : 0)))
  return { units: BigInt(t), scale: 2 }
}

/** مرز بالای بازه: هدف × (۱ + t). ۱۳ و ۱۰٪ → ۱۴٫۳. */
export function upperBound(target: number, tolerancePercent: number): Dec {
  return mul(target, add('1', percentFraction(tolerancePercent)))
}

/** مرز پایین بازه: هدف × (۱ − t). */
export function lowerBound(target: number, tolerancePercent: number): Dec {
  return mul(target, sub('1', percentFraction(tolerancePercent)))
}

/** وضعیت نوار هدف — همان `BaleTarget.State` سرور (بی گرد کردن، مقایسهٔ دقیق). */
export function targetState(areaM2: number, target: number | null | undefined, tolerancePercent: number): TargetState {
  if (target === null || target === undefined || !(target > 0)) return 'none'
  const area = tryDec(areaM2) ?? dec(0)
  if (cmp(area, upperBound(target, tolerancePercent)) > 0) return 'above'
  if (cmp(area, lowerBound(target, tolerancePercent)) < 0) return 'below'
  return 'within'
}

/** پهنای پر نوار به درصد (فقط نمایش): متراژ نسبت به مرز بالا، حداکثر ۱۰۰. */
export function targetFill(areaM2: number, target: number | null | undefined, tolerancePercent: number): number {
  if (target === null || target === undefined || !(target > 0)) return 0
  const upper = Number(toFixedString(upperBound(target, tolerancePercent), 4))
  const percent = Number(toFixedString(round(mul(tryDec(areaM2) ?? 0, 100), 2), 2))
  // تقسیم فقط برای پهنای دیدنی نوار است (نه متراژ، نه پول)
  return upper > 0 ? Math.max(0, Math.min(100, Math.round(percent / upper))) : 0
}

/** متن نوار هدف: بیشتر از هدف همان پیام آمادگی است که هدف را نام می‌برد («… از ۱۳ متر هدف بیشتر است.»). */
export function targetText(bale: BaleFigures, target: number | null | undefined, tolerancePercent: number): string {
  const state = targetState(bale.areaM2, target, tolerancePercent)
  if (state === 'none' || target === null || target === undefined) return ''
  if (state === 'above') return message('W_BALE_OVER_TARGET', { n: bale.number, area: faLoose(bale.areaM2), target: faLoose(target) })
  if (state === 'within') return message('I_BOARD_TARGET_WITHIN', { target: faLoose(target) })
  return message('I_BOARD_TARGET_BELOW', { target: faLoose(target) })
}

// ═══════════════════════════════════════════════════════════════════
// نگلهٔ جاری (C.2)
// ═══════════════════════════════════════════════════════════════════

/** نسخهٔ کمینهٔ Storage (برای آزمون). */
export interface KeyValueStorage {
  getItem(key: string): string | null
  setItem(key: string, value: string): void
  removeItem(key: string): void
}

function safeLocalStorage(): KeyValueStorage | null {
  try {
    return typeof localStorage === 'undefined' ? null : localStorage
  } catch {
    return null
  }
}

/** کلید «نگلهٔ جاری» برای هر کاربر و هر محموله؛ بی کاربر هیچ. */
export function currentBaleKey(tenantId: string | null, userId: string | null, shipmentId: string): string | null {
  if (!tenantId || !userId || !shipmentId) return null
  return `rugs:current-bale:${tenantId}:${userId}:${shipmentId}`
}

export function readCurrentBale(key: string | null, storage: KeyValueStorage | null = safeLocalStorage()): number | null {
  if (!key || !storage) return null
  try {
    const raw = storage.getItem(key)
    const n = raw === null ? NaN : Number(raw)
    return Number.isSafeInteger(n) && n >= 1 ? n : null
  } catch {
    return null
  }
}

export function writeCurrentBale(key: string | null, value: number, storage: KeyValueStorage | null = safeLocalStorage()): void {
  if (!key || !storage) return
  try {
    storage.setItem(key, String(value))
  } catch {
    /* حالت خصوصی یا سهمیهٔ پر: فقط یادآوری از دست می‌رود */
  }
}

/** بزرگ‌ترین شمارهٔ نگله (۰ بی نگله). */
export function maxBaleNumber(bales: readonly Pick<BaleSummary, 'number'>[]): number {
  return bales.reduce((max, b) => Math.max(max, b.number), 0)
}

/**
 * نگلهٔ جاری پس از بار: ذخیره‌شده اگر هنوز معنا دارد (یک نگلهٔ موجود، یا «بزرگ‌ترین + ۱» که «نگلهٔ بعد» گذاشته و هنوز
 * فرشی نگرفته)، وگرنه بزرگ‌ترین نگله، وگرنه ۱.
 */
export function resolveCurrent(stored: number | null, bales: readonly Pick<BaleSummary, 'number'>[]): number {
  const max = maxBaleNumber(bales)
  if (stored !== null && stored >= 1 && (stored <= max + 1 || bales.some((b) => b.number === stored))) return stored
  return max >= 1 ? max : 1
}

// ═══════════════════════════════════════════════════════════════════
// نشاندن نتیجهٔ lines/batch
// ═══════════════════════════════════════════════════════════════════

function sortRows(rows: BaleRow[]): BaleRow[] {
  return rows.sort((a, b) => a.seq - b.seq)
}

function emptyBale(summary: BaleSummary, target: number | null): Bale {
  return {
    id: summary.id,
    number: summary.number,
    version: summary.version,
    weightKg: summary.weightKg,
    notes: null,
    pieces: summary.pieces,
    areaM2: summary.areaM2,
    usdTotal: summary.usdTotal,
    targetAreaM2: target,
    rows: [],
  }
}

/** ردیف را از هر نگله‌ای که هست برمی‌دارد. */
export function dropRow(bales: Bale[], lineId: string): BaleRow | null {
  for (const bale of bales) {
    const index = bale.rows.findIndex((r) => r.lineId === lineId)
    if (index >= 0) return bale.rows.splice(index, 1)[0]
  }
  return null
}

/** ردیف تازهٔ سرور را جای خودش می‌نشاند (اگر نگله‌اش هنوز محلی نیست، از خلاصه ساخته می‌شود). */
export function placeRow(bales: Bale[], row: BaleRow, target: number | null = null): void {
  dropRow(bales, row.lineId)
  let bale = bales.find((b) => b.id === row.baleId)
  if (!bale) {
    bale = emptyBale(
      { id: row.baleId, number: row.baleNumber, version: 0, lines: 0, pieces: 0, areaM2: 0, usdTotal: 0, weightKg: null },
      target,
    )
    bales.push(bale)
    bales.sort((a, b) => a.number - b.number)
  }
  bale.rows.push(row)
  sortRows(bale.rows)
}

/**
 * خلاصهٔ تازهٔ همهٔ نگله‌ها (پاسخ `lines/batch`): شماره، نسخه و جمع‌ها جایگزین می‌شوند، نگلهٔ تازه ساخته و نگلهٔ
 * حذف‌شده برداشته می‌شود. ردیف‌ها دست نمی‌خورند.
 */
export function applySummaries(bales: Bale[], summaries: readonly BaleSummary[], target: number | null = null): Bale[] {
  const byId = new Map(bales.map((b) => [b.id, b]))
  const next: Bale[] = []
  for (const s of summaries) {
    const bale = byId.get(s.id) ?? emptyBale(s, target)
    bale.number = s.number
    bale.version = s.version
    bale.pieces = s.pieces
    bale.areaM2 = s.areaM2
    bale.usdTotal = s.usdTotal
    bale.weightKg = s.weightKg
    for (const row of bale.rows) row.baleNumber = s.number
    next.push(bale)
  }
  return next.sort((a, b) => a.number - b.number)
}

/**
 * نتیجهٔ `lines/batch` روی نگله‌های محلی: هر کار موفق ردیفش را می‌نشاند (خروج برمی‌دارد)، بعد خلاصه‌ها. خروجی: نگله‌های
 * تازه (همان آرایه یا آرایهٔ تازه).
 */
export function applyBatchResult(bales: Bale[], result: LinesBatchResult, ops: readonly LineOp[], target: number | null = null): Bale[] {
  result.results.forEach((r, i) => {
    if (!r.ok) return
    const kind = r.op ?? ops[i]?.op
    if (kind === 'remove') {
      const lineId = r.row?.lineId ?? ops[i]?.lineId
      if (lineId) dropRow(bales, lineId)
      return
    }
    if (r.row) placeRow(bales, r.row, target)
  })
  return applySummaries(bales, result.bales, target)
}

/** همهٔ ردیف‌ها به ترتیب نگله و Seq. */
export function allRows(bales: readonly Bale[]): BaleRow[] {
  return [...bales].sort((a, b) => a.number - b.number).flatMap((b) => [...b.rows].sort((x, y) => x.seq - y.seq))
}

// ═══════════════════════════════════════════════════════════════════
// کارها و «برگرد»
// ═══════════════════════════════════════════════════════════════════

/** کار اندازهٔ نهایی برای یک ردیف. */
export function measureOp(row: Pick<BaleRow, 'rugId' | 'rugVersion'>, size: FinalSizeInput, clientOpId?: string): LineOp {
  const op: LineOp = { op: 'measure', rugId: row.rugId, rugVersion: row.rugVersion }
  if (clientOpId) op.clientOpId = clientOpId
  if (size.kind === 'pair') {
    op.finalLength = size.length
    op.finalWidth = size.width
  } else if (size.kind === 'same') {
    op.sameAsIntake = true
  } else {
    op.clear = true
  }
  return op
}

/** اندازهٔ نهایی فعلی ردیف به شکل «برگرد» (نبود اندازه → پاک کردن). */
export function finalSizeOf(row: Pick<BaleRow, 'finalLength' | 'finalWidth'>): FinalSizeInput {
  return row.finalLength !== null && row.finalWidth !== null
    ? { kind: 'pair', length: row.finalLength, width: row.finalWidth }
    : { kind: 'clear' }
}

/**
 * Seq برای جابه‌جایی یک ردیف در همان نگله (بالا/پایین) با عمل `move` و `seq`: سرور ردیفی را که آن Seq را دارد و
 * بعدی‌هایش را یکی جلو می‌برد، پس «بالا» Seq ردیف قبلی و «پایین» Seq ردیف بعدی + ۱ است. ته یا سرِ نگله → null.
 */
export function reorderSeq(rows: readonly Pick<BaleRow, 'lineId' | 'seq'>[], lineId: string, direction: 'up' | 'down'): number | null {
  const ordered = [...rows].sort((a, b) => a.seq - b.seq)
  const index = ordered.findIndex((r) => r.lineId === lineId)
  if (index < 0) return null
  if (direction === 'up') return index === 0 ? null : ordered[index - 1].seq
  return index === ordered.length - 1 ? null : ordered[index + 1].seq + 1
}

/** یک قدم «برگرد». */
export type UndoEntry =
  | { kind: 'measure'; rugId: string; before: FinalSizeInput; serial: string | null }
  | { kind: 'move'; lineId: string; baleId: string; baleNumber: number; seq: number; serial: string | null }
  | { kind: 'remove'; rugId: string; baleId: string; baleNumber: number; seq: number; serial: string | null }
  | { kind: 'add'; lineId: string; serial: string | null }
  | { kind: 'group'; entries: UndoEntry[] }

/** کار وارونهٔ یک قدم، با نسخه‌های تازهٔ ردیف‌های محلی؛ ردیفی که دیگر نیست → بی آن کار. */
export function inverseOps(entry: UndoEntry, rows: readonly BaleRow[], baleExists: (baleId: string) => boolean, newId: () => string): LineOp[] {
  switch (entry.kind) {
    case 'group':
      return entry.entries.flatMap((e) => inverseOps(e, rows, baleExists, newId))
    case 'measure': {
      const row = rows.find((r) => r.rugId === entry.rugId)
      return row ? [measureOp(row, entry.before, newId())] : []
    }
    case 'move': {
      const row = rows.find((r) => r.lineId === entry.lineId)
      if (!row) return []
      return [
        {
          op: 'move',
          clientOpId: newId(),
          lineId: row.lineId,
          lineVersion: row.lineVersion,
          ...(baleExists(entry.baleId) ? { baleId: entry.baleId } : { baleNumber: entry.baleNumber }),
          seq: entry.seq,
        },
      ]
    }
    case 'remove':
      return [
        {
          op: 'add',
          clientOpId: newId(),
          rugId: entry.rugId,
          ...(baleExists(entry.baleId) ? { baleId: entry.baleId } : { baleNumber: entry.baleNumber }),
          seq: entry.seq,
        },
      ]
    case 'add': {
      const row = rows.find((r) => r.lineId === entry.lineId)
      return row ? [{ op: 'remove', clientOpId: newId(), lineId: row.lineId, lineVersion: row.lineVersion }] : []
    }
  }
}

/** کاری که Seq دیگران را جابه‌جا می‌کند (پس نسخهٔ محلی آن‌ها کهنه می‌شود و نگله‌ها باید دوباره بار شوند). */
export function shiftsOthers(ops: readonly LineOp[]): boolean {
  return ops.some((o) => (o.op === 'add' || o.op === 'move') && o.seq !== null && o.seq !== undefined)
}

// ═══════════════════════════════════════════════════════════════════
// ستون‌های جدول (G.2)
// ═══════════════════════════════════════════════════════════════════

export type BoardColumnKey =
  | 'bale'
  | 'serial'
  | 'qty'
  | 'fieldColor'
  | 'borderColor'
  | 'pattern'
  | 'origin'
  | 'material'
  | 'intake'
  | 'finalLength'
  | 'finalWidth'
  | 'area'
  | 'rate'
  | 'usd'

/** کلید فیلد فرش برای ستون‌های توصیفی (ویرایش از `POST /api/grid/rugs/batch`). */
export const DESCRIPTIVE_FIELD: Readonly<Partial<Record<BoardColumnKey, string>>> = {
  fieldColor: 'rug.custom.bg_color',
  borderColor: 'rug.custom.border_color',
  pattern: 'rug.pattern',
  origin: 'rug.origin',
  material: 'rug.material',
}

/** عضو `BaleRow` هر ستون توصیفی. */
export const DESCRIPTIVE_MEMBER: Readonly<Partial<Record<BoardColumnKey, keyof BaleRow>>> = {
  fieldColor: 'fieldColor',
  borderColor: 'borderColor',
  pattern: 'pattern',
  origin: 'origin',
  material: 'material',
}

export interface BoardColumn {
  key: BoardColumnKey
  label: string
  /** پهنا به `ch` (با اندازهٔ نوشته بزرگ می‌شود) */
  width: number
  /** نوع ویرایش: اندازه (همیشه خانهٔ ورودی)، متن توصیفی، نرخ دستی (Business)، یا هیچ */
  edit: 'size' | 'text' | 'rate' | null
  /** ایستگاه Tab است (در حالت ساده ستون‌های توصیفی نیستند، G.2) */
  tabStop: boolean
}

/** کلیدهایی که جدول نگله از چیدمان کارگاه می‌شناسد (نام‌های کاتالوگ E.3 و جدول فرش‌ها). */
const LAYOUT_KEYS: Readonly<Record<string, BoardColumnKey>> = {
  'bale.number': 'bale',
  'line.bale': 'bale',
  'line.baleNumber': 'bale',
  'rug.serial': 'serial',
  'rug.qty': 'qty',
  'rug.quantity': 'qty',
  'rug.custom.bg_color': 'fieldColor',
  'rug.custom.border_color': 'borderColor',
  'rug.pattern': 'pattern',
  'rug.origin': 'origin',
  'rug.material': 'material',
  'rug.intakeSize': 'intake',
  'line.intakeSize': 'intake',
  'rug.intakeLength': 'intake',
  'rug.intakeWidth': 'intake',
  'rug.finalLength': 'finalLength',
  'line.length': 'finalLength',
  'line.finalLength': 'finalLength',
  'rug.finalWidth': 'finalWidth',
  'line.width': 'finalWidth',
  'line.finalWidth': 'finalWidth',
  'line.area': 'area',
  'line.lineArea': 'area',
  'rug.areaM2': 'area',
  'rug.totalAreaM2': 'area',
  'line.usdPerM2': 'rate',
  'line.rate': 'rate',
  'line.usd': 'usd',
  'line.lineUsd': 'usd',
  'line.pieceUsd': 'usd',
  'line.totalUsd': 'usd',
}

/** نام ستون جدول برای یک کلید چیدمان؛ ناشناخته → null. */
export function boardKeyOf(layoutKey: string): BoardColumnKey | null {
  return LAYOUT_KEYS[layoutKey] ?? null
}

const DEFAULT_ORDER: readonly BoardColumnKey[] = [
  'bale', 'serial', 'qty', 'fieldColor', 'borderColor', 'pattern', 'origin', 'material',
  'intake', 'finalLength', 'finalWidth', 'area', 'rate', 'usd',
]

/** ستون‌های پیش‌نمایش «اندازه‌گیری» (G.2، H.6): شماره · اندازهٔ اول · طول · عرض · مساحت. */
export const MEASURING_KEYS: readonly BoardColumnKey[] = ['serial', 'intake', 'finalLength', 'finalWidth', 'area']

function defaultLabel(key: BoardColumnKey, terms: Pick<AppTerms, 'bale'>): string {
  switch (key) {
    case 'bale':
      return terms.bale
    case 'serial':
      return 'شماره'
    case 'qty':
      return 'تعداد'
    case 'fieldColor':
      return 'رنگ متن'
    case 'borderColor':
      return 'رنگ حاشیه'
    case 'pattern':
      return 'نقشه'
    case 'origin':
      return 'محل بافت'
    case 'material':
      return 'مواد مصرفی'
    case 'intake':
      return 'اندازهٔ اول'
    case 'finalLength':
      return 'طول'
    case 'finalWidth':
      return 'عرض'
    case 'area':
      return 'مساحت'
    case 'rate':
      return 'ارزش متر مربع'
    case 'usd':
      return 'جمع کل دلار'
  }
}

const WIDTHS: Readonly<Record<BoardColumnKey, number>> = {
  bale: 7,
  serial: 13,
  qty: 6,
  fieldColor: 10,
  borderColor: 10,
  pattern: 10,
  origin: 10,
  material: 10,
  intake: 13,
  finalLength: 9,
  finalWidth: 9,
  area: 12,
  rate: 13,
  usd: 11,
}

function makeColumn(key: BoardColumnKey, label: string, width: number, simple: boolean, canRate: boolean): BoardColumn {
  const descriptive = DESCRIPTIVE_FIELD[key] !== undefined
  const edit: BoardColumn['edit'] =
    key === 'finalLength' || key === 'finalWidth' ? 'size' : descriptive ? 'text' : key === 'rate' && canRate ? 'rate' : null
  return {
    key,
    label,
    width,
    edit,
    // فقط‌خواندنی هرگز ایستگاه Tab نیست؛ ستون توصیفی در حالت ساده با لمس، دوبار کلیک یا F2 ویرایش می‌شود
    tabStop: edit === 'size' || (edit === 'text' && !simple),
  }
}

export interface ColumnOptions {
  terms: Pick<AppTerms, 'bale'>
  simple: boolean
  /** نرخ دستی (Business) */
  canRate: boolean
  /** پیش‌نمایش «اندازه‌گیری» */
  measuring?: boolean
}

/**
 * ستون‌های جدول: از چیدمان کارگاه اگر داده شده (ترتیب، پنهان، برچسب، پهنا)، وگرنه G.2. ستون‌های ناشناختهٔ چیدمان
 * نادیده‌اند؛ «طول» و «عرض» همیشه هستند (کار اصلی این صفحه)، و «شماره» همیشه اول نمای اندازه‌گیری است.
 */
export function boardColumns(layout: readonly GridColumn[] | null | undefined, options: ColumnOptions): BoardColumn[] {
  const { terms, simple, canRate } = options
  const picked: BoardColumn[] = []
  const seen = new Set<BoardColumnKey>()

  const known = (layout ?? []).filter((c) => boardKeyOf(c.key) !== null)
  if (known.length >= 3) {
    for (const column of known) {
      const key = boardKeyOf(column.key) as BoardColumnKey
      if (seen.has(key)) continue
      seen.add(key)
      const label = key === 'intake' && column.key !== 'rug.intakeSize' && column.key !== 'line.intakeSize'
        ? defaultLabel('intake', terms)
        : column.label?.trim() || defaultLabel(key, terms)
      picked.push(makeColumn(key, label, Math.max(6, Number(column.width) || WIDTHS[key]), simple, canRate))
    }
  } else {
    for (const key of DEFAULT_ORDER) {
      seen.add(key)
      picked.push(makeColumn(key, defaultLabel(key, terms), WIDTHS[key], simple, canRate))
    }
  }

  // اندازهٔ نهایی همیشه (چیدمانی که پنهانش کرده باشد این صفحه را بی‌کار می‌کند)
  for (const key of ['serial', 'finalLength', 'finalWidth'] as const) {
    if (!seen.has(key)) {
      const at = key === 'serial' ? 0 : picked.length
      picked.splice(at, 0, makeColumn(key, defaultLabel(key, terms), WIDTHS[key], simple, canRate))
      seen.add(key)
    }
  }

  if (!options.measuring) return picked
  const byKey = new Map(picked.map((c) => [c.key, c]))
  return MEASURING_KEYS.map((key) => byKey.get(key) ?? makeColumn(key, defaultLabel(key, terms), WIDTHS[key], simple, canRate))
}

/** متن خانهٔ یک ستون فقط‌خواندنی/نمایشی. */
export function cellText(column: BoardColumnKey, row: BaleRow, simple: boolean): string {
  switch (column) {
    case 'bale':
      return faDigitsOf(row.baleNumber)
    case 'serial':
      return serialText(row.serial)
    case 'qty':
      return faDigitsOf(row.qty)
    case 'intake':
      // در حالت ساده فقط وقتی با نهایی فرق دارد (A.3)
      return simple && !sizeDiffers(row) ? '' : sizeText(row.intakeLength, row.intakeWidth)
    case 'finalLength':
      return row.finalLength === null ? '' : faFixed(row.finalLength)
    case 'finalWidth':
      return row.finalWidth === null ? '' : faFixed(row.finalWidth)
    case 'area':
      return areaCellText(row)
    case 'rate':
      return row.usdPerM2 === null ? '' : faLoose(row.usdPerM2)
    case 'usd':
      return row.lineUsd === null ? '' : faFixed(row.lineUsd)
    default: {
      const member = DESCRIPTIVE_MEMBER[column]
      const value = member ? row[member] : null
      return typeof value === 'string' ? value : ''
    }
  }
}

/** مقدار خام یک ستون توصیفی. */
export function descriptiveValue(column: BoardColumnKey, row: BaleRow): string | null {
  const member = DESCRIPTIVE_MEMBER[column]
  const value = member ? row[member] : null
  return typeof value === 'string' && value.trim() ? value : null
}

/** نسخهٔ تازه‌ای از ردیف با یک مقدار توصیفی تازه. */
export function withDescriptive(row: BaleRow, column: BoardColumnKey, value: string | null): BaleRow {
  const member = DESCRIPTIVE_MEMBER[column]
  return member ? ({ ...row, [member]: value } as BaleRow) : row
}

/** فیلتر «نشانم بده»: شناسهٔ فرش‌های نشان‌دادنی، یا null یعنی همه. */
export function filterRows(rows: readonly BaleRow[], only: ReadonlySet<string> | null): BaleRow[] {
  return only ? rows.filter((r) => only.has(r.rugId)) : [...rows]
}

// ═══════════════════════════════════════════════════════════════════
// خواندن خانهٔ اندازه (همان قاعده‌های G.4 جدول ورود)
// ═══════════════════════════════════════════════════════════════════

export interface SizeChoice {
  label: string
  /** مقدار یک خانه (متر، رشتهٔ لاتین)؛ null یعنی «پس پذیرفته نیست» */
  value: string | null
  /** برای «208×210»: هر دو خانه */
  values?: [string, string] | null
}

export type SizeParse =
  | { status: 'empty' }
  | { status: 'ok'; value: string }
  | { status: 'pair'; values: [string, string] }
  | { status: 'ask'; message: string; choices: [SizeChoice, SizeChoice] }
  | { status: 'error'; message: string }

/**
 * متن یک خانهٔ طول/عرض: «۲٫۰۸»، «2,08»، «208» (پرسش سانت)، «25» (متر یا سانت؟)، و در خانهٔ طول «208×210» که هر دو را
 * پر می‌کند. پرسش‌ها همان پیام‌ها و دکمه‌های جدول ورودند (اول پیشنهاد اصلی، دوم «نه»).
 */
export function parseSizeText(text: string, allowPair: boolean, options: ParseDecimalOptions = {}): SizeParse {
  if (allowPair && looksLikeDimensionPair(text)) {
    const pair = parseDimensionPair(text, options)
    if (pair) {
      if (pair.status === 'ok') return { status: 'pair', values: pair.values }
      if (pair.status === 'error') return { status: 'error', message: message(pair.code, pair.tokens) }
      return {
        status: 'ask',
        message: message(pair.code, pair.tokens),
        choices: [
          { label: pair.choices[0].label, value: null, values: normalizeSizePair(pair.choices[0].values) },
          { label: pair.choices[1].label, value: null, values: normalizeSizePair(pair.choices[1].values) },
        ],
      }
    }
  }
  const parsed = parseDecimal(text, 'dim', options)
  switch (parsed.status) {
    case 'empty':
      return { status: 'empty' }
    case 'ok':
      return { status: 'ok', value: parsed.value }
    case 'error':
      return { status: 'error', message: message(parsed.code, parsed.tokens) }
    case 'ask':
      return {
        status: 'ask',
        message: message(parsed.code, parsed.tokens),
        choices: [
          { label: parsed.choices[0].label, value: parsed.choices[0].value },
          { label: parsed.choices[1].label, value: parsed.choices[1].value },
        ],
      }
  }
}

function normalizeSizePair(values: [string, string] | null): [string, string] | null {
  return values ? [toPlainString(values[0]), toPlainString(values[1])] : null
}

/** پیام «نگلهٔ ۴ هنوز فرشی ندارد…» یا جمع نگلهٔ جاری. */
export function currentSummaryText(bale: BaleFigures | null, current: number): string {
  return bale && bale.pieces > 0 ? totalsText(bale) : message('I_BOARD_CURRENT_EMPTY', { n: current })
}
