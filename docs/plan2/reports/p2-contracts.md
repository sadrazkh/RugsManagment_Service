## p2-contracts (WP2-C contracts, Stage 2) report

Stage 2 is done and every check passes. The P2 contracts (DTO appends, new DTOs, `RugQuery` filters, link round-trip, `IRugRepository` signatures, `lib/types/rugs.ts`) are in place. Two findings matter for Stage 3:
- **The I.1 compile guard does not exist on this SDK.** With SDK 10.0.401 (C# 14), a DTO built inside an EF projection compiles even when optional arguments are left out (CS0854 is never raised). The missing members silently project their default values. A scratch check printed `x => new R(x, 7, null)`. So I.1's "pass every appended member explicitly" rule has to be enforced by review or a test, for WP2-B (`Project`, `ListDeletedAsync`) and for P5 (`ProviderWorkItemDto`).
- **Operators will see money unless `MoneyRedaction` is updated.** No P2 package owns `App/Common/MoneyRedaction.cs`, and the new money members are not redacted yet. Once WP2-B fills them, they reach Operators. The exact change is under "Needs from others" #1.

### Files
All paths are under `C:\Users\sadra\source\repos\RugsManagment_Service\`.

**Created (owned)**
- `src/RugsManagment.Application/DTOs/Rugs/RugPurchaseDtos.cs`: `PurchaseLineDto` (I.3), `ReplacePurchaseLinesRequest`, `PurchaseLinesDto` (the PUT response `{lines, purchaseCost, version}` plus an optional `Costs`), `PurchaseCategoryOptionDto`.
- `src/RugsManagment.Application/DTOs/Rugs/RugCostBreakdownDtos.cs`:
  - `RugCostBreakdownDto` and `AllocationLineDto`, as in I.3.
  - `StepCostDto`, which I.3 uses but never outlines. Defined with `EffectiveCost` and `CountedCost` (Completed/InProgress, floored at 0).
- `src/RugsManagment.Application/DTOs/Rugs/RugSerialDtos.cs`: `NextSerialDto(int? Next)`, `RugBySerialDto(Rug, Location, MatchType)`, and `SerialMatchType` string constants (`"exact"`/`"inRange"`, matching the wire contract).
- `src/RugsManagment.Application/DTOs/Rugs/RugOperationDtos.cs`: `SetFinalSizeRequest`, `SplitRugRequest(Quantity, SerialFrom?, SerialTo?, Version)`, `SplitRugResultDto(Original, Created)`.
  - I read I.2's ambiguous names this way: `SerialFrom` is the source rug's new serial text and `SerialTo` is the new rug's serial text. Both null means automatic.
- `src/RugsManagment.Application/DTOs/Rugs/RugValuesQuery.cs`: `RugValueColumn` (explicit values) and `RugValuesQuery`, with `Sanitized()` and `TryFromFieldKey`.
  - Accepted keys: `rug.title`, `rug.origin`, `rug.pattern`, `rug.material`, `batch.seller`, `rug.custom.{key}`.
  - Custom keys are checked against the `CustomFieldService` key rule.
- `src/RugsManagment.Application/DTOs/Rugs/LedgerDiagnosticsDtos.cs`: `CostConsistencyReportDto`, `CostMismatchDto`, `CostMismatchCheck` codes and `LedgerRebuildResultDto`. This is the diagnostics contract between WP2-B and p2-app.
- `src/RugsManagment.Web/ClientApp/src/lib/types/rugs.ts`:
  - v2 `Rug`, `RugListItem` and `RugCostSummary` extend the legacy types, so they can still be passed to legacy-typed code.
  - Enums: `CostGroup`, `CostBasis`, `RugLocation`, `WorkflowStepStatus`, `RugSortBy`, `SerialMatchType`.
  - Also mirrored: `RugQuery`, `DeletedRug`, the requests (final-size, split, purchase-lines, v2 create/update), the breakdown, by-serial, next-serial and diagnostics types, `FieldValues`/`JsonValue`, and `FieldDef` (mirrors I.3's `FieldDefDto`).
- New tests:
  - `tests/RugsManagment.Tests/RugContractsTests.cs`: positional order of the I.3 appends, the new record shapes, every `RugQuery` filter turning on `HasActiveFilters` (found by reflection), `Sanitized`, `TryFromFieldKey`, take clamping.
  - `tests/RugsManagment.IntegrationTests/RugListRouteValuesTests.cs`: needs no DB. Every filter survives sort, page and chip links, converted back with the binder's TypeConverter. Also covers location chips and dates under `fa-IR`.

**Modified (owned)**
- `src/RugsManagment.Application/DTOs/Rugs/RugDtos.cs`:
  - `RugDto` gets the I.3 appends in I.3 order, then four extras: `IntakeAreaM2`, `FinalMeasuredAt`, `BatchNumber`, `SellerName`.
  - `RugCostSummaryDto` gets the I.3 appends.
  - `UpdateRugRequest` gets `Version` and `Values`. `CreateRugRequest` gets `Values`, which the v2 RugForm create needs.
  - Documented rule: a request with `Values` is a v2 request. Legacy W/L, money and descriptive members are then ignored, because the non-nullable W/L bind to 0. A request without `Values` follows the legacy I.3 rules.
- `src/RugsManagment.Application/DTOs/Rugs/RugListDtos.cs`:
  - `RugListItemDto` gets the I.3 appends.
  - `DeletedRugDto` gets `SerialNo` and `Quantity`.
  - `RugSortBy.Serial = 7` (numeric first; text and unnumbered last in both directions).
  - `RugQuery` gets `ShipmentId`, `BaleId`, `NotBaled`, `Location` and `Serial`, with `Sanitized` and `HasActiveFilters` terms.
- `src/RugsManagment.Application/Abstractions/Persistence/IRugRepository.cs`:
  - New methods: `GetMaxSerialAsync`, `FindBySerialAsync(Guid, SerialNumber, ct)` (exact match, else in-range), `ListDistinctValuesAsync(Guid, RugValuesQuery, ct)`.
  - Docs now say `GetWithWorkflowAsync` loads `PurchaseLines` and never `CostTotal`, and describe serial search, sort and location for `SearchAsync`.
- `src/RugsManagment.Web/Models/Rugs/RugListViewModel.cs`:
  - New static `ToRouteValues` covers every filter, including `withoutBatch`, which was never kept before.
  - New helpers: `WithFilter`, `LocationRoute`, `IsLocation`, `CreatedFromText`/`CreatedToText`, `DayText`.
  - Fixed an older bug: links wrote the sanitized UTC date, so on a +03:30 server the date filter moved back one day per click, and the format depended on the current culture.

**Stubs and glue in files I don't own (only to keep the build green and satisfy I.1)**
- `src/RugsManagment.Infrastructure/Persistence/Repositories/SpecializedRepositories.cs`, class `RugRepository`:
  - Three `NotImplementedException` stubs for the new interface methods.
  - Explicit arguments in `Project`: `r.SerialNo`, `r.Quantity`, `r.TotalAreaM2`, `false`, `(decimal?)null`, `r.Sale != null ? Sold : InStock`, `null`, `null`, `EF.Property<uint>(r,"xmin")`.
  - Explicit arguments in `ListDeletedAsync`: `r.SerialNo`, `r.Quantity`.
- `tests/RugsManagment.Tests/Fakes.cs`: three `NotSupportedException` members on `FakeRugRepository`, matching the fake's existing style.

Nothing was deleted.

### Verification
| Check | Result |
|---|---|
| `dotnet build RugsManagment_Service.slnx -p:NuGetAudit=false` | 0 errors, 0 warnings |
| Release build with `-warnaserror` | Succeeded |
| Unit tests | 470/470 (429 before + 41 new) |
| Integration tests (with `RUGS_TEST_PG`) | 229/229 (213 before + 16 new) |
| `npm run build` (vue-tsc strict + vite) | Passed |
| `npx vitest run` | 207/207 |

- The integration run includes `SearchAsync` returning rows on Postgres, so the new projection arguments (xmin, sale join, `TotalAreaM2`) translate.
- The only DBs used were the test infrastructure's own `rugs_it_*`, created and dropped automatically. `rugs_management` was not touched, and no git state was changed.

### Could not do / limits
- The real implementations of the three repository methods, the cost columns of the list row, and the Location arms for bale and shipment are WP2-B and P4a work. The stubs are listed above.
- `MoneyRedaction.cs` and `RugsController.cs` have no P2 owner, so I wrote the changes below instead of editing them.

### Needs from others
1. **`App/Common/MoneyRedaction.cs`, owner to assign (suggest p2-app):**
   - `Redact(RugDto)`: add `PurchaseLines = null`.
   - `Redact(RugListItemDto)` and the `PagedResult` overload: `with { TotalInvestment = null, CostPerM2 = null }`.
   - Add `Redact(RugBySerialDto hit) => CanSeeMoney ? hit : hit with { Rug = Redact(hit.Rug) }`.
   - The split endpoint is Floor, so redact both `SplitRugResultDto.Original` and `Created`.
2. **WP2-B:**
   - Implement the three `IRugRepository` methods in the extracted `RugRepository.cs`, following the interface docs.
   - Include `PurchaseLines.OrderBy(SortOrder)` in `GetWithWorkflowAsync`.
   - In `Project`:
     - set `CostComputed = r.CostTotal != null` and `CostPerM2` from `RugCostTotal`;
     - take `TotalInvestment` from `RugCostTotal.Total` (sites 2–4, trash included);
     - use `AreaSquareMeters = r.AreaM2`.
   - Keep my explicit arguments, and do not rely on the compiler (see the CS0854 finding).
   - `ApplyFilters`:
     - `Search` also covers `SerialNo`;
     - `Serial` filter: exact or in-range, invalid text gives no rows;
     - `Location` in P2: Sold means a sale exists, InStock means none, InBale/Exported/Trashed give no rows;
     - until P4a: `ShipmentId`/`BaleId` give no rows, `NotBaled` gives all rugs.
   - `ApplySort` for `Serial`: null `SerialSort` last, then `SerialSort`, `SerialNo`, `Id`.
   - `EntityMappers.ToDto` must fill every appended `RugDto` member:
     - `PurchaseCost` = Σ line amounts;
     - `Version` read through `IConcurrencyGuard`, since xmin is a shadow property;
     - `BatchNumber` and `SellerName` from `Batch`;
     - the new `RugCostSummaryDto` members, with `CostAtSale = Sale?.CostAtSale`.
   - `LedgerVerifier` and `ILedgerMaintenance` should return `CostConsistencyReportDto` and `LedgerRebuildResultDto`, using the `CostMismatchCheck` codes.
3. **p2-app:**
   - Apply the `Values`-means-v2 rule in `RugManagementService`.
   - Use the new DTOs in `RugsApiController`. For values, `RugValuesQuery.TryFromFieldKey` returning false means 400 (needs a `Messages.fa.Rugs` text).
   - The C# `FieldDefDto` must match the I.3 shape, because the TS `FieldDef` mirrors it.
   - Expose `PurchaseCategoryOptionDto` through an endpoint of your choice for the editor.
   - In `Views/Rugs/Index.cshtml`:
     - date inputs use `Model.CreatedFromText`/`CreatedToText`, because the current `q.CreatedFrom?.ToString("yyyy-MM-dd")` has the day-shift bug;
     - add hidden inputs for `serial`, `location`, `withoutBatch`, `shipmentId`, `baleId` and `notBaled` in the GET forms;
     - location chips via `Model.LocationRoute`/`IsLocation`;
     - Serial as the first column, sortable with `RugSortBy.Serial`.
4. **`Web/Controllers/RugsController.cs`, owner to assign:** replace `BuildRoute` with `RedirectToAction(nameof(Index), RugListViewModel.ToRouteValues(displayQuery, page: result.TotalPages))`. Today the redirect drops `withoutBatch` and the P2 filters, and has the same date shift.
5. **P4a:** shipment and bale arms for `Location`, `BaleNumber` and `ShipmentNumber` in `Project`; the `ShipmentId`/`BaleId`/`NotBaled` filters; `IRugDtoEnricher` fills the shipment, bale and USD members of `RugDto`.