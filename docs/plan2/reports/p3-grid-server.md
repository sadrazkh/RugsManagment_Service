# p3-grid-server: report (resumed run 3)

The p3-grid-server package is complete, and all 15 P3 integration tests pass on the current combined working tree, which now includes P4a's M3 migration. Earlier runs had already written every file this package owns. This run checked that work, made one comment-only fix and re-ran the tests.

## Already done before this run
- Every owned file was written on 2026-10-04 and nothing in P3's scope has changed since. There are no TODO or NotImplemented markers and nothing is half-written.
- I re-read the service, DTOs, controllers, views, modules, nav item and settings link against G.1–G.7, H.1, I.2/I.5, K.2/P3, E6 and E7.
- The server DTOs match the TypeScript mirror `CA/lib/types/grid.ts` (owned by p3-grid-ui) member for member.

## Done in this run
- **`src/RugsManagment.Application/Features/Intake/RugGridService.cs`:** the comment for restoring a deleted rug (E6) sat above `IntakePageAsync`, which had two summaries. I moved it onto `RestoreAsync`. No code changed.
- **Re-verification:** I copied the whole working tree to `docs/plan2/work\p3-grid-server`, tested there, and deleted the copy afterwards.
- **Local patch in the copy only:** I removed a duplicate `var quantityChanged` that P2-review-fix's unfinished edit left at `RugFieldWriter.cs:216`. Without that, the solution does not compile; the line is identical to line 193, so behaviour is the same.

## Verification
| Check | Result |
|---|---|
| Release build with `-warnaserror` | Domain, Application, Infrastructure, Web, ArchitectureTests and IntegrationTests: 0 warnings, 0 errors |
| P3 integration tests | 15/15 pass, again after the comment fix |
| Architecture tests | 15/15 pass |
| Full integration suite | 326/330 pass; the 4 failures are outside P3 (below) |
| Unit tests | 763/764 pass. P4a's unfinished `tests/RugsManagment.Tests/P4a` folder was left out of the copy because it does not compile. The 1 failure is P6-engine's `ExpressionTests.Limits_1000Characters_AndDepth32` |
| Databases | `rugs_management` was never touched. My test databases were dropped; the others still there belong to other packages |
| Git | No state-changing command was run |

The 4 integration failures:
1. `XlsxGoldenTests.LotFree_...` belongs to P6-engine, which is still in progress.
2. `HomeStock_UsesTheLotRuleInSql...` expects 3 pieces / 5.20 m² but gets 4 / 14.20. This is a P2 review finding P2-review-fix is still working on.
3. `DiGraph_ResolvesEveryPlatformService...` expects `EmptyHomeShipmentSource`, but P4a now registers the real `HomeShipmentSource` (E3).
4. `P4aWebTests.Lines_AreFloor_...` fails because the workspace page has no `data-readiness=` yet. This is P4a-web, still in progress.

## Acceptance (server side)
Each bullet below is covered by a passing P3 test:
- D.7 #1–#3 typed in the grid give 40,116,600 / 15,300,000 / 73,174,800.
- After «3985 - 4003» the next serial is 4004. In Auto mode a 50-piece lot gets «4004 - 4053».
- An old Total purchase line shows as «کل». Two lines of one category show a read-only «چند سطر» cell. Clearing a cell deletes its line.
- Two sellers on one day make two batches. Changing one row's seller moves only that row. Yesterday's batch reopens.
- Two tabs editing different fields merge; the same field gives a conflict. Two concurrent purchase edits give exactly one conflict.
- **Operator rules:**
  - An Operator gets no money keys, and an injected money key returns 403 for the whole request.
  - He can delete only his own money-free rug from today; any other delete fails for that row only.
  - He can restore his own deleted rug (E6).
- **Settings and errors:**
  - A stale grid-layout save returns 409.
  - One bad row does not stop the others, and a serial overlap returns the next free number.
  - Sending the same row twice creates one rug.
  - Other tenants' rugs are invisible.

Keyboard entry, paste, cm/metre questions, offline drafts, card mode and the timed walkthrough belong to p3-grid-ui and p3-integrate.

## Not done
- **Browser walkthrough:** belongs to p3-integrate.
- **Unit tests:** none added, because the brief gives this package only the P3 integration-test folder.
- **Clean full-suite run on the combined tree:** waits for the other packages to finish.

## Needs from others
1. **`RugBatchDto` needs seller and number (spec I.2).** No package in this wave owns `src/RugsManagment.Application/DTOs/Batches/BatchDtos.cs`. Two edits go together:
   - Append `string? SellerName = null, int? Number = null` as trailing optional members of `RugBatchDto`.
   - Then add `batch.SellerName, batch.Number` as the last two arguments in `RugBatchService.ToDto`.

   Until then, `/api/batches/today` and `/recent` already return `IntakeBatchDto`, which has these fields plus day, pieces and area.
2. **P2-review-fix:** delete the inner `var quantityChanged = quantity != rug.Quantity;` around `RugFieldWriter.cs:216`. It duplicates line 193 and fails to compile (CS0136).
3. **P4a:**
   - `tests/RugsManagment.Tests/P4a/*` does not compile: `EntityChange` is missing, and `P4aChanges.Rug(decimal, decimal)` is used as a type.
   - The `DiGraph_...` test must now expect `HomeShipmentSource`.
4. **P6-engine:**
   - `tests/RugsManagment.Tests/Documents/LayoutValidatorTests.cs:15` has a `Main(JsonObject)` method; its warning (CS0028) becomes an error under `-warnaserror`.
   - `ExpressionTests.Limits_...` and `XlsxGoldenTests.LotFree_...` still fail.
5. **p3-integrate:** re-run the whole suite once these settle, then do the browser walkthrough.

## Files
- **Modified this run:** `C:\Users\sadra\source\repos\RugsManagment_Service\src\RugsManagment.Application\Features\Intake\RugGridService.cs` (comment only).
- **Created or modified in earlier runs, checked this run:** the full list is in the report file.
- **Deleted:** nothing in the repo; only the throwaway work folder.

`docs/plan2/reports\p3-grid-server-contract.md` is an empty, unused file left by an earlier attempt.

Report: `docs/plan2/reports\p3-grid-server.md`