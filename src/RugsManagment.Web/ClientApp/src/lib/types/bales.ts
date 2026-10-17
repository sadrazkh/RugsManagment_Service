/**
 * نوع‌های صفحهٔ نگله‌بندی از فاز ۴b — props جزیره‌های `baling-board` و `pack-mode` (I.5)، رویدادهای میان جزیره‌های
 * کارگاه محموله، و نوع‌های کمکی کارهای تخته (انتقال، اندازه، «برگرد»). DTOهای خودِ محموله و نگله در `shipments.ts`
 * (فاز ۴a) است؛ این فایل آن‌ها را تکرار نمی‌کند و فقط import می‌کند.
 *
 * چرا جدا: `shipments.ts` آینهٔ DTOهای سرور است و فاز ۴a صاحبش است؛ این‌ها قرارداد خودِ فرانت‌اند (هیچ‌کدام در C# نیست).
 */
import type { BaleSummary, ShipmentStatus, ShipmentTotals } from './shipments'

// ── props جزیره‌ها (I.5) ───────────────────────────────────────────────

/** نمای تخته: جدول (مثل اکسل، با پانویس هر نگله) یا کارت‌ها (تبلت). */
export type BoardView = 'table' | 'cards'

/**
 * props جزیرهٔ `baling-board` (زبانهٔ «نگله‌ها»). چهار عضو اول همان I.5 است؛ بقیه اختیاری‌اند و اگر سرور نفرستد از
 * app-context یا پیش‌فرض پر می‌شوند.
 */
export interface BalingBoardProps {
  shipmentId: string
  view?: BoardView
  /** متراژ هدف هر نگله (محموله ?? ترجیح کارگاه)؛ null یعنی بی‌هدف */
  targetAreaM2?: number | null
  /** بازهٔ مجاز اطراف هدف به درصد */
  tolerancePercent?: number
  /** محموله قطعی است: همه‌چیز فقط‌خواندنی */
  locked?: boolean
  /** شمارهٔ محموله (برای پیام‌ها)؛ نبودنش فقط پیام را کوتاه‌تر می‌کند */
  shipmentNumber?: number | null
  /** نشانی حالت اسکن؛ پیش‌فرض `/Shipments/{id}/Pack` */
  packUrl?: string | null
}

/** props جزیرهٔ `pack-mode` (`/Shipments/{id}/Pack`، گوشی). */
export interface PackModeProps {
  shipmentId: string
  /** نگله‌ای که «جاری» شود (از `?bale=`)؛ null یعنی همان جاریِ ذخیره‌شده */
  baleId?: string | null
  locked?: boolean
  targetAreaM2?: number | null
  tolerancePercent?: number
  shipmentNumber?: number | null
  /** بازگشت به زبانهٔ نگله‌ها؛ پیش‌فرض `/Shipments/{id}?tab=bales` */
  backUrl?: string | null
}

// ── کارهای تخته ────────────────────────────────────────────────────────

/** مقصد «انتقال»: نگله‌ای با شماره (شمارهٔ تازه همان نگله را می‌سازد)، انبار، یا جابه‌جایی در همان نگله. */
export type MoveTarget =
  | { kind: 'bale'; number: number }
  | { kind: 'stock' }
  | { kind: 'up' }
  | { kind: 'down' }

/** اندازهٔ نهایی‌ای که ثبت می‌شود: طول و عرض، «همان اندازهٔ اول»، یا پاک کردن (برگشت به اندازهٔ اول). */
export type FinalSizeInput =
  | { kind: 'pair'; length: number; width: number }
  | { kind: 'same' }
  | { kind: 'clear' }

/** وضعیت نوار هدف (C.2): بی‌هدف، کمتر، در بازه، بیشتر. */
export type TargetState = 'none' | 'below' | 'within' | 'above'

/** یک فرش که همین نوبت اضافه شد (حالت اسکن: «آخرین ۵ فرش»، هر کدام با «برگردان»). */
export interface RecentAdd {
  lineId: string
  rugId: string
  serial: string | null
  baleNumber: number
  at: number
}

/** کاری که برای قطع اینترنت فرستاده نشد و با «الان دوباره بفرست» دوباره می‌رود (G.5، G.7؛ بی فرستادن خودکار). */
export interface PendingAdd {
  clientOpId: string
  serial: string | null
  rugId: string | null
  baleNumber: number
  at: number
}

// ── رویدادهای میان جزیره‌ها (هر جزیره اپ Vue جداست؛ رویداد روی window) ──

/** هر بار که نگله‌ها یا وضعیت محموله عوض شد (تخته، سربرگ، حالت اسکن). */
export const SHIPMENT_CHANGED_EVENT = 'rugs:shipment-changed'

export interface ShipmentChangedDetail {
  shipmentId: string
  /** کدام جزیره فرستاد (تا خودش دوباره واکنش نشان ندهد) */
  source: string
  totals?: ShipmentTotals | null
  bales?: BaleSummary[] | null
  /** وضعیت تازه (سربرگ پس از «قطعی کن» / «باز کردن دوباره» می‌فرستد) */
  status?: ShipmentStatus | null
}

/** فرمانی که جزیرهٔ دیگری (سربرگ، با تراشه‌های آمادگی) به تختهٔ نگله‌بندی می‌دهد. */
export const BALES_COMMAND_EVENT = 'rugs:bales-command'

export type BalesCommand =
  /** فقط این فرش‌ها را نشان بده («نشانم بده») */
  | { command: 'show'; rugIds: string[] }
  /** «بقیه همان اندازهٔ اول» برای این فرش‌ها (پس از تأیید) */
  | { command: 'same-as-intake'; rugIds: string[] }
  /** فوکوس در جعبهٔ شماره («افزودن فرش») */
  | { command: 'focus-serial' }
  /** حذف نگلهٔ خالی */
  | { command: 'delete-bale'; baleId: string }

export type BalesCommandDetail = BalesCommand & { shipmentId: string }
