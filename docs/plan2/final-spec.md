# Export rug workflow: final specification (revised)

**Status note.** The text I was given to revise started partway through H.3. Sections A to H.3 were missing, and no copy of them exists on disk. I rebuilt A–H.3 here from four sources: the roughly 40 references to them in I–M, `brief.md`, `map.md`, and the sample dump plus the sample's raw sheet XML. Every critic fix is applied. **A–H.3 are reconstructions, so the critic must run again on them before the build starts.** Round 2 of the review (96 findings) has been applied throughout; see «Revision log → Round 2» at the end.

Path abbreviations follow `map.md`: `Domain/`, `App/`, `Infra/`, `Web/`, `CA/` (= `Web/ClientApp/src/`).

---

## A. Product shape, people, roles and modes

### A.1 Roles, policies and session revalidation

**Personas**

| Person | Role | Mode |
|---|---|---|
| The father: bazaar exporter, daily user | `Merchant` | Simple, text Large |
| The son: configures once | `TenantAdmin` | Full, with a «نمای ساده» preview toggle |
| Warehouse helper (optional) | `Operator` | Simple |
| Platform owner | `SystemAdmin` | `/Admin` only (unchanged) |

**`UserRole`.** Append-only, with explicit values: `SystemAdmin = 0, TenantAdmin = 1, Operator = 2, Merchant = 3`.

Labels live in one place (`RoleLabels`):
- TenantAdmin: «تنظیم‌کننده (مدیر)»
- Merchant: «بازرگان»
- Operator: «کاربر / انباردار»

**Policies** (`Web/Auth/Policies.cs`)

| Policy | Roles | Grants |
|---|---|---|
| `Floor` | TenantAdmin, Merchant, Operator | Intake grid; stock; shipments and bales (create bales, add, move or remove lines, final sizes); reading declared USD values; printing and exporting non-money templates; groups; labels (print); workflows (read); media; help |
| `Business` | TenantAdmin, Merchant | All Toman money (purchase lines, costs, totals, cost per m², profit); sales; providers; creating, editing and deleting shipments; finalize and reopen; per-line USD rate override; import; money templates; trash, delete and restore; reports |
| `Configure` | TenantAdmin | Settings hub; users; custom fields; step types; workflow templates; label designer; cost-category setup; declared-value table writes; grid layouts; document designer; preset; preferences; diagnostics |

**Session revalidation** (migration M0)

- `User.SecurityStamp` is a `varchar(64)`. Login adds an `sstamp` claim.
- `Web/Auth/SecurityStampValidator.cs` is wired to `CookieAuthenticationEvents.OnValidatePrincipal`. Its result is cached for 5 minutes per user in `IMemoryCache`.
- It loads:
  - from the user: `IsActive`, `Role`, `SecurityStamp`, `TenantId`
  - from the tenant: `IsActive`, `SubscriptionExpiresAt`
- It rejects the principal and signs out when:
  - the stamp differs;
  - the user is inactive;
  - **the tenant is inactive;**
  - **the subscription has expired.**
- The stamp rotates on:
  - a role change;
  - a password change (by an admin, or the user's own);
  - an `IsActive` change;
  - «خروج از همهٔ دستگاه‌ها».
- Rotating on this instance evicts the cache entry. Across instances, the worst case is 5 minutes.
- **The user's own password change keeps the current session.** `AuthResponse` (not the public `UserDto`) carries the fresh `SecurityStamp`; `ToClaims(UserDto, string securityStamp)` adds `sstamp`; both Login and the Profile re-sign path (`AccountController.cs:118-122`) pass the fresh stamp and evict the validator's cache entry. Only the user's *other* sessions end.
- `UsersController.Edit` no longer forces `IsActive = true`.

### A.2 Concept budget: the merchant's glossary

The merchant meets only the words below, **each with exactly one meaning**. Nothing else appears in Simple mode. The glossary test (L.1) enforces the reserved words.

| Word | One meaning | Where he meets it |
|---|---|---|
| فرش | One row: a single rug, or a دسته | Everywhere |
| تخته | One physical piece. **Every count shown to the merchant is Σqty in تخته**; with a lot present the row count follows in brackets: «۵۲ تخته (۳ ردیف)». | Footers, chips, toasts |
| دسته | Several identical تخته with one serial range («3911 - 3960») | Grids, split dialog |
| شماره | His serial «3778», or a range «3911 - 3960» | Grids, search, scanning |
| ورودی | The rugs of one seller on one day (`RugBatch`; the old «گروه») | Intake, «ورودی‌ها» |
| فروشنده | The seller's name on a ورودی | Intake grid |
| انبار | Rugs that are in no محموله | Stock |
| محموله | One export declaration (`Shipment`). Used for nothing else. | Shipments |
| نگله (or عدل) | A bale inside a shipment | Baling |
| اندازهٔ اول / اندازهٔ نهایی | The size at intake / the final size at baling | Grids, rug page |
| ارزش گمرکی | Declared USD per m² | Baling, output |
| هزینه | Money paid for some rugs | Costs |
| تقسیم‌نشده | A cost not yet split over any rug | Costs |
| پیش‌نویس / قطعی | A shipment or its output: draft / final. **Reserved for shipments and outputs.** | Shipment header, outputs |
| صادر شده | A rug in a قطعی shipment | Stock chips |

**Renames that follow from the glossary** (applied in P1 for existing screens, and in each phase for new ones):
- The tile «فرش رسید» becomes «ثبت فرش تازه»; the nav item becomes «ورود فرش».
- The tile and tab «خروجی» become «اکسل و چاپ». The shipment tab «خلاصه» becomes «حساب محموله». (The xlsx summary *sheet* keeps its sample name «خلاصه».)
- `RugBatch` is «ورودی» / «ورودی‌ها» on every screen, including the legacy Groups pages; the word «محموله» never appears on a RugBatch screen (J.2).
- `RugStatus.Draft` shows «ثبت‌شده», never «پیش‌نویس»; `InProgress` shows «در کارگاه», in Full mode only (J.2).
- The intake size column is «اندازهٔ اول» (not «اندازهٔ رسید»).

These stay attributes, not concepts:
- «قیمت خرید» and «دلالی» are intake-grid columns.
- «اندازهٔ نهایی» is a column on the baling screen, with a «همان اندازه» shortcut.
- «ارزش متر مربع» is a column; its source chip shows only on tap.

These are configurator vocabulary and appear only in Full mode or under «بیشتر»: allocation method, area basis, scope names, pins, FX, categories setup, templates, readiness codes and step pricing.

### A.3 Modes and per-user preferences

**Modes and preferences**

| Setting | Values | Default |
|---|---|---|
| `UiMode` (per user) | `Simple = 0`, `Full = 1` | Simple for Merchant and Operator; Full for TenantAdmin |
| TenantAdmin «نمای ساده» (`previewSimple`) | on / off | Changes only chrome: nav and hidden controls. Never permissions. |
| `TextScale` | `Normal = 0`, `Large = 1`, `XLarge = 2` | Root font size 16 / 18 / 20 px via `data-text-scale`. Large for Merchant. |
| `GridEnterDirection` | `Down = 0`, `Right = 1` | Down |

**Changing the text size.** The user menu and the HelpDrawer carry «اندازهٔ نوشته: [آ] [آ+] [آ++]», a radiogroup that saves through `PUT /api/me/preferences` and applies at once via `data-text-scale`. Grid column widths are in `ch` (`GridColumnDto.Width`), so they grow with the text. Grid rows have `min-height: 2.75rem` (3rem under `pointer: coarse`). At XLarge on a 1024 px screen the شماره column and the size columns stay visible (frozen first column plus the «اندازه‌گیری» column preset, G.2).

**What Simple mode hides**
- The workflow-steps panel (moved under «کارهای کارگاه» in «بیشتر»).
- The SKU (shown small, never as a primary column).
- QR codes and label designer entry points.
- Custom-field keys.
- Step pricing models.
- In the cost panel: method, currency/FX, pins, provider, receipt number and AreaBasis (H.2).
- The intake-versus-final size pair, unless the two differ.
- On `/Rugs` and rug details: the workflow status column and its filter. Only the Location chip shows (from P4b; J.2).
- In the stock bulk bar: «افزودن به ورودی» (the old «افزودن به گروه»), workflow bulk-advance and bulk status change.

### A.4 Navigation and home

Navigation is built by `NavBuilder` from `INavContributor`s. The bottom nav is explicit (no more `Take(5)`).

| Role / mode | Sidebar | Phone bottom nav |
|---|---|---|
| Merchant (Simple) | خانه · فرش‌ها · محموله‌ها · هزینه‌ها · بیشتر | Same 5 |
| Operator | خانه · فرش‌ها · محموله‌ها · بیشتر | Same 4 |
| TenantAdmin (Full) | خانه · ورود فرش · فرش‌ها · محموله‌ها · هزینه‌ها · ورود از اکسل · گزارش‌ها · فروش · کارگاه‌ها · کارهای کارگاه · ورودی‌ها · برچسب‌ها · تنظیمات | خانه · فرش‌ها · محموله‌ها · هزینه‌ها · بیشتر |

- **`/More` («بیشتر»)** lists every other page the role may open: ورودی‌ها, labels, workflows, sales, providers, reports, import, help card, profile.
- **Every pre-existing URL** keeps working.

**Home (`/`)** is assembled from `IHomeCard`s:

1. **Four big tiles:** «ثبت فرش تازه» → `/Intake`; «نگله‌بندی» → the target shipment's bales tab; «ثبت هزینه» (Business; opens the cost panel); «اکسل و چاپ» → the target shipment's output tab.
   - **Target shipment.** `HomeSummaryDto.OpenShipments` lists every Open shipment, ordered by latest line activity. Each shipment tile shows its target under the label: «محمولهٔ ۸ · ۱۲ نگله · ۱۸۵ متر».
     - 0 open: Business users get «محمولهٔ تازه» (one tap, H.1); an Operator reads «هنوز محمولهٔ بازی نیست؛ از بازرگان بخواهید یکی بسازد.»
     - 1 open: the tile opens it directly.
     - 2 or more: the tile opens a sheet of 56 px buttons «محمولهٔ ۸ (۱۴۰۵/۰۶/۱۵) — ۱۲ نگله», with the latest focused.
   - **«ثبت هزینه» from Home** pre-fills «برای» with the target shipment; with none, «ورودی امروز»; with neither, «برای» stays empty and focused.
2. **Cards:**
   - open shipments: pieces (تخته), m², USD, bales;
   - readiness chips;
   - stock: pieces and m²;
   - money (Business only): cost per m² of the open shipment, unallocated entries;
   - recent activity.

### A.5 Money visibility (decision: Operators see no Toman money anywhere)

- **`App/Common/IMoneyVisibility`** is implemented in Web from the `Business` policy. `App/Common/MoneyRedaction` nulls the money members of DTOs.
- **Redaction happens in the services that build DTOs:** `RugManagementService` (get, search), `DashboardService`, `EntityMappers`, `WorkflowManagementService` (step types) and `ServiceProviderService`.
  - `RugDto.Costs` becomes nullable.
  - `RugDto.PurchaseCost` and `TargetSalePrice` are null for non-Business.
  - `RugListItemDto.TotalInvestment` and `CostPerM2` become `decimal?` and are null for non-Business.
  - **`RugWorkflowStepDto` money members** (`EffectiveCost`, `CalculatedCost`, `AppliedUnitRate`, `ManualCostOverride`, `Adjustment`, `PricingConfigJson`) are nulled; `EffectiveCost` becomes `decimal?`. `CA/lib/types` and `RugWorkflowPanel` follow in WP1-B.
  - **Every `DashboardStatsDto` money member** (including `OutstandingReceivable`) becomes `decimal?` and is null for non-Business.
  - `ProcessStepTypeDto.DefaultUnitRate` (`/api/lookups/step-types`) and the `ServiceProviderDto` rates are null for non-Business.
  - `RugSortBy.TotalCost` falls back to `CreatedAt` for non-Business, so sort order cannot leak cost.
- **Endpoints that are money by nature move to Business:** `GET /api/analytics` (`AnalyticsApiController.Get`: step costs, sales net, profit); `Aging` stays Floor (no money). `SalesApiController` moves to Business as a whole class (GET, PUT and DELETE `/api/sales/rug/{id}` and the report).
- **Money writes from non-Business users are refused.** Any request from a non-Business user that carries `PurchaseCost`, `TargetSalePrice`, `PricingModel`, `UnitRate`, `ManualCostOverride` or `Adjustment` returns 403: `PUT /api/rugs/{id}`, `PUT /api/rugs/bulk/fields`, `PUT /api/rugs/{id}/steps/{id}/pricing` and step completion. Step completion without these members falls back to provider or default rate injection.
- **Views:**
  - `Dashboard/Index.cshtml:55,68,124,171` hides the money tiles, the outstanding receivable and the investment column.
  - `Rugs/Index.cshtml:276,356` hides the investment cells.
  - `Rugs/Details.cshtml:140-182` hides the cost block and does not render `RugSalePanel`.
  - `RugWorkflowPanel` and `CostRuleEditor` hide step amounts and the rate dialog; completion then uses provider or default rate injection.
- **Reports moves to Business** (it shows profit).
- **USD is not internal cost.** Declared USD fields carry `FieldDefDto.Money = false` and stay visible to Floor.
- **Test:** the auth-matrix test asserts that no money key, and no Toman-formatted value, appears in any Operator JSON or HTML response, including `/api/analytics`, `/api/sales/rug/{id}`, `/api/rugs/{id}` (steps) and `/api/lookups/step-types`, and that each money write above returns 403 (L.2).

### A.6 End-to-end flow

1. **Intake.** Rugs are typed into the grid with serial, size and attributes; purchase lines are added (Business).
2. **Stock.** Rugs wait in stock.
3. **Shipment.** A shipment is created. Bales are packed by serial or scan, final sizes recorded, declared value defaulted from the table.
4. **Costs.** Cost entries are recorded at any time and allocated automatically.
5. **Output.** An Excel file or printout is produced from the default template.
6. **Finalize.** «قطعی» finalizes the shipment, which locks it. Late costs remain possible.

---

## B. Data model

### B.1 Conventions

- **Ids.** Guid ids are client-generated (`BaseEntity`). New children are added with `AddAsync`. Never call `Update()` on a just-added entity, and never on an **untracked** `Rug`, `RugWorkflowStep`, `RugSale`, `ShipmentLine` or `CostEntry` instance (architecture test; D.4 explains why).
- **Tenancy.** Every new tenant-owned table has `TenantId` (FK cascade to Tenants) and indexes that lead with `TenantId`. Repositories always filter by `tenantId`. Cross-tenant access → `KeyNotFoundException` → 404.
- **Soft delete.** Tables hanging off Rug get `HasQueryFilter(x => x.Rug.DeletedAt == null)`.
- **`xmin` concurrency** (`Property<uint>("xmin").IsRowVersion()`) is on the user-edited tables: Rug, RugBatch, Shipment, Bale, ShipmentLine, CostEntry, CostCategory, DocumentTemplate, ImportJob, TenantPreferences and TenantGridLayout. The generated xmin DDL is deleted from each migration by hand, and CI fails if a migration script contains xmin DDL (J.1 #3).
- **Derived tables** (`RugCostTotal`, `CostAllocation`, `CostEntryResult`, `ShipmentLineValue`) have no xmin. Only ledger stages write them, and those writes never touch `Rugs.xmin`.
- **Precision:**
  - dimensions `numeric(9,2)`
  - area `numeric(12,2)`
  - Toman/Rial money `numeric(18,2)`, stored at `MoneyScale` (default 0)
  - USD per m² and USD amounts `numeric(14,2)`
  - FX `numeric(18,6)`
  - percent `numeric(7,4)`
  - allocation weights `bigint`
  
  All SQL arithmetic stays `numeric`. There are no `float8` columns or parameters.
- **Enums** are stored as int with explicit values (B.14).
- **Text** goes through `PersianText.Normalize`. Serials are stored with Latin digits.
- **Uniqueness under soft delete** uses partial indexes (`WHERE "DeletedAt" IS NULL`). Nullable tenant keys use `AreNullsDistinct(false)`.
- **One `IEntityTypeConfiguration` per entity.** Existing entities stay in `EntityConfigurations.cs`; new entities go in per-feature `*Configurations.cs` files.

### B.2 M0: TenantCounter and User.SecurityStamp

**`TenantCounter`**
- Columns: `TenantId`, `Key varchar(40)`, `Value bigint`; primary key `(TenantId, Key)`.
- Keys: `sku:{yyyyMM}`, `shipment`, `batch`, `cost`, `import`.
- `ITenantCounters.NextBlockAsync(tenantId, key, n)` runs:

  ```sql
  INSERT … ON CONFLICT ("TenantId","Key")
    DO UPDATE SET "Value" = "TenantCounters"."Value" + n
  RETURNING "Value"
  ```

  It is atomic and needs no retry. The first use of an `sku:` key seeds from the existing maximum suffix (including soft-deleted rows).

**`User.SecurityStamp`** is `varchar(64)`, not null. Existing rows are back-filled with random values.

### B.3 M1: platform tables

- **`User`** gains:
  - `UiMode int`
  - `TextScale int`
  - `GridEnter int`
  - `CoachDismissedJson jsonb?`

**`TenantPreferences`** (1:1 with Tenant; primary key `TenantId`; xmin)

| Column | Type / default | Notes |
|---|---|---|
| `SkuPrefix` | `varchar(12)` `'RUG'` | |
| `SerialMode` | `SerialMode`, SuggestNext | |
| `SerialStart` | `int?` | Lower bound for the next-serial rule (G.4) |
| `MoneyScale` | `smallint`, 0 (0..2) | **Locked once any money exists** (D.5 #22) |
| `MaxPlausibleSideM` | `numeric(4,2)`, 12 | A typed side above this asks «متر یا سانت؟» (G.4) |
| `DefaultUsdFx` | `numeric(18,6)?` | |
| `DefaultMethod` | ByArea | |
| `DefaultAreaBasis` | Effective | |
| `DeclaredValueKeyField` | `varchar(60)` `'rug.origin'` | |
| `DeclaredValueFallbackUsd` | `numeric(14,2)?` | |
| `DeclaredValuesStamp` | `int`, 0 | Concurrency stamp for the replace-all PUT |
| `DefaultBaleTargetAreaM2` | `numeric(6,2)?` | |
| `BaleTargetTolerancePercent` | `smallint`, 10 | |
| `RequireFinalSizeToFinalize` | `bool`, true | |
| `BaleTerm` | `BaleTerm`, Negleh | |
| `DefaultDeclarationTemplateId` | `uuid?` | FK added in M5 |
| `ExporterName`, `ExporterCode`, `ExporterAddress`, `ExporterPhone`, `CustomsOffice` | text | Exporter header fields |

**Other M1 tables and columns**
- **`TenantGridLayout`:** `TenantId`, `GridId varchar(20)`, `ColumnsJson jsonb`, xmin; primary key `(TenantId, GridId)`.
- **`AuditEntry`:** `+ DetailsJson jsonb?`.
- **`CustomFieldDefinition`:**
  - `+ Scope int` (Rug = 0)
  - `+ ShowInGrid bool`
  - `+ GridOrder int?`
  - `+ Exportable bool` (default true)

### B.4 M2: Rug v2 (`[RequiresBackup("Rugs")]`)

- **Serial:**
  - `SerialNo varchar(40)?`, the normalised display form («3778» or «3911 - 3960»)
  - `SerialFrom int?`, `SerialTo int?`
  - `SerialSort bigint?` (= `SerialFrom`; null for non-numeric serials such as «A-12»)
  - A partial unique index on `(TenantId, SerialNo) WHERE DeletedAt IS NULL AND SerialNo IS NOT NULL`. Range overlap is checked by `SerialOverlapStage` under the tenant ledger lock.
- **Quantity:** `Quantity int not null default 1`, with `CHECK (Quantity >= 1)` and `CHECK (SerialFrom IS NULL OR SerialTo - SerialFrom + 1 >= Quantity)`. A range may be longer than the quantity, never shorter.
- **Intake size:** `IntakeLength` and `IntakeWidth` are `numeric(9,2) not null`.
- **Final size:** `FinalLength`, `FinalWidth numeric(9,2)?`, with a both-or-none CHECK. Also `FinalMeasuredAt timestamptz?` and `FinalMeasuredBy uuid?`.
- **Effective size:** `LengthMeters` and `WidthMeters` remain the effective pair (= Final ?? Intake), now `numeric(9,2)`. Only `RugFieldWriter` writes them, so legacy SQL keeps working.
- **M2 dimension order** (one transaction; the effective pair and the intake pair must agree on every row):
  1. Copy the originals to `_m2_rug_dims_backup(RugId, Length, Width)` and log the number of rows whose values change on rounding.
  2. `UPDATE "Rugs" SET "LengthMeters" = round("LengthMeters", 2), "WidthMeters" = round("WidthMeters", 2)` (PostgreSQL `round(numeric)` is half away from zero).
  3. `ALTER` both columns from unbounded `numeric` to `numeric(9,2)`.
  4. Add `IntakeLength`/`IntakeWidth` and set them from the rounded pair.
  5. Add the generated columns:
     - `AreaM2 = round("LengthMeters"*"WidthMeters", 2)` (per piece, effective)
     - `TotalAreaM2 = round("LengthMeters"*"WidthMeters", 2) * "Quantity"`
     - `IntakeAreaM2 = round("IntakeLength"*"IntakeWidth", 2)`
  
  The self-verify asserts `"LengthMeters" = COALESCE("FinalLength","IntakeLength")` and the same for width on every row; `LedgerVerifier` keeps checking that invariant. `M2.Down()` restores the dimensions from the backup table and drops the generated columns; only M7 drops the backup table (B.15).
- **Grid idempotency:** `ClientRowId uuid?`, unique `(TenantId, ClientRowId)` where not null.
- **Legacy purchase cost:** M2 moves non-null values into one `Total` purchase line («قیمت خرید»). Nothing writes the column after M2, and M7 drops it.
  - `[Obsolete]` is added to `Rug.PurchaseCost` in the **last P2 merge** (WP2-B), after every other usage is gone, because CI builds with `-warnaserror`. The one remaining reference, the EF mapping in `EntityConfigurations.cs`, is wrapped in a local `#pragma warning disable CS0618`. No csproj changes.
  - `RugDto.PurchaseCost` reports Σ purchase-line amounts (never lump shares, never the obsolete column) and is null for non-Business. It is display-only.
  - The legacy write rules for `UpdateRugRequest.PurchaseCost`, `WidthMeters` and `LengthMeters` are in I.3: they never collapse multi-line purchases and never overwrite the intake size with the final size.
- **Location** is computed, not stored (C.10).
- **M2 self-verify:** in the same transaction, it recomputes every rug's total. The new total must equal the old `TotalInvestment` within 0 for unchanged data; if not, it raises an exception (L.2). It then back-fills `RugSale.CostAtSale` = the verified Total and `CostAtSaleAt` = `SoldAt` for existing sales (D.6).

### B.5 M2: RugBatch, RugSale and RugWorkflowStep additions

- **RugBatch:** `Number int` (counter `batch`; unique `(TenantId, Number)`), `SellerName varchar(200)?`, `SellerKey varchar(200)?` (`KeyNormalize(SellerName)`, empty for no seller), `ReceivedOn date?` (the Iran day of `ReceivedAt`), xmin. A partial unique index on `(TenantId, ReceivedOn, SellerKey) WHERE ReceivedOn IS NOT NULL` (nulls not distinct) is the «ورودی» key: one batch per seller per day (H.1). `Name` is filled as «ورودی {Number} · {SellerName} · {jalali date}».
- **RugSale:** `CostAtSale numeric(18,2)?`, `CostAtSaleAt timestamptz?`.
- **RugWorkflowStep:** `PricedLength`, `PricedWidth numeric(9,2)?` and `PricedQuantity int?`. This snapshot is taken when a step is priced. Re-pricing uses the snapshot unless «با اندازهٔ تازه» is chosen. Every pricing model's lot rule is in D.3 («Step pricing on lots»).

### B.6 M2: RugPurchaseLine

- **Columns:**
  - `Id`, `TenantId`
  - `RugId` (cascade)
  - `CategoryId?` (Restrict)
  - `Label varchar(120)`
  - `Basis` (`CostBasis`: Total, PerM2, PerPiece, Percent)
  - `Rate numeric(18,4)`
  - `Amount numeric(18,2)` (written by `PurchaseLineStage`)
  - `SortOrder`, `Notes`
- **Rug filter** applies.
- **PerM2 lines always use the intake area** (M.2 #12).
- **Percent lines** apply to the sum of the rug's non-Percent lines: `Amount = ROUND(Σ non-Percent line amounts × p/100, MoneyScale)`, once per line (D.3 lot-rule table).
- **Intake-grid columns** map to these lines by category (G.2 «Purchase columns»).

### B.7 M2: CostCategory

- **Columns:**
  - `Id`, `TenantId`
  - `SeedKey varchar(60)?` (unique per tenant where not null)
  - `Name(120)`
  - `Group` (Purchase, Expense)
  - `Icon?`, `SortOrder`, `IsActive`
  - Defaults: `DefaultBasis`, `DefaultRate?`, `DefaultCurrency?`, `DefaultMethod`, `DefaultAreaBasis`, `DefaultScope?`
  - `ShowInIntake bool`
  - `StepTypeId?` (used for the `W_STEP_DOUBLE` check)
  - xmin
- **`Group` cannot change once the category is used.**

### B.8 M2: RugCostTotal (derived)

- **Columns:**
  - `RugId` (primary key, cascade), `TenantId`
  - `PurchaseCost`, `StepCost`, `ExpenseCost`, `Total` (`numeric(18,2)`)
  - `CostPerM2?`, `CostPerPiece`, `ExportRevenueShare?`
  - `ComputedAt`, `LedgerVersion bigint`
- **Rug filter** applies.
- **All seven `TotalInvestment` sites read from here** (D.4).

### B.9 M3: shipments

| Table | Columns |
|---|---|
| **Shipment** | `Id`, `TenantId`, `Number int` (counter `shipment`, unique), `Title(200)`, `DocumentDate date`, `Destination?`, `BuyerName?`, `DeclarationNo?`, `CustomsOffice?`, `Status`, `FinalizeCount int`, `FinalizedAt?`, `FinalizedBy?`, `SnapshotJson text?`, `SnapshotHash char(64)?`, `LastSnapshotJson text?`, `TargetBaleAreaM2?`, `UsdRate numeric(18,6)?`, `RevenueAmount numeric(18,2)?`, `RevenueCurrency int?`, `RevenueFx numeric(18,6)?`, `Notes?`, xmin. The snapshots are `text`, not `jsonb`, so the canonical bytes that `SnapshotHash` covers survive storage (jsonb reorders keys). |
| **Bale** | `Id`, `TenantId`, `ShipmentId` (cascade), `Number int`, `WeightKg numeric(8,2)?`, `Notes?`, xmin. Bale numbers are unique per shipment through `CONSTRAINT "UX_Bales_Shipment_Number" UNIQUE ("ShipmentId","Number") DEFERRABLE INITIALLY DEFERRED`, written as hand-edited SQL in M3 (recorded in `HandEdits/Shipments.md`) instead of a unique index, so a renumber is one flush. Renumbering also runs inside `IUnitOfWork.ExecuteInTransactionAsync` (D.4). |
| **ShipmentLine** | `Id`, `TenantId`, `ShipmentId`, `BaleId` (NO ACTION), `RugId` (NO ACTION; **unique**, so a rug is in at most one line), `Seq int`, `UsdPerM2Override numeric(14,2)?`, xmin. Index `(BaleId, Seq)`. Rug filter applies. |
| **ShipmentLineValue** (derived) | `LineId` (primary key, cascade), `TenantId`, `ShipmentId`, `UsdPerM2?`, `RateSource`, `PieceUsd?`, `LineAreaM2`, `LineUsd?`, `ComputedAt` |
| **DeclaredValueRate** | `Id`, `TenantId`, `KeyValue(120)`, `KeyNormalized(120)` (unique per tenant), `UsdPerM2 numeric(14,2)` |

### B.10 M4: cost ledger

**CostEntry**
- Identity: `Id`, `TenantId`, `Number` (counter `cost`).
- What and when: `CategoryId` (Restrict), `Date date`, `Description?`, `ReceiptNo?`, `ServiceProviderId?` (NO ACTION).
- How much: `Basis` (Total, PerM2, PerPiece), `Rate?`, `Amount?`, `Currency`, `FxRate?`, `BaseAmount` (tenant currency; computed), `FrozenAt timestamptz?` (set when a PerM2/PerPiece entry is frozen as a receipt, D.3).
- How it splits: `AreaBasis`, `Method`, `Scope`.
- Where: `ShipmentId?` (**SET NULL**), `BatchId?` (**SET NULL**), `ScopeNote?`.
- `ClientKey uuid?` (unique per tenant where not null; makes create idempotent).
- `DeletedAt?` and xmin.

**Child and derived tables**
- **CostEntryTarget:** `EntryId` (cascade), `RugId?` (cascade), `BaleId?` (cascade); CHECK exactly one is set.
- **CostEntryPin:** `EntryId` (cascade), `RugId` (cascade), `Amount numeric(18,2)` in the **tenant base currency** (D.3 «Pins»). Pins are Total-basis only.
- **CostAllocation** (derived): `EntryId` (cascade), `RugId` (cascade), `TenantId`, `Amount`, `WeightUnits bigint`, `IsPinned`; primary key `(EntryId, RugId)`. Rug filter applies.
- **CostEntryResult** (derived): `EntryId` (primary key, cascade), `Lines`, `Pieces`, `AreaM2`, `TotalWeightUnits`, `Allocated`, `Unallocated`, `State`, `WarningsJson jsonb`.

### B.11 M5: documents

- **DocumentTemplate:**
  - `Id`, `TenantId`, `Name`, `Kind`, `SeedKey?`
  - `IsDefault` (partial unique per `(TenantId, Kind)`)
  - `Revision int`, `LayoutJson jsonb`, `UsesMoneyFields bool`
  - xmin
- **DocumentTemplateRevision:** `TemplateId` (cascade), `Revision`, `LayoutJson`, `SavedAt`, `SavedBy`.
- **ExportRun:**
  - `Id`, `TenantId`
  - `ShipmentId?` (SET NULL), `TemplateId?` (SET NULL), `TemplateRevision`
  - `Format`, `IsDraft`, `DataHash char(64)`, `FileSha256?`
  - `UserId`, `At`, `RugCount?`
- **M5 also adds** the FK `TenantPreferences.DefaultDeclarationTemplateId` → DocumentTemplates (SET NULL).

### B.12 M6: import

**ImportJob**
- Identity: `Id`, `TenantId`, `Number` (counter `import`).
- File: `FileName(200)`, `FileSha256`, `StoredPath` (server-generated).
- State: `Status`, `Sheet?`, `HeaderRow?`, `MappingJson?`, `OptionsJson?`, `Mode`, `ExistingSerialPolicy`.
- Parsed title: `TitleText?`, `TitleDate?`.
- Results: `PreviewJson?`, `ResultJson?` (created ids; attached rugs' previous values and xmin), `ShipmentId?` (SET NULL), `BatchId?` (SET NULL).
- Audit: `CreatedBy`, `CommittedAt?`, xmin.

**ImportMappingPreset:** `TenantId`, `Name`, `HeaderSignature`, `MappingJson`.

### B.13 Foreign keys and delete behaviour

| FK | Behaviour | Pass trigger (D.5) |
|---|---|---|
| ShipmentLine → Rug | NO ACTION (restrict semantics, checked at statement end, so a cascade from a tenant still works). A rug in a line cannot be hard-deleted. | — |
| ShipmentLine → Bale | NO ACTION. A bale can be deleted only when it is empty. | — |
| Bale → Shipment | Cascade. A shipment can be deleted only when Open and line-free. | #17 |
| CostEntryTarget → Rug / Bale | Cascade | #6, #18 |
| CostEntryPin → Rug | Cascade | #6 |
| CostAllocation → Rug / CostEntry | Cascade | derived |
| CostEntryResult, ShipmentLineValue, RugCostTotal → parent | Cascade | derived |
| RugPurchaseLine → Rug | Cascade | #6 |
| CostEntry → Shipment / RugBatch | SET NULL. The entry becomes Unallocated with `W_TARGET_DELETED`. | #17, #12 |
| CostEntry → CostCategory | Restrict. A category in use is deactivated, not deleted. | — |
| CostEntry → ServiceProvider | NO ACTION. A provider with history cannot be deleted (existing rule). | — |
| ExportRun, ImportJob → Shipment / Template / Batch | SET NULL | — |
| Every new table → Tenant | Cascade. No tenant hard delete exists today; this is declared for safety. | — |

There is no permanent rug delete today: `RugsController.Delete` (`:89-90`) is a soft delete. The only hard delete of rugs is an import undo, which goes through the pass (F.7). An architecture test forbids `Remove(Rug)` outside `ImportService.UndoAsync`.

### B.14 Enums: every member has an explicit value

| Enum | Members |
|---|---|
| `UserRole` | SystemAdmin 0, TenantAdmin 1, Operator 2, **Merchant 3** |
| `RugStatus` | Draft 0, InProgress 1, ReadyForSale 2, Sold 3, Archived 4, **Exported 5** |
| `CurrencyUnit` | Toman 0, Rial 1, **Usd 2**. Usd is never offered as a tenant currency; the settings page whitelists Toman and Rial. |
| `UiMode` | Simple 0, Full 1 |
| `TextScale` | Normal 0, Large 1, XLarge 2 |
| `GridEnterDirection` | Down 0, Right 1 |
| `BaleTerm` | Negleh 0, Adl 1 |
| `SerialMode` | Manual 0, SuggestNext 1, Auto 2 (behaviour of each: G.4 «Next serial») |
| `CostGroup` | Purchase 0, Expense 1 |
| `CostBasis` | Total 0, PerM2 1, PerPiece 2, Percent 3 (purchase lines only) |
| `AreaBasis` | Intake 0, Effective 1 |
| `AllocationMethod` | ByArea 0, ByCount 1, Equal 2, ByDeclaredValue 3, ByPurchaseCost 4, Manual 5 |
| `RugLocation` | InStock 0, InBale 1, Exported 2, Sold 3, Trashed 4 |
| `ShipmentStatus` | Open 0, Finalized 1 |
| `DeclaredRateSource` | Override 0, Table 1, Fallback 2, Missing 3 |
| `CostScope` | Selection 0, Bale 1, Shipment 2, IntakeBatch 3 |
| `AllocationState` | Allocated 0, Partial 1, Unallocated 2 |
| `DocumentKind` | ShipmentDeclaration 0, ShipmentCost 1, RugList 2 |
| `ExportFormat` | Xlsx 0, Print 1 |
| `ImportStatus` | Uploaded 0, Previewed 1, Committed 2, Undone 3, Failed 4 |
| `ImportMode` | Declaration 0, StockOnly 1 |
| `ExistingSerialPolicy` | Attach 0, Skip 1, Error 2 |
| `ProviderWorkKind` | Step 0, Receipt 1 |
| `GridRowStatus` (Application) | Saved 0, Invalid 1, Conflict 2, Deleted 3 |

`AuditAction` additions are all made in P1:
- 60 PreferencesChanged, 61 PresetApplied, 62 GridLayoutChanged
- 70 RugSplit, 71 FinalSizeRecorded
- 80 ShipmentCreated, 81 ShipmentUpdated, 82 ShipmentDeleted, 83 ShipmentFinalized, 84 ShipmentReopened
- 85 BaleCreated, 86 BaleDeleted, 87 BalesRenumbered, 88 LinesChanged, 89 DeclaredValuesChanged
- 90 CostEntryCreated, 91 CostEntryUpdated, 92 CostEntryDeleted, 93 CostEntryRestored, 94 CostCategoryChanged, 95 RevenueChanged
- 100 TemplateCreated, 101 TemplateUpdated, 102 TemplateDeleted, 103 TemplateRestored, 104 Exported
- 110 ImportCommitted, 111 ImportUndone
- 120 LedgerRebuilt

An architecture test checks that every enum member under `Domain/Enums` and `App/DTOs` has an explicit initializer.

### B.15 Migration order and the regenerate rule

| # | Migration | Phase | Content | Backup |
|---|---|---|---|---|
| — | **Chain repair** (no new migration) | P0 | Edit `20260604082452_PendingModelSync` and `20260705115025_SyncModelState` so both `Up()` methods use `ADD COLUMN IF NOT EXISTS` for the three step-pricing columns. `SyncModelState.Down()` is a no-op; `PendingModelSync.Down()` runs `ALTER TABLE "RugWorkflowSteps" DROP COLUMN IF EXISTS` for each of the three columns, so `RugBatches.Down()` can re-add them without 42701. Either history order, or both, then migrates up and down. | — |
| M0 | `TenantCountersAndSecurityStamp` | P0 | B.2 | — |
| M1 | `PlatformPreferences` | P1 | B.3 | — |
| M2 | `RugModelV2AndCosting` | P2 | B.4–B.8, data moves, self-verify | **`[RequiresBackup("Rugs")]`** (rounds legacy dimensions) |
| M3 | `Shipments` | P4a | B.9 | — |
| M4 | `CostLedger` | P5 | B.10 | — |
| M5 | `Documents` | P6-wiring | B.11 | — |
| M6 | `Import` | P8 | B.12 | — |
| M7 | `DropRugPurchaseCost` | P9 | Drops the column | **`[RequiresBackup("Rugs")]`** |

**`[RequiresBackup(table)]` gate.** The Migrator refuses such a migration only when the named table has rows and `Database:ConfirmedBackupFor` ≠ the migration id. An empty table, a fresh install or CI is never blocked.

**`Down()` for M0–M7.** Every migration has a working `Down()`. In particular `M2.Down()` restores the dimensions from `_m2_rug_dims_backup` and drops the generated columns; the backup table is dropped only by M7; `M7.Down()` re-adds `PurchaseCost` and back-fills it from Σ purchase lines. A CI step runs `database update` to head, then down to `20260603141744_CatalogPatternsLabels`, then back up.

**Regenerate rule.** Nobody hand-merges `AppDbContextModelSnapshot.cs`.
- If a lane's migration would merge out of the fixed order, or its snapshot conflicts after a rebase, that lane:
  1. deletes its migration;
  2. rebases onto the merged head;
  3. regenerates with the same name (`dotnet ef migrations add`);
  4. re-applies its hand edits (xmin removal, data SQL), which are kept in `Migrations/HandEdits/{Name}.md` for exactly this purpose.
- CI then proves `has-pending-model-changes` is empty.

---

## C. Shipments, bales, final sizes, declared values, finalize

### C.1 Lifecycle

- **Status transitions:** Open → Finalized → (reopen) Open. `FinalizeCount` increments on every finalize.
- **Create** (Business). One tap on «محمولهٔ تازه» creates it with no dialog. Defaults:
  - `DocumentDate` = today (Iran);
  - title «اظهار {number}»;
  - `TargetBaleAreaM2` and `UsdRate` from preferences.
  
  The optional fields (destination, buyer, declaration number, customs office, USD rate, notes) sit under «بیشتر» in the shipment header.
- **Delete** (Business) is allowed only when the shipment is Open and has no lines. Empty bales go with it.
  - Cost entries whose `ShipmentId` pointed at it become Unallocated with `W_TARGET_DELETED`, after confirm «۲ هزینه به این محموله وصل است و «تقسیم‌نشده» می‌شود».
  - Export runs are kept.

### C.2 Bales

- **Create** (Floor): number = max + 1, or a typed number. **A bale is normally created by its first rug:** an `add` op whose `baleNumber` does not exist yet creates that bale in the same pass, so the merchant never has to make an empty bale first.
- **«نگلهٔ جاری» (current bale).** One model shared by the table, the cards and pack mode (H.1):
  - the bales-tab header shows «نگلهٔ جاری: ۳ ▾  [نگلهٔ بعد]»; the serial box and scans add to it; a new grid row defaults its نگله cell to it;
  - «نگلهٔ بعد» only sets current = max + 1. Nothing is created until its first rug arrives, so no empty bale is left behind;
  - the current bale is per user and per shipment (client state, `localStorage` with try/catch), defaulting to the highest-numbered bale, or 1 when there is none.
- **Renumber** (Floor, Open only) closes gaps to 1..n in current order, in one flush (B.9 deferred uniqueness).
- **Target bar** (target = shipment `TargetBaleAreaM2` ?? preference; tolerance t %):

  | Area | Bar |
  |---|---|
  | Below target × (1 − t) | Neutral |
  | Within ± t | Success |
  | Above target × (1 + t) | Warning |

  For example, target 13 m² with 10 % turns to warning above 14.3 m².
- **Delete** only when empty (409 otherwise). Its `CostEntryTarget(BaleId)` rows cascade, and the pass re-allocates those entries. An entry with no remaining target becomes Unallocated with `W_TARGET_DELETED`.
- **An empty bale** is allowed while Open. It is a blocking readiness issue (`E_EMPTY_BALE`), and documents skip it with `W_EMPTY_BALE`.

### C.3 Lines (membership)

**Ways to add a rug** to the current bale (`baleId|baleNumber`):
- type its serial;
- scan it (QR or `code_128`, exact match; `BarcodeScan.vue`, H.6);
- pick it from stock;
- use the stock bulk bar «افزودن به نگله», which opens a picker of the target shipment's bales (A.4), «نگلهٔ جاری» first, plus «نگلهٔ تازه».

**Serial matching**

| Match | Result |
|---|---|
| Exact match on `SerialNo` | `matchType: exact` |
| A number inside a range | `matchType: inRange` → «این شماره داخل دستهٔ ۳۹۱۱ تا ۳۹۶۰ است: [کل دسته را بگذار] [جدا کن]» |

**Where the rug already is**

| Situation | Outcome |
|---|---|
| Another bale of this shipment | Moved after confirm |
| Another Open shipment | Moved after confirm «این فرش در محمولهٔ ۷ / نگلهٔ ۳ است؛ منتقل شود؟» |
| A Finalized shipment | Refused (lock) |
| Trashed or sold | Refused |

- **Order:** `Seq` appends; dragging reorders (`order` op), and so does the «انتقال» picker (H.1), so no action needs dragging.
- **Removing a line** returns the rug to stock. The row menu «خارج کردن از نگله» shows the toast «۳۷۷۸ به انبار برگشت [برگردان]»; «برگردان» re-adds the rug to the same bale at the same `Seq`.

### C.4 Final sizes

- **Edit:** `PUT /api/rugs/{id}/final-size`, or the `measure` op inside lines/batch.
  - «همان اندازه» (key `=`, or the row button «همان») copies the intake size.
  - `clear` reverts to intake.
  - **In bulk:** the bales bulk bar has «همان اندازه», and the `E_NO_FINAL_SIZE` readiness chip offers «[بقیه همان اندازهٔ اول]». It asks «اندازهٔ نهایی ۱۲ فرش همان اندازهٔ اول ثبت شود؟ [ثبت کن] [نه]», then sends `measure` ops with `sameAsIntake` for `ReadinessIssueDto.RugIds`.
  - **Focus flow:** after a rug is added by serial, focus moves to that row's final «طول» with the first size as placeholder («۲٫۰۸»). Tab goes to «عرض»; Enter returns to the serial box (H.1).
- **`SizeChangeCm`** is the signed larger-magnitude change of length or width, in cm. The badge reads «↓۵ سانت» or «↑۲ سانت», with `aria-label` «۵ سانت کوچک‌تر شده».
- **The final size changes:**
  - effective area
  - declared USD
  - ByArea allocations with `AreaBasis = Effective`
  - PerM2 entries with Effective basis
  - cost per m²
- **It never changes:** purchase PerM2 lines (intake basis), or intake-basis washing and repair entries.

### C.5 Declared value (USD per m²)

**Resolution per line**
1. Line override (`rate` op; Business).
2. Table rate by the normalised value of `DeclaredValueKeyField` (default `rug.origin`).
3. Tenant `DeclaredValueFallbackUsd`.
4. Missing (`E_NO_DECLARED_VALUE`, blocking).

**`KeyNormalize`:** `PersianText.Normalize`, then Latin digits, then collapsed spaces. So «نائین ۹ لا» = «نائين 9 لا».

**Validation.** `UsdPerM2Override`, `DeclaredValueRate.UsdPerM2` and `DeclaredValueFallbackUsd` must be ≥ 0 (400).

**A new key the merchant can fix himself.** `POST /api/declared-values` (Business) `{keyValue, usdPerM2, stamp}` only **adds** a key that is missing after `KeyNormalize`; an existing key returns 409, and editing or deleting rates stays Configure. It is audited as `DeclaredValuesChanged` and re-values every open line with that key (D.5 #20). The `E_NO_DECLARED_VALUE` chip and the «ندارد» rate-source chip in the bales grid open the inline form «ارزش گمرکی «کاشان» برای هر متر: [___] دلار [ثبت]».

**Recalculation:** Open shipments recalculate whenever an input changes (D.5). Finalized shipments are frozen.

**Values per line**
- `PieceUsd = ROUND(pieceArea × rate, 2)`
- `LineUsd = PieceUsd × qty`

This is the per-piece rule (G.4).

### C.6 Readiness

`GET /api/shipments/{id}/readiness?templateId=` returns `ReadinessIssueDto{Code, Message, Count, RugIds, Blocking}`.

| Code | Blocking | Meaning |
|---|---|---|
| `E_EMPTY_SHIPMENT` | yes | No lines |
| `E_EMPTY_BALE` | yes | A bale without lines |
| `E_NO_FINAL_SIZE` | yes when `RequireFinalSizeToFinalize` | A line without a final size |
| `E_NO_DECLARED_VALUE` | yes | Rate Missing |
| `W_BALE_OVER_TARGET` | no | Above target + tolerance |
| `W_NO_SERIAL` | no | Rug without a serial |
| `W_NO_USD_RATE` | no (template preflight) | Template uses `shipment.usdRate` and it is empty |
| `W_TEMPLATE_FIELD_EMPTY` | no (template preflight) | A template column is empty on N rows |

Each chip carries its Persian message and fix actions from `Messages.fa` (H.3b), for example «[نشانم بده]» (filters the bales grid to `RugIds`), «[بقیه همان اندازهٔ اول]» (C.4) and «[نرخ بگذارید]» (C.5). `ShipmentListItemDto.ReadinessBlocking` for the list comes from one grouped SQL over all listed shipments (D.4 «Performance»).

### C.7 Finalize and snapshot

`POST /finalize` (Business, with `{version}`) runs in one pass. Readiness must have no blocking issue. Then:
1. `Status = Finalized`, `FinalizeCount++`, `FinalizedAt/By` set.
2. Every rug in a line gets `Status = Exported`, set-based (one split query loads all line rugs with their steps, `WHERE "Id" = ANY(@ids)`; D.4 «Performance»).
3. `SnapshotBuilder` writes `SnapshotJson` (a `text` column): canonical JSON with sorted keys, invariant numbers at fixed scale, NFC text. It holds:
   - header fields;
   - bales and rows (serial, qty, descriptive and exportable custom fields, intake and final sizes, piece and line area, rate, rate source, piece and line USD);
   - totals;
   - the terms and exporter fields in use.
   
   It contains **no Toman money**.
4. `SnapshotHash = SHA-256(UTF-8 bytes of SnapshotJson)`. Because the column is `text`, re-hashing the stored value gives the same hash (an L.2 test checks this). It is stable across re-finalize when nothing changed.

**Documents of a Finalized shipment** render from the snapshot, using the template revision chosen at export time. `ExportRun` records the template, revision and `DataHash` (= `SnapshotHash`).

### C.8 Lock rules (`ShipmentLockGuard`, an `ILedgerGuard`, so every write path is covered)

**When a shipment is Finalized**

| Rejected (409 `ShipmentLockedException`) | Allowed |
|---|---|
| A rug's intake or final size, quantity, serial, descriptive fields and exportable custom fields, and the `DeclaredValueKeyField` value | Cost entries (including entries targeting this shipment) |
| `Rug.Status`, except the changes made by `FinalizeService` and reopen | Purchase lines |
| Trash and delete of the rug | Revenue |
| A sale | Shipment `Notes` |
| Workflow advance, go-back and activate, and workflow path edits (`UpdateWorkflowPath`, `BulkUpdateWorkflow`), which re-resolve the status and would overwrite `Exported` | Provider data |
| Line ops (add, move, remove, measure, rate, order) | Images, rug notes |
| Bale create, delete, renumber and weight | — |
| Shipment `Title`, `DocumentDate`, `Destination`, `DeclarationNo`, `CustomsOffice` and `UsdRate` | — |

**Guards see only real changes.** They consume the value diff from `ChangeSetBuilder` (D.4), never EF's `IsModified` flags, so a `rugs.Update(rug)` that only touches `UpdatedAt` or the notes of an Exported rug passes.

**`BaleMembershipGuard`** (P4a):
- a rug is in at most one line (also the unique index);
- line ops are allowed only on Open shipments;
- a line's bale belongs to the line's `ShipmentId`;
- a rug in a line cannot be trashed or sold (C.10 refusals).

Rejection copy: «این فرش در محمولهٔ قطعی‌شدهٔ ۸ است…» (Business) or «این محموله قطعی شده؛ برای تغییر به بازرگان بگویید.» (Operator). A banner says late costs change profit, not the declaration.

### C.9 Reopen

`POST /reopen` (Business, with `{version, reason?}` audited):
- `Status = Open`.
- Rugs return to the status that `RugStatusResolver` gives, resolved in memory after one split query for all line rugs and their steps.
- `SnapshotJson` moves to `LastSnapshotJson`.
- `ShipmentLineValue` is recomputed from the current rate sources (table, fallback, key field), which may differ from the frozen values; the D.4 propagation rules then re-run the ByDeclaredValue entries and RevenueShare that intersect the changed lines.
- Outputs are «پیش‌نویس» until the next finalize.

### C.10 Location and status

**Location** is computed in SQL projections and in the enricher, never stored. The first condition that holds wins:

| Order | Condition | Location |
|---|---|---|
| 1 | `DeletedAt` set | Trashed |
| 2 | Has a sale | Sold («فروخته‌شده») |
| 3 | Line in a Finalized shipment | Exported («صادر شده») |
| 4 | Line in an Open shipment | InBale («در نگله») |
| 5 | Otherwise | InStock («در انبار») |

**`RugStatusResolver`:** Sold > Exported > workflow-derived status. `Exported` is never overwritten by the workflow.

**Refusals**
- Trashing a baled rug: «اول این فرش را از نگلهٔ ۱۲ خارج کنید.»
- Selling a baled rug: «این فرش در نگلهٔ ۱۲ است؛ اول آن را از نگله خارج کنید.»

---

## D. Costs: purchase lines, cost ledger, allocation, the Consistency Pass

### D.1 Where money attaches

1. **Purchase lines** on a rug, set at intake (Business):
   - «قیمت خرید»: PerM2, intake area
   - «دلالی»: Percent
   - «کرایه تا انبار»: PerPiece
   - any Total line
2. **Cost entries** (the «هزینه‌ها» ledger) for a set of rugs. Group `Purchase` (for example «خرید یکجا») or `Expense` (washing, repair, freight, customs…).
3. **Legacy workflow step costs** (`StepCost` bucket).

**`RugCostTotal`**
- `PurchaseCost` = Σ purchase lines + Σ Purchase-group allocations
- `StepCost` = Σ step `EffectiveCost` over Completed and InProgress steps, each floored at 0
- `ExpenseCost` = Σ Expense-group allocations
- `Total` = PurchaseCost + StepCost + ExpenseCost
- `CostPerM2` = ROUND(Total / TotalAreaM2, MoneyScale); null when the area is 0
- `CostPerPiece` = ROUND(Total / Quantity, MoneyScale)

### D.2 Cost entry: basis, scope, method

**Basis** (chosen per entry, also in Simple mode, H.2)
- `Total`: an amount to split.
- `PerM2`: rate × area. Live only while it is not a receipt (D.3 «Receipts freeze»).
- `PerPiece`: rate × pieces. Same rule.

**Scope**

| Scope | Targets | Membership |
|---|---|---|
| `Selection` | Explicit `CostEntryTarget.RugId`s. Includes «این فرش» and anything resolved from serial text, a filter or «همهٔ انبار». | Fixed list. A trashed rug drops out while trashed and returns on restore. |
| `Bale` | `CostEntryTarget.BaleId`s | Dynamic: the current lines of those bales |
| `Shipment` | `CostEntry.ShipmentId` | Dynamic: all lines of the shipment |
| `IntakeBatch` | `CostEntry.BatchId` | **Purchase group: snapshot.** On save the batch's current members are written as `CostEntryTarget.RugId` rows; `Scope` stays `IntakeBatch` for display and `ScopeNote` reads «۱۲ فرش ورودی ۳». Later `BatchId` changes do not move purchase money. «فرش‌های این ورودی را دوباره بگیر» re-snapshots on demand. **Expense group: dynamic** (rugs whose `BatchId` is the batch); a `BatchId` change on a rug that has an IntakeBatch-scoped expense share needs the confirm «این فرش در هزینهٔ «…» ورودی ۱ سهم دارد؛ با جابه‌جایی سهمش به فرش‌های دیگر می‌رسد» and is audited. |

`POST /api/costs/resolve-targets` turns serial text («3778، 3780-3790»), a `RugQuery` or «همهٔ انبار» into explicit RugIds **at save time**. There is never a live filter scope. `ScopeNote` records how the list was made: «۲۴۲ فرش انبار در ۱۴۰۵/۰۶/۱۵».

### D.3 Allocation rules (`App/Features/Costs/CostAllocator`, pure)

**Lot rule (applies everywhere: C#, SQL, `num.ts`, `ExcelCompiler`, composer).** Round per piece, then multiply by quantity. All rounding is half away from zero; `s` = MoneyScale; `fx` = the entry's FX (USD) or 10 / 0.1 (Rial ↔ Toman). Every row has a `math-vectors.json` entry (G.4).

| Quantity | Per piece | Line |
|---|---|---|
| Area | `ROUND(L×W, 2)` | piece area × qty |
| USD (declared) | `ROUND(pieceArea × usdPerM2, 2)` | piece USD × qty |
| Money, PerM2 | `ROUND(rate × pieceArea, s)` | piece × qty |
| Money, PerPiece | `ROUND(rate, s)` | piece × qty |
| Purchase Percent line | — | `ROUND(Σ non-Percent line amounts × p/100, s)`, once per line |
| Purchase Total line | — | `ROUND(rate, s)` |
| Foreign-currency PerM2 entry | `ROUND(ROUND(rate × pieceArea, 2) × fx, s)` | piece base × qty |
| Foreign-currency PerPiece entry | `ROUND(rate × fx, s)` | piece base × qty |
| Toman ↔ Rial PerM2 / PerPiece | as the two rows above, with fx = 10 or 0.1 | piece base × qty |
| Total entry, revenue | `BaseAmount = ROUND(amount × fx, s)` once, then LR | — |

Example: usdPiece 7.29, fx 105,000.5, qty 50 → piece base `ROUND(7.29 × 105,000.5) = 765,454` → line 38,272,700 (not `ROUND(364.50 × 105,000.5) = 38,272,682`).

**Step pricing on lots** (`StepCost`; C# `RugCostMath`, SQL `CostSql`). Rounding at MoneyScale, half away from zero, on the `Priced*` snapshot dimensions and `PricedQuantity`:

| Pricing model | Per piece | Line |
|---|---|---|
| Fixed / CustomFormula (acts as Fixed) | `ROUND(rate, s)` | × qty |
| PerSquareMeter | `ROUND(rate × pieceArea, s)` | × qty |
| PerSquareFoot | `ROUND(rate × pieceArea × 10.7639104, s)` | × qty |
| PerLength / PerWidth | `ROUND(rate × L, s)` / `ROUND(rate × W, s)` | × qty |
| Combined | Σ items, each rounded per piece | × qty |

`ManualCostOverride` and `Adjustment` are line-level (never × qty). For Fixed, the step dialog offers «برای کل دسته» to price the lot once (stored as a `ManualCostOverride`). These rules replace today's mixed `ToEven` 2-dp rounding (map §2.1); `CostCalculationServiceTests` move with them (L.1).

**Receipts freeze (PerM2 / PerPiece entries).** A PerM2 or PerPiece entry is live (T = Σ over the current members) only while it has **no `ServiceProviderId` and no `ReceiptNo`**. When either is set, saving freezes it:
- `Amount := current T`, `FrozenAt` is set, and `BaseAmount` is fixed from then on;
- it is allocated as a Total entry: ByArea with the same `AreaBasis` for PerM2, ByCount for PerPiece;
- if Σ(rate × current basis) later differs from `Amount`, the result carries `W_RATE_DRIFT` with the difference; «به‌روزرسانی مبلغ» re-freezes on demand.

So a paid invoice never shrinks when a member is trashed, and a provider statement is reproducible.

**Allocation table**

| Basis | Method | Per-rug amount | Weight (`WeightUnits`) |
|---|---|---|---|
| PerM2 (live) | — | lot-rule row above; T = Σ | — |
| PerPiece (live) | — | lot-rule row above; T = Σ | — |
| Total (or frozen) | ByArea | LR(T, w) | `pieceArea(basis) × 100 × qty` (hundredths of m²) |
| Total | ByCount | LR | `qty` |
| Total | Equal | LR | 1 per rug (a lot counts once) |
| Total | ByDeclaredValue | LR | `LineUsd` in cents. A rug with no line gets weight 0 and `W_NO_DECLARED_VALUE`. |
| Total | ByPurchaseCost | LR | `max(0, PurchaseCost) × 100` (cents, independent of MoneyScale). Expense group only; a Purchase-group entry gets `E_CIRCULAR`. |
| Total | Manual | Pins only | Σpins ≠ T → `Partial`, Unallocated = T − Σpins |

**Weights are never negative.** Each weight is `max(0, w)`; `W_NEGATIVE_WEIGHT` lists the clamped rugs (a negative PurchaseCost can come from a negative Total purchase line).

**Largest remainder (LR) algorithm**

1. Work with A = |T| in units of 10^−scale.
2. Use exact integer arithmetic (`Int128` / `BigInteger`):
   - floorᵢ = (A·wᵢ) div W
   - remᵢ = (A·wᵢ) mod W
3. Give one extra unit to each of the R = A − Σfloor rugs with the largest remᵢ.
4. Break ties by larger wᵢ, then lower `SerialSort` (nulls last), then `RugId` compared as its lowercase `"D"` string with `StringComparer.Ordinal` (= RFC 4122 byte order, the same in C#, PostgreSQL `uuid` and TypeScript). Never `Guid.CompareTo`.
5. Reapply the sign of T.

**Properties**
- Σ = T exactly.
- Every share has the sign of T, or is 0.
- |shareᵢ| ≤ |T|.
- |shareᵢ| ∈ {⌊|exactᵢ|⌋, ⌈|exactᵢ|⌉}.
- Independent of input order, and deterministic.
- Example: −100 split equally three ways gives −34 / −33 / −33.

**Pins** (Total and frozen entries only; a pin on a live PerM2 or PerPiece entry → 400 `E_PINS_BASIS`)
- Pins are stored and validated in **base currency**, after FX or Toman↔Rial conversion. `E_PINS_EXCEED` (400) compares Σpins with `BaseAmount`, and also fires for a pin with the wrong sign.
- Pinned rugs take their pin. The residual R = T − Σ(active pins) is allocated over the unpinned members.
- **All members pinned and R ≠ 0:** State = `Partial`, Unallocated = R, `W_PINS_RESIDUAL`. (This is how trashing or moving out the unpinned members ends.)
- The zero-weight → Equal rule applies to the **unpinned subset** only.
- A pin on a rug no longer in the targets is ignored with `W_PIN_DROPPED`. It stays stored and applies again if the rug returns.

**Empty or zero targets**
- Zero total weight with members → Equal, plus `W_ZERO_WEIGHTS`.
- No members and no active pins → `Unallocated` (whole T) plus `W_EMPTY_TARGET`; the chip shows «تقسیم‌نشده».
- **Invariant** (property-tested, L.1): Allocated + Unallocated = `BaseAmount` for every entry, including the all-pinned case.

**Currency.** `Amount` is in `Currency`; the lot-rule table above gives every conversion.
- **Toman ↔ Rial:** ×10 or ÷10, rounded to MoneyScale (Total: once on `BaseAmount`; PerM2/PerPiece: per piece).
- **USD:**
  - Total: × FxRate (`E_FX_REQUIRED` if missing), rounded to MoneyScale once, then allocated.
  - PerM2 and PerPiece: the piece amount is computed in USD at 2 dp, converted and rounded **per piece**, then × qty.
  
  The order is always original currency first, then base.

**Area basis.** Intake uses `IntakeArea`; Effective uses Final ?? Intake.

### D.4 The Consistency Pass (`App/Ledger`, P2)

**Steps** (inside `UnitOfWork.SaveChangesAsync`). The pass is **two-phase**: it reads and guards before the flush, and computes derived rows **after** the request's own rows are flushed, inside one transaction. SQL run before a flush cannot see the change tracker's pending rows, so no stage ever runs before step 6.

1. **Build** (in memory). `ChangeSetBuilder` produces a **value diff**: for every tracked entry it compares `OriginalValues` with `CurrentValues` per property and **ignores `EntityState.Modified` and `IsModified`**, because the legacy `Repository.Update()` (`DbSet.Update`) marks every column of a loaded graph Modified. Decimals compare by value; `xmin` and `UpdatedAt` are excluded. Added and Deleted entries (including deleted principals) are listed as such. An entity attached without a tracked load (no original snapshot) is marked **unknown**: it gets full impact for that rug, and the guards check every locked field against a fresh DB read.
2. **Fast-path decision** (in memory, no DB reads). If the ChangeSet touches no ledger-relevant and no guarded entity (`Rug`, `RugWorkflowStep`, `RugSale`, `RugPurchaseLine`, `RugBatch`, `Shipment`, `Bale`, `ShipmentLine`, `DeclaredValueRate`, `CostEntry` and its children, `CostCategory`, `TenantPreferences`, `Tenant`, `CustomFieldDefinition`), it is a plain `SaveChanges`. Everything else, including changes that only guards care about (descriptive fields, Status, bale renumber or weight, shipment header fields), takes the locked path below. Guards therefore run on **every** save that could violate a lock.
3. **Lock.** `BEGIN` (or reuse `Database.CurrentTransaction`, see «Ambient transactions»), then:

   ```sql
   SELECT pg_advisory_xact_lock(hashtext('rugs:ledger:' || tenantId));
   ```

   taken once per transaction.
4. **Contributors** (`IChangeImpactContributor`) map the diff to a `LedgerImpact`: rugs, entries, lines, shipments. **Every contributor DB read happens here, after the lock**, including the dependents of principals about to be deleted (the cascade and `SET NULL` happen in step 6, so they must be read now).
5. **Guards** (`ILedgerGuard`): `ShipmentLockGuard`, `BaleMembershipGuard`, `PreferencesLockGuard`, `KeyFieldGuard`, `CostCategoryGuard`. In-memory checks plus one indexed lookup of the affected rugs' shipment status per ChangeSet.
6. **Flush.** `await db.SaveChangesAsync(acceptAllChangesOnSuccess: false)` inside the transaction, with a re-entrancy flag so this flush does not start a nested pass. FK cascades and `SET NULL` happen here.
7. **Stages** (`ILedgerStage`) run in `LedgerStageOrder` as set-based SQL over the **flushed** state and upsert the derived rows. `SerialOverlapStage` sees the request's own rows (so an automatic lot split, which shortens the original and inserts the new range, is not a self-overlap); raising here rolls everything back. With an empty impact (a guard-only save) no stage runs.
8. **Derived entities in the tracker.** `LedgerSession` detaches every tracked `RugCostTotal`, `CostAllocation`, `ShipmentLineValue` and `CostEntryResult` entry for the affected keys, so a DTO built from the same context after the save re-reads fresh values. Repository loads include `RugCostTotal` only through `AsNoTracking` projections; `WorkflowEngine.CalculateRugCosts` reads a freshly queried `RugCostTotal` after `SaveChanges`.
9. **Commit.** `ChangeTracker.AcceptAllChanges()`, then `COMMIT` (or leave it to the ambient transaction's owner).

Writes to derived tables are excluded from the ChangeSet, so there is no recursion. On any exception: `ROLLBACK`, then `session.Reset()` clears the change tracker.

**Ambient transactions.** `IUnitOfWork.ExecuteInTransactionAsync(Func<CancellationToken, Task> work)` (P2) opens one transaction for several flushes. The pass reuses `Database.CurrentTransaction`, takes the advisory lock only once per transaction, and runs its stages after **each** flush. Users: `BaleService.Renumber`, the import commit (F.7), the import undo and the grid's per-row fallback (G.6).

**Propagation rules** (stage level, so no D.5 row has to enumerate second-order effects):
- `PurchaseRollupStage` emits the set of rugs whose `PurchaseCost` changed. `ExpenseEntryStage` re-runs every non-deleted **ByPurchaseCost** entry whose current members intersect that set, whatever its scope (intake-size changes, purchase-line CRUD and Purchase-group entries all move PurchaseCost).
- `ShipmentLineValueStage` emits the rugs whose `LineUsd` changed. `PurchaseEntryStage` and `ExpenseEntryStage` re-run every **ByDeclaredValue** entry that intersects them, and `RevenueShareStage` re-runs their shipments.
- Every entry re-run recomputes **all** of its warnings from scratch (`W_STEP_DOUBLE`, `W_NO_DECLARED_VALUE`, `W_PURCHASE_DOUBLE`, …), so stored `WarningsJson` never goes stale.

**Performance** (300-rug shipments under the tenant lock):
- Finalize and reopen load all line rugs plus their steps in one split query (`WHERE "Id" = ANY(@ids)`) and resolve statuses in memory.
- Guards resolve the locked rug set with one query per ChangeSet.
- `ShipmentListItemDto` readiness counts come from one grouped SQL for the whole list.
- Allocation stages diff-write only changed shares with set-based SQL.
- L.2 asserts: finalize and reopen of a 300-line shipment issue fewer than 20 SQL commands, and a `measure` op completes in under 200 ms.

**Bypass rule.** The architecture test "no writes bypass the pass" forbids `ExecuteUpdate`/`ExecuteDelete` and raw SQL writes everywhere except `Infra/Persistence/Ledger/*`, the per-feature `*LedgerRepository` classes that stages call (P4a `ShipmentLedgerRepository`, P5 `CostLedgerRepository`) and `TenantCounterRepository`, the only legitimate raw-SQL writers. It also forbids `Update()` on untracked `Rug`, `RugWorkflowStep`, `RugSale`, `ShipmentLine` and `CostEntry` instances (B.1).

**`LedgerStageOrder`** (`App/Ledger/LedgerStageOrder.cs`, published in P2 with reserved slots)

| Order | Stage | Owner |
|---|---|---|
| 100 | `SerialOverlapStage` | P2 |
| 200 | `PurchaseLineStage` | P2 |
| 300 | `ShipmentLineValueStage` | P4a (reserved) |
| 400 | `PurchaseEntryStage` | P5 (reserved) |
| 500 | `PurchaseRollupStage` | P2 |
| 600 | `ExpenseEntryStage` (ByDeclaredValue; ByPurchaseCost via `ByPurchaseCostExpander`) | P5 (reserved) |
| 700 | `RevenueShareStage` | P5 (reserved) |
| 1000 | `RugCostTotalStage` | P2. **Must be last.** |

An architecture test checks that the orders are unique, that `RugCostTotal` is the maximum, and that every registered stage uses a `LedgerStageOrder` constant.

`RugCostTotalStage` also writes `RugSale.CostAtSale` (and `CostAtSaleAt`) for rugs whose `RugSale` is Added in the ChangeSet, using the Total it has just computed, so the snapshot includes this pass's own changes (D.6).

**One formula site**
- The step `EffectiveCost` SQL `CASE` and the purchase/total composition exist only in `Infra/Persistence/Ledger/CostSql.cs` (SQL) and `Domain/Costing/RugCostMath.cs` (C#). An architecture test checks this, and a parity test compares the two.
- `RugCostTotalStage` computes set-based through `LedgerRepository`, using `CostSql` for steps.
- `ICostSourceProvider` (P2 ships a no-op default; P5 registers the real one) supplies purchase and expense allocations and `AnyEntriesAsync`.

**The seven `TotalInvestment` sites after P2** (map §2.2)

| Site | After P2 |
|---|---|
| 1 `WorkflowEngine.CalculateRugCosts` | Reads `RugCostTotal`; `RugCostMath` serves in-memory previews |
| 2–4 `RugRepository` (sort, list row, trash) | Join `RugCostTotal.Total` |
| 5–6 `ServiceProviderRepository` (balances, work items) | Use `CostSql.StepEffectiveCost`; P5 adds the receipt union |
| 7 `DatabaseSeeder` | Seeds through the pass and rebuild |

**Recovery**
- `LedgerVerifier` recomputes everything and lists mismatches (`/api/diagnostics/cost-consistency`).
- `ILedgerMaintenance.RebuildAsync` rewrites all derived rows.
- `LedgerStartupCheck` (an `IStartupTask`, Order 200) runs for each tenant:
  - if derived rows are missing, or the `LedgerVersion` constant changed, it rebuilds;
  - otherwise it verifies a random sample of 50 rugs and logs the result.

### D.5 Recompute triggers

| # | Change | Contributor (phase) | Impact |
|---|---|---|---|
| 1 | Rug added | `RugImpact` (P2) | Its purchase lines; Expense-group IntakeBatch entries of its batch (Purchase-group ones are snapshots, D.2); RugCostTotal |
| 2a | `IntakeLength`/`IntakeWidth` | `RugImpact` | Purchase PerM2 and Percent lines; entries containing it with ByArea(Intake) or PerM2(Intake). If there is no final size, also everything in #3. |
| 2b | `Quantity` (always, final size or not) | `RugImpact` | Purchase PerM2, PerPiece and Percent lines; entries containing it with ByArea (both bases), ByCount, PerM2 (both bases) or PerPiece; its `ShipmentLineValue` (`LineAreaM2`, `LineUsd`); ByDeclaredValue entries and RevenueShare of its shipment; CostPerM2 and CostPerPiece |
| 3 | `FinalLength`/`FinalWidth` | `RugImpact` | Effective-basis entries containing it; its `ShipmentLineValue`; ByDeclaredValue entries and RevenueShare of its shipment; CostPerM2 |
| 4 | `BatchId` (single, `AssignRugs`, `BulkUpdateFields`, `GroupDetail` move/split) | `RugImpact` | Expense-group IntakeBatch entries of the old and new batch (confirm and audit, D.2). Purchase-group entries are snapshots and do not move. |
| 5 | Soft delete / restore | `RugImpact` | Every entry containing it (resolution skips trashed rugs); refused when baled |
| 6 | Hard delete (import undo only) | `RugImpact` | As #5, plus cascades of its lines, pins and targets |
| 7 | Value of `DeclaredValueKeyField` | `RugImpact` (emits FieldChanged) → `ShipmentImpact` (P4a) | `ShipmentLineValue` (Open, no override); ByDeclaredValue; RevenueShare |
| 8 | Other descriptive fields, Status, serial, workflow path | — | Lock guards only (locked path, D.4 step 2) |
| 9 | `RugPurchaseLine` CRUD | `PurchaseLineImpact` (P2) | PurchaseLine and PurchaseRollup; RugCostTotal; `W_PURCHASE_DOUBLE` check. ByPurchaseCost entries follow by the D.4 propagation rule (as they do for #2a, #2b and #24). |
| 10 | `RugWorkflowStep` cost, status, add or remove | `StepImpact` (P2) | RugCostTotal; `W_STEP_DOUBLE` |
| 11 | `RugSale` add or remove | `SaleImpact` (P2) | `CostAtSale` snapshot on add; RugCostTotal; refused when baled |
| 12 | `RugBatch` deleted | `BatchImpact` (P5) | Entries with that `BatchId` (SET NULL) → Unallocated plus `W_TARGET_DELETED`; former members' RugCostTotal |
| 13 | `ShipmentLine` add, move, remove or reorder | `ShipmentImpact` (P4a) | `ShipmentLineValue`; Bale- and Shipment-scoped entries of the old and new bale or shipment; ByDeclaredValue; RevenueShare; Location |
| 14 | Line `UsdPerM2Override` | `ShipmentImpact` | `ShipmentLineValue`; ByDeclaredValue; RevenueShare |
| 15 | Shipment `UsdRate`, revenue fields | `ShipmentImpact` | RevenueShare |
| 15b | Shipment `Title`, `DocumentDate`, `Destination`, `DeclarationNo`, `CustomsOffice` | — | Lock guard only (locked path) |
| 16 | Finalize / reopen | `ShipmentImpact` | Rug Status via the resolver; snapshot. Finalize freezes `ShipmentLineValue`. **Reopen recomputes `ShipmentLineValue` from the current rate sources**; the D.4 propagation rule then re-runs the intersecting ByDeclaredValue entries and RevenueShare. |
| 17 | Shipment deleted (Open, line-free) | `ShipmentImpact` + `CostEntryImpact` (P5) | Entries with that `ShipmentId` (SET NULL) → Unallocated plus `W_TARGET_DELETED`. **Also**, read before the flush: every `CostEntryTarget` whose `BaleId` belongs to the shipment's bales (they cascade with the bales); those entries re-allocate, and one with no remaining target gets `W_TARGET_DELETED`. |
| 18 | Bale deleted (empty) | `ShipmentImpact` + `CostEntryImpact` (P5) | Entries whose target rows cascaded → re-allocated; none left → Unallocated plus `W_TARGET_DELETED` |
| 19 | Bale renumber, weight, notes | — | Lock guard only (locked path); no recompute (ids are stable) |
| 20 | `DeclaredValueRate` replace-all | `DeclaredValueImpact` (P4a) | Open shipments' lines without an override; ByDeclaredValue; RevenueShare |
| 21 | Preferences `DeclaredValueKeyField` / `DeclaredValueFallbackUsd` | **`PreferencesImpact` (P4a)** | Same as #20 |
| 22 | Preferences `MoneyScale`; `Tenant.Currency` | **`PreferencesLockGuard` (P2)** | Refused (409) once any stored money exists: a `RugPurchaseLine`; a `RugWorkflowStep` with a cost; a `RugSale`; a `CostEntry`; a `ProviderPayment`; a `ServiceProviderRate`; a tenant `ProcessStepType` with `DefaultUnitRate > 0`; a non-null `Rug.TargetSalePrice`; a non-null `CostCategory.DefaultRate`; a non-null `Shipment.RevenueAmount` |
| 23 | Other preferences | — | None (defaults apply only to new entries) |
| 24 | `CostEntry` CRUD, restore, pins, targets | `CostEntryImpact` (P5) | That entry; old and new member rugs' RugCostTotal |
| 25 | `CostCategory.Group` | `CostCategoryGuard` (P5) | Refused when the category is used. Rename or icon: none. |
| 25b | `CostCategory.StepTypeId` | `CostEntryImpact` (P5) | Re-runs every entry of that category (warnings only; money unchanged) |
| 26 | `CustomFieldDefinition` deactivate or delete while it is `DeclaredValueKeyField` | `KeyFieldGuard` (P4a) | Refused |
| 27 | Lot split | `RugSplitService` → #1, #2, #9, #13 plus target duplication (D.9) | — |
| 28 | Import commit / undo | The underlying rows | One pass per job |
| 29 | Provider rates, payments | — | None (steps are snapshots; the ledger reads live) |
| 30 | Tenant delete | — | Database cascade; no pass (no tenant-delete path exists) |

`AffectedEntryFinder` (P5) finds entries from rugs, bales, shipments and batches, using indexes on `CostEntryTarget.RugId`, `CostEntryTarget.BaleId`, `CostEntry.ShipmentId` and `CostEntry.BatchId`.

### D.6 Sale snapshot, revenue and profit

- **On sale:** `CostAtSale = RugCostTotal.Total`, written by `RugCostTotalStage` in the same pass (D.4). Profit for a sold rug = `NetAmount − (CostAtSale ?? Total)`.
  - `ActualProfit` uses that formula in `EntityMappers`, `AnalyticsService` (monthly profit) and the sales report, replacing today's `net − live TotalInvestment`.
  - Pre-M2 sales get `CostAtSale` back-filled by M2 (B.4).
  - **Editing an existing sale** (`SaveAsync` with `isNew = false`) keeps `CostAtSale`, unless «به‌روزرسانی بهای تمام‌شده» is chosen.
- **Later changes:** if `Total` later differs from `CostAtSale`, a chip reads «هزینه پس از فروش تغییر کرده: +۲۰۰٬۰۰۰ تومان».
- **Revenue share:** shipment revenue is converted to base currency and split by LR over `LineUsd` cents. `ExportRevenueShare` per rug; profit per rug = share − Total; shipment profit = revenue − Σ Total.

### D.7 Worked example

Setup:
- Toman, MoneyScale 0.
- Categories from the preset.
- Intake batch «ورودی ۱» (seller «حاج رضا»).

| # | Action | Expected |
|---|---|---|
| 1 | Intake: A «3778» گبه 2.08×2.10; B «3911 - 3960» گبه qty 50 at 0.40×0.40; C «3782» گبه 3.35×2.52 | Areas A 4.37, B 0.16 per piece → 8.00, C 8.44. Total 20.81 m², 52 pieces. |
| 2 | Purchase lines: A «قیمت خرید» PerM2 9,000,000; B line basis «هر تخته» 300,000; C PerM2 8,500,000 | 39,330,000 / 15,000,000 / 71,740,000 |
| 3 | «دلالی» Percent 2 on each | 786,600 / 300,000 / 1,434,800. PurchaseCost 40,116,600 / 15,300,000 / 73,174,800. |
| 4 | شستشو PerM2 85,000, Intake basis, «برای: این فرش‌ها» {A,B,C}, provider «کارگاه شستشو» | 371,450 / 680,000 (= 13,600 × 50) / 717,400. T = 1,768,850. Because it has a provider, the save **freezes** it (D.3): Amount 1,768,850, allocated ByArea Intake over weights 437 / 800 / 844, which is exactly 850 per 0.01 m², so the shares are the same. The provider statement shows one receipt row of 1,768,850. |
| 5 | کرایهٔ خارجی Total 10,000,000, ByArea Effective, {A,B,C} | Weights 437 / 800 / 844. Exact 2,099,951.95 / 3,844,305.62 / 4,055,742.43. Floors sum to 9,999,998; the remaining 2 go to A and B. Result 2,099,952 / 3,844,306 / 4,055,742. |
| 6 | Rug totals | 42,588,002 / 19,824,306 / 77,947,942 (sum 140,360,250). Per m² 9,745,538 / 2,478,038 / 9,235,538. B per piece 396,486. |
| 7 | C final size 3.30×2.50 (8.25) | Freight re-split **2,119,302 / 3,879,728 / 4,000,970**. Washing and purchase are unchanged (intake basis). C total 77,893,170, per m² 9,441,596. A 42,607,352, B 19,859,728. Sum unchanged: 140,360,250. |
| 8 | Trash B (not baled), then restore | While trashed: freight 3,462,758 / 6,537,242 over A and C; the frozen washing receipt stays 1,768,850 and is re-split over A and C by intake weights 437 / 844 → **603,425 / 1,165,425**; A **44,182,783**; C **80,877,467**. The provider statement stays 1,768,850. After restore: exactly the values from #7. |
| 9 | Shipment 8 (1405/06/15): bale 1 = A, B; bale 2 = C; گبه = 45 USD/m². A and B are recorded as «همان اندازه» (final = intake), so finalize is not blocked by `E_NO_FINAL_SIZE` at #12. | USD: A 196.65; B 7.20 per piece → 360.00; C 371.25. Bale 1 «شامل ۵۱ تخته · ۱۲٫۳۷ · ۵۵۶٫۶۵». Bale 2 «شامل ۱ تخته · ۸٫۲۵ · ۳۷۱٫۲۵». Shipment 52 / 20.62 / 927.90. |
| 10 | گمرک و ترخیص Total 3,000,000, ByDeclaredValue, Scope Shipment | Weights 19,665 / 36,000 / 37,125 cents → 635,790 / 1,163,919 / 1,200,291 |
| 11 | Revenue 1,800 USD × 105,000 = 189,000,000 | Shares 40,054,801 / 73,326,867 / 75,618,332. Totals 43,243,142 / 21,023,647 / 79,093,461 (sum 143,360,250). Profit −3,188,341 / 52,303,220 / −3,475,129. Shipment profit 45,639,750. |
| 12 | Finalize, then a late «انبارداری» Total 600,000 ByArea, Scope Shipment | Allowed, with a banner. 127,158 / 232,784 / 240,058. Shipment profit 45,039,750. The declaration export keeps the same `DataHash` and has no draft mark. |

### D.8 Warnings and errors (`WarningDto{Code, Message, Count}`)

**Warnings**

| Code | Meaning |
|---|---|
| `W_STEP_DOUBLE` | The category is linked to a step type that this rug also has with a cost |
| `W_PURCHASE_DOUBLE` | The rug has a non-zero purchase line and a Purchase-group allocation |
| `W_PIN_DROPPED` | A pin's rug is no longer in the targets |
| `W_ZERO_WEIGHTS` | Total weight is zero; split equally |
| `W_NO_DECLARED_VALUE` | A target rug has no declared value |
| `W_EMPTY_TARGET` | The entry has no members |
| `W_TARGET_DELETED` | The entry's bale, shipment or batch was deleted |
| `W_LATE_COST` | The entry was added after finalize |
| `W_RATE_DRIFT` | A frozen receipt's rate × current basis now differs from its amount |
| `W_PINS_RESIDUAL` | Every member is pinned and T − Σpins ≠ 0; the residual stays unallocated |
| `W_NEGATIVE_WEIGHT` | Some weights were negative and were clamped to 0 |

**Errors (400)**

| Code | Meaning |
|---|---|
| `E_PINS_EXCEED` | Pins exceed `BaseAmount` or have the wrong sign |
| `E_PINS_BASIS` | Pins on a live PerM2 or PerPiece entry |
| `E_CIRCULAR` | ByPurchaseCost on a Purchase-group entry |
| `E_FX_REQUIRED` | USD amount without an FX rate |
| `E_SCOPE_EMPTY_SELECTION` | Selection scope with no rugs |

Every code has its Persian message in `Messages.fa` (H.3b).

### D.9 Lot split (`RugSplitService`, `POST /api/rugs/{id}/split`)

- **Preconditions:**
  - qty ≥ 2;
  - 1 ≤ k < qty;
  - no sale on the lot (sell after splitting; M.2 #27);
  - its shipment is not Finalized.
- **Serials:**
  - automatic only when range length = qty (the new rug takes the last k numbers);
  - otherwise both sub-ranges are typed and validated against overlap.
- **New rug:**
  - copies the descriptive fields, intake and final sizes, batch and custom fields;
  - gets a new SKU from the counter;
  - if the original is baled, gets a new line in the same bale at `Seq + 1`.
- **Purchase lines:**
  - PerM2, PerPiece and Percent lines are copied (they recompute by qty);
  - Total lines are split by qty with LR.
- **Pins** are split by qty with LR.
- **Selection targets.** The new rug joins every entry that targets the original explicitly (`CostEntryTarget` rows duplicated). Bale, shipment and batch membership follow automatically.
- **Workflow steps** are copied; `CalculatedCost`, `ManualCostOverride` and `Adjustment` are split by qty with LR.
- **Images** stay with the original. The new rug's page shows «جداشده از 3911 - 3960».
- **The pass** re-allocates. The action is audited as `RugSplit`.
- **Dialog.** It previews the result before confirming: «۵۰ تخته ← ۳۰ + ۲۰ · شماره‌ها ۳۹۱۱ تا ۳۹۴۰ و ۳۹۴۱ تا ۳۹۶۰ [جدا کن] [نه]». A split has no one-click undo in v1 (pins, targets and steps are split too); the preview is the safeguard.

---

## E. Documents (output designer, xlsx, print)

### E.1 Kinds and starter templates

| Kind | Starter (`Defaults/*.json`) | Phase | Money |
|---|---|---|---|
| ShipmentDeclaration | `shipment-declaration.v1` «صورت نگله بندی و اظهار» (default) | P6 | no |
| RugList | `rug-list.v1`: `lotRow: perPiece`; columns serial, `rug.qty` (`visibleWhen: hasLots`, right after serial), pattern, origin, size, area (per piece), location; a totals footer with count = `SUM(qty)` and area = `SUMPRODUCT(qty, area)` | P6 | no |
| ShipmentCost | `shipment-cost.v1` (per-rug cost, cost per m², allocations) | **P7** | yes |

`DocumentTemplateSeedContributor` seeds every embedded `Defaults/*.v*.json` by `SeedKey`, so P7 adds a file and never edits the contributor. The files are embedded because P0 adds `<EmbeddedResource Include="Features\Documents\Defaults\*.json" />` to `Application.csproj` (the wildcard works before the folder exists; the csproj is frozen after P0).

### E.2 Layout JSON (`schemaVersion: 1`)

**Shape** (the complete default, `shipment-declaration.v1`, is written out in E.9):

```jsonc
{ "schemaVersion": 1, "kind": "ShipmentDeclaration", "lotRow": "perPiece" | "perLine",
  "page": { "paper": "A4", "orientation": "portrait", "fitToWidth": true, "scale": 95,
            "margins": { "left", "right", "top", "bottom", "header", "footer" },          // inches
            "zoom": 70, "rtl": true, "font": "B Nazanin", "fontSize": 11, "rowHeight": 16.8,
            "printTitleRows": 2, "keepGroupTogether": true, "draftHeader": "پیش‌نویس",
            "summaryPlacement": "sheet" },
  "sheets": [
    { "name": "اظهار", "role": "main", "columnStyle": "<style>",
      "title":   { "fromCol": "<columnKey>", "toCol": "<columnKey>" | "last", "expr": "<expression>", "style": "<style>" },
      "columns": [ <column> … ],
      "group":   { "by": "bale", "mergeKey": true, "keyColumn": "<columnKey>", "skipEmpty": true,
                   "footerStyle": "<style>", "footer": [ <footerCell> … ] },
      "grandTotal":   null | { "label": "<text>", "cells": [ <footerCell> … ], "style": "<style>" },
      "checkAgainst": [ { "footerId": "<id>", "ledgerField": "<catalogue key>" } … ] },
    { "name": "خلاصه", "role": "summary", "startCol": "D",
      "title":   { "fromCol": "C", "toCol": "H", "expr": "<expression>", "style": "summaryTitle" },
      "columns": [ { "label": "<text>", "from": "footer:<id>" } … ],
      "headerStyle": "<style>", "rowStyle": "<style>",
      "equalsRow": true,
      "totals":  { "sum": [ "<footer id>" … ], "blankBordered": [ "<footer id>" … ] } } ],
  "styles": { "<name>": { "font", "size", "colorRgb"?, "fillRgb"?, "border": "none" | "thin" | "outer",
                          "hAlign", "vAlign", "shrinkToFit"? } … } }
```

- **Column:** `{key, label, width, field? | expr?, format: "general"|"0.00"|…, visibleWhen: "always"|"hasLots"|"never", style?, headerStyle?}`. `width` is in Excel character units, written exactly.
- **Footer cell:** `{id, col: <columnKey>, span: <n visible columns>, text? | expr? | agg: "count"|"sum", of?: <columnKey>, style?}`. **`col` is always a column key, never a letter**, so footers follow when the qty column shifts the letters (I:J → J:K). Aggregates:
  - `count` is pieces (`ROWS()` when qty is hidden, `SUM(qty)` when shown);
  - `sum(of)` is weighted by qty (`SUMPRODUCT`) when `lotRow = perPiece` and the qty column is visible.
- **Summary columns** bind to footer ids (`footer:count`, `footer:area`, `footer:usd`); `FormulaResolver` turns them into the right cross-sheet A1 links (E.9). Summary-sheet letters (`startCol`, title range) are fixed because that sheet has no lot shift.
- **Text tokens.** `label` and `text` may contain `{terms.bale}`, `{terms.baleFooter}` and the other `terms.*` keys (I.6); expressions reference the same values as catalogue fields `terms.*` (E.3).
- **`checkAgainst`** compares a footer cell with a ledger total per group and gives `W_CHECK_MISMATCH` (E.5).

**`LayoutValidator` rules** (each with a Persian message and a JSON path):
- unique column keys, footer ids and style names;
- references resolve (fields in the catalogue, `col.*` keys, styles, `of`, `footer:<id>`, `checkAgainst.footerId`, an `Aggregatable` `ledgerField`);
- no cycles;
- `title.fromCol` / `toCol` and `group.keyColumn` are column keys (or `last`); a footer `col` that looks like an A1 letter is rejected;
- each footer cell's `span` fits inside the visible columns, and footer cells do not overlap;
- merges do not overlap;
- **with `lotRow: perPiece`, a `rug.qty` column visible at least `hasLots` is required**;
- summary `totals` ids exist among its columns;
- style values are from the enums above (border `none|thin|outer`; colours `AARRGGBB`);
- at most 30 columns;
- a money field sets `UsesMoneyFields`.

### E.3 Field catalogue (`FieldCatalog` + `IFieldCatalogContributor`; `GET /api/fields`)

`FieldDefDto(Key, Label, Scope rug|line|bale|shipment|tenant|terms|purchase, DataType, DefaultFormat, Aggregatable, Money, GridEditable)`.

| Scope | Keys |
|---|---|
| rug | `rug.serial`, `rug.qty`, `rug.custom.{key}` (for example `bg_color` «رنگ متن», `border_color` «رنگ حاشیه»), `rug.pattern`, `rug.origin`, `rug.material`, `rug.intakeLength`, `rug.intakeWidth` |
| line | `line.length` and `line.width` (effective), `line.area` (per piece), `line.usdPerM2`, `line.pieceUsd`, `line.rateSource` |
| bale | `bale.number`, `bale.pieces`, `bale.area`, `bale.usd` |
| shipment | `shipment.number`, `.title`, `.documentDate`, `.destination`, `.buyerName`, `.declarationNo`, `.customsOffice`, `.usdRate` |
| tenant | `tenant.exporterName`, … |
| terms | `terms.bale`, `terms.bales`, `terms.baleOf`, `terms.baleFooter` (Text; from `BaleTerm`, I.6) |
| money (`Money = true`; Business only) | `rug.purchaseCost`, `rug.stepCost`, `rug.expenseCost`, `rug.totalCost`, `rug.costPerM2`, `rug.costPerPiece`, `rug.revenueShare` |
| purchase (`Money = true`, `GridEditable = true`; Business only; grid purpose only) | One key per active Purchase-group category with `ShowInIntake`: its `SeedKey` (preset keys already start with `purchase.`, for example `purchase.price`, `purchase.broker`), or `purchase.cat:{Id}` for a category the configurator created without one. Data type `money+unit` for Total/PerM2/PerPiece defaults, `percent` for Percent. Contributed by `PurchaseFieldContributor` (P3). |

USD fields have `Money = false`.

### E.4 Expression language (`App/Features/Documents/Expressions`)

- **Literals:** invariant numbers, `"text"` (with `""` as the escape), `true`, `false`.
- **References:** `col.<key>` (same row) and catalogue fields.
- **Operators:** `+ - * /` and unary minus; `= <> < <= > >=` on numbers only (text comparisons are rejected); `&` concatenation.
- **Functions:** `round(x, n)`, `if(c, a, b)`, `and`, `or`, `not`, `coalesce(a, b)`, `abs`, `min`, `max`, `jdot(date)`.
- **Footer aggregates:** `sum(col.x)`, `sumproduct(col.a, col.b)`, `count()`.
- **Types:** Number, Text, Bool, Date. A column's result must be Number or Text.
- **Excel semantics:**
  - a blank is 0 in arithmetic and "" in concatenation;
  - `/0` evaluates to blank and compiles to `IF(b=0,"",a/b)`.
- **Limits:** 1,000 characters, depth 32. Cycles are rejected.

**Compilation (`ExcelCompiler`)**
- `col.*` becomes a same-row A1 reference. Catalogue fields are inlined as constants.
- `round(col.length*col.width,2)` → `ROUND(H3*I3, 2)` without the qty column, and → `ROUND(I3*J3, 2)` with it.
- `round(col.area*col.rate,2)` → `ROUND(J3*K3, 2)`.
- Footer `sum` over a perPiece lot layout → `SUMPRODUCT(H3:H6,K3:K6)`.
- `coalesce(a,b)` → `IF(a="", b, a)`.
- The optional `lotRow: perLine` example `round(col.length*col.width,2)*col.qty` → `ROUND(I3*J3, 2)*H3`, folded to `ROUND(H3*I3, 2)` when qty is hidden.
- A text literal over 255 characters makes the cell value-only.

### E.5 Composer and render model

1. **Data.** `IDocumentDataRepository` loads the shipment in one query: lines, rugs, values and terms. For a Finalized shipment it loads `SnapshotJson` instead.
2. **Compose.** `DocumentComposer` builds `RenderModel`: title rows, header rows, bale groups (data rows plus footer), the summary sheet and manual page breaks.
3. **Resolve.** `FormulaResolver` binds logical references (group ranges, footer anchors, cross-sheet links) to A1 after layout. Breaks add no rows, so addresses are stable.
4. **Evaluate.** `Evaluator` computes every cached value with the `Num` rules (G.4).

The same `RenderedDocumentDto` feeds three outputs:
- print (Razor);
- the xlsx writer;
- preview JSON (`maxGroups` limits the size).

`checkAgainst` (a footer cell compared with a ledger total) gives `W_CHECK_MISMATCH`. The default template checks count, area and USD per bale against `bale.pieces`, `bale.area` and `bale.usd`; on the sample it never fires.

### E.6 Day-1 spike (gates P6)

Prove with ClosedXML 0.105.1 and DocumentFormat.OpenXml (the version ClosedXML requires):

1. Writing works:
   - RTL and zoom;
   - Persian sheet names;
   - merges;
   - formula text;
   - fonts, fills and borders;
   - column widths and custom row heights;
   - `fitToPage` with `fitToHeight = 0`;
   - margins;
   - `_xlnm.Print_Titles` on one sheet;
   - manual row breaks (`rowBreaks`);
   - a centre header text;
   - `fullCalcOnLoad`.
2. `CachedValueInjector` (OpenXml) sets `<v>` on every formula cell from the composer's decimals, and writes invariant numbers with no float noise.
3. Excel and LibreOffice open the file and show the values before recalculating; recalculating gives the same values.
4. ClosedXML reads cached values without evaluating, reads merges, and reads 10,000 rows in under 3 s.
5. **In Excel and LibreOffice print preview**, a `fitToWidth = 1` / `fitToHeight = 0` sheet **honours the manual `rowBreaks`** (Excel documents that it ignores manual breaks under "Fit To"). The check is visual: page 2 of the full sample starts at bale 11.
6. `fitToPage = 1` and `scale = 95` are both written, as in the sample (through `CachedValueInjector` if ClosedXML treats them as alternatives), and the title colour renders as `#767171`.

If ClosedXML fails two or more of 1–4, the writer switches to pure OpenXml behind the same `IXlsxDocumentWriter`. **If 5 fails**, the writer emits `fitToPage = 0` with an explicit `scale = ⌊s × 100⌋` from `PageBreakPlanner` (100 for the sample's 12 columns, 97 with the qty column), and the planner computes capacity from that integer scale (E.7). This is recorded as an intentional golden difference (E.10).

### E.7 xlsx writer rules

**Cell content**
- **Numbers** are numeric cells. **A serial is written as a number only when it is all digits, has no leading zero and has at most 15 digits** («3778»); ranges, «0123» and other serials are text.
- **Text** is always a string cell, never a formula. **Data-sourced** text starting with `= + - @` (a rug's field value) gets the quote prefix (`quotePrefix`). Template literals, such as the summary's «=» row, are plain strings with no quote prefix, as in the sample (D298 `s=1`).
- **Number format** is General (numFmtId 0) unless the template asks otherwise. The sample has no `numFmts`, so it shows 2.9 and 4.37.
- **Fonts.** The workbook Normal style font stays **Calibri 11**, as in the sample; B Nazanin is applied through column and cell styles only. (The page-break width rule depends on it.)

**Merges**
- Merges are written only when rowspan or colspan > 1. **A single-row bale gets no A-column merge.**
- **Empty bales** are skipped, with `W_EMPTY_BALE` in the preview. This avoids reversed ranges such as `SUM(J8:J7)`.

**Rows and printing**
- **Row heights:** every row is written with `ht = 16.8` and `customHeight = 1`, so the layout is the same regardless of font fallback.
- **Print titles** `'اظهار'!$1:$2` are set on the main sheet only. The summary sheet has none.

**Bale-aware page breaks** (`keepGroupTogether`, default true)

1. Printable height H = (11.69 − top − bottom) in × 72 = 769.68 pt.
2. Sheet width uses Excel's column-to-pixel rule with the Normal font's maximum digit width: Calibri 11 = 7 px; pixels per column = ⌊(256w + ⌊128/7⌋)/256 × 7⌋; sheet width in pt = Σpx × 72/96. If a template changes the Normal font, the planner takes that font's maximum digit width from a table, or else adds 2 more slack rows.
3. Printable width = (8.27 − left − right) in × 72 = 566.64 pt. Scale s = min(1, printable width ÷ sheet width in pt), **not** floored to an integer percent in `fitToPage` mode (Excel's own fit scale is the floored percent, so using the unfloored s is conservative). In the spike's fallback mode (E.6 #5) s = ⌊s × 100⌋ / 100, the scale actually written.
4. Capacity C = ⌊H ÷ (16.8 × s)⌋ − 2 rows of slack.
5. Page 1 holds C rows. Later pages hold C − 2 body rows, because of the print titles.
6. Walk the bale blocks (data rows plus footer). Insert a manual break before any block that does not fit in what remains of the page.
7. A block taller than a full page is broken inside, with `W_BALE_TOO_LONG`.

Worked numbers:
- **no lots** (12 columns): 734 px = 550.5 pt, s = 1, C = 43;
- **lots** (13 columns): 772 px = 579 pt, s = 0.9787, C = 44 (fallback mode: s = 0.97, C = 45).

On the sample's data (lots, C = 44) this puts bales 1–10 on page 1, 11–19 on page 2, 20–22 on page 3 and 23–27 on page 4, which is the merchant's own grouping. In fallback mode (C = 45) page 2 holds bales 11–20.

**Other rules**
- **Draft:** the centre header is «پیش‌نویس». Final: no header. There is no page footer, as in the sample.
- The file has no external links and no macros. `ExportRun` stores `FileSha256` and `DataHash`.
- **Download name.** `Content-Disposition: attachment; filename="declaration-{number}-{yyyy-MM-dd}.xlsx"; filename*=UTF-8''اظهار-{number}-{jdot}[-پیش‌نویس].xlsx` (RFC 5987; the draft suffix only on drafts).
- **Summary sheet and Ctrl+P.** The summary lives on its own sheet (M.2 #15), so Excel's default «Print Active Sheets» prints only the main sheet. The Output tab therefore makes «چاپ» (the print page, which always ends with the summary) the primary button, and both the Output tab and the help card say «در اکسل برای چاپ خلاصه، «چاپ کل کتاب کار» را بزنید».

### E.8 Print page (`/shipments/{id}/print`, Layout = null)

- `@@page { size: A4 portrait; margin: 0.5in 0.2in; }`.
- **Each `RenderedSheet` is its own `<table>`.**
- **Main table:** a `<thead>` with the title row (fill `#ffff00`, text `#767171`) and the header row, repeated on every page; then one `<tbody class="bale">` per bale with `break-inside: avoid`. `table-layout: fixed` with column widths proportional to the Excel widths, so 12 or 13 columns fit A4 portrait. Long text cells use `white-space: nowrap` with a per-cell fitted font size (the print counterpart of `shrinkToFit`).
- **Summary:** a separate `<section style="break-before: page">` with its own title and header (never repeated), then one row per bale, the «=» row and the totals. **The last printed page is the summary, with no main header above it.**
- Footer rows are yellow (`#ffff00`).
- Digits are Persian.
- Drafts get a fixed «پیش‌نویس» header.
- Fonts: `"B Nazanin", Vazirmatn` (self-hosted).
- A nonce'd `autoprint` script. The console stays clean under CSP.
- **On screen** (`DocumentPreview.vue`), the same model renders inside a `.paper` surface with a fixed light background and the template's own colours (as in print), framed by design tokens, so dark mode never inverts the yellow fills.

### E.9 Default declaration template: cell by cell (`shipment-declaration.v1`)

**Main sheet «اظهار».** `rightToLeft = 1`, zoom 70, default row height 16.8, every row `customHeight`. Every `<col>` carries the sample's column style (B Nazanin 11, centred; `columnStyle: "plain"`).

**Columns without lots** (widths are the sample's exact values)

| Col | Header | Width | Content |
|---|---|---|---|
| A | `{terms.bale}` («نگله») | 3.88671875 | `bale.number` (number), merged vertically when the bale has ≥ 2 rows |
| B | شماره | 6.5546875 | `rug.serial`: a number when all digits (E.7 rule), else text («3911 - 3960») |
| C | رنگ متن | 7.88671875 | `rug.custom.bg_color` |
| D | رنگ حاشیه | 8.6640625 | `rug.custom.border_color` |
| E | نقشه | 9.109375 | `rug.pattern` |
| F | محل بافت | 8 | `rug.origin` |
| G | مواد مصرفی | 10.109375 | `rug.material` (header at **9 pt**) |
| H | طول | 6.109375 | `line.length` |
| I | عرض | 6.44140625 | `line.width` |
| J | مساحت | 7.109375 | `=ROUND(H3*I3, 2)` |
| K | ارزش متر مربع | 14.88671875 | `line.usdPerM2` |
| L | جمع کل دلار | 16 | `=ROUND(J3*K3, 2)` |

**With lots** (`visibleWhen: hasLots`): H «تعداد» (width 5.5; header style, 11 pt) is inserted, and the old H..L become I..M.
- **H holds the numeric qty on every data row: 1 for single rugs, never blank**, so `SUM(H…)` and `SUMPRODUCT(H…, …)` count every piece.
- Area: K = `=ROUND(I3*J3, 2)` (per piece).
- USD: M = `=ROUND(K3*L3, 2)` (per piece).
- The title merge becomes A1:M1; footer fill and borders span A:M; the footer area and USD anchors move to J and L.

**Rows**

| Row | Content | Style |
|---|---|---|
| 1 | A1:L1 merged (A1:M1 with lots). Text constant (value only) «صورت نگله بندی و اظهار فرش صادراتی    تاریخ  1405.06.15». **Four spaces before تاریخ, two after.** Built from the title expression with `terms.bale` and `jdot(shipment.documentDate)`. | B Nazanin 11, colour RGB **`FF767171`** (the sample's Background 2 darker 50 %, written as RGB so it does not depend on the theme part), solid fill `FFFFFF00`, thin border, centred horizontally and vertically |
| 2 | Headers as above | B Nazanin 11 (G2 at 9 pt), thin borders, centred, no fill |
| 3.. | One row per line in `Seq` order | B Nazanin 11, General, thin borders, centred, **`shrinkToFit = 1`** (long origins such as «خراسان تجاری» shrink as the merchant did by hand at 8–9 pt) |
| footer | A:B merged `{terms.baleFooter}` («نگله شماره») · C bale number · D «شامل» · E count · F «تخته» · G, H blank (G..I with lots) · I:J merged area sum (J:K) · K:L merged USD sum (L:M) | Whole row A..last column (A:L, or A:M with lots): fill `FFFFFF00`, B Nazanin 11, thin borders, centred |

**Footer formulas**
- **No lots:**
  - E `=ROWS(B3:B6)`
  - I `=SUM(J3:J6)`
  - K `=SUM(L3:L6)`
- **Lots:**
  - E `=SUM(H3:H6)`
  - J:K `=SUMPRODUCT(H3:H6,K3:K6)`
  - L:M `=SUMPRODUCT(H3:H6,M3:M6)`

**Page setup**
- `paperSize = 9` (A4), portrait.
- `fitToPage = 1`, `fitToWidth = 1`, `fitToHeight = 0`, `scale = 95`, as in the sample. If the E.6 spike shows that "Fit To" ignores the manual breaks, the writer instead emits `fitToPage = 0` with `scale = ⌊s × 100⌋` (100 without lots, 97 with lots), an intentional golden difference (E.10).
- Margins: L/R 0.2, T/B 0.5, header/footer 0.3.
- Print titles `$1:$2`; manual row breaks (E.7).
- There are no spacer rows. The merchant's spacer rows and re-typed titles (sample rows 44–45, 46–47, 85–88, 121–131, 171–174, 211–217, 226) are replaced by print titles and page breaks.

**Summary sheet «خلاصه».** Same view, page setup and column widths; no print titles.

| Row | Content | Style |
|---|---|---|
| 1 | C1:H1 merged, text «صورت نگله بندی و اظهار فرش صادراتی  تاریخ1405.06.15» (two spaces before تاریخ, none after, as at sample row 260; `terms.bale` token) | B Nazanin 11, fill `FFFFFF00`, **no border**, centred |
| 2 | D2:G2: `{terms.bale}` · تعداد فرش · متراژ · ارزش گمرکی | Thin border, centred |
| 3..(n+2) | One row per printed bale, linked to that bale's footer by footer id (`footer:baleNo`, `footer:count`, `footer:area`, `footer:usd`). **Without lots:** D `='اظهار'!C7`, E `='اظهار'!E7`, F `='اظهار'!I7`, G `='اظهار'!K7`. **With lots:** D `…!C{n}`, E `…!E{n}`, F `…!J{n}`, G `…!L{n}`. | Thin border |
| n+3 | D:G each «=» (template literals, no quote prefix) | **No border**, centred |
| n+4 | D blank; E `=SUM(E3:E{n+2})`, F `=SUM(F…)`, G `=SUM(G…)` | **Thin border including the blank D**, centred |

With `BaleTerm = Negleh` every token renders exactly as the sample; with `Adl` every occurrence (title, A2, summary D2, footers) becomes «عدل» / «عدل شماره».

**`shipment-declaration.v1` in full** (`Features/Documents/Defaults/shipment-declaration.v1.json`; omitted members take the defaults `format: "general"`, `visibleWhen: "always"`, `style: "cell"`, `headerStyle: "header"`):

```jsonc
{ "schemaVersion": 1, "kind": "ShipmentDeclaration", "lotRow": "perPiece",
  "page": { "paper": "A4", "orientation": "portrait", "fitToWidth": true, "scale": 95,
            "margins": { "left": 0.2, "right": 0.2, "top": 0.5, "bottom": 0.5, "header": 0.3, "footer": 0.3 },
            "zoom": 70, "rtl": true, "font": "B Nazanin", "fontSize": 11, "rowHeight": 16.8,
            "printTitleRows": 2, "keepGroupTogether": true, "draftHeader": "پیش‌نویس", "summaryPlacement": "sheet" },
  "sheets": [
    { "name": "اظهار", "role": "main", "columnStyle": "plain",
      "title": { "fromCol": "bale", "toCol": "last", "style": "title",
                 "expr": "\"صورت \" & terms.bale & \" بندی و اظهار فرش صادراتی    تاریخ  \" & jdot(shipment.documentDate)" },
      "columns": [
        { "key": "bale",        "label": "{terms.bale}",  "width": 3.88671875,  "field": "bale.number" },
        { "key": "serial",      "label": "شماره",         "width": 6.5546875,   "field": "rug.serial" },
        { "key": "bgColor",     "label": "رنگ متن",       "width": 7.88671875,  "field": "rug.custom.bg_color" },
        { "key": "borderColor", "label": "رنگ حاشیه",     "width": 8.6640625,   "field": "rug.custom.border_color" },
        { "key": "pattern",     "label": "نقشه",          "width": 9.109375,    "field": "rug.pattern" },
        { "key": "origin",      "label": "محل بافت",      "width": 8,           "field": "rug.origin" },
        { "key": "material",    "label": "مواد مصرفی",    "width": 10.109375,   "field": "rug.material", "headerStyle": "header9" },
        { "key": "qty",         "label": "تعداد",         "width": 5.5,         "field": "rug.qty", "visibleWhen": "hasLots" },
        { "key": "length",      "label": "طول",           "width": 6.109375,    "field": "line.length" },
        { "key": "width",       "label": "عرض",           "width": 6.44140625,  "field": "line.width" },
        { "key": "area",        "label": "مساحت",         "width": 7.109375,    "expr": "round(col.length*col.width,2)" },
        { "key": "rate",        "label": "ارزش متر مربع", "width": 14.88671875, "field": "line.usdPerM2" },
        { "key": "usd",         "label": "جمع کل دلار",   "width": 16,          "expr": "round(col.area*col.rate,2)" } ],
      "group": { "by": "bale", "mergeKey": true, "keyColumn": "bale", "skipEmpty": true, "footerStyle": "footer",
        "footer": [
          { "id": "label",  "col": "bale",        "span": 2, "text": "{terms.baleFooter}" },
          { "id": "baleNo", "col": "bgColor",     "span": 1, "expr": "bale.number" },
          { "id": "incl",   "col": "borderColor", "span": 1, "text": "شامل" },
          { "id": "count",  "col": "pattern",     "span": 1, "agg": "count" },
          { "id": "unit",   "col": "origin",      "span": 1, "text": "تخته" },
          { "id": "area",   "col": "width",       "span": 2, "agg": "sum", "of": "area" },
          { "id": "usd",    "col": "rate",        "span": 2, "agg": "sum", "of": "usd" } ] },
      "grandTotal": null,
      "checkAgainst": [ { "footerId": "count", "ledgerField": "bale.pieces" },
                        { "footerId": "area",  "ledgerField": "bale.area" },
                        { "footerId": "usd",   "ledgerField": "bale.usd" } ] },
    { "name": "خلاصه", "role": "summary", "startCol": "D",
      "title": { "fromCol": "C", "toCol": "H", "style": "summaryTitle",
                 "expr": "\"صورت \" & terms.bale & \" بندی و اظهار فرش صادراتی  تاریخ\" & jdot(shipment.documentDate)" },
      "columns": [ { "label": "{terms.bale}", "from": "footer:baleNo" }, { "label": "تعداد فرش", "from": "footer:count" },
                   { "label": "متراژ", "from": "footer:area" },        { "label": "ارزش گمرکی", "from": "footer:usd" } ],
      "headerStyle": "summaryHeader", "rowStyle": "summaryCell", "equalsRow": true,
      "totals": { "sum": [ "count", "area", "usd" ], "blankBordered": [ "baleNo" ] } } ],
  "styles": {
    "title":         { "font": "B Nazanin", "size": 11, "colorRgb": "FF767171", "fillRgb": "FFFFFF00", "border": "thin", "hAlign": "center", "vAlign": "center" },
    "header":        { "font": "B Nazanin", "size": 11, "border": "thin", "hAlign": "center", "vAlign": "center" },
    "header9":       { "font": "B Nazanin", "size": 9,  "border": "thin", "hAlign": "center", "vAlign": "center" },
    "cell":          { "font": "B Nazanin", "size": 11, "border": "thin", "hAlign": "center", "vAlign": "center", "shrinkToFit": true },
    "footer":        { "font": "B Nazanin", "size": 11, "fillRgb": "FFFFFF00", "border": "thin", "hAlign": "center", "vAlign": "center" },
    "summaryTitle":  { "font": "B Nazanin", "size": 11, "fillRgb": "FFFFFF00", "border": "none", "hAlign": "center", "vAlign": "center" },
    "summaryHeader": { "font": "B Nazanin", "size": 11, "border": "thin", "hAlign": "center", "vAlign": "center" },
    "summaryCell":   { "font": "B Nazanin", "size": 11, "border": "thin", "hAlign": "center", "vAlign": "center" },
    "plain":         { "font": "B Nazanin", "size": 11, "border": "none", "hAlign": "center", "vAlign": "center" } } }
```

### E.10 Golden tests (`DocumentComposer` + xlsx round trip)

**Committed fixtures** (the real xlsx stays out of the repo; the owner approves committing these transcriptions at the P1 and P6 reviews; they hold no names and no Toman money):
- **`tests/fixtures/sample-shipment.json`** (P1, WP1-A): all 161 lines transcribed from `dump.txt`. Per line: bale, serial text, qty, `bg_color`, `border_color`, pattern, origin, material, length and width snapped to 2 dp, and usdPerM2. Bale 31 = sample rows 192–199 (4004 included); bale 33 stays 33.
- **`tests/fixtures/sample-rows-1-43.expected.json`** (P6-engine): per cell, the decimal value; the formula text with shared formulas expanded; the style tuple (font name, size and colour, fill, outer edge borders, alignment). Also the merges, the exact column widths, and `pageSetup` / `pageMargins` / `sheetView`.

E.10 #1, E.10 #2, L.1 «All 161 sample lines» and the `PageBreakPlanner` tests read these files. P8's `SampleSheetBuilder` generates the quirky xlsx **from** `sample-shipment.json` and does not own the data.

**1. Lot-free fixture** (sample bales 1–10, from `sample-shipment.json`). Must match **`sample-rows-1-43.expected.json` cell for cell**: values, formula text, merges (A1:L1, A3:A6, A7:B7, I7:J7, K7:L7 …), fonts, fills, borders, alignment, column widths, page setup and view.

Comparison rules: formulas are compared **after expanding the sample's shared formulas** (L3:L5 `si=0`, L8:L24 `si=1`); borders of merged ranges are compared **by their outer edges only** (the sample has partial inner borders, for example A8 `s=11`, A9/A10 `s=19`, A21 `s=12`).

Intentional differences:
- E-footer is `=ROWS(…)` (equal in value to the sample's constant);
- explicit row heights;
- sheet name «اظهار»;
- a print-titles defined name;
- exact decimal cached values;
- shared formulas written as plain formulas, and uniform thin borders inside merges;
- `shrinkToFit = 1` on data cells instead of the merchant's hand-set 8–9 pt cells (E63, D64, F201–F229);
- if the E.6 #5 fallback is active: `fitToPage = 0` with an explicit integer scale.

**2. Full sample fixture** (`sample-shipment.json` → shipment)
- **Totals:** 36 bales, 161 lines, 242 pieces, 464.74 m², 20,322.50 USD.
- **Layout:** 13 columns (lots). H = 1 on single-rug lines such as 3778.
- **Bale 29:**
  - one row, no A merge;
  - H = 50, K = 0.16, M = 7.2;
  - footer E = 50, J:K = 8, L:M = 360.
- **Bale 30:**
  - rows with qty 24 and 10, areas 0.16 and 0.24, USD 7.2 and 10.8;
  - footer 34 / 6.24 / 280.80.
- **Bale 31** starts with 4004. **Bale 33's footer** C = 33.
- **Midpoint rows cache the decimal values:**
  - 3789 → 2.90
  - 3816 → 3.80 (the sample's J57)
  - 3853 → 0.76
  - 3882 → 0.39
  - 3901 → 0.75
  - 4009 → 0.40
- **Breaks:** no row break inside any bale.
- **Summary sheet:**
  - C1:H1 title text and style exact;
  - 36 link rows, using the lots anchors `='اظهار'!J{n}` (area) and `='اظهار'!L{n}` (USD);
  - the «=» row has no border and no quote prefix, and D is blank but bordered;
  - totals 242 / 464.74 / 20,322.50.
- **Rules:**
  - `checkAgainst` never fires;
  - a Finalized fixture renders from the snapshot.

**3. Empty-bale fixture.** The bale is skipped with `W_EMPTY_BALE`, and no reversed range appears anywhere.

**4. Adl fixture** (bales 1–3 with `BaleTerm = Adl`): the title reads «صورت عدل بندی…», A2 and summary D2 read «عدل», and footers read «عدل شماره».

### E.11 Designer (configurator, P7)

Screens:
- `ColumnStrip`: drag to reorder, widths, hide.
- `FieldPicker`: the catalogue, grouped by scope.
- `ExpressionEditor`: live samples and compiled Excel text from `validate-expression`.
- `FooterEditor` and `SummaryEditor` (they edit the E.2 `group.footer`, `title`, summary `columns`/`totals` and `styles` members; footer cells pick a column key, never a letter).
- `PageSetupPanel`.
- `RevisionsDrawer`: restore a revision.
- «بازگردانی پیش‌فرض»: reset to the seed.

The preview uses a sample shipment. Saving creates a revision.

---

## F. Excel import (P8)

### F.1 Flow

1. **Upload** (`POST /api/imports`, multipart).
2. **Choose** the sheet; header row detected; title parsed.
3. **Mapping:** suggested, editable, and remembered as `ImportMappingPreset`.
4. **Options:** mode and existing-serial policy.
5. **Preview** (`/preview`): counts, reconciliation, rows with actions, issues by code, suggested declared-value rates. Rows can be overridden.
6. **Commit** (`/commit`).
7. **Undo** (`/undo`).

**Simple mode** (the merchant has Business, so he can reach the wizard):
- The sheet step is skipped when the file has one sheet.
- Mapping is skipped when the `HeaderSignature` matches a saved preset, or when `HeaderDictionary` scores at least 10 of the 12 headers. Otherwise the wizard stops with «ستون‌ها را نشناختم؛ از تنظیم‌کننده کمک بگیرید».
- Mode defaults to Declaration when the file has a bale column, else StockOnly.
- The preview is one card: «۲۴۲ تخته در ۳۶ نگله · ۴۶۴٫۷۴ متر · ۲۰٬۳۲۲٫۵۰ دلار · ۷ هشدار [دیدن هشدارها] [وارد کن]».

### F.2 Reading and mapping

- **Reader.** `ClosedXmlWorkbookReader` → `SheetGrid` (cached values plus merge map).
- **Float snapping.** A numeric value within 1e−9 of a 2-dp decimal is snapped (2.2999999999999998 → 2.30), with `W_DIM_SNAPPED` (info).
- **Header row** = the first row among rows 1–30 with at least 5 `HeaderDictionary` hits. The dictionary maps:

  | Header text | Field |
  |---|---|
  | نگله, عدل | bale |
  | شماره, سریال, کد | serial |
  | رنگ متن, رنگ زمینه | `bg_color` |
  | رنگ حاشیه | `border_color` |
  | نقشه, طرح | pattern |
  | محل بافت, نوع | origin |
  | مواد مصرفی, جنس | material |
  | تعداد | qty |
  | طول | length |
  | عرض | width |
  | مساحت, متراژ | area (check only) |
  | ارزش متر مربع | usdPerM2 |
  | جمع کل دلار | usd (check only) |

- **Title.** The row above the header that matches `\d{4}[./]\d{1,2}[./]\d{1,2}` gives `TitleText` and `TitleDate` (1405.06.15 → 2026-09-06).
- **Row classes** (`RowClassifier`): Title, Header (repeat), Data, BaleFooter («نگله شماره» … «تخته»), Spacer, SummaryTitle, SummaryHeader, SummaryRow, SummaryEquals, SummaryTotal, Unknown.
- **Bales** (`BaleResolver`): from the merged column-A ranges and first-row values. A data row after a footer that sits outside any merge is adopted into the next bale.
- **Lots** (`LotParser`): a range serial plus a number in the material column gives qty.
- **Cells covered by a merge read as empty**, with two exceptions:
  1. vertical merges in the bale column propagate the bale number down;
  2. a horizontal merge whose top-left value spans known pattern / origin / material columns goes to `KnownValueSplitter`.
  
  A merge that starts at the serial column (B187:C187, B189:C189, B190:C190) assigns its value to the serial only; `bg_color` stays null. So the three lot rugs never get «3911 - 3960» as a colour.
- **Merged values** (`KnownValueSplitter`): a merged value spanning pattern and origin (E187:F187 «گبه پشم»), or origin and material, is split into known origin and material values (origin «گبه», material «پشم»; pattern stays null).

### F.3 Errors (the row is excluded until fixed in the preview)

| Code | Level | Meaning |
|---|---|---|
| `E_NOT_XLSX` | file | Not an xlsx file |
| `E_FILE_TOO_LARGE` | file | File too large |
| `E_ZIP_BOMB` | file | Archive limits exceeded (F.8) |
| `E_NO_HEADER` | file | No header row found |
| `E_TOO_MANY_ROWS` | file | More than 10,000 rows |
| `E_SERIAL_INVALID` | row | Unreadable serial |
| `E_RANGE_REVERSED` | row | «3960-3911» |
| `E_RANGE_TOO_LONG` | row | Span over 10,000 |
| `E_DIM_INVALID` | row | Outside 0.05–30 m, or more than 2 dp after snapping |
| `E_DIM_MISSING` | row | No size |
| `E_QTY_INVALID` | row | Bad quantity |
| `E_SERIAL_DUP_IN_FILE` | row | Same serial twice in the file |
| `E_SERIAL_OVERLAP_IN_FILE` | row | Overlapping ranges in the file |
| `E_SERIAL_EXISTS` | row | Serial already exists (policy Error) |
| `E_SERIAL_LOCKED` | row | Serial is in a Finalized shipment |

### F.4 Warnings and info (expected rows of the sample)

| Code | Sample rows |
|---|---|
| `W_QTY_FROM_MATERIAL` | 187 (50), 189 (24), 190 (10) |
| `W_RANGE_QTY_DIFF` | 190 («3985 - 4003» spans 19, qty 10) |
| `W_ROW_ADOPTED` | 192 (4004 → bale 31) |
| `W_FOOTER_LABEL` | 210 (says 26; the bale stays 33) |
| `W_MERGED_SPLIT` | 187, 189, 190 («گبه پشم» → «گبه / پشم») |
| `W_SERIAL_OUT_OF_SEQUENCE` | 227–229 (2026–2028) |
| `W_MISSING_FIELD` | 98 and 177 (pattern), 182 (border); 187, 189 and 190 (`bg_color` and pattern: the lot rows import with `bg_color` = null, pattern = null, origin «گبه», material «پشم») |
| `W_FOOTER_MISMATCH`, `W_SUMMARY_MISMATCH` | none on the sample |
| `W_DUPLICATE_FILE` | re-uploading the same SHA-256 |
| `W_DIM_CM` | a size that looks like cm |
| `W_NO_DECLARED_VALUE` | a rate not in the table and none in the file |
| `I_REPEATED_HEADER` | 46–47, 87–88, 130–131, 173–174, 216–217, 226 (skipped) |
| `I_SUMMARY_DETECTED` | 260–299 (used for reconciliation) |
| `I_TITLE_DATE` | 1 (1405.06.15) |

`SuggestedRates` from the file are گبه 45, خراسان تجاری 25 and نائین 9 لا 80, offered for the declared-value table. Lines whose file rate equals the table rate get no override; other lines get an override.

### F.5 Reconciliation (`Reconciler`)

For each bale, it compares three sources:
- computed pieces (Σqty), area (Σ piece area × qty) and USD (Σ piece USD × qty);
- the footer (E, I, K);
- the summary (E, F, G).

Before comparing, footer and summary numeric caches within 1e−9 of a 2-dp decimal are snapped (R7 `13.510000000000002` → 13.51, R299 `20322.500000000004` → 20,322.50), with the info code `I_VALUE_SNAPPED` (not `W_DIM_SNAPPED`). Piece counts compare as integers. After snapping, the tolerance is 0. On the sample every bale is ok: bale 30 gives 280.8 from the footer formula `SUM(K189*I191)`, and bale 29 gives 8 from `SUM(50*J187)`.

### F.6 Modes and existing serials

- **Declaration** (default):
  - creates the shipment (title and date from the title row), its bales and its lines;
  - new rugs get intake = final = the file's sizes, so final is explicitly «همان اندازه» (M.2 #21);
  - an existing serial follows `ExistingSerialPolicy`. **Attach** (default) puts the existing rug into its bale and sets its final size from the file; **Skip** and **Error** do what they say.
- **StockOnly:** creates rugs in a new intake batch «ورود از اکسل {date}» with intake sizes. The bale column is ignored.

### F.7 Commit and undo

**Commit**
- Allowed only when `Status = Previewed` and the request's `version` equals the job's xmin. A double commit returns 409.
- Runs inside `IUnitOfWork.ExecuteInTransactionAsync` (one transaction, the tenant ledger lock taken once, the pass after each flush; D.4).
- `ResultJson` records:
  - created rug, shipment, bale and line ids;
  - attached rugs' previous final sizes and xmin.
- `Status` becomes Committed.

**Undo** (allowed while the shipment is Open, every created or attached rug is unchanged since commit (xmin equal), **and no non-deleted cost entry depends on the import**: `ShipmentId` = the job's shipment, `BatchId` = the job's batch, a `BaleId` target among the created bales, or a `RugId` target or pin among the created rugs):
1. Delete the created lines.
2. Hard-delete the created rugs, through the pass.
3. Restore the attached rugs' final sizes.
4. Delete the created bales and shipment (Declaration), or the intake batch it created (StockOnly).
5. Set `Status = Undone`.

Undo runs inside `ExecuteInTransactionAsync`. It returns `{ok, blockers[]}` that name the edited rugs and the dependent entries («۲ هزینه به این ورود وصل است»). Counter values are not rolled back; checksums exclude counters.

### F.8 File safety

- **Format checks:**
  - magic `PK\x03\x04`;
  - `[Content_Types].xml` and `xl/workbook.xml` present;
  - `.xlsm` and VBA rejected.
- **Limits:**
  - ≤ 10 MB upload (`[RequestSizeLimit(10_485_760)]` plus an explicit check);
  - ≤ 200 zip entries;
  - ≤ 100 MB uncompressed;
  - a per-entry compression ratio ≤ 100.
- **Storage:** `{Storage:DataRoot}/imports/{tenantId}/{jobId}.xlsx`. The client file name never reaches a path.
- **Rate limit:** `heavy`.

---

## G. Grid and data entry

### G.1 Grid component (`CA/components/grid/*`)

- **Files:**
  - `DataGrid.vue`, `GridCell.vue`
  - `useGridKeyboard.ts`, `useGridSelection.ts`, `useGridDrafts.ts`, `useGridSave.ts`
  - `gridParse.ts`, `useVirtualRows.ts`
  - `GridCardList.vue` (card mode under 640 px)
- **Behaviour:**
  - frozen header and first column;
  - virtualised rows;
  - `data-hotkeys-off`, so the `n` and `/` hotkeys stay off;
  - `role="grid"` semantics (H.7);
  - no `@vueuse/core`;
  - **touch keyboards:** `GridCell` sets `inputmode="decimal"` for dim, money and percent cells, `numeric` for int, `text` for serial, and `enterkeyhint="next"` on every cell;
  - a visible «برگرد» toolbar button (icon `undo`) mirrors Ctrl+Z; key hints such as «[Enter: بله]» appear only under `pointer: fine`.
- **Data** comes from `GET /api/grid/{gridId}`, served by **`IGridSource`** keyed by `gridId`:
  - P3 provides `intake` and `rugs`;
  - **P4b** adds `BalesGridSource` (`bales`), because it needs both P3's `IGridSource` and P4a's shipment domain;
  - an unknown id returns 404.

### G.2 Default layouts (`GridLayoutService` defaults; overridden per tenant in `TenantGridLayout`)

**`intake`**

| Key | Label | Type | Sticky | Notes |
|---|---|---|---|---|
| `rug.serial` | شماره | serial | — | Next number per the G.4 rule and `SerialMode` |
| `batch.seller` | فروشنده | combo | yes | The row's ورودی is resolved on save from (the header date, this seller) (H.1) |
| `rug.qty` | تعداد | int | — | Filled from the range; default 1 |
| `rug.custom.bg_color` | رنگ متن | combo | yes | Suggestions from `/api/rugs/values` |
| `rug.custom.border_color` | رنگ حاشیه | combo | yes | |
| `rug.pattern` | نقشه | combo | — | |
| `rug.origin` | محل بافت | combo | yes | Drives the declared value |
| `rug.material` | مواد مصرفی | combo | yes | |
| `rug.intakeLength` / `rug.intakeWidth` | طول / عرض | dim | — | |
| `rug.areaM2` | متراژ | readonly | — | A lot shows «۰٫۱۶ × ۵۰ = ۸٫۰۰» |
| `purchase.price` | قیمت خرید | money+unit | yes (value and unit) | Business. A unit chip «هر متر \| هر تخته \| کل» (default «هر متر»; the same words as the cost panel, H.2), changed by tapping it or Alt+↓ |
| `purchase.broker` | دلالی ٪ | percent | yes | Business |
| `rug.purchaseTotal` | جمع خرید | readonly money | — | Business. Shows the arithmetic: «۳۰۰٬۰۰۰ × ۵۰ = ۱۵٬۰۰۰٬۰۰۰» |
| `rug.notes` | یادداشت | text | — | |

"Sticky" means the value carries down to each new row.

**Purchase columns** (`RugFieldWriter`; FieldCatalog `purchase` scope, E.3)
- **Which columns.** Every active Purchase-group category with `ShowInIntake` becomes a grid field (`purchase.price`, `purchase.broker`, or `purchase.cat:{Id}`), placed by `TenantGridLayout`; the `GridLayoutService` defaults include every such category.
- **Read.** The cell shows the rug's **single** purchase line of that category: its `Rate` and, for money categories, its basis as the unit chip (PerM2 «هر متر», PerPiece «هر تخته», Total «کل»). A legacy rug's M2 Total line therefore shows as «کل ۴۰٬۰۰۰٬۰۰۰», never as a per-metre rate.
- **Write.** Typing upserts that line: `Label` = category name, `SortOrder` = category order, `Basis` from the unit chip (Percent for a percent category), `Rate` = the typed value. **Clearing the cell deletes the line.** Changing only the unit chip changes the basis and recomputes.
- **Read-only case.** When the rug has two or more lines of that category, the cell is read-only and shows the summed amount with the chip «چند سطر», linking to `PurchaseLinesEditor`.
- **Concurrency.** A grid op that carries purchase keys also bumps the rug's `UpdatedAt`, so the rug version (xmin) moves and a concurrent purchase edit gives 409 (I.2).

**`bales`** (one row per line, grouped by bale with a footer row like the Excel)

- نگله (int; a new row defaults to «نگلهٔ جاری», C.2)
- شماره (adds by serial)
- تعداد (readonly)
- رنگ متن · رنگ حاشیه · نقشه · محل بافت · مواد مصرفی (editable while Open). **In Simple mode these are not Tab stops**; they are edited by tap, double-click or F2. Read-only cells are never Tab stops, so Tab goes straight from شماره to the final طول (`GridColumnDto.TabStop`).
- اندازهٔ اول (readonly «۲٫۰۸ × ۲٫۱۰»; in Simple mode shown only when it differs)
- طول · عرض (final; after an add by serial the focus lands on طول, C.4)
- مساحت (readonly)
- ارزش متر مربع (the `rate` op is Business; others see it readonly with a source chip; «ندارد» opens the add-rate form, C.5)
- جمع کل دلار (readonly)

Column labels that name the bale resolve from `terms`. A second column preset **«اندازه‌گیری»** (شماره · اندازهٔ اول · طول · عرض · مساحت) is the default under 1100 px wide.

### G.3 Keyboard and paste

- **Movement (RTL):** ArrowLeft = next column; Tab = next cell; Shift+Tab = previous cell.
- **Enter** commits and moves down (or right, per user preference).
- **Tab-run return:** after a run of Tabs, Enter goes to the next row at the column where the run started (Excel behaviour).
- **Editing keys:**
  - F2 or typing starts editing; Esc cancels; Delete clears;
  - Ctrl+D fills down;
  - Ctrl+Z undoes locally (50 steps).
- **Paste:** Ctrl+V pastes TSV. Quotes and CRLF are handled, Persian digits are accepted, and header lines are detected and skipped. The paste is mapped to columns starting at the active cell. **All cm-looking cells of one paste are collected into one confirm**: «۲۰ اندازه به سانت نوشته شده؛ همه به متر تبدیل شود؟ [تبدیل کن] [نه]».
- **Combo cells:** type-ahead; Enter accepts; new values are allowed.
- **Deleting a row.** Each row has a «⋮» menu (44 px). In the intake grid it holds «حذف این فرش»: confirm «فرش ۳۷۷۸ حذف شود؟ [حذف کن] [نه]», then the toast «حذف شد [برگردان]» for 8 s, which calls restore. Policy (server-side, G.6): Floor may delete a rug it created within the last 24 h that has no line, allocation, sale or step cost; in every other case only Business may, and the server returns 403 «این فرش را فقط بازرگان می‌تواند حذف کند.». The menu item is hidden when the rule already says no. In the bales grid the menu holds «خارج کردن از نگله» and «انتقال» (C.3, H.1).

### G.4 Numbers, parsing and rounding

**`parseDecimal(text, kind)`** (`CA/lib/money.ts`; server mirror `App/Common/NumberParser`)
- Persian and Arabic digits become Latin; spaces, ZWNJ and RLM are stripped.
- «٫», and «/» between digits, become «.».
- `kind = dim`: «,» is the decimal mark («2,08»).
- `kind = money`: «,» and «٬» are grouping.
  - **Suffixes:** «م» or «میلیون» × 1,000,000; «ه» or «هزار» × 1,000. So «۱۰م» → ۱۰٬۰۰۰٬۰۰۰ and «۸۵ه» → ۸۵٬۰۰۰.
  - **At MoneyScale 0**, a single «.» or «٫» followed by exactly three digits («10.000») asks inline «منظورتان ۱۰٬۰۰۰ است؟ [بله] [نه]». Any other decimal is rejected with «مبلغ تومان اعشار ندارد».
- More than one decimal mark is rejected.

**Dimensions**
- Hard limits: 0.05 ≤ v ≤ 30 with at most 2 dp.
- A value up to `MaxPlausibleSideM` (default 12) is metres, silently.
- **An integer from 13 to 30** asks «۳۰ متر؟ یا ۳۰ سانت (۰٫۳۰ متر)؟ [۳۰ سانت] [۳۰ متر]», with the cm answer first (so 25×25 cm pieces cannot silently become 625 m²).
- An integer from 31 to 3000 triggers the cm confirm «۲۰۸ ← ۲٫۰۸ متر؟».
- A piece area over 60 m² gives the non-blocking warning «مساحت این فرش ۶۲٫۴ متر شد؛ درست است؟».
- «208×210», «208*210», «208x210» and «2.08×2.10» fill both fields (applying the cm rule).
- Vectors: «25» → ask; «30» → ask; «12» → 12 m; «40» → cm confirm to 0.40; «208×210» → 2.08 × 2.10.

**Serials**
- Accepted forms: «3778», «۳۷۷۸», «3911 - 3960», «3911–3960», «3911_3960», «3911 تا 3960». The display form is «3911 - 3960».
- «3960-3911» is an error. A span over 10,000 is an error.
- Range vs qty: the range may be longer than the qty, never shorter.
- «A-12» is allowed as text.

**Next serial** (`SerialSuggester`, used by `GET /api/rugs/next-serial`, `GridDataDto.NextSerial` and the grid)
- **Server rule:** `next = max(SerialStart ?? 1, MAX(COALESCE(SerialTo, SerialFrom)) + 1)` over the tenant's rugs with `DeletedAt IS NULL`, **Exported and Sold included** (serials stay unique among them, M.2 #4). Non-numeric serials are ignored. Example: after «3985 - 4003» the next is **4004** (SerialTo + 1), not SerialFrom + qty (3995).
- **Typos do not lower it:** the out-of-sequence 2026–2028 in the sample are below the maximum, so `next` stays above 4004.
- **In the grid** (local, across unsaved rows): after a saved or draft row, `next = (row.SerialTo ?? row.SerialFrom) + 1`. A row that is given only a qty > 1 in SuggestNext or Auto mode gets the range From..From + qty − 1.
- **Modes** (`SerialMode`):
  - **Manual:** no prefill.
  - **SuggestNext** (default): a grey placeholder; Enter or Tab accepts it; editable.
  - **Auto:** the server assigns the serial at save, inside `SerialOverlapStage` under the ledger lock, and ignores client text.
- **Collisions.** When two users take the same suggestion, the second save gets 409 `SerialOverlapException` carrying `nextFree`; the grid offers «شمارهٔ ۴۰۰۵ آزاد است؛ همان را بگذارم؟ [بله] [خودم می‌نویسم]».

**Rounding** (`Domain/Common/Num` and `AreaMath`)
- `Num.Round(x, dp)` is always `MidpointRounding.AwayFromZero`.
- `AreaMath` applies the D.3 lot rule:
  - `PieceArea = Round(L×W, 2)`
  - `LineArea = PieceArea × qty`
  - `PieceUsd = Round(PieceArea × rate, 2)`
  - `LineUsd = PieceUsd × qty`
  - `PieceMoney = Round(rate × PieceArea, MoneyScale)`
  - `LineMoney = PieceMoney × qty`
  - every other money row (PerPiece, Percent, foreign currency, step pricing) follows the D.3 tables.
- Square feet use the exact factor 10.7639104.

**`CA/lib/num.ts`**
- Parses decimal strings into scaled `BigInt`, multiplies exactly, and rounds half away from zero.
- **Never uses float multiplication**, because `2.30*1.65 = 3.7949999999999995` in JS.

**SQL.** Every area and money expression stays `numeric` (generated columns, `round(numeric, n)`). Npgsql sends decimals as `numeric`; there are no `float8` parameters.

**Shared vectors** (`tests/fixtures/math-vectors.json`, run by xUnit and vitest). Every entry carries an explicit `kind` (`area` | `usd` | `money`) and `scale`.
- The sample's midpoints.
- USD, scale 2: rate `45.55` USD/m² on a 0.16 m² piece × 50 → PieceUsd 7.29, LineUsd **364.50** (not 364.40).
- Money, scale 0: a PerM2 purchase line of 45.55 on the same lot → piece `ROUND(7.288, 0) = 7` → line **350**; at scale 2 → 7.29 × 50 = 364.50.
- Money, scale 0: `9,000,000 × 0.16` → 1,440,000 per piece × 50.
- Money, scale 0: USD piece 7.29 × fx 105,000.5 × qty 50 → **38,272,700** (per piece, D.3), not 38,272,682.
- Money, scale 0: a Percent 2 line on a lot (B of D.7: 15,000,000 → 300,000), and a PerPiece line with a fractional rate (12,345.5 × 3 → `ROUND(12,345.5) = 12,346` × 3 = 37,038).
- Step pricing: one vector per pricing model with qty 50 (D.3).
- The snapped import value `2.2999999999999998 × 1.65` → 3.80.

### G.5 Pack mode (`/Shipments/{id}/Pack`, phone)

- Reached from the bales tab by the link «حالت اسکن (گوشی)», which also shows a QR of the pack URL so it opens on a phone.
- Big serial field (`inputmode="numeric"`) and a scan button (`BarcodeScan.vue`, H.6).
- «نگلهٔ جاری» header with live totals and the target bar (C.2).
- The last 5 rugs added, each with undo.
- «نگلهٔ بعد» (sets the current bale to max + 1; the bale is created by its first rug, C.2).
- Optional «اندازهٔ نهایی را هم بپرس» after each add.
- Totals are announced through `aria-live`.
- When offline, operations stay as drafts with explicit retry (G.7). There is no background replay.

### G.6 Batch save protocol (`POST /api/grid/rugs/batch`; bales via `POST /api/shipments/{id}/lines/batch`)

1. **Request:** `GridBatchRequest`; rows have `ClientRowId`, `Version`, `Values` and `BaseValues`.
2. **Validate:**
   - The server validates every op through `RugFieldWriter`.
   - Three-way merge: a field changed by someone else but not by this client is auto-merged. The same field changed on both sides gives `Conflict`, with `Current` returned.
3. **Save:**
   - one transaction and one pass;
   - on a database error, fall back to one transaction per row (`session.Reset()` between rows) so the faulty row is isolated.
4. **Idempotency:**
   - creates use `Rug.ClientRowId` (a retry returns the existing rug);
   - line `add` ops are idempotent by membership;
   - an `add` op naming a `baleNumber` that does not exist creates that bale in the same pass (C.2).
5. **Money keys** sent by a non-Business user make the **whole request 403**. An Operator's grid never shows money columns, so this only catches a tampered request.
6. **Deletes** (`GridOp.Delete`): allowed to Floor only for a rug that user created within the last 24 h and that has no line, allocation, sale or step cost; otherwise only Business. A refused delete returns 403 «این فرش را فقط بازرگان می‌تواند حذف کند.» for that row (`GridRowStatus.Invalid`), not for the whole request. The delete is a soft delete, so «برگردان» restores it.

### G.7 Drafts and retries

- **Drafts:**
  - the localStorage key is `rugs:draft:{tenantId}:{userId}:{gridId}:{contextId}`;
  - every dirty row is written on change (debounced 300 ms);
  - every storage call is wrapped in try/catch.
- **Restore:** on load, «۵ ردیفِ نوشته‌شده از دفعهٔ قبل ذخیره نشده بود. [برگرداندن ردیف‌ها] [پاک کردن]», with «برگرداندن ردیف‌ها» as the primary, focused button.
- **Discarding is never one tap.** «پاک کردن» opens a danger confirm «این ۵ ردیف پاک شود؟ دیگر برنمی‌گردد. [پاک کن] [نه، نگه دار]» with «نه» focused. Before deleting, the payload moves to `rugs:draft-discarded:{tenantId}:{userId}:{gridId}:{contextId}` for 24 h, so the toast «پاک شد [برگردان]» can bring it back.
- **Save timing:** after 1.5 s idle, or when the row is left.
- **On network failure:**
  - back off 2, 4, 8, 16 and 30 s;
  - show the banner (H.3);
  - offer «الان دوباره بفرست».
- **Reload protection:** `ui.blockReload` while anything is dirty (this suppresses UiShell's reload after an offline flush), plus a `beforeunload` prompt.
- **The cost panel** uses the same rules (H.2): its state is drafted to `rugs:draft:{tenantId}:{userId}:cost-entry`, a failed save keeps the panel open, and a retry reuses the same `ClientKey`.

---

## H. Merchant UX

### H.1 Screens

**Home** (A.4).

**Intake (`/Intake`)**
- Header:
  - the date, a `JalaliDateField` labelled «تاریخ رسیدن» (today by default);
  - a title that follows the date: «ورودی امروز» / «ورودی دیروز» / «ورودی ۱۴۰۵/۰۶/۱۰»;
  - a «ورودی‌های اخیر» strip: chips for the last 7 days («۱۴۰۵/۰۶/۱۴ · حاج رضا · ۸ تخته») that reopen that day to fix a typo.
- **The ورودی key is (tenant, Iran day, normalised seller)**; an empty seller is its own key (B.5). «فروشنده» is a sticky grid column right after شماره, so each row's batch is resolved on save (find-or-create through `POST /api/batches/today` semantics). Changing the seller of a row moves only that row; no typed row ever vanishes from the grid.
- The `intake` grid shows every row of the chosen day.
- Footer, grouped by seller when there are several: «حاج رضا: ۱۲ تخته · ۳۴٫۵ متر · خرید ۴۵۰٬۰۰۰٬۰۰۰ تومان» (money for Business only). Counts are Σqty in تخته; with a lot: «۵۲ تخته (۳ ردیف) · ۲۰٫۸۱ متر».
- Buttons «چاپ برچسب‌ها», «ورود از اکسل» (Business) and «هزینه یا خرید یکجای این ورودی» (Business; `openPanel('cost-entry', {scope: IntakeBatch, batchId})`, H.2).

**Stock (`/Rugs`)**
- Location chips: «در انبار», «در نگله», «صادر شده», «فروخته‌شده».
- Serial is the first column. In Simple mode (from P4b) the workflow status column and filter are hidden.
- Search by serial.
- Bulk bar (each action is a `bulk-actions/*.action.ts` file registered through the P1 slot, K.1):
  - «افزودن به نگله» (P4b; opens a picker of the target shipment's bales, «نگلهٔ جاری» first, plus «نگلهٔ تازه»);
  - «ثبت هزینه برای این‌ها» (P5, Business; opens the cost panel preset to these rugs).

**Shipment workspace (`/Shipments/{id}`)**
- Header: number, title, date, status chip and readiness chips, plus «بیشتر» for the optional fields (C.1).
- **«قطعی کن»** (Business) is the header's primary button. It is disabled while blocking issues exist, with the hint «اول این‌ها را درست کنید: …» listing the chips. After finalize it becomes «باز کردن دوباره».
- Tabs (`IShipmentTab`): نگله‌ها · هزینه‌ها (Business) · حساب محموله (Business) · اکسل و چاپ.

**Bales tab**
- Header: «نگلهٔ جاری: ۳ ▾  [نگلهٔ بعد]» (C.2) and the link «حالت اسکن (گوشی)» to pack mode (G.5).
- «شماره را بزنید یا اسکن کنید», which adds to the current bale; focus then moves to that row's final «طول» (C.4).
- Two views:
  - a table: the `bales` grid with Excel-like footers «نگلهٔ ۱ · شامل ۴ تخته · ۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار» (on-screen tokens `bg-secondary-container` / `text-on-secondary-container`, never a literal yellow) and the target bar;
  - cards: each rug card has a «انتقال» button (icon plus text) that opens a picker of 44 px bale-number buttons, «نگلهٔ تازه» and «برگشت به انبار»; with a card focused, the key M opens the same picker. **Dragging between bales is an optional extra**, never the only way. Each card also has an «اندازه» chip (H.6).
- «همان اندازه»: key `=`, the row button «همان», and the bales bulk bar for many rows (C.4).
- The row menu: «خارج کردن از نگله» (undo toast, C.3) and «انتقال».
- Empty state: «این محموله هنوز نگله ندارد. شمارهٔ اولین فرش نگلهٔ ۱ را بزنید.», with focus in the serial box.
- Lot split dialog (preview, D.9).

**Costs tab and `/Costs`:** list with a «تقسیم‌نشده» filter; the cost panel (H.2). Deleting a cost shows the toast «هزینهٔ شمارهٔ ۱۲ حذف شد [برگردان]», which calls `/api/costs/{id}/restore`.

**«حساب محموله» tab** (Business): «فروش این محموله: [__] دلار × نرخ [__] = … تومان» (`PUT /revenue`), «قیمت تمام‌شدهٔ هر متر», «سود» (`ShipmentProfitDto`).

**«اکسل و چاپ» tab (Output)**
- **Simple mode:** two 56 px buttons, **«چاپ»** (primary; the print page always ends with the summary, E.8) and «دانلود اکسل», plus the note «در اکسل برای چاپ خلاصه، «چاپ کل کتاب کار» را بزنید». The template chooser is hidden when only one non-money template exists, and the export history is hidden.
- **Full mode:** template choice (default selected), the two buttons, export history.
- Draft banner in both.

**Rug details**, in order:
1. مشخصات
2. اندازه‌ها
3. خرید (Business)
4. هزینه‌ها (Business)
5. محموله / نگله
6. کارهای کارگاه (collapsed in Simple)
7. عکس‌ها
8. فروش (Business)

### H.2 Cost panel, Simple mode (`panels/CostEntry.panel.vue`)

**Three inputs and one read-only sentence**

1. **«نوع هزینه»:** tiles of the active **Expense** categories (icon and name, the icon from `CostCategory.Icon`), plus «+ نوع تازه» (`/api/cost-categories/quick`). The category gives the *default* basis.
2. **«مبلغ»**, with a **3-segment switch «هر متر | هر تخته | کل»** directly above it (a radiogroup of 44 px segments, visible in Simple mode). It defaults from `CostCategory.DefaultBasis` and is sent as `Basis`. The field label («مبلغ هر متر», «مبلغ هر تخته», «مبلغ کل») and the sentence follow the selected segment. So the same category can be entered per metre one day and as one lump receipt the next.
3. **«برای»:** pre-filled from where the panel was opened (from Home: A.4). Chips, all counting تخته:
   - «این محموله (۲۴۲ تخته)»
   - «این نگله»
   - «این فرش‌ها (۵۲ تخته)»
   - «ورودی امروز»
   - «فرش‌های دیگر…» (serial text or picker)
4. **The sentence:**
   - Total: «۱۰٬۰۰۰٬۰۰۰ تومان کرایه بین ۲۴۲ تخته به نسبت متراژ تقسیم می‌شود [تغییر]»
   - PerM2: «۸۵٬۰۰۰ تومان برای هر متر × ۲۰٫۸۱ متر = ۱٬۷۶۸٬۸۵۰ تومان»
   - PerPiece: «۵۰٬۰۰۰ تومان برای هر تخته × ۵۲ تخته = ۲٬۶۰۰٬۰۰۰ تومان»

**[تغییر]** opens the method choice: «به نسبت متراژ», «به تعداد تخته», «به نسبت ارزش گمرکی», «به نسبت قیمت خرید», «خودم سهم هر فرش را می‌نویسم» (Manual). «مساوی برای هر ردیف» (Equal) is shown only when the targets contain a lot, the only case where it differs from «به تعداد تخته».

**«بیشتر»** holds: date (today), «شمارهٔ رسید», «کارگاه / طرف حساب», «به دلار؟ نرخ ارز», «مبلغ ثابت برای یک فرش» (pins), and «توضیح». A receipt number or a provider freezes a per-metre or per-piece amount (D.3).

**Never in Simple mode:** `AreaBasis`. The category default applies (washing and repair use intake, the rest effective).

**Saving:** «ثبت هزینه» sends `SaveCostEntryRequest` with the category defaults filled in. The toast reads «ثبت شد · ۱۰٬۰۰۰٬۰۰۰ تومان بین ۵۲ تخته تقسیم شد».

**When saving fails** (offline or 5xx): the panel stays open with its inputs and shows «ذخیره نشد؛ اینترنت را نگاه کنید. [دوباره بفرست]»; the retry reuses the same `ClientKey`, so it cannot double-post. The panel state is drafted (G.7) and offered again on the next open; `ui.blockReload` is set while the save is pending.

**Accessibility.** The category tiles, the basis segments and the «برای» chips are `role="radiogroup"` with arrow keys in RTL; the selected item shows the check icon plus text, never colour alone; every target is at least 44 px.

**Purchase in one place:**
- Purchase-group tiles do not appear in Simple mode.
- Exception: when the panel is opened from an intake batch (the Intake footer button «هزینه یا خرید یکجای این ورودی», H.1), «خرید یکجای این ورودی» (`purchase.lump`, Scope IntakeBatch, membership snapshotted at save, D.2) is offered.
- `W_PURCHASE_DOUBLE` warns when the same rug ends up with both a purchase line and a lump share.

### H.3 Copy (Persian; every bale word comes from `terms`, see I.6)

| Key | Copy |
|---|---|
| Saving | در حال ذخیره… |
| Saved | ذخیره شد |
| Offline / retrying | ذخیره نشد؛ دوباره امتحان می‌کنیم · اینترنت قطع است. نوشته‌هایتان روی همین دستگاه مانده و از بین نمی‌رود. [الان دوباره بفرست] |
| Drafts found | ۵ ردیفِ نوشته‌شده از دفعهٔ قبل ذخیره نشده بود. [برگرداندن ردیف‌ها] [پاک کردن] |
| Drafts discard confirm | این ۵ ردیف پاک شود؟ دیگر برنمی‌گردد. [پاک کن] [نه، نگه دار] → toast «پاک شد [برگردان]» |
| Empty: shipments (Business) | هنوز محموله‌ای نساخته‌اید. وقتی فرش‌ها آمادهٔ بسته‌بندی شدند یک محموله بسازید. [محمولهٔ تازه] |
| Empty: shipments (Operator) | هنوز محمولهٔ بازی نیست؛ از بازرگان بخواهید یکی بسازد. |
| Empty: bales | این محموله هنوز نگله ندارد. شمارهٔ اولین فرش نگلهٔ ۱ را بزنید. |
| Empty: stock | انبار خالی است. [ثبت فرش تازه] (Business also: [ورود از اکسل]) |
| Empty: costs | هزینه‌ای ثبت نشده. رسیدها را اینجا بزنید تا قیمت تمام‌شدهٔ هر متر معلوم شود. [ثبت هزینه] (`openPanel('cost-entry')`) |
| Delete rug | فرش ۳۷۷۸ حذف شود؟ [حذف کن] [نه] → toast «حذف شد [برگردان]» · refused: این فرش را فقط بازرگان می‌تواند حذف کند. |
| Remove from bale | ۳۷۷۸ به انبار برگشت [برگردان] |
| Cost deleted | هزینهٔ شمارهٔ ۱۲ حذف شد [برگردان] |
| Error: duplicate serial | شمارهٔ ۳۷۷۸ تکراری است؛ این فرش در نگلهٔ ۲ است. (In stock: …؛ این فرش در انبار است.) |
| Error: overlap | شمارهٔ ۳۹۲۰ داخل دستهٔ ۳۹۱۱ تا ۳۹۶۰ است. |
| Error: dimension | اندازه را به متر بنویسید، مثل ۲٫۰۸ (از ۵ سانت تا ۳۰ متر) |
| Confirm: cm | ۲۰۸ ← ۲٫۰۸ متر؟ As 44 px buttons [بله، ۲٫۰۸ متر] [نه]; the hints «Enter / Esc» show only under `pointer: fine` |
| Ask: metres or cm (13–30) | ۳۰ متر؟ یا ۳۰ سانت (۰٫۳۰ متر)؟ [۳۰ سانت] [۳۰ متر] |
| Paste: cm | ۲۰ اندازه به سانت نوشته شده؛ همه به متر تبدیل شود؟ [تبدیل کن] [نه] |
| Large area | مساحت این فرش ۶۲٫۴ متر شد؛ درست است؟ |
| Money «10.000» | منظورتان ۱۰٬۰۰۰ است؟ [بله] [نه] · rejected: مبلغ تومان اعشار ندارد |
| Money words line | ده میلیون تومان (live, under every money field, H.4) |
| Range/qty | شماره‌ها از ۳۹۸۵ تا ۴۰۰۳ است (۱۹ شماره) ولی تعداد را ۱۰ نوشته‌اید؛ درست است؟ [بله] [درستش می‌کنم] |
| Lock (Business) | این فرش در محمولهٔ قطعی‌شدهٔ ۸ است. برای تغییر، اول «باز کردن دوباره» را بزنید. |
| Lock (Operator) | این محموله قطعی شده؛ برای تغییر به بازرگان بگویید. |
| Finalize confirm | محمولهٔ ۸ قطعی شود؟ ۲۴۲ تخته «صادر شده» می‌شوند و اندازه‌ها، نگله‌ها و ارزش گمرکی قفل می‌شوند. هزینهٔ تازه هنوز قابل ثبت است. [قطعی کن] [فعلاً نه] |
| Reopen confirm | محمولهٔ ۸ دوباره باز شود؟ تا دوباره قطعی نکنید، خروجی‌ها «پیش‌نویس» چاپ می‌شوند. [باز کن] [فعلاً نه] |
| Trash baled | اول این فرش را از نگلهٔ ۱۲ خارج کنید. |
| Sale baled | این فرش در نگلهٔ ۱۲ است؛ اول آن را از نگله خارج کنید. |
| Conflict | این ردیف را همین الان کس دیگری هم عوض کرد. [نوشتهٔ من بماند] [نوشتهٔ او را ببینم] |
| Unallocated | ۱ هزینه هنوز بین فرش‌ها تقسیم نشده (محموله فرشی ندارد). |
| Target deleted | این هزینه به نگله‌ای وصل بود که حذف شد؛ دوباره مشخص کنید برای کدام فرش‌هاست. [انتخاب فرش‌ها] |
| Purchase double | برای ۳ فرش هم «قیمت خرید» ثبت شده و هم «خرید یکجا»؛ مطمئنید دوبار حساب نشده؟ |
| Late cost | این محموله قطعی شده؛ هزینهٔ تازه فقط سود را تغییر می‌دهد، نه اظهار را. |
| Money locked | واحد پول و تعداد رقم اعشار مبلغ‌ها بعد از ثبت اولین مبلغ عوض نمی‌شود. |
| Empty bale | نگلهٔ ۵ خالی است؛ یا فرش در آن بگذارید یا حذفش کنید. |
| Over target | نگلهٔ ۳ شد ۱۵٫۱ متر؛ از ۱۳ متر هدف بیشتر است. |
| Split | این دسته ۵۰ تخته است. چند تخته جدا شود؟ → preview «۵۰ تخته ← ۳۰ + ۲۰ · شماره‌ها ۳۹۱۱ تا ۳۹۴۰ و ۳۹۴۱ تا ۳۹۶۰ [جدا کن] [نه]» |
| Serial taken (Auto/Suggest race) | شمارهٔ ۴۰۰۵ آزاد است؛ همان را بگذارم؟ [بله] [خودم می‌نویسم] |
| Add declared value | ارزش گمرکی «کاشان» برای هر متر: [___] دلار [ثبت] |
| Excel summary hint | در اکسل برای چاپ خلاصه، «چاپ کل کتاب کار» را بزنید |
| Draft banner | این اظهار هنوز قطعی نشده؛ روی خروجی «پیش‌نویس» چاپ می‌شود. |
| Import done | ۲۴۲ تخته در ۳۶ نگله وارد شد. [دیدن محموله] [بازگرداندن این ورود] |
| Cost changed after sale | هزینه پس از فروش تغییر کرده: +۲۰۰٬۰۰۰ تومان |

**Template tokens.** Every «نگلهٔ N» in the copy is written as `{baleOf} N`, and «نگله شماره» as `{baleFooter}`. With `BaleTerm = Adl` this gives «عدل ۱۲» (no written kasre, as UI text never writes it) and «عدل شماره».

**Copy follows the viewer's `policies`** (app-context): a button the viewer may not use is never offered (no «محمولهٔ تازه» or «ورود از اکسل» for an Operator; the Operator lock copy names the Merchant instead of «باز کردن دوباره»).

### H.3b Messages for every code (`App/Common/Messages.fa.cs`, mirrored in `CA/lib/messages.ts`)

One source maps each code to a Persian template with `{n}`, `{key}`, `{label}`, `{serial}`, `{category}`, `{pins}`, `{total}`, `{amount}` and `{baleOf}` tokens. A unit test asserts that every code constant (C.6, D.8, F.3, F.4) has a message and that no message contains Latin letters outside its placeholders.

**Readiness (C.6)**

| Code | Message and actions |
|---|---|
| `E_EMPTY_SHIPMENT` | هنوز فرشی در این محموله نیست. [افزودن فرش] |
| `E_EMPTY_BALE` | {baleOf} ۵ خالی است؛ یا فرش در آن بگذارید یا حذفش کنید. [حذف کن] |
| `E_NO_FINAL_SIZE` | اندازهٔ نهایی {n} فرش ثبت نشده. [نشانم بده] [بقیه همان اندازهٔ اول] |
| `E_NO_DECLARED_VALUE` | ارزش گمرکی {n} فرش «{key}» معلوم نیست. [نرخ بگذارید] |
| `W_BALE_OVER_TARGET` | {baleOf} ۳ شد ۱۵٫۱ متر؛ از ۱۳ متر هدف بیشتر است. |
| `W_NO_SERIAL` | {n} فرش شماره ندارد. [نشانم بده] |
| `W_NO_USD_RATE` | نرخ دلار این محموله نوشته نشده. [نوشتن نرخ] |
| `W_TEMPLATE_FIELD_EMPTY` | ستون «{label}» در {n} ردیف خالی است. |

**Allocation (D.8)**

| Code | Message |
|---|---|
| `W_STEP_DOUBLE` | برای {n} فرش «{category}» در کارهای کارگاه هم حساب شده؛ دوبار حساب نشود؟ |
| `W_PURCHASE_DOUBLE` | برای {n} فرش هم «قیمت خرید» ثبت شده و هم «خرید یکجا»؛ مطمئنید دوبار حساب نشده؟ |
| `W_PIN_DROPPED` | مبلغ ثابتِ فرش {serial} حساب نشد، چون دیگر جزو این فرش‌ها نیست. |
| `W_ZERO_WEIGHTS` | چیزی برای نسبت گرفتن نبود؛ مساوی تقسیم شد. |
| `W_NO_DECLARED_VALUE` | {n} فرش ارزش گمرکی ندارند و سهمی نگرفتند. |
| `W_EMPTY_TARGET` | فرشی برای تقسیم نیست؛ هزینه «تقسیم‌نشده» ماند. |
| `W_TARGET_DELETED` | جایی که این هزینه به آن وصل بود ({bale}، محموله یا ورودی) حذف شد؛ دوباره مشخص کنید برای کدام فرش‌هاست. [انتخاب فرش‌ها] |
| `W_LATE_COST` | این محموله قطعی شده؛ هزینهٔ تازه فقط سود را تغییر می‌دهد، نه اظهار را. |
| `W_RATE_DRIFT` | مبلغ این رسید با اندازه‌های فعلی {amount} تومان فرق دارد. [به‌روزرسانی مبلغ] |
| `W_PINS_RESIDUAL` | {amount} تومان از این هزینه به هیچ فرشی نرسید، چون همهٔ فرش‌ها مبلغ ثابت دارند. |
| `W_NEGATIVE_WEIGHT` | قیمت خرید {n} فرش منفی بود و سهمی نگرفتند. |
| `E_PINS_EXCEED` | جمع مبلغ‌های ثابت ({pins}) از کل هزینه ({total}) بیشتر است. |
| `E_PINS_BASIS` | مبلغ ثابت فقط برای هزینهٔ «کل» است. |
| `E_CIRCULAR` | خرید را نمی‌شود به نسبت قیمت خرید تقسیم کرد. |
| `E_FX_REQUIRED` | برای مبلغ دلاری نرخ دلار را بنویسید. |
| `E_SCOPE_EMPTY_SELECTION` | هیچ فرشی انتخاب نشده؛ شماره‌ها را بنویسید یا از لیست بزنید. |

**Chips**
- Partial: «{amount} تومان هنوز تقسیم نشده.»
- RateSource: «دستی» / «از جدول» / «نرخ پیش‌فرض» / «ندارد».

**Import options (F.6)**
- ImportMode Declaration: «این فایل اظهار است؛ محموله و نگله‌ها را بساز»
- ImportMode StockOnly: «فقط فرش‌ها را به انبار اضافه کن»
- Attach: «اگر شماره از قبل هست، همان فرش را در نگلهٔ فایل بگذار و اندازهٔ نهایی را از فایل بگیر»
- Skip: «اگر شماره از قبل هست، آن ردیف را رد کن»
- Error: «اگر شماره از قبل هست، جلوی ورود را بگیر»

**Import codes (F.3, F.4).** Every code gets a message in the same file, in the same voice; for example `E_RANGE_REVERSED` «شماره‌ها برعکس نوشته شده (۳۹۶۰ تا ۳۹۱۱)؛ اول عدد کوچک‌تر را بنویسید.», `W_QTY_FROM_MATERIAL` «تعداد ({n}) از ستون مواد مصرفی خوانده شد.», `W_FOOTER_LABEL` «جمع زیر این {baleOf} شمارهٔ دیگری نوشته بود؛ همان {baleOf} ۳۳ ماند.», `I_REPEATED_HEADER` «سرستون تکراری رد شد.».

### H.4 Number and Jalali inputs (`CA/components/ui/`)

**`NumberField.vue`**
- Wraps `parseDecimal` (G.4), including the «م» / «ه» suffixes and the «10.000» question for money.
- Shows grouped Persian digits when blurred and the raw text while focused.
- Attributes: `inputmode="decimal"`, `dir="ltr"`, `text-end`. Optional suffix «تومان / دلار / متر».
- **Money read-back:** every money field shows a live words line under it, «ده میلیون تومان», in an `aria-live="polite"` region. Words come from `CA/lib/numWords.ts`, with a C# mirror (`App/Common/NumberWords`) for print.
- **Rial receipts:** in Simple mode the money suffix is a toggle «تومان | ریال» (in a Toman tenant). Rial is divided by 10 on save; the words line shows the Toman value.
- Accepts negatives only with `allowNegative`.
- Replaces `MoneyInput` in new code. `MoneyInput` stays on old screens until P9.

**`JalaliDateField.vue`**
- A masked text field «۱۴۰۵/۰۶/۱۵».
- Accepts `1405/6/15`, `1405.06.15` and `14050615`, in Latin, Persian or Arabic digits, with the separators «/ . - ٫».
- An impossible date shows «۱۴۰۵/۰۶/۳۲ وجود ندارد» (also 1405/12/30 in a non-leap year). A year above 1500 shows «سال را شمسی بنویسید (مثل ۱۴۰۵)».
- Chips «امروز» and «دیروز».
- Calendar popover: a month grid with Persian month names, keyboard navigable.
- Conversion uses `CA/lib/jalali.ts` (about 60 lines, no dependency), mirrored on the server by `App/Common/JalaliCalendarHelper` (existing) plus `PersianFormat.JalaliDotted`.
- The value is sent as ISO `yyyy-MM-dd` (Gregorian) into `DateOnly`.
- A shared 200-date fixture runs in both xUnit and vitest, including the Persian-digit, separator, impossible-date and Gregorian-year cases above.

### H.5 Help and onboarding

- **«؟ راهنما»** on every merchant screen opens `HelpDrawer.vue` with 3–5 numbered steps, icons and keyboard keys. The content lives in `CA/lib/help.ts`, keyed by page.
- **«کارت راهنما»** (`/Help/Card`, Layout = null, one A4 page): the five steps with icons, to print and keep beside the computer.
- **First-run coach marks** on Home, dismissed per user (`CoachDismissedJson`).
- **Configurator checklist** at `/settings/setup` (H.9).

### H.6 Mobile and tablet in the warehouse

- **Tablet in landscape:** the cards board in two columns, or the bales table with the «اندازه‌گیری» column preset (G.2).
- **Phone:** pack mode (G.5).
- **Manifest:** `orientation` becomes `any` (done in P0).
- **Scanning.**
  - `components/scan/BarcodeScan.vue` (new, P4b): `BarcodeDetector` with `qr_code` and `code_128`, emitting `scanned(code)`. The bales tab and pack mode use it, so a scan **adds the rug to the current bale** instead of navigating.
  - The layout island `QrScanner.vue` (P2) keeps its navigation behaviour, but with `code_128` and **exact** serial and SKU matching, which fixes the first-partial-match defect.
  - Labels printed at intake carry the serial as a QR code («برچسب شماره», H.8).

**Tablet entry** (recording final sizes on a tablet is the core warehouse job):
- (a) Touch keyboards get the right layout: `inputmode` and `enterkeyhint="next"` on every grid cell (G.1).
- (b) Under `pointer: coarse`, a sticky 56 px action bar sits above the on-screen keyboard: «همان اندازه» · «قبلی» · «بعدی» · «برگرد» · «بستن». It mirrors `=`, Shift+Tab, Tab/Enter, Ctrl+Z and Esc, so no action is keyboard-only.
- (c) Each rug card in the cards view has an «اندازه» chip that opens a bottom sheet with two `NumberField`s (طول، عرض) and the buttons «همان اندازهٔ اول» and «ثبت».
- (d) The bales grid defaults to the «اندازه‌گیری» column preset under 1100 px.
- (e) Inline confirms render as 44 px buttons («بله، ۲٫۰۸ متر» / «نه»); key hints appear only under `pointer: fine`.
- **Announcements:** totals are read out in an `aria-live="polite"` region: «نگلهٔ ۳: ۴ تخته، ۱۳٫۵۱ متر».
- **Dead spots:** drafts and retries (G.7) cover them.

### H.7 Accessibility

- **Grid semantics:**
  - `role="grid"`;
  - `aria-rowcount`, `aria-colcount`, `aria-rowindex` and `aria-colindex`;
  - `aria-selected`;
  - `aria-invalid` with `aria-describedby` for errors.
- **Dialogs and drawers** reuse UiShell's focus trap. Esc closes them and focus returns to the opener. The `RugWorkflowPanel` modal is ported in P9.
- **Contrast and status:** AA contrast in both themes. Status always shows an icon plus text.
- **No dragging-only actions** (WCAG 2.2 SC 2.5.7): moving a rug between bales has the «انتقال» picker (button, or key M on a focused card) besides dragging; reordering has the same picker. axe cannot detect this, so P4b checks it by hand (touch and keyboard).
- **New controls:**
  - category tiles, «برای» chips and the basis segments are `role="radiogroup"` with RTL arrow keys; the selected item shows the check icon plus text;
  - the bale target bar is `role="meter"` with `aria-valuetext` «۱۲٫۳۷ از ۱۳ متر», and its three states use tokens with an icon and text: `outline-variant` below target, `success` within it, `warning` above it;
  - readiness chips are buttons with `aria-describedby` pointing to the full message (H.3b);
  - every target is at least 44 px.
- **Motion and navigation:** `prefers-reduced-motion` is respected. The skip link's `sr-only-focusable` class is defined.
- **RTL text:** `<bdi dir="ltr">` wraps codes and serial ranges.

### H.8 «بستهٔ صادرات فرش» preset

`POST /api/settings/presets/export-rugs` (Configure) is idempotent. It adds only missing items, matched by key or `SeedKey`, and never overwrites anything. It is audited as `PresetApplied`.

| Item | Content |
|---|---|
| Custom fields | Reuse `bg_color`. If it is missing, create it with the label «رنگ متن»; if it exists, keep its label and report «موجود بود: {label}». Create `border_color` «رنگ حاشیه» (Text). |
| Purchase categories | «قیمت خرید» `purchase.price` (PerM2, ShowInIntake, icon `receipt`); «دلالی» `purchase.broker` (Percent 2, ShowInIntake, icon `receipt`); «کرایه تا انبار» `purchase.freightIn` (PerPiece, icon `truck`); «خرید یکجا» `purchase.lump` (Total, ByArea, Scope IntakeBatch, icon `coins`; offered only from an intake batch in Simple mode). The first three are already seeded in P2 (K.2), so the preset reports them as «موجود بود». |
| Cost categories (each with its `Icon`) | شستشو `expense.washing` (PerM2, Intake, StepType washing; `droplet`); رفوگری `expense.repair` (Total, ByArea, Intake, StepType repair; `needle`); حمل داخلی (Total, ByArea, Effective; `truck`); کرایهٔ خارجی (Total, ByArea, Effective; `ship`); «بسته‌بندی و {bale}» (PerPiece; `tape`; the name uses the chosen bale term); گمرک و ترخیص (Total, ByDeclaredValue, Scope Shipment; `stamp`); انبارداری (Total, ByArea; `warehouse`); سایر (Total, ByArea; `dots`) |
| Declared values | گبه 45, خراسان تجاری 25, نائین 9 لا 80. Added **only** when the checkbox «این نرخ‌ها نمونه‌اند؛ تأیید می‌کنم» is ticked. |
| Grid layouts | G.2 (labels resolve from `terms`) |
| Label template | «برچسب شماره»: the serial at 18 pt plus a QR of the serial, 50 × 30 mm (needed for scan-to-bale) |
| Preferences | `DefaultBaleTargetAreaM2 = 13`; `SerialStart` (asked in the dialog); `BaleTerm` (asked) |
| Templates | E.9 set as default, plus the cost and rug-list templates |

A test asserts that every `Icon` used by a seed or the preset exists in `icons.svg`.

### H.9 Configurator checklist (`/settings/setup`, `GET /api/settings/setup-status`)

Each `done` is computed server-side and is never ticked by hand. «آماده است» means steps 1–6 are done. Until then the son's Home shows «۲ کار تا آماده شدن مانده [ادامه]».

| # | Key | Required | Done when | Link |
|---|---|---|---|---|
| 1 | `preset` | yes | A `PresetApplied` audit row exists | `/settings/setup` (preset dialog) |
| 2 | `serials` | yes | `SerialStart` is set, or any rug has a `SerialNo` | `/settings/preferences` |
| 3 | `declaredValues` | yes | At least one rate exists, and every distinct origin (the key field's value) in stock or in open lines has a rate; otherwise the step lists the missing values | `/settings/declared-values` |
| 4 | `baleTarget` | yes | `DefaultBaleTargetAreaM2` is set | `/settings/preferences` |
| 5 | `merchantUser` | yes | An active user exists with role Merchant, `UiMode` Simple and `TextScale` ≥ Large | `/Users` |
| 6 | `declarationTemplate` | yes | `DefaultDeclarationTemplateId` is set and the template validates | `/settings/documents` |
| 7 | `testExport` | no | An `ExportRun` exists or a preview has been opened | a shipment's «اکسل و چاپ» tab |
| 8 | `serialLabel` | no | A label template contains a `rug.serial` QR | `/Labels` |
| 9 | `exporter` | no (needed only when the default template uses `tenant.*` fields) | The exporter fields are filled | `/settings/preferences` |

---

## I. API, pages and islands

### I.1 Policies and conventions

- **Policies.** `Web/Auth/Policies.cs` defines `Floor`, `Business` and `Configure` (A.1), plus a helper `Policies.IsBusiness(ClaimsPrincipal)` for views.
- **`ApiControllerBase`** changes from `[Authorize]` to `[Authorize(Policy = Policies.Floor)]`. Actions add Business or Configure where needed.
- **Error mapping** (P1, ordered arms, all placed **before** `InvalidOperationException`):
  - `KeyNotFoundException` → 404. This covers cross-tenant ids.
  - `AppValidationException` (Application) and `DomainValidationException` (Domain) → 400 `{message, errors:{field:[msg]}}`. The name avoids a clash with `System.ComponentModel.DataAnnotations.ValidationException`.
  - `DomainConflictException(message, field?)` (Domain) → 409 `{message, field}`.
    - Later phases derive from it: `ShipmentLockedException` (P4a), `SerialOverlapException` (P2, `field: "rug.serial"`), `StaleStampException` (P4a).
    - The filter is never edited again.
  - Existing arms are unchanged.
- **MVC actions** get the same mapping through `DomainExceptionPageFilter` (P1, WP1-A), registered globally in `Program.cs` via `AddControllersWithViews(o => o.Filters.Add<DomainExceptionPageFilter>())`. In non-API controllers it catches `DomainConflictException` and `AppValidationException`, sets `TempData["Toast"]` with level `error` and the exception's Persian message, and redirects to the referer (the Details page for the Rugs actions). So trashing a baled rug via `POST /Rugs/Delete` gives 302 plus «اول این فرش را از نگلهٔ ۱۲ خارج کنید.», not a 500; the same holds for `Rugs/Restore` (`SerialOverlapException`), the Groups POSTs and the Settings forms.
- **DTOs** are positional records, mirrored in `CA/lib/types/*.ts`. New members on existing records are appended as trailing parameters with defaults, **but** defaults help only in-memory call sites:
  - `RugListItemDto` and `ProviderWorkItemDto` end with **required** parameters today, and both are constructed inside EF expression trees (`RugRepository.Project`, `ServiceProviderRepository.ListWorkAsync`), where omitted optional arguments do not compile (CS0854).
  - Rule: every member appended to a DTO that an EF projection constructs (`RugListItemDto`, `DeletedRugDto`, `ProviderWorkItemDto`, `ProviderBalanceDto`) is passed **explicitly in the projection in the same commit**, and the DTO change and its projection belong to the same work package (K.2: RugListItemDto + `RugRepository.Project` → WP2-B; ProviderWorkItemDto + `ListWorkAsync` → P5).
- **`version`** is xmin as a JSON number (uint).
- **Money in responses:** money members are null for non-Business users (A.5).

### I.2 Endpoints

| Area | Route | Verb | Policy | Request → Response |
|---|---|---|---|---|
| Me | `/api/me/preferences` | PUT | Floor | `{textScale, gridEnter, uiMode?}` → 204 |
| Fields | `/api/fields?purpose=grid\|document&kind=` | GET | Floor | → `FieldDefDto[]` (money fields only for Business) |
| Preferences | `/api/settings/preferences` | GET/PUT | Configure | `TenantPreferencesDto` (+version). A change of `MoneyScale` once money exists → 409. |
| Grid layout | `/api/settings/grid-layout/{gridId}` | GET/PUT | Configure | `GridLayoutDto(gridId, columns, version)`; stale version → 409 |
| Preset | `/api/settings/presets/export-rugs` | POST | Configure | `{serialStart?, baleTerm, confirmSampleRates}` → `PresetResultDto(added[], skipped[])` |
| Setup | `/api/settings/setup-status` | GET | Configure | → `{ready, steps:[{key, required, done, link, missing?: string[]}]}` (H.9) |
| Home | `/api/home/summary` | GET | Floor | → `HomeSummaryDto` (money null for non-Business) |
| Rugs | existing `/api/rugs*` | — | Floor | PUT now requires `version` and uses `RugFieldWriter`. `RugDto` appended (I.3). Money redacted; money members in a non-Business request → 403 (A.5). |
| Rugs | `/api/rugs/values?field=&q=` | GET | Floor | → `string[]` |
| Rugs | `/api/rugs/next-serial` | GET | Floor | → `{next: int?}` (G.4 rule; null in Manual mode) |
| Rugs | `/api/rugs/by-serial/{serial}` | GET | Floor | → `{rug: RugListItemDto, location, matchType: 'exact'\|'inRange'}` or 404 |
| Rugs | `/api/rugs/{id}/final-size` | PUT | Floor | `{length?, width?, sameAsIntake?, clear?, version}` → `RugDto` |
| Rugs | `/api/rugs/{id}/split` | POST | Floor | `{quantity, serialFrom?, serialTo?, version}` → `{original: RugDto, created: RugDto}`. 409 when sold or locked. |
| Rugs | `/api/rugs/{id}/purchase-lines` | GET/PUT | Business | `PurchaseLineDto[]` replace-all + rug `version` → `{lines, purchaseCost, version}`. The PUT also sets `Rug.UpdatedAt`, so the rug's xmin advances and a second PUT with the same version gets 409. |
| Rugs | `/api/rugs/{id}/cost-breakdown` | GET | Business | → `RugCostBreakdownDto` |
| Batches | existing `/api/batches*` | — | Floor | `RugBatchDto` + `SellerName, Number`. DELETE → entries scoped to the batch become Unallocated (confirm in UI). |
| Batches | `/api/batches/today` | POST | Floor | `{sellerName, receivedAt?}` → find-or-create `RugBatchDto` by the key (tenant, Iran day of `receivedAt` ?? today, `KeyNormalize(sellerName)`) (B.5) |
| Batches | `/api/batches/recent?days=7` | GET | Floor | → `RugBatchDto[]` with piece counts (the «ورودی‌های اخیر» strip) |
| Grid | `/api/grid/{gridId}?batchId=&date=&shipmentId=&skip=&take=` | GET | Floor | → `GridDataDto(columns, rows, totals, nextSerial)`; dispatched to `IGridSource`; unknown id → 404. `intake` with `date` returns every row of that Iran day. |
| Grid | `/api/grid/rugs/batch` | POST | Floor (money keys from non-Business → 403 for the whole request; deletes per G.6) | G.6 |
| Shipments | `/api/shipments?status=` | GET | Floor | → `ShipmentListItemDto[]` |
| Shipments | `/api/shipments` | POST | Business | `CreateShipmentRequest` → `ShipmentDto` |
| Shipments | `/api/shipments/{id}` | GET / PUT / DELETE | Floor / Business / Business | `ShipmentDto`. PUT `{…, version}` (409 if Finalized). DELETE only when Open and line-free (entries → Unallocated). |
| Shipments | `/api/shipments/{id}/readiness?templateId=` | GET | Floor | → `ReadinessIssueDto[]` (also the preflight) |
| Shipments | `/api/shipments/{id}/finalize` | POST | Business | `{version}` → `{ok, issues[], shipment}` |
| Shipments | `/api/shipments/{id}/reopen` | POST | Business | `{version, reason?}` → `ShipmentDto` |
| Shipments | `/api/shipments/{id}/bales` | GET / POST | Floor | → `BaleDto[]` (with rows); POST `{number?}` → `BaleDto` |
| Shipments | `/api/shipments/{id}/bales/renumber` | POST | Floor | `{version}` → `BaleDto[]` |
| Bales | `/api/bales/{id}` | PUT / DELETE | Floor | `{number?, weightKg?, notes?, version}`. DELETE `?version=` only when empty (409 otherwise). |
| Lines | `/api/shipments/{id}/lines/batch` | POST | Floor (`rate` op Business) | `{ops:[{op:'add', clientOpId, baleId\|baleNumber, rugId?\|serial?}, {op:'move', lineId, lineVersion, baleId\|baleNumber, seq?}, {op:'remove', lineId, lineVersion}, {op:'measure', rugId, rugVersion, finalLength?, finalWidth?, sameAsIntake?}, {op:'rate', lineId, lineVersion, usdPerM2Override}, {op:'order', baleId, lineIds[]}]}` → `{results:[{clientOpId, ok, errors[], row?: BaleRowDto}], bales: BaleSummaryDto[], totals}`. An `add` with a `baleNumber` that does not exist creates the bale in the same pass (C.2). |
| Lines | `/api/shipments/{id}/find-rug?serial=` | GET | Floor | → `{rug?, inBale?: {shipmentNo, baleNo}, matchType}` |
| Revenue | `/api/shipments/{id}/revenue` | PUT | Business | `{amount, currency, fx, version}` → `ShipmentProfitDto` |
| Profit | `/api/shipments/{id}/profit` | GET | Business | → `ShipmentProfitDto` |
| Declared | `/api/declared-values` | GET | Floor | → `DeclaredValuesDto(rates, stamp)` (outside `/api/settings/*`, so "every `/api/settings/*` is 403 for non-Configure" stays true) |
| Declared | `/api/declared-values` | POST | Business | `{keyValue, usdPerM2, stamp}` → `{affectedOpenLines, stamp}`. Adds a missing key only; an existing key → 409; `usdPerM2 < 0` → 400 (C.5). |
| Declared | `/api/settings/declared-values` | PUT | Configure | `{rates[], stamp}` replace-all → `{affectedOpenLines, stamp}`. Stale stamp → 409 `StaleStampException`. |
| Costs | `/api/costs?shipmentId&batchId&categoryId&state&from&to&page` | GET | Business | → `Paged<CostEntryListItemDto>` |
| Costs | `/api/costs` | POST | Business | `SaveCostEntryRequest` → `CostEntryDto` (idempotent by `ClientKey`) |
| Costs | `/api/costs/{id}` | GET / PUT / DELETE | Business | `CostEntryDto`. PUT with `version`. DELETE `?version=` (soft). |
| Costs | `/api/costs/{id}/restore` | POST | Business | → `CostEntryDto` |
| Costs | `/api/costs/preview` | POST | Business | `SaveCostEntryRequest` → `AllocationPreviewDto` |
| Costs | `/api/costs/resolve-targets` | POST | Business | `{serialsText?, filter?: RugQuery, inStock?: bool}` → `{rugIds[], lines, pieces, areaM2, unresolved[], scopeNote}` |
| Categories | `/api/cost-categories` | GET | Floor (Purchase group only) / Business (all) | → `CostCategoryDto[]` |
| Categories | `/api/cost-categories/quick` | POST | Business | `{name, group}` → `CostCategoryDto` (defaults from the group) |
| Categories | `/api/cost-categories[/{id}]` | POST/PUT/DELETE | Configure | DELETE = deactivate when in use. A group change on a used category → 409. |
| Analytics | existing `/api/analytics` | GET | **Business** (`Aging` stays Floor) | money (A.5) |
| Sales | existing `/api/sales/*` | all | **Business** (whole class) | A.5 |
| Diagnostics | `/api/diagnostics/cost-consistency` | GET | Configure | → `{checked, mismatches[]}` |
| Diagnostics | `/api/diagnostics/ledger-rebuild` | POST | Configure | → `{rugs, entries, lines}` |
| Templates | `/api/document-templates?kind=` | GET | Floor (Operator: `!UsesMoneyFields`) | → `DocumentTemplateListItemDto[]` |
| Templates | `/api/document-templates/{id}` | GET | Floor (money templates: Business) | → `DocumentTemplateDto` |
| Templates | `/api/document-templates[/{id}]` | POST/PUT/DELETE | Configure | `{name, kind, layout, version}`; 400 `{errors:[{path, message}]}` |
| Templates | `/api/document-templates/{id}/duplicate` · `/reset` · `/set-default` | POST | Configure | → `DocumentTemplateDto` |
| Templates | `/api/document-templates/{id}/revisions` · `/revisions/{rev}/restore` | GET · POST | Configure | → `RevisionDto[]` · `DocumentTemplateDto` |
| Templates | `/api/document-templates/validate` | POST | Configure | `{layout}` → `{errors[], usesMoneyFields}` |
| Templates | `/api/document-templates/validate-expression` | POST | Configure | `{expr, kind, columnKeys[], sampleShipmentId?}` → `{ok, type, error?, excel?, samples[]}` |
| Documents | `/api/documents/preview` | POST | Floor (an unsaved `layout` needs Configure; money templates need Business) | `{templateId?\|layout?, shipmentId?\|rugIds?, maxGroups}` → `RenderedDocumentDto` |
| Documents | `/api/shipments/{id}/exports` | GET | Floor | → `ExportRunDto[]` |
| Import | `/api/imports` | GET / POST (multipart) | Business; POST `[RequestSizeLimit(10_485_760)]`, rate `heavy` | → `ImportJobDto[]` / `ImportJobDto(id, sheets[], headerRow, mapping, mode, title?, duplicateOf?, version)` |
| Import | `/api/imports/{id}/preview` | POST | Business | `{sheet, mapping, options, overrides}` → `ImportPreviewDto` (job → Previewed) |
| Import | `/api/imports/{id}/commit` | POST | Business | `CommitImportRequest(saveMapping, version)` → `ImportResultDto`. Allowed only when `Status = Previewed` and the version matches; otherwise 409. |
| Import | `/api/imports/{id}/undo` | POST | Business | → `{ok, blockers[]}` |

**MVC routes**

| Route | Policy | Notes |
|---|---|---|
| `GET /shipments/{id}/export.xlsx?template=` | Floor (money template: Business) | `DocumentsController`, rate `heavy`; RFC 5987 `Content-Disposition` (E.7) |
| `GET /shipments/{id}/print?template=&autoprint=` | Same | Layout = null |
| `POST /documents/rugs.xlsx` | Floor (money template: Business) | `[ValidateAntiForgeryToken]`; form `rugIds[]`, `templateId?` (RugList). Foreign ids → 404, with no ExportRun and no file. |

### I.3 DTO outlines (Application, positional records unless noted)

```csharp
// Platform
public record TenantPreferencesDto(string SkuPrefix, SerialMode SerialMode, int? SerialStart, short MoneyScale, decimal? DefaultUsdFx,
    AllocationMethod DefaultMethod, AreaBasis DefaultAreaBasis, string DeclaredValueKeyField, decimal? DeclaredValueFallbackUsd,
    decimal? DefaultBaleTargetAreaM2, short BaleTargetTolerancePercent, bool RequireFinalSizeToFinalize, BaleTerm BaleTerm,
    Guid? DefaultDeclarationTemplateId, string? ExporterName, string? ExporterCode, string? ExporterAddress, string? ExporterPhone,
    string? CustomsOffice, bool MoneyScaleLocked, uint Version, decimal MaxPlausibleSideM = 12m);
public record FieldDefDto(string Key, string Label, string Scope, string DataType, string DefaultFormat, bool Aggregatable, bool Money, bool GridEditable);
public record HomeSummaryDto(IReadOnlyList<ShipmentListItemDto> OpenShipments /* every Open shipment, latest line activity first (A.4) */,
    IReadOnlyList<ReadinessIssueDto> Readiness /* of the first */, int StockPieces, decimal StockAreaM2,
    decimal? OpenShipmentCostPerM2, int? UnallocatedEntries, IReadOnlyList<ActivityItemDto> Recent);
public record GridLayoutDto(string GridId, IReadOnlyList<GridColumnDto> Columns, uint Version);

// Rugs. Appended params carry defaults, but a DTO built inside an EF projection gets every new member passed
//   explicitly in that projection in the same commit (I.1; CS0854).
// RugDto: AreaSquareMeters now reports the stored per-piece AreaM2 (rounded); Costs becomes RugCostSummaryDto? (null for non-Business);
//   PurchaseCost = Σ purchase-line amounts only (never lump shares, never the obsolete column; display-only);
//   PurchaseCost / TargetSalePrice null for non-Business. WorkflowSteps[] money members null for non-Business (A.5).
// RugDto += (string? SerialNo = null, int Quantity = 1, decimal IntakeLength = 0, decimal IntakeWidth = 0, decimal? FinalLength = null,
//            decimal? FinalWidth = null, decimal TotalAreaM2 = 0, RugLocation Location = RugLocation.InStock, Guid? ShipmentId = null,
//            int? ShipmentNumber = null, Guid? BaleId = null, int? BaleNumber = null, decimal? UsdPerM2 = null, decimal? LineUsd = null,
//            uint Version = 0, IReadOnlyList<PurchaseLineDto>? PurchaseLines = null /* Business only */)
//   Shipment/bale/USD members are filled by IRugDtoEnricher (P2 seam; P4a ShipmentRugDtoEnricher, one query keyed by RugId).
// RugListItemDto: TotalInvestment becomes decimal? (null for non-Business);
//   += (string? SerialNo = null, int Quantity = 1, decimal TotalAreaM2 = 0, bool CostComputed = false, decimal? CostPerM2 = null,
//       RugLocation Location = RugLocation.InStock, int? BaleNumber = null, int? ShipmentNumber = null, uint Version = 0)
//   Every one of these is passed explicitly in RugRepository.Project (WP2-B owns both the DTO append and the projection).
// RugCostSummaryDto: existing TotalProcessCost (= StepCost), PurchaseCost (= RugCostTotal.PurchaseCost: lines + lump shares)
//   and TotalInvestment (= Total) keep their names; appended only:
//   += (decimal StepCost = 0, decimal ExpenseCost = 0, decimal? CostPerM2 = null, decimal CostPerPiece = 0,
//       decimal? ExportRevenueShare = null, bool Computed = false, decimal? CostAtSale = null)
// UpdateRugRequest += (uint? Version = null, IReadOnlyDictionary<string, JsonElement?>? Values = null)  // Values → RugFieldWriter
//   Legacy members, so the old RugForm round trip (it resends purchaseCost and the effective W/L on every save) cannot corrupt data:
//   - PurchaseCost (Business only; a non-Business request carrying it or TargetSalePrice → 403):
//       the rug has no purchase lines        → creates one Total line «قیمت خرید»;
//       exactly one line, and it is Total    → updates that line's rate;
//       otherwise, equal to Σ line amounts   → ignored;
//       otherwise                            → 409 «قیمت خرید این فرش چندسطری است؛ از بخش «خرید» ویرایش کنید».
//   - WidthMeters / LengthMeters: written to IntakeWidth/IntakeLength only while FinalLength is null;
//       with a final size they are ignored when equal to the effective pair, else 409 (use /final-size).
//   From P2 RugForm.vue sends only Values + Version (no purchaseCost, targetSalePrice or W/L) and embeds PurchaseLinesEditor (Business).
// RugQuery (init-property record, RugListDtos.cs:66; all declared in P2):
//   + Guid? ShipmentId, Guid? BaleId, bool NotBaled, RugLocation? Location, string? Serial
//   + HasActiveFilters terms for each; RugListViewModel link round-trip (incl. existing WithoutBatch)
public record PurchaseLineDto(Guid? Id, Guid? CategoryId, string Label, CostBasis Basis, decimal Rate, decimal Amount, int SortOrder, string? Notes);
public record RugCostBreakdownDto(IReadOnlyList<PurchaseLineDto> Purchase, IReadOnlyList<StepCostDto> Steps,
    IReadOnlyList<AllocationLineDto> Allocations, RugCostSummaryDto Summary);
public record AllocationLineDto(Guid EntryId, int EntryNumber, string Category, CostGroup Group, DateOnly Date, decimal Amount,
    long WeightUnits, long TotalWeightUnits, AllocationMethod Method, bool IsPinned, decimal EntryBase);

// Grid
public record GridColumnDto(string Key, string Label, string Type /* serial|text|combo|select|int|dim|money|money+unit|percent|readonly */,
    decimal Width /* in ch, so it scales with TextScale */, bool Sticky, bool ReadOnly, bool Required, IReadOnlyList<string>? Options, bool Money,
    bool TabStop = true /* false for the bales grid's descriptive columns in Simple mode */);
public record GridRowDto(Guid RugId, uint Version, string? Sku, IReadOnlyDictionary<string, JsonElement?> Values,
    IReadOnlyDictionary<string, JsonElement?> Derived, Guid? LineId = null, uint? LineVersion = null /* filled only by BalesGridSource */);
public record GridDataDto(IReadOnlyList<GridColumnDto> Columns, IReadOnlyList<GridRowDto> Rows, GridTotalsDto Totals, int? NextSerial);
public record GridTotalsDto(int Rows, int Pieces, decimal AreaM2, decimal? Purchase);
public record GridBatchRequest(string GridId, GridBatchContext? Batch, Guid? ShipmentId, IReadOnlyList<GridOp> Ops);
public record GridBatchContext(Guid? Id, string? SellerName, DateOnly? ReceivedAt);
public record GridOp(Guid ClientRowId, Guid? RugId, uint? Version, IReadOnlyDictionary<string, JsonElement?> Values,
    IReadOnlyDictionary<string, JsonElement?>? BaseValues, bool Delete, uint? LineVersion = null);
public enum GridRowStatus { Saved = 0, Invalid = 1, Conflict = 2, Deleted = 3 }
public record GridOpResult(Guid ClientRowId, Guid? RugId, uint? Version, string? Sku, GridRowStatus Status,
    IReadOnlyDictionary<string, JsonElement?>? Derived, IReadOnlyList<FieldErrorDto>? Errors, IReadOnlyDictionary<string, JsonElement?>? Current,
    uint? LineVersion = null);
public record FieldErrorDto(string Field, string Message);

// Shipments
public record CreateShipmentRequest(string? Title, DateOnly DocumentDate, string? Destination, string? BuyerName, string? DeclarationNo,
    string? CustomsOffice, decimal? TargetBaleAreaM2, decimal? UsdRate, string? Notes);
public record ShipmentListItemDto(Guid Id, int Number, string Title, DateOnly DocumentDate, ShipmentStatus Status, int Bales, int Lines,
    int Pieces, decimal AreaM2, decimal UsdTotal, int ReadinessBlocking);
public record ShipmentDto(Guid Id, int Number, string Title, DateOnly DocumentDate, string? Destination, string? BuyerName,
    string? DeclarationNo, string? CustomsOffice, ShipmentStatus Status, int FinalizeCount, DateTimeOffset? FinalizedAt, string? SnapshotHash,
    decimal? TargetBaleAreaM2, decimal? UsdRate, string? Notes, uint Version, IReadOnlyList<BaleSummaryDto> Bales,
    ShipmentTotalsDto Totals, IReadOnlyList<ReadinessIssueDto> Readiness);
public record BaleSummaryDto(Guid Id, int Number, uint Version, int Lines, int Pieces, decimal AreaM2, decimal UsdTotal, decimal? WeightKg);
public record BaleDto(Guid Id, int Number, uint Version, decimal? WeightKg, string? Notes, int Pieces, decimal AreaM2, decimal UsdTotal,
    decimal? TargetAreaM2, IReadOnlyList<BaleRowDto> Rows);
public record BaleRowDto(Guid LineId, uint LineVersion, Guid RugId, uint RugVersion, int Seq, string? Serial, int Qty, string? FieldColor,
    string? BorderColor, string? Pattern, string? Origin, string? Material, decimal IntakeLength, decimal IntakeWidth, decimal? FinalLength,
    decimal? FinalWidth, int? SizeChangeCm, decimal AreaM2 /* per piece */, decimal LineAreaM2 /* × qty */, decimal? UsdPerM2,
    DeclaredRateSource RateSource, decimal? PieceUsd, decimal? LineUsd /* PieceUsd × qty */);
public record ShipmentTotalsDto(int Bales, int Lines, int Pieces, decimal AreaM2, decimal UsdTotal);
public record ReadinessIssueDto(string Code, string Message, int Count, IReadOnlyList<Guid> RugIds, bool Blocking);
public record ShipmentProfitDto(int Pieces, decimal AreaM2, decimal UsdTotal, decimal Purchase, decimal Steps, decimal Expenses, decimal Total,
    decimal? CostPerM2, decimal? RevenueBase, decimal? Profit, bool Finalized);
public record DeclaredValueRateDto(Guid? Id, string KeyValue, decimal UsdPerM2);
public record DeclaredValuesDto(IReadOnlyList<DeclaredValueRateDto> Rates, int Stamp);

// Costs
public record SaveCostEntryRequest(Guid CategoryId, DateOnly Date, string? Description, string? ReceiptNo, Guid? ServiceProviderId,
    CostBasis Basis, decimal? Rate, decimal? Amount, CurrencyUnit Currency, decimal? FxRate, AreaBasis AreaBasis, AllocationMethod Method,
    CostScope Scope, Guid? ShipmentId, Guid? BatchId, IReadOnlyList<Guid>? RugIds, IReadOnlyList<Guid>? BaleIds, string? ScopeNote,
    IReadOnlyList<PinDto>? Pins, uint? Version, Guid? ClientKey);
public record PinDto(Guid RugId, decimal Amount);
public record CostEntryListItemDto(Guid Id, int Number, DateOnly Date, string Category, CostGroup Group, string? Description,
    string TargetsText, decimal BaseAmount, decimal? OriginalAmount, CurrencyUnit Currency, AllocationState State, decimal Unallocated, bool Deleted);
public record CostEntryDto(Guid Id, int Number, SaveCostEntryRequest Request, uint Version, AllocationPreviewDto Result, DateTimeOffset CreatedAt);
public record AllocationPreviewDto(decimal OriginalAmount, decimal BaseAmount, int Lines, int Pieces, decimal AreaM2, decimal? PerM2,
    AllocationState State, decimal Unallocated, IReadOnlyList<WarningDto> Warnings, IReadOnlyList<ShareDto> Shares, string Sentence);
public record ShareDto(Guid RugId, string? Serial, long WeightUnits, string WeightUnit, decimal Amount, bool IsPinned, decimal? PerPiece);
public record WarningDto(string Code, string Message, int Count);
public record CostCategoryDto(Guid Id, string Name, CostGroup Group, string? Icon, int SortOrder, bool IsActive, CostBasis DefaultBasis,
    decimal? DefaultRate, CurrencyUnit? DefaultCurrency, AllocationMethod DefaultMethod, AreaBasis DefaultAreaBasis, CostScope? DefaultScope,
    bool ShowInIntake, Guid? StepTypeId, string? SeedKey, uint Version);

// Providers (appended): ProviderWorkItemDto += (ProviderWorkKind Kind = ProviderWorkKind.Step, Guid? CostEntryId = null,
//   int? CostEntryNumber = null, int? RugCount = null) — passed explicitly in ServiceProviderRepository.ListWorkAsync (P5 owns both).

// Documents
public record DocumentTemplateDto(Guid Id, string Name, DocumentKind Kind, bool IsDefault, int Revision, bool UsesMoneyFields,
    JsonElement Layout, uint Version);
public record RenderedDocumentDto(PageSetupDto Page, IReadOnlyList<RenderedSheetDto> Sheets, IReadOnlyList<WarningDto> Warnings, bool IsDraft);
public record RenderedSheetDto(string Name, string Role, IReadOnlyList<RenderedColumnDto> Columns, IReadOnlyList<RenderedRowDto> Rows,
    int PrintTitleRows, IReadOnlyList<int> RowBreaksAfter);
public record RenderedRowDto(string RowId, string Kind, IReadOnlyList<RenderedCellDto> Cells);
public record RenderedCellDto(int Col, int ColSpan, int RowSpan, string Display, string Style, string? FormulaA1, bool ValueOnly, bool IsNumber);
public record ExportRunDto(Guid Id, DateTimeOffset At, string User, string Template, int Revision, ExportFormat Format, bool IsDraft, string DataHash, string? FileSha256);

// Import
public record ImportJobDto(Guid Id, int Number, string FileName, ImportStatus Status, IReadOnlyList<string> Sheets, int? HeaderRow,
    JsonElement? Mapping, ImportMode Mode, string? TitleText, DateOnly? TitleDate, ImportDuplicateDto? DuplicateOf, DateTimeOffset CreatedAt, uint Version);
public record CommitImportRequest(bool SaveMapping, uint Version);
public record ImportPreviewDto(ImportCountsDto Counts, IReadOnlyList<ReconciliationRowDto> Reconciliation,
    IReadOnlyList<ImportRowDto> Rows, IReadOnlyList<IssueSummaryDto> IssuesByCode, IReadOnlyList<DeclaredValueRateDto>? SuggestedRates, uint Version);
public record ImportCountsDto(int Lines, int Pieces, int Bales, decimal AreaM2, decimal UsdTotal, int Errors, int Warnings, int Attach, int Skip);
public record ImportRowDto(int SheetRow, string Action /* create|attach|skip|move|error */, int? Bale,
    IReadOnlyDictionary<string, JsonElement?> Values, IReadOnlyList<ImportIssueDto> Issues);
public record ImportIssueDto(string Code, string Severity, string Message, string? Field);
public record ReconciliationRowDto(int? Bale, int Pieces, int? FooterPieces, int? SummaryPieces, decimal Area, decimal? FooterArea,
    decimal? SummaryArea, decimal Usd, decimal? FooterUsd, decimal? SummaryUsd, bool Ok);
public record ImportResultDto(int CreatedRugs, int AttachedRugs, Guid? ShipmentId, int? ShipmentNumber, Guid? BatchId, int SkippedRows);
```

### I.4 Razor pages

| Route | Controller → view | Policy | Islands |
|---|---|---|---|
| `/` | `HomeController.Index` → `Views/Home/Index.cshtml` (renders `IHomeCard`s). `[Authorize]` on **Index only**: SystemAdmin → `/Admin`; anonymous → login challenge; tenant roles render Home. `Error` and `StatusCodeError` are `[AllowAnonymous]`. | any authenticated user (tenant roles satisfy Floor) | `home-coach` |
| `/Dashboard` | existing (money tiles redacted for non-Business) | Floor | existing |
| `/Reports` | existing | **Business** | existing |
| `/More` | `MoreController` → `Views/More/Index.cshtml` | Floor | — |
| `/Intake` | `IntakeController` → `Views/Intake/Index.cshtml` | Floor | `intake-grid` |
| `/Rugs`, `/Rugs/Details/{id}` | existing `RugsController` (Details sections as partials, plus the `IRugDetailPanel` slot) | Floor | `rugs-grid`, `rug-final-size`, `purchase-lines-editor` (Business), `rug-cost-breakdown` (Business), existing islands |
| `/Shipments` | `ShipmentsController.Index` (P4a shell, P4b island) | Floor | `shipment-list` |
| `/Shipments/{id}` | `ShipmentsController.Workspace` → `Workspace.cshtml` + `IShipmentTab` partials (P4a shell) | Floor | `shipment-header`, `baling-board`, `shipment-costs`, `shipment-output`, `shipment-summary` |
| `/Shipments/{id}/Pack` | `ShipmentPackController` (P4b) | Floor | `pack-mode` |
| `/shipments/{id}/print`, `/export.xlsx`, `/documents/rugs.xlsx` | `DocumentsController` | Floor / per template | none (nonce'd script) |
| `/Costs` | `CostsController` → `Views/Costs/Index.cshtml` | Business | `cost-ledger` |
| `/Import`, `/Import/History` | `ImportController` | Business | `import-wizard`, `import-history` |
| `/Help/Card` | `HelpController.Card` (Layout = null) | Floor | — |
| `/Settings` (hub), `/settings/setup`, `/settings/grid`, `/settings/cost-categories`, `/settings/declared-values`, `/settings/preferences`, `/settings/documents[/{id}]`, `/settings/ledger-health` | Attribute-routed controllers in `Web/Controllers/Settings/`. Denied users get 302 → `/account/denied`. | Configure | `setup-checklist`, `grid-columns-editor`, `cost-categories-manager`, `declared-values-editor`, `preferences-form`, `document-templates-list`, `document-designer`, `ledger-health` |

### I.5 Islands and panels

Islands are auto-registered by `import.meta.glob('./**/*.island.vue')`; the island name is the kebab-case of the file stem.

| Island | File | Props (`data-props`) |
|---|---|---|
| `home-coach` | `islands/platform/HomeCoach.island.vue` | `{ steps: string[] }` |
| `preferences-form` | `islands/settings/PreferencesForm.island.vue` | `{ }` (loads via API) |
| `setup-checklist` | `islands/settings/SetupChecklist.island.vue` | `{ }` |
| `grid-columns-editor` | `islands/settings/GridColumnsEditor.island.vue` | `{ gridId: 'intake' \| 'bales' \| 'rugs' }` |
| `intake-grid` | `islands/intake/IntakeGrid.island.vue` | `{ date: string /* ISO, the Iran day */, defaultSeller: string \| null, serialMode: number, maxPlausibleSideM: number }` |
| `rugs-grid` | `islands/rugs/RugsGrid.island.vue` | `{ query: Record<string,string> }` |
| `rug-final-size` | `islands/rugs/RugFinalSize.island.vue` | `{ rugId: string, version: number, intake: [number, number], final: [number, number] \| null, locked: boolean }` |
| `purchase-lines-editor` | `islands/rugs/PurchaseLinesEditor.island.vue` | `{ rugId: string, version: number, readOnly: boolean }` |
| `rug-cost-breakdown` | `islands/rugs/RugCostBreakdown.island.vue` | `{ rugId: string }` |
| `shipment-list` | `islands/shipments/ShipmentList.island.vue` | `{ canCreate: boolean }` |
| `shipment-header` | `islands/shipments/ShipmentHeader.island.vue` | `{ shipmentId: string, canBusiness: boolean }` |
| `baling-board` | `islands/shipments/BalingBoard.island.vue` | `{ shipmentId: string, view: 'table' \| 'cards', targetAreaM2: number \| null, tolerancePercent: number, locked: boolean }` |
| `pack-mode` | `islands/shipments/PackMode.island.vue` | `{ shipmentId: string, baleId: string \| null, locked: boolean }` |
| `declared-values-editor` | `islands/settings/DeclaredValuesEditor.island.vue` | `{ keyFieldLabel: string }` |
| `shipment-costs` | `islands/costs/ShipmentCosts.island.vue` | `{ shipmentId: string, locked: boolean }` |
| `shipment-summary` | `islands/costs/ShipmentSummary.island.vue` | `{ shipmentId: string }` |
| `cost-ledger` | `islands/costs/CostLedger.island.vue` | `{ filters: { shipmentId?: string, batchId?: string, categoryId?: string }, openNew: boolean, preset?: CostPanelPreset }` |
| `cost-categories-manager` | `islands/settings/CostCategoriesManager.island.vue` | `{ }` |
| `ledger-health` | `islands/settings/LedgerHealth.island.vue` | `{ }` |
| `shipment-output` | `islands/documents/ShipmentOutput.island.vue` | `{ shipmentId: string, templates: { id: string, name: string, money: boolean, isDefault: boolean }[], isDraft: boolean }` |
| `document-templates-list` | `islands/documents/DocumentTemplatesList.island.vue` | `{ }` |
| `document-designer` | `islands/documents/DocumentDesigner.island.vue` | `{ templateId: string, sampleShipmentId: string \| null }` |
| `import-wizard` | `islands/import/ImportWizard.island.vue` | `{ jobId: string \| null }` |
| `import-history` | `islands/import/ImportHistory.island.vue` | `{ }` |

The 19 legacy entries stay explicit in `registry.ts`.

**Panels (P1 host).**
- **Host.** UiShell hosts lazily loaded panels: `ui.openPanel(name, props)` resolves `import.meta.glob('../panels/*.panel.vue')`.
- **Global access.** `window.rugsUI = { toast, confirm, openPanel }`.
- **Razor buttons** open a panel under CSP with `<button data-open-panel="cost-entry" data-panel-props='{…}'>`, delegated in `wwwroot/js/site.js` (no inline handlers).
- **P5 adds** `panels/CostEntry.panel.vue` (name `cost-entry`). It has the props `CostPanelPreset = { scope: number, shipmentId?, batchId?, baleIds?, rugIds?, categorySeedKey? }`, and `cost-ledger` opens it too.

### I.6 App context (`_Layout.cshtml`, `<script type="application/json" id="app-context">`)

```json
{ "tenantId": "…", "userId": "…", "role": "Merchant",
  "policies": { "floor": true, "business": true, "configure": false }, "moneyVisible": true,
  "uiMode": "simple", "previewSimple": false, "textScale": "large", "gridEnter": "down",
  "currency": { "code": "IRT", "label": "تومان", "scale": 0 }, "moneyScale": 0,
  "terms": { "bale": "نگله", "bales": "نگله‌ها", "baleOf": "نگلهٔ", "baleFooter": "نگله شماره" },
  "today": "2026-09-06", "todayJalali": "1405/06/15",
  "commands": [ { "label": "ثبت فرش تازه", "href": "/Intake", "icon": "plus" } ] }
```

- **`today`** is the Gregorian ISO date in Iran time, safe for `DateOnly` and `new Date()`. **`todayJalali`** is for display.
- **With `BaleTerm = Adl`:** `bale` = «عدل», `bales` = «عدل‌ها», `baleOf` = «عدل» (no written kasre), `baleFooter` = «عدل شماره».
- `terms` also feeds the document catalogue (`terms.*`, E.3), grid column labels and the preset's category names.

---

## J. Prerequisite fixes

### J.1 Must land first (P0, before any new migration)

**1. Migration chain repair** (B.15, first row)
- Before the first `MigrateAsync`, read `__EFMigrationsHistory` (read-only) in every environment (local and CapRover) and record it.
- Verify on an empty DB, on a copy of the local DB and on a history-only copy of production.

**2. Re-enable migrations through a startup pipeline**
- **`App/Abstractions/IStartupTask`** `{ int Order; Task RunAsync(IServiceProvider, CancellationToken); }`.
- **`Program.MigrateAndSeedWithRetryAsync`:**
  1. take `pg_advisory_lock(hashtext('rugs:startup'))` on a dedicated connection;
  2. run `Migrator`;
  3. run `DatabaseSeeder`;
  4. run every registered `IStartupTask` in `Order`;
  5. unlock.
- **Later phases never edit `Program.cs`:**
  - P1 registers `TenantSeedRunner` (Order 100) from `AddPlatformModule()`.
  - P2 registers `LedgerStartupCheck` (Order 200) from `AddLedgerModule()`.
- **`Infra/Persistence/Seed/Migrator.cs`:**
  - runs only `if (config.GetValue("Database:MigrateOnStartup", true))`;
  - logs pending migrations;
  - throws **`MigrationBackupRequiredException : StartupAbortException`** for a pending `[RequiresBackup(table)]` migration when that table has rows and `Database:ConfirmedBackupFor` ≠ its id;
  - otherwise calls `MigrateAsync`.
- **EF 10 behaviour (documented):**
  - `MigrateAsync` takes EF's own migration lock;
  - it throws on pending model changes; `Migrator` wraps that as a `StartupAbortException`.
  
  Our advisory lock exists for seeding and the startup tasks, which EF's lock does not cover.
- **Retry loop.** The existing loop (`Program.cs:175-192`, 10 × 3 s) becomes:

  ```csharp
  catch (Exception ex) when (attempt < maxAttempts && ex is not StartupAbortException)
  ```

  A backup refusal or a pending model change therefore crashes immediately with a critical log, and is never retried.
- **Cleanup:**
  - the seeder's commented-out migrate line is removed;
  - README:63, DEPLOY:73 and the Program.cs comments are fixed;
  - `scripts/db-backup.ps1|.sh` (pg_dump) is added.

**3. CI database job** (`.github/workflows/ci.yml`)
- `services: postgres:17`.
- `.config/dotnet-tools.json` pins `dotnet-ef` 10.0.10; CI runs `dotnet tool restore`.
- `dotnet ef database update` on an empty DB, then down to `20260603141744_CatalogPatternsLabels` and up again (B.15).
- `dotnet ef migrations has-pending-model-changes`.
- `dotnet ef migrations script --idempotent -o migrate.sql`, and **fail if `migrate.sql` contains `"xmin"` DDL**.
- `tests/RugsManagment.IntegrationTests` (env `RUGS_TEST_PG`); the suite **fails** when `CI=true` and the variable is absent.

**4. Session revalidation** (M0)
- `User.SecurityStamp` plus `SecurityStampValidator` (A.1), which also checks `Tenant.IsActive` and `SubscriptionExpiresAt`.
- Login adds `sstamp`.
- Role, password and IsActive changes rotate the stamp.
- The user's own password change re-signs the current cookie with the fresh stamp (`AuthResponse` carries it; `ToClaims(UserDto, securityStamp)`), so only other sessions end (A.1).
- `UsersController.Edit` no longer forces `IsActive = true`.

**5. Cross-tenant defects**
- `RugBatchService.AssignRugsAsync`: scoped lookup.
- `BulkUpdateFieldsAsync`: BatchId ownership check (400, not an FK 500).
- Move `ProcessStepTypeLookup` out of `JwtTokenGenerator.cs` into `Infra/Persistence/Repositories/ProcessStepTypeLookup.cs`, scoped to system ∪ tenant.
- `ServiceProviderId` tenant check in `WorkflowEngine` advance and custom steps.
- `LabelsApiController` writes become TenantAdmin (later Configure).
- `TenantSettingsService.DefaultWorkflowTemplateId` ownership check.
- 403 → 404 in `CustomFieldService.cs:77-78`, `UserManagementService.cs:69-70` and `LabelTemplateService.cs:62-65`.

**6. Cost-engine bugs that would corrupt totals**
- `CostRuleEditor.toPayload` sends null model and rate when untouched, and pre-fills from the step.
- `ApplyStepPricing` no longer clears `ManualCostOverride` when only a model is sent.
- `BulkAdvance` gains provider-rate injection and audit, matching single advance.

**7. Interim SKU retry.** Call `IUnitOfWork.Reset()` (→ `ChangeTracker.Clear()`) before each retry in `RugManagementService.cs:81-95`. `TenantCounters` replaces it in P2.

**8. Re-apply the lost cleanup**
- JWT dead code (`AuthService.cs:42, 88-92`, `JwtTokenGenerator`, `JwtSettings`, the `System.IdentityModel.Tokens.Jwt` package, the `Jwt` appsettings section), after item 5 has moved the lookup.
- Remove FluentValidation and `@vueuse/core`.
- `AsSplitQuery()` on `GetWithWorkflowAsync` and the other multi-collection loads.
- `sw.js` cache-version constant.
- PNG PWA icons 192 and 512 plus maskable.
- Manifest `orientation: any`.
- `npm audit fix` (postcss).
- Untrack `Web/App_Data/keys/*.xml` (`.gitignore`; the user runs `git rm --cached`) and rotate the keys.
- Align the docker-compose password.
- Refresh the README.

**9. Legacy hosts.** Delete `src/RugsManagment.Api`, `client/` and the stray `RugsManagment_Service/`, **only with the owner's explicit approval** at the P0 review. `_design_ref*` stay until he decides.

### J.2 Lands in P1 (the platform features depend on it)

**Merchant role and the sweep of every `[Authorize]` site**

| Site | New policy |
|---|---|
| `ApiControllerBase` | Floor |
| Dashboard, Groups, Labels (class), Media, Rugs (class), Workflows (class) | Floor |
| **Reports** | **Business** |
| Rugs `:83/:89/:113` (trash, delete, restore), Sales (class), **`SalesApiController` (whole class)**, **`AnalyticsApiController.Get`** (`Aging` stays Floor), Providers, `ProvidersApi` | Business |
| Settings, Users, `CustomFieldsApi`, `StepTypesApi :22/27/32`, `WorkflowsApi :22/27/32`, `Workflows :19/22`, `Labels :19/62`, `LabelsApi` writes | Configure |
| `HomeController.Index` | `[Authorize]` (action only). SystemAdmin redirect kept. `Error` and `StatusCodeError` are `[AllowAnonymous]`. |
| Admin | SystemAdmin (unchanged) |

**Other P1 items**
- **Money visibility** (A.5): `IMoneyVisibility`, `MoneyRedaction`, the services, the sort guard, the Dashboard (`:55,68,124,171`; every `DashboardStatsDto` money member nullable), Rugs Index and Details views, the `RugSalePanel` render guard, `RugWorkflowStepDto` money members (nullable `EffectiveCost`), `ProcessStepTypeDto.DefaultUnitRate` and provider rates, the 403 on money members in non-Business requests, and money hidden in `RugWorkflowPanel` and `CostRuleEditor`.
- **`DomainExceptionPageFilter`** for MVC actions (I.1), registered globally in `Program.cs` (WP1-A).
- **Role labels** in one place, `Web/Support/RoleLabels.cs` plus `App/Common/RoleLabels.cs`. They replace `UserManagementService.cs:107-108`, **`AccountController.cs:161-167` (WP1-A)**, `Users/Index.cshtml:4-9` and `Users/Form.cshtml:40-41`. `EnsureTenantRole` accepts Merchant.
- **Status labels** in one place: `DisplayHelpers` (WP1-A: `Rugs/Index.cshtml:11-19`, `Dashboard/Index.cshtml:8-16`) plus `CA/lib/labels.ts` (**WP1-B**: `labelFields.ts:46`, `GroupDetail.vue:35`, `RugBulkToolbar.vue:16-21`). `Exported` shows as «صادر شده», tertiary chip, icon `truck`. **`Draft` becomes «ثبت‌شده»** (never «پیش‌نویس», which is reserved for shipments and outputs); `InProgress` shows «در کارگاه» in Full mode only; `Sold` stays «فروخته‌شده».
- **RugBatch is «ورودی»** (glossary, A.2): WP1-A renames the Groups views (`Groups/Index.cshtml:10` «گروه‌ها / محموله‌ها» → «ورودی‌ها», the placeholder `:20` → «ورودی ۱۴۰۵/۰۶/۱۵ · حاج رضا») and the summaries in `BatchesApiController` and `GroupsController`; WP1-B renames `GroupDetail.vue:113,118,168`, the nav entries and `RugBulkToolbar.vue:294` («افزودن به گروه» → «افزودن به ورودی»). The word «محموله» disappears from every RugBatch screen.
- **Simple-mode bulk bar.** `RugBulkToolbar.vue` hides «افزودن به ورودی», workflow bulk-advance and bulk status change in Simple mode, and gains a `[data-bulk-action]` **extension slot**: `CA/lib/bulkActions.ts` registers every `CA/bulk-actions/*.action.ts` through `import.meta.glob`, so P4b («افزودن به نگله») and P5 («ثبت هزینه برای این‌ها») add files instead of editing the toolbar.
- **Glossary test** (L.1): `.cshtml` and `.vue` files outside the shipment and document features must not contain «محموله», and none may use «پیش‌نویس» outside shipment/output contexts; exceptions are listed in the test.
- **Tailwind sources.** Tailwind v4 scans only ClientApp and `Views/**/*.cshtml`, so classes returned from C# are never generated. WP1-B adds to `app.css`: `@source '../../../Support/**/*.cs'; @source '../../../Navigation/**/*.cs'; @source '../../../Home/**/*.cs'; @source '../../../Shipments/**/*.cs'; @source '../../../RugDetails/**/*.cs';` (the folders where `DisplayHelpers`, `NavBuilder`, `IHomeCard`, `IShipmentTab` and `IRugDetailPanel` implementations live). An architecture test forbids Tailwind-looking class strings in `Web/**/*.cs` outside those folders.
- **Exceptions:** `DomainConflictException`, `AppValidationException` and the filter arms (I.1).
- **Tenant currency select:** whitelisted to Toman and Rial.
- **`offlineQueue.ts`** is scoped by tenant and user.
- **UI shell:**
  - `ui.blockReload`;
  - `ui.openPanel` plus the `site.js` `[data-open-panel]` delegate;
  - the skip-link class is defined;
  - the TempData toast gets a level (`success|error|warning`).
- **`api.ts`** keeps `errors`, adds `postForm` (multipart) and `download` (blob → `a[download]`), and `del` takes a body.
- **Self-hosted fonts.**
- **`ActivityLogService`** pages in SQL.
- **The full icon list is completed in `icons.svg` in P1.** Reused as they are: `printer`, `download`, `receipt`, `undo`, `qr`, `plus`, `package` (stays the ورودی icon). Added:
  - home, more, help;
  - truck, bale (a strapped bundle, distinct from `package`), move, scan, ruler, calculator;
  - upload, spreadsheet;
  - lock, unlock, split;
  - coins, dollar, copy, eye, grip;
  - chevron-left, chevron-right;
  - keyboard, wand (preset), sliders;
  - cost-category icons: droplet (شستشو), needle (رفوگری), ship (کرایهٔ خارجی), stamp (گمرک و ترخیص), warehouse (انبارداری), tape (بسته‌بندی), dots (سایر).
  
  A test asserts that every `Icon` used by a seed or the preset exists in the sprite (H.8).
- **Text-size control** (A.3) in the user menu and the HelpDrawer.
- **`ISettingsLink`** seam: the settings hub renders every registered link, so P3, P4a, P5 and P7 add a link file instead of editing the hub or `SettingsNavContributor`.

### J.3 Can wait (tracked)

| Item | When |
|---|---|
| Secrets in appsettings and the `postgres/postgres` fallback | Before go-live (independent PR) |
| Seeded default credentials outside Development | P9 (behaviour of the `Seed:Demo` key, which is declared in P1) |
| `ForwardedHeaders`, `AllowedHosts`, root container, HEALTHCHECK | P9 |
| `CsvWriter` negative numbers | P6 |
| Label `v-html` escaping | P9 (the new engine never uses `v-html`) |
| Sales date-range mismatch | P5 |
| User admin (last admin, self-demotion, password policy) | P1 (alongside roles) |
| Template-path pricing / dead template overrides | Outside the merchant path; P9 if time allows |
| Analytics full-tenant loads | P9 |

---

## K. Phased plan, file ownership and parallelisation

### K.1 Ground rules

- **Approval and commits.** Every phase ends with a demo in the running app and the owner's approval. **The owner makes every commit.** Persian test data goes through the browser `fetch` path only, never through Git Bash.
- **Usability checkpoints.** The P3, P4b and P5 acceptance each include a timed task with the father, or a stand-in over 60 who does not use Excel daily, on the warehouse tablet and on the desktop (P3: intake of 10 rugs including the lot «3911 - 3960»; P4b: baling them into 2 bales with final sizes; P5: two costs, freight and washing). The time, the errors and every hesitation go into the phase notes. **The phase fails if he needs help more than once.**
- **Shared-file ownership.** A shared file is edited only by its owner phase. Several owners are listed only when those phases run in sequence, either in one lane or across a dependency edge.

| Shared file | Owner(s), in order |
|---|---|
| `Infra/Persistence/AppDbContext.cs` | **never edited** |
| `Web/Program.cs` | P0 (WP0-A: Migrator, `IStartupTask` runner, `StartupAbortException` filter, cookie `Events` → `SecurityStampValidator`) → P1 (policies, `heavy` limiter, global `DomainExceptionPageFilter`). **Frozen.** |
| `appsettings.json`, `appsettings.Development.json` | P0 (WP0-A: `Database:*`, `Jwt` removal) → P1 (`Storage:DataRoot`, `Seed:Demo` default false / Development true). **Frozen.** |
| `App/DependencyInjection.cs`, `Infra/DependencyInjection.cs` | P0 (WP0-A: JWT removal, `ProcessStepTypeLookup`, `TenantCounterRepository`) → P1 (module calls `AddPlatformModule()`, `AddLedgerModule()`, `AddIntakeModule()`, `AddShipmentsModule()`, `AddCostsModule()`, `AddDocumentsModule()`, `AddImportModule()` plus empty module files). **Frozen.** |
| `Infrastructure.csproj` | P0 (WP0-A: JWT package removed) → P6-wiring (ClosedXML, OpenXml) |
| `Application.csproj` | P0 (WP0-A: FluentValidation removed; `<ItemGroup><EmbeddedResource Include="Features\Documents\Defaults\*.json" /></ItemGroup>` added in advance for P6/P7). **Frozen.** |
| `tests/RugsManagment.Tests/*.csproj`, `tests/RugsManagment.IntegrationTests/*.csproj` | P0 (WP0-A: `<None Include="..\fixtures\*.json" CopyToOutputDirectory="PreserveNewest" />` for `math-vectors.json`, `sample-shipment.json` and later fixtures; IntegrationTests references `Microsoft.AspNetCore.Mvc.Testing` and the Web project). **Frozen.** `tests/RugsManagment.ArchitectureTests/*.csproj`: P2 (WP2-B) only. |
| `Domain/Entities/User.cs` | P0 (WP0-A: SecurityStamp) → P1 |
| `Infra/Persistence/Configurations/EntityConfigurations.cs` | P0 (WP0-A: SecurityStamp inside the existing User block) → P1 (User, AuditEntry, CustomFieldDefinition) → P2 (Rug, RugBatch, RugSale, RugWorkflowStep) → P9 (PurchaseCost removal) |
| `Domain/Entities/Rug.cs` | P2 (all fields) → P9 |
| `Infra/Persistence/UnitOfWork.cs`, `App/Abstractions/IUnitOfWork.cs` | P0 (WP0-B: `Reset`) → P2 (WP2-B: the pass and `ExecuteInTransactionAsync`). **Frozen.** |
| `Infra/Persistence/Seed/DatabaseSeeder.cs` | P0 (WP0-A) → P2 (rebuild through the pass) → P9 (`Seed:Demo`) |
| `Views/Shared/_Layout.cshtml`, `CA/islands/registry.ts`, `CA/styles/app.css` (with the C# `@source` lines, J.2), `wwwroot/icons.svg` (full list, J.2), `wwwroot/fonts/*`, `wwwroot/js/site.js`, `CA/lib/ui.ts`, `CA/lib/bulkActions.ts`, `CommandPalette.vue`, `UiShell.vue`, `OfflineIndicator.vue` | P1 only |
| `CA/islands/RugBulkToolbar.vue` | P1 (WP1-B: labels, Simple-mode hiding, `[data-bulk-action]` slot). **Frozen.** P4b and P5 add `CA/bulk-actions/*.action.ts` files. |
| `CA/components/QrScanner.vue` | P2 (WP2-C: `code_128`, exact matching; still navigates). P4b's scanning uses its own `components/scan/BarcodeScan.vue`. |
| `wwwroot/sw.js`, `wwwroot/manifest.webmanifest` | P0 only. P0 already lists `/fonts/` in the cache-first list, so P1's self-hosted fonts need no edit. |
| `App/Common/Messages.fa.cs` (partial class), `CA/lib/messages/index.ts` | P1 (core and platform codes). Later phases add `Messages.fa.{Feature}.cs` and `CA/lib/messages/{feature}.ts` partials (H.3b). |
| `AuditAction.cs`, `ActivityLogService.cs` labels, `Views/Settings/Activity.cshtml` | P1 (all values at once, B.14) |
| `RugStatus.cs`, `UserRole.cs`, `CurrencyUnit.cs`, status and role label sites (J.2) | P1 |
| `SpecializedRepositories.cs` | P0 (AsSplitQuery) → P2 (extracts `RugRepository.cs`, `ServiceProviderRepository.cs`, `RugBatchRepository.cs`). Afterwards `RugRepository.cs`: P2 → P4a; `ServiceProviderRepository.cs`: P2 → P5. |
| `RugManagementService.cs` | P0 → P1 (money redaction) → P2. **Frozen.** Locks and guards live in the pass. |
| `WorkflowEngine.cs` | P0 → P2. **Frozen.** |
| `EntityMappers.cs`, `App/Services/WorkflowManagementService.cs` (which contains `IDashboardService`/`DashboardService`, `:154-170`; never extracted in parallel) | P1 (redaction, nullable `Costs`) → P2 (WP2-B). **Frozen.** |
| `RugSaleService.cs`, `AnalyticsService.cs` | P2 |
| `App/DTOs/Rugs/RugListDtos.cs` (incl. `RugQuery`), `RugDtos.cs`, `Web/Models/Rugs/RugListViewModel.cs` | P1 (nullable money) → P2 (WP2-C contracts first: every new `RugQuery` property, `HasActiveFilters` terms, link round-trip, appended `RugDto` members; the `RugListItemDto` appends go with `RugRepository.Project` in WP2-B). **Frozen.** |
| `App/Abstractions/Persistence/IRugRepository.cs` | P2 (WP2-C writes the new signatures in its contracts merge; WP2-B implements them) → P4a (filter members). |
| `IProcessStepTypeLookup` (declared in `App/Services/WorkflowEngine.cs:440`) | Stays where it is; WP0-B moves only the implementation to `Infra/Persistence/Repositories/ProcessStepTypeLookup.cs`. |
| `App/Ledger/*` (pass core, `LedgerStageOrder`, `ICostSourceProvider`), `Infra/Persistence/Ledger/{LedgerRepository, CostSql}.cs` | P2. Later phases add **new** contributor, guard and stage files and their own repositories. |
| `Views/Rugs/Details.cshtml` | P1 (money guards) → P2 (partials, `IRugDetailPanel` slot) → P9 |
| `Views/Rugs/Index.cshtml` | P1 (status labels, money guards) → P2 → P3 → P4b → P9 |
| `Views/Dashboard/Index.cshtml` | P1. **Frozen.** |
| `RugWorkflowPanel.vue`, `CostRuleEditor.vue`, `RugSalePanel.vue` | P0 (payload) → P1 (money props) → P9 (Jalali, modal) |
| `Views/Home/Index.cshtml` | P1 (renders `IHomeCard`s). Later phases add cards only. |
| `Web/Controllers/ShipmentsController.cs`, `Views/Shipments/{Index, Workspace}.cshtml`, `Web/Shipments/{IShipmentTab, ShipmentTabsViewComponent}.cs` | **P4a** (shells; E-Core owns the seam) → P4b (`Index` island tag only). P5 and P6 add `IShipmentTab` implementations as new files. |
| `CA/lib/types.ts` | P1 → becomes `lib/types/index.ts` + `legacy.ts`. `index.ts` is **frozen as legacy re-exports only**; new code imports `@/lib/types/{feature}` directly, and phases own `lib/types/{feature}.ts`. |
| Tenant-isolation test samples (L.2) | Each action carries `[IsolationSample(...)]` (or a per-phase `Samples/{Phase}.cs` partial), so no phase edits a shared sample table. |
| `App/Seed/DocumentTemplateSeedContributor.cs` | P6 (seeds every embedded `Defaults/*.v*.json`). P7 adds JSON files only. |
| Migrations folder and snapshot | The phase's migration owner (B.15 order, regenerate rule) |

**Seams created early so later phases add files instead of editing**

| Phase | Seams |
|---|---|
| P0 | `IStartupTask`; the `Defaults/*.json` embedded-resource glob; the test fixtures copy glob |
| P1 | `INavContributor`, `IHomeCard`, `ITenantSeedContributor`, `ISettingsLink`, panel host (`panels/*.panel.vue`), island glob, bulk-action slot (`bulk-actions/*.action.ts`), `Messages.fa` partials, module DI files, `DomainConflictException`, `AppValidationException`, `DomainExceptionPageFilter`, `[IsolationSample]` |
| P2 | `IFieldCatalogContributor`, `IRugDetailPanel`, `IRugDtoEnricher`, `IChangeImpactContributor`, `ILedgerGuard`, `ILedgerStage` + `LedgerStageOrder`, `ICostSourceProvider`, `IUnitOfWork.ExecuteInTransactionAsync` |
| P3 | `IGridSource` and the Grid DTOs |
| P4a | `IShipmentTab` |

**Dependency graph and lanes**

```
P0 → P1 → P2 ─┬→ E-Grid:  P3 (grid core, intake; IGridSource intake|rugs) ─────────────┐
              │                                                                        ├→ P4b (baling UI + BalesGridSource) → P8 (import, M6) ─┐
              ├→ E-Core:  P4a (shipment domain M3 + workspace shell) ──────────────────┤                                                       │
              │                  └→ P5 (costs M4; CostsTab/SummaryTab into the P4a shell) ─┐                                                   ├→ P9
              └→ E-Docs:  P6-engine (pure, fixtures) ··· P6-wiring (M5; needs P4a + P5) ───┴→ P7 (designer, preset, cost template) ───────────┘
Edges: P4b ← P3 + P4a.  P5 ← P4a.  P6-wiring ← P4a (M3) + P5 (M4 merges before M5).  P7 ← P3 (GridLayoutService for the preset) + P5 + P6-wiring.
       P8 ← P3, P4a, P4b, P6-wiring (acceptance export).
Migration merge order: M3 (P4a) → M4 (P5) → M5 (P6-wiring) → M6 (P8); otherwise the regenerate rule (B.15) applies.
```

### K.2 Phases and work packages

#### P0: repair and hygiene (2 engineers)

**WP0-A (DB, CI, startup, shared configuration). It owns every file that both packages need.**
- **Migrations and startup:**
  - `Infra/Persistence/Migrations/20260604082452_PendingModelSync.cs`, `20260705115025_SyncModelState.cs`, new M0
  - `Infra/Persistence/Seed/Migrator.cs` (new), `App/Abstractions/{IStartupTask, StartupAbortException}.cs` (new), `MigrationBackupRequiredException`, `RequiresBackupAttribute`
  - `DatabaseSeeder.cs`
  - `Web/Program.cs` (including wiring the cookie `Events` to WP0-B's validator)
- **Shared configuration:**
  - `appsettings.json`, `appsettings.Development.json`
  - both `DependencyInjection.cs`, both `*.csproj` (applying WP0-B's registration and package checklist; `Application.csproj` also gets the `Defaults/*.json` `EmbeddedResource` item, K.1)
- **Data model:**
  - `Domain/Entities/{User (SecurityStamp), TenantCounter}.cs`
  - `EntityConfigurations.cs` (SecurityStamp inside the existing User block)
  - `Infra/Persistence/Configurations/PlatformConfigurations.cs` (TenantCounter only)
  - `Infra/Persistence/Repositories/TenantCounterRepository.cs` + `App/Abstractions/Persistence/ITenantCounters.cs`
- **CI and tooling:**
  - `.github/workflows/ci.yml`, `.config/dotnet-tools.json`
  - `tests/RugsManagment.IntegrationTests/*` (new project; xUnit + Npgsql + `Microsoft.AspNetCore.Mvc.Testing`, referencing the Web project): `WebApplicationFactory<Program>`; a test authentication handler registered via `ConfigureTestServices` that issues role and tenant claims (cookie login is used only by the SecurityStamp tests, because the `login` rate limiter allows 10 per 5 minutes and every TestServer request shares the `unknown` partition); antiforgery tokens fetched from a GET before POSTs; the test csproj fixture-copy item (K.1)
  - `RugsManagment_Service.slnx`
  - `scripts/db-backup.*`
  - `README.md`, `DEPLOY.md`, `docker-compose.yml`

**WP0-B (security, cross-tenant, cleanup).** It delivers code files and hands a registration and package checklist to WP0-A.
- **Security:**
  - `Web/Auth/SecurityStampValidator.cs` (new; checks user, tenant and subscription)
  - `ClaimsPrincipalExtensions.cs` (`ToClaims(UserDto, string securityStamp)`), `AccountController.cs` (`sstamp` on Login and on the Profile re-sign), `AuthService.cs` / `AuthResponse` (carries the stamp), `UsersController.cs`, `UserManagementService.cs`, `ProfileService.cs` (rotate on password; evict the validator cache)
- **Cross-tenant fixes:** `RugBatchService.cs`, `RugManagementService.cs`, `WorkflowEngine.cs`, `CustomFieldService.cs`, `LabelTemplateService.cs`, `TenantSettingsService.cs`, `Api/LabelsApiController.cs`.
- **JWT removal:** `Infra/Identity/JwtTokenGenerator.cs` is deleted and its lookup moves to `Infra/Persistence/Repositories/ProcessStepTypeLookup.cs`; `AuthService.cs`.
- **Persistence:** `SpecializedRepositories.cs` (AsSplitQuery); `UnitOfWork.cs`/`IUnitOfWork.cs` (`Reset`).
- **Frontend and PWA:** `CA/components/CostRuleEditor.vue`, `CA/islands/RugWorkflowPanel.vue` (payload), `package.json`, `wwwroot/sw.js` (cache version; `/fonts/` already in the cache-first list for P1), `wwwroot/manifest.webmanifest` (icons, `orientation: any`), icons, `.gitignore`.
- **Legacy folders:** deleted, with approval.

**Merge order:** WP0-A first, or both packages merge together with the checklist applied.

**Acceptance**
- An empty Postgres plus `dotnet run` creates the schema and seeds it. A restart is idempotent. Two instances do not race.
- A pending `[RequiresBackup]` migration on a populated table aborts at once, without the 10 × 3 s retries.
- CI is green, including the DB job, `has-pending-model-changes` and the xmin-DDL check.
- Cross-tenant regression tests return 404.
- Demoting or disabling a logged-in user, deactivating the tenant, or letting the subscription expire takes effect within 5 minutes. Changing your own password keeps the current session and ends a second one.
- `database update` up, down to `20260603141744_CatalogPatternsLabels`, and up again succeeds (B.15).
- Completing a step without typing a rate keeps the provider or default rate.
- 50 parallel `NextBlockAsync` calls produce no duplicates.
- `dotnet build -warnaserror` and `npm run build` pass.

#### P1: platform: roles, modes, preferences, shell, UI kit, money visibility (2 engineers); migration M1

**WP1-A (backend and Razor)**

- **Domain:**
  - `Enums/{UserRole, RugStatus, CurrencyUnit, AuditAction}.cs` (append, explicit values)
  - `Enums/{UiMode, TextScale, GridEnterDirection, BaleTerm, SerialMode}.cs`
  - `Entities/{User, TenantPreferences, TenantGridLayout, AuditEntry, CustomFieldDefinition}.cs`
  - `Common/{Num, AreaMath, DomainValidationException, DomainConflictException}.cs`
- **Application:**
  - `Features/Platform/{PlatformModule, TenantPreferencesService, UserPreferencesService}.cs`, `DTOs/Platform/*.cs`
  - `Common/{AppValidationException, RoleLabels, IMoneyVisibility, MoneyRedaction, Messages.fa, NumberWords}.cs`, `Abstractions/ITenantSeedContributor.cs`
  - `WorkflowManagementService.cs` (step-type rate redaction), `ServiceProviderService.cs` (rate redaction), the 403 on money members in non-Business requests (A.5)
  - `ActivityLogService.cs` (labels + SQL paging), `UserManagementService.cs` (Merchant, UiMode)
  - `RugManagementService.cs`, `DashboardService`, `EntityMappers.cs` (redaction, nullable `Costs`, sort guard)
  - `DTOs/Rugs/{RugDtos, RugListDtos}.cs` (nullable money only)
  - empty `Features/*/…Module.cs` (App and Infra) for Intake, Shipments, Costs, Documents, Import and Ledger
- **Infrastructure:**
  - `EntityConfigurations.cs` (User, AuditEntry, CustomFieldDefinition), `Configurations/PlatformConfigurations.cs` (+TenantPreferences, TenantGridLayout)
  - `Seed/TenantSeedRunner.cs` (an `IStartupTask`, Order 100, registered in `AddPlatformModule()`; also called from `TenantManagementService.CreateAsync`)
  - `TenantManagementService.cs`, `AuditLog.cs` (DetailsJson), migration M1
- **Web:**
  - `Program.cs` (policies, `heavy` limiter, global `DomainExceptionPageFilter`), `Auth/Policies.cs`, `Filters/DomainExceptionPageFilter.cs`
  - `Support/{UserPreferencesAccessor, RoleLabels, DisplayHelpers, PersianFormat (jdot, usd)}.cs`, `TenantSettingsAccessor.cs`
  - every controller in the J.2 sweep (including `ReportsController` → Business, `AnalyticsApiController.Get` and `SalesApiController` → Business, and `HomeController`)
  - `AccountController.cs` (role labels)
  - `Api/{ApiControllerBase (+exception arms), MeApiController, SettingsPreferencesApiController}.cs`
  - `Controllers/{MoreController, HelpController}.cs` (controllers only; their views are WP1-B's), `Controllers/Settings/{SettingsHubController, PreferencesController}.cs` + views, `Web/Settings/{ISettingsLink, CoreSettingsLinks}.cs`
  - `Views/Users/{Index, Form}.cshtml`, `Views/Settings/{Activity, Tenant}.cshtml` (currency whitelist)
  - `Views/Groups/*.cshtml`, `GroupsController.cs`, `Api/BatchesApiController.cs` summaries («ورودی» relabel, J.2)
  - `Views/Rugs/{Index, Details}.cshtml` and `Views/Dashboard/Index.cshtml` (status labels and money guards only)
  - `appsettings*.json` (`Storage:DataRoot`, `Seed:Demo`)
- **Tests:** `tests/fixtures/sample-shipment.json` (E.10; the 161 sample lines), the `Num`/`AreaMath` tests that read it, the glossary test.

**WP1-B (frontend kit and shell)**
- **Layout and server-side shell:**
  - `_Layout.cshtml` (NavBuilder, app-context, `data-ui`/`data-text-scale`, preview toggle)
  - `Web/Navigation/{INavContributor, NavItem, NavBuilder, CoreNavContributor, SettingsNavContributor}.cs`
  - `Web/Home/{IHomeCard.cs, Cards/TilesCard.cs, Cards/StockCard.cs}`
  - `Views/Home/Index.cshtml`, `Views/More/Index.cshtml`, `Views/Help/Card.cshtml`
- **Client libraries:**
  - `CA/islands/registry.ts` (glob + legacy)
  - `CA/lib/{context, api, format, money, num, numWords, jalali, ids, ui (openPanel host), offlineQueue, labels, help, bulkActions}.ts`, `CA/lib/messages/index.ts`, `CA/lib/types/{index, legacy, platform}.ts`
  - `wwwroot/js/site.js` (`[data-open-panel]` delegate, toast levels)
- **Components and shell:**
  - `CA/components/ui/{NumberField (words line, suffixes, Rial toggle), JalaliDateField, Modal, Drawer, Field, Button, Combobox, EmptyState, HelpDrawer (text-size control), Stepper}.vue`
  - `CommandPalette.vue`, `UiShell.vue` (panel host), `OfflineIndicator.vue`
  - `styles/app.css` (+ the C# `@source` lines), `wwwroot/icons.svg` (full list), `wwwroot/fonts/*`
- **Label sites:** `labelFields.ts:46`, `GroupDetail.vue:35,113,118,168`, `RugBulkToolbar.vue:16-21,294` (plus its Simple-mode hiding and the `[data-bulk-action]` slot).
- **Money props:** `RugWorkflowPanel.vue` and `CostRuleEditor.vue` (hidden when not Business).
- **Islands:** `islands/platform/HomeCoach.island.vue`, `islands/settings/PreferencesForm.island.vue`.
- **Testing:** `package.json` (+vitest), `vitest.config.ts`, `lib/*.spec.ts`.

**Acceptance**
- A Merchant sees 5 nav items.
- **MVC** `/Settings/*`, `/settings/*` and `/Users` → **302 to `/account/denied`**. **API** `/api/settings/*` → **403**.
- **Operator:**
  - nav as in A.4;
  - **no Toman money in any page or JSON**: Dashboard, Rugs list and details, `/api/rugs`, `/api/home/summary` (auth-matrix test);
  - `/Reports` → 302 denied.
- **Home route:**
  - `/` signed out → login; SystemAdmin → `/Admin`;
  - `/status/404` and `/Home/Error` work anonymously.
- TenantAdmin «نمای ساده» shows the merchant nav without changing permissions.
- The text scale persists.
- Every pre-existing URL returns 200 for the right role.
- A Rial tenant shows «ریال» in islands. The tenant currency select offers only Toman and Rial.
- `NumberField` parses «۲/۵», «۲٫۵» and «2.5».
- Jalali 1405/06/15 ↔ 2026-09-06.
- A Razor `[data-open-panel]` button opens a stub panel under CSP with a clean console.
- Dark mode is checked on all new chrome. The «صادر شده» chip renders styled (its classes come from `DisplayHelpers.cs`, now a Tailwind source).
- **Operator money:** `/api/analytics` and `/api/sales/rug/{id}` → 403; `/api/rugs/{id}` steps and `/api/lookups/step-types` carry no amounts; a rug PUT, bulk-fields PUT or step-pricing PUT with money members → 403.
- Trashing a baled rug through `POST /Rugs/Delete` gives 302 with the error toast, not 500 (after P4a; the filter lands here).
- Groups pages read «ورودی‌ها»; a new rug shows «ثبت‌شده», never «پیش‌نویس»; the glossary test passes.
- The text-size control changes the root size at once and persists.

#### P2: rug model v2, the Consistency Pass, cost unification (3 engineers); migration M2

**WP2-A (domain + persistence + migration)**
- `Domain/Entities/{Rug, RugBatch, RugSale, RugWorkflowStep, RugPurchaseLine, CostCategory, RugCostTotal}.cs`
- `Domain/Enums/{CostGroup, CostBasis, AreaBasis, AllocationMethod, RugLocation}.cs`
- `Domain/Common/{SerialNumber, Dimension}.cs`, `Domain/Costing/RugCostMath.cs`
- `EntityConfigurations.cs` (Rug, RugBatch, RugSale, RugWorkflowStep), `Configurations/CostingConfigurations.cs`
- Migration M2 (`[RequiresBackup("Rugs")]`, backup table, self-verify)

**WP2-B (ledger pass + cost unification)**
- **Pass core:** `App/Ledger/{ChangeSet, LedgerImpact, LedgerContext, IChangeImpactContributor, ILedgerGuard, ILedgerStage, LedgerStageOrder, ILedgerPass, ILedgerSession, ILedgerRepository, ICostSourceProvider (+NoCostSources), LedgerPass, PurchaseMath, LedgerVerifier, ILedgerMaintenance}.cs`
- **Contributors:** `App/Ledger/Contributors/{RugImpactContributor, PurchaseLineImpactContributor, StepImpactContributor, SaleImpactContributor}.cs`
- **Guards:** `App/Ledger/Guards/PreferencesLockGuard.cs`
- **Stages:** `App/Ledger/Stages/{SerialOverlapStage, PurchaseLineStage, PurchaseRollupStage, RugCostTotalStage}.cs`
- **Infrastructure:**
  - `Infra/Persistence/Ledger/{ChangeSetBuilder (value diff, D.4), LedgerSession (derived-entity detach), LedgerRepository, CostSql, LedgerStartupCheck (IStartupTask, Order 200, registered in AddLedgerModule())}.cs`
  - `UnitOfWork.cs`, `IUnitOfWork.cs` (two-phase pass, `ExecuteInTransactionAsync`)
  - `App/Abstractions/Persistence/IConcurrencyGuard.cs` + `Infra/Persistence/ConcurrencyGuard.cs`
- **Cost unification:**
  - `WorkflowEngine.cs` (`CalculateRugCosts` reads `RugCostTotal`, step pricing snapshot, resolver), `CostCalculationService.cs`
  - `App/Features/Rugs/{RugStatusResolver, IRugDtoEnricher}.cs`
  - `EntityMappers.cs`, `AnalyticsService.cs` (`ActualProfit` from `CostAtSale ?? Total`), `RugSaleService.cs` (CostAtSale kept on edit, totals), `WorkflowManagementService.cs` (`DashboardService`), `DatabaseSeeder.cs` (rebuild)
- **Repositories:**
  - `Infra/Persistence/Repositories/{RugRepository, ServiceProviderRepository, RugBatchRepository}.cs` are extracted. **RugRepository covers TotalInvestment sites 2–4**, now joining `RugCostTotal` (`AsNoTracking` projections). **ServiceProviderRepository covers sites 5–6** through `CostSql`. Site 1 (`WorkflowEngine`) reads a freshly queried `RugCostTotal`; site 7 (the seeder) goes through the pass.
  - **`RugListItemDto` appends and `RugRepository.Project`** (every new member passed explicitly, I.1), and the implementation of WP2-C's new `IRugRepository` signatures (next-serial, by-serial, values, the SerialNo search and sort).
  - `SpecializedRepositories.cs` keeps the remaining small repositories.
- **`[Obsolete]` on `Rug.PurchaseCost`**: added in WP2-B's last merge, after every usage is gone; the EF mapping is wrapped in `#pragma warning disable CS0618` (B.4).
- **Tests:** `tests/RugsManagment.ArchitectureTests/*` (new; including the bypass rule's whitelist and the `Update()`-on-untracked rule, D.4).

**WP2-C (rug application + web)**
- **Application:**
  - `App/Features/Rugs/{RugFieldWriter, RugSplitService}.cs`
  - `App/Fields/{FieldCatalog, IFieldCatalogContributor, RugFieldContributor, RugMoneyFieldContributor}.cs`
  - `RugManagementService.cs` (writer, counters, no retry loop, the legacy `UpdateRugRequest` rules of I.3)
  - `App/Features/Rugs/SerialSuggester.cs` (next-serial rule, G.4)
  - `App/Seed/CostCategorySeedContributor.cs`: «قیمت خرید» `purchase.price` (PerM2, ShowInIntake), «دلالی» `purchase.broker` (Percent 2, ShowInIntake) and «کرایه تا انبار» `purchase.freightIn` (PerPiece), idempotent by `SeedKey`, so the P3 intake grid's purchase columns work before the P7 preset
- **DTOs and contracts** (merged first, see «Merge order»):
  - `DTOs/Rugs/*.cs`: appended `RugDto` members; **every `RugQuery` property, its `HasActiveFilters` terms and the link round-trip in `RugListViewModel`**
  - `App/Abstractions/Persistence/IRugRepository.cs`: the new signatures (WP2-B implements them)
- **Web:**
  - `Web/Controllers/Api/{RugsApiController, LookupsApiController (fields, values), DiagnosticsApiController}.cs`
  - `Web/RugDetails/{IRugDetailPanel.cs, RugDetailPanelsViewComponent.cs}`
  - `Views/Rugs/{Index, Details, _Details.*, Trash}.cshtml`
  - `Controllers/Settings/LedgerHealthController.cs` + view
- **Frontend:**
  - `CA/islands/RugForm.vue`: serial, qty, intake and final sizes, version, tokens; it **stops sending `purchaseCost`, `targetSalePrice` and W/L** (only `Values` + `Version`) and drops those inputs, embedding `purchase-lines-editor` for Business users instead
  - `islands/rugs/{PurchaseLinesEditor, RugCostBreakdown, RugFinalSize}.island.vue`, `islands/settings/LedgerHealth.island.vue`
  - `components/PurchaseLinesEditor.vue`, `lib/labelFields.ts`, `QrScanner.vue`, `lib/types/rugs.ts`

**Merge order** (the three packages share `RugRepository.cs`, `IRugRepository.cs`, `EntityMappers.cs` and the DTOs):
1. WP2-A: entities, enums, M2.
2. WP2-C contracts: `DTOs/Rugs/*`, `RugQuery`, the `IRugRepository` signatures.
3. WP2-B: `RugRepository` (implementations and the `RugListItemDto` projection), `EntityMappers`, the pass, the stages.
4. The rest of WP2-C: services, web, Vue.
5. WP2-B's last commit: `[Obsolete]` on `Rug.PurchaseCost`.

`RugRepository.cs` and `EntityMappers.cs` are edited only by WP2-B; WP2-C sends it a checklist, as WP0-B does in P0.

**Acceptance**
- M2 on a copy of the dev DB: the self-verify passes, `LengthMeters = COALESCE(FinalLength, IntakeLength)` holds on every row, and `cost-consistency` = 0.
- List, sort, dashboard, sales profit and provider statements show the same numbers as before for unchanged data.
- A lot «3911 - 3960» × 50 at 0.40 × 0.40 shows 8.00 m². A PerM2 purchase line of 45.55 on it gives `ROUND(45.55 × 0.16, 0) = 7` per piece → **350** at MoneyScale 0 (7.29 × 50 = 364.50 at MoneyScale 2). A declared rate of 45.55 USD/m² gives PieceUsd 7.29 and LineUsd 364.50.
- An overlapping «3950-3970» is refused with a Persian 409 (`field: rug.serial`). After «3985 - 4003», next-serial returns 4004.
- Step pricing on a lot follows the D.3 table for every model (vectors with qty 50), half away from zero.
- **One request, fresh numbers:** an intake-size PUT on a rug with a PerM2 purchase line returns the new `costs.totalInvestment` in the same response, and persists it (the two-phase pass and the derived-entity detach, D.4).
- **Legacy round trip:** GET a rug with PerM2 + Percent lines, a lump share and a final size, then PUT the unchanged DTO through the old RugsApi path: purchase lines, `IntakeLength`/`IntakeWidth` and `RugCostTotal` are unchanged. A PUT with only the title changed keeps both lines. A differing `purchaseCost` on a multi-line rug returns 409.
- **Real diffs only:** `rugs.Update(rug)` with only `UpdatedAt` or the notes changed gives an empty impact (unit test).
- An edit through the legacy RugsApi PUT and through `BulkUpdateFields` refreshes `RugCostTotal` (integration test).
- Changing `MoneyScale` or the tenant currency after the first stored money of any kind (D.5 #22) returns 409.
- The architecture tests pass: stage order, `CostSql` as the single formula site, explicit enum values, no writes that bypass the pass (with its whitelist), no `Update()` on untracked ledger entities.
- `dotnet build -warnaserror` passes after the `[Obsolete]` merge.

#### P3: grid and intake (E-Grid, 1–2 engineers); no migration

- **Grid component:** `CA/components/grid/*` (+ specs).
- **Application:**
  - `App/Features/Intake/{IntakeModule, RugGridService, GridLayoutService}.cs`
  - **`App/Features/Intake/Grid/{IGridSource, IntakeGridSource, RugsGridSource}.cs`**
  - `App/Features/Intake/PurchaseFieldContributor.cs` (the `purchase.*` grid fields and the G.2 «Purchase columns» read/write rules in `RugGridService`)
  - `DTOs/Grid/*.cs`, `RugBatchService.cs` (seller, number, the (day, seller) key, today, recent)
- **Infrastructure:** `Infra/Features/Intake/IntakeModule.cs`, `Infra/Persistence/Repositories/IntakeGridRepository.cs`.
- **Web:**
  - `Web/Controllers/IntakeController.cs` + `Views/Intake/Index.cshtml`
  - `Api/{GridApiController (dispatch by gridId), BatchesApiController (today)}.cs`
  - `Controllers/Settings/GridSettingsController.cs` + view, `Api/GridLayoutApiController.cs`, `Web/Settings/GridSettingsLink.cs`
  - `Web/Navigation/IntakeNavContributor.cs`, `Views/Rugs/Index.cshtml` (button)
- **Islands:** `islands/intake/IntakeGrid.island.vue`, `islands/rugs/RugsGrid.island.vue`, `islands/settings/GridColumnsEditor.island.vue`, `lib/types/grid.ts`.

**Acceptance**
- 20 rows typed keyboard-only. Tab and Enter behave like Excel, including the Tab-run return.
- 50 TSV rows pasted from the sample (B–I) with Persian digits; headers skipped; the cm-looking cells of the paste get one confirm.
- Sticky origin, material and seller; serial auto-increment by the G.4 rule (after «3985 - 4003» the next row suggests 4004; a lot row given only qty 50 gets a 50-number range).
- **D.7 #1–#3 typed entirely in the intake grid** (A per metre, B «هر تخته» 300,000, C per metre, «دلالی» 2) give PurchaseCost 40,116,600 / 15,300,000 / 73,174,800.
- A legacy rug's M2 Total line shows as «کل» with its amount, never as a per-metre rate; a rug with two lines of a category shows a read-only «چند سطر» cell; clearing a purchase cell deletes its line.
- Two sellers on the same day make two ورودی batches; changing a row's seller moves only that row; yesterday's ورودی reopens from the «ورودی‌های اخیر» strip.
- Kill the network, type 5 rows, reload: the drafts are offered, restored and saved once (`ClientRowId`). «پاک کردن» needs the danger confirm, and «برگردان» on its toast brings the rows back.
- Two tabs editing different fields auto-merge; the same field shows the conflict UI. Two concurrent purchase-line edits give exactly one 409.
- An Operator never receives purchase keys, and an injected money key returns 403. An Operator can delete a rug he created today with no money on it, and gets 403 for any other.
- «208» triggers the cm confirm; «25» and «30» ask metres or cm; «12» is 12 m; «208×210» fills both. «۱۰م» enters ۱۰٬۰۰۰٬۰۰۰ and the words line reads «ده میلیون تومان».
- A stale grid-layout PUT returns 409.
- Card mode works at 375 px.
- **Usability checkpoint** (K.1): intake of 10 rugs including the lot «3911 - 3960».

#### P4a: shipment domain and workspace shell (E-Core); migration M3

- **Domain:** `Entities/{Shipment, Bale, ShipmentLine, ShipmentLineValue, DeclaredValueRate}.cs`, `Enums/{ShipmentStatus, DeclaredRateSource}.cs`.
- **Application:**
  - `Features/Shipments/{ShipmentsModule, ShipmentService, BaleService, LineService, DeclaredValueService, ReadinessService, FinalizeService, SnapshotBuilder, ShipmentFieldContributor, ShipmentRugDtoEnricher}.cs`
  - `Features/Shipments/Ledger/{ShipmentImpactContributor, DeclaredValueImpactContributor, PreferencesImpactContributor, ShipmentLockGuard, BaleMembershipGuard, KeyFieldGuard, ShipmentLineValueStage}.cs`
  - `Features/Shipments/{ShipmentLockedException, StaleStampException}.cs` (derived from `DomainConflictException`), `Messages.fa.Shipments.cs`
  - (`BalesGridSource` moved to P4b, which depends on both P3 and P4a.)
  - `DTOs/Shipments/*.cs`, `Abstractions/Persistence/IShipmentRepository.cs`
- **Infrastructure:**
  - `Configurations/ShipmentConfigurations.cs`, `Repositories/{ShipmentRepository, ShipmentLedgerRepository}.cs`
  - `RugRepository.cs`: ShipmentId/BaleId/NotBaled/Location/Serial filter clauses, plus projection of location, bale and shipment number
  - `Features/Shipments/ShipmentsModule.cs`, migration M3 (with the hand-edited deferrable bale-number constraint, `HandEdits/Shipments.md`)
- **Web:**
  - `Api/{ShipmentsApiController, BalesApiController, DeclaredValuesApiController (GET and Business POST at /api/declared-values; Configure PUT at /api/settings/declared-values)}.cs`, `Web/Settings/DeclaredValuesSettingsLink.cs`
  - **`Controllers/ShipmentsController.cs` (Index, Workspace), `Views/Shipments/{Index, Workspace}.cshtml` shells, `Web/Shipments/{IShipmentTab.cs, ShipmentTabsViewComponent.cs}`**
  - `Controllers/Settings/DeclaredValuesController.cs` + view, `islands/settings/DeclaredValuesEditor.island.vue`, `lib/types/shipments.ts`

**Acceptance (API level)**
- Create a shipment and bales; add rugs by serial; record final sizes.
- `RugDto` for a baled rug carries shipment, bale and USD values (enricher). `/api/rugs?shipmentId=` filters.
- The declared value defaults from the table and re-defaults on an origin change (Open only). Changing `DeclaredValueKeyField` or the fallback re-values the open lines. A stale declared-values stamp returns 409.
- **A Merchant fixes a new origin in one action:** `POST /api/declared-values` for «کاشان» re-values its 20 open lines; an existing key returns 409; a negative rate returns 400.
- An `add` op with a new `baleNumber` creates the bale; there is never an empty bale left by «نگلهٔ بعد».
- Finalize is blocked with the full issues list; then finalize succeeds.
- After finalize, these all return 409 with the Persian message: a rug PUT, `BulkUpdateFields` (including `Status = Archived`), a workflow advance or path edit, a sale and a trash; a pattern edit; a bale renumber or weight; a `DocumentDate` edit. Each guard check also has a unit test with an otherwise-empty impact set. Cost entries, images and rug notes still work. (The grid-batch lock check is in P4b.)
- Bale delete: refused when the bale has lines; allowed when empty. Shipment delete: only Open and line-free. Bale renumber swaps numbers in one flush.
- Reopen clears the lock and re-values the lines from the current table. The snapshot hash is stable across re-finalize with unchanged data, and re-hashing the stored text equals `SnapshotHash`.
- Bale totals match a hand computation (per-piece lot rule).
- The workspace URL renders the empty tab shell.

#### P4b: baling UI (E-Grid, after P3 + P4a)

- **Application:** **`Features/Shipments/Grid/BalesGridSource.cs`** (moved from P4a); `App/Features/Intake/Grid/RugsGridSource.cs` (location column).
- **Web:**
  - `Views/Shipments/{_TabBales, Pack}.cshtml`, `Views/Shipments/Index.cshtml` (island tag)
  - `Web/Shipments/BalesTab.cs`, `Web/Controllers/ShipmentPackController.cs`
  - `Web/Navigation/ShipmentsNavContributor.cs`, `Web/Home/Cards/{OpenShipmentCard (target-shipment logic, A.4), ReadinessCard}.cs`
  - `Views/Rugs/Index.cshtml` and `Views/Rugs/Details.cshtml` partials (location chips and column; in Simple mode the workflow status column and filter hidden, A.3)
- **Islands and components:** `islands/shipments/{ShipmentList, ShipmentHeader, BalingBoard, PackMode}.island.vue`, `components/bales/{BaleCard (انتقال, اندازه chip), BaleMovePicker, FinalSizeSheet, StockPicker, BaleFooterRow, LotSplitDialog, CurrentBaleHeader}.vue`, **`components/scan/BarcodeScan.vue`**, `bulk-actions/AddToBale.action.ts`, `lib/messages/shipments.ts`.

**Acceptance**
- Rebuild bales 1–3 of the sample by typing serials, on a 1024 px tablet viewport and on a 375 px phone (pack mode). The first add creates bale 1; «نگلهٔ بعد» leaves no empty bale.
- **Keyboard-only measuring:** bale 1 of the sample (4 rugs) is added and measured with serial → طول → عرض per rug, with no extra Tabs (descriptive columns are not Tab stops in Simple mode).
- **Tablet:** at 1024×768 with touch emulation and no hardware keyboard, record final sizes for bales 1–3 (action bar, «اندازه» sheet) and undo one of them.
- «[بقیه همان اندازهٔ اول]» on the `E_NO_FINAL_SIZE` chip records the rest in one confirm.
- Footers read «شامل ۴ تخته · ۱۳٫۵۱ متر · ۶۰۷٫۹۵ دلار».
- **The bar turns amber above 14.3 m² (target 13 m² + 10 %)**, and the over-target copy names the 13 m² target.
- Moving a rug between bales updates both footers. **Move a rug from bale 2 to bale 3 without dragging, by touch and by keyboard** (the «انتقال» picker, key M).
- «خارج کردن از نگله» shows the undo toast; «برگردان» puts the rug back at the same `Seq`.
- Lot split works (D.9), with the preview.
- Scanning a serial QR (`BarcodeScan.vue`) adds the rug to the current bale, without navigating.
- With two Open shipments, the Home tiles open the chooser sheet (A.4).
- After finalize, a `/api/grid/rugs/batch` edit of one of its rugs and a bales-grid op both return 409 (moved from P4a, which has no grid yet).
- The bale term «عدل» shows «عدل ۳» everywhere, including the grid labels.
- **Usability checkpoint** (K.1): baling the P3 rugs into 2 bales with final sizes.

#### P5: cost ledger and allocation (E-Core); migration M4

- **Domain:** `Entities/{CostEntry, CostEntryTarget, CostEntryPin, CostAllocation, CostEntryResult}.cs`, `Enums/{CostScope, AllocationState}.cs`.
- **Application:**
  - `Features/Costs/{CostsModule, CostAllocator, CostEntryService, TargetResolver, ProfitService, CostFieldContributor}.cs`
  - `Features/Costs/Ledger/{CostEntryImpactContributor, BatchImpactContributor, AffectedEntryFinder, CostCategoryGuard, PurchaseEntryStage, ByPurchaseCostExpander, ExpenseEntryStage, RevenueShareStage}.cs` (with the D.4 propagation rules, receipt freezing, pins in base currency, weight clamping)
  - `DTOs/Costs/*.cs`, `ServiceProviderService.cs`, `DTOs/Providers/ProviderDtos.cs` (the `ProviderWorkItemDto` append together with `ServiceProviderRepository.ListWorkAsync`, I.1), `Messages.fa.Costs.cs`
  - `App/Seed/CostCategorySeedContributor.cs` is unchanged; the preset adds the rest in P7.
- **Infrastructure:**
  - `Configurations/CostLedgerConfigurations.cs`, `Repositories/{CostEntryRepository, CostLedgerRepository}.cs`
  - `LedgerRepository.cs` → **no edit**: `CostLedgerRepository` registers the real `ICostSourceProvider`, which `PurchaseRollupStage`, `RugCostTotalStage` and `PreferencesLockGuard` consume.
  - `ServiceProviderRepository.cs` (receipt union), `Features/Costs/CostsModule.cs`, migration M4
- **Web:**
  - `Controllers/CostsController.cs` + `Views/Costs/Index.cshtml`
  - `Api/{CostsApiController, CostCategoriesApiController}.cs`, `Controllers/Settings/CostCategoriesController.cs` + view, `Web/Settings/CostCategoriesSettingsLink.cs`
  - `Web/Shipments/{CostsTab, SummaryTab}.cs` + partials (into the P4a shell)
  - `Web/RugDetails/CostsPanel.cs`, `Web/Home/Cards/MoneyCard.cs`, `Web/Navigation/CostsNavContributor.cs`
- **Frontend:**
  - `islands/costs/{CostLedger, ShipmentCosts, ShipmentSummary}.island.vue`, `islands/settings/CostCategoriesManager.island.vue`
  - **`panels/CostEntry.panel.vue`** (Simple and Full; H.2), `components/costs/{TargetPicker, AllocationPreviewTable, MethodChooser}.vue`
  - `bulk-actions/CostEntry.action.ts` («ثبت هزینه برای این‌ها» via `openPanel`; `RugBulkToolbar.vue` itself is not edited), `islands/ProviderStatement.vue` (receipt rows), `lib/messages/costs.ts`
  - `SalesController` / `SalesApiController` date-range fix, `lib/types/costs.ts`

**Acceptance**
- D.7 #1–#12 reproduce exactly through the API and UI.
- Changing C's final size re-splits the freight to 2,119,302 / 3,879,728 / 4,000,970 **in the same request** (integration test: one PUT of C's final size returns and persists freight 2,119,302 / 3,879,728 / 4,000,970 and C total 77,893,170).
- Trashing B (unbaled) redistributes: the frozen washing receipt stays 1,768,850 and splits 603,425 / 1,165,425 over A and C (A 44,182,783, C 80,877,467); restoring B returns the #7 numbers. The provider statement stays 1,768,850.
- A lump purchase over a ورودی lands in PurchaseCost; moving a rug to another ورودی afterwards does not move its purchase share (snapshot). `W_PURCHASE_DOUBLE` shows when the rug also has a price line.
- The provider statement shows the washing receipt.
- An empty shipment shows the «تقسیم‌نشده» chip. Deleting a targeted bale, shipment or batch leaves the entry Unallocated with `W_TARGET_DELETED` (including Bale-scoped entries of a deleted shipment).
- The Simple-mode panel shows only type, the basis switch, amount, «برای» and the sentence. AreaBasis never appears. **Washing entered as «کل ۱٬۷۶۸٬۸۵۰ · به نسبت متراژ (اندازهٔ اول)» on {A,B,C} gives exactly 371,450 / 680,000 / 717,400**, identical to D.7 #4 entered per metre.
- A failed save (offline) keeps the panel open; the retry with the same `ClientKey` creates exactly one entry. Deleting a cost shows the undo toast.
- −100 over three equal rugs gives −34 / −33 / −33. All members pinned with a residual gives `Partial` and `W_PINS_RESIDUAL`; a pin on a live PerM2 entry gives 400.
- A ByPurchaseCost entry re-splits when a member's intake size changes its PerM2 purchase line (propagation rule).
- 200 random operations leave `cost-consistency` at 0.
- **Usability checkpoint** (K.1): two costs, freight and washing.

#### P6: document engine, xlsx and print (E-Docs); migration M5

- **The day-1 spike (E.6) gates the rest**, including the print-preview break check (#5) and the fitToPage + scale check (#6). P6-engine runs on pure fixtures with no DB. P6-wiring waits for M3 and M4 to merge.
- **P6-engine fixtures:** `tests/fixtures/sample-rows-1-43.expected.json` (E.10; transcribed from the sample XML), read together with P1's `sample-shipment.json` by the E.10 goldens and the `PageBreakPlanner` tests.
- **Domain:** `Entities/{DocumentTemplate, DocumentTemplateRevision, ExportRun}.cs`, `Enums/{DocumentKind, ExportFormat}.cs`.
- **Application:**
  - `Features/Documents/{DocumentsModule, DocumentTemplateService, ExportService, DocumentFieldContributor}.cs`
  - `Features/Documents/Layout/{Layout, LayoutValidator}.cs`
  - `Features/Documents/Expressions/{Lexer, Parser, Ast, TypeChecker, Evaluator, ExcelCompiler}.cs`
  - `Features/Documents/Composer/{RenderModel, DocumentComposer, FormulaResolver, PageBreakPlanner}.cs`
  - `Features/Documents/Defaults/{shipment-declaration.v1, rug-list.v1}.json` (embedded through the P0 csproj glob; v1 exactly as written in E.9)
  - `Abstractions/Services/IXlsxDocumentWriter.cs`, `Abstractions/Persistence/IDocumentDataRepository.cs`
  - `App/Seed/DocumentTemplateSeedContributor.cs` (seeds the `Defaults/*.v*.json` glob)
- **Infrastructure:**
  - `Infrastructure.csproj` (+ClosedXML 0.105.1, +DocumentFormat.OpenXml at the version ClosedXML requires)
  - `Documents/{ClosedXmlDocumentWriter, CachedValueInjector}.cs`
  - `Repositories/{DocumentDataRepository, DocumentTemplateRepository}.cs`, `Configurations/DocumentConfigurations.cs`
  - `Features/Documents/DocumentsModule.cs`, migration M5
- **Web:**
  - `Controllers/DocumentsController.cs`, `Views/Documents/{Print, _RenderedSheet}.cshtml`
  - `Api/{DocumentsApiController, DocumentTemplatesApiController (read endpoints only)}.cs`
  - `Web/Shipments/OutputTab.cs` + partial, `Support/CsvWriter.cs` (negatives)
- **Frontend:** `components/documents/DocumentPreview.vue`, `islands/documents/ShipmentOutput.island.vue`, `lib/types/documents.ts`.

**Acceptance**
- **Golden:** the lot-free fixture matches `sample-rows-1-43.expected.json` cell for cell, under the E.10 comparison rules (E.10 #1). The Adl fixture renders «عدل» everywhere (E.10 #4).
- The full sample gives 36 / 161 / 242 / 464.74 / 20,322.50; H = 1 on single-rug lines; the summary links use J and L.
- **Lot rows are per piece:**
  - bale 29: qty 50, area 0.16, USD 7.2, footer 50 / 8.00 / 360.00;
  - bale 30: 34 / 6.24 / 280.80.
- The row for serial 3816 caches 3.8 (the sample's J57), and the other midpoint rows match their decimal values.
- Serials are numeric cells; number formats are General; a single-row bale has no A merge; an empty bale is skipped with `W_EMPTY_BALE`.
- **Print titles** `$1:$2` on the main sheet only. **No row break falls inside a bale.**
- **Summary sheet:** title, «=» row and totals row formatted as specified in E.9, with links.
- The file opens in Excel and LibreOffice with values shown before recalculation.
- **The print page** repeats headers, does not split bales, prints yellow footers, and the console is clean under CSP. **The last printed page is the summary, with no main header above it.**
- In Excel print preview, page 2 of the full sample starts at bale 11 (E.6 #5), or the fallback scale is in effect and recorded.
- The download is named per E.7 (RFC 5987), with «پیش‌نویس» in the name for drafts.
- A draft shows «پیش‌نویس». ExportRun hashes are recorded.
- A foreign shipment, rug or template id returns 404, with no ExportRun and no file.

#### P7: designer, configurator setup and the cost template (E-Docs); no migration

- `Api/DocumentTemplatesApiController.cs`. Ownership transfers from P6 when P6 merges. P7 adds the writes, validate, validate-expression, revisions, reset, duplicate and set-default.
- `Features/Documents/Defaults/shipment-cost.v1.json`, a new file picked up by the seed glob.
- `Controllers/Settings/{DocumentTemplatesController, SetupController}.cs` + views, `Web/Settings/{DocumentsSettingsLink, SetupSettingsLink}.cs`, `Web/Home/Cards/SetupProgressCard.cs` («۲ کار تا آماده شدن مانده»).
- `Api/SettingsPresetsApiController.cs`, `App/Features/Documents/PresetService.cs` (categories with icons, the «برچسب شماره» label template, grid layouts through P3's `GridLayoutService`), `App/Features/Platform/SetupStatusService.cs` (the H.9 steps).
- `islands/documents/{DocumentDesigner, DocumentTemplatesList}.island.vue`.
- `components/designer/{ColumnStrip, FieldPicker, ExpressionEditor, FooterEditor, SummaryEditor, PageSetupPanel, RevisionsDrawer}.vue`.
- `islands/settings/SetupChecklist.island.vue`.

**Acceptance**
- From an empty tenant, the preset plus the checklist reach «آماده است»: exactly the H.9 required steps 1–6 are done, each computed server-side; a missing origin rate is listed by name in step 3. Applying the preset twice adds nothing; existing items (including P2's seeded purchase categories) show «موجود بود: {label}». Every preset icon exists in the sprite.
- Adding the computed column «جمع ریالی = round(col.usdTotal * shipment.usdRate, 0)» shows the formula preview and exports with live formulas.
- Moving a footer cell keeps the formulas correct.
- An invalid expression shows a Persian message with a JSON path; a cycle is rejected; a per-piece template without a qty column is rejected.
- Revision restore works; «بازگردانی پیش‌فرض» works.
- **The cost template** exports for Business. **An Operator gets 403** on it, and it does not appear in the Operator's template list.

#### P8: Excel import and undo (E-Grid); migration M6

- **Domain:** `Entities/{ImportJob, ImportMappingPreset}.cs`, `Enums/{ImportStatus, ImportMode, ExistingSerialPolicy}.cs`.
- **Application:**
  - `Features/Import/{ImportModule, ImportService, SheetGrid, HeaderDictionary, RowClassifier, BaleResolver, LotParser, KnownValueSplitter, Reconciler, ImportPlanner}.cs`
  - `Abstractions/Services/{IWorkbookReader, IImportFileStorage}.cs`, `App/Common/XlsxSignature.cs`, `DTOs/Import/*.cs`
- **Infrastructure:**
  - `Features/Import/{ClosedXmlWorkbookReader, XlsxPackageInspector}.cs`, `Storage/ImportFileStorage.cs`
  - `Configurations/ImportConfigurations.cs`, `Repositories/ImportJobRepository.cs`
  - `Features/Import/ImportModule.cs`, migration M6
- **Web:** `Controllers/ImportController.cs` + `Views/Import/*`, `Api/ImportsApiController.cs`, `Web/Navigation/ImportNavContributor.cs`.
- **Frontend:** `islands/import/{ImportWizard, ImportHistory}.island.vue`, `components/import/*`, `lib/types/import.ts`.
- **Tests:** `tests/Fixtures/SampleSheetBuilder.cs`, which **generates** the quirky xlsx from P1's `tests/fixtures/sample-shipment.json` (it reproduces every quirk but does not own the data).
- `Messages.fa.Import.cs`, `lib/messages/import.ts` (every F.3/F.4 code and the option labels, H.3b).

**Acceptance (the synthetic fixture runs in CI; the real file is tested manually, outside the repo)**
- 36 bales, 161 lines, 242 pieces, 464.74 m², 20,322.50 USD.
- Every F.4 code appears at its expected row: row 210 label warning (the bale stays 33); row 192 adopted by bale 31; `W_QTY_FROM_MATERIAL` ×3 (50/24/10); `W_RANGE_QTY_DIFF` on row 190; the merged-split suggestion «گبه / پشم»; `W_MISSING_FIELD` at 98, 177, 182, 187, 189 and 190. **The three lot rugs import with `bg_color` = null and material «پشم»** (never a serial range as a colour).
- Footer and summary caches with float noise reconcile after snapping (`I_VALUE_SNAPPED`).
- In Simple mode, the sample file goes straight from upload to the one-card preview (one sheet, headers recognised).
- The repeated titles and headers at 46–47, 87–88, 130–131, 173–174, 216–217 and 226 are skipped.
- Reconciliation is all ok.
- Commit creates a shipment dated 1405.06.15. Exporting it reproduces the totals, with lot rows per piece.
- A second commit, or a commit with a stale version, returns 409.
- Attach mode on rugs pre-entered at intake sets their final sizes.
- Re-uploading the file warns about the duplicate hash.
- Undo restores the DB (row counts and checksums). An edited rug blocks undo, and so does a cost entry that targets the imported shipment, its bales or rugs, or its batch; the blockers are listed. StockOnly undo also deletes the batch it created.

#### P9: polish, mobile, help, accessibility, contract (everyone); migration M7

- **Content and screens:**
  - `CA/lib/help.ts` content, `Views/Help/Card.cshtml` content
  - the remaining Simple variants of `Views/Rugs/{Index, Details}.cshtml` (the workflow-status hiding already landed in P4b)
  - the RugWorkflowPanel modal on UiShell's focus trap
- **Jalali inputs** replace the native ones in `Rugs/Index.cshtml`, `StepFieldsForm.vue`, `RugWorkflowPanel.vue`, `RugSalePanel.vue`, `SalesReport.vue`, `ProviderStatement.vue` and `Activity.cshtml`.
- **`bg-white` cleanup** in `RugForm`, `LabelDesigner`, `CustomFieldsManager`, `WorkflowTemplateEditor`, `RugGallery` and `PwaInstall`.
- **Fixes:**
  - label `v-html` escaping;
  - `GroupDetail.vue` (no `prompt()`, no 100-row cap);
  - `AnalyticsService` set-based.
- **Platform hygiene (J.3):** `DatabaseSeeder.cs` honours `Seed:Demo`.
- **M7:** drops `Rugs.PurchaseCost` and removes the `[Obsolete]` property (`Rug.cs`, `EntityConfigurations.cs`). Gated by `[RequiresBackup("Rugs")]` and `Database:ConfirmedBackupFor`.

**Acceptance**
- Keyboard-only flow from intake to bale to cost to export.
- axe reports zero serious issues; Lighthouse accessibility ≥ 90 on Home, Intake, Shipment and Costs. A manual check confirms that no action needs dragging (SC 2.5.7), which axe cannot detect.
- The father, or a stand-in, does intake of 10 rugs, bales them, adds freight and prints, without help, in under 20 minutes.
- Production backup confirmed before M7.

---

## L. Tests and verification

### L.1 Unit tests (`tests/RugsManagment.Tests`; Application + Domain)

| Area | Cases |
|---|---|
| `Num` / `AreaMath` | 2.675→2.68, −2.675→−2.68, 0.005→0.01; never ToEven. **Midpoint vectors:** R17 1.93×1.50→2.90, R57 2.30×1.65→3.80, R107 1.51×0.50→0.76, R149 0.77×0.50→0.39, R176 1.49×0.50→0.75, R197 0.79×0.50→0.40; plus 2.50×0.37→0.93, 1.50×0.83→1.25, 1.05×0.50→0.53, 0.45×0.45→0.20. **Lot rule:** 0.40×0.40×50 → line 8.00; USD 0.16×45.55 → piece 7.29 → line **364.50** (not 364.40); money PerM2 45.55 on 0.16 at scale 0 → 7 × 50 = **350** (scale 2 → 364.50); PerM2 money 9,000,000×0.16 → 1,440,000 per piece × 50; FX per piece 7.29 × 105,000.5 × 50 → 38,272,700. **Snapped import value:** 2.2999999999999998×1.65 → 3.80. **All 161 sample lines** of `tests/fixtures/sample-shipment.json` (P1) reproduce the file's J and L. Shared JSON vectors live in `tests/fixtures/math-vectors.json` (each with `kind` and `scale`; also run by vitest). |
| `NumberParser` / `SerialSuggester` | Money suffixes «۱۰م», «۸۵ه»; «10.000» at scale 0 asks; other decimals rejected; dims «25»/«30» ask, «12» → 12 m, «40» → cm confirm, «208×210». Next serial: after «3985 - 4003» → 4004; 2026–2028 do not lower it; `SerialStart` floor; trashed rugs excluded, Exported and Sold included; grid-local increment after a lot row. |
| `Dimension` / `SerialNumber` | >2 dp rejected; 0.04 and 30.01 rejected; «3778», «۳۷۷۸», «3911 - 3960», «3911–3960», «3911_3960», «3911 تا 3960», «A-12»; «3960-3911» error; span >10,000 error; range length vs qty; display «3911 - 3960» |
| `Rug` domain | Final overrides intake; clear reverts; both-or-none; TotalAreaM2 for a lot (0.16×50 = 8.00) |
| `PurchaseMath` | PerM2 on intake area, per piece then × qty; PerPiece `ROUND(rate, s)` × qty; Percent `ROUND(Σ non-Percent × p/100, s)` once per line, on a lot too; negative Total line; recompute on size change. Grid purchase columns: legacy Total line read as «کل», PerM2 entry on a lot (rate × 0.16 × 50), clearing deletes, two lines → read-only. |
| `RugCostMath` | Step filter (Completed/InProgress), per-step floor at 0, purchase vs expense buckets, deleted entries excluded, per-m² null at 0 area. **Step pricing on lots:** one vector per model (Fixed, CustomFormula, PerSquareMeter, PerSquareFoot, PerLength, PerWidth, Combined) with qty 50; `ManualCostOverride` and `Adjustment` line-level; C#/SQL parity. |
| `CostAllocator` | Every row of D.3. D.7 #1–#12 verbatim (including the frozen washing receipt at #8). Pins: valid, exceeding (vs `BaseAmount`, after FX), dropped, all-pinned residual (`W_PINS_RESIDUAL`), pins on a live PerM2 entry (`E_PINS_BASIS`), zero weights among the unpinned subset only. Receipt freeze and `W_RATE_DRIFT`. Manual unallocated. Negative T: −100 over 3 equal → −34/−33/−33. Zero weights → equal. Negative weights clamped (`W_NEGATIVE_WEIGHT`); ByPurchaseCost weights in cents. FX order (original then base), per piece for PerM2/PerPiece. Toman↔Rial. `E_CIRCULAR`. Tie-break: two equal-weight rugs with null `SerialSort` whose `Guid.CompareTo` and ordinal string order disagree → the string order wins. **Property tests** (1,000 cases: 1–2,000 members, weights −10⁹..10⁹, amounts ±10¹⁵, random pins): negative weights clamped; Σ exact; Allocated + Unallocated = `BaseAmount` for every entry, including all-pinned; every share has T's sign or is 0, and \|share\| ≤ \|T\|; \|share\| ∈ {⌊\|exact\|⌋, ⌈\|exact\|⌉}; order independence; determinism. 10,000 members < 50 ms. |
| `ChangeSetBuilder` | Value diff, not `IsModified`: `rugs.Update(rug)` on a tracked rug where only Notes changed → ChangeSet = {Notes}, and no lock violation on an Exported rug; only `UpdatedAt` changed → empty; decimals by value; an untracked attach → "unknown" with full impact. |
| Impact contributors | Each D.5 row (1–30, including 2a, 2b, 15b and 25b) produces the expected impact sets (old and new batch, bale, shipment) from synthetic ChangeSets, including principal deletes read after the lock and before the flush. The D.4 propagation rules (PurchaseCost → ByPurchaseCost; LineUsd → ByDeclaredValue and RevenueShare) and full warning recompute. |
| Guards | `ShipmentLockGuard`: each locked C.8 field rejected **with an otherwise-empty impact set** (descriptive fields, Status, serial, workflow path, bale renumber and weight, shipment header fields); allowed fields (costs, purchase lines, revenue, notes, images) pass; finalize and reopen change sets pass. `BaleMembershipGuard` (one line per rug, Open only, bale belongs to the shipment). `PreferencesLockGuard`: MoneyScale and currency are refused once any D.5 #22 money exists. `KeyFieldGuard`. `CostCategoryGuard`. |
| `RugStatusResolver` | Sold > Exported > workflow; Exported is never overwritten |
| `DeclaredValue` | override > table > fallback > missing; `KeyNormalize` («نائین ۹ لا» = «نائين 9 لا»); negative rates rejected; the Business add refuses an existing key |
| `RugSplitService` | Serial auto vs typed; Total lines and pins split by LR; PerM2/PerPiece lines copied; targets duplicated; steps split; images stay; blocked when sold or locked |
| Expressions | Precedence, unary minus, Excel blank semantics, `/0` → blank, `if`/`and`/`or`/`not`, `&`, text comparisons rejected, Bool column rejected, limits. **Compile:** `round(col.length*col.width,2)` → `ROUND(H3*I3, 2)` (qty hidden) and → `ROUND(I3*J3, 2)` (qty shown); a perPiece footer → `SUMPRODUCT(H3:H6,K3:K6)` and count → `SUM(H3:H6)`; the `lotRow: perLine` case `round(col.length*col.width,2)*col.qty` → `ROUND(I3*J3, 2)*H3`, folded to `ROUND(H3*I3, 2)`; `coalesce` → `IF(a="", b, a)`; string literals with `"` doubled; a literal over 255 characters → value-only |
| `LayoutValidator` | Each E.2 rule with its Persian message and path, including the perPiece-requires-qty rule, a footer `col` written as a letter, an unknown `footer:<id>`, an overlapping footer span, and an unknown style. `shipment-declaration.v1` and `rug-list.v1` validate. |
| `PageBreakPlanner` | Capacity formula with the worked numbers (734 px → s = 1, C = 43; 772 px → 579 pt, s = 0.9787, C = 44; fallback s = 0.97, C = 45); a block that does not fit breaks before it; a block taller than a page → `W_BALE_TOO_LONG`; on `sample-shipment.json`, pages hold bales 1–10, 11–19, 20–22 and 23–27 (fallback: page 2 = 11–20) |
| `DocumentComposer` golden | E.10 #1–#4, against `sample-rows-1-43.expected.json` and `sample-shipment.json`: row map (title 1, header 2, bale 1 rows 3–6, footer 7, …); merges, with none for single-row bales; footer anchors by id; `ROWS(B3:B6)` and the qty variants; H = 1 on single-rug lines; numeric serial cells (and «0123» as text); General format; exact widths; title colour `FF767171`; `shrinkToFit` on data cells; summary sheet (title C1:H1 yellow with no border, «=» row with no border and no quote prefix, totals row with a bordered blank D, links J/L with lots); totals 242/464.74/20,322.50; bale 29 = 50/8.00/360.00 with per-piece row 0.16/7.2; bale 30 = 34/6.24/280.80; bale 31 contains 4004; bale 33 footer = 33; `checkAgainst` never fires; a Finalized fixture renders from the snapshot; an empty bale is skipped; Adl renders «عدل» |
| Import | `HeaderDictionary` scoring; `RowClassifier`, `BaleResolver`, `LotParser`, `KnownValueSplitter`, `Reconciler` on `SampleSheetBuilder` (every F.3/F.4 code at the expected row); covered merge cells read as empty except the two exceptions (F.2), so the lot rugs get `bg_color` = null; float snapping of dims (`W_DIM_SNAPPED`) and of footer/summary caches (`I_VALUE_SNAPPED`); attach planning; undo blockers from dependent cost entries |
| Messages, glossary, icons | Every code constant has a `Messages.fa` entry with no Latin letters outside placeholders (H.3b); the glossary test (A.2, J.2); every seed and preset `Icon` exists in `icons.svg` |
| Jalali / format | 200-date fixture; `jdot` 2026-09-06 → «1405.06.15»; invariant numbers (no `fa` culture) |
| Existing | `CostCalculationServiceTests` moved to AwayFromZero and qty; the misleading `TemplateOverride_WinsOverStepTypeDefault` rewritten; `WorkflowEngineTests` totals come from `CostTotal` |

### L.2 Integration tests (`tests/RugsManagment.IntegrationTests`, real Postgres)

- **Migrations and startup:**
  - empty → head; `has-pending-model-changes` is empty;
  - a legacy fixture (schema at `SoftDeleteRugs` plus data) → head, with M2's self-verify passing; a crafted inconsistency makes M2 raise;
  - both chain-repair histories migrate (Pending then Sync, and Sync then Pending);
  - a test migration marked `[RequiresBackup]` on a populated table makes startup throw `MigrationBackupRequiredException` **once, without retries**; on an empty table it proceeds;
  - head → down to `20260603141744_CatalogPatternsLabels` → head again (B.15);
  - M2 on legacy dimensions with 3 dp: `LengthMeters = COALESCE(FinalLength, IntakeLength)` on every row and generated `AreaM2` = `IntakeAreaM2` for rugs without a final size;
  - startup tasks run in Order.
- **Pass coverage.** Each legacy write path refreshes `RugCostTotal` and allocations **in the same request** (the response already carries the new totals): RugsApi PUT, `BulkUpdateFields`, `BulkAdvance`, `AssignRugs`, `WorkflowEngine` advance/pricing/activate, sale save/cancel, trash/restore, batch delete, the seeder. One PUT of C's final size (D.7 #7) returns and persists freight 2,119,302 / 3,879,728 / 4,000,970 and C total 77,893,170.
- **Legacy round trip** (LM-02): GET a rug with PerM2 + Percent purchase lines, a lump share and a final size, then PUT the unchanged DTO through the old path: purchase lines, `IntakeLength`/`IntakeWidth` and `RugCostTotal` are byte-identical.
- **Locks.** Each of those paths returns 409 on a Finalized shipment's rug, including guard-only saves (a pattern edit, `Status = Archived` through `BulkUpdateFields`, a bale renumber, a `DocumentDate` edit).
- **Sale snapshot.** A sale created in the same request as a cost change snapshots the post-change Total; editing a sale keeps `CostAtSale`.
- **Concurrency:**
  - two parallel transactions on one tenant serialize (timestamps), and the final allocations equal a fresh rebuild;
  - a stale `version` gives 409;
  - derived writes never change `Rugs.xmin`;
  - **a stale declared-values stamp and a stale grid-layout version give 409;**
  - **a double import commit gives exactly one success and one 409;**
  - two purchase-line PUTs with the same rug version give exactly one 409.
- **Serial race.** Two concurrent creates of overlapping ranges → exactly one succeeds; in SuggestNext the loser's 409 carries `nextFree`.
- **Ledger fuzz.** 500 random operations, then `LedgerVerifier` reports zero drift. The operations are: create, resize, **qty edit on a rug with a final size in an Open bale**, move between bales, **move a rug between ورودی groups**, trash/restore, entry CRUD (including receipts that freeze), pins, **lot split**, finalize/reopen, **a table, fallback or key-field change while Finalized followed by reopen**, import/undo, **bale delete, shipment delete, batch delete, bale renumber, declared-value replace and add, `DeclaredValueKeyField` and fallback changes, and refused MoneyScale/currency changes**.
- **Performance.** Finalize and reopen of a 300-line shipment issue fewer than 20 SQL commands; a `measure` op on a 300-rug shipment with 8 Effective-basis entries completes in under 200 ms (D.4).
- **Snapshot.** Re-hashing the stored `SnapshotJson` text equals `SnapshotHash`.
- **C#/SQL parity:**
  - the provider-ledger SQL step cost vs `EffectiveCost` over random steps;
  - grid `derived` equals the stored columns;
  - bale `GROUP BY` totals equal the composer totals;
  - generated `AreaM2` equals `AreaMath.PieceArea` over the math vectors.
- **Tenant isolation.**
  - **Every action of every controller in `Web/Controllers`** (API, MVC and `Settings/*`) is enumerated by reflection and called with sample route values from its `[IsolationSample]` attribute (or a per-phase `Samples/{Phase}.cs` partial). The test fails when an action has no sample.
  - Calling them with tenant B's ids under tenant A gives 404 and no side effects.
  - In particular, `/shipments/{id}/export.xlsx`, `/print`, `POST /documents/rugs.xlsx` (foreign `rugIds[]` or `templateId`), `/Shipments/{id}`, `/Shipments/{id}/Pack`, the Rugs/Details panels and `/Import?jobId=` write **no ExportRun row and no file**.
  - A convention test asserts that every new repository query method takes `Guid tenantId`.
- **xlsx round trip.** Write the file, then read it back with **DocumentFormat.OpenXml**, not ClosedXML evaluation, and check:
  - RTL, zoom and page setup;
  - print titles `$1:$2` on the main sheet only;
  - **`rowBreaks` fall only between bale blocks;**
  - merges;
  - formula text;
  - `<v>` values equal the composer's (including 3.8 for serial 3816);
  - serial cells numeric, ranges text;
  - data-sourced text cells starting with `= + - @` are strings with a quote prefix; the summary's «=» literals have none;
  - the workbook Normal style font is Calibri 11;
  - `fitToPage`/`scale` as the spike decided (E.6 #5–#6);
  - `fullCalcOnLoad`;
  - the summary-sheet styles.
- **Import end to end.** Synthetic xlsx → preview → commit → export → totals equal → undo → DB equal to before (row counts and checksums). A zip bomb and a non-xlsx file are rejected.
- **Auth matrix** (in-process `WebApplicationFactory<Program>` with the test authentication handler, P0):
  - Merchant → 403 on Configure endpoints (every `/api/settings/*`; the Floor declared-values read lives at `/api/declared-values`) and a 302 denial on Configure pages;
  - Operator → 403 on Business endpoints (`/api/analytics`, `/api/sales/*`, `POST /api/declared-values`) and money grid keys; 403 on money members in a rug PUT, bulk-fields PUT or step-pricing PUT; a grid delete of a rug the Operator did not just create → 403 for that row;
  - **no money key (`totalInvestment`, `purchaseCost`, `costPerM2`, `targetSalePrice`, `estimatedMargin`, `outstandingReceivable`, sale amounts, step amounts such as `effectiveCost` and `defaultUnitRate`) and no Toman-formatted value appears in any Operator JSON or HTML response** (Dashboard, Rugs list and details, `/api/rugs`, `/api/rugs/{id}` with steps, `/api/home/summary`, `/api/fields`, `/api/lookups/step-types`);
  - MVC domain errors: trashing a baled rug through `POST /Rugs/Delete` → 302 plus an error toast, not 500;
  - the security stamp, tenant deactivation and subscription expiry each invalidate the session within the window; changing your own password keeps the current session and invalidates a second one.

### L.3 Frontend tests (vitest; `npm test` in CI)

- `lib/num.spec.ts`: the shared vectors (with `kind` and `scale`), including the lot, money-scale, FX-per-piece and snapped cases; a test that **no `Number` multiplication** is used.
- `lib/money.spec.ts`: `parseDecimal` with «۲/۰۸», «۲٫۰۸», «2,08» in dimension vs money columns; the «م» / «ه» suffixes; «10.000» at scale 0; the metres-or-cm vectors of G.4.
- `lib/numWords.spec.ts`: «ده میلیون تومان» and edge cases; `lib/messages.spec.ts`: every code has a message.
- `lib/jalali.spec.ts`: the shared fixture.
- `lib/ids.spec.ts`: the fallback without `randomUUID`.
- `lib/ui.spec.ts`: `openPanel` resolves the glob; unknown names fail gracefully.
- `grid/gridParse.spec.ts`: TSV quotes, CRLF, header detection, «208×210», the cm-confirm trigger, one confirm per paste, serial ranges, the local next-serial increment after a lot row.
- `grid/useGridKeyboard.spec.ts`: RTL arrows, the Tab-run Enter return, the Enter-right preference, non-Tab-stop columns skipped.
- `grid/useGridDrafts.spec.ts`: storage that throws; «پاک کردن» moves the payload to `rugs:draft-discarded:*` and «برگردان» restores it.
- `npm run build`: strict vue-tsc.

### L.4 Verification in the running app (Persian data via the browser pane `fetch` + `JSON.stringify` only)

Each step runs from the first phase where it is possible, and at every phase approval after that.

| # | Step | First phase | Before that |
|---|---|---|---|
| 1 | Log in as the son (TenantAdmin). Create «پدر» as Merchant with «نمای ساده». Apply «بستهٔ صادرات فرش»; the checklist reaches «آماده است». | P1 (users); P7 (preset, checklist) | P2–P4: the three purchase categories are seeded by P2; declared values through `PUT /api/settings/declared-values` (P4a) via browser `fetch`; expense categories through the cost-categories API from P5 |
| 2 | Log in as پدر: 5 nav items. MVC `/settings/*` → 302 to `/account/denied`; `/api/settings/*` → 403. As an Operator: no Toman money anywhere. | P1 | — |
| 3 | «ثبت فرش تازه»: 3 rugs including the lot «3911-3960» at 0.40×0.40, with the seller column; paste 10 sample rows; reload mid-typing and recover. | P3 | P2: `RugForm`, one rug at a time (lot area 8.00) |
| 4 | New shipment 1405/06/15 (one tap). Bales 1–3 by serial at 1024×768 (touch emulation, no keyboard) and in pack mode at 375 px. Final size of 3782 = 3.30 × 2.50 (badge «↓۵ سانت»); the rest «همان اندازه» in one action. | P4b | P4a: API only |
| 5 | Costs: washing PerM2 85,000 (intake), freight 10,000,000 by area through the Simple panel. Check D.7 on rug details. Change C's final size and check the re-split. | P5 | — |
| 6 | Output: download the xlsx; open it in Excel and LibreOffice. Check formulas, RTL, the repeated header on page 2, no bale split across pages in **print preview** (page 2 starts at bale 11), per-piece lot rows, the summary-sheet links and format, 3.8 for 3816, «پیش‌نویس» in the header and the file name. Print to PDF from the print page: the last page is the summary with no main header above it. | P6 | — |
| 7 | Finalize. Edits are blocked (rug page, grid, bulk bar). Add a late freight cost: profit changes, the document does not. Re-export: no draft mark, same data hash. | P6 (full); P4a (lock via API); P5 (late cost) | — |
| 8 | Import the real sample manually as a new shipment: reconciliation ok; warnings as in P8; undo. | P8 | — |
| 9 | `/settings/ledger-health`: zero mismatches. | P2 | — |
| 10 | Dark mode and Large/XLarge text pass on every new screen: on-screen bale footers use tokens (no literal yellow), the target bar's three states use tokens with icon and text, `DocumentPreview` keeps its light `.paper` surface, and at XLarge on 1024 px the شماره and size columns stay visible. | P1 onward, each phase's screens | — |

---

## M. Risks, defaults and out of scope

### M.1 Risks and mitigations

| Risk | Mitigation |
|---|---|
| The Consistency Pass changes every save (blast radius) | Fast path for saves that touch no ledger or guarded entity. A value diff (never `IsModified`), so legacy `Update()` calls do not explode the impact. Two phases: stages run only after the request's own rows are flushed. Contributors are unit-tested. The pass ships in P2 with the architecture tests, the 500-operation fuzz and the legacy-path integration tests. `LedgerVerifier` and rebuild exist for recovery. |
| A write bypasses UoW (`ExecuteUpdate`, raw SQL) | Architecture tests fail CI. A code-review checklist item. |
| A principal delete cascades in the DB without the pass seeing dependents | Contributors read dependents after taking the lock and before the flush (D.4). FK behaviours are fixed in B.13. Bale, shipment and batch deletes are in the fuzz. |
| The legacy rug form silently rewrites purchase lines or the intake size | The I.3 rules for `UpdateRugRequest.PurchaseCost` and W/L, RugForm stops sending them in P2, and the legacy round-trip integration test. |
| Money leaks through older endpoints (analytics, sales, step DTOs) | The A.5 list of endpoints and DTO members, the 403 on money writes, and the extended auth matrix. |
| A failed pass leaves a dirty context | `session.Reset()` clears the tracker. Services do one SaveChanges per request; the grid's per-row fallback reloads. |
| ClosedXML APIs or package behaviour differ | Day-1 spike. The OpenXml injector owns the cached values, print titles and breaks. Value-only fallback per cell, logged in ExportRun. Manual checks in Excel and LibreOffice. |
| Page-break estimates drift across printers and fonts | Fixed row heights, Excel's own width formula, 2 rows of slack, a round-trip test that breaks fall between bales, and print-page `break-inside: avoid`. |
| B Nazanin is missing or not redistributable | The xlsx references the font by name, as the sample does. Print falls back to self-hosted Vazirmatn. The template font is configurable. |
| Money leaks to Operators through an old view or JSON | `MoneyRedaction` in services, view guards, the sort guard, and an auth-matrix test over every Floor endpoint and page. |
| Changes to preferences or tenant currency silently re-value money | MoneyScale and currency are locked once money exists. Value-affecting preferences go through `PreferencesImpactContributor`. |
| Advisory-lock contention | Hold time is milliseconds, up to about 3 s for an import. If needed, keys can later be split per shipment. |
| M2 rounds legacy dimensions | `[RequiresBackup]`, a backup table, the count reported in the migration log, and a self-verify of totals. |
| The merchant is confused by «قطعی» | Plain copy, a lock banner with reopen, readiness chips on Home. |
| Step cost and a ledger receipt are counted twice | Separate buckets on the rug page plus `W_STEP_DOUBLE`. Purchase lines and a lump purchase: one entry point in Simple mode plus `W_PURCHASE_DOUBLE`. |
| Import heuristics mis-guess | Everything is a warning shown in the editable preview before commit. Undo exists. |
| Parallel lanes conflict on shared files and migrations | Ownership table (K.1), the seams, explicit dependency edges, a fixed migration merge order, the regenerate rule, CI `has-pending-model-changes`. |
| `fa` culture minus sign corrupts numbers | All numeric serialisation is invariant; tests assert ASCII output. |

### M.2 Open questions resolved with defaults

| # | Question | Default |
|---|---|---|
| 1 | Father's role | Merchant (Business, no Configure). The son assigns it in Users. Migration changes nothing automatically. |
| 2 | Bale word | «نگله», switchable per tenant to «عدل» («عدل ۳», no written kasre, and the «عدل شماره» footer), in every UI string, grid label, preset name and document (title, headers, footers). |
| 3 | Final size required before finalize | Yes (preference, default true). «همان اندازه» is explicit, per row or in bulk («[بقیه همان اندازهٔ اول]», C.4). |
| 4 | Serial reuse | Unique among active rugs, including exported ones. Trashed rugs release their serial; restore re-checks. |
| 5 | Lot range vs qty | The range may be longer than qty, never shorter (CHECK). |
| 6 | Lot split sub-range | Automatic only when range length = qty; otherwise typed. |
| 7 | Declared-value key | `rug.origin`. Any text catalogue field can be chosen; composite keys are unsupported. |
| 8 | Costs after finalize | Allowed, with a banner. |
| 9 | Who finalizes and reopens | Business (Merchant, TenantAdmin). |
| 10 | Operators and money | Operators see the declaration (USD is not internal cost). **No Toman money anywhere**: pages, lists, dashboard, API JSON. Reports moves to Business. |
| 11 | Money scale | 0 for Toman and Rial. Locked once money exists. |
| 12 | Purchase PerM2 basis | Intake, fixed. |
| 13 | Default allocation | By area, effective sizes. Washing and repair default to intake. |
| 14 | qty column position | Between «مواد مصرفی» and «طول». Shown only when the shipment has a lot. |
| 15 | Summary placement | Its own sheet in the xlsx, formatted like the sample's (E.9); `summaryPlacement: "sheet"` is the only v1 value. The print page always ends with the summary on its own page (E.8), so «چاپ» is the primary button and the xlsx note tells the merchant to print the whole workbook. A same-sheet placement with physical repeated headers is v2. |
| 16 | Footer count and sums | `ROWS()` and `SUM` when qty is hidden; `SUM(qty)` and `SUMPRODUCT(qty, …)` when shown. |
| 17 | Draft exports | Allowed, with «پیش‌نویس» in the page header. |
| 18 | USD rate | Per shipment (`UsdRate`), pre-filled from preferences. No online source. |
| 19 | Export revenue | A shipment-level amount; the per-rug share is by declared USD. |
| 20 | Import existing serials | Attach (default) in Declaration mode. |
| 21 | Import dimensions | Final in Declaration mode, intake in StockOnly mode. |
| 22 | Import undo window | No time limit; only while the rows are untouched and the shipment is Open. |
| 23 | Enter key | Down (Excel); a per-user "Enter moves right" option. |
| 24 | Legacy Api, client and stray folders | Deleted in P0, with the owner's approval. |
| 25 | `@vueuse/core` | Removed (the grid uses in-house windowing and storage). |
| 26 | Print digits | Persian in print and preview. The xlsx keeps numeric cells and the Latin-digit title, as in the sample. |
| 27 | Lot partial sale | Split first. |
| 28 | Starter templates | Declaration (default) and rug list (P6), cost sheet (P7). Others are v2. |
| 29 | Lot rounding | Per piece, then × qty, everywhere (D.3). |
| 30 | Page breaks | Bales are kept together with manual row breaks; an over-long bale breaks inside, with a warning. |
| 31 | Deleting a bale, shipment or batch that cost entries target | Allowed after confirm; the entries become «تقسیم‌نشده» (`W_TARGET_DELETED`). |
| 32 | Empty bales | Allowed while Open; skipped in documents; they block finalize. |
| 33 | Serial cells in xlsx | Numbers when all digits; text otherwise. |
| 34 | Cost panel for the merchant | «سه ورودی: نوع هزینه، مبلغ (هر متر / هر تخته / کل)، برای» plus one sentence; everything else under «بیشتر» (H.2). |
| 35 | Per-metre or per-piece cost with a provider or receipt number | Frozen at save as a receipt (D.3); the amount never changes retroactively; `W_RATE_DRIFT` offers a re-freeze. |
| 36 | «خرید یکجای این ورودی» when rugs later change ورودی | Purchase-group IntakeBatch entries snapshot their members at save; expense entries stay dynamic with a confirm (D.2). |
| 37 | What one «ورودی» is | One seller on one Iran day (B.5, H.1). |
| 38 | Next serial | `max(SerialStart, max(SerialTo ?? SerialFrom) + 1)` over active rugs, Exported and Sold included (G.4). |
| 39 | Who may delete a rug from the grid | Floor only for its own rug from the last 24 h with no line, allocation, sale or step cost; otherwise Business (G.6). |

### M.3 Deliberately out of scope (v1)

- Closing the books and locked pins. The schema leaves room: a `CostEntryPin.Kind` can be appended.
- A btree_gist exclusion constraint on serial ranges (optional hardening).
- EF named tenant filters and composite tenant FKs.
- Server-side PDF. Browser «ذخیره به‌صورت PDF» is used instead.
- FX history and online FX lookup; customs e-filing; any external service.
- Supplier payables and seller balances. The seller is only a name on the ورودی.
- Per-piece tracking and labels inside lots; partial lot sale without a split.
- Bale stickers with QR cards, price lists with parameters, ColumnRepeat, template-from-xlsx, and import mapping transforms. These are candidates for v2 through the same engine.
- Multi-sheet or `.xls` import, CSV import, and Excel round-trip edit-back.
- Conditional formatting, images, charts or logos in xlsx.
- Background offline replay for baling (drafts plus explicit retry only); real-time collaborative editing.
- Allocation by weight (kg); "as-of date" cost reports.
- Native mobile apps (the PWA, card modes and pack mode cover the warehouse).
- Double-entry accounting, GL export, tax and VAT.

---

## Revision log

| # | Critic item (severity) | Resolution | Where |
|---|---|---|---|
| 1 | Spec text truncated; A–H.3 missing (blocker) | Sections A–H.3 rebuilt in full from the references in I–M, the brief, the map and the sample XML, with all fixes applied. Flagged in the status note for a fresh critic pass. I could not write `scratchpad/spec.md` because this task forbids modifying files; the orchestrator should save this output there. | A–H.3 |
| 2 | Program.cs frozen, but P2 needs `LedgerStartupCheck`; retries hit the backup refusal (major) | `IStartupTask` added in P0; Program runs Migrator → Seeder → tasks by Order. `TenantSeedRunner` (P1, 100) and `LedgerStartupCheck` (P2, 200) register from module DI. `StartupAbortException` and `MigrationBackupRequiredException` are excluded from the retry filter. | J.1 #2, K.1, K.2 P0–P2 |
| 3 | Global cost drawer needs P1-only files (major) | P1 adds a generic panel host: `ui.openPanel`, the `panels/*.panel.vue` glob, `window.rugsUI.openPanel`, and a `site.js` `[data-open-panel]` delegate for Razor under CSP. P5 only adds `CostEntry.panel.vue`. | I.5, J.2, K.1, K.2 P1/P5 |
| 4 | Workspace tabs are owned by P4b, but P5 and P6 need them (major) | `IShipmentTab`, `ShipmentTabsViewComponent`, the Workspace and Index shells and `ShipmentsController` moved to P4a (E-Core). Explicit edges P5 → P6-wiring and P5 → P7. `shipment-cost.v1` and its Operator-403 acceptance moved to P7. | K.1 graph, K.2 P4a/P4b/P6/P7, E.1 |
| 5 | P3 grid contract depends on P4a's shipment lines (major) | `IGridSource` keyed by gridId; P3 implements `intake` and `rugs`; P4a adds `BalesGridSource`. `LineId`/`LineVersion` are now optional trailing members. | G.1, I.2, I.3, K.2 P3/P4a |
| 6 | RugDto and RugQuery enrichment hits frozen files (major) | P2 adds `IRugDtoEnricher` and declares every `RugQuery` property, its `HasActiveFilters` terms and the link round-trip. P4a adds `ShipmentRugDtoEnricher` and the filter clauses in `RugRepository`. `RugQuery` is correctly described as an init-property record. | I.3, K.1, K.2 P2/P4a |
| 7 | P0 WP overlap on Program, appsettings, DI, User and the mapping (major) | WP0-A owns Program.cs, both appsettings files, both DI files, both csproj files, User.cs, M0, and the SecurityStamp mapping inside the existing User block (one configuration per entity). WP0-B delivers code plus a checklist. | K.1, K.2 P0, B.1 |
| 8 | Excel lot rows are line-level (major) | The default template is per piece: area `ROUND(L×W,2)`, USD `ROUND(area×rate,2)`. With qty shown, footers use `SUM(qty)` and `SUMPRODUCT`. `lotRow: perPiece\|perLine` is exposed, plus a validator rule. Golden assertions: bale 29 row 0.16/7.2, footer 50/8/360; bale 30 34/6.24/280.80. | E.2, E.4, E.9, E.10, L.1, K.2 P6 |
| 9 | Lot rounding is ambiguous (364.50 vs 364.40) (major) | One rule, applied identically in C#, SQL, `num.ts`, `ExcelCompiler` and the composer: round per piece, then × qty. The 45.55 vector is added. | D.3, G.4, L.1, K.2 P2, M.2 #29 |
| 10 | No bale-aware page breaks in the xlsx (major) | `PageBreakPlanner` computes manual row breaks (A4 height, Excel's width formula, scale, 2 rows of slack). `keepGroupTogether` defaults to true; fixed row heights; `W_BALE_TOO_LONG`. The round-trip test asserts breaks fall only between bales. On the sample data it reproduces the merchant's page grouping. | E.7, E.9, E.10, L.1, L.2, M.1 |
| 11 | Secondary cell details (minor) | All-digit serials are numbers; General format; no merge when rowspan = 1; empty bales skipped with `W_EMPTY_BALE` and blocking for finalize; the summary sheet's title (C1:H1, yellow, no border, the sample's spacing), «=» row (no border) and totals row (bordered blank D) are specified and added to the golden test. Every page-setup and style value is encoded. | E.7, E.9, E.10, C.6 |
| 12 | Operator money exposure (major) | Decided: Operators see no Toman money. `IMoneyVisibility` and `MoneyRedaction` in services; the four views; the RugSalePanel guard; money hidden in the workflow panel; sort guard; Reports moves to Business; USD fields `Money = false`; auth-matrix test for money keys. | A.5, J.2, K.2 P1, L.2, M.2 #10 |
| 13 | Missing recompute triggers and FK rules (major) | The D.5 table now covers 30 rows, including preference changes, MoneyScale and currency locks, bale, shipment and batch deletes, key-field guard and lot split. B.13 defines the FK behaviour (NO ACTION rather than RESTRICT, so a tenant cascade still works). D.9 specifies split. The fuzz is extended. There is no permanent rug delete today (`RugsController:89` is a soft delete); the only hard delete, import undo, runs through the pass. | B.13, D.5, D.9, L.2 |
| 14 | Allocator "non-negativity" property is wrong (minor) | The property is now: the sign of T, and \|share\| ≤ \|T\|. The allocator splits \|T\| and reapplies the sign; −100 split three ways gives −34/−33/−33. | D.3, L.1 |
| 15 | JS and SQL rounding implementation (minor) | `num.ts` uses BigInt scaled integers with half-away rounding and no float multiplication. SQL stays `numeric` only. The snapped value 2.2999999999999998×1.65 is added to the vectors. | G.4, L.1, L.3 |
| 16 | Isolation test misses MVC endpoints (major) | Reflection now covers every controller in `Web/Controllers`, including Settings, with sample route values. It asserts 404 and that no ExportRun or file is written. | L.2, K.2 P6 |
| 17 | Exception mapping ownership and naming (minor) | P1 defines `DomainConflictException` (409) and `AppValidationException` (400), with match arms placed before `InvalidOperationException`. Later exceptions derive from them, so the filter is never edited again. | I.1, J.2, K.2 P1/P4a |
| 18 | Holes in the K.1 ownership table (minor) | Rows added for `appsettings.Development.json`, both csproj files, `DatabaseSeeder`, the manifest and sw.js, site.js, ui.ts, the Vue label sites (WP1-B), `AccountController` (WP1-A), and Rugs/Index (P1 first). `Seed:Demo` is declared in P1. The full icon list is enumerated in P1. | K.1, J.2, J.3 |
| 19 | Concurrency on settings and import (minor) | `DeclaredValuesStamp` for the replace-all PUT; a version on the grid-layout PUT; import commit only when `Previewed` and the xmin matches (409 otherwise). | B.3, I.2, I.3, F.7, L.2 |
| 20 | Migration and CI details (minor) | CI fails on xmin DDL; M2 and M7 are explicitly `[RequiresBackup("Rugs")]` (gated only on populated tables); `.config/dotnet-tools.json` pins dotnet-ef 10.0.10; EF 10's migration lock and pending-model-change throw are documented, along with why our advisory lock still exists. | B.15, J.1 #2–3 |
| 21 | Enums without explicit values (minor) | Every enum is listed with explicit values, plus an architecture test. `CurrencyUnit.Usd = 2` is whitelisted out of the tenant-currency select. | B.14, J.2, K.2 P2 |
| 22 | Cost entry has too many concepts for the merchant (major) | The Simple panel has three inputs (type tiles, amount, «برای» pre-filled) and one sentence. Method, FX, pins and provider sit under «بیشتر». AreaBasis never appears in Simple mode. The merchant's concept budget is 7 nouns and 2 states. | A.2, H.2, K.2 P5 |
| 23 | Purchase price can be entered in two places (minor) | Simple mode offers the lump purchase only from an intake batch, and `W_PURCHASE_DOUBLE` warns. | H.2, H.8, D.8 |
| 24 | Persian copy and bale term (minor) | `terms.baleOf` («نگلهٔ» / «عدلِ») and `baleFooter`; the dimension copy is rewritten; the duplicate-serial copy no longer repeats the number; the preset reports «موجود بود: {label}»; the reopen copy is rewritten. | H.3, H.8, I.6 |
| 25 | App context date has a Jalali value in ISO shape (minor) | `today` is Gregorian ISO ("2026-09-06"), plus `todayJalali` ("1405/06/15"). | I.6 |
| 26 | Per-phase verification cannot run as written (minor) | Each L.4 step is mapped to its first phase, with manual seeding before P7. Expected results are 302 → `/account/denied` for MVC and 403 for the API. | L.4, K.2 P1 |
| 27 | Home route and SystemAdmin (minor) | `[Authorize]` on Index only; the SystemAdmin redirect is kept; Error and StatusCodeError are `[AllowAnonymous]`. Added to the P1 acceptance. | I.4, J.2, K.2 P1 |
| 28 | Session revalidation scope (minor) | The validator also checks `Tenant.IsActive` and `SubscriptionExpiresAt`, using the same 5-minute cache. | A.1, J.1 #4, L.2 |
| 29 | Ledger stage ordering across modules (minor) | `LedgerStageOrder` constants with slots reserved for P4a and P5, plus an architecture test that orders are unique and `RugCostTotal` runs last. | D.4, K.2 P2, L.1 |
| 30 | TotalInvestment site mapping text is wrong (minor) | Corrected: RugRepository covers sites 2–4, ServiceProviderRepository 5–6, WorkflowEngine 1, the seeder 7. `CostSql` and `RugCostMath` are the only formula sites, enforced by an architecture test. | D.4, K.2 P2 |
| — | Excel-fidelity mismatches 1–7 | Lots per piece (#8); page breaks (#10); summary placement and format (#11); numeric serials, General format, single-row bales, empty bales (#11). All page-setup values are encoded in E.9. | E.7, E.9, E.10 |
| — | Self-found: `RugCostSummaryDto += (decimal PurchaseCost …)` duplicated an existing parameter | Only new members are appended; the existing `PurchaseCost`, `TotalProcessCost` and `TotalInvestment` are reinterpreted. | I.3 |
| — | Self-found: `RugDto` and `RugListItemDto` appends must be optional | The existing records already end with optional parameters, so appended members carry defaults. `AreaSquareMeters` reports the stored per-piece area, so there is no duplicate `AreaM2`. | I.1, I.3 |
| — | Self-found: deleting a batch (`RugBatchService:66`) silently orphans IntakeBatch-scoped entries | `CostEntry.BatchId` is SET NULL, and `BatchImpactContributor` (P5) makes those entries Unallocated with a warning. | B.13, D.5 #12 |
| — | Self-found: the P4b acceptance about the target bar was ambiguous | Now reads "amber above 14.3 m² (target 13 m² + 10 %)", consistent with C.2. | C.2, K.2 P4b |

Round 1 note: the self-found row «RugListItemDto appends must be optional» was wrong (CS0854) and is superseded by feas-cs0854 below.

### Round 2

Conflicts resolved: LM-01 and feas-pass-order were merged into one two-phase D.4. LM-02 and feas-purchasecost-roundtrip were merged; LM-02's 409 on a differing value was chosen over silently ignoring it, so a stale form can never pass unnoticed. XL-5 and ux-purchase-basis-grid were merged into one «Purchase columns» rule in G.2. ux-declared-value-missing and feas-settings-api-floor were reconciled by moving both the Floor read and the Business add to `/api/declared-values`. Numbers that changed were recomputed: D.7 #8 (603,425 / 1,165,425; A 44,182,783; C 80,877,467); the FX vector (38,272,700 vs 38,272,682); the money vector 45.55 × 0.16 → 7 × 50 = 350; the planner widths (734 px / 772 px → 579 pt, s = 0.9787, C = 43 / 44).

- XL-1: APPLIED. `sample-shipment.json` (P1, WP1-A, with the math tests) and `sample-rows-1-43.expected.json` (P6-engine); E.10, L.1, PageBreakPlanner and P8's SampleSheetBuilder read them; K.2 P1/P6/P8 updated.
- XL-2: APPLIED. E.2 defines title, group/footer (column keys only), summary, grandTotal, checkAgainst and styles, with validator rules; `shipment-declaration.v1` is written out in full in E.9.
- XL-3: APPLIED (option a). The print page renders each sheet as its own table, with the summary on its own last page and no main header; «چاپ» is the Output tab's primary button, with the whole-workbook note; `belowNewPage` was deleted (M.2 #15); P6 acceptance added.
- XL-4: APPLIED. G.4 «Next serial» rule, grid increment, the three modes, the 409 with `nextFree`; `SerialSuggester` in WP2-C; tests in L.1, L.2, P2, P3.
- XL-5: APPLIED-ALTERED. Merged with ux-purchase-basis-grid. A basis mismatch (the legacy M2 Total line) is shown through the unit chip («کل») instead of a read-only cell; the cell is read-only only with two or more lines of the category. Key = SeedKey (preset keys already start with `purchase.`) or `purchase.cat:{Id}`.
- XL-6: APPLIED. Covered merge cells read as empty, except bale-column vertical merges and splitter-eligible horizontal merges; F.4 lists `W_MISSING_FIELD` at 187/189/190 (bg_color and pattern); assertion added to P8 and L.1.
- LM-01: APPLIED-ALTERED. Merged with feas-pass-order: the fast-path decision is made in memory before BEGIN, but any guarded or ledger entity takes the locked path; contributors read after the lock; flush with `acceptAllChangesOnSuccess: false`; stages on flushed state; re-entrancy flag; same-request integration test added.
- LM-02: APPLIED. I.3 legacy rules for PurchaseCost and W/L; `RugDto.PurchaseCost` = Σ lines; RugForm stops sending them in P2; the round-trip test in L.2 and P2.
- LM-03: APPLIED-ALTERED. Guards run on every save that touches a guarded entity, but **under the ledger lock** rather than on a lockless fast path, so a concurrent finalize cannot slip between check and write. C.8 gained Rug.Status, workflow path edits and `BaleMembershipGuard`; D.5 #8/#15b/#19; per-field guard tests.
- LM-04: APPLIED. D.5 #2 split into 2a (intake size) and 2b (quantity, always); fuzz operation added.
- LM-05: APPLIED. Stage-level propagation rules in D.4; D.5 #16 and C.9 recompute line values on reopen; fuzz operation added.
- LM-06: APPLIED. P2 acceptance corrected to 350 (scale 0) / 364.50 (scale 2 and USD); vectors carry `kind` and `scale`.
- LM-07: APPLIED. D.3 «Receipts freeze», `CostEntry.FrozenAt`, `W_RATE_DRIFT`; D.7 #4 and #8 updated with recomputed numbers; P5 acceptance updated.
- LM-08: APPLIED. Residual with all members pinned (`W_PINS_RESIDUAL`), zero-weight on the unpinned subset, pins in base currency, `E_PINS_BASIS`, invariant Allocated + Unallocated = BaseAmount.
- LM-09: APPLIED. Weights clamped with `W_NEGATIVE_WEIGHT`; ByPurchaseCost weight = max(0, PurchaseCost) × 100; rates ≥ 0 (C.5); property test over −10⁹..10⁹.
- LM-10: APPLIED. D.3 «Step pricing on lots» table for every model; line-level override and adjustment; «برای کل دسته»; vectors per model.
- LM-11: APPLIED. Purchase group snapshots IntakeBatch membership; Expense group stays dynamic with a confirm and audit; D.5 #1/#4 and the fuzz updated.
- LM-12: APPLIED. B.4 «M2 dimension order» (backup, round, narrow to numeric(9,2), intake from the rounded pair, generated columns) plus the invariant check.
- LM-13: APPLIED. ChangeSetBuilder diffs values and ignores `IsModified`; "unknown" entities; architecture test against `Update()` on untracked ledger entities (B.1, D.4).
- LM-14: APPLIED. D.3 lot-rule table now covers Percent, PerPiece, foreign-currency and Toman↔Rial per-piece conversion, and Total/revenue; vectors added.
- feas-pass-order: APPLIED-ALTERED. Merged with LM-01 (see there); the bypass whitelist covers `Infra/Persistence/Ledger/*`, the per-feature `*LedgerRepository` classes and `TenantCounterRepository`.
- feas-purchasecost-roundtrip: APPLIED-ALTERED. All four rules applied, except that a PurchaseCost that differs from Σ lines on a multi-line rug returns 409 (LM-02) instead of being ignored, and a single legacy Total line is updated in place.
- feas-money-leak: APPLIED. A.5 and J.2 cover analytics, sales, step DTO money, the dashboard receivable, step-type and provider rates, and the 403 on money writes; L.2 and P1 acceptance extended.
- feas-stale-derived: APPLIED. D.4 step 8 detaches tracked derived entities; `AsNoTracking` loads; same-request freshness test.
- feas-changeset-diff: APPLIED. Same fix as LM-13; unit test for an UpdatedAt-only save on a finalized shipment's rug.
- feas-ambient-tx: APPLIED (both options). `IUnitOfWork.ExecuteInTransactionAsync` (P2) with the pass reusing the ambient transaction, and a deferrable bale-number unique constraint in M3 so a renumber is one flush.
- feas-cs0854: APPLIED. I.1 and I.3 corrected: appended members are passed explicitly in EF projections; DTO and projection owned by the same work package (WP2-B, P5).
- feas-p2-parallel-files: APPLIED. P2 merge order; `RugRepository.cs` and `EntityMappers.cs` edited by WP2-B only; `IRugRepository.cs` added to K.1.
- feas-gridsource-edge: APPLIED. `BalesGridSource` moved to P4b; the grid-batch lock check moved to P4b's acceptance.
- feas-embedded-json: APPLIED. The `EmbeddedResource` glob goes into `Application.csproj` in P0; the test csproj fixture-copy item and its owner are in K.1.
- feas-p4b-owned-files: APPLIED (extension-slot option). P1 adds the `[data-bulk-action]` slot and `bulk-actions/*.action.ts` glob; P4b adds `BarcodeScan.vue`; QrScanner keeps navigating with exact matching.
- feas-mvc-exceptions: APPLIED. `DomainExceptionPageFilter` in P1, registered globally; L.2 test for `POST /Rugs/Delete`.
- feas-tailwind-source: APPLIED. The C# `@source` lines go into app.css in P1, plus an architecture test.
- feas-xlsx-fit-breaks: APPLIED. E.6 #5 (print-preview break check) and #6 (fitToPage + scale); fallback to an explicit integer scale; intentional golden difference; L.4 manual check.
- ux-cost-basis: APPLIED. A 3-segment basis switch in Simple mode; P5 acceptance with the «کل» washing; M.2 #34 rewritten.
- ux-purchase-basis-grid: APPLIED-ALTERED. The unit chip reads «هر متر | هر تخته | کل» (the cost panel's words and the glossary's «تخته») instead of «متری | دانه‌ای | کل»; otherwise as proposed, including the P3 acceptance for D.7 #1–#3.
- ux-open-shipment-ambiguous: APPLIED. `HomeSummaryDto.OpenShipments`; tile targets for 0 / 1 / 2+ shipments; P4b acceptance.
- ux-current-bale: APPLIED. One «نگلهٔ جاری» model (C.2, H.1, G.5); bales are created by their first rug; new empty-bales copy.
- ux-final-size-flow: APPLIED. Focus flow to the final طول; descriptive columns not Tab stops in Simple mode; bulk «همان اندازه» from the readiness chip and the bulk bar; P4b keyboard acceptance.
- ux-tablet-entry: APPLIED. H.6 «Tablet entry» (inputmode, action bar, «اندازه» sheet, «اندازه‌گیری» preset, 44 px confirms); P4b tablet acceptance.
- ux-drag-only: APPLIED. The «انتقال» picker (button and key M) in H.1 and H.7; P4b acceptance; manual check in P9.
- ux-delete-undo: APPLIED. Row menu delete with undo toast and the Floor/Business policy (G.3, G.6); «خارج کردن از نگله» with undo; visible «برگرد» button; cost-delete undo toast; split preview. A one-click split undo is explicitly out of scope for v1 (D.9).
- ux-drafts-discard: APPLIED. New copy, danger confirm, 24 h `draft-discarded` slot with an undo toast; vitest case.
- ux-money-entry: APPLIED. Words line, «م» / «ه» suffixes, the «10.000» question at scale 0, the Toman/Rial toggle; vectors in L.1 and L.3.
- ux-dim-cm-ambiguity: APPLIED. `MaxPlausibleSideM` (default 12), the 13–30 question, the 60 m² warning, one confirm per paste; vectors.
- ux-declared-value-missing: APPLIED-ALTERED. The Business add lives at `POST /api/declared-values` instead of `/api/settings/declared-values/add`, because P1 asserts that every `/api/settings/*` is 403 for a Merchant (conflict with feas-settings-api-floor).
- ux-setup-checklist: APPLIED. New H.9 with server-computed steps; «آماده است» = steps 1–6 (step 7 is listed as recommended, consistent with the finding's own ready rule); the «برچسب شماره» label in the preset; P7 acceptance.
- ux-intake-batch-key: APPLIED. The (tenant, Iran day, seller) key on RugBatch; seller as a sticky grid column; the day view and the «ورودی‌های اخیر» strip; the Name format.
- ux-vocab-batch-collision: APPLIED. The «ورودی» relabel of the Groups screens and the bulk bar in P1, Simple-mode hiding, and the glossary test.
- ux-vocab-draft-collision: APPLIED. `Draft` → «ثبت‌شده», «در کارگاه» in Full only, the location chip «فروخته‌شده», and status hiding on /Rugs in Simple mode from P4b.
- ux-codes-copy: APPLIED. H.3b `Messages.fa` with every readiness, allocation, chip and import-option message, as partial files per feature (K.1 ownership), plus the no-Latin test.
- XL-7: APPLIED. F.5 snaps caches with `I_VALUE_SNAPPED`.
- XL-8: APPLIED. H = 1 on non-lot rows, the qty header style, footer fill A..last, and summary links by footer id; E.10 asserts.
- XL-9: APPLIED. Exact widths and column styles; shared-formula expansion and outer-edge border comparison; quotePrefix only on data-sourced text.
- XL-10: APPLIED. Title colour RGB `FF767171`; spike check.
- XL-11: APPLIED. `shrinkToFit` on data cells; print-page fitting; intentional difference.
- XL-12: APPLIED. `terms.*` tokens in the title, A2 and summary D2 (the same fix as ux-bale-term-propagation).
- XL-13: APPLIED. The Normal font stays Calibri 11; fallback table or extra slack; round-trip assertion.
- XL-14: APPLIED. A serial is numeric only when all digits, with no leading zero and at most 15 digits.
- XL-15: APPLIED. RFC 5987 `Content-Disposition` with a draft suffix.
- LM-15: APPLIED. B is «گبه» at #1; A and B are «همان اندازه» at #9; no number changes.
- LM-16: APPLIED. M2 back-fill, keep-on-edit, `ActualProfit = Net − (CostAtSale ?? Total)`, written by `RugCostTotalStage`.
- LM-17: APPLIED (UpdatedAt option). The purchase-lines PUT and purchase grid ops bump the rug's xmin; the concurrent-PUT test.
- LM-18: APPLIED. Explicit pt conversion and unfloored s, with worked numbers (and the fallback-mode numbers).
- LM-19: APPLIED. Summary links bind to footer ids, with both letter variants.
- LM-20: APPLIED. Undo blockers for dependent cost entries; StockOnly undo deletes its batch.
- LM-21: APPLIED. The D.5 #22 money list now covers payments, rates, defaults, target prices and revenue.
- LM-22: APPLIED. Ordinal lowercase-string tie-break and its test.
- LM-23: APPLIED. D.5 #25b and full warning recompute on every re-run.
- LM-24: APPLIED. D.5 #17 reads Bale-scoped targets before the flush.
- LM-25: APPLIED. `rug-list.v1` defined with `perPiece`, qty and SUMPRODUCT totals.
- feas-chain-down: APPLIED. `PendingModelSync.Down` drops the columns; Down() for M0–M7; CI up/down/up step.
- feas-settings-api-floor: APPLIED. The Floor read moved to `GET /api/declared-values`.
- feas-snapshot-jsonb: APPLIED. The snapshots are `text`; re-hash test.
- feas-obsolete-warnaserror: APPLIED-ALTERED. `[Obsolete]` lands in WP2-B's last merge, with a local `#pragma` on the EF mapping, instead of a `DiagnosticId` plus csproj `NoWarn` (both csproj files are frozen).
- feas-integration-infra: APPLIED. `Mvc.Testing`, `WebApplicationFactory<Program>`, a test auth handler, antiforgery handling (P0).
- feas-parallel-shared: APPLIED. More/Help views go to WP1-B; `ISettingsLink`; `[IsolationSample]`; a frozen types index; `/fonts/` pre-listed in sw.js by P0.
- feas-plan-edges-minor: APPLIED. The P7 ← P3 edge; P2 seeds the three purchase categories; `RugsGridSource` in P4b.
- feas-perf-300: APPLIED. D.4 «Performance», C.7/C.9 set-based, and the L.2 perf test.
- feas-k1-paths: APPLIED. The `RugListViewModel` path, `DashboardService` inside `WorkflowManagementService.cs`, and the `IProcessStepTypeLookup` note.
- feas-sstamp-resign: APPLIED. The stamp is carried in `AuthResponse`, `ToClaims` with the stamp, cache eviction, and the L.2/P0 test.
- ux-glossary-names: APPLIED-ALTERED. The full glossary and all renames are applied, but «صادر شده» keeps its existing spelling, and the xlsx summary sheet keeps the sample's name «خلاصه» (only the shipment tab is renamed).
- ux-count-unit: APPLIED. Σqty in تخته everywhere, with the row count in brackets when a lot is present.
- ux-copy-polish: APPLIED. All nine strings.
- ux-bale-term-propagation: APPLIED. Template tokens, grid labels, the preset category name, the Adl golden (E.10 #4).
- ux-icons: APPLIED. Existing icons are reused; `bale` and the category icons are added; the preset sets `Icon`; the sprite test.
- ux-text-scale-control: APPLIED. The text-size control (A.3), `Width` in ch, row heights.
- ux-operator-dead-ends: APPLIED. Role-aware empty states, the tile and lock copy; «[ثبت هزینه]» on the empty costs state.
- ux-cost-panel-entry-points: APPLIED. The Intake footer button, the Home pre-fill order, the bale picker for «افزودن به نگله».
- ux-shipment-simple-screens: APPLIED. One-tap create, the header «قطعی کن», the Simple Output tab, the «حساب محموله» tab, and the pack-mode link with its QR.
- ux-darkmode-tokens: APPLIED. Footer and target-bar tokens, the `.paper` preview surface, L.4 #10.
- ux-jalali-field: APPLIED. Persian digits and separators, invalid-date and Gregorian-year messages, the date-following intake label, fixture cases.
- ux-cost-panel-offline: APPLIED. The failure state, the same `ClientKey` on retry, the draft, `blockReload`.
- ux-a11y-controls: APPLIED. Radiogroups, `role="meter"`, chip buttons with `aria-describedby`, 44 px targets.
- ux-usability-checkpoints: APPLIED. The K.1 rule and the P3/P4b/P5 acceptance lines.
- ux-import-simple: APPLIED. The F.1 Simple path and the P8 acceptance.

Totals: 96 findings, of which 87 were APPLIED, 9 were APPLIED-ALTERED and 0 were REJECTED.
