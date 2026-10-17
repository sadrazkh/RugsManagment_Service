# RugsManagment_Service: consolidated architecture map for capabilities 1–7

**Repo root:** `C:\Users\sadra\source\repos\RugsManagment_Service` (branch `master`). This map was built read-only from six reader reports. Every claim below comes from those reports.

**Path abbreviations used below**

| Abbrev. | Path |
|---|---|
| `Domain/` | `src/RugsManagment.Domain/` |
| `App/` | `src/RugsManagment.Application/` |
| `Infra/` | `src/RugsManagment.Infrastructure/` |
| `Web/` | `src/RugsManagment.Web/` |
| `CA/` | `src/RugsManagment.Web/ClientApp/src/` |
| `SpecializedRepositories.cs` | `Infra/Persistence/Repositories/SpecializedRepositories.cs` |
| `EntityConfigurations.cs` | `Infra/Persistence/Configurations/EntityConfigurations.cs` |
| `Snapshot` | `Infra/Persistence/Migrations/AppDbContextModelSnapshot.cs` (ProductVersion 10.0.10) |

---

## 1. Current domain model

### 1.1 Base conventions

- **BaseEntity**: `Guid Id = Guid.NewGuid()` is set on the client (`BaseEntity.cs:10`). `CreatedAt = UtcNow` (`:13`). `UpdatedAt?` is set by hand in services (`:16`). There are no DB-side defaults.
  - Because the Id is pre-set, a child added only through a tracked parent's navigation is treated as Modified. That causes UPDATE of 0 rows and then a ConcurrencyConflictException.
  - New child rows must be added with `repo.AddAsync` (`WorkflowManagementService.cs:90-92`, `RugManagementService.cs:338-341, 398-403`).
  - Never call `Update()` on a just-added entity, because it flips the state to Modified (`RugSaleService.cs:70`).
- **One `AppDbContext`** has 16 DbSets (`AppDbContext.cs:12-27`) and uses `ApplyConfigurationsFromAssembly`. It has no SaveChanges override, no interceptors and no tenant filter. All Fluent configuration lives in `EntityConfigurations.cs`.
- **`ITenantScoped`** (`Domain/Common/ITenantScoped.cs`) is a marker that nothing consumes.
- **Enums are stored as int.** `AuditAction` uses explicit, gapped values.

### 1.2 Entities

| Entity (table) | Key fields | Relationships, indexes, filters | Source |
|---|---|---|---|
| **Tenant** (Tenants) | Name(200) req; Slug(100) UNIQUE; IsActive; ContactPhone?; ContactEmail?; SubscriptionExpiresAt?; Currency int (Toman=0/Rial=1, display label only); LogoFileName?; DefaultWorkflowTemplateId? (no FK, never read) | Cascades to Rugs, WorkflowTemplates, RugBatches, ServiceProviders, ProviderPayments, AuditEntries, CustomFieldDefinitions, LabelTemplates, RugImages, RugSales. No concurrency token. Settings are plain columns; there is no settings JSON. | `Tenant.cs:10-42`; `EntityConfigurations.cs:8-16` |
| **User** (Users) | TenantId? (null = SystemAdmin; FK NO ACTION); Email(256) UNIQUE globally; PasswordHash (PBKDF2); FullName(200); Role int (SystemAdmin0/TenantAdmin1/Operator2); IsActive; LastLoginAt? | Login is by email only. No security stamp, no per-user preferences or permissions. | `User.cs:9-25`; `EntityConfigurations.cs:18-27` |
| **ProcessStepType** | TenantId? (null = system, NO FK); Code(50); NameFa/NameEn(100); Icon='circle'; SortOrder; DefaultPricingModel (default PerSquareMeter); DefaultUnitRate numeric; FieldSchemaJson jsonb?; ExpectedDurationDays?; IsActive | UNIQUE(TenantId,Code) at `EntityConfigurations.cs:36` does NOT hold for system rows, because PostgreSQL treats NULLs as distinct. Cascades to RugWorkflowSteps, WorkflowTemplateSteps and ServiceProviderRates, so in-use types are deactivated instead of deleted (`StepTypeService.cs:119-137`). System types are read-only for tenants (`StepTypeService.cs:144-149`). | `ProcessStepType.cs:10-56` |
| **WorkflowTemplate** | TenantId (cascade); Name(200); Description?; IsDefault; IsActive | Deactivated, never removed. | `WorkflowTemplate.cs:9-24`; `EntityConfigurations.cs:45-53` |
| **WorkflowTemplateStep** | WorkflowTemplateId (cascade); ProcessStepTypeId; OrderIndex; IsOptional; DefaultServiceProviderId? (NO ACTION); OverridePricingModel?; OverrideUnitRate?; ConfigJson text? | No TenantId. Configured by convention only. Override*/ConfigJson are stored but never applied (dead). | `WorkflowTemplateStep.cs:9-33`; Snapshot 817-862 |
| **Rug** (Rugs) | TenantId; Sku(50) UNIQUE(TenantId,Sku) including soft-deleted rows; Title/Origin/Pattern/Material text?; KnotDensity?; **WidthMeters/LengthMeters numeric, unbounded, a single pair**; PurchaseCost?; TargetSalePrice?; Status int; ImageUrl? (legacy); Notes?; MetadataJson jsonb?; WorkflowTemplateId? (NO ACTION); BatchId? (SET NULL); CurrentStepIndex; DeletedAt?; DeletedByUserId? (no FK); shadow xmin; ignored `AreaSquareMeters = W*L` (unrounded) | Query filter `DeletedAt == null` (`EntityConfigurations.cs:70`). Index (TenantId, DeletedAt) is NOT partial despite the comment (`:71-72`). xmin at `:77`. WorkflowSteps, Images and Sale (1:1) cascade. | `Rug.cs:9-72`; `EntityConfigurations.cs:55-79`; Snapshot 268-360 |
| **RugBatch** ("گروه/محموله") | TenantId (cascade); Name(200); Description?; ReceivedAt? | Flat, one per rug. No number, ordering, per-member data, totals or xmin. | `RugBatch.cs:6-15`; `EntityConfigurations.cs:105-112` |
| **RugWorkflowStep** | RugId (cascade); ProcessStepTypeId; OrderIndex; IsOptional; Status (Pending0/InProgress1/Completed2/Skipped3/Cancelled4; Cancelled is never set); ServiceProviderId? (NO ACTION); StartedAt?; CompletedAt?; CompletedByUserId?; CompletedByName?; CalculatedCost?; ManualCostOverride?; AppliedPricingModel?; AppliedUnitRate?; PricingConfigJson text?; Adjustment? (negative = discount); FieldValuesJson text?; Notes?; xmin; ignored EffectiveCost | Index (RugId, OrderIndex) at `:243`. xmin at `:246`. Filter `s.Rug.DeletedAt == null` at `:250`. No TenantId; scoped through Rug. | `RugWorkflowStep.cs:10-54`; `EntityConfigurations.cs:239-252` |
| **RugImage** | TenantId; RugId; FileName(120); ThumbnailFileName?; ContentType; SizeBytes; Width; Height; SortOrder; IsPrimary | Maximum 12 per rug (`RugImageService.cs:34`). Single primary is enforced only in the service. Filter at `:229`. | `RugImage.cs:12-38`; `EntityConfigurations.cs:216-233` |
| **RugSale** | TenantId; **RugId UNIQUE (1:1)**; BuyerName(200); BuyerPhone(40)?; SalePrice/Discount/ReceivedAmount numeric(18,2); PaymentMethod; SoldAt; Reference?; Notes?; ignored NetAmount and OutstandingAmount | Unique at `:186`. Filter at `:206`. A sale blocks soft delete of the rug (`RugManagementService.cs:188-189`). | `RugSale.cs:15-53`; `EntityConfigurations.cs:182-210` |
| **ServiceProvider** | TenantId; Name; Specialty?; Phone?; Address?; IsActive; Notes?; Rates and Payments (cascade) | Index (TenantId, Name). Hard delete only when there is no history. | `ServiceProvider.cs:9-34`; `EntityConfigurations.cs:114-129` |
| **ServiceProviderRate** | ServiceProviderId; ProcessStepTypeId; PricingModel; UnitRate numeric(18,2); PricingConfigJson? (never written); Notes? | UNIQUE(ServiceProviderId, ProcessStepTypeId) at `:136`. No TenantId. | `ServiceProviderRate.cs:13-30` |
| **ProviderPayment** | TenantId; ServiceProviderId; Amount(18,2) > 0; PaidAt; Reference?; Notes? | Index (ServiceProviderId, PaidAt). Cannot be edited. Not audited. | `ProviderPayment.cs:11-29` |
| **AuditEntry** | TenantId; UserId?; UserName(200, default 'سیستم'); Action int; EntityType(60); EntityId?; EntityLabel(200)?; Summary(500) | Indexes (TenantId, CreatedAt) and (TenantId, EntityType, EntityId) at `:166-167`. Append-only. | `AuditEntry.cs:12-36` |
| **CustomFieldDefinition** | TenantId; Key(60) UNIQUE(TenantId,Key), normalised to lowercase, immutable; Label(120); FieldType (Text0/Number1/Date2/Select3/Boolean4); OptionsJson jsonb?; IsRequired; SortOrder; IsActive | Applies to Rug only. Values are written to `Rug.MetadataJson[Key]`. Hard delete leaves orphan keys behind. | `CustomFieldDefinition.cs:10-30`; `EntityConfigurations.cs:82-92` |
| **LabelTemplate** | TenantId (cascade); Name(200); WidthMm=90; HeightMm=50 (clamped to 20..300); Mode (Visual0/Html1); ElementsJson jsonb?; HtmlContent? | No IsDefault, Kind, Version or orientation. Both JSON and HTML are saved regardless of Mode. | `LabelTemplate.cs:10-28`; `EntityConfigurations.cs:95-103` |

**Relationship sketch**

- Tenant → Users, Rugs, RugBatches, WorkflowTemplates, ServiceProviders, CustomFieldDefinitions, LabelTemplates, AuditEntries, and so on.
- Rug → Batch (0..1, SET NULL), WorkflowTemplate (0..1), WorkflowSteps (n), Images (n), Sale (0..1).
- RugWorkflowStep → ProcessStepType and ServiceProvider (0..1).
- WorkflowTemplate → WorkflowTemplateSteps → ProcessStepType.
- ServiceProvider → Rates (per ProcessStepType) and Payments.

### 1.3 JSON payloads

These are string properties, so LINQ cannot reach into their keys.

| Column | Type | Shape |
|---|---|---|
| `Rug.MetadataJson` | jsonb | Flat `{key: string}`. RugForm writes every value as a string. The seed uses `{"bg_color": ...}`. |
| `ProcessStepType.FieldSchemaJson` | jsonb | `StepFieldDefinition[]` `{key (ASCII [A-Za-z0-9_]), label, type 0-4, required, options?, hint?}`, at most 15 fields (`StepFieldSchema.cs:42, 87`). |
| `RugWorkflowStep.PricingConfigJson` | text | `CombinedPricingConfig {items:[{model:int, rate:decimal}]}`. No label per item. Read case-insensitively (`CostCalculationService.cs:124, 127-136`). |
| `RugWorkflowStep.FieldValuesJson` | text | Sanitised values matching the step schema. |
| `LabelTemplate.ElementsJson` | jsonb | `LabelLayout {columns, elements[]}`, defined only in TypeScript (`CA/lib/labelFields.ts:7-27`). |
| `CustomFieldDefinition.OptionsJson` | jsonb | `string[]` |
| `WorkflowTemplateStep.ConfigJson`, `ServiceProviderRate.PricingConfigJson` | text | Never used. |

### 1.4 Delete behaviours (Snapshot 864-1087)

- **Tenant** cascades to everything it owns.
- **Rug** cascades to WorkflowSteps, Images and Sale.
- **Rug.BatchId** is SET NULL.
- **NO ACTION FKs:** Rug.WorkflowTemplateId, User.TenantId, RugWorkflowStep.ServiceProviderId, WorkflowTemplateStep.DefaultServiceProviderId. A hard delete of the principal would surface as a raw 500, because UnitOfWork does not map 23503.
- **ProcessStepType** cascades to steps, template steps and rates. **ProcessStepType.TenantId has no FK**, so tenant-private step types are orphaned when a tenant is deleted.

### 1.5 Enums

| Enum | Values | Source |
|---|---|---|
| RugStatus | Draft0, InProgress1, ReadyForSale2, Sold3, Archived4 | `RugStatus.cs` |
| WorkflowStepStatus | Pending0, InProgress1, Completed2, Skipped3, Cancelled4 | `WorkflowStepStatus.cs` |
| StepPricingModel | Fixed0, PerSquareMeter1, PerSquareFoot2, CustomFormula3 (acts as Fixed), PerLength4, PerWidth5, Combined6 | `StepPricingModel.cs` |
| CustomFieldType | Text0, Number1, Date2, Select3, Boolean4 (shared by custom fields and step schemas) | `CustomFieldType.cs` |
| CurrencyUnit | Toman0, Rial1 (label only, `CurrencyUnit.cs:3-13`) | |
| UserRole | SystemAdmin0, TenantAdmin1, Operator2 | |
| LabelMode | Visual0, Html1 | |
| PaymentMethod | Cash, Card, Transfer, Cheque, Installment | |
| AuditAction | Created0, Updated1, Deleted2, Restored3, StepAdvanced10, StepReverted11, StepSkipped12, WorkflowChanged13, SaleRecorded20, SaleCancelled21, ProviderPaid30, UserInvited40, UserDisabled41, PasswordChanged42, SettingsChanged50 | |

### 1.6 Concepts that do not exist anywhere

- Export shipment or declaration ("اظهار")
- Bale ("نگله/عدل") or bale item
- Initial vs final dimensions
- Purchase-price components
- Cost entry, lump-sum cost or allocation
- Quantity or lot, serial range, merchant serial number
- USD, any second currency, FX rate
- Export or document template, xlsx import or export
- Supplier or seller on a rug (only batch-level ReceivedAt)
- Tenant-level preferences beyond the four scalar columns

---

## 2. Cost and pricing semantics today

### 2.1 Formula engine: `App/Services/CostCalculationService.cs`

`CostCalculationService` is documented as the only place pricing formulas live (`:10-13`; `ICostCalculationService.cs:7`). It is pure: no EF, operating on in-memory entities.

**ComputeSingle (`:76-88`).** `area = W*L`, not rounded.

| Model | Result |
|---|---|
| Fixed | `rate` (not rounded) |
| PerSquareMeter | `Math.Round(rate*area, 2)` |
| PerLength | `Math.Round(rate*L, 2)` |
| PerWidth | `Math.Round(rate*W, 2)` |
| PerSquareFoot | `Math.Round(rate*area*10.7639m, 2)` (approximate; the exact factor is 10.7639104) |
| Anything else | `rate`, so CustomFormula=3, and Combined without JSON, behave as Fixed |

**Combined (`:32-37, 90-96`).** Applies only when `combinedJson` is not blank. Each item is rounded to 2 decimals and then the items are summed. An empty list gives 0. Broken JSON gives 0 without throwing (`:98-102`; test `CostCalculationServiceTests.cs:78-84`). A StepType whose default model is Combined has nowhere to store default JSON, so it silently becomes Fixed `DefaultUnitRate`.

**CalculateStepCost precedence (`:16-30`).**
1. A `manualOverride` argument wins outright.
2. Model = `stepInstance.AppliedPricingModel ?? overrideModel ?? stepType.DefaultPricingModel`.
3. Rate = `AppliedUnitRate ?? overrideRate ?? DefaultUnitRate`.

Model and rate resolve independently, so a mismatched pair is possible (for example a Fixed 150,000 rate used as per-m²). The engine reads only the rug's current `WidthMeters/LengthMeters` (`:29`). It has no concept of quantity, currency or which set of dimensions to use.

**Preview (`:39-72`).** `POST /api/pricing/preview` returns `{base, adjustment, total, formula, components[{label, amount}]}`, with `total = max(0, base + adj)` (`:70`). Labels are built on the server with invariant `#,0.##` (`:105-122`).

**Rounding.** `Math.Round` uses the .NET default, ToEven (banker's rounding). Excel's ROUND rounds half away from zero. `RugForm.vue:35-38` rounds half-up with JS. Server `AreaSquareMeters` is unrounded. That gives three different area-rounding behaviours. Totals are never re-rounded.

**Precision.** Rug and step money and dimensions use unbounded `numeric` (Snapshot ~295-337, 519-560). Sales, payments and rates use `numeric(18,2)` (`EntityConfigurations.cs:137, 150`).

### 2.2 Effective cost and total investment

- `EffectiveCost = Math.Max(0, (ManualCostOverride ?? CalculatedCost ?? 0) + (Adjustment ?? 0))` (`RugWorkflowStep.cs:53`). Each step is floored at 0 before summing, so a discount can never lower another step or the purchase price. EF ignores this property (`EntityConfigurations.cs:245`), so every SQL site reimplements it with a CASE.
- `TotalInvestment = (PurchaseCost ?? 0) + Σ EffectiveCost` over steps whose status is **Completed or InProgress**. Pending, Skipped and Cancelled count nothing. `EstimatedMargin = TargetSalePrice − total`, or null (`WorkflowEngine.CalculateRugCosts`, `WorkflowEngine.cs:302-315`; tests `WorkflowEngineTests.cs:206-244`).

**The same formula is duplicated.** All copies must stay identical, and every one of them must change for any new cost source:

| # | Site | Purpose |
|---|---|---|
| 1 | `WorkflowEngine.cs:302-315` (C#) | Used by `EntityMappers.ToDto` (`EntityMappers.cs:73`), dashboard (`:167, 188-208`), `AnalyticsService.cs:126` (step breakdown, Completed only), `:174` (monthly profit) and `RugSaleService.cs:174` |
| 2 | `SpecializedRepositories.cs:147-152` | `TotalInvestmentExpression`, used for sorting |
| 3 | `SpecializedRepositories.cs:196-200` | `Project`, the list row |
| 4 | `SpecializedRepositories.cs:295-299` | `ListDeletedAsync`, the trash page |
| 5 | `SpecializedRepositories.cs:423-432` (in 413-440) | Provider `ListBalancesAsync`; per-step only, Completed and InProgress shown separately |
| 6 | `SpecializedRepositories.cs:458-460` (in 442-461) | Provider `ListWorkAsync` |
| 7 | `DatabaseSeeder.cs:152` | Seed |

The comments at `SpecializedRepositories.cs:142-146, 162-166, 206-211` require that these stay in sync. They agree semantically today, but **no test asserts C#/SQL parity**, because there are no repository or integration tests. Every load that feeds `CalculateRugCosts` must also Include any new cost data: `GetWithWorkflowAsync`, `ListByTenantAsync`, and the batch and sales loads.

### 2.3 Where costs are written (all snapshots)

- **AdvanceStepAsync (`WorkflowEngine.cs:89-125`) and UpdateStepPricingAsync (`:127-153`)** recompute `CalculatedCost` from the rug's current W/L. `RecalculateStepCost` (`:342-354`) is private and never called.
- **Nothing recomputes on change.** Editing rug dimensions (`RugManagementService.cs:161-162`), step-type defaults or provider rates leaves stored step costs unchanged. Re-pricing an old step later silently switches it to the new dimensions.
- **Template path vs custom path diverge.** `InitializeWorkflowFromTemplateAsync → MapTemplateStep` (`:318-330`) sets no `CalculatedCost` and ignores template overrides, so template rugs cost 0 until each step is priced. The custom and path-edit routes compute a default cost at build time through `ApplyPricing` (`:75, 288, 332-340`), but do not store the override in `Applied*`, so the next Advance reverts to the default.
- **ApplyStepPricing (`:356-371`)** overwrites Model, Rate and Adjustment only when they carry a value. If a request has a PricingModel but no manual value, it **clears the stored ManualCostOverride** (`:366-367`).
- **Provider rate injection** (`RugManagementService.ApplyProviderRateAsync`, `:530-548`) runs only when the request has none of ManualCostOverride, UnitRate or PricingModel. It then uses `FindRateAsync` (tenant-checked, `ServiceProviderService.cs:174-180`). The documented order (`:527-528`) is manual → explicit rate → provider agreed rate → step-type default. The provider rate's `PricingConfigJson` is ignored and never persisted.
- **UI cost dialog bug.** `CostRuleEditor.toPayload` (`CA/components/CostRuleEditor.vue:54-62`) always sends `pricingModel = state.model` (default 1) and `unitRate ?? 0`. Completing a step from `RugWorkflowPanel` (`movePayload`, `RugWorkflowPanel.vue:130-138`) without typing a rate therefore stores cost 0, and provider injection never runs. The dialog is never pre-filled from existing values. The modal's date field (`:323`) is never sent; `AdvanceStepRequest` has no CompletedAt.
- **Quick and bulk paths send all-null pricing** (`ListQuickAdvance.vue:31-34`, `GroupDetail.vue:97-99`, `RugBulkToolbar.vue:107-111`). Single quick-advance gets the provider or default rate. `BulkAdvanceAsync` (`RugManagementService.cs:405-430`) skips provider-rate injection, field validation, CompletedBy stamping and audit.
- **"Save only"** (`UpdateStepPricingAsync`) drops `FieldValuesJson` (`WorkflowEngine.cs:138-142`). The service does not validate it either (`RugManagementService.cs:267-280`).
- **ActivateStepAsync (`:218-253`)** marks earlier steps Completed without pricing them. They still count toward investment and provider balances.

### 2.4 Provider ledger

- `CompletedWorkTotal = Σ EffectiveCost` of Completed steps for the provider. `InProgressTotal` is shown separately. `PaidTotal = Σ payments`. `Balance = Completed − Paid`; a negative value means prepaid. All of this is computed in SQL and never stored (`SpecializedRepositories.cs:413-440`; `ProviderDtos.cs:46-62`).
- `ListBalancesAsync` filters only by ServiceProviderId. `ListWorkAsync` also filters by `s.Rug.TenantId`.
- Payments (`ServiceProviderService.cs:135-172`): amount must be > 0 and is converted to UTC. They can be deleted but not edited, and are not linked to specific steps. Rates are upserted by diffing on ProcessStepTypeId (`:207-244`).

### 2.5 Money and currency

Money is stored without a unit. `Tenant.Currency` is a display label only. Razor views use `@Tenant.Currency` (`Dashboard/Index.cshtml:69`, `Rugs/Index.cshtml`), while islands hard-code "تومان" (`CA/lib/format.ts:18-21`, `RugForm.vue:177-185`, `CostRuleEditor.vue:100-148`). A comment at `ProcessStepType.cs:35` says Rial, but the default is Toman. Profit on sold rugs is recomputed live and never snapshotted at sale time (`EntityMappers.cs:148-155`, `RugSaleService.cs:174`, `AnalyticsService.cs:174`).

**Front-end model menus disagree with each other:**
- `CostRuleEditor.vue:17-22` has no PerSquareFoot.
- `StepTypeManager.vue:48-54` has Combined but no PerSquareFoot.
- `ProvidersManager.vue:35-41` has PerSquareFoot but no Combined.
- `AppliedPricingModel` is serialised as a string (`EntityMappers.cs:136`), while every other enum is a number.

---

## 3. Rug lifecycle and existing features, with extension points

| Feature | How it works | Extension points |
|---|---|---|
| **Create (intake)** | `RugForm.vue:87-115` checks W/L > 0 and required custom fields on the client only, POSTs `/api/rugs`, then redirects to Details (`:110`). The server `CreateOnceAsync` (`RugManagementService.cs:97-149`) generates the SKU, sets Status Draft (`:116`), applies the template or custom steps through the engine, adds the rug, audits, saves, and reloads the full graph. It retries 5 times on DuplicateKey (`:81-95`), but the retry is broken (see §8). There is no server validation of W/L or PurchaseCost, and no `PersianText.Normalize` on Create/Update (`:104-114, 156-168`). | `RugManagementService.cs`, `App/DTOs/Rugs/RugDtos.cs:71-140`, `CA/islands/RugForm.vue`, `Web/Controllers/Api/RugsApiController.cs` |
| **SKU** | `GenerateNextSkuAsync` (`SpecializedRepositories.cs:256-275`) uses prefix `RUG-{UTC yyyyMM}-` (Gregorian), takes the max suffix + 1 across all rows including soft-deleted ones (`IgnoreQueryFilters`, `:260-263`), formatted D4. The unique (TenantId,Sku) index (`EntityConfigurations.cs:59`) is the backstop. SKUs are allocated one at a time from committed rows. | Same file |
| **Update** | `UpdateAsync` (`RugManagementService.cs:151-174`) uses `request.X ?? rug.X`, so a field can never be cleared. W/L are always overwritten. Status can be set to any value (`:165`). There is no recompute. | Same |
| **Soft delete / trash** | Blocked when a sale exists (`:188-189`). Restore at `:201-218`. Trash, Delete and Restore are TenantAdmin-only MVC actions. | `Web/Controllers/RugsController.cs`, `Views/Rugs/Trash.cshtml` |
| **Status machine** | Draft on create. The template or custom path moves to InProgress (`WorkflowEngine.cs:41, 79`), or ReadyForSale when nothing is pending (`:396-399`). Advance calls MoveToNextStep (`:408-425`). GoBack and Activate set InProgress (`:214, 251`). Saving a sale sets Sold (`RugSaleService.cs:75`). Cancelling a sale calls `ResolveStatusFromWorkflow` (`WorkflowEngine.cs:379-388`, which returns only Draft, InProgress or ReadyForSale). Archived is manual only. The bulk UI offers statuses 0,1,2,4 (`RugBulkToolbar.vue:16-21`), but the API accepts any value. | Status labels are duplicated in `Web/Support/DisplayHelpers.cs:24-31`, `labelFields.ts:46`, `GroupDetail.vue:35`, `RugBulkToolbar.vue:16-21`, `Views/Rugs/Index.cshtml:11-19`, `Dashboard/Index.cshtml:8-16`; status checks also in `AnalyticsService.cs:51` and `EntityMappers.cs:190-214` |
| **List / search** | `SearchAsync` (`SpecializedRepositories.cs:70-89`): Sanitized (page size 5..100, default 25; `RugListDtos.cs:99-106`), then filters (Status, BatchId, WithoutBatch, StepTypeId with an InProgress step, CreatedFrom/To, Search), then Count (early return on 0), then sort on the entity with `ThenBy(Id)` (`:212-248`), then Project with correlated subqueries (`:167-204`), then Skip/Take. Search is ILIKE over Sku, Title, Origin, Pattern, Material and Notes. The needle goes through full `PersianText.Normalize` (`PersianText.cs:48-83`), but the column side only replaces ي and ك (`:113-137`). MetadataJson is not searched. Sort keys (`RugSortBy`, `RugListDtos.cs:51-60`): CreatedAt, Sku, Title, Area, TotalCost, Status, CurrentStep. The controller converts the chosen dates to Iran-day UTC (`RugsController.cs:30-35`). `withoutBatch` is bindable but is not kept in links (`RugListViewModel.cs:55-86`). | `RugQuery` (`RugListDtos.cs:66-117`), `ApplyFilters`/`ApplySort`/`Project`, `Views/Rugs/Index.cshtml` (cards below md, table at md and up, `:226-380`) |
| **Bulk operations** | BulkAdvance (`:405-430`), BulkGoBack (`:375-396`), BulkUpdateWorkflow (`:493-521`) and BulkUpdateFields (`:432-487`) each load every rug with `GetWithWorkflowAsync` (N+1), catch errors per item, and call ONE SaveChanges at the end. BulkUpdateFields treats null as "don't touch" and `Guid.Empty` BatchId as "remove from group" (`:465-467`), runs `Blank()` normalisation (`:490-491`), and writes one aggregate audit row (`:479-483`). Supported fields are Origin, Pattern, Material, KnotDensity, TargetSalePrice, Status and BatchId; Title, W/L, PurchaseCost, Notes and metadata are not supported. | `RugDtos.cs:125-155` (`BulkOperationResultDto`, `BulkItemErrorDto`), `CA/islands/RugBulkToolbar.vue` (tick-to-apply UI `:29-83`, sticky bar `:171-326`, reads `[data-rug-select]` at `:85-94`) |
| **Batches / groups** | `RugBatchService` (`App/Services/RugBatchService.cs`). Create defaults ReceivedAt to now (`:37`). Assign sets `rug.BatchId` per rug (`:70-83`). Delete nulls the FK. ToDto builds an "N× stepName" summary (`:101-116`), but the list query does not include steps, so the summary is always null. Pages: `GroupsController.cs`. `GroupDetail.vue` buckets rugs by current step (`:48-56`), caps lists at 100 (`:19, 67-71`), and supports split, move and add-unassigned. Split uses `prompt()` (`:113`). It has its own fetch helper for DELETE with a body (`:87-92`). | `RugBatch.cs`, `SpecializedRepositories.cs:369-384`, `BatchesApiController.cs` |
| **Custom fields** | `CustomFieldService.cs`: normalises keys (`:82-89`; note that `char.IsLetterOrDigit` allows Persian letters) and rejects duplicate keys (`:28-49`). Update cannot change the key (`:51-64`). Delete is a hard delete (`:66-71`). Cross-tenant access gives 403 (`:77-78`). The lookup returns active fields only (`LookupsApiController.cs:23-25`). `RugForm.vue:198-219` renders by type and keeps every value as a string; dates use native Gregorian inputs. Unknown or inactive keys survive a round trip (`:66-68`). **The server never validates MetadataJson** (`RugManagementService.cs:115, 168`). `Details.cshtml:121-126` shows raw keys for orphan fields. `CustomFieldsManager.vue` can add, toggle and delete only (`:44-70`). | `CustomFieldDefinition.cs`, `CustomFieldsApiController.cs` (TenantAdmin), `StepFieldSchema.ValidateValues` (`App/Common/StepFieldSchema.cs:104-170`) is the reusable validator |
| **Workflow / step types / templates** | Engine state machine (`WorkflowEngine.cs`). Skip is allowed only on optional steps (`:158-170`). GoBack is at `:172-216`. `UpdateWorkflowPathAsync` (`:255-297`) keeps Completed and Skipped steps. Required step fields are enforced on advance (`RugManagementService.cs:233-237`). Islands: `RugWorkflowPanel`, `StepTypeManager`, `WorkflowTemplateEditor` (always sends null overrides and provider). | `IWorkflowEngine.cs:8-85`, `StepTypeService.cs`, `WorkflowManagementService.cs` |
| **Providers** | CRUD, rates, statement and settle (`ProvidersManager.vue`, `ProviderStatement.vue:69-71`). The `/api/providers*` endpoints are TenantAdmin only (`ProvidersApiController.cs:15`). | `ServiceProviderService.cs` |
| **Sales** | One sale per rug. `SaveAsync` validates buyer, price > 0, discount and received ranges (`RugSaleService.cs:151-170`). The report is N+1 (`:126-131`) and uses live investment. Outputs are the `/Sales` page, CSV and Print, all TenantAdmin. | `SalesController.cs`, `SalesApiController.cs`, `RugSalePanel.vue`, `SalesReport.vue` |
| **Labels** | `LabelTemplate` entity with CRUD (`LabelTemplateService.cs`, `/api/labels`). Designer: `LabelDesigner.vue` (palette, VueDraggable grid, properties panel, sample preview from `/api/rugs/samples`, popup print `:117-138`). Rendering: `LabelRender.vue`, one rug per render. Field catalogue: `labelFields.ts` (`STATIC_FIELDS` with 14 keys `:29-44`, `resolveField` `:54-74` falling back to metadata, `{{key}}` `fillPlaceholders` `:77-79`). Printing: `LabelPrint.vue` (per-rug GETs, copies 1..10, injected `@page`, one label per page). Entry points: `RugBulkToolbar.vue:132-138` and `Details.cshtml:39-43`. | Precedent for the export designer (§7, capability 4) |
| **Analytics / dashboard** | `AnalyticsService` loads all rugs. Aging (`:43-111`), step cost breakdown (`:116-145`), Jalali monthly trend (`:150-178`). Charts use dependency-free `BarChart.vue` and `LineChart.vue`. There is no export or print. The dashboard counts rugs as units (`EntityMappers.cs:205, 210-214`). | `AnalyticsApiController.cs`, `ReportsController.cs`, `AnalyticsDashboard.vue` |
| **Audit / activity** | `AuditLog.Record` adds the entry to the same DbContext, so it commits atomically. It skips when there is no tenant, truncates labels and summaries, and swallows errors (`Infra/Persistence/AuditLog.cs:22-50`). `ActivityLogService` loads all entries into memory before paging (`:20-42`). Adding an action means changing `AuditAction.cs`, `ActivityLogService.ActionLabel` (`:65-83`) and `Views/Settings/Activity.cshtml:13-39`. | |
| **Tenant settings** | `SettingsController.Tenant` GET/POST (`:28-56`) and Logo (`:59-85`), backed by `TenantSettingsService` (`:29-60`, audited as SettingsChanged). `TenantSettingsAccessor` (request-cached, sync-over-async, `:22-45`) exposes `@Tenant.Currency`, `LogoUrl` and `Money()`. The logo appears only on the settings page. DefaultWorkflowTemplateId is dead. | `Tenant.cs`, `TenantSettingsDtos.cs`, `Views/Settings/Tenant.cshtml` |
| **Images** | `RugImageService.cs`: magic-byte checks and at most 12 images. `LocalImageStorage` stores `{root}/{tenant}/{rug}/{guid}.ext` outside wwwroot, with an extension whitelist (others become `.bin`, `:118-122`). Images are served by `MediaController.cs:54-77`, tenant-checked. | |

**Second host.** `src/RugsManagment.Api` (JWT) also consumes `IRugManagementService` and `IRugBatchService`. It is not in `RugsManagment_Service.slnx` and is not built by CI, so DTO changes there go unchecked.

---

## 4. Frontend architecture and UX primitives

### 4.1 Architecture

- **Razor host with Vue 3 islands.** An element `<div data-island="name" data-props='@Json.Serialize(...)' data-skeleton="18rem|off">` is found by `CA/main.ts:48-73`. It looks the name up in `CA/islands/registry.ts:5-25` (19 lazy imports), shows a skeleton, mounts the component, and shows a Persian error box on failure (`:66-72`). Invalid props become `{}`.
- **Each island is its own Vue app.** There is no Pinia or router. Shared state uses module-level reactive singletons (`ui.ts`, `offlineQueue.ts`) or DOM data attributes. Behaviour-only islands enhance Razor markup: `list-quick-advance` uses `[data-advance-*]`, and `rug-bulk-toolbar` uses `[data-rug-select]`.
- **UiShell** is mounted automatically (`main.ts:79-88`, `UiShell.vue`). It renders toasts (aria-live, `:88-119`), the confirm alertdialog (focus trap, Esc, `:44-83, 121-180`), the Ctrl+K `CommandPalette` and the `OfflineIndicator`, and sets `window.rugsUI = {toast, confirm}` (`ui.ts:128-130`) for `wwwroot/js/site.js`.
- **Build.** Vite, single entry `src/main.ts`, `base /dist/`, manifest written to `wwwroot/dist` (`vite.config.ts`). `ViteAssets.cs:40-52` reads the manifest. `npm run build` runs `vue-tsc --noEmit` (strict) before `vite build`. The HMR dev-server mode (`ViteAssets.cs:25, 32-37`) is blocked by CSP. In practice the dev loop is `npm run watch`.
- **Legacy code to ignore:** the `/client` SPA (router, pinia, axios, JWT) and `docs/راهنمای-کد-فارسی.md:38-46`, which describes it.

### 4.2 Reusable primitives

| Primitive | Location | Notes |
|---|---|---|
| API helper | `CA/lib/api.ts` | Sends X-CSRF-TOKEN from `<meta name=csrf-token>` (`:6-19`; `_Layout.cshtml:65`). Errors become `Error(data.message \|\| 'خطا در ارتباط با سرور')` (`:21-30`), so ValidationProblemDetails details are lost. Has `get/post/put/del`; `del` takes no body (`:37-42`). `sendOrQueue` (`:57-81`) queues only on network errors. No multipart, upload or blob-download helpers. |
| Offline queue | `CA/lib/offlineQueue.ts` | localStorage key `rugs-offline-queue`, not scoped to user or tenant (`:17`). 30 s cross-tab lock. Replays in order and stops at the first network error. Only `ListQuickAdvance` uses it. |
| Toast / confirm | `CA/lib/ui.ts` | `toast.success/error/warning/info` (`:65-70`; errors 8 s, warnings 6 s, others 4 s). `toastAfterReload` (`:72-100`). `confirmDialog({title, message, confirmLabel, cancelLabel, danger}) → Promise<boolean>` (`:106-116`). |
| Money input | `CA/components/MoneyInput.vue`, `CA/lib/money.ts` | Integer only (display rounds to 0 decimals on every keystroke). `parseNumber` drops `٫` and `-` (`money.ts:27-32`). `normalizeDigits` converts Persian and Arabic digits (`:8-18`). LTR, end-aligned, optional suffix. |
| Formatting | `CA/lib/format.ts`; server `Web/Support/PersianFormat.cs` | `faNumber(v, maxFrac)` uses en-US grouping, then swaps digits. `faMoney` hard-codes " تومان". `faDate`/`faDateTime` use `fa-IR-u-ca-persian` in Asia/Tehran. Server `Money` uses `#,0` with no decimals, `Number` uses `0.##` with no grouping, plus `Count` and `IranDayStartUtc/EndUtc` (`:104-119`). There is no dotted Jalali date format (`1405.06.15`) and no USD formatter. |
| Icons | `wwwroot/icons.svg` (35 Lucide-style symbols); `AppIcon.vue`; `<icon>` tag helper (`IconTagHelper.cs`) | Missing icons: upload/import, spreadsheet/table, ruler, box/bale, truck/ship, dollar, copy, calculator, grip, chevron-left/right, eye, home, help, more. |
| Design tokens | `CA/styles/app.css` | Tailwind v4 `@theme` (`:22-91`): surface ladder, on-surface/variant, outline; primary `#6b0008`, secondary gold, tertiary navy; success, warning, error; chart-1..4; aging-*. Vazirmatn font (from jsDelivr). Radius lg/xl. `.dark` overrides at `:99-141`. Base rules: 16 px root, line-height 1.75, tabular-nums via `[data-numeric]`, focus ring, reduced motion (`:148-217`). Utilities `.touch-target` (44 px), `.skeleton`, `pb-safe` (`:224-272`). Print rule `:275-282`. Tailwind scans only ClientApp and `Views/**/*.cshtml` (`:7`). |
| Recurring idioms | | Card: `rounded-xl border border-outline-variant bg-surface-container-lowest p-5 shadow-sm`. Primary button: `min-h-11 rounded-lg bg-primary px-4 font-semibold text-on-primary`. Chips, `data-numeric`, `font-mono dir=ltr` for codes. Status triples (label, chip class, icon) in `DisplayHelpers.cs:24-54`. |
| Navigation | `Web/Views/Shared/_Layout.cshtml:26-53` | `navItems` tuple per role: SystemAdmin 1 item, TenantAdmin 13, Operator 6. Sidebar on md and up, drawer on mobile (`site.js:35-104`). Bottom nav is `navItems.Take(5)` (`:56, 230-241`), which gives Dashboard, Rugs, Groups, Workflows, Labels. The command palette scrapes `#app-sidebar` links (`CommandPalette.vue:56-65`) and searches rugs through `/api/rugs?search=` (`:83-106`). |
| Hotkeys | `CommandPalette.vue:162-195` | Ctrl+K works everywhere. `n` (new rug) and `/` (search) fire whenever focus is not in INPUT, TEXTAREA, SELECT or contentEditable. |
| Forms and pickers | | Dynamic fields: `StepFieldsForm.vue` (schema to form; `toPayload`, `missingRequired`) and RugForm's custom-field renderer. Cost editor: `CostRuleEditor.vue`. Bulk bar: `RugBulkToolbar`. Grouped columns: `GroupDetail.vue:172-209`. Drag-and-drop: `vue-draggable-plus`. Codes: `QrImage.vue`, `BarcodeImage.vue`. Camera: `QrScanner.vue` (BarcodeDetector, qr_code only). |
| Print | `Views/Sales/Print.cshtml`, `LabelPrint.vue` | Standalone print page (Layout=null, A4 landscape `@@page`, repeated `thead`, `tfoot` totals, nonce'd button). In-layout print (`@page` injected, page breaks). |
| site.js behaviours | `wwwroot/js/site.js` | Theme toggle; drawer; TempData-to-toast bridge (always shown as success, `_Layout.cshtml:222`); `form[data-confirm]`; `[data-autosubmit]` with 450 ms debounce. |
| PWA | `sw.js`, `manifest.webmanifest` | Cache-first for `/dist`, `/lib`, `/js`. Offline page. Manifest locks `portrait-primary` (`:10`). |

Also installed but unused: `@vueuse/core`.

Missing entirely:
- Shared Modal, Input, Select, Button or Field components. Each island has its own scoped `.fld`/`.input`.
- Stepper or wizard
- Editable grid
- Jalali date picker
- Decimal NumberInput
- Combobox with "add new option"
- Onboarding or empty-state framework
- xlsx library

### 4.3 UX assessment for a non-technical bazaar merchant

**Fit with the workflow.** The information architecture is built around a workshop process (workflow steps, step types, cost formulas, label designer, providers), not an exporter's flow of intake, baling, costs and documents. The TenantAdmin sees 13 nav items plus 4 settings pages.

**Too technical.**
- The text is dense and often small: `text-xs`/`text-sm`, plus 0.65–0.7rem kbd, bottom-nav and chips (`_Layout.cshtml:165, 236`; `RugWorkflowPanel.vue:271`; `CommandPalette.vue:299`).
- The jargon is engineer-facing: SKU, QR, mm, HTML, `{{sku}}`, "کلید (انگلیسی)" (`CustomFieldsManager.vue:84`), "نرخ × مساحت", "ترکیبی".
- The mobile header packs up to 7 icon buttons.

**Hard to enter data with.**
- The form handles one rug per page and redirects after every save.
- Date inputs are native Gregorian pickers everywhere (`Rugs/Index.cshtml:160, 166`; `StepFieldsForm.vue:98`; `RugWorkflowPanel.vue:323`; RugSalePanel; SalesReport; ProviderStatement; `Activity.cshtml:67, 73`).
- Decimals cannot be typed into money fields.
- A Persian keyboard's `٫` corrupts numbers.
- Native `type=number` fields may reject Persian digits.

**Inconsistent.**
- `bg-white` breaks dark mode in RugForm, LabelDesigner, CustomFieldsManager, WorkflowTemplateEditor, RugGallery and PwaInstall.
- `GroupDetail` uses `prompt()`.
- RugWorkflowPanel's modal (`:309-344`) has no dialog role, focus trap or Esc.
- Some icons are text glyphs (`✕ ⠿ ⋮⋮`).
- Islands say Toman while Razor views say Rial.
- Mutations reload the whole page and lose scroll position and selection (`RugBulkToolbar.vue:145`; `RugWorkflowPanel.vue:160, 166, 173`).

**Reusable strengths.** 44 px targets, focus rings, aria-live toasts, an accessible confirm dialog, the command palette, the offline queue, the QR scanner, the bulk selection bar and the grouped-columns board.

**Direction suggested by the readers.**
- Make a Shipments/Declarations hub the home page.
- Give each shipment a sticky stepper (Intake, Baling, Costs, Output) showing counts and completion.
- Give the merchant a simplified sidebar: خانه، محموله‌ها، انبار فرش، هزینه‌ها، خروجی‌ها, with admin-only "تنظیمات" collapsed.
- Define the bottom nav explicitly instead of using `Take(5)`.

---

## 5. Platform constraints every new piece must obey

1. **Clean Architecture.**
   - Application references only Domain, FluentValidation and DI abstractions; there is no EF.
   - Queries that need EF (projections, `IgnoreQueryFilters`, `EF.Functions`) go in Infrastructure repositories behind `App/Abstractions/Persistence` interfaces. Projection DTOs live in Application.
   - Pure calculators such as the allocator belong in Application. The xlsx library goes in Infrastructure (or `Web/Support`) behind an Application abstraction.
2. **Tenant isolation is manual.**
   - Every query must include `TenantId == tenantId`, taken from `User.RequireTenantId()` (`ClaimsPrincipalExtensions.cs:29-30`).
   - Generic `Repository<T>.GetByIdAsync` is `FindAsync` with no tenant check (`Repository.cs:14-15`).
   - Cross-tenant access should throw `KeyNotFoundException`, which maps to 404. Some existing services throw `UnauthorizedAccessException` (403) instead: `CustomFieldService.cs:77-78`, `UserManagementService.cs:69-70`, `LabelTemplateService.cs:62-65`. Do not copy that.
   - Consider a tenant `HasQueryFilter` for new tables.
3. **Soft delete.** New entities that hang off Rug through a required FK (bale items, allocations, purchase lines) must add `HasQueryFilter(x => x.Rug.DeletedAt == null)`, following `EntityConfigurations.cs:206, 229, 250`. Otherwise EF warns and trashed rugs leak into totals.
4. **Concurrency.**
   - Use `builder.Property<uint>("xmin").IsRowVersion()`, and delete the generated `AddColumn`/`CreateTable` xmin line from the migration by hand (`ConcurrencyAndStepIndex.cs:7-13`).
   - xmin is not in any DTO today, so spreadsheet editing would need it exposed.
   - Bulk operations with one SaveChanges are all-or-nothing.
   - Tenant has no concurrency token, so tenant-level counters need one or a DB sequence.
5. **UnitOfWork mapping** (`UnitOfWork.cs:19-33`): concurrency maps to `ConcurrencyConflictException` (409) and 23505 maps to `DuplicateKeyException` (409). FK violations (23503) and invalid jsonb become raw 500s, so validate first.
6. **Adding children.** Use `AddAsync` for new child rows. `Repository.Update` marks every column Modified. `ListAsync` returns untracked entities.
7. **Cost-formula parity.** Every new cost source must reach all sites listed in §2.2, or the total must be centralised or denormalised. Expressions must stay translatable to SQL: use inline `string.Replace` with constants and no helper calls (`SpecializedRepositories.cs:115-117, 143-146`). Sort on the entity before Project, and always add `ThenBy(Id)`.
8. **Uniqueness and enums.** PostgreSQL treats NULLs as distinct. Use `HasFilter("\"DeletedAt\" IS NULL")` or `AreNullsDistinct(false)`. Append enum values with explicit numbers and never renumber.
9. **API contract.**
   - New API controllers inherit `ApiControllerBase`: `[Authorize]`, `[AutoValidateAntiforgeryToken]`, and `ApiExceptionFilter`, which turns KeyNotFound→404, UnauthorizedAccess→403, Concurrency/Duplicate→409, InvalidOperation/Argument→400 into a `{message}` body (`ApiControllerBase.cs:12-43`).
   - Every MVC POST needs `[ValidateAntiForgeryToken]`.
   - There is no authorization fallback policy (`Program.cs:79`), so each controller needs an explicit `[Authorize(Roles=…)]`.
   - Configuration writes should be TenantAdmin. `LabelsApiController.cs:23-35` forgot this.
   - DTOs are positional records. Append optional trailing parameters (as `RugDtos.cs:29-31` did), and mirror them by hand in `CA/lib/types.ts`.
10. **CSP** (`Web/Support/SecurityHeadersMiddleware.cs:23-56`):
    - Scripts are allowed only from `'self'` or with the nonce (`Context.CspNonce()`). Inline handlers are blocked.
    - CDN scripts are not allowed, so libraries must be bundled by Vite.
    - `connect-src 'self'`, so exchange-rate lookups must happen on the server.
    - `img-src 'self' data: blob:`, so logos must come from `/media/tenant/...`.
    - `style-src` allows `'unsafe-inline'` and jsDelivr.
    - `frame-ancestors 'none'` plus `X-Frame-Options DENY` block even same-origin iframes, and `object-src 'none'` blocks `<object>`/`<embed>`. Print previews must open a new tab, a popup, or use a dedicated print page.
    - Razor must escape at-rules as `@@page` and `@@media`.
11. **Print.** Pages inside the main layout hide `header`, `nav`, `aside` and `[data-no-print]` when printing (`app.css:275-282`), so never use those elements for document content. Layout=null pages get no `app.css`, islands or UiShell.
12. **Persian, Jalali and numbers.**
    - Store and compute numbers with InvariantCulture and Latin digits. Persian digits are for display only. Never format exported numbers with the current (`fa`) culture, whose negative sign is not ASCII.
    - Pass user and imported text through `PersianText.Normalize`. Store serials as Latin digits.
    - All numeric input must go through `normalizeDigits` and must handle `٫`.
    - `timestamptz` requires UTC offsets (`RugListDtos.cs:92-106`). Day filters use `IranDayStartUtc/EndUtc`.
    - Display Jalali dates in Iran time.
    - The currency label must come from the tenant and be passed to islands as a prop. USD must be an explicit, currency-tagged field, never mixed into PurchaseCost, TargetSalePrice or sale amounts.
13. **Roles.**
    - Today: TenantAdmin can do everything in the tenant. Operator gets Dashboard, Rugs, Groups, Workflows (read), Labels (print), Reports, and recording sales through the API. Financial outputs (sales, providers) are TenantAdmin.
    - Role claims live in the cookie and are never revalidated, so any new role or flag takes effect only after re-login.
    - New roles append to the enum as values 3+. Role labels are duplicated in `UserManagementService.cs:107-108`, `AccountController.cs:161-167`, `Users/Index.cshtml:4-9` and `Users/Form.cshtml:40-41`, and `EnsureTenantRole` (`:101-105`) must be extended.
    - The nav tuple must match the server-side role gates.
14. **Frontend.**
    - Each new screen is a Razor view plus an island registered in `registry.ts`.
    - Use tokens, never `bg-white` or hex colours (except in print surfaces).
    - Keep 44 px targets, aria patterns, and `dir=ltr` with `text-end` for numbers.
    - In RTL, ArrowLeft means "next".
    - Grids must opt out of the `n` and `/` hotkeys and must survive UiShell's automatic reload after an offline flush (`UiShell.vue:21-25`) and `OfflineIndicator.vue:31`.
    - Strict vue-tsc must pass.
    - Keep Tailwind classes in `.vue` or `.cshtml` files, or add an `@source` entry.
15. **Uploads and files.** Check the type by magic bytes (`App/Common/ImageSignature.cs` pattern). Enforce `[RequestSizeLimit]` plus an explicit length check. Never use client file names in paths. Store files outside wwwroot. In containers, persistent data goes under `/data` (`Dockerfile:34-35`).
16. **Audit.** A new `AuditAction` value needs three edits (see §3). Audit writes happen in the same UoW and must never throw.
17. **CI.** `dotnet build -warnaserror` on the slnx with Nullable enabled (`.github/workflows/ci.yml:32`), `npm ci && npm run build`, and `docker build`.
18. **Working agreement** (from the user's memory): approval per phase; the user makes every commit; Persian text must never pass through the Git Bash terminal; seed or test Persian data through the browser fetch path.
19. **Seeding.** Seeding is all-or-nothing based on whether a table is empty, so new defaults for existing tenants need an idempotent path. `TenantManagementService.CreateAsync` seeds nothing for new tenants.

---

## 6. Libraries for .xlsx export and Excel import

**Present today**

| Area | What exists |
|---|---|
| Backend (all 7 csproj: Api, Application, Domain, Infrastructure, Web, Tests, stray `RugsManagment_Service/`) | No ClosedXML, EPPlus, DocumentFormat.OpenXml, NPOI, MiniExcel, QuestPDF or any PDF library. Application references FluentValidation, which is unused. Infrastructure has `System.IdentityModel.Tokens.Jwt` (dead in the Web host). Tests use xUnit 2.9.3 and reference only Application and Domain (`RugsManagment.Tests.csproj:21-24`). |
| Frontend (`Web/ClientApp/package.json`) | `vue` 3.5, `@vueuse/core` (unused), `vue-draggable-plus`, `qrcode`, `jsbarcode`. No `xlsx`/SheetJS, `exceljs`, grid, date picker, jsPDF or puppeteer. The legacy `client/` package.json has none either. |
| Only spreadsheet output | `Web/Support/CsvWriter.cs`: UTF-8 BOM, a `sep=,` first line, CRLF rows, invariant `0.##` numbers, Jalali dates with Persian digits, بله/خیر booleans, and an apostrophe guard for values starting with `= + - @` (`:55-73`). Used only by `SalesController.ExportCsv` (`:25-45`), which sends `File(bytes, "text/csv; charset=utf-8", ...)`. |
| PDF | Browser "Save as PDF" from standalone print pages (`Views/Sales/Print.cshtml`). The runtime image is `mcr.microsoft.com/dotnet/aspnet:10.0` with no Chromium or fonts (`Dockerfile:26`). |
| Upload | Images only: `IImageStorage` whitelists webp, jpg, jpeg and png; `ImageSignature` knows only images. `api.ts` sends JSON only. |

**Options named by the readers**

| Option | License / status | Notes |
|---|---|---|
| ClosedXML | MIT | Reads and writes. Supports RightToLeft, merged cells, formulas and print titles. **Recommended**, in Infrastructure behind an `IExcelWriter`/`IExcelReader` abstraction. |
| DocumentFormat.OpenXml | MIT | Low-level alternative. |
| MiniExcel | | Mentioned as a server-side option. |
| EPPlus 5+ | Polyform Noncommercial | Avoid unless licensed. |
| npm `xlsx` 0.18.5 | Stale | Current SheetJS ships from its own CDN, and CDN scripts are banned. |
| exceljs | | Possible client-side option, bundled by Vite. Blob downloads are allowed by CSP. |

**Recommended shape (reader consensus): render once, output many times.**

- An Application-level composer turns template plus data into a typed `RenderedDocument`: sections; rows of kind Header, Data, GroupHeader, GroupFooter, Summary or GrandTotal; cells with value, format, colspan and an optional formula kind such as RoundProduct or SumOfGroup.
- That one model feeds three outputs: the Razor print view, the xlsx writer, and a JSON preview for the Vue designer.
- **The xlsx writer must emit:**
  - real formulas (`ROUND(H*I,2)`, `ROUND(J*K,2)`, a SUM per bale, a grand SUM)
  - merged cells
  - a `RightToLeft` sheet
  - repeating print-title rows
  - A4 portrait, fit to width
  - number formats
  - text cells always written as strings, never formulas (carrying over CsvWriter's injection rule)
  - numbers always numeric
- **Import** should be parsed on the server with the same library, with an XLSX ZIP magic check (`PK\x03\x04`), a size limit, a dry-run preview and per-row errors.
- **Fonts:** self-host Vazirmatn. Printed output currently depends on jsDelivr, and xlsx output depends on the fonts installed on the user's machine (the sample uses B Nazanin).

---

## 7. Gap list for capabilities 1–7, with the files that would change

### Cross-cutting prerequisites

- **Fix first.** Migrations (§8 #1–2). The cost-dialog zeroing bug (`CostRuleEditor.vue:54-62`). Cross-tenant lookups (`RugBatchService.cs:72`, `JwtTokenGenerator.cs:56-61`). The SKU retry (`RugManagementService.cs:81-95`).
- **Tenant settings home.** Today Tenant has only 4 scalar columns. The new features need numbering format (the SKU prefix is hard-coded at `SpecializedRepositories.cs:258`), serial mode, area rounding precision, default allocation method, default USD rate, and exporter header fields (name, code, address, customs office). Options: more columns, or a jsonb `TenantPreferences` exposed through `TenantSettingsDto`, `TenantSettingsService`, `Views/Settings/Tenant.cshtml` and `SettingsController.cs`. Keep `TenantSettingsAccessor` scalar and light.
- **Island bootstrap.** Islands receive no tenant context. Add a server-rendered JSON bootstrap (meta tag or data-props) carrying role, currency label, rounding and simple-mode, so islands stop hard-coding "تومان". Files: `_Layout.cshtml`, `TenantSettingsAccessor.cs`, `CA/lib/format.ts`.
- **Roles for "admin configures once, merchant uses simply".** Options from the readers:
  - (a) configurator = TenantAdmin and merchant = Operator, with Operator rights widened to shipments, bales, costs and exports;
  - (b) a new role or per-user permission flags;
  - (c) merchant stays TenantAdmin with a per-user SimpleMode or LockSettings flag.
  
  Role changes need cookie revalidation (`OnValidatePrincipal` with a security stamp). A SystemAdmin cannot configure inside a tenant today: `RequireTenantId` throws and AuditLog drops tenant-less events.
- **Catalogs.** Use the system-plus-tenant catalog pattern from ProcessStepType (`TenantId` null = system; `ListForTenantAsync`, `SpecializedRepositories.cs:339-354`) for cost categories, purchase components and a default export template. Fix the NULL-distinct unique index problem when copying it.
- **Navigation and IA.** `_Layout.cshtml:26-53` (grouping, merchant menu, explicit bottom nav). `CommandPalette.vue:50-54` gets new commands such as محمولهٔ جدید، عدل جدید، ورود از اکسل، خروجی اظهارنامه.
- **Audit.** New AuditAction values (Baled, FinalDimensionsRecorded, CostAdded/Allocated, Imported, Exported, TemplateChanged), each needing the three-place update in §3. Replace the in-memory paging in `ActivityLogService` before import volume arrives.
- **Shared UI kit.** NumberInput (decimals, `٫`, negatives, suffix), Modal/Drawer (reusing UiShell's focus trap), Field/Input/Select/Button, a Jalali date picker, a combobox with "add option", a stepper, and helpers `faUsd`/`faCurrency(value, unit)`. Files: `CA/components/*`, `CA/lib/money.ts`, `CA/lib/format.ts`.

### Capability 1: Intake, dynamic purchase price, dynamic fields

**Gap.**
- `Rug.PurchaseCost` is a single `decimal?` (`Rug.cs:25`). It is read by the C# total and 3 SQL totals, and cannot be cleared because Update uses `??`.
- There are no components or labels, and no per-m² or per-piece purchase price.
- Intake dimensions are overwritten by `UpdateAsync` (`RugManagementService.cs:161-162`).
- Custom fields are not validated on the server, have no scope, and the manager cannot edit or reorder them.
- Entry is one rug per page.
- There is no supplier or seller on a rug.

**Design hooks from the reports.**
- New `RugPurchaseComponent` entity (RugId, CategoryId or label, Basis Fixed|PerM2|PerPiece, Rate, Quantity, Amount, Currency?, FX?, SortOrder), with the soft-delete filter, plus a tenant purchase-component catalog.
- Keep `Rug.PurchaseCost` as a **denormalised total** kept in sync by the service, so all SQL, sorting, the dashboard, sales profit and labels keep working.
- Optionally extend `CombinedPricingItem` with a Label, which is backward-compatible because deserialisation is case-insensitive.
- Validate MetadataJson with `StepFieldSchema.ValidateValues`, passing ALL definitions so inactive keys are not stripped, and adding Date validation.
- Map the Excel columns: field colour → seeded `bg_color` (`DatabaseSeeder.cs:129-133`); border colour → a new custom field; design → Pattern; weaving origin → Origin; material → Material.
- Columns that must be sorted, filtered or summed may deserve real columns, because jsonb strings are not LINQ-queryable.

**Files.**
- `Domain/Entities/Rug.cs`, new `RugPurchaseComponent.cs` and catalog entity, `CustomFieldDefinition.cs` (Scope/AppliesTo, IsListColumn, IsExportable, Color/Lookup type), `CustomFieldType.cs`
- `EntityConfigurations.cs`, `AppDbContext.cs`, new migration
- `RugManagementService.cs:97-174` (validation, W/L > 0, Normalize, fix `??` clearing), `RugDtos.cs`, `CustomFieldService.cs`, `CostCalculationService.cs` (a PerPiece basis)
- `RugForm.vue` (repeatable component editor, NumberInput, Jalali date, `bg-white` → tokens), `CustomFieldsManager.vue`, `types.ts`, `Views/Rugs/Details.cshtml`, `LookupsApiController.cs`

### Capability 2: Baling and final dimensions

**Gap.**
- There is no Shipment/Declaration, Bale or BaleItem entity.
- There is only one W/L pair.
- RugStatus has no Baled or Exported state, and `ResolveStatusFromWorkflow` would overwrite one.
- Nothing locks a finalised shipment.
- `GroupDetail` caps lists at 100 and groups by workflow step.

**Design hooks.**
- `ExportShipment` (TenantId, Number/Title, Date, Destination, Buyer, CurrencyCode USD, FX, Status Draft/Finalized/Exported, MetadataJson, xmin).
- `Bale` (ShipmentId, BaleNumber unique per shipment, sort, notes, weight?).
- `BaleItem` (BaleId, RugId, SortOrder, FinalWidth, FinalLength, rounded area, UnitValuePerM2 in USD, TotalValue, Quantity, and a snapshot of the descriptive fields).
- On Rug, keep `WidthMeters/LengthMeters` as the "effective" pair so existing SQL keeps working, and add `IntakeWidth/IntakeLength` plus nullable `FinalWidth/FinalLength` (and MeasuredAt/By). Alternatively add only nullable final dimensions, with `effective = Final ?? Initial`.
- Keep `RugBatch` for intake. **Do not overload `BatchId` for bales**: a rug needs both an intake batch and a bale.
- Decide how step costs relate to final dimensions: a per-step `PricedWidth/PricedLength` snapshot, or a `ProcessStepType.DimensionBasis`. Otherwise later re-pricing silently switches to the final dimensions.
- Block soft delete of a baled rug, or remove it from its bale first.

**Every W×L consumer must be reviewed:**
- `Rug.cs:71`
- `SpecializedRepositories.cs:176, 226-228`
- `CostCalculationService.cs:29`
- `labelFields.ts:65-66`
- `Details.cshtml:99-103`
- `Index.cshtml:264-268, 345`
- `Trash.cshtml:59`
- `RugWorkflowPanel.vue:316`
- `RugForm.vue:155-171`

**Files.**
- New domain entities; `RugStatus.cs` (append 5, 6); `WorkflowEngine.cs:379-388`; the status label sites (§3)
- `RugManagementService.cs:188-189` (delete guard)
- `RugQuery` and `ApplyFilters` (ShipmentId, BaleId, NotBaled, modelled on WithoutBatch at `RugListDtos.cs:78` and `SpecializedRepositories.cs:99-100`)
- New repositories with set-based SQL projections (avoid `GetWithWorkflowAsync` N+1; consider `AsSplitQuery`)
- New controllers and API; new islands (a baling board built on `GroupDetail`'s grouped columns plus `vue-draggable-plus` and `RugBulkToolbar` selection, and a final-size quick-edit row with a shrink/grow indicator) registered in `registry.ts`
- `QrScanner.vue:110` (exact match only, add `code_128`) for "scan to add to bale"; `AuditAction`

### Capability 3: Cost list and allocation

**Gap.** Costs exist only as per-rug, per-step `RugWorkflowStep` rows. There is no lump sum, no group or set scope, no allocation, no recompute and no locking.

**Design hooks.**
- `CostEntry` (TenantId, CategoryId, Date, ServiceProviderId?, Basis PerM2|PerPiece|PerRug|LumpSum, Rate/Amount, Currency + FX, Scope Rug|Selection|Bale|Shipment|Batch|All, AllocationMethod ByArea|ByCount|Equal|ByValue|Manual, xmin).
- `CostEntryTarget` for explicit selections.
- Materialised `CostAllocation` (CostEntryId, RugId, Amount, IsManual), with the soft-delete filter.
- A pure allocator in Application using the **largest-remainder method**, so shares sum exactly. Today's per-item `Math.Round` would leak remainders.
- Count weights = Σqty and area weights = Σ(area × qty).
- "By value" needs a defined basis (purchase cost, USD value, or investment excluding allocations) to avoid a circular definition.
- A recompute service triggered by changes to dimensions, quantity, membership, soft delete/restore or the entry itself.
- A preview endpoint (an AllocationPreview modelled on `/api/pricing/preview`) returning per-rug rows plus the rounding remainder.
- Manual overrides follow EffectiveCost semantics.
- Consider a denormalised `Rug.AllocatedCostTotal` to keep the SQL simple.
- Add allocations to every total site (§2.2), and to the provider ledger when the cost is paid to a provider (union into CompletedWorkTotal and the work items, so `ProviderStatement.vue` keeps working unchanged).
- Optionally a "finalised" lock per shipment so issued documents don't drift.
- Retroactive allocations change historical profit unless cost is snapshotted at sale time.
- Pre-fill from `ServiceProviderRate`.
- Reuse `ComputeBase` (`CostCalculationService.cs:32-37`) wrapped with a quantity and dimension-basis overload. Do not change its semantics.

**Files.**
- New domain entities and configuration; new allocator service, recompute service and repositories
- `WorkflowEngine.cs:302-315`; `SpecializedRepositories.cs:147-152, 196-200, 295-299, 413-461`; includes in `GetWithWorkflowAsync`, `ListByTenantAsync`, the batch and sales loads; `EntityMappers.cs`, `AnalyticsService.cs`, `RugSaleService.cs`
- New cost-ledger island (target picker, basis, amount, allocation preview table, allocated vs unallocated summary) using `confirmDialog`; simplify `CostRuleEditor`'s wording; `PricingApiController`
- Fix the three inconsistent model menus

### Capability 4: Output designer, .xlsx and print/PDF

**Gap.**
- Templates are single-record: one rug per render.
- There are no columns with headers, grouping, group headers or footers, aggregates, summary table, grand total, page setup, widths, formats or merged cells. The `table` element only shows key/value rows of one rug (`LabelRender.vue:37-41`).
- Field resolution exists only in TypeScript.
- There is no USD/m² or total-USD field, no FX, and no xlsx library.
- There is no bulk data endpoint: LabelPrint does a GET per rug, and `/api/rugs/samples` loads every rug.

**Design hooks.**
- An `ExportTemplate` entity modelled on LabelTemplate: TenantId, Name, Kind/target (shipment, bale, selection), IsDefault, LayoutJson (jsonb, validated before save) holding columns (source key including metadata keys, label, width, format, formula), group-by bale, subtotal text (`نگله شماره {n} شامل {k} تخته`), per-bale summary, grand totals, header and footer blocks with `{{placeholders}}`, and page setup (A4, orientation, margins, repeating header).
- Make the field catalogue a server-side typed catalogue `{key, label, scope (rug|line|bale|shipment), dataType, defaultFormat, aggregatable}`, served from something like `GET /api/lookups/fields`, so the designer and the C# renderer share it.
- Reuse the `LabelDesigner` UX: palette, draggable column list (`:176-188`), properties panel (`:191-210`), sample preview (`:226-240`).
- Print view based on `Sales/Print.cshtml`: `thead { display: table-header-group }` replaces the hand-typed repeated headers, and the page opens in a new tab.
- Endpoints `ExportXlsx` and `Print` share one query builder (the `SalesController.BuildQuery` pattern).
- Seed a default "صورت نگله بندی و اظهار فرش صادراتی" template (precedent: `DatabaseSeeder.cs:179-192`).
- Add a dotted Jalali date format (`1405.06.15`) to `PersianFormat`.
- Rounding: `MidpointRounding.AwayFromZero` and ROUND(L×W, 2) per line before multiplying by price, applied identically on screen, in print and in xlsx.
- **Escape field values.** Do not copy the label's unescaped `v-html`.
- Gate by role: USD and cost documents are TenantAdmin; bale stickers can be Operator. Bale stickers can reuse `LabelPrint`/`LabelsController.Print` with baleIds.

**Target document** (`C:\Users\sadra\Desktop\baba-1404-08-11 (2) (2).xlsx`, inspected read-only).

*Sheet setup:* one RTL sheet, zoom 70, A4 portrait, scale 95, fitToHeight=0, margins L/R 0.2 in and T/B 0.5 in, font B Nazanin. Column widths A..L ≈ 3.9 / 6.6 / 7.9 / 8.7 / 9.1 / 8 / 10.1 / 6.1 / 6.4 / 7.1 / 14.9 / 16.

*Title row:* `صورت نگله بندی و اظهار فرش صادراتی    تاریخ  1405.06.15`.

*Columns:*

| Col | Header | Content |
|---|---|---|
| A | نگله | Bale number, merged vertically over its rows |
| B | شماره | Merchant serial, or a lot range such as `3911 - 3960` |
| C | رنگ متن | Field colour |
| D | رنگ حاشیه | Border colour |
| E | نقشه | Design |
| F | محل بافت | Weaving origin |
| G | مواد مصرفی | Material (misused for lot quantity) |
| H | طول | Length |
| I | عرض | Width |
| J | مساحت | `=ROUND(H*I,2)` |
| K | ارزش متر مربع | USD/m², varies per line (45, 25, 80) |
| L | جمع کل دلار | `=ROUND(J*K,2)` |

*Bale footer row:*
- A:B merged: "نگله شماره"
- C: the bale number
- D: "شامل"
- E: the count
- F: "تخته"
- I:J merged: `SUM` of area
- K:L merged: `SUM` of USD

A lot row (for example `0.4×0.4`, quantity 50) uses a hand-written multiplying footer.

*Summary and totals:* a summary table at rows 260–297 (نگله | تعداد فرش | متراژ | ارزش گمرکی) with hand-typed values, then a grand total at row 299: 242 pieces, 464.74 m², 20,322.5 USD.

*Manual errors a generator should eliminate:*
- Bale 33 is labelled 26 (row 210).
- A lot formula only works for a single price (row 191).
- Summary values are typed rather than linked.
- Headers are re-typed at page breaks (rows 87-88, 130-131, 216-217, 226).
- Serials 2026–2028 are out of sequence.
- A bale number is placed on the bale's second row (A193).

**Files.**
- New `ExportTemplate` entity, service, API and MVC trio (copy `LabelTemplateService.cs`, `LabelsApiController.cs`, `LabelsController.cs`, fixing the role gate and the 404 rule)
- `App/` document composer and field resolver; `Infra/` xlsx writer (new package)
- New print view `Views/Export/*.cshtml` (Layout=null); designer and preview islands; `registry.ts`
- `PersianFormat.cs`; `DatabaseSeeder.cs` (idempotent template seed); self-hosted fonts in `wwwroot`

### Capability 5: Lots

**Gap.**
- Rug assumes one physical piece.
- There is no Quantity and no serial range.
- `ComputeBase` prices one piece only, so a 50-piece lot is under-costed by a factor of 50.
- `RugSale` has a unique RugId (`EntityConfigurations.cs:186`), so a lot cannot be partly sold.
- The dashboard and analytics count rugs as units.

**Design hooks.**
- `Quantity int` default 1, plus `SerialFrom/SerialTo` (or a range text).
- Area × Quantity everywhere totals appear: `SpecializedRepositories.cs:176, 227`, per-m² costing, allocation weights, `EntityMappers.cs:211-214`, analytics counts, and the bale footer's "K تخته", which must use Σqty.
- A split-lot operation, or a quantity on the sale.
- Decide one label per lot or one per piece.
- The provider statement shows one line for N pieces.
- Range input `3911 - 3960` should derive the quantity automatically.

**Files.** `Rug.cs`, `EntityConfigurations.cs`, migration, `CostCalculationService.cs`, the allocator, `SpecializedRepositories.cs`, `EntityMappers.cs`, `AnalyticsService.cs`, `RugSale.cs`/`RugSaleService.cs` (split or quantity), `labelFields.ts`, `ProviderDtos.cs`, grid and document columns.

### Capability 6: Manual merchant serial numbers

**Gap.** There is no field. The palette and scanner match SKU only. `ProviderWorkItemDto` shows only SKU and Title (`ProviderDtos.cs:65-73`).

**Design hooks.**
- `Rug.SerialNo` varchar, Latin digits after `Normalize`, plus an optional numeric `SerialSort`.
- Partial unique index on `(TenantId, SerialNo) WHERE DeletedAt IS NULL AND SerialNo IS NOT NULL`. Decide whether soft-deleted rows reserve a serial, consistent with SKU behaviour.
- Range-overlap checks for lots, in the app or with a Postgres exclusion constraint on `int4range`.
- `DuplicateKeyException` mapping gives a 409 with a Persian message for free.
- Optionally suggest the next serial as max + 1.
- Adding SerialNo to `ApplyFilters` makes it searchable automatically in `QrScanner` and `CommandPalette`.

**Files.** `Rug.cs`, `EntityConfigurations.cs`, migration, `SpecializedRepositories.cs:113-137` (search) and sort, `RugListDtos.cs:51-60` (`RugSortBy`), `RugListItemDto`/`RugDto`, `types.ts`, `labelFields.ts` (`STATIC_FIELDS`), `Views/Rugs/Index.cshtml` and `Details.cshtml` (a prominent "شماره" column), `ProviderDtos.cs`.

### Capability 7: Excel import, spreadsheet entry, simpler UX

**Gap.**
- There is no upload-and-parse path, xlsx reader or batch-create.
- `CreateAsync` works one rug at a time, scans the month's SKUs, and reloads the full graph after each insert.
- `GenerateNextSkuAsync` cannot allocate N SKUs before one SaveChanges.
- The SKU retry is broken.
- There is no grid component, patch endpoint or exposed concurrency version.
- `BulkUpdateFields` lacks Title, W/L, final dimensions, PurchaseCost, Notes and metadata.

**Design hooks.**
- A bulk-create/upsert service with **block SKU allocation**: an in-memory max+1..max+N sequence, a tenant counter with a concurrency token, or a Postgres sequence.
- Column mapping, remembered per tenant. It must handle the sample file's quirks: lot quantity in the material column (G187 = 50), bale numbers merged vertically, subtotal rows and repeated header rows mixed into the data, and hand-typed summaries.
- A dry-run preview, then commit, with per-row errors (`BulkOperationResultDto` plus row numbers).
- An optional `ImportJob` entity (file name, mapping JSON, row results) for an audit trail and undo.
- A structured error contract for per-cell errors.
- The grid island needs:
  - a cell focus model with RTL arrows, Enter and Tab
  - type-to-edit, fill-down and multi-row TSV paste from Excel
  - per-cell validation
  - dirty-row tracking with batched autosave (optionally `sendOrQueue` for idempotent saves)
  - undo, a frozen header and first column, and virtualisation (`useVirtualList`)
  - unsaved-draft persistence (`useStorage`), since UiShell may reload the page
  - hotkey opt-out (for example `closest('[data-hotkeys-off]')` in `CommandPalette.vue:163-195`)
- A `heavy` rate-limit policy and `RequestSizeLimit` on import and export.

**Files.**
- `RugManagementService.cs` (bulk create; `ChangeTracker.Clear` or a new scope on retry)
- `SpecializedRepositories.cs:256-275`, `RugDtos.cs`, a new import service, `IExcelReader` in Infrastructure, a magic check in the `ImageSignature` style
- New `ImportController`/API; `api.ts` (`postForm`/upload, `download`, `del` with a body)
- New grid and import-wizard islands, `CommandPalette.vue`, `UiShell.vue`/`OfflineIndicator.vue` (reload suppression), `offlineQueue.ts` (scope by user and tenant)
- `Program.cs` (rate-limit policy)

---

## 8. Risks and pre-existing defects, ranked

1. **Critical: no migrations are applied anywhere.** `Database.MigrateAsync()` is commented out (`DatabaseSeeder.cs:22`, commit `c09eceb`). The method named `MigrateAndSeedWithRetryAsync` (`Web/Program.cs:169, 174-192`) only seeds. It retries 10 times, 3 s apart, and the last failure crashes startup. README (`:63`), DEPLOY (`:73`) and the Program.cs comment all wrongly say migrations run automatically. The Dockerfile and CI have no `ef database update` step. A fresh CapRover DB has no schema and the app crashes; new schema for capabilities 1–7 will never deploy.
2. **Critical: the migration chain is broken.**
   - `StepPricingFields` adds 3 columns (`:13-29`).
   - `RugBatches` drops them (`:32-42`).
   - `PendingModelSync` re-adds them (`:13-29`).
   - `SyncModelState` adds the same 3 again (`:13-29`).
   
   On a fresh DB, `database update` fails with PG 42701. Provenance: commit `c09eceb` added `PendingModelSync`, and merge `208c008` brought `SyncModelState` in from branch `7900031`. The Designer files and snapshot are consistent; only the `Up()` chain is broken. **Fix before adding migrations:** make `SyncModelState.Up/Down` a no-op, or use `ADD COLUMN IF NOT EXISTS`. Also add a pending-model-changes check to CI.
3. **Secrets and credentials.**
   - The DB password `7082` is tracked in `Web/appsettings.json:10` and `Api/appsettings.json:10`.
   - The JWT secret is in `appsettings.json:13` and as the `JwtSettings` default (`JwtTokenGenerator.cs:16`).
   - There is a silent fallback connection string with `postgres/postgres` (`Infra/DependencyInjection.cs:25-26`). `docker-compose.yml:7` uses a password that does not match.
   - An **unencrypted data-protection key is committed** (`Web/App_Data/keys/key-84a535a8-….xml`). Authorization relies only on cookie claims, so a key in a deployment's key ring could forge any cookie. Purge it from history, rotate keys, and use `ProtectKeysWith*`.
   - Default credentials (`Admin@12345`, `Demo@12345`) and demo data are seeded in every environment (`DatabaseSeeder.cs:38-122, 45, 70`).
4. **Cross-tenant defects.**
   - `RugBatchService.AssignRugsAsync` uses an unscoped `GetByIdAsync(batchId)` (`:72-73`), so a tenant can attach its rugs to another tenant's batch.
   - `BulkUpdateFieldsAsync` sets BatchId with no check (`RugManagementService.cs:466-467`). A missing id causes an FK violation, a 500, and the whole bulk rolls back.
   - `ProcessStepTypeLookup.GetRequiredAsync` is an unscoped `FindAsync` (`Infra/Identity/JwtTokenGenerator.cs:56-61`, used at `WorkflowEngine.cs:62, 275`).
   - `ServiceProviderId` in advance and custom-step requests is never tenant-checked (`WorkflowEngine.cs:96-97`), which can inflate another tenant's balance (`SpecializedRepositories.cs:423-435`).
   - The legacy Api `WorkflowsController.StepTypes` lists all tenants' step types (`WorkflowManagementService.cs:30-34`).
   - `TenantSettings.DefaultWorkflowTemplateId` has no ownership check (`TenantSettingsService.cs:44`).
   - `LabelsApiController` writes have no TenantAdmin guard (`:23-35`).
   - Some services return 403 instead of 404, which reveals that an id exists.
5. **No session revalidation** (`Program.cs:64-77`, no `OnValidatePrincipal`). Disabled users, demoted admins, deactivated tenants and expired subscriptions keep access until the 8-hour sliding cookie expires; RememberMe defaults to true. Changing a password does not end other sessions.
6. **Cost-engine bugs that would corrupt the new cost features.**
   - The `CostRuleEditor` zero-cost overwrite (§2.3).
   - The template path never prices steps (`WorkflowEngine.cs:318-330`).
   - `ApplyPricing` overrides are not persisted (`:332-340`).
   - "Save only" drops field values.
   - Provider injection clears a manual override (`:366-367`).
   - `BulkAdvance` is inconsistent with single advance.
   - Model and rate resolve independently (`CostCalculationService.cs:27-28`).
   - Template overrides are dead paths.
   - Tenants cannot change system default rates.
7. **The SKU-collision retry cannot work.** The failed entity stays Added in the scoped DbContext (no `ChangeTracker.Clear`), so all 5 attempts fail and return 409 (`RugManagementService.cs:81-95`). This breaks bulk import and concurrent quick entry.
8. **Data-integrity gaps.**
   - `UpdateAsync` cannot clear values.
   - MetadataJson is accepted raw: invalid JSON gives a 500, required fields are checked only in the browser.
   - The status machine is unguarded: Sold can exist without a sale, and advancing a sold rug changes its status.
   - There is no server validation of W/L > 0 or PurchaseCost ≥ 0.
   - Stale snapshot costs after dimension or rate changes.
   - Profit is not snapshotted at sale time.
   - The demo rug "نایین آبی" is seeded as Sold with no `RugSale` (`DatabaseSeeder.cs:176`).
9. **Rounding and precision inconsistency.** `Math.Round` ToEven vs Excel away-from-zero vs JS half-up. Unrounded area in the domain and SQL vs `ROUND(L*W,2)` per line in Excel. The approximate ft² factor. Unbounded `numeric` vs `(18,2)`. Without one centralised rule, USD totals will differ by cents. Choose explicit precisions for new fields, such as (18,2) for money and (18,4) for FX and dimensions.
10. **Performance.**
    - Cartesian product of Images × Steps in `GetWithWorkflowAsync` (no `AsSplitQuery`, `SpecializedRepositories.cs:34-43`).
    - N+1 in bulk operations, batch assign, the sales report (`RugSaleService.cs:126-131`) and LabelPrint.
    - Full-tenant loads in `/api/rugs/samples`, analytics and the dashboard.
    - The `GroupDetail` 100-row cap would hide rows and break totals.
    - The batch list never includes steps, so its summary is always null.
    - Activity paging happens in memory.
11. **Export and print defects.**
    - `CsvWriter` turns negative numbers into text with the apostrophe guard (`:60, 68-69`).
    - `sep=,` plus a BOM may garble Persian in some Excel versions.
    - The sales screen and CSV/Print cover different date ranges (`SalesApiController.cs:36-39` vs `SalesController.cs:67-72`; `SalesReport.vue:79-80` uses a UTC date).
    - Element ids collide in `LabelDesigner` (`:28, 45, 88`).
    - `LabelRender` shows raw keys for custom fields (`:13-15`).
    - **Unescaped values in `v-html`** (`LabelPrint.vue:163`, `LabelDesigner.vue:120, 236`, `labelFields.ts:77-79`): stored HTML injection that only CSP keeps from becoming script.
    - External logo URLs are silently blocked by CSP.
    - In-layout label printing can overflow, and the expiry banner prints.
    - `CsvWriter` falls back to culture-sensitive `ToString()`.
12. **Frontend and UX defects.**
    - MoneyInput takes no decimals or negatives. `٫` is silently corrupted (`money.ts:28`).
    - Currency label is inconsistent between islands and Razor.
    - `bg-white` breaks dark mode.
    - The `n` and `/` hotkeys can navigate away from unsaved work.
    - Automatic reload after an offline flush.
    - The offline queue is not scoped to user or tenant, so it can replay under another login.
    - `QrScanner` opens the first partial match (`:110`).
    - The skip link uses an undefined class `sr-only-focusable` (`_Layout.cshtml:98`).
    - Gregorian date inputs everywhere.
    - Vazirmatn from jsDelivr is not cached by the service worker (`sw.js:36`).
    - The PWA is locked to portrait.
    - Error messages show as success toasts (`_Layout.cshtml:222`).
    - The CSP blocks `offline.html:19`'s `onclick`.
    - TS/DTO drift: `types.ts:119` uses `imageUrl` where the server sends `thumbnailUrl`, and RugCostSummary lacks the actual-sale and profit fields.
13. **Users and admin.**
    - `UsersController.Edit` always sets `IsActive = true` (`:37`), so editing a disabled user re-enables them.
    - No guard against removing the last admin or self-demotion.
    - Password policy is inconsistent: none, 6 or 8 characters.
    - The disabled-account message is returned before password verification, plus a timing side channel.
    - `AdminController.Create` turns a duplicate email into a 500 (`:36`).
    - SystemAdmin actions are never audited (`AuditLog.cs:31`).
    - Profile name and password are saved in separate transactions.
14. **Dead code and stray projects.**
    - The JWT pipeline still runs on every login (`AuthService.cs:42, 88-92`). Removing it requires moving `ProcessStepTypeLookup` out of `JwtTokenGenerator.cs` first.
    - FluentValidation is unused.
    - `RecalculateStepCost` is dead. `WorkflowStepStatus.Cancelled` is never set. There is a duplicated `<summary>` comment (`WorkflowEngine.cs:373-378`).
    - `src/RugsManagment.Api` is not in the slnx and contains a committed upload. The `client/` SPA, the stray `RugsManagment_Service/` and `_design_ref*/` are still tracked. The Dockerfile copies all of `src/`.
    - The comments on the unique index and the "partial index" in `EntityConfigurations.cs:33-35, 71` are wrong.
15. **Platform hygiene.**
    - `ForwardedHeaders` trusts any proxy (`Program.cs:49-50`).
    - `AllowedHosts *`.
    - The container runs as root on port 80 with no HEALTHCHECK.
    - Only the login has a rate limiter.
    - `.claude/launch.json` runs with `--no-launch-profile`, so local runs behave as Production.
16. **Test coverage.** The tests cover only `CostCalculationService`, `JalaliCalendarHelper`, `StepFieldSchema` and `WorkflowEngine`. Nothing covers repositories, tenant isolation, authorization, services such as Rug, Batch, CustomField, Sale or Settings, Web/Support (CsvWriter, PersianFormat), C#/SQL formula parity, migrations or the frontend. The test `TemplateOverride_WinsOverStepTypeDefault` (`CostCalculationServiceTests.cs:120-129`) gives false confidence, because the engine never passes template overrides. The new allocation maths, numbering and import parsing need unit tests in `tests/RugsManagment.Tests`.
