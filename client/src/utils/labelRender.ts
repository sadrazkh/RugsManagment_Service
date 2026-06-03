import type { Rug } from '@/types'
import { formatCurrency, statusLabel } from '@/utils/format'

export type LabelBlockKind = 'text' | 'field' | 'image' | 'divider' | 'row'

export interface LabelBlock {
  id: string
  kind: LabelBlockKind
  field?: string
  text?: string
  columns?: number
  children?: LabelBlock[]
  x?: number
  y?: number
  w?: number
  h?: number
  fontSize?: number
  align?: 'start' | 'center' | 'end'
}

export const LABEL_FIELDS = [
  { key: 'title', label: 'عنوان' },
  { key: 'sku', label: 'SKU' },
  { key: 'pattern', label: 'طرح' },
  { key: 'dimensions', label: 'ابعاد' },
  { key: 'currentStep', label: 'مرحله' },
  { key: 'totalInvestment', label: 'سرمایه' },
]

export function fieldValue(rug: Rug, key: string): string {
  const step = rug.workflowSteps.find((s) => s.status === 'InProgress')
  switch (key) {
    case 'title': return rug.title || 'فرش'
    case 'sku': return rug.sku
    case 'pattern': return rug.pattern || '—'
    case 'dimensions': return `${rug.widthMeters}×${rug.lengthMeters} م`
    case 'currentStep': return step?.stepNameFa ?? '—'
    case 'totalInvestment': return formatCurrency(rug.costs.totalInvestment)
    default: return ''
  }
}

function renderBlock(b: LabelBlock, rug: Rug): string {
  if (b.kind === 'divider') {
    return `<hr style="border:0;border-top:1px solid #6b0008;margin:6px 0" />`
  }
  if (b.kind === 'row' && b.children?.length) {
    const cols = b.columns ?? b.children.length
    const cells = b.children.map((c) => `<div style="flex:1;min-width:0;padding:2px">${renderBlock(c, rug)}</div>`).join('')
    return `<div style="display:flex;gap:4px;width:100%">${cells}</div>`
  }
  if (b.kind === 'field' && b.field) {
    return `<span style="font-size:${b.fontSize ?? 11}px">${fieldValue(rug, b.field)}</span>`
  }
  return `<span style="font-size:${b.fontSize ?? 11}px">${b.text ?? ''}</span>`
}

export function renderBlocksHtml(rug: Rug, blocks: LabelBlock[]): string {
  return blocks.map((b) => renderBlock(b, rug)).join('')
}

export const DEFAULT_LABEL_BLOCKS: LabelBlock[] = [
  { id: '1', kind: 'field', field: 'title', fontSize: 14 },
  { id: '2', kind: 'divider' },
  { id: '3', kind: 'row', columns: 2, children: [
    { id: '3a', kind: 'field', field: 'sku' },
    { id: '3b', kind: 'field', field: 'dimensions' },
  ]},
  { id: '4', kind: 'row', columns: 3, children: [
    { id: '4a', kind: 'field', field: 'currentStep' },
    { id: '4b', kind: 'field', field: 'pattern' },
    { id: '4c', kind: 'field', field: 'totalInvestment' },
  ]},
]
