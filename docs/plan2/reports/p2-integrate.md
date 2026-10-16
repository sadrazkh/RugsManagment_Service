## p2-integrate (Phase 2, Stage 4) report

Phase 2 is integrated and every check passes. I applied all the open "needs from others" items from the four reports. `[Obsolete]` is now on `Rug.PurchaseCost`, and the Release `-warnaserror` build passes. I also found and fixed one failure the earlier stages could not see.

I ran the app and walked every K.2/P2 acceptance bullet that does not need the real DB. One check compared the old (pre-M2) build with the new build on the same demo database, which had sales, a provider and a floored adjustment added. All 316 money numbers came out identical. Both throwaway DBs are dropped. Only `rugs_management` remains, and I never touched it. I changed no git state.

The full report is also at `docs/plan2/reports\p2-integrate.md`.

### Files
All paths are under `C:\Users\sadra\source\repos\RugsManagment_Service\`.

**Modified**
- `tests/RugsManagment.Tests/PlatformServicesTests.cs`: applied p2-app's verified patch. This fixed the build error CS7036.
- `src/RugsManagment.Infrastructure/Persistence/Repositories/PlatformRepositories.cs`: `HasStoredMoneyAsync` now asks the same money checks (`IStoredMoneyProbe`) as `PreferencesLockGuard`.
  - The preferences page and the 409 lock now always agree.
  - It no longer reads the obsolete `PurchaseCost` column.
- `tests/RugsManagment.IntegrationTests/PlatformServicesIntegrationTests.cs`: the money on the trashed rug is now a Total purchase line.
- `src/RugsManagment.Domain/Entities/Rug.cs`: `[Obsolete]` on `PurchaseCost`. No `#pragma` was needed, because EF maps the column by convention.
- `src/RugsManagment.Application/Services/RugBatchService.cs`: `Number` now comes from the `batch` counter. A tenant's second batch no longer fails with 23505.
- `src/RugsManagment.Infrastructure/Persistence/Seed/DatabaseSeeder.cs`: uses the same counter key constant.
- `src/RugsManagment.Application/Common/MoneyRedaction.cs`:
  - the full rug DTO now also hides purchase lines;
  - list rows now also hide `CostPerM2`, in both overloads;
  - new redaction for by-serial results and for both halves of a split.
- `src/RugsManagment.Application/Features/Rugs/RugDtoBuilder.cs`: redacts again after the enrichers, so money that later phases add cannot reach an Operator.
- `src/RugsManagment.Application/Services/RugManagementService.cs`: uses `MoneyRedaction` instead of its own private copies.
- `src/RugsManagment.Web/Controllers/Api/SalesApiController.cs`: new Business-only `POST api/sales/rug/{rugId}/refresh-cost` for «به‌روزرسانی بهای تمام‌شده».
- `src/RugsManagment.Web/ClientApp/src/islands/RugSalePanel.vue`: shows the «هزینه پس از فروش تغییر کرده» chip with the refresh button.
- `src/RugsManagment.Web/Controllers/RugsController.cs`: the redirect past the last page now uses `RugListViewModel.ToRouteValues`, and `BuildRoute` is deleted. It used to drop `withoutBatch` and the P2 filters, and moved the date by a day.
- `src/RugsManagment.Web/Home/Cards/TilesCard.cs`: the shipment cost scope now comes from `CostScope.Shipment`.
- `src/RugsManagment.Web/Views/Rugs/Index.cshtml`: reworded one line of a Razor comment that contained «محموله».
  - `GlossaryTests` failed on it.
  - Earlier stages ran their unit tests in scratch projects, so they never saw this.
- `src/RugsManagment.Application/DTOs/Rugs/RugCostBreakdownDtos.cs` and `ClientApp/src/lib/types/rugs.ts`: doc comments now say `EffectiveCost` is floored at 0.
- `tests/RugsManagment.IntegrationTests/P1/P1TestSupport.cs`: the Operator money check now also covers `purchaseLines` and `costPerPiece`.

**Created**
- `tests/RugsManagment.Tests/P2App/P2MoneyRedactionTests.cs`: 4 unit tests for the new redactions.
- `tests/RugsManagment.IntegrationTests/P2/P2IntegrationGlueTests.cs`: 3 tests:
  - batch numbering;
  - refresh-cost (400 without a sale, 403 Operator, 404 other tenant, snapshot kept until refresh);
  - the list redirect keeps every filter.

**Deleted:** none.

### Verification
| Command | Result |
|---|---|
| Debug build | 0 warnings, 0 errors |
| Release build with `-warnaserror` (also with `--no-incremental`) | Succeeded |
| `has-pending-model-changes` | No changes |
| Unit tests | 614/614 |
| Architecture tests | 15/15 |
| Integration tests | 287/287 |
| `npm run build` | Passed |
| `npx vitest run` | 222/222 |

### K.2/P2 acceptance
| Bullet | Result | Evidence |
|---|---|---|
| M2 self-verify, `Length = COALESCE(Final, Intake)`, `cost-consistency` = 0 | Pass on a demo-seeded copy; the real DB is yours | Without `ConfirmedBackupFor`, startup stops with the backup error. With it, M2 and its self-verify pass. psql shows 0 bad size pairs; the API reports `{checked:5, mismatches:[]}`. |
| Same numbers before and after M2 (list, sort, dashboard, sales profit, provider statements) | Pass | 0 differences across 316 values. Area was left out on purpose, because I.3 now stores it rounded to 2 decimals. |
| Lot 8.00 m² with PerM2 45.55 → 350 at money scale 0 | Pass | Live on «5011 - 5060»: 7 per piece, 350 total, 44 per m² |
| Same lot at money scale 2 → 364.50; declared USD 7.29 / 364.50 | Pass (tests only) | Integration test for scale 2; USD is covered by the vectors, since shipments arrive in P4a |
| «3950-3970» refused; next serial 4004 | Pass | Persian 409 on `rug.serial` with `nextFree`. Next serial went 3961, then 4004 after «3985 - 4003». Editing a serial to an overlapping one (E1) also gets 409. |
| Step pricing on a 50-piece lot | Pass | PerSquareMeter 680,000; Fixed 2,500,000; PerSquareFoot 86,100; PerLength 24,700 |
| One request returns fresh numbers | Pass | Intake PUT: 39,330,000 → 54,000,000 in the same response, and the same on re-read |
| Legacy round trip | Pass; the lump-share part waits for P5 | Unchanged PUT changes nothing; a title-only PUT keeps both lines; a different `purchaseCost` gives 409 |
| Only real changes count | Pass (unit tests) | `ChangeSetBuilderTests` |
| Legacy PUT and BulkUpdateFields refresh the cost total | Pass | Legacy size PUT returns and saves 67,500,000; bulk edit leaves 0 mismatches |
| Money scale and currency lock | Pass | Preferences PUT → 409 on `moneyScale`. A currency change on the tenant form is refused with a Persian toast. |
| Architecture tests; `-warnaserror` after `[Obsolete]` | Pass | 15/15; Release build green |
| `/settings/ledger-health` shows zero mismatches | Pass | Page returns 200; 0 mismatches after every live change |

**D.7 worked example**
- Rows #1–#3 pass. The new batch «ورودی ۱» got number 2. Totals came to 40,116,600 / 15,300,000 / 73,174,800.
- Rows #7 and #8 pass for the purchase part:
  - C's final size gives 8.25 m² and 8,869,673 per m², with the purchase unchanged.
  - Trashing and restoring B leaves every total intact.
- Rows #4–#6, and the washing and freight parts of #7–#8, need cost entries, which arrive in P5.

**Refresh chip, live:** after a cost change following a sale, the chip showed «+۵۰ تومان». Clicking the refresh button updated the sale's cost and profit, and the chip disappeared.

### Could not do / deliberate choices
- **Real-DB M2 run:** that is the orchestrator's step.
- **Live Operator walk:** skipped, because it needs a new Operator account. Tests cover it.
- **Live money-scale-2 lot:** skipped, because it needs a second tenant. An integration test covers it.
- **Two identical chips on the rug details page:** one in the cost summary and one in the sale panel next to the button. I kept both because each sits next to its own «سود واقعی» row.
- **Two spec deviations kept from earlier stages; please decide:**
  - The money lock ignores a Percent default rate on a cost category. Otherwise the seeded «دلالی» 2% would lock the money scale of every new tenant.
  - There is no `#pragma` around the EF mapping, because the column has no explicit mapping.

### Needs from others
1. **Orchestrator:**
   - Back up the real DB and run M2 with `Database:ConfirmedBackupFor=20261004063353_RugModelV2AndCosting`.
   - Commit the P2 tree.
   - Decide the two deviations above.
2. **P3:**
   - Have vitest consume `costVectors`, `serialVectors` and `serialQtyVectors` (optional item from the domain report).
   - The seller and day fields of a new batch stay with P3; `RugBatchService.CreateAsync` now only sets `Number`.
3. **P9 (optional):** merge the duplicate after-sale chip on the rug details page.