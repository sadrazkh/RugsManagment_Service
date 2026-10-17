# Resumed implementation and integration, 2026-10-17

Work started from da29500 (feature/export-workflow) in an isolated worktree, codex/finish-export-workflow. Earlier work was preserved. P5, P6/P7, P4b/review/P9 and P8/integration were completed in coordinated lanes; .NET builds and migration generation were serialized.

## P8 implemented

- ImportJob/ImportMappingPreset, explicit enums, tenant-scoped repositories, M6 Import and xmin hand-edit documentation.
- Bounded upload/storage and OpenXML cached-value reader: 10 MB, 200 ZIP entries, 100 MB expanded, per-entry compression ratio 100, no macros/external links/DTD, server-generated GUID paths, cleanup of partial uploads.
- Header detection/mapping/presets, Jalali title/date, merged bale membership, adopted rows, lot quantities, known merged origin/material split, precise decimal snapping, row overrides, duplicate/overlap validation, reconciliation and suggested rates.
- Declaration and StockOnly previews/commit/undo; existing-serial Attach/Skip/Error, stale-version/double-commit conflicts, single ledger-locked transaction and same-request derived updates. Matching declared-value table rates remain table-driven rather than unnecessary overrides.
- Undo saves original final sizes and timestamps; it refuses changed rug versions, changed shipment/batch/bale/line versions, changed photos/workflow/purchase/sale fingerprints and dependent costs. Parent/child row locks protect the check-and-delete window. No user changes are silently erased.
- Import wizard/history, editable mapping/options/row errors, precise number inputs, reconciliation, commit and undo blockers. A captured preview-input signature prevents committing a stale preview or accepting a late response for newer edits.

## P8 evidence

- Synthetic sample preview and DB commit: 36 bales / 161 lines / 242 pieces / 464.74 m² / USD 20,322.50. All footer and summary totals reconcile.
- Imported DB shipment exports to an actual XLSX; reading that file again reproduces the same counts, area and USD with zero errors and matching footers, including the explicit quantity column.
- Real owner workbook read directly from its original desktop path, never copied or committed: same exact counts/totals, zero errors, all reconciliation rows OK, title date 2026-09-06. Issues: quantity-from-material x3, merged-split x3, range-quantity difference x1, adopted row x1, footer-label x1, missing-field x9, out-of-sequence x3; numeric noise snapped without monetary drift.
- Tests cover attach/undo restoring previous final sizes, StockOnly batch removal, duplicate commit, foreign-tenant 404s, Operator 403s, preserving later shipment revenue and newly attached photos, bad quantities/ranges/dimensions/merged bale numbers, ZIP bombs/macros/external links.
- The six findings in p8-adversarial-costs.md were fixed and re-reviewed. Frontend preview-signature tests cover both post-preview edits and edits made while preview is in flight.

## Migrations and real-data preparation

- Generated M4 CostLedger -> M5 Documents -> M6 Import -> M7 DropRugPurchaseCost in order. Applied M0-M3 were not changed.
- M7 removes the obsolete domain property/column, requires `[RequiresBackup("Rugs")]`, removes the M2 dimension backup table; Down backfills purchase amounts and restores the backup-table schema for older rollback paths. The external backup is required to recover pre-M2 fractional precision after M7.
- Full migration up/down/up tests and M7 backup-gate/column-backfill tests pass on disposable PostgreSQL databases.
- Original database inspected read-only: M3 Shipments, 6 RugCostTotals, total 344,937,269.00.
- Full custom-format backup created outside the repository: `work/backups/rugs_20261017_131516.dump`, 90,193 bytes; pg_restore list validation succeeds.
- Backup restored into a separate `rugs_restore_1017_final` database; M4-M7 applied successfully to that copy. Still 6 totals and 344,937,269.00 after M7. The original database has not been migrated at this checkpoint.
- Actual application `ILedgerVerifier` on the restored M7 replica: tenant checks 5 and 1 rugs respectively, both `Mismatches: []`.

## Combined checks at this checkpoint

- Release build with `-warnaserror -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -nr:false`: 0 warnings, 0 errors.
- Unit tests: 944 passed, 0 skipped. Architecture tests: 15 passed.
- P5: 11 pure tests (including 1,000 allocation trials), 4 PostgreSQL tests (D7 #1-12 and 200 random operations with zero mismatches). P6/P7: 9 PostgreSQL tests passed.
- Full integration final: **387/387 passed, zero skipped, 59 seconds**. Corrected the stale P1 DI assertion and optimized cost-source queries. The timing test runs in a nonparallel collection so it measures its own operation rather than unrelated migration/testhost load; the original <200 ms assertion remains unchanged. Actual five measurements: 178 / 194 / 238 / 183 / 192 ms; finalize/reopen 16/17 SQL commands.
- Final frontend: **554/554 passed** and production build successful.
- EF pending-model check clean; idempotent migration script has no xmin DDL.
- Final post-freeze Release build remains warning/error-free; WebSmoke/P4bWeb/P6w smoke filter **14/14** passed. Final axe audit **32/32** cases with zero violations and document overflow; all four required Lighthouse accessibility scores **100**. Durable screenshot/summary evidence is in `evidence/p9/`.
- Browser usability and accessibility evidence is recorded in p4b-review-p9-resumed.md. Human father acceptance and any unavailable Excel/LibreOffice UI checks must remain explicit, never inferred from automated tests.

No credentials, database dumps, owner workbook or generated large binary artifacts belong in Git.
