/**
 * فیلدهای قابل‌استفاده در برچسب + جایگزینی placeholderها با دادهٔ واقعی فرش.
 *
 * فیلدهای پولی (`money: true`) برای کسی که Business نیست از سرور null می‌آیند و «—» چاپ می‌شوند؛
 * طراح برچسب می‌تواند با همین پرچم آن‌ها را جدا نشان دهد. برچسب وضعیت از `lib/labels` است.
 */
import { rugStatusLabel } from '@/lib/labels'
import { formatThousands } from '@/lib/money'
import type { Rug } from '@/lib/types'

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

export const STATIC_FIELDS: LabelField[] = [
  { key: 'sku', label: 'کد (SKU)' },
  { key: 'url', label: 'لینک صفحهٔ فرش' },
  { key: 'title', label: 'عنوان' },
  { key: 'origin', label: 'اصالت' },
  { key: 'pattern', label: 'طرح' },
  { key: 'material', label: 'جنس' },
  { key: 'knotDensity', label: 'رجشمار' },
  { key: 'dimensions', label: 'ابعاد' },
  { key: 'area', label: 'مساحت' },
  { key: 'purchaseCost', label: 'قیمت خرید', money: true },
  { key: 'targetSalePrice', label: 'قیمت فروش', money: true },
  { key: 'totalInvestment', label: 'هزینهٔ کل', money: true },
  { key: 'currentStep', label: 'مرحلهٔ جاری' },
  { key: 'status', label: 'وضعیت' },
]

function metadata(rug: Rug): Record<string, string> {
  if (!rug.metadataJson) return {}
  try { return JSON.parse(rug.metadataJson) as Record<string, string> } catch { return {} }
}

/** مقدار یک فیلد برای یک فرش. کلیدهای ناشناخته از فیلدهای سفارشی (metadata) خوانده می‌شوند. */
export function resolveField(rug: Rug, key: string): string {
  // مبلغ‌ها برای غیر Business از سرور null می‌آیند
  const num = (v?: number | null) => (v == null ? '—' : formatThousands(v))
  switch (key) {
    case 'sku': return rug.sku
    // آدرس کامل، چون QR ممکن است با گوشی‌ای اسکن شود که این صفحه را باز نکرده است
    case 'url': return `${window.location.origin}/Rugs/Details/${rug.id}`
    case 'title': return rug.title || '—'
    case 'origin': return rug.origin || '—'
    case 'pattern': return rug.pattern || '—'
    case 'material': return rug.material || '—'
    case 'knotDensity': return rug.knotDensity != null ? String(rug.knotDensity) : '—'
    case 'dimensions': return `${rug.widthMeters}×${rug.lengthMeters} م`
    case 'area': return `${rug.areaSquareMeters} م²`
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
export function fillPlaceholders(rug: Rug, text: string): string {
  return text.replace(/\{\{\s*([\w]+)\s*\}\}/g, (_, k: string) => resolveField(rug, k))
}
