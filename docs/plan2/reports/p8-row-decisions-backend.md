# Import problem-row decisions — backend

2026-10-18. Scope: DTOs, pure planner, Persian import messages and planner unit tests. Service wiring, API integration and frontend are owned by root/other agents.

## Changes

- PreviewImportRequest has trailing optional SkippedRows; ImportIssueDto has trailing optional RelatedRows. Existing positional contracts remain compatible.
- ImportPlanner.Plan accepts trailing optional skippedRows. Explicitly skipped data rows retain their mapped raw values plus manual overrides, action `skip`, Excel SheetRow and the information message I_ROW_SKIPPED. They have no error blockers, duplicate participation, totals or suggested rates. Clearing the skip selection reparses the row and restores applicable errors.
- Duplicate and overlap issues appear once per issue code per row. RelatedRows lists every other directly conflicting Excel row, sorted and unique. Three equal duplicates have all two other row references on every side. Skipping the middle row of an overlap chain correctly removes only those conflicts.
- Missing or invalid serial rows remain visible when dimension cells contain text, including invalid dimension strings; prior detection required a parseable numeric dimension and silently hid such rows.
- Added the Persian I_ROW_SKIPPED explanation so discarding a row is explicit in the preview.

## Verification

`dotnet test tests/RugsManagment.Tests --filter FullyQualifiedName~P8.ImportPlannerTests -c Release -warnaserror -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false --no-restore`

**18 passed, 0 failed, 0 skipped; no build warnings** (69ms test duration).

Regression coverage: skip duplicate clears surviving error; unskip restores blockers; skip invalid row preserves raw/manual identity without errors; missing/invalid serial plus invalid dimensions remains visible; three-way duplicate exact references; direct-only overlap references and skip re-evaluation; skipped rows excluded from rates/totals; structural/unknown skip ids do not manufacture data rows.

## Audit / integration handoff

- Structural title/header/footer/summary branches precede skipping, so skippedRows cannot convert them to committed data. Invalid row ids simply match no data row.
- Extra override keys are retained only as row data; downstream commit reads fixed field names rather than dynamic entity property names.
- Root was notified of one narrow pre-existing classification edge: missing-serial rows with only quantity/origin/material values and both dimensions blank remain Unknown and filtered out. Extending candidate identity fields can expose those rows too; no change was made during root's final build window.
- Root owns passing request.SkippedRows into the planner, UI row decisions, and API/commit regression tests. No git mutations or real database access occurred in this backend task.
