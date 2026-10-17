export interface ImportJob {
  id: string; number: number; fileName: string; status: number; sheets: string[]; headerRow: number | null
  mapping: Record<string, number> | null; mode: number; titleText: string | null; titleDate: string | null
  duplicateOf: { id: string; number: number } | null; createdAt: string; version: number
}
export interface ImportIssue { code: string; severity: string; message: string; field: string | null }
export interface ImportRow { sheetRow: number; action: string; bale: number | null; values: Record<string, string | null>; issues: ImportIssue[] }
export interface ReconciliationRow {
  bale: number | null; pieces: number; footerPieces: number | null; summaryPieces: number | null
  area: number; footerArea: number | null; summaryArea: number | null; usd: number; footerUsd: number | null; summaryUsd: number | null; ok: boolean
}
export interface ImportPreview {
  counts: { lines: number; pieces: number; bales: number; areaM2: number; usdTotal: number; errors: number; warnings: number; attach: number; skip: number }
  reconciliation: ReconciliationRow[]; rows: ImportRow[]; issuesByCode: { code: string; severity: string; message: string; count: number }[]
  suggestedRates: Record<string, number>; version: number
}
export interface ImportResult { createdRugs: number; attachedRugs: number; shipmentId: string | null; shipmentNumber: number | null; batchId: string | null; skippedRows: number }
export interface ImportUndo { ok: boolean; blockers: string[] }
