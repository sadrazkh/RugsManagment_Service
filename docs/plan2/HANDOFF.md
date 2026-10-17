# Plan 2 handoff: «بستهٔ صادرات فرش» (export rug workflow)

> **2026-10-18 import UX follow-up:** blockers now appear automatically with exact Excel/related row numbers, inline correction and explicit reversible row skipping. Decisions survive reopen/refresh, stay isolated by sheet and require a fresh preview before commit. Reconciliation uses included rows. No new migration. Release build clean; P8 API16/16, planner20/20, frontend566/566. CUA tested160-row duplicate case through skip, reload, unskip, correction and successful commit. See `reports/import-decisions-integration-20261018.md`.

> **2026-10-17 continuation — this status supersedes the historical WIP status below.**
> P4b/P5/P6-wiring/P7/P8/P9 implementation is complete in `codex/finish-export-workflow`, based on `da29500`.
> All 25 confirmed wave-2 findings and the previously unverified concurrency finding were addressed; six further import-review findings and a browser measurement race were fixed.
> Release `-warnaserror`: 0 warnings/errors; unit **944/944**, architecture **15/15**, PostgreSQL integration **387/387** (no skips), frontend **554/554**, production frontend build successful. Pending-model check clean, no xmin DDL. Performance threshold unchanged: measure best **178 ms**, finalize/reopen **16/17 SQL commands**.
> M4 `20261017044805_CostLedger`, M5 `20261017045100_Documents`, M6 `20261017045204_Import`, M7 `20261017091136_DropRugPurchaseCost` are generated and verified up/down/up. Applied M0–M3 were not modified.
> Real owner XLSX parsed with zero errors: **36 / 161 / 242 / 464.74 / USD 20,322.50**, all reconciliation rows OK. Synthetic sample imports into DB, exports to XLSX and re-import previews with the same exact totals.
> Real DB backup `work/backups/rugs_20261017_131516.dump` (outside Git, **90,193 bytes**, SHA256 `E091389BA2AF255D819258269EC929A9F3C0A4D4B1D2A849A1D37B3119F623C1`) restored into a disposable replica and upgraded through M7: **6 totals / 344,937,269.00**, unchanged. **Owner subsequently explicitly confirmed the backup and real upgrade. Original DB now at M7**: fresh pre-upgrade backup `work/backups/rugs_before_m7_owner_confirmed.dump`, **90,193 bytes**, SHA256 `45F30EC4F46165CA1C76EE08996F2E74C63729C63B40E6DCD61C5E2F604B56D8`. Committed snapshot `9eb0d18` ran once with `Seed__Demo=false` and M7 backup confirmation under the startup lock. All four migrations succeeded; both tenants' ledgers healthy (5 + 1 rugs), **6 totals / 344,937,269.00 unchanged**.
> Browser scripted workflow reached print with 10 intake rows (including a 50-piece lot), two bales and freight. Home/Intake/Shipment/Costs Lighthouse accessibility **100** each. Final axe audit: **32/32 cases, zero violations and no document overflow**, eight screens × light/dark × 375/1024 px. Actual father acceptance and unavailable Excel/LibreOffice UI verification remain human checks.
> Current evidence: `reports/p5-resumed.md`, `reports/p6-p7-resumed.md`, `reports/p4b-review-p9-resumed.md`, `reports/p8-adversarial-costs.md`, **`reports/p8-p9-final-integration.md`**. Read these before the historical sections.
> Delivery: implementation commit **`dc94f21`**, real-upgrade evidence **`6aa78ac`**, fast-forwarded into the original checkout's **`feature/export-workflow`** and pushed after explicit owner authorization. Original-path Release build and frontend production build succeed. **Draft PR [#1](https://github.com/sadrazkh/RugsManagment_Service/pull/1)** targets `master`, mergeable without conflicts. Master is not directly changed. Remote validation: [CI runs](https://github.com/sadrazkh/RugsManagment_Service/actions/workflows/ci.yml); do not infer its current outcome from local test counts.


**Start here.** This file describes:
- the state of the plan-2 work on branch `feature/export-workflow`;
- what is done and what is left;
- the rules a new session (human or AI) must follow to finish it;
- the final review checklist.

Everything referenced lives in this folder (`docs/plan2/`), in the repo.

Last updated: 2026-10-16, by the Claude Code session that ran waves 0–3.

---

## خلاصه برای صاحب پروژه (فارسی)

- **هدف:** سامانه برای پدرت ساده و همه‌کاره شود. پدرت تاجر فرش صادراتی است و فنی نیست.
- **روال کار:** فرش وارد می‌شود، اندازه و قیمت خرید چندجزئی ثبت می‌شود. موقع نگله‌بندی اندازهٔ نهایی ثبت می‌شود. هزینه‌ها هر متر، هر تخته یا یکجا وارد و خودکار سرشکن می‌شوند. آخر کار خروجی اکسل و چاپ مثل همان «صورت نگله‌بندی» می‌گیرد. قالب خروجی را هم می‌شود طراحی کرد.
- **انجام‌شده، کامیت‌شده و پوش‌شده:** فاز ۰ تا ۳، فاز ۴الف، موتور خروجی فاز ۶، و رفع همهٔ ایرادهای بازبینی فاز ۱ و ۲.
  - همهٔ تست‌ها سبزند.
  - دیتابیس واقعی تا migration فاز ۴الف (M3) جلو رفته و هیچ عددی عوض نشده.
- **نیمه‌کاره:** فاز ۴ب (صفحهٔ نگله‌بندی) و فاز ۵ (دفتر هزینه و سرشکن) تا جایی نوشته شده‌اند و در یک کامیت «WIP» پوش شده‌اند.
  - برنامه فعلاً روی این کامیت **بالا نمی‌آید**، چون بخش Infrastructure فاز ۵ نوشته نشده.
  - آخرین نقطهٔ سالم کامیت `82e2383` است.
- **مانده:** تمام کردن ۴ب و ۵، اتصال خروجی اکسل به دیتابیس (۶)، طراح خروجی و پیش‌تنظیم (۷)، ورود از اکسل (۸)، و پرداخت نهایی و دسترس‌پذیری (۹).
  - ۲۵ ایراد تأییدشدهٔ بازبینی موج ۲ هم باید رفع شوند (`wave2-review.json`).
  - در پایان یک بازبینی کامل لازم است.
- **کارهایی که فقط خودت یا پدرت می‌توانید بکنید:**
  - آزمون کاربری با پدرت در فازهای ۳، ۴ب، ۵ و ۹;
  - تأیید گرفتن بکاپ پیش از M7 (حذف ستون قدیمی قیمت خرید);
  - چند تصمیم که در بخش ۹ آمده.

---

## 1. Where everything is

| What | Where |
|---|---|
| Branch | `feature/export-workflow` on `https://github.com/sadrazkh/RugsManagment_Service`. PR not opened yet: `https://github.com/sadrazkh/RugsManagment_Service/pull/new/feature/export-workflow` (no `gh` CLI on the owner's machine). |
| Last **green** commit | `82e2383` "Wave 2: …". Release build `-warnaserror` clean, unit 909/909, architecture 15/15, integration 352/352, vitest 468/468 (29 files), `npm run build` ok, `has-pending-model-changes` clean. |
| WIP commit (on top of the green one) | `e21615f` "WIP wave 3 …". Partial P4b and P5; builds, but the **app does not start** (DI: `ICostLedgerRepository`/`ICostEntryRepository` have no Infrastructure implementation yet). See §4. |
| Spec (single source of truth) | `final-spec.md` (3,320 lines) + `spec-errata.md` (E1–E18; **errata override the spec**) |
| Owner's original request + persona notes | `brief.md` |
| Pre-plan code map (stale in places; always read real files) | `map.md` |
| Rules every agent must follow | `hard-rules.md` |
| Per-phase implementation briefs | `p2-brief.md`, `p3-brief.md`, `p4a-brief.md`, `p4b-brief.md`, `p5-brief.md`, `p6e-brief.md` (engine), `p6w-brief.md` (wiring), `p7-brief.md`, `p8-brief.md`, `p9-brief.md`, `p1fix-brief.md`, `fix-brief.md` (generic review-fix brief) |
| Review results | `p1-review.json` (fixed), `p2-review.json` (fixed), **`wave2-review.json` (OPEN: 25 confirmed + 1 unverified, review of `82e2383`)** |
| Agent reports (what each package did, verification, "needs from others") | `reports/*.md` |
| Merchant's real sheet, dumped (every cell + formula; no personal data) | `xlsx/dump.txt`. The real file is outside the repo: `C:\Users\sadra\Desktop\baba-1404-08-11 (2) (2).xlsx`. Never commit it. |
| Orchestration scripts (Claude Code `Workflow` tool) | `workflows/wave3.js` (the last wave, use as the template), `workflows/wave2-example.js`, `wf-review.js` (adversarial review child workflow) |
| Real-DB backups | **Local only, never in git:** `C:\Users\sadra\rugs-plan2\backups\` (`rugs_management_at_m2_2026-10-15.dump`, `rugs_management_before_m3.dump`). Older backups were lost (see §8). |

## 2. The product in one paragraph

Two personas (spec A.1–A.3):
- **The merchant** (Merchant role «بازرگان», Simple mode, large text). The owner's father: an elderly bazaar rug exporter, used to Excel and paper.
- **The configurator** (the owner). Sets fields, cost categories, declared-value table, numbering and templates once.

The flow (spec A.6):
1. **Intake grid**: Excel-like, keyboard and paste. Serials and lots («3911 - 3960» × 50), intake size, purchase price as dynamic lines (per m² / per piece / total / percent).
2. **Shipment → bales (نگله/عدل)**: record the final size per rug, with live bale totals against a 13 m² target.
3. **Costs**: per m², per piece or lump. Scope is rug / rugs / bale / shipment / intake batch / stock. Split by area (intake or final), pieces, equally or by purchase cost. Largest remainder; everything re-splits automatically through the Consistency Pass.
4. **Output**: the «صورت نگله‌بندی و اظهار فرش صادراتی» xlsx with live formulas, plus a print page, from designable templates.
5. **Import** of the merchant's old Excel sheets with all their quirks.

The default output must reproduce the merchant's sheet (`xlsx/dump.txt`) cell for cell, minus its hand-made errors. Full sample: 36 bales, 161 lines, 242 pieces, 464.74 m², 20,322.50 USD.

## 3. Phase status

The phase plan, file ownership and acceptance per phase are in spec **K.1/K.2**. Migration order is M0 → … → M7 (spec B.15).

| Phase | Content | Status | Migration | Real DB |
|---|---|---|---|---|
| P0 | Repair: migration chain, startup pipeline + advisory lock, `[RequiresBackup]` gate, SecurityStamp, cross-tenant fixes, CI DB job | **Done**, reviewed, fixed (P0b) | M0 `20261003195050_CountersAndSecurityStamp` | applied |
| P1 | Platform: Merchant role, Floor/Business/Configure policies, Simple/Full mode, text scale, nav/home tiles, UI kit (NumberField, JalaliDateField, Combobox…), money visibility/redaction | **Done**, reviewed; 16 defects fixed in wave 2 | M1 `20261003234036_PlatformPreferences` | applied |
| P2 | Rug v2 (serials, lots, intake/final size, purchase lines, cost categories), **Consistency Pass** (`App/Ledger`), `RugCostTotal`, `CostSql`/`RugCostMath` single formula sites, rug API/islands, ledger health | **Done**, reviewed; 25 defects fixed in wave 2 | M2 `20261004063353_RugModelV2AndCosting` (`[RequiresBackup]`, self-verify) | applied 2026-10-04; totals identical |
| P3 | Excel-like grid (`components/grid/**`), intake page, rugs grid, grid layouts, batched save `POST /api/grid/rugs/batch`, seller/day batches | **Done** (wave 2). Review findings open (`wave2-review.json`, lens `grid`/`frontend`) | — | — |
| P4a | Shipments, bales, lines, declared values, readiness, finalize + snapshot hash, reopen, lock guards, workspace shell (`IShipmentTab`) | **Done** (wave 2). Review findings open (lens `shipments`/`money-usd`) | M3 `20261004194543_Shipments` | applied 2026-10-15; totals identical |
| P6-engine | Layout + validator, expression language, composer, page-break planner, ClosedXML writer with decimal-exact cached values; goldens match the sheet | **Done** (wave 2). Real Excel confirmed: values shown before recalculation, page 2 starts at bale 11. Review findings open (lens `documents`) | — | — |
| P4b | Baling board, pack mode (phone), bale components, barcode scan, BalesGridSource, Home open-shipment/readiness cards, location chips | **WIP, unverified** (§4) | — | — |
| P5 | Cost ledger + allocation, cost panel (Simple/Full), costs page, shipment costs/summary tabs, provider receipts, profit | **WIP**: Domain + Application + Web + Vue written, **Infrastructure + M4 + tests missing** (§4) | M4 `CostLedger` (not created yet) | — |
| P6-wiring | Documents from the DB: templates/revisions/export runs, xlsx download, print page, output tab | **Not started** | M5 `Documents` | — |
| P7 | Designer, `/settings/setup` checklist, «بستهٔ صادرات فرش» preset, cost template | Not started | — | — |
| P8 | Excel import + undo | Not started | M6 `Import` | — |
| P9 | Polish, help, Jalali inputs, a11y, `Seed:Demo`; **M7 drops `Rugs.PurchaseCost`** | Not started | M7 `DropPurchaseCost` (`[RequiresBackup]`) | — |

Commits so far:
- `5b2e98a` P0–P1
- `2b5870c` merge of master
- `d45368b` P2
- `82e2383` wave 2: P3 + P4a + P6-engine, plus the P1/P2 review fixes
- `e21615f` WIP wave 3
- this handoff commit

## 4. Exact state of the WIP (wave 3)

Wave 3 ran three things in parallel:
- an adversarial review of `82e2383`, which **finished**: `wave2-review.json`;
- P4b (`p4b-ui`, `p4b-web`);
- P5 (`p5-domain`, `p5-web`), with P6-wiring after it.

The agents hit the account's weekly usage limit mid-work. None of the wave-3 implementation agents reported, so **everything below is unverified and must be audited before it is trusted.**

**Verified at the WIP commit:**
- `dotnet build RugsManagment_Service.slnx -p:NuGetAudit=false`: 0 errors, 0 warnings.
- `npm run build`: passes.
- **The app and the integration tests do not start.** DI cannot resolve `RugsManagment.Application.Features.Costs.Ledger.ICostLedgerRepository` or `RugsManagment.Application.Features.Costs.ICostEntryRepository`, so every service that depends on the ledger pass fails.

**P5: present.**
- Domain:
  - `Domain/Entities/{CostEntry, CostEntryTarget, CostEntryPin, CostAllocation, CostEntryResult}.cs`
  - `Domain/Enums/{AllocationState, ProviderWorkKind}.cs`
- Application:
  - `App/Features/Costs/{CostAllocator (498 lines), CostEntryService (874), CostCategoryService, TargetResolver, ProfitService, CostFieldContributor, CostBreakdownDecorator, CostSplitDecorator, ICostEntryRepository, CostsModule}.cs`
  - `App/Features/Costs/Ledger/{AffectedEntryFinder, BatchImpactContributor, ByPurchaseCostExpander, CostCategoryGuard, CostEntryImpactContributor, CostLedgerVerifier, CostWarningJson, EntryRunner, ExpenseEntryStage, ICostLedgerRepository, PurchaseEntryStage, RevenueShareStage}.cs`
  - `App/DTOs/Costs/*`, `Messages.fa.Costs.cs`
  - `ProviderDtos.cs` and `ServiceProviderRepository.cs` (modified)
- Web:
  - `CostsController`, `Api/{CostsApiController, CostCategoriesApiController, ShipmentMoneyApiController}`
  - `Settings/CostCategoriesController` + view, `CostCategoriesSettingsLink`
  - `Shipments/{CostsTab, SummaryTab}` + partials, `RugDetails/CostsPanel`
  - `Home/Cards/MoneyCard` + view, `Navigation/CostsNavContributor`
  - Sales date-range fix
- Vue:
  - `panels/CostEntry.panel.vue` (1,019 lines)
  - `components/costs/{TargetPicker, MethodChooser, AllocationPreviewTable, CostList, costText(+spec)}`
  - `islands/costs/{CostLedger, ShipmentCosts, ShipmentSummary}`, `islands/settings/CostCategoriesManager`
  - `bulk-actions/CostEntry.action.ts`, `lib/{types,messages}/costs.ts`, `ProviderStatement.vue` (modified)

**P5: missing.**
- `Infra/Persistence/Configurations/CostLedgerConfigurations.cs`.
- `Infra/Persistence/Repositories/{CostEntryRepository, CostLedgerRepository}.cs`. `CostLedgerRepository` must also register the real `ICostSourceProvider` that replaces `NoCostSources`.
- The body of `Infra/Features/Costs/CostsInfrastructureModule.cs` (`AddCostsModule`).
- Migration **M4** `CostLedger` + `Migrations/HandEdits/CostLedger.md`.
- All P5 tests:
  - `tests/RugsManagment.Tests/P5/**`: D.3 rules and the D.7 vectors appended to `math-vectors.json`;
  - `tests/RugsManagment.IntegrationTests/P5/**`;
  - the 200-random-operations `cost-consistency` property test.
- `p5-integrate`.

**P4b: present (all unverified).**
- Application: `App/Features/Shipments/Grid/BalesGridSource.cs` (+ registration in `ShipmentsModule.cs`), `RugsGridSource.cs` (location column).
- Web:
  - `Controllers/ShipmentPackController.cs`, `Shipments/BalesTab.cs`
  - `Views/Shipments/{_TabBales, Pack}.cshtml`, `Views/Shipments/Index.cshtml` (island tag)
  - `Navigation/ShipmentsNavContributor.cs`
  - `Home/Cards/{OpenShipmentCard, ReadinessCard}.cs` + views
  - `Views/Rugs/{_Details.Location, _Details.LocationChip}.cshtml`, `Rugs/{Index, Details}.cshtml`
- Vue:
  - `islands/shipments/{BalingBoard (1,476 lines), PackMode, ShipmentHeader, ShipmentList, shipmentShell(+spec)}`
  - `components/bales/{AddToBaleFlow, BaleCard, BaleFooterRow, BaleMovePicker, BaleTargetBar, CurrentBaleHeader, FinalSizeSheet, LotSplitDialog, StockPicker, baleModel(+spec), useBalingBoard}`
  - `components/scan/BarcodeScan.vue`, `bulk-actions/AddToBale.action.ts`, `lib/types/bales.ts`, `lib/messages/shipments.ts` (extended)
- Tests: `tests/RugsManagment.IntegrationTests/P4b/P4bWebTests.cs`.

**P4b: missing.** Verification, `p4b-integrate` (browser acceptance walk), and the timed walkthrough.

**P6-wiring:** nothing exists yet. Its "needs" are listed in `reports/wave2-global.md` §4.2:
- register `IXlsxDocumentWriter` → `ClosedXmlDocumentWriter`;
- map the DB and the finalize snapshot to `DocumentData`;
- pass the Document field catalogue as `ComposeOptions.Fields`;
- add a styles member to the rendered DTO.

## 5. What to do next, in order

Run work in parallel wherever the briefs say the packages are disjoint (spec K.1 ownership). One orchestrator commits and pushes after each wave. Agents never touch git state.

### Wave 3 (finish)

1. **P5 Infrastructure first** (unblocks the app):
   - configurations, repositories, real `ICostSourceProvider`, `AddCostsModule` body;
   - **M4** `CostLedger` via `dotnet ef migrations add CostLedger --project src/RugsManagment.Infrastructure --startup-project src/RugsManagment.Web`; remove generated xmin DDL by hand and document it in `HandEdits/CostLedger.md`;
   - then the P5 tests.

   Brief: `p5-brief.md`, package "p5-domain". Audit the existing Application code against spec D.2–D.8 while doing it.
2. **In parallel:**
   - audit/finish **P4b** (`p4b-brief.md`, both packages, then "p4b-integrate");
   - audit/finish the **P5 web** side, then "p5-integrate". Acceptance is spec K.2/P5, including D.7 #1–#12 exactly and the freight re-split 2,119,302 / 3,879,728 / 4,000,970 in the same request.
3. **P6-wiring** after P5 (`p6w-brief.md`, packages "p6w-server" ∥ "p6w-web", then "p6w-integrate"). Migration **M5** `Documents` goes after M4.
4. **Global integration**:
   - Release `-warnaserror` build;
   - `has-pending-model-changes` clean;
   - all unit/architecture/integration tests and vitest;
   - a browser walk of the whole flow: intake → bale → cost → export/print.
5. **Fix `wave2-review.json`** (`fix-brief.md`). Re-verify the one `unverified` finding first: `LineService.cs:393`, bale number/Seq picked before the tenant lock.
6. Commit and push. Apply **M4 + M5** to the real DB (§7).
7. **Review wave 3** with `wf-review.js` (sha = the wave-3 commit, baseSha = `82e2383`). Lenses:
   - baling UI;
   - cost allocation math and propagation;
   - ledger/Consistency Pass interaction;
   - documents wiring;
   - security/tenant/money visibility;
   - frontend UX.

### Wave 4

- Run in parallel:
  - fix the wave-3 review findings;
  - **P7** (`p7-brief.md`);
  - **P8** (`p8-brief.md`, migration M6);
  - **P9** packages "p9-content", "p9-ui", "p9-a11y" (`p9-brief.md`).
- Then the global integration.
- Then **"p9-m7"** alone: migration M7 drops `Rugs.PurchaseCost`. It is `[RequiresBackup]`; the owner confirms the backup.
- Then "p9-integrate", the end-to-end acceptance and the timed «father» walkthrough.
- Commit and push. Apply M6, then M7 after a fresh backup and the owner's OK.

### Wave 5

- A final adversarial review of the whole plan: `wf-review.js` with baseSha `2b5870c` and sha = HEAD, all lenses.
- Fix the findings, then run the final verification (§6).
- Update this file, push, and open the PR into `master`.

## 6. How to verify (exact commands)

```bash
dotnet build RugsManagment_Service.slnx -c Release -warnaserror -p:NuGetAudit=false
dotnet ef migrations has-pending-model-changes --project src/RugsManagment.Infrastructure --startup-project src/RugsManagment.Web
dotnet test tests/RugsManagment.Tests -p:NuGetAudit=false
dotnet test tests/RugsManagment.ArchitectureTests -p:NuGetAudit=false
RUGS_TEST_PG="Host=localhost;Port=5432;Username=postgres;Password=7082" dotnet test tests/RugsManagment.IntegrationTests -p:NuGetAudit=false
cd src/RugsManagment.Web/ClientApp && npm ci && npm run build && npx vitest run
```

**Running the app on a throwaway DB:**
- Set `ConnectionStrings__DefaultConnection` to a `rugs_<x>` DB, plus `ASPNETCORE_ENVIRONMENT=Development` (`Seed:Demo` true) and `ASPNETCORE_URLS=http://localhost:5299`.
- Run `dotnet run --project src/RugsManagment.Web --no-launch-profile`.
- Startup migrates, seeds the demo tenant and runs `LedgerStartupCheck`.
- Drop the DB afterwards.

**Persian test data:** never through Git Bash (it becomes `?????`). Send it through the browser (`fetch` + `JSON.stringify`) or write it with an editor.

## 7. Applying migrations to the owner's REAL database (`rugs_management`)

Only the orchestrator does this, one migration batch per committed wave:

1. **Back up** to a durable folder, never `%TEMP%`:
   ```bash
   pg_dump -h localhost -U postgres -Fc -f C:/Users/sadra/rugs-plan2/backups/rugs_management_before_<mN>.dump rugs_management
   ```
   (PostgreSQL 18 tools are in `C:\Program Files\PostgreSQL\18\bin`; there is also `scripts/db-backup.{sh,ps1}`.)
2. Export the committed snapshot with `git archive <sha>` to a folder outside the repo, then build `src/RugsManagment.Web`.
3. Run that snapshot **once** against the real DB with these settings:
   - `ASPNETCORE_ENVIRONMENT=Development`
   - `Seed__Demo=false` (**never seed demo data into the real DB**)
   - `Database__ConfirmedBackupFor=<the [RequiresBackup] migration id>` (needed for M2 and M7)
   - a spare port

   Startup applies the pending migrations under the advisory lock, then `TenantSeedRunner` (Order 100) and `LedgerStartupCheck` (Order 200), which rebuilds or verifies `RugCostTotal`.
4. Verify with `psql`:
   - `__EFMigrationsHistory` shows the new migration;
   - `select count(*), sum("Total") from "RugCostTotals"` is unchanged unless the migration is meant to change money;
   - the ledger check says «سالم است».

   As of M3: 6 rugs, sum 344,937,269.
5. Stop the app and delete the snapshot folder.

**Never** edit an applied migration (M0–M3). **Never** run tests against `rugs_management`.

## 8. Rules that must be respected

The full list is in `hard-rules.md`. The most important:

- **Spec + errata are the source of truth.** Read the sections the brief lists before coding. Errata override the spec.
- **Math:**
  - decimal/numeric only, never double;
  - one rounding rule everywhere, half away from zero (C# `MidpointRounding.AwayFromZero`, SQL `round(numeric)`, TS `lib/num.ts`);
  - area per piece = round(L×W, 2), then × qty;
  - USD per piece = round(rate × pieceArea, 2), then × qty;
  - Toman/Rial money and USD are never mixed;
  - every rule has a row in `tests/fixtures/math-vectors.json`, tested by both C# and vitest.
- **Consistency Pass:**
  - every write goes through `IUnitOfWork.SaveChangesAsync` (two-phase pass under the per-tenant advisory lock);
  - no `ExecuteUpdate`/raw SQL writes outside the whitelisted ledger repositories;
  - new cost behaviour goes in as NEW contributor/guard/stage files in the reserved `LedgerStageOrder` slots;
  - the architecture tests enforce this.
- **Money visibility:**
  - Operators never receive Toman money in any page or JSON;
  - non-null money members in a non-Business request → 403 (E5: nulls are ignored);
  - after enrichers, `RugDtoBuilder` redacts again.
- **Tenant isolation:** every query filters by tenant; a cross-tenant id → 404.
- **Migrations:**
  - B.15 order;
  - remove generated xmin DDL by hand and document it in `Migrations/HandEdits/`;
  - `[RequiresBackup("Rugs")]` on destructive ones (M2, M7);
  - never touch applied ones.
- **DTOs:** append trailing optional members only. In EF projections pass every member explicitly; this SDK does not raise CS0854, so a forgotten member silently projects its default.
- **Enums:** append-only, explicit values (architecture test).
- **Frontend:**
  - CSP: no inline scripts or handlers; `nonce` on `<script>`;
  - Tailwind tokens only, icons only from `/icons.svg`;
  - RTL, Persian digits, 44 px targets, dark mode;
  - `NumberField`/`JalaliDateField` for inputs;
  - every bale word comes from `terms` (نگله vs عدل);
  - islands `islands/**/*.island.vue`, panels `panels/*.panel.vue`, bulk actions `bulk-actions/*.action.ts` (never edit `RugBulkToolbar.vue`).
- **Build:** always `-p:NuGetAudit=false`. The owner's global NuGet config has an unreachable private feed (NU1900).
- **Durable storage:** plans, reports and DB backups never live in `%TEMP%`. Windows Storage Sense deleted the original planning folder on 2026-10-12. It was rebuilt from transcripts, and this repo copy is now the source.
- **Working agreement:**
  - The owner normally commits everything himself.
  - For plan 2 he approved continuous building, with commit and push per wave on `feature/export-workflow` and a PR into `master` at the end.
  - Never commit to `master` directly.
  - Never push large binaries or the merchant's real xlsx.

## 9. Open decisions and things only the owner can do

1. **Usability checkpoints with the father** (spec K.1). Agents only did timed scripted walkthroughs.
   - P3: intake of 10 rugs, including the lot «3911 - 3960».
   - P4b: baling them into 2 bales with final sizes.
   - P5: two costs, freight and washing.
   - P9: the whole flow in under 20 minutes without help.
2. **Backup confirmation before M7** (drops the legacy `Rugs.PurchaseCost` column).
3. **Decisions left open by the agents:**
   - **Sticky purchase price in the intake grid.** A new row with no price silently takes the previous row's price (spec G.2). Keep or ask on each row?
   - **Number formatting.** It is inconsistent: the server shows «٬» and «٫», the client shows «,» and «.», and `FormatNumber` drops trailing zeros («۸٫۵» instead of «۸٫۵۰»). P9 candidate.
   - **Money-scale lock.** It ignores a Percent default rate on a cost category; otherwise the seeded «دلالی ۲٪» would lock every new tenant. Deviation kept.
   - **LibreOffice check of the xlsx** (E.6 #3/#5). Not verified, because LibreOffice is not installed. Excel itself was verified.
4. **Real-data housekeeping:**
   - one rug photo is missing on disk (`App_Data/uploads` did not move with the repo);
   - the old demo batch in the real data is named «محمولهٔ نمونه».

## 10. How the work was orchestrated (for an AI continuing it)

- **Per-wave model:** disjoint packages in parallel → a lane integration agent → a global integration agent → a review-fix agent. Meanwhile an adversarial review of the previous wave's commit runs on a frozen `git archive` snapshot. Each finding needs two skeptics, and a finding is confirmed when the "real" votes are at least half of the votes cast and there is at least one. The orchestrator commits and pushes, then applies migrations to the real DB.
- **Prompts:** every agent gets `hard-rules.md` plus its brief plus "who else is running" (the `WAVE` text in `workflows/wave3.js`). Agents write their report to `reports/<label>.md`.
- **Interrupted agents:** when an agent dies (network error, session end, usage limit), re-run it with the RESUME note: audit what is on disk, finish, verify, and report what was already done versus done now. Never start over on top of partial work.
- **Reviews:** `wf-review.js` returns `{confirmed, rejected}`. Save the result as `<wave>-review.json` here before doing anything else.

## 11. Final review checklist (for the reviewer at the end)

- [ ] Every K.2 acceptance bullet for P3–P9 passes, with evidence (tests or a browser walk) recorded in `reports/`.
- [ ] D.7 worked example #1–#12 reproduces exactly; the cost-consistency check is 0 after random operations; ledger health is 0 on the demo and on the real DB.
- [ ] The full sample exports to 36 / 161 / 242 / 464.74 / 20,322.50; the goldens hold; import of the generated sample reconciles; the real file previews correctly.
- [ ] No Operator money leaks (auth-matrix tests); cross-tenant 404 on every new endpoint; Business/Configure gates per spec I.2.
- [ ] All review findings (P1, P2, wave 2, wave 3, wave 4, final) are fixed or explicitly rejected with evidence.
- [ ] Release `-warnaserror` build, `has-pending-model-changes`, unit/architecture/integration tests, vitest and `npm run build` are green; CI is green on the PR.
- [ ] The real DB is at the last migration, backups exist for each step, and the totals are verified.
- [ ] The UI works at 375 px and 1024 px, in dark mode, keyboard-only, with no drag-only actions and a clean CSP console; axe shows no serious issues.
- [ ] The owner's open decisions (§9) are answered and the father's walkthroughs are done.
- [ ] This file is updated, and the PR is opened into `master` with a summary.
