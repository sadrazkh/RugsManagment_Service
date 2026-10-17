All 25 confirmed Phase 2 findings are dealt with: 24 are fixed in this package, and #23 was already fixed by p1-fix. Each one has a test.

To show the tests are real, I reverted the server fixes in a throwaway copy of the tree:
- 17 of the 22 new unit tests failed there. The other 5 test normal user behaviour and are meant to pass.
- All 13 new or changed integration tests failed there.
- All of them pass on the real tree.

I never touched `rugs_management` and ran no git command that changes state. The scratch copy under `work/p2-review-fix` is deleted.

## What was already on disk vs done now
**Already on disk (uncommitted) from the stopped runs:**
- **Server fixes:**
  - #0: `ITrashAwareQueries` + `TrashAwareQueries`, registered in `LedgerInfrastructureModule` and used by `StepTypeService`.
  - #1: `RugSaleService`.
  - #2: `LedgerSession.ChangedSerial`.
  - #5: `WorkflowEngine.EnsureStepMoneyFits`.
  - #8: `AnalyticsService`.
  - #9: `CostCategorySeedContributor`.
  - #11 and #13: `RugFieldWriter`, plus `RugPurchaseService.Validate` for #13.
  - #12: `RugManagementService`.
  - Server half of #17/#19: `NextSerialDto.Mode`.
- **Messages and tests:** the new message codes and the P2App test-double updates.
- **Not done:** no tests for any fix and no frontend work. `RugFieldWriter.cs` did not compile (CS0136, `quantityChanged` declared twice).

**Done in this run:**
- Fixed the compile error.
- Fixed #3 (server and client), #4 and #10.
- Added a fallback for #13: any numeric overflow (Postgres 22003) inside the ledger pass now returns 400 instead of 500.
- Fixed all frontend findings (#6, #7, #14–#22, #24) and checked #23.
- Wrote all the tests and ran the fail-before check and full verification.

## Per finding
**Server**
- **#0** (step type deleted while used only on a trashed rug): fixed — `P2ReviewFixLedgerTests.DeletingAStepType_UsedOnlyOnATrashedRug_DeactivatesIt_AndKeepsTheRugsStepsAndTotal`
- **#1** (refresh cost at sale uses a stale value; a sale edit can undo it): fixed. The refresh now waits for the ledger lock and re-reads the total. A sale edit writes only the columns it changed.
  - `RefreshCost_RacingAnUncommittedCostChange_WaitsForTheLock_AndSnapshotsTheCommittedTotal` (a real race, checked through `pg_locks`)
  - `SaleEdit_LoadedBeforeARefresh_DoesNotRevertTheRefreshedCostAtSale`
- **#2** (restore 409 shows an empty serial): fixed — `Restore_WhenTheSerialIsNowTaken_Is409NamingTheSerial`
- **#3** (step-cost preview ignores lot quantity and the priced snapshot): fixed.
  - `PricingPreviewRequest` gets a trailing `int Quantity = 1`.
  - The preview prices one piece × quantity and applies the floor at 0 to the whole line.
  - `RugWorkflowStepDto` gets trailing `PricedLength/PricedWidth/PricedQuantity`.
  - `RugWorkflowPanel` and `CostRuleEditor` now send the snapshot size and quantity.
  - Tests: `Preview_OfALot_IsPiecePriceTimesQuantity_WithTheDiscountOnTheLine`, `Preview_OnThePricedSnapshot_EqualsTheSavedStepCost`, `Preview_Combined_AndManual_OnALot`, `PricingPreview_TakesTheQuantity`.
- **#4** (home stock card ignores quantity): fixed in `RugHomeStockSource` — `HomeStock_DefaultSource_CountsLotPieces_AndTheLotArea` (51 pieces, 12.37 m²)
- **#5** (step manual cost and adjustment ignore MoneyScale): fixed — `StepMoney_WithMoreDecimalsThanMoneyScale_Is400` (4 cases), `StepMoney_WithinMoneyScale_IsAccepted`
- **#8** (analytics average rounds half to even): fixed — `Analytics_AverageStepCost_RoundsHalfAwayFromZero`
- **#9** (legacy purchase line on a trashed rug never linked): fixed — `Seed_LinksTheLegacyLineOfATrashedRug_SoRestoreThenCellWrite_KeepsOneLine`
- **#10** (M2.Down brings back a deleted price): fixed. Only Down was edited, and the HandEdits note (§6) is updated. Up is untouched.
  - Down now recomputes the price column for every rug; a rug with no lines gets NULL.
  - `M2_DownThenUp_…` gained the "all lines deleted → NULL" case.
- **#11 (high)** (Operator writes money through a key with a leading space): fixed.
  - `PaddedMoneyKey_FromAnOperator_IsRejectedByTheGuard` (5 keys)
  - `PaddedTargetSalePrice_…`, `PaddedPurchaseKey_…`, `PaddedKey_FromABusinessUser_StillWrites`
  - `Operator_PaddedMoneyKeys_Are403_AndChangeNoMoney`
- **#12** (legacy money fields beside `values` not refused): fixed — `Operator_LegacyMoneyMembers_BesideValues_Are403_WhileNullsStayAllowed`
- **#13** (an amount that overflows its column gives 500): fixed. Purchase writes are checked before saving. Anything else that overflows inside the ledger pass now returns 400 `E_AMOUNT_OVERFLOW`, via `ILedgerSession.IsNumericOverflow` and `LedgerPass`.
  - `PerM2Rate_OnA30By30Rug_…`, `PerPieceRate_TimesALargeLot_…`, `QuantityChange_ThatWouldOverflowAKeptLine_Is400OnTheQuantity`, `NormalLargeValues_StillPass`
  - `PurchaseRate_WhoseAmountOverflowsTheColumn_Is400_NotA500`, `AmountOverflow_FromAnyOtherPath_Is400_NotA500_AndNothingIsSaved`, `PurchaseAmount_OverflowingTheColumn_Is400OnTheCell`

**Frontend.** The logic is in three new pure modules (`costBreakdownText.ts`, `salePreview.ts`, `rugFormRules.ts`). They are tested in `islands/rugs/p2ReviewFixes.spec.ts` (20 tests), which also reads the island source files to guard fixes that live only in templates.
- **#6** (sale edit preview uses today's cost): fixed. Editing a sale previews profit from the cost at sale; a new sale uses today's cost.
- **#7 / #22** (float sum shows a false lump-purchase line): fixed. Line amounts are summed exactly with `lib/num`.
- **#21** (breakdown shows rates rounded to MoneyScale): fixed. Rates show with up to 4 decimals.
- **#14** (Enter in the purchase editor submits the rug form; Save can stay stuck):
  - Enter in an input inside the editor no longer submits the rug form.
  - After a successful edit, the form takes the new version, clears its saving state, then navigates.
- **#15** (RugFinalSize never announces its new version): fixed.
  - After a save, RugFinalSize takes the version and final size from the response and announces them to the other islands.
  - RugSplit now listens for those announcements, uses its local version and announces after a split.
- **#16** (per-m² rates skip the «۱۰٬۰۰۰؟» question): fixed in `lib/money.ts`. The question now follows the tenant's currency MoneyScale, not the field's 4-decimal precision.
- **#17** (suggested serial not accepted by Enter/Tab): fixed. Enter or Tab accepts it; Enter only accepts and does not submit. The placeholder shows Persian digits.
- **#18** (focusing a rate marks the editor dirty): fixed. The editor is marked dirty only when the rate value really changes.
- **#19** (Auto serial mode still editable): fixed. Auto mode shows a read-only «خودکار: ۴۰۰۴».
- **#20** (form adopts the editor's newer version): fixed. The editor reports the version it saved on. The form adopts the new version only if that matches its own; otherwise the form's save gets a 409.
- **#23** (MaxPlausibleSideM ignored): already fixed by p1-fix through app-context and the `NumberField` fallback. A guard test checks that RugForm and RugFinalSize don't hard-code a value.
- **#24** (numeric keypad on serial inputs): fixed. The serial inputs in RugForm and RugSplit use `inputmode="text"`.

## Verification (whole tree, about 13:20, all parallel packages present)
| Check | Result |
|---|---|
| Release build with `-warnaserror` | 0 warnings, 0 errors |
| `has-pending-model-changes` | No changes |
| Unit tests | 909/909 |
| Architecture tests | 15/15 |
| Integration tests | 349/352 (the 3 failures are P4a's, below); all 13 P2Fix and all 6 M2 migration tests pass |
| `npm run build` | passes |
| `npx vitest run` | 460/460 in 29 files |

## Notes
- I did not do a browser walkthrough. Template-level fixes are covered by the source-guard vitest tests and the strict `npm run build`.
- I did not run a fail-before check on the frontend tests. The new modules didn't exist before, and the reviewers had already shown that the old code accepts «300.000» as 300.
- `lib/money.ts` was edited earlier by p1-fix but is not on the do-not-edit list. My change is one hunk: a new optional `moneyScale` option plus the `currencyScale`/`asTyped` lines in `parseMoney`. Their `money.spec.ts` still passes unchanged.

## Needs from others
1. **P4a** — 3 integration failures come from P4a's service replacements:
   - `PlatformServicesIntegrationTests.HomeStock_UsesTheLotRuleInSql_AndSkipsSoldAndTrashed`: the test now resolves `ShipmentHomeStockSource`. That source treats a rug as sold only if it has a `RugSales` row. The test seeds `Status = Sold` with no sale row, so it gets 4 pieces / 14.20 instead of 3 / 5.20.
   - `PlatformServicesIntegrationTests.DiGraph_ResolvesEveryPlatformService_AndRegistersTheSeedTaskAt100`: expects `EmptyHomeShipmentSource`, but P4a's `HomeShipmentSource` replaces it.
   - `P4aPerformanceTests.FinalizeAndReopen_OfA300LineShipment_IssueFewerThan20SqlCommands`: P4a's own test.
   - Edit: P4a should update the two PlatformServices assertions to its replacement sources, for example by seeding a `RugSales` row for the sold rug.
2. **p1-fix** — for information: #23 is closed by your app-context `maxPlausibleSideM` and the `NumberField` fallback. Please keep my `lib/money.ts` `moneyScale` hunk (#16) if you rewrite that file.

The same report is saved at `docs/plan2/reports\p2-review-fix.md`.

New files:
- `C:\Users\sadra\source\repos\RugsManagment_Service\tests\RugsManagment.Tests\P2Fix\P2ReviewFixTests.cs`
- `C:\Users\sadra\source\repos\RugsManagment_Service\tests\RugsManagment.IntegrationTests\P2Fix\P2ReviewFixLedgerTests.cs`
- `C:\Users\sadra\source\repos\RugsManagment_Service\tests\RugsManagment.IntegrationTests\P2Fix\P2ReviewFixApiTests.cs`
- `C:\Users\sadra\source\repos\RugsManagment_Service\src\RugsManagment.Web\ClientApp\src\islands\rugs\costBreakdownText.ts`
- `C:\Users\sadra\source\repos\RugsManagment_Service\src\RugsManagment.Web\ClientApp\src\islands\rugs\salePreview.ts`
- `C:\Users\sadra\source\repos\RugsManagment_Service\src\RugsManagment.Web\ClientApp\src\islands\rugs\rugFormRules.ts`
- `C:\Users\sadra\source\repos\RugsManagment_Service\src\RugsManagment.Web\ClientApp\src\islands\rugs\p2ReviewFixes.spec.ts`
- `C:\Users\sadra\source\repos\RugsManagment_Service\src\RugsManagment.Application\Abstractions\Persistence\ITrashAwareQueries.cs`
- `C:\Users\sadra\source\repos\RugsManagment_Service\src\RugsManagment.Infrastructure\Persistence\Repositories\TrashAwareQueries.cs`