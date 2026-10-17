import type { ImportJob, ImportPreviewInput, ImportRow } from '@/lib/types/import'
import { ref, watch, type Ref } from 'vue'

export type ReviewFilter = 'issues' | 'all' | 'skipped'
export const isProblem = (row: ImportRow) => row.action === 'error' || row.issues.some(issue => issue.severity === 'error')
export const sortedSkippedRows = (rows: readonly number[]) => [...new Set(rows)].sort((a, b) => a - b)
export const problemRows = (rows: readonly ImportRow[], skipped: readonly number[]) => rows.filter(row => isProblem(row) && !skipped.includes(row.sheetRow))
export const reviewRows = (rows: readonly ImportRow[], filter: ReviewFilter, skipped: readonly number[]) => rows.filter(row => filter === 'all' || (filter === 'skipped'
  ? skipped.includes(row.sheetRow) || row.action === 'skip'
  : !skipped.includes(row.sheetRow) && row.issues.some(issue => issue.severity === 'error' || issue.severity === 'warning')))
export const relatedRows = (row: ImportRow) => sortedSkippedRows(row.issues.flatMap(issue => issue.relatedRows ?? []).filter(number => number !== row.sheetRow))
export function setRowSkipped(rows: readonly number[], row: number, skip: boolean): number[] {
  return sortedSkippedRows(skip ? [...rows, row] : rows.filter(number => number !== row))
}
export function rowFieldValue(row: ImportRow, overrides: Record<string, string | null> | undefined, field: string) {
  if (overrides && Object.hasOwn(overrides, field)) return overrides[field] ?? ''
  return (field === 'bale' ? row.bale : row.values[field]) ?? ''
}
export function restoreReview(job: ImportJob): ImportPreviewInput {
  const saved = job.lastPreview
  return { sheet: saved?.sheet ?? job.sheets[0] ?? '', headerRow: saved ? saved.headerRow : job.headerRow,
    mapping: { ...(saved?.mapping ?? job.mapping) }, options: saved ? { ...saved.options } : { mode: job.mode, existingSerialPolicy: 0, convertCentimeters: false },
    overrides: structuredClone(saved?.overrides ?? {}), skippedRows: sortedSkippedRows(saved?.skippedRows ?? []) }
}
/** شمارهٔ ردیف فقط داخل همان برگه معنا دارد؛ تصمیم قبلی هرگز روی برگهٔ دیگر اعمال نشود. */
export function useSheetDecisions(sheet: Ref<string>) {
  const overrides = ref<Record<number, Record<string, string | null>>>({})
  const skippedRows = ref<number[]>([])
  const sheetNotice = ref('')
  watch(sheet, (_current, previous) => {
    overrides.value = {}; skippedRows.value = []
    if (previous) sheetNotice.value = 'برگه عوض شد؛ اصلاح‌ها و کنارگذاری‌های برگهٔ قبلی پاک شدند. برگهٔ تازه را بازبینی کنید.'
  }, { flush: 'sync' })
  return { overrides, skippedRows, sheetNotice }
}
