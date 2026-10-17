import { computed, createSSRApp, effectScope, h, reactive, ref } from 'vue'
import { renderToString } from '@vue/server-renderer'
import { describe, expect, it } from 'vitest'
import type { ImportJob, ImportRow } from '@/lib/types/import'
import { importLabels } from '@/lib/messages/import'
import { createPreviewGate } from './previewGate'
import { problemRows, relatedRows, restoreReview, reviewRows, rowFieldValue, setRowSkipped, sortedSkippedRows, useSheetDecisions } from './importReview'
import ImportRowReview from './ImportRowReview.vue'

const row = (number: number, serial = String(number)): ImportRow => ({ sheetRow: number, action: 'create', bale: 2, values: { serial, length: '2', width: '3' }, issues: [] })
const problem = (number: number, serial = String(number)): ImportRow => ({ ...row(number, serial), action: 'error', issues: [{ code: 'E_DUPLICATE', severity: 'error', field: 'serial', message: 'شماره با ردیف دیگری تکراری است.', relatedRows: [7, 3, 7, number] }] })
describe('import decisions review', () => {
  it('discovers every blocker past both pagination boundaries without hiding empty serials', () => {
    const rows = Array.from({ length: 157 }, (_, index) => row(index + 2))
    rows[58] = problem(60); rows[129] = problem(131, '')
    expect(problemRows(rows, []).map(row => row.sheetRow)).toEqual([60, 131])
    expect(reviewRows(rows, 'issues', []).map(row => row.sheetRow)).toEqual([60, 131])
    expect(reviewRows(rows, 'all', [])).toHaveLength(157)
  })
  it('keeps explicit per-row decisions sorted, reversible and independent from edits', () => {
    let decisions = setRowSkipped([], 131, true)
    decisions = setRowSkipped(decisions, 60, true)
    decisions = setRowSkipped(decisions, 60, true)
    expect(decisions).toEqual([60, 131])
    expect(problemRows([problem(60), problem(131)], decisions)).toEqual([])
    decisions = setRowSkipped(decisions, 60, false)
    expect(problemRows([problem(60), problem(131)], decisions).map(row => row.sheetRow)).toEqual([60])
    expect(sortedSkippedRows([131, 60, 131])).toEqual([60, 131])
  })
  it('includes explicit and policy skips in the optional skipped filter, without unrelated rows', () => {
    expect(reviewRows([row(2), problem(60), { ...row(131), action: 'skip' }], 'skipped', [60]).map(row => row.sheetRow)).toEqual([60, 131])
  })
  it('invalidates commit immediately for skip, undo and in-flight decision changes until a fresh response', () => {
    const input = reactive({ skippedRows: [] as number[], overrides: {} })
    const gate = createPreviewGate(computed(() => ({ ...input, skippedRows: sortedSkippedRows(input.skippedRows) })))
    gate.accept(gate.capture()); expect(gate.current.value).toBe(true)
    input.skippedRows = setRowSkipped(input.skippedRows, 131, true)
    expect(gate.current.value).toBe(false)
    const sent = gate.capture()
    input.skippedRows = setRowSkipped(input.skippedRows, 60, true)
    gate.accept(sent); expect(gate.current.value).toBe(false)
    gate.accept(gate.capture()); expect(gate.current.value).toBe(true)
    input.skippedRows = setRowSkipped(input.skippedRows, 131, false)
    expect(gate.current.value).toBe(false)
  })
  it('preserves cleared text and null numeric corrections rather than restoring the rejected file value', () => {
    expect(rowFieldValue(row(2), { serial: '' }, 'serial')).toBe('')
    expect(rowFieldValue(row(2), { length: null }, 'length')).toBe('')
    expect(rowFieldValue(row(2), undefined, 'bale')).toBe(2)
  })
  it('normalizes exact related Excel row references without self references', () => {
    expect(relatedRows(problem(131))).toEqual([3, 7])
  })
  it('restores persisted sheet, decisions and corrections when reopening a multi-sheet import', () => {
    const job: ImportJob = { id: 'job', number: 1, fileName: 'test.xlsx', status: 1, titleText: null, titleDate: null, duplicateOf: null, createdAt: '', version: 1,
      sheets: ['First', 'Chosen'], headerRow: 1, mapping: { serial: 1 }, mode: 0,
      lastPreview: { sheet: 'Chosen', headerRow: 4, mapping: { serial: 3 }, options: { mode: 1, existingSerialPolicy: 2, convertCentimeters: true }, overrides: { 131: { serial: '', length: null } }, skippedRows: [131, 60, 131] } }
    const restored = restoreReview(job)
    expect(restored).toEqual({ ...job.lastPreview, skippedRows: [60, 131] })
    restored.overrides![131].serial = 'new'
    expect(job.lastPreview!.overrides![131].serial).toBe('')
    expect(restoreReview({ ...job, lastPreview: undefined }).sheet).toBe('First')
    expect(restoreReview({ ...job, lastPreview: undefined }).skippedRows).toEqual([])
  })
  it('never carries edits or skipped row numbers between sheets and invalidates an old response', () => {
    const scope = effectScope()
    scope.run(() => {
      const sheet = ref('A')
      const decisions = useSheetDecisions(sheet)
      decisions.overrides.value = { 2: { serial: 'fixed-A' } }; decisions.skippedRows.value = [2]
      const gate = createPreviewGate(computed(() => ({ sheet: sheet.value, overrides: decisions.overrides.value, skippedRows: decisions.skippedRows.value })))
      const sent = gate.capture(); gate.accept(sent); expect(gate.current.value).toBe(true)
      sheet.value = 'B'
      expect(decisions.overrides.value).toEqual({}); expect(decisions.skippedRows.value).toEqual([])
      expect(decisions.sheetNotice.value).toContain('پاک شدند')
      gate.accept(sent); expect(gate.current.value).toBe(false)
      sheet.value = 'A'
      expect(decisions.overrides.value).toEqual({}); expect(decisions.skippedRows.value).toEqual([])
      expect(gate.current.value).toBe(false)
      // Reopen hydration assigns the sheet first, then its persisted decisions.
      sheet.value = 'B'; decisions.overrides.value = { 131: { serial: '' } }; decisions.skippedRows.value = [131]
      expect(decisions.skippedRows.value).toEqual([131])
    })
    scope.stop()
  })
  it('renders blank serial, exact row, field reason, other serials and undo control accessibly', async () => {
    const render = (skipped: boolean) => renderToString(createSSRApp({ render: () => h(ImportRowReview, { row: problem(131, ''), labels: importLabels, skipped, busy: false, context: 'problem', allRows: [row(3, '3911 - 3960'), row(7, '3950')] }) }))
    const html = await render(false)
    expect(html).toContain('ردیف اکسل ۱۳۱')
    expect(html).toContain('شماره خالی است')
    expect(html).toContain('شماره با ردیف دیگری تکراری است.')
    expect(html).toContain('3911 - 3960')
    expect(html).toContain('3950')
    expect(html).toContain('این ردیف را کنار بگذار')
    expect(await render(true)).toContain('لغو کنار گذاشتن این ردیف')
  })
  it('explains a policy skip instead of offering an undo that cannot override the policy', async () => {
    const html = await renderToString(createSSRApp({ render: () => h(ImportRowReview, { row: { ...row(3), action: 'skip' }, labels: importLabels, skipped: false, busy: false, context: 'all', allRows: [] }) }))
    expect(html).toContain('طبق روش ورود کنار گذاشته شده')
    expect(html).not.toContain('این ردیف را کنار بگذار')
    expect(html).not.toContain('لغو کنار گذاشتن این ردیف')
  })
  it('shows file footer control rows as read-only rather than missing-serial rug editors', async () => {
    const html = await renderToString(createSSRApp({ render: () => h(ImportRowReview, { row: { ...row(170), values: {}, action: 'skip' }, labels: importLabels, skipped: false, busy: false, context: 'all', allRows: [] }) }))
    expect(html).toContain('ردیف کنترل جمع فایل')
    expect(html).not.toContain('شماره خالی است')
    expect(html).not.toContain('این ردیف را کنار بگذار')
    expect(html).not.toContain('<input')
  })
  it('keeps control IDs unique when a row appears in both the problems panel and optional list', async () => {
    const props = { row: problem(131), labels: importLabels, skipped: false, busy: false, allRows: [] }
    const html = await renderToString(createSSRApp({ render: () => h('div', [h(ImportRowReview, { ...props, context: 'problem' }), h(ImportRowReview, { ...props, context: 'all' })]) }))
    const ids = Array.from(html.matchAll(/\sid="([^"]+)"/g), match => match[1])
    expect(ids.length).toBeGreaterThan(2)
    expect(new Set(ids).size).toBe(ids.length)
    expect(ids).toContain('problem-row-131')
    expect(ids).toContain('all-row-131')
  })
})
