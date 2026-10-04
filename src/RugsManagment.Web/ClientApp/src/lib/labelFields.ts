/**
 * فیلدهای قابل‌استفاده در برچسب + جایگزینی placeholderها با دادهٔ واقعی فرش.
 *
 * فیلدهای پولی (`money: true`) برای کسی که Business نیست از سرور null می‌آیند و «—» چاپ می‌شوند؛
 * طراح برچسب می‌تواند با همین پرچم آن‌ها را جدا نشان دهد. برچسب وضعیت از `lib/labels` است.
 *
 * از فاز ۲: «شماره»ی بازرگان (`serial`، برای «برچسب شماره» با QR همان شماره، H.8)، تعداد دسته، اندازهٔ اول و
 * نهایی، متراژ کل و ورودی/فروشنده. اندازه‌ها به ترتیب جدول‌ها «طول × عرض» و با ارقام فارسی نوشته می‌شوند؛ فقط
 * `serial` و `sku` (که در QR و بارکد هم می‌روند) با ارقام لاتین می‌مانند تا اسکنر همان را بخواند که در سرور است.
 */
import { faDecimal, toPersianDigits } from '@/lib/format'
import { rugStatusLabel } from '@/lib/labels'
import { formatThousands } from '@/lib/money'
import type { Rug as LegacyRug } from '@/lib/types'
import type { Rug as RugV2 } from '@/lib/types/rugs'

export interface LabelField {
  key: string
  label: string
  /** مبلغ تومانی (فقط Business) */
  money?: boolean
}

export type ElementType = 'heading' | 'text' | 'field' | 'divider' | 'qr' | 'barcode' | 'image' | 'table'

export interface LabelElement {
  id: string
  type: ElementType
  colSpan: number
  text?: string
  field?: string
  prefix?: boolean
  source?: string
  src?: string
  fields?: string[]
  fontSize?: number
  bold?: boolean
  align?: 'right' | 'center' | 'left'
}

export interface LabelLayout {
  columns: number
  elements: LabelElement[]
}

/**
 * فرشی که برچسب از آن پر می‌شود: شکل قدیمی (نمونه‌های طراح و چاپ) و عضوهای فاز ۲ اگر آمده باشند. عضو نیامده «—»
 * چاپ می‌شود، نه خطا.
 */
export type LabelRug = LegacyRug &
  Partial<
    Pick<
      RugV2,
      | 'serialNo'
      | 'quantity'
      | 'intakeLength'
      | 'intakeWidth'
      | 'finalLength'
      | 'finalWidth'
      | 'totalAreaM2'
      | 'sellerName'
      | 'batchNumber'
    >
  >

export const STATIC_FIELDS: LabelField[] = [
  { key: 'serial', label: 'شماره' },
  { key: 'quantity', label: 'تعداد (تخته)' },
  { key: 'sku', label: 'کد (SKU)' },
  { key: 'url', label: 'لینک صفحهٔ فرش' },
  { key: 'title', label: 'عنوان' },
  { key: 'origin', label: 'محل بافت' },
  { key: 'pattern', label: 'نقشه' },
  { key: 'material', label: 'مواد مصرفی' },
  { key: 'knotDensity', label: 'رجشمار' },
  { key: 'dimensions', label: 'ابعاد' },
  { key: 'intakeSize', label: 'اندازهٔ اول' },
  { key: 'finalSize', label: 'اندازهٔ نهایی' },
  { key: 'area', label: 'مساحت' },
  { key: 'totalArea', label: 'متراژ کل' },
  { key: 'batch', label: 'ورودی' },
  { key: 'seller', label: 'فروشنده' },
  { key: 'purchaseCost', label: 'قیمت خرید', money: true },
  { key: 'targetSalePrice', label: 'قیمت فروش', money: true },
  { key: 'totalInvestment', label: 'هزینهٔ کل', money: true },
  { key: 'currentStep', label: 'مرحلهٔ جاری' },
  { key: 'status', label: 'وضعیت' },
]

function metadata(rug: LabelRug): Record<string, string> {
  if (!rug.metadataJson) return {}
  try { return JSON.parse(rug.metadataJson) as Record<string, string> } catch { return {} }
}

/** «۲٫۰۸ × ۲٫۱۰ م» (طول × عرض)، یا «—» اگر یکی نیست. */
function sizeText(length: number | null | undefined, width: number | null | undefined): string {
  if (length == null || width == null) return '—'
  return `${faDecimal(length, 2, 2)} × ${faDecimal(width, 2, 2)} م`
}

/** مقدار یک فیلد برای یک فرش. کلیدهای ناشناخته از فیلدهای سفارشی (metadata) خوانده می‌شوند. */
export function resolveField(rug: LabelRug, key: string): string {
  // مبلغ‌ها برای غیر Business از سرور null می‌آیند
  const num = (v?: number | null) => (v == null ? '—' : formatThousands(v))
  switch (key) {
    // شماره و کد با ارقام لاتین: همین متن در QR/بارکد می‌رود و اسکنر باید دقیقاً همان را بخواند
    case 'serial': return rug.serialNo ?? '—'
    case 'quantity': return rug.quantity != null ? toPersianDigits(rug.quantity) : '—'
    case 'sku': return rug.sku
    // آدرس کامل، چون QR ممکن است با گوشی‌ای اسکن شود که این صفحه را باز نکرده است
    case 'url': return `${window.location.origin}/Rugs/Details/${rug.id}`
    case 'title': return rug.title || '—'
    case 'origin': return rug.origin || '—'
    case 'pattern': return rug.pattern || '—'
    case 'material': return rug.material || '—'
    case 'knotDensity': return rug.knotDensity != null ? String(rug.knotDensity) : '—'
    case 'dimensions': return `${rug.widthMeters}×${rug.lengthMeters} م`
    case 'intakeSize': return sizeText(rug.intakeLength, rug.intakeWidth)
    case 'finalSize': return sizeText(rug.finalLength, rug.finalWidth)
    case 'area': return `${rug.areaSquareMeters} م²`
    case 'totalArea': return rug.totalAreaM2 != null ? `${faDecimal(rug.totalAreaM2, 2, 2)} م²` : '—'
    case 'batch': return rug.batchName || '—'
    case 'seller': return rug.sellerName || '—'
    case 'purchaseCost': return num(rug.purchaseCost)
    case 'targetSalePrice': return num(rug.targetSalePrice)
    case 'totalInvestment': return num(rug.costs?.totalInvestment)
    case 'currentStep': return rug.workflowSteps.find((s) => s.status === 1)?.stepNameFa ?? '—'
    // برچسب چاپی همیشه برچسب کامل وضعیت است (مستقل از حالت ساده/کامل بیننده)
    case 'status': return rugStatusLabel(rug.status, false)
    default: return metadata(rug)[key] ?? ''
  }
}

/** جایگزینی {{key}} در یک متن با مقدار فیلدها (برای QR/بارکد و حالت HTML). */
export function fillPlaceholders(rug: LabelRug, text: string): string {
  return text.replace(/\{\{\s*([\w]+)\s*\}\}/g, (_, k: string) => resolveField(rug, k))
}
