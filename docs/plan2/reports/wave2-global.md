# wave2-global: whole-solution integration report

Every check passes on the combined tree: build, migrations, all .NET and frontend tests, and the browser smoke test. The build needed no fixes. One test was failing at the start, and I fixed it. I applied every open "needs from others" item that belongs to this wave. Items that are future packages' scope (P4b, P6-wiring, P7) are listed under open problems, not built.

## 1. Final command results (after all fixes)

| Check | Result |
|---|---|
| `dotnet build RugsManagment_Service.slnx -c Release -warnaserror -p:NuGetAudit=false --no-incremental` | Build succeeded, **0 warnings, 0 errors**. It was already clean, so nothing needed fixing. |
| `dotnet ef migrations has-pending-model-changes` | **No changes.** No migration conflict: M2 `20261004063353` → M3 `20261004194543_Shipments`. P3 and P6e added no migration. |
| M2 `20261004063353` Up | **Untouched.** `git diff` shows only `Down` changed (P2-review-fix #10). |
| Unit tests | **909/909** |
| Architecture tests | **15/15** |
| Integration tests (`RUGS_TEST_PG` set) | **352/352**. It was 351/352 at the start. |
| `npm run build` (vue-tsc strict + vite) | passes |
| `npx vitest run` | **29 files, 468/468** (467 plus 1 new guard test) |
| App start on an empty throwaway DB | All 20 migrations applied, the demo seed ran, and the startup ledger check reported healthy. |

## 2. What I fixed or applied

1. **The one failing test.** `tests/RugsManagment.IntegrationTests/PlatformServicesIntegrationTests.cs:65-66` (P1 file, reported by 7 lane reports): `DiGraph_…` now expects `RugsManagment.Application.Features.Shipments.HomeShipmentSource` (erratum E3).
2. **Rugs grid size limit.** `ClientApp/src/islands/rugs/RugsGrid.island.vue:42` (p3-grid-ui, asked by P1-fix): `() => 12` became `() => ctx.maxPlausibleSideM`. A new guard test in `p2ReviewFixes.spec.ts:18,168-172` checks it.
3. **Batch seller and number (spec I.2).**
   - `Application/DTOs/Batches/BatchDtos.cs:9-12`: `RugBatchDto` gets trailing optional `SellerName` and `Number`.
   - `Application/Services/RugBatchService.cs:282-284`: `ToDto` passes `batch.SellerName, batch.Number`.
   - `IntegrationTests/P3/P3LayoutAndBatchTests.cs:99-104`: asserts `GET /api/batches` returns both.
4. **Seller shown twice.** `Views/Rugs/_Details.Sizes.cshtml:52-54` (asked by p3-integrate): the seller is added only when the batch name doesn't already contain it. In the browser it now reads «ورودی ۲ · کریمی · ۱۴۰۵/۰۷/۲۳».
5. **Fixtures README.** `tests/fixtures/README.md:9,21-23` (asked by P6-engine): heading renamed and the `sample-rows-1-43.expected.json` section added, word for word from the request.
6. **Focus bug found in the smoke test.** `ClientApp/src/islands/rugs/RugFinalSize.island.vue:19,85-87`: after «اندازهٔ تازه», focus now moves to «طول نهایی» (spec H.7). Checked in the browser.
7. **Leftover databases.** Dropped `rugs_p3ui_a` and `rugs_p1fix_app`, which had no open connections.

**Already done when I checked (no edit needed):**
- The `RugFieldWriter` CS0136 compile error, the P4a unit-test compile errors and the P6 CS0028 warning are all fixed.
- The P2Fix test compile errors are fixed.
- Renumbering a finalized shipment now returns Locked (`BaleService.cs:152`).
- The HomeStock test passes.

## 3. Browser smoke test

The app ran on the throwaway DB `rugs_w2g_app`, seeded by the demo seeder, at `w2g.localhost:5391`, signed in as the demo admin.

- **Home:** coach, tiles and stock card (4 pieces / 22.88 m²) render. Once the shipment existed, «محمولهٔ ۱ · ۲ نگله · ۱۸٫۷۸ متر» came from the real `HomeShipmentSource`.
- **Intake grid:** I typed 3 rows with the keyboard: `5001`, the lot `5101 - 5150` (quantity filled in as 50) and `5003`.
  - The serial suggestion and the seller carried down, and Enter went back to «شماره».
  - Every save request returned 200 and «ذخیره شد» appeared.
  - Footer: «۵۲ تخته (۳ ردیف) · ۱۸٫۲۵ متر · خرید ۹۲٬۰۰۰٬۰۰۰ تومان».
- **Rugs list and details:**
  - Rugs list: all 8 rugs show the right sizes and costs.
  - Purchase lines: the editor shows «۸۰,۰۰۰ × ۵۰ = ۴,۰۰۰,۰۰۰».
  - Final size: 0.42 × 0.41 saved, showing «↑۲ سانت · ۰.۱۷ × ۵۰».
  - Islands: all mount.
  - Grid view: `?view=grid` works.
- **Shipments:**
  - «محمولهٔ تازه» created shipment 1. I added a bale through `POST /bales`.
  - Through the browser `fetch` path I added `5001` and «۵۰۰۳» (Persian digits) to bale 1, and the lot to bale 2, which was created by that first add.
  - Serial 5120 returned the prompt `Q_LINE_SERIAL_IN_RANGE` instead of being added.
  - Workspace totals are right: 2 bales, 52 pieces, 18.75 m.
  - The readiness lines show 2 rugs missing a final size and 3 rugs with no declared value.
- **Settings hub:** all 11 tile links return 200.
- **Declared values:** the «کاشان» chip added a row, 40 was saved, and «جدول ارزش گمرکی ذخیره شد.» appeared.
- **Console:** no CSP violations on any page. The only entries:
  - "unregistered island" warnings for `shipment-list` and `shipment-header`, expected until P4b;
  - service-worker registration errors. These come from the browser pane: it refuses any script as a service worker, while `/sw.js` itself serves 200.

**Cleanup:** the app is stopped, `rugs_w2g_app` dropped, the browser tab closed and the work folder deleted. I never touched `rugs_management` and ran no git command that changes state.

## 4. Open problems

1. **P4b (next wave, already in its brief):**
   - register the `shipment-list` and `shipment-header` islands;
   - add `BalesTab`;
   - put «قطعی کن» / «باز کردن دوباره» with a confirm dialog in the header island;
   - decide how the grid batch reports a lock refusal per row;
   - build the baling board.
2. **P6-wiring:**
   - register `IXlsxDocumentWriter` → `ClosedXmlDocumentWriter` in `DocumentsModule`;
   - map the database and the finalize snapshot to `DocumentData`;
   - pass the Document field catalogue as `ComposeOptions.Fields`;
   - give the rendered DTO a styles member (or render Razor from `RenderModel`).
3. **P6-wiring / P7:** copy the engine's message codes into `lib/messages/documents.ts`.
4. **LibreOffice check (E.6 #3/#5):** still not verified. LibreOffice isn't installed, and installing it needs the owner's OK.
5. **Number formatting is inconsistent (P9 candidate).**
   - The server shows «٬» and «٫».
   - The client's `faDecimal`/`faMoney` show «,» and «.», for example «۰.۴۲ × ۰.۴۱».
   - `FormatNumber` drops trailing zeros, so a bale area shows «۸٫۵» instead of «۸٫۵۰».
   - This is how P1 built it; changing it touches many tests, so I left it.
6. **Prompt wording:** adding a serial whose lot is already in this shipment asks «داخل دسته است» rather than saying it is already baled. Nothing is saved; the P4b board text should handle it.
7. **Sticky purchase price (spec G.2, owner decision):** a row typed with no price silently took the previous row's «۵۰۰٬۰۰۰ هر متر» and got a purchase line.
8. **Not walked in the browser this run:** the Operator role, 375 px card mode and two-tab conflicts (the P3 and P4a integrate runs covered these), and the offline page, which the pane cannot run.

Files changed this run are the 9 listed in section 2, plus `wwwroot/dist` rebuilt by `npm run build`. No migration was added or changed.

Report: `docs/plan2/reports\wave2-global.md`