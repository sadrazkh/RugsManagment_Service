export interface DocumentColumn { key: string; label: string; width?: number; field?: string; expr?: string; format?: string; visibleWhen?: string; from?: string }
export interface FooterCell { id: string; col: string; span?: number; text?: string; expr?: string; agg?: string; of?: string }
export interface DocumentSheet { name: string; role: string; title?: { fromCol: string; toCol: string; expr: string; style?: string }; columns: DocumentColumn[]; group?: { by: string; mergeKey: boolean; keyColumn: string; skipEmpty: boolean; footer: FooterCell[]; footerStyle?: string } | null; [key: string]: unknown }
export interface DocumentLayout { schemaVersion: number; kind: string; lotRow: string; page: { paper: string; orientation: string; scale: number; fontSize: number; printTitleRows: number; [key: string]: unknown }; sheets: DocumentSheet[]; styles: Record<string, unknown> }
export interface DocumentTemplate { id: string; name: string; kind: number; isDefault: boolean; revision: number; usesMoneyFields: boolean; layout: DocumentLayout; version: number }
export interface RenderCell { col: number; colSpan: number; rowSpan: number; display: string; style: string; formula: string | null; isNumber: boolean }
export interface RenderRow { rowId: string; kind: string; group: number | null; cells: RenderCell[] }
export interface RenderSheet { name: string; role: string; rows: RenderRow[]; columns: { key: string; label: string; index: number }[] }
export interface RenderDocument { sheets: RenderSheet[]; warnings: { code: string; message: string; group?: number }[]; isDraft: boolean; truncated: boolean }
export interface ExportRun { id: string; at: string; user: string; template: string; revision: number; format: number; isDraft: boolean; dataHash: string; fileSha256: string | null }
export interface Revision { revision: number; savedAt: string; savedBy: string | null }
export interface LayoutIssue { path: string; message: string; code?: string }
export interface SetupStatus { ready: boolean; remaining: number; steps: { key: string; label: string; required: boolean; done: boolean; href: string; missing: string[] | null }[] }
