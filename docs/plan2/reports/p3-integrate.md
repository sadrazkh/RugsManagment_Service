# p3-integrate: report

P3 works end to end on the real server and Postgres, and every K.2/P3 acceptance bullet passed in the browser on the combined working tree. While walking them through I found six UI defects. I fixed all six in P3-lane files and added 7 vitest tests. The one failing test left on the final tree belongs to another lane.

The run used a Release build of the combined tree (all five parallel packages as they stood at 14:00). I ran it on a throwaway database, `rugs_p3_int`, seeded by the demo seeder. `rugs_management` was never touched.

## Glue
Nothing was missing:
- **Services:** `AddIntakeModule()` registers both the Application and Infrastructure parts.
- **Navigation:** the «ورود فرش» menu item and the home tile both open `/Intake`.
- **Settings:** the «ستون‌های جدول» tile opens `/settings/grid`.
- **Stock page:** `/Rugs` has the «جدول/فهرست» toggle (`?view=grid`), and «ثبت فرش تازه» now goes to `/Intake`.
- **Islands:** the intake grid, rugs grid and column editor all load.

## Fixes made this run
1. **Keyboard focus fell out of the grid.**
   - **When:** after Ctrl+Home or PageUp to a row outside the drawn area. It also happened now and then while typing rows 11–12, when the grid starts to scroll; the next row's first Tab was then lost.
   - **Fix:** two changes in `useVirtualRows.ts` (new `renderedIndices()`) and `GridCell.vue` (a cell that appears already active takes focus).
   - **Re-checked:** after the fix, PageUp ×6, Ctrl+End, Ctrl+Home and PageDown ×4 left no keystroke landing outside the grid.
2. **«جمع خرید» hid its result.**
   - **Before:** in 16 of 21 cells the total was cut off, e.g. «۱۰٬۰۰۰٬۰۰۰ × ۴٫۳۷…» with no «= ۴۴٬۵۷۴٬۰۰۰». Next to the «چند سطر» chip, «۳۵٬۰۰۰٬۰۰۰» showed as «٬۰۰۰٬۰۰۰», which reads as a smaller number.
   - **Fix:** `GridCell.vue` now cuts the start of the sum so the result stays visible. In `RugGridColumns.cs` the default widths changed: price column 18→21, «جمع خرید» 18→22, «متراژ» 14→16.
3. **A suggestion could replace what was typed.** Enter or Tab picked the first suggestion that starts with the typed text, so «گبه» could silently become «گبه پشم» (both exist in the father's sheet). An exact match now wins (`suggestionIndex()` in `gridParse.ts`).
4. **A 401/403 caused an endless resave loop.**
   - **What happened:** another agent's login on `localhost` replaced my session. The grid then re-sent every 1.5 s forever and stacked six identical «نشست شما تمام شده» notices.
   - **Fix:** a request-level 401/403 no longer retries on its own. The rows stay unsaved and in drafts, and the next edit or «الان دوباره بفرست» retries. An identical notice now replaces the previous one instead of stacking (`useGridSave.ts`, `useGridController.ts`).
5. **Focus after banner and notice buttons.** After «برگردان», «نوشتهٔ من بماند / او را ببینم» or «بله», focus now goes back to the grid instead of the page body (`DataGrid.vue`).
6. **Stale screen-reader heading.** The hidden page heading said «ورودی امروز» even after switching days. It is now just «ورود فرش» (`Views/Intake/Index.cshtml`).

## Verification (final combined tree)
| Check | Result |
|---|---|
| Release build with `-warnaserror` (from scratch) | 0 warnings, 0 errors |
| Unit tests | 909/909 |
| Architecture tests | 15/15 |
| Integration tests | 351/352; all 15 P3 tests pass. The one failure is P4a's DI test (needs from others #2). |
| `npm run build` | passes |
| vitest | 29 files, 467 tests (7 new) |

## Acceptance (all pass, in the browser on the real server)
- **20 rows keyboard only:** typed and saved. Tab, Enter and the Tab-run return behave like Excel.
- **Paste:** 50 TSV rows from the sample (3783–3832, Persian digits). The header was skipped and only one cm question was asked («۲۰ اندازه…»). All 50 saved; every size matches the sample and the total area is 214.51.
- **Serials:** after «3985 - 4003» the suggestion is 4004. A row given only qty 50 gets «4004 - 4053». Seller, origin and material carry down.
- **D.7 #1–#3 typed in the grid:** the server's purchase costs are 40,116,600 / 15,300,000 / 73,174,800.
- **Purchase cells:** an old rug's single purchase line shows «کل ۴۵٬۰۰۰٬۰۰۰». Two lines show a read-only «چند سطر». Clearing the cell deletes the line.
- **Sellers and days:** two sellers on one day make two batches. Changing one row's seller moves only that row. Yesterday's batch reopens from its chip.
- **Offline:** 5 rows typed offline came back as drafts after a reload. «پاک کردن» asks first, with «نه» focused, and «برگردان» on its toast brings the rows back. They saved once (one request, unique ClientRowIds, no duplicates).
- **Two tabs:** different fields merge silently; the same field shows the conflict choice. Two concurrent purchase-line saves give [200, 409].
- **Operator:** no purchase columns or values reach him, and an injected money key returns 403 for the whole request. He can delete his own rug from today with no money and restore it. Deleting anyone else's rug is refused for that row only.
- **Cm/metre questions:**
  - «208» asks to convert to 2.08 m.
  - «25» and «30» ask metres or cm, with cm first.
  - «12» is 12 m silently.
  - «208×210» fills both cells.
  - «۱۰م» shows «ده میلیون تومان».
- **Stale layout save:** returns 409, through the API and in the settings page.
- **Card mode at 375 px:** no horizontal scroll, 44 px inputs, and Enter moves field by field within the card.

## Timed walkthrough
This replaces the session with the father, which an agent cannot run: 10 rugs including the lot «3911 - 3960», on an empty day, keyboard only.
- **Result:** all 10 rows saved (59 pieces, 47.98 m²) with no errors and no questions.
- **Effort:** 216 keystrokes (94 key presses plus 122 typed characters). My own run took 55 s; I estimate about 3 minutes for a non-typist over 60.

Friction worth an owner decision:
- **Seven Tabs per row.** Getting from serial to length takes 7 Tabs, 63 of the 94 key presses. The column settings already allow fixing this per workshop.
- **Serial jumps after an out-of-order number.** Typing a serial below the workshop's highest (3901) makes the next suggestion the highest + 1 (4061), not 3902. This follows spec G.4.
- **Paste picks up the last price.** Pasted rows silently get the last typed purchase price and broker percent, which creates purchase lines.
- **Esc picks metres.** In «۲۵ متر؟ یا ۲۵ سانت؟», Esc chooses metres.
- **The undo toast lasts 8 s.** That is short for a slow reader.

## Needs from others
1. **`RugBatchDto` is missing seller and number** (`App/DTOs/Batches/BatchDtos.cs`, no owner in this wave). Append `string? SellerName = null, int? Number = null`. Then pass `batch.SellerName, batch.Number` as the last two arguments in `RugBatchService.ToDto`.
2. **`PlatformServicesIntegrationTests.cs:65`** should expect `HomeShipmentSource`, not `EmptyHomeShipmentSource`. This is the only failing test on the final tree.
3. **`Views/Rugs/_Details.Sizes.cshtml` (lines 52–55) shows the seller twice:** «ورودی ۳ · حاج محمود · ۱۴۰۵/۰۷/۲۳ · حاج محمود». Only add `· SellerName` when the batch name does not already contain it.
4. **Orchestrator:** a throwaway database `rugs_p3ui_a` that I didn't create is still on the server; drop it if no package uses it.

## Files
All edits are in P3-owned files under `C:\Users\sadra\source\repos\RugsManagment_Service`. In `src\RugsManagment.Web\ClientApp\src\components\grid\`:
- `useVirtualRows.ts` and `useVirtualRows.spec.ts`
- `GridCell.vue`
- `DataGrid.vue`
- `gridParse.ts` and `gridParse.spec.ts`
- `useGridSave.ts` and `useGridSave.spec.ts`
- `useGridController.ts` and `useGridController.spec.ts`

Elsewhere:
- `src\RugsManagment.Application\Features\Intake\Grid\RugGridColumns.cs`
- `src\RugsManagment.Web\Views\Intake\Index.cshtml`

Nothing was created or deleted in the repo, and no git state changed. `rugs_p3_int` was dropped and the work folder `rugs-plan2\work\p3-integrate` deleted; the shared `node_modules` is intact.

Report: `docs/plan2/reports\p3-integrate.md`