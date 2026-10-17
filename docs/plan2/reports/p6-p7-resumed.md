# P6 wiring and P7 resumed implementation

Worktree: `codex/finish-export-workflow`, 2026-10-17. P6/P7 automated acceptance is green.

## Implemented

- Domain document templates, immutable revisions and export runs; explicit spec `DocumentKind` values (declaration 0, cost 1, rug list 2).
- M5 `20261017045100_Documents` creates the three document tables, tenant/default/seed indexes, revision cascade and shipment/template SET NULL relationships, including the declaration preference FK. System xmin declaration removed and documented in `Migrations/HandEdits/Documents.md`. No production database touched.
- Tenant-scoped document/template repositories, full DB-to-engine mapping, immutable finalized snapshot reader with hash verification, and current derived costs for the monetary cost report. Finalized declaration dimensions, rates, terms and exporter details come from its snapshot. Rug-list locations are derived from memberships, shipment status and sales.
- Shared export service uses the existing composer and ClosedXML writer for preview, print and xlsx, supplying the tenant's document field catalogue. Template revision, data SHA256 and actual file SHA256 are recorded. Money templates and money-template history are hidden/forbidden to Operators. Every foreign identifier is validated before creating an ExportRun or file response.
- Template reads/writes, JSON-path validation, expression validation/sample formulas, revisions, restore, reset, duplicate, delete and transactional set-default with xmin concurrency. Template writes and completed exports are audited.
- Export/print/rug-list download routes, heavy rate limiting, antiforgery for rug-list downloads, RFC5987 Persian draft filename; shipment Output tab, template picker, preview, readiness links and history; rug-list bulk Excel action.
- Layout-free print view: repeated table header, one tbody per bale, planned page breaks, yellow footer cells, summary on its own last page, Persian digits, template styles/page margins, external nonce-tagged print script.
- Embedded Business-only cost template with per-line money totals (lots are not multiplied twice).
- Idempotent export preset: missing custom fields and purchase/expense categories, matching existing seeded purchase categories; declared sample rates only with confirmation; actual label renderer schema (serial 18pt / QR, 50x30mm); default templates/preferences and grid layouts through GridLayoutService. All preset icons exist in the existing sprite.
- Server-computed nine-step setup checklist (required steps 1–6), missing origin rate names, home progress card, settings links and setup UI.
- Working designer UI with field picker, column label/width/visibility, drag AND keyboard button reordering, expression validation/sample formula, footer anchors, summary editor, page settings, immutable revisions drawer, live preview, optimistic save/409 feedback and advanced layout/style editing.
- CSV writer preserves existing instance API and treats negative numeric amounts as numbers while neutralizing formula-like text.

## Files

New files are under Domain `Entities/Document*`, `ExportRun`, `Enums/DocumentKind`, `ExportFormat`; Application `DTOs/Documents`, `Features/Documents` services and cost default, persistence interfaces, `Features/Platform/SetupStatusService`, `Seed/DocumentTemplateSeedContributor`; Infrastructure document/setup repositories/configuration/module and M5; Web document/preset controllers, settings pages/links, Output tab/home card, print views/assets; ClientApp document/designer components and islands, types/messages and rug export action. Existing engine algorithms were left to the review agent, who owns their fixes.

## Verification

- Infrastructure and Web builds succeeded during M5 scaffolding. Temporary configuration exclusions were restored. M5 was inspected to contain only document changes and its system xmin DDL was removed.
- `npx vue-tsc --noEmit`: passed before final backend-only fixes.
- Frontend production build and complete Vitest suite are run centrally by the orchestrator (initial sandbox run failed at native Vite child-process launch, not TypeScript). The orchestrator reported production build + 550/550 Vitest passed before the final summary/footer UI enhancements; the subsequent local `vue-tsc --noEmit` also passed.
- New integration coverage: `P6w/DocumentWorkflowTests` (DB values/lots/hashes/draft/print, foreign IDs/no run, Operator policy, finalized snapshot hash), `P7/DesignerSetupTests` (preset twice/checklist/missing rates, revision/stale409/reset/default, invalid expression/cycle/noqty).
- New pure P7 regression tests cover per-line cost sums on lots and footer-to-summary links after moving a footer anchor. Existing default validation now expects the new cost template to use money fields.
- Release pure `Documents` + `P7` filter: **140/140 passed**.
- Final Release P6w/P7 integration run: **9/9 passed, 0 skipped**, using disposable PostgreSQL databases, after the orchestrator's full Release build (0 warnings/errors). Exact command: `dotnet test tests/RugsManagment.IntegrationTests --no-build -c Release --filter 'FullyQualifiedName~P6w|FullyQualifiedName~P7' -m:1 -nr:false -p:NuGetAudit=false` with `RUGS_TEST_PG` set. The only failure in the initial run was the new test expecting HTTP200 from the existing preferences PUT; the correct HTTP204 expectation was fixed.
- Added the missing document readiness contributor for `W_NO_USD_RATE` and per-column `W_TEMPLATE_FIELD_EMPTY`; Output correctly consumes the readiness array and supplies the selected template ID.
- Fresh tenants can use the exact default declaration before the preset: two optional default colour keys have catalogue fallbacks only when no tenant definition exists. An explicit inactive/non-exportable field definition is never bypassed.
- Summary editing now includes named columns, footer-reference selection, totals and equals-row controls; footer editing includes mutually exclusive text/expression/count/sum modes, span and key-anchored destination. JSON is an optional advanced control.

## Remaining manual acceptance

The user's real spreadsheet/real database were not changed. Excel/LibreOffice print-preview checks and the timed merchant usability walk require an interactive human application/session and remain explicitly manual; this report does not claim they ran. The previous engine golden suite already covers the complete sample and exact workbook XML; the new tests exercise its database wiring separately. Full integration results will be appended below.

## Acceptance evidence

| Requirement | Result / evidence |
|---|---|
| Fresh tenant can download and print default declaration before preset | PASS, `FreshTenant_DefaultDeclarationWorksBeforePreset` |
| DB lot uses per-piece values, qty50 / area8 / USD360 | PASS, `XlsxAndPreview_UseDatabaseData_RecordHashes_AndDraftFilename` |
| Actual xlsx SHA256, data SHA256, revision, rug count, draft RFC5987 filename recorded | PASS, same test verifies bytes against persisted hash |
| Foreign shipment/template/rug ->404 before creating ExportRun | PASS, `ForeignIds_Return404_AndNeverCreateRun` |
| Money template forbidden to Operator; hidden from list; Configure writes blocked | PASS, `Operator_CannotSeeOrExportMoneyTemplates_OrWriteDesigner` |
| Finalized source uses snapshot and matching snapshot hash | PASS, `FinalizedExports_UseSnapshotAndItsHash` |
| Preset twice adds no categories/fields/labels; purchase seeds reported existing | PASS, `Preset_IsIdempotent_LeavesExistingLabels_AndSetupListsMissingRate` |
| Checklist required1–6 ready from server state; missing origin named; every icon exists | PASS, same test |
| Designer samples produce27000 and workbook retains live `ROUND(L3*100, 0)` | PASS, `ComputedColumn_HasRealSamples_LiveXlsxFormula_AndTemplatePreflight` |
| Template column preflight emits Persian empty-field warning | PASS, same test |
| Revisions increment, stale save409, restore/reset/default | PASS, `Designer_SaveRevision_Stale409_Restore_Reset_AndDefault` |
| Invalid expression/cycle/missingqty rejected with Persian path errors | PASS, `InvalidExpression_Cycle_AndMissingLotQty_ReturnPersianJsonPaths` |
| Cost template never multiplies per-line lot money twice | PASS, pure `CostTemplate_AmountsArePerLine_AndDoNotMultiplyLotsAgain` |
| Footer move preserves summary formula link by ID | PASS, pure `MovingFooterAnchor_KeepsSummaryFormulaBoundToItsId` |
| Original full golden sample / 3816 decimal / bale boundaries / workbook metadata | PASS in existing engine tests (part of140 pure tests); full161-line DB/browser round trip was not repeated in this lane |
| Excel + LibreOffice UI / timed father walk / final print pagination visual | MANUAL, not claimed |

## Cross-review of P5

Read-only review covered CostEntryService planning, write locks and client-key retries; CostMembership tenant/active membership SQL; cost repository detail/list/pin/target filters; live per-piece rounding and frozen totals in CostAllocator; CostCategoryGuard; and ProfitService. No new confirmed defect was found. The internal allocations-of-rug reader returns an empty list for a foreign ID, but no exposed route using it without prior rug validation was found, so it was not reported as a vulnerability.

## Integration needs

No shared AppDbContext or global Application/Web DI edits are required; registration uses the pre-existing Documents modules and Web discovery conventions. M5 is complete. The orchestrator owns final combined build/tests, source transfer/commit/PR and any production migration/backup procedure. No remaining code work is delegated by this lane.
