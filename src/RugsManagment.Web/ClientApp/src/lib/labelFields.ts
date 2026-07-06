/** فیلدهای قابل‌استفاده در برچسب + جایگزینی placeholderها با دادهٔ واقعی فرش. */
import { formatThousands } from '@/lib/money'
import type { Rug } from '@/lib/types'

export interface LabelField { key: string; label: string }

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
  { key: 'title', label: 'عنوان' },
  { key: 'origin', label: 'اصالت' },
  { key: 'pattern', label: 'طرح' },
  { key: 'material', label: 'جنس' },
  { key: 'knotDensity', label: 'رجشمار' },
  { key: 'dimensions', label: 'ابعاد' },
  { key: 'area', label: 'مساحت' },
  { key: 'purchaseCost', label: 'قیمت خرید' },
  { key: 'targetSalePrice', label: 'قیمت فروش' },
  { key: 'totalInvestment', label: 'هزینهٔ کل' },
  { key: 'currentStep', label: 'مرحلهٔ جاری' },
  { key: 'status', label: 'وضعیت' },
]

const STATUS: Record<number, string> = { 0: 'پیش‌نویس', 1: 'در جریان', 2: 'آمادهٔ فروش', 3: 'فروخته‌شده', 4: 'بایگانی' }

function metadata(rug: Rug): Record<string, string> {
  if (!rug.metadataJson) return {}
  try { return JSON.parse(rug.metadataJson) as Record<string, string> } catch { return {} }
}

/** مقدار یک فیلد برای یک فرش. کلیدهای ناشناخته از فیلدهای سفارشی (metadata) خوانده می‌شوند. */
export function resolveField(rug: Rug, key: string): string {
  const num = (v?: number) => (v == null ? '—' : formatThousands(v))
  switch (key) {
    case 'sku': return rug.sku
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
    case 'status': return STATUS[rug.status] ?? '—'
    default: return metadata(rug)[key] ?? ''
  }
}

/** جایگزینی {{key}} در یک متن با مقدار فیلدها (برای QR/بارکد و حالت HTML). */
export function fillPlaceholders(rug: Rug, text: string): string {
  return text.replace(/\{\{\s*([\w]+)\s*\}\}/g, (_, k: string) => resolveField(rug, k))
}
