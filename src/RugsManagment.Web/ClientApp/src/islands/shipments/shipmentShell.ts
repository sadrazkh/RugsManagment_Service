/**
 * منطق خالص دو جزیرهٔ قاب محموله (فاز ۴b): فهرست محموله‌ها (`shipment-list`) و سربرگ کارگاه محموله (`shipment-header`).
 * این‌جا هیچ DOM و درخواستی نیست تا در vitest (محیط node) آزموده شود؛ جزیره‌ها فقط صدا می‌زنند.
 *
 * متن‌ها: متن‌های همین دو جزیره (پرسش «قطعی کن»، «باز کردن دوباره»، دکمه‌های تراشه‌های آمادگی) واژه به واژه از جدول H.3 و
 * H.3b‌اند و هر «نگله» با `{bale}`/`{bales}`/`{baleOf}` نوشته شده تا کارگاهی که «عدل» می‌گوید همه‌جا «عدل» ببیند
 * (`fillMessage` واژه‌ها را از app-context می‌گذارد). پیام‌هایی که سرور هم دارد (قفل، «اول این‌ها را درست کنید») از
 * `lib/messages` خوانده می‌شوند، نه این‌جا.
 *
 * رویدادها: سربرگ و صفحهٔ نگله‌بندی (`baling-board`) دو جزیرهٔ جدا روی یک صفحه‌اند؛ با رویدادهای `window` هم‌دیگر را تازه
 * می‌کنند (`SHIPMENT_CHANGED_EVENT`) و سربرگ کار دکمه‌های تراشه را به تخته می‌سپارد (`BALES_COMMAND_EVENT`).
 */
import { fillMessage, formatMessageNumber, type MessageTokens } from '@/lib/messages'
import {
  ReadinessCode,
  ShipmentStatus,
  type Bale,
  type LineOp,
  type ReadinessIssue,
  type ShipmentListItem,
} from '@/lib/types/shipments'

// ── رویدادهای میان جزیره‌ها ───────────────────────────────────────────────

// همان نام‌ها و شکل‌های `lib/types/bales.ts` (صفحهٔ نگله‌بندی)؛ این‌جا تکرار شده‌اند تا این دو جزیره به فایلِ بستهٔ دیگر
// وابستهٔ کامپایل نباشند. اگر یکی عوض شود، آزمون این فایل رشته‌ها را می‌سنجد.

/**
 * چیزی در محموله عوض شد (افزودن، انتقال، اندازه، حذف نگله، «قطعی»): `detail = { shipmentId, source, … }`. هر جزیرهٔ همان
 * محموله (سربرگ، صفحهٔ نگله‌بندی) داده‌اش را دوباره می‌خواند؛ `source` جزیره‌ای است که فرستاد تا خودش دوباره نخواند.
 */
export const SHIPMENT_CHANGED_EVENT = 'rugs:shipment-changed'

/**
 * فرمان سربرگ به صفحهٔ نگله‌بندی (وقتی همان صفحه باز است): «[نشانم بده]» (`show`)، «[افزودن فرش]» (`focus-serial`)،
 * «[بقیه همان اندازهٔ اول]» (`same-as-intake`؛ تخته خودش می‌پرسد و «برگرد» دارد) و «[حذف کن]» نگلهٔ خالی (`delete-bale`).
 */
export const BALES_COMMAND_EVENT = 'rugs:bales-command'

export interface ShipmentChangedDetail {
  shipmentId: string
  source: string
}

export type BalesCommand =
  | { command: 'show'; rugIds: string[] }
  | { command: 'same-as-intake'; rugIds: string[] }
  | { command: 'focus-serial' }
  | { command: 'delete-bale'; baleId: string }

export type BalesCommandDetail = BalesCommand & { shipmentId: string }

/** فرمان یک دکمهٔ تراشه برای صفحهٔ نگله‌بندی؛ null برای کاری که فقط خودِ سربرگ می‌کند («نرخ بگذارید»). */
export function balesCommand(issue: ReadinessIssue, action: ReadinessAction): BalesCommand | null {
  switch (action) {
    case 'addRug':
      return { command: 'focus-serial' }
    case 'showMe':
      return { command: 'show', rugIds: [...issue.rugIds] }
    case 'sameSize':
      return { command: 'same-as-intake', rugIds: [...issue.rugIds] }
    case 'deleteBale':
      return issue.baleId ? { command: 'delete-bale', baleId: issue.baleId } : null
    default:
      return null
  }
}

// ── متن‌ها ──────────────────────────────────────────────────────────────────

/** متن‌های دو جزیره (H.3، H.3b)؛ بیرون از نشانه‌های `{…}` هیچ حرف لاتینی ندارند (آزمون می‌سنجد). */
export const shellCopy = {
  draft: 'پیش‌نویس',
  final: 'قطعی',
  newShipment: 'محمولهٔ تازه',
  emptyBusiness: 'هنوز محموله‌ای نساخته‌اید. وقتی فرش‌ها آمادهٔ بسته‌بندی شدند یک محموله بسازید.',
  emptyOperator: 'هنوز محمولهٔ بازی نیست؛ از بازرگان بخواهید یکی بسازد.',
  emptyFiltered: 'محموله‌ای با این وضعیت نیست.',
  readyToFinalize: 'آمادهٔ قطعی شدن',
  blockingLeft: '{n} کار مانده تا قطعی شدن',
  shipmentName: 'محمولهٔ {number}',
  piecesWithRows: '{pieces} تخته ({rows} ردیف)',
  pieces: '{pieces} تخته',
  rowsSuffix: '({rows} ردیف)',
  listLine: '{bales} {bale} · {pieces} · {area} متر · {usd} دلار',
  finalize: 'قطعی کن',
  reopen: 'باز کردن دوباره',
  finalizeConfirmTitle: 'محمولهٔ {number} قطعی شود؟',
  finalizeConfirm:
    '{pieces} تخته «صادر شده» می‌شوند و اندازه‌ها، {bales} و ارزش گمرکی قفل می‌شوند. هزینهٔ تازه هنوز قابل ثبت است.',
  finalizeYes: 'قطعی کن',
  finalizeNo: 'فعلاً نه',
  finalized: 'محمولهٔ {number} قطعی شد.',
  reopenConfirmTitle: 'محمولهٔ {number} دوباره باز شود؟',
  reopenConfirm: 'تا دوباره قطعی نکنید، خروجی‌ها «پیش‌نویس» چاپ می‌شوند.',
  reopenYes: 'باز کن',
  reopened: 'محمولهٔ {number} دوباره باز شد.',
  lateCost: 'این محموله قطعی شده؛ هزینهٔ تازه فقط سود را تغییر می‌دهد، نه اظهار را.',
  deleteConfirmTitle: 'محمولهٔ {number} حذف شود؟',
  deleteConfirm: 'این محموله فرشی ندارد؛ {bales} خالی‌اش هم حذف می‌شوند. هزینه‌ای که به آن وصل بوده «تقسیم‌نشده» می‌شود.',
  deleteYes: 'حذف کن',
  deleteLabel: 'حذف محمولهٔ {number}',
  deleted: 'محمولهٔ {number} حذف شد.',
  allShipments: 'همهٔ محموله‌ها',
  actionAddRug: 'افزودن فرش',
  actionDeleteBale: 'حذف کن',
  actionShowMe: 'نشانم بده',
  actionSameSize: 'بقیه همان اندازهٔ اول',
  actionSetRate: 'نرخ بگذارید',
  sameSizeConfirmTitle: 'اندازهٔ نهایی {n} فرش همان اندازهٔ اول ثبت شود؟',
  sameSizeYes: 'ثبت کن',
  no: 'نه',
  sameSizeDone: 'اندازهٔ نهایی {n} فرش همان اندازهٔ اول ثبت شد.',
  sameSizePartial: 'اندازهٔ نهایی {done} فرش ثبت شد؛ {failed} فرش ماند. صفحه را تازه کنید و دوباره بزنید.',
  baleDeleted: '{baleOf} {n} حذف شد.',
  rateFormLabel: 'ارزش گمرکی «{key}» برای هر متر',
  rateSubmit: 'ثبت',
  more: 'بیشتر',
  edit: 'ویرایش',
  editTitle: 'مشخصات محمولهٔ {number}',
  save: 'ذخیره',
  cancel: 'انصراف',
  saved: 'مشخصات محموله ذخیره شد.',
  lockedFieldsHint: 'محموله قطعی است؛ فقط یادداشت و متراژ هدف {bale} عوض می‌شود.',
  loadFailed: 'محموله بارگذاری نشد.',
  retry: 'دوباره',
  back: 'بازگشت به محموله‌ها',
  readinessLabel: 'آمادگی برای قطعی شدن',
} as const

export type ShellCopyKey = keyof typeof shellCopy

/** متن پرشدهٔ یک کلید (عدد با ارقام فارسی و «٬»/«٫»؛ واژه‌های نگله از app-context). */
export function copy(key: ShellCopyKey, tokens?: MessageTokens): string {
  return fillMessage(shellCopy[key], tokens)
}

/** عدد برای نمایش: همان شکل `Messages.FormatNumber` سرور (۱۳٫۵۱، ۱۰٬۰۰۰)، تا متن پیش و پس از سوار شدن جزیره یکی باشد. */
export function num(value: number | null | undefined): string {
  return value === null || value === undefined || !Number.isFinite(value) ? '—' : formatMessageNumber(value)
}

/** «محمولهٔ ۸». */
export function shipmentName(number: number): string {
  return copy('shipmentName', { number })
}

/** «۵۲ تخته (۳ ردیف)» با دسته، وگرنه «۴ تخته» (واژه‌نامهٔ A.2). */
export function piecesText(pieces: number, rows: number): string {
  return rows !== pieces ? copy('piecesWithRows', { pieces, rows }) : copy('pieces', { pieces })
}

/** خط جمع یک کارت فهرست: «۲ نگله · ۵۲ تخته (۳ ردیف) · ۱۸٫۲۵ متر · ۸۵۹٫۸ دلار». */
export function listLine(item: Pick<ShipmentListItem, 'bales' | 'pieces' | 'lines' | 'areaM2' | 'usdTotal'>): string {
  return fillMessage(shellCopy.listLine, {
    bales: num(item.bales),
    pieces: piecesText(item.pieces, item.lines),
    area: num(item.areaM2),
    usd: num(item.usdTotal),
  })
}

// ── فهرست ───────────────────────────────────────────────────────────────────

/** فیلتر صفحه (`open`/`finalized`) → مقدار `?status=` API؛ هر چیز دیگر یعنی همه. */
export function statusQuery(status: string | null | undefined): string {
  if (status === 'open') return `?status=${ShipmentStatus.Open}`
  if (status === 'finalized') return `?status=${ShipmentStatus.Finalized}`
  return ''
}

/** فقط محمولهٔ باز و بی‌فرش حذف می‌شود (C.1)؛ و فقط Business. */
export function canDeleteShipment(item: Pick<ShipmentListItem, 'status' | 'lines'>, canBusiness: boolean): boolean {
  return canBusiness && item.status === ShipmentStatus.Open && item.lines === 0
}

// ── آمادگی و «قطعی کن» ─────────────────────────────────────────────────────

/** کمبودهای بازدارنده (E_…). */
export function blockingIssues(issues: readonly ReadinessIssue[]): ReadinessIssue[] {
  return issues.filter((i) => i.blocking)
}

/** بازدارنده‌ها اول، بعد هشدارها؛ ترتیب سرور در هر گروه می‌ماند. */
export function orderIssues(issues: readonly ReadinessIssue[]): ReadinessIssue[] {
  return [...issues.filter((i) => i.blocking), ...issues.filter((i) => !i.blocking)]
}

/** «آمادهٔ قطعی شدن»: فرش دارد و هیچ بازدارنده‌ای نیست (هشدار مانع نیست). */
export function isReady(lines: number, issues: readonly ReadinessIssue[]): boolean {
  return lines > 0 && blockingIssues(issues).length === 0
}

/** کار هر دکمهٔ تراشه (H.3b). */
export type ReadinessAction = 'addRug' | 'deleteBale' | 'showMe' | 'sameSize' | 'setRate'

/**
 * دکمه‌های یک تراشه برای این بیننده: «[افزودن فرش]»، «[حذف کن]» (نگلهٔ خالی)، «[نشانم بده]»، «[بقیه همان اندازهٔ اول]» و
 * «[نرخ بگذارید]» (فقط Business، چون افزودن نرخ Business است). محمولهٔ قطعی هیچ دکمه‌ای ندارد.
 */
export function readinessActions(issue: ReadinessIssue, canBusiness: boolean, locked: boolean): ReadinessAction[] {
  if (locked) return []
  switch (issue.code) {
    case ReadinessCode.EmptyShipment:
      return ['addRug']
    case ReadinessCode.EmptyBale:
      return issue.baleId ? ['deleteBale'] : []
    case ReadinessCode.NoFinalSize:
      return issue.rugIds.length > 0 ? ['showMe', 'sameSize'] : []
    case ReadinessCode.NoDeclaredValue:
      return canBusiness && issue.key ? ['setRate'] : []
    case ReadinessCode.NoSerial:
      return issue.rugIds.length > 0 ? ['showMe'] : []
    default:
      return []
  }
}

/** برچسب دکمهٔ یک کار تراشه. */
export function actionLabel(action: ReadinessAction): string {
  switch (action) {
    case 'addRug':
      return copy('actionAddRug')
    case 'deleteBale':
      return copy('actionDeleteBale')
    case 'showMe':
      return copy('actionShowMe')
    case 'sameSize':
      return copy('actionSameSize')
    case 'setRate':
      return copy('actionSetRate')
  }
}

/**
 * «اندازهٔ نهایی ۱۲ فرش همان اندازهٔ اول» (C.4): برای هر فرشِ تراشه که هنوز اندازهٔ نهایی ندارد یک عمل `measure` با
 * `sameAsIntake` و نسخهٔ همان ردیف (از `GET /bales`). فرشی که در نگله‌ها نیست یا حالا اندازه دارد کنار می‌رود.
 */
export function sameSizeOps(bales: readonly Bale[], rugIds: readonly string[]): LineOp[] {
  const wanted = new Set(rugIds)
  const ops: LineOp[] = []
  for (const bale of bales) {
    for (const row of bale.rows) {
      if (!wanted.has(row.rugId) || (row.finalLength !== null && row.finalWidth !== null)) continue
      wanted.delete(row.rugId)
      ops.push({ op: 'measure', clientOpId: `same-${row.rugId}`, rugId: row.rugId, rugVersion: row.rugVersion, sameAsIntake: true })
    }
  }
  return ops
}

/** نسخهٔ نگلهٔ خالیِ یک تراشه (برای `DELETE /api/bales/{id}?version=`)؛ null اگر نگله دیگر نیست. */
export function baleVersion(bales: readonly { id: string; version: number }[], baleId: string | null): number | null {
  if (!baleId) return null
  return bales.find((b) => b.id === baleId)?.version ?? null
}
