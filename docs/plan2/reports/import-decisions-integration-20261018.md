# Import decisions integration — 2026-10-18

The previous preview disabled commit for two errors but buried those rows in an optional, paginated list. Blank-serial rows were filtered away and the user could not explicitly omit a bad source row.

## Delivered behavior

- An automatic, unpaginated problems panel shows the Excel row number, serial (including blank), bale, field and reason. Duplicate/overlap messages include exact related Excel rows and their identities.
- Users can correct the row inline, explicitly skip it or undo a skip. Decisions require a fresh server preview before commit. Explicitly skipped rows are excluded from duplicate checks, totals, rate suggestions and persistence.
- The preview/recheck action stays accessible outside collapsed mapping and row sections; disabled commit explains the blocker. Warnings are separate and nonblocking. Reconciliation compares included-row totals against actual footer/summary numbers.
- The selected sheet, options, manual corrections and skips are saved in existing OptionsJson, exposed as LastPreview and restored/replanned on reopen. Uploads retain a query-id URL for refresh. No schema migration is needed.
- Sheet changes clear previous-sheet decisions and cards with a visible notice; stale in-flight responses are discarded. Missing-serial candidate rows are exposed even when only quantity or other input fields are populated.
- Footer/control rows and policy-based skips have distinct read-only explanations. Only actual writable rug inputs are editable; derived control totals remain in the comparison table.

## Verification

- Release solution build with warnings as errors: zero warnings/errors.
- Backend P8 API/reader/storage suite: **16/16**, no skips. API test covers exact related rows, blocked commit, explicit skip, persisted/reopened decisions, fresh preview/version conflict, successful commit of 160 remaining sample rows with one skipped, plus policy-skip reconciliation.
- Pure P8 planner suite: **20/20**. Includes three-way duplicates, direct overlap references, skipped invalid/missing rows, quantity-only missing identity, unskip and original sample behavior.
- Full frontend suite: **566/566**; import helper/SSR/gating suite **14/14**. Strict typecheck/production build pass, including the final removal of derived-total editors.
- CUA live browser against isolated `rugs_import_ui_1018` on localhost5191: synthetic 160-row workbook has the same serial at Excel rows70 and151. Both problem cards appear immediately without opening the all-rows list; exact mutual references are shown.
- Explicit skip151 + recheck: zero blockers,159 included rows,318m²,USD14,310; commit enabled. History reopen and actual reload retain the decision. Undo skip + recheck restores both blockers. Correct serial1151 + recheck:160 included rows,320m²,USD14,400,zero warnings. Actual browser commit succeeds with160 newly created rugs.
- Browser proof images are outside the repository in the task outputs directory. Only synthetic data was imported; the owner's database and input file were not modified by these tests.

The earlier completion report remains historical. This follow-up changes no migration or money formula. Detailed frontend and planner evidence is in `import-problems-ux-20261018.md` and `p8-row-decisions-backend.md`.
