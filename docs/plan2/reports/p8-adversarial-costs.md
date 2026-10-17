# P8 adversarial review from the costs lane

2026-10-17. Read-only review requested by root after the P5 Release tests passed. No P8 implementation files were edited by this reviewer. Findings were sent to root for coordinated repair; status below records that handoff, not unverified completion.

## Findings

1. **P1 — displayed import corrections could be ignored at commit.** In ImportWizard, edits to row overrides, mapping, sheet, mode/policy or centimeter settings leave the previous preview and commit button active. Reproduction: preview; change a row length; click the main «وارد کن» button without clicking «اصلاح‌ها را بازبینی کن». Commit sends only the prior preview version, so the uncorrected stored preview is imported. Root confirmed and is implementing a dirty-preview gate plus regression coverage.

2. **P1 — undo can erase post-import financial and related edits.** The initial undo checks only Rug xmin, line-id membership, Open status and non-deleted cost-entry dependencies. Reproduction: import a declaration, save shipment revenue, then undo. Revenue changes Shipment and derived RugCostTotal, not Rug xmin; undo therefore deletes newly saved revenue. Editing line USD override/move/order similarly preserves the line id and Rug xmin. Uploading photos also writes RugImage without touching Rug xmin, so created-rug undo can delete new photo rows. Root confirmed and is implementing committed parent/child versions and dependency fingerprints (shipment, bales, lines, batch, images, steps, purchase, sale).

3. **P2 — numeric single serial with multiple pieces previews successfully but cannot commit.** LotParser initially checks range coverage only when IsRange. Input numeric serial `100`, quantity `2` passes the planner but Rug.SetSerialAndQuantity calls SerialNumber.Covers and rejects it at commit. Text serials can cover multiple pieces; numeric single serials cannot. Root notified to use the domain Covers predicate in preview.

4. **P2 — merged bale numbers bypass validation.** BaleResolver initially validates direct values as integral 1..9999 but casts merged inherited values directly. A merged `1.5` truncates to bale 1; a sufficiently large inherited numeric throws OverflowException and produces a 500 outside the workbook reader's exception wrapper. Root notified to apply the same integral/range predicate before casting inherited values.

5. **P2 — invalid explicit quantity silently becomes a default.** Nonblank mapped quantity text that fails numeric parsing is treated as absent and becomes 1 or the serial-range count. This can silently change piece counts. Root notified to return E_QTY_INVALID for nonblank unparseable values.

6. **P2 — cancelled/failed file creation can leave an orphan.** ImportService.UploadAsync calls files.SaveAsync before entering its cleanup try/catch. An I/O error/cancellation during file creation leaves an incomplete file but no ImportJob. Move SaveAsync inside cleanup coverage, or make ImportFileStorage.SaveAsync remove partial files on failure. Root notified.

## Checks with no finding

- Tenant ids are applied to job, mapping, rugs, line, cost-dependency and parent lookups; foreign job ids are resolved before filesystem access.
- Storage paths are built from server tenant/job GUIDs; client filenames never become a path.
- Commit takes the tenant lock before re-reading versions/status; double commit and serial races are handled transactionally by job xmin and the consistency pass.
- Workbook reads use cached XML values, do not execute formulas, reject macro/external workbook parts, prohibit DTDs/resolvers and bound archive size, expansion, row/column count.
- Existing-rug attachment checks serial/quantity, current membership and current Rug xmin before changing final size.
- No real database was accessed during this review.

## Repair re-review

The six findings above were re-read after root's changes. Source-level status: **all six addressed**.

- ImportWizard uses createPreviewGate over sheet/header/mapping/options/overrides, captures the submitted state before awaiting preview, accepts only that signature afterward, and checks previewCurrent in both the commit handler and disabled state. Changes made during an in-flight preview therefore remain dirty.
- Commit persists ImportRelatedState with Shipment/Batch xmin, complete Bale/ShipmentLine version dictionaries and deterministic scalar fingerprints for images/workflow steps/purchase lines/sales. Undo locks the parents and dependent rows before comparing them and before deleting; child additions must wait on parent FK locks. The new revenue/photo integration regression checks both blockers and preservation; the restored attached-rug regression verifies prior final-size restoration.
- LotParser rejects nonblank unparseable quantities and uses SerialNumber.Covers after validating the numeric range/integrality, avoiding overflow during cast.
- BaleResolver applies the direct-value integral/range check to merged inherited numbers, with fractional and extreme-value regressions.
- ImportFileStorage.SaveAsync owns partial-file cleanup in its catch, after disposing the failed file stream, while retaining CreateNew semantics.

Root reported that the final full integration run passed all importer tests. This reviewer separately verified the source changes; the costs lane did not claim a second execution of the importer integration suite.

Final closure: the reviewer read `work/test-results/full-integration.trx` after root's final serialized Release run: **387 executed, 387 passed, 0 failed, 0 skipped**. All six finding statuses are **closed**, supported by the repaired source, the preview-gate regressions (targeted run passed), parser regressions and importer integration tests included in the complete passing run.
