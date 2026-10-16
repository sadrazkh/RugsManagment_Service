## p2-app (WP2-C rest, Stage 3) report: resumed run

The p2-app package is complete and verified. The stopped attempt had already done most of it. In this run I audited it against the spec, fixed four defects, added 8 API tests and smoke-tested it in the running app. One unit-test file outside my package (`PlatformServicesTests.cs`) still breaks the build of `RugsManagment.Tests`; a verified patch is ready (Needs from others #1).

### Found already done (stopped attempt)
Everything below was on disk. I read it all and checked it against the spec and errata.
- **Application:**
  - `Features/Rugs/{RugFieldWriter, RugSplitService, SerialSuggester, RugSkuGenerator, RugPurchaseService, RugDtoBuilder, RugFeaturesRegistration}.cs`
  - `Fields/{FieldCatalog, FieldDefDto, IFieldCatalogContributor, RugFieldContributor, RugFieldKeys, RugMoneyFieldContributor}.cs`
  - `Seed/CostCategorySeedContributor.cs`, `Common/Messages.fa.Rugs.cs`
  - `Services/RugManagementService.cs`: field writer, SKU from the counter, no retry loop, I.3 legacy rules, E2, E5, E17
- **Web:**
  - Controllers: `Api/{RugsApiController, LookupsApiController, DiagnosticsApiController, RugMoneyApiController}.cs`, `Settings/LedgerHealthController.cs`
  - `RugDetails/{IRugDetailPanel, RugDetailPanelsViewComponent, RugDetailsFormat, SplitHistoryPanel}.cs`
  - Views: `Views/Rugs/{Index, Details, Trash}.cshtml`, `_Details.{Custom, Money, Panels, Sizes, Specs, SplitHistory}.cshtml`, `Views/Settings/LedgerHealth.cshtml`
- **ClientApp:**
  - `islands/RugForm.vue`, `islands/QrScanner.vue`
  - `islands/rugs/{PurchaseLinesEditor, RugCostBreakdown, RugFinalSize, RugSplit}.island.vue` plus `purchasePreview.ts` (+spec) and `rugEvents.ts`
  - `islands/settings/LedgerHealth.island.vue`, `components/PurchaseLinesEditor.vue`
  - `lib/labelFields.ts` (+spec), `lib/messages/rugs.ts` (+spec)
- **Tests:** `tests/RugsManagment.Tests/P2App/*` (7 files), `tests/RugsManagment.IntegrationTests/P2/{P2RugApiTests, P2RugTestSupport}.cs`

The old 12/12 integration-test failures were caused by a query bug in WP2-B's `RugCostTotalReader`. It is fixed now, and those tests pass.

### Completed in this run (all paths under `C:\Users\sadra\source\repos\RugsManagment_Service\`)
- **`src/RugsManagment.Application/Features/Rugs/RugPurchaseService.cs`:** the cost-breakdown endpoint wrote the step-cost formula outside `CostSql`/`RugCostMath`, which failed the single-formula-site architecture test. It now uses `RugWorkflowStep.EffectiveCost`.
- **`.../Features/Rugs/RugFieldWriter.cs`:** an unchanged legacy save of a rug with one "Total" purchase line used to overwrite its rate with the rounded amount (rate 1000.5 became 1001 at money scale 0). This broke the "lines unchanged" rule of the legacy round trip (LM-02). Sending back the line's own amount now leaves it alone.
- **`src/RugsManagment.Application/Services/RugManagementService.cs`:** in Auto serial mode, rugs created through the old request shape now also get a serial.
- **`.../Features/Rugs/RugSplitService.cs` and `Views/Rugs/_Details.SplitHistory.cshtml`:** the split chip converted the SKU to Persian digits ("RUG-۲۰۲۶۱۰-۰۰۰۷"). The audit text now has the serial in Persian digits and the SKU in Latin.
- **Tests:**
  - `P2App/RugFieldWriterTests.cs`: one new test.
  - `P2App/RugSplitServiceTests.cs`: updated expected text.
  - New `tests/RugsManagment.IntegrationTests/P2/P2RugApiEdgeTests.cs` (8 tests):
    - Auto serial (E2): the response carries a fresh version, client text is ignored, and legacy creates get a serial too.
    - Money scale 2: the lot comes to 364.50 (cost per m² 45.56, per piece 7.29).
    - Serial race: exactly one of two concurrent creates succeeds and the loser gets `nextFree`.
    - Serial edit to an overlapping range (E1) gives a 409.
    - Cross-tenant 404 on every new rug route.
    - E17: a template holding another tenant's step is refused on create and on apply-template.
    - A legacy size PUT returns the fresh total; bulk-fields keeps the ledger consistent.
    - Legacy round trip of a fractional "Total" line keeps its rate.

Nothing was deleted.

### Verification
| Check | Result |
|---|---|
| Release build with `-warnaserror` (Web, IntegrationTests, ArchitectureTests) | Succeeded |
| Release build of the whole `.slnx` | Only error is CS7036 in `PlatformServicesTests.cs` (#1) |
| Architecture tests | 15/15 (14/15 before the formula fix) |
| P2 integration tests | 21/21 |
| P1 + P2 integration tests | 151/151 |
| Full integration suite | 283/283 (run before the last two fixes and the new money-scale-2 test) |
| P2App unit tests (scratch project, file links only) | 77/77 |
| Whole unit suite with the patched `PlatformServicesTests` (scratch) | 610/610 |
| `npm run build` (strict vue-tsc) | Passed |
| `npx vitest run` | 222/222 |

**Live check** on the throwaway DB `rugs_p2_appsmoke`, run with the demo seed and dropped afterwards. `rugs_management` was never touched and I changed no git state.
- RugForm created the lot «3911 - 3960» × 50 at 0.40 × 0.40. It showed 8.00 m², a purchase preview of 350 plus broker 7, and the details page showed a total of 357, 45 per m² and 7 per piece.
- The list sorted by serial, and `/settings/ledger-health` reported zero mismatches.
- Through the API in the browser: "same as intake", then a split of 20 (214 + 143 = 357). After that, next serial was 3961 and «۳۹۵۰» was found as `inRange`.
- Typed clicks on the final-size and split buttons could not be run because the browser pane was hidden. Those paths are covered at API level.

### Acceptance (API-level bullets)
| Bullet | Result |
|---|---|
| Lot 8.00 m²; PerM2 45.55 → 350 at scale 0 and 364.50 at scale 2 | Pass |
| Overlap «3950-3970» → Persian 409 on `rug.serial`; next-serial 4004 | Pass |
| One request returns fresh numbers (intake PUT returns and persists the new total) | Pass |
| Legacy round trip (lines, intake size, total unchanged; title-only keeps both lines; differing price → 409) | Pass |
| Legacy PUT refreshes the cost total | Pass |
| BulkUpdateFields refreshes the cost total | Partial: it goes through the pass and the ledger stays consistent. In P2 no bulk field moves money, so there is nothing to refresh. |
| Money scale locked after the first money | Pass |
| Two purchase-line PUTs with one version → exactly one 409 | Pass |
| Operator: money writes 403, no money in responses | Pass |
| `/settings/ledger-health` shows zero mismatches | Pass |

### Could not do / deliberate choices
- Some D.9 split steps belong to later phases. P4a: the "not in a Finalized shipment" check and a new line in the same bale. P5: pins and cost-entry targets. The cost breakdown's allocation list stays empty until P5.
- A legacy PUT without `version` is still accepted, because the existing `P1MoneyVisibilityTests` sends one and expects 200. The new shape (`values`) requires a version.
- `StepCostDto.EffectiveCost` is now floored at 0 (single formula site). The DTO's doc comment, which is contracts-owned, still describes the unfloored value.

### Needs from others
1. **`tests/RugsManagment.Tests/PlatformServicesTests.cs` (P1 file, no P2 owner) — blocks the unit-test build.** It constructs `RugManagementService` with the old constructor and asserts the obsolete `Rug.PurchaseCost` column.
   - Patch: `scratchpad\p2app-platformfix\PlatformServicesTests.patch`, checked with `git apply --check`. The finished file is next to it.
   - It rewrites only the "money in the rug service" region: same tests, P2 semantics. A legacy price becomes a «قیمت خرید» Total line, and a 3-value `Deconstruct` keeps the OperatorSearch test unchanged.
2. **`App/Services/RugBatchService.cs` (P3 listed owner; blocks the real data after M2).** `CreateAsync` never sets `Number`, so the second new batch per tenant fails on the unique index. Fix:
   - inject `ITenantCounters counters`;
   - add `Number = (int)await counters.NextBlockAsync(tenantId, "batch", 1, ct),` to the `new RugBatch { … }`.
3. **`Web/Controllers/RugsController.cs` (owner unassigned):** in `Index`, replace `BuildRoute(displayQuery with { Page = result.TotalPages })` with `RugListViewModel.ToRouteValues(displayQuery, page: result.TotalPages)` and delete `BuildRoute`. Today it drops `withoutBatch` and the P2 filters and shifts the date by a day.
4. **`App/Common/MoneyRedaction.cs` (optional, owner unassigned):** add `CostPerM2 = null` next to `TotalInvestment = null` in `Redact(RugListItemDto)` and the `PagedResult` overload. Today only `RugManagementService` returns list rows, and it already redacts both.
5. **WP2-B:** nothing open from my side. The repository methods and `EntityMappers` match what my code expects; tests cover next-serial, by-serial, values and search/sort.