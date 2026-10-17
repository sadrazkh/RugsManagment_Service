## p2-ledger (WP2-B, Stage 3) report — resumed run

WP2-B is finished except for one step: the `[Obsolete]` attribute on `Rug.PurchaseCost` is not applied yet. Three of the remaining usages are in files I don't own, so the attribute would break the Release `-warnaserror` build until those edits land (needs #1–#3).

The previous attempt had written almost the whole package but had never run it against Postgres. When I ran it, 32 of the 34 ledger integration tests failed on one EF translation bug, and that bug broke every save that goes through the pass. It is fixed now. All 283 integration tests pass, the architecture tests pass 15/15, and the Release `-warnaserror` build has one error, in p2-app's `PlatformServicesTests.cs`.

### What the previous attempt left on disk (audited, kept)
- **Pass core in `src/RugsManagment.Application/Ledger/`:** ChangeSet, LedgerImpact, LedgerContext, the contributor/guard/stage interfaces, LedgerStageOrder, LedgerPass, ILedgerSession/ILedgerTransaction/ILedgerLock, ILedgerRepository, ICostSourceProvider with NoCostSources and IStoredMoneyProbe, IRugCostTotalReader, LedgerVerifier, LedgerMaintenance, LedgerVersion, PurchaseMath, SerialOverlapException.
  - Contributors: Rug (including E1 serial changes), PurchaseLine, Step, Sale.
  - Guard: PreferencesLockGuard.
  - Stages: SerialOverlap (check only, per E2), PurchaseLine, PurchaseRollup, RugCostTotal (also writes CostAtSale).
- **Infrastructure in `src/RugsManagment.Infrastructure/Persistence/Ledger/`:** ChangeSetBuilder (value diff), LedgerSession, LedgerTracking, LedgerOptions, LedgerRepository, CostSql, LedgerStartupCheck (order 200), RugCostTotalReader, CoreStoredMoneyProbe.
- **Other files:**
  - `IConcurrencyGuard` and `ConcurrencyGuard`.
  - `UnitOfWork`/`IUnitOfWork` with `ExecuteInTransactionAsync`.
  - The three extracted repositories: `RugRepository`, `ServiceProviderRepository` (E17 tenant filter), `RugBatchRepository`.
  - `RugStatusResolver`, `IRugDtoEnricher`.
  - `EntityMappers`, `WorkflowEngine` (`Priced*` snapshot, lot pricing, E17 check on template instantiation), `CostCalculationService`, `AnalyticsService`, `RugSaleService`, `DatabaseSeeder` (saves through the pass).
  - Both `AddLedgerModule()` files, the slnx entry, `tests/RugsManagment.ArchitectureTests/**`, the ledger unit and integration tests.
- All seven old `TotalInvestment` sites now read `RugCostTotal` or use `CostSql`.
- `WorkflowManagementService.cs` needed no change: the dashboard already reads the preloaded totals through `EntityMappers`, and template steps are already checked against the tenant when saved.

### Completed in this run
1. **`RugCostTotalReader.cs` (the bug):** it filtered *after* projecting into a positional record, which EF cannot translate. It now filters first, then projects.
2. **`DatabaseSeeder.cs`:** the sold demo rug was created first and took SKU `-0001`. Creation order and SKU order match the old seeder again.
3. **`RugSaleService.cs`:** added `RefreshCostAtSaleAsync`, the D.6 «به‌روزرسانی بهای تمام‌شده» action. Editing a sale still keeps `CostAtSale`.
4. **Tests:**
   - `RugRepositoryV2Tests`: the expected cost per m² was wrong. At MoneyScale 0, 500 ÷ 8.00 = 62.5 rounds to 63, not 62.50.
   - `LedgerPassIntegrationTests`: added sale edit and refresh through the real `IRugSaleService`.
   - `PlatformServicesIntegrationTests` (RugSearch and OperatorRugEdit) and `P1MoneyVisibilityTests`: these relied on the old `PurchaseCost` column. Demo rugs now have purchase lines, and direct `DbContext` saves skip the pass, so the tests use lines plus a ledger rebuild.
   - `ExplicitEnumValuesTests`: now also checks every non-private enum in `src`.

All paths are under `C:\Users\sadra\source\repos\RugsManagment_Service\`. Nothing was deleted. No DB was left behind and `rugs_management` was never touched.

### Verification
| Check | Result |
|---|---|
| `dotnet build RugsManagment_Service.slnx -p:NuGetAudit=false` | src, architecture and integration projects build. 1 error: `tests/RugsManagment.Tests/PlatformServicesTests.cs(526)` CS7036, caused by p2-app's `RugManagementService` constructor |
| `-c Release -warnaserror` | Same single error, nothing else |
| Unit tests, run through scratch `p2l-unit/P2LUnit.csproj` (all test files except `PlatformServicesTests.cs`) | 560/560 |
| Architecture tests | 15/15 |
| Integration tests with `RUGS_TEST_PG` | 283/283 (34/34 ledger) |
| Temporary `[Obsolete("PROBE")]` build, then reverted | CS0618 only in `PlatformRepositories.cs:68`, `PlatformServicesIntegrationTests.cs:161`, `PlatformServicesTests.cs:521/544/562/572` |

### K.2/P2 acceptance (my scope)
| Bullet | Result | Evidence |
|---|---|---|
| Same numbers for unchanged data (list, sort, trash, provider statements) | pass | `RugRepositoryV2Tests`, `ProviderBalances_CountOnlyThisTenantsSteps_E17`, RugSearch 3000/2000/1000 |
| Lot 8.00 m², purchase line 350 / 364.50 | pass | `CostSqlParityTests` vectors, `P2RugApiTests.Lot_*` |
| Overlap is 409 with nextFree; next serial 4004; E1 | pass | `OverlappingRange_*`, `SerialEdit_IsOverlapChecked_E1`, `RestoringARug_RechecksItsSerial` |
| Step pricing on lots (qty 50, half away from zero) | pass | `CostCalculationServiceTests` vectors, `WorkflowEngineLedgerTests` |
| One request returns fresh numbers | pass | `IntakeSizeChange_ReturnsTheNewTotal_InTheSameRequest` |
| Only real changes count | pass | `ChangeSetBuilderTests`, `DescriptiveFields_HaveNoImpact` |
| Legacy PUT and BulkUpdateFields refresh `RugCostTotal` | pass | `P2RugApiEdgeTests` (p2-app) |
| MoneyScale and currency 409 | pass | `MoneyScale_IsLockedOnceMoneyExists`, unit guard tests |
| Architecture tests | pass | 15/15 |
| `-warnaserror` after `[Obsolete]` | **not done** | blocked, see needs #1–#3 |
| Real-DB M2 and `cost-consistency` = 0 | orchestrator | Demo seed verifies with 0 mismatches (`SeedThroughLedgerTests`) |

### Needs from others
1. **p2-app, `tests/RugsManagment.Tests/PlatformServicesTests.cs`:** fix the constructor call at line 526 (CS7036). Replace `Rug.PurchaseCost` at lines 521, 544, 562 and 572 with `RugPurchaseLine`s.
2. **Owner to assign (P1 file), `src/RugsManagment.Infrastructure/Persistence/Repositories/PlatformRepositories.cs`, `HasStoredMoneyAsync`:** replace lines 67–68 with:
   ```csharp
   db.Set<RugPurchaseLine>().Any(l => l.TenantId == tenantId)
   || db.Rugs.Any(r => r.TenantId == tenantId && r.TargetSalePrice != null)
   || db.Set<CostCategory>().Any(c => c.TenantId == tenantId && c.DefaultRate != null && c.DefaultBasis != CostBasis.Percent)
   ```
   In the same change, in `PlatformServicesIntegrationTests.MoneyScale_IsLocked_OnceAnyMoneyExists_EvenOnATrashedRug`, replace `PurchaseCost = 100m` with a Total `RugPurchaseLine` of rate 100 on that trashed rug. This test must change together with the repository.
3. **After #1 and #2:** add `[Obsolete("از M2 قیمت خرید = جمع سطرهای خرید (RugPurchaseLine)؛ این ستون فقط برای M2/Down مانده و M7 حذفش می‌کند.")]` above `public decimal? PurchaseCost` in `src/RugsManagment.Domain/Entities/Rug.cs`. No `#pragma` is needed: this differs from B.4, because the column has no explicit EF mapping (it is mapped by convention), so nothing references it at compile time.
4. **Expose `IRugSaleService.RefreshCostAtSaleAsync`:** a Business-only endpoint in `SalesApiController` (for example `POST api/rugs/{rugId}/sale/refresh-cost`), plus the chip action in `RugSalePanel.vue` (P9 owns that file per K.1).
5. **`RugBatchService.CreateAsync` (still open from Stage 1):** set `Number` from the `batch` counter. The demo seeder now takes batch #1 from that counter, and a tenant's second new batch still fails with 23505.
6. **`App/Common/MoneyRedaction.cs`:** its `RugListItemDto` and `PagedResult` overloads null `TotalInvestment` but not `CostPerM2`. No leak today, because `RugManagementService.RedactItem` nulls both, but any other caller of those overloads would show Operators the cost per m².