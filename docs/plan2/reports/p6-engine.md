# P6-engine report: document engine + xlsx writer (pure; no DB, no migration)

The package is complete and verified. It builds with `-c Release -warnaserror`, every engine test passes, and the golden comparison against the merchant's sheet has no differences. Real Excel confirmed the gating spike items: values show before recalculation, and page 2 starts at bale 11. LibreOffice is not installed, so that check is still open, and the unit-test project in the shared working tree is blocked by another package's half-written file (§7).

This was a resumed run. Most of the package was already on disk from the stopped attempts. I audited all of it against E.1–E.11, D.3, A.2/I.6, H.3b, K.2/P6, L.1 and errata E8, finished what was missing, fixed the defects, and verified it again. The same report is in `docs/plan2/reports\p6-engine.md`.

## 1. Found on disk vs done in this run

**Already on disk (from the earlier attempts, uncommitted):** every file in the package.
- App: Layout, Validator, 6 Expressions files, 4 Composer files, 4 DocumentData files, the 2 Defaults JSONs, `IXlsxDocumentWriter`.
- Infra: `ClosedXmlDocumentWriter`, `CachedValueInjector`, ClosedXML 0.105.1 + DocumentFormat.OpenXml 3.1.1 in `Infrastructure.csproj`.
- `tests/fixtures/sample-rows-1-43.expected.json`.
- 7 unit test files and 2 integration test files.

State of that work when I found it:
- Unit tests: 131 of 132 passed. The failing one was the expression limit test, and the test itself was wrong (a 998-digit number literal overflows `decimal`).
- Integration tests: 9 of 10 passed. ClosedXML writes a bogus `<numFmt numFmtId="0" formatCode=""/>`.
- Warning CS0028 in `LayoutValidatorTests` (a helper named `Main`). It would fail CI under `-warnaserror`.
- The spike folder was lost (Temp was wiped), and there was no report.

**Done in this run:**
1. **E.6 spike redone** under `rugs-plan2\work\p6-engine` (since deleted). It had three parts:
   - a raw ClosedXML probe;
   - a 10,000-row read-back timing;
   - real-Excel checks through COM: cached values before recalculation (with a tamper test), recalculation equality, honoured manual breaks under Fit-To, title colour, PDF pages rendered and viewed.
   All outputs also passed the OpenXML schema validator (Office 2019): 0 errors. Results are in §2.
2. **Fixture verified independently.** I re-transcribed rows 1–43 from the real sample xlsx (`Desktop\baba-1404-08-11 (2) (2).xlsx`, read-only, extracted to the work dir and deleted) with a separate script and diffed it against `sample-rows-1-43.expected.json`. Values, expanded shared formulas, style tuples, merges, merge outer borders, `<col>`s, sheetView, sheetFormat, pageSetup and margins all match: **NO DIFFERENCES** (516 cells, 41 merges).
3. **`shipment-declaration.v1.json` matches E.9.** It is structurally identical to the E.9 jsonc block, parsed and compared programmatically, with the same key order.
4. **Fixes:**
   - `CachedValueInjector` now drops built-in `numFmt` entries (id < 164) and an empty `numFmts`. Output has no `numFmts`, like the sample (E.7).
   - The expression-limit test was corrected: exactly 1,000 characters is accepted, 1,001 gives `E_EXPR_TOO_LONG`, and a decimal-overflow literal gives `E_EXPR_SYNTAX` instead of throwing.
   - The CS0028 helper was renamed to `MainSheet`.
5. **Additions:**
   - **Validator:** a column key that itself looks like an A1 letter («E») is rejected with `E_LAYOUT_LETTER_COL` at `.key`, plus a test. Otherwise a footer `col: "E"` could get past the «col is never a letter» rule by matching a key.
   - **`DocumentTenant`:** gained trailing optional `ExporterCode`, `ExporterAddress`, `ExporterPhone` and `CustomsOffice`. `DocumentFields` and `DocumentFieldKeys` gained `tenant.exporterCode`, `tenant.exporterAddress`, `tenant.exporterPhone` and `tenant.customsOffice`. These match the keys P4a's `ShipmentFieldContributor` already puts in the Document catalogue, so templates using them render values instead of blanks.
   - **`shipment.usdRate`:** now `Money = false`. The E.3 table lists it with the shipment fields, not the money row, and P4a's contributor uses the same value.
   - **Writer speed-up:** one detached style prototype per (style, format), from `XLWorkbook.DefaultStyle`, copied to cells. Measured 2.2 s → 0.12 s for 162k cells. Merge anchors are now looked up in O(1) instead of O(rows) per merge. Goldens are unchanged.
6. Sample outputs, PDFs, page PNGs, the spike report and all verification scripts are saved to `docs/plan2/reports\p6-engine-samples\`.

## 2. E.6 day-1 spike results (gate): PASS, no fallback needed

| # | Item | Result |
|---|---|---|
| 1 | RTL, zoom 70, Persian sheet names, merges, formula text, fonts/fills/borders, custom row heights (16.8, customHeight=1), margins, centre header `&Cپیش‌نویس`, `fullCalcOnLoad` | **ClosedXML writes all of these natively** (raw probe A1–A13). |
| 1 | Column widths | ClosedXML shifts them (3.88671875 → 4.597344; defaultColWidth 9.82). **The injector writes the exact widths**, the default 9.109375, and `<col min=14 max=16384 style>` as in the sample. |
| 1 | `fitToPage` + `fitToHeight=0` + `scale=95` | ClosedXML treats them as alternatives: setting Scale drops fitToPage. **The injector writes both** (`<pageSetUpPr fitToPage="1"/>`, `scale="95" fitToWidth="1" fitToHeight="0"`). |
| 1 | `_xlnm.Print_Titles` on one sheet | ClosedXML writes `اظهار!1:2`. **The injector writes `'اظهار'!$1:$2`** on localSheetId 0 only. The summary has none (Excel: `PrintTitleRows=''`). |
| 1 | `rowBreaks` | ClosedXML writes `max=1048576`. **The injector writes 16383** (Excel's value), `man=1`. |
| 2 | Cached values | ClosedXML writes **no `<v>`** on formula cells (raw read-back: blank, NeedsRecalculation). **The injector writes `<v>` from the composer's decimals**, invariant, with no float noise. Full sample: 430/430 main-sheet and 147/147 summary formula cells carry `<v>`. Serial 3816 → `<x:f>ROUND(I53*J53, 2)</x:f><x:v>3.8</x:v>`. |
| 3 | Excel shows cached values before recalculation | **PASS (Excel 16, COM, manual calc).** A tampered copy with K3 `<v>`=9.99 showed 9.99 on open, so Excel displays our `<v>` and did not recalculate on load. On the real files, 39 checked values matched expectations before recalculation; after `CalculateFull` there were **0 differences**. Values checked: 3816 → 3.8; bale 29 footer 50/8/360; bale 30 footer 34/6.24/280.8; bale 33 footer C=33; summary totals 242/464.74/20322.5. **LibreOffice: not verified** (not installed; see §7). |
| 4 | ClosedXML read-back | Reads cached values without evaluating (`NeedsRecalculation=false` on links) and reads merges (golden tests). **10,000 data rows (12,502 rows): load + read every used cell = 864 ms (< 3 s) PASS.** Writing that file took 4.2 s (about 2.3 s once warm); the 161-line sample writes in about 0.1–0.3 s. |
| 5 | Print preview honours manual breaks under Fit-To (fitToWidth=1, fitToHeight=0) | **PASS in Excel.** `HPageBreaks` = 5, all manual (type −4135): rows 44 (bale 11), 81 (bale 20), 113 (bale 23), 152 (bale 28), 192 (bale 35). `Pages.Count=6`, so Excel inserted no automatic break inside a bale. Viewing the exported PDF pages confirms it: page 1 = bales 1–10, page 2 = 11–19, page 3 = 20–22, page 4 = 23–27, and print titles repeat on every page. **The fallback (`fitToPage=0` + integer scale) is not needed**, and `ComposeOptions.FallbackScale` stays false by default (kept and tested). |
| 6 | `fitToPage=1` and `scale=95` both written; title colour | Both are written. Excel reads `FitToPagesWide=1`, `FitToPagesTall=False`, `Zoom=False`. A1 font colour = 7434614 (#767171), fill = 65535 (yellow), B Nazanin 11. |
| — | Number formats General | No `numFmts`. Excel `NumberFormat`=General. |
| — | RFC 5987 file name | `DocumentFileName`: `attachment; filename="declaration-12-2026-09-06.xlsx"; filename*=UTF-8''<%XX>` decodes to `اظهار-12-1405.06.15-پیش‌نویس.xlsx`. The ZWNJ is kept as `%E2%80%8C`. Unit-tested. |

Other notes:
- Excel's PDF export renders digits as Persian.
- The summary sheet prints on one page with the C1:H1 yellow title, borderless «=» row, totals 242/464.74/20322.5 and a bordered blank D.
- Final-mode files have no `headerFooter`. Draft files have `&Cپیش‌نویس`.

## 3. Files (all owned by this package)

The core of the package was created in earlier attempts; this run modified the starred (*) files.

**Application (`src/RugsManagment.Application/`)**
- `Abstractions/Services/IXlsxDocumentWriter.cs`
- `Features/Documents/Layout/Layout.cs`
- `Features/Documents/Layout/LayoutValidator.cs` *
- `Features/Documents/Expressions/{Lexer, Parser, Ast, TypeChecker, Evaluator, ExcelCompiler}.cs`
- `Features/Documents/Composer/{RenderModel, DocumentComposer, FormulaResolver, PageBreakPlanner}.cs`
- `Features/Documents/DocumentData/DocumentData.cs` *
- `Features/Documents/DocumentData/DocumentFields.cs` *
- `Features/Documents/DocumentData/DocumentFileName.cs`
- `Features/Documents/DocumentData/DocumentMessages.cs` (partial `Messages`: `E_LAYOUT_*`, `E_EXPR_*`, `W_EMPTY_BALE`, `W_BALE_TOO_LONG`, `W_CHECK_MISMATCH`)
- `Features/Documents/Defaults/shipment-declaration.v1.json` (= E.9 exactly)
- `Features/Documents/Defaults/rug-list.v1.json` (E.1)

**Infrastructure (`src/RugsManagment.Infrastructure/`)**
- `RugsManagment.Infrastructure.csproj`: +ClosedXML 0.105.1, +DocumentFormat.OpenXml 3.1.1 (ClosedXML's nuspec needs `[3.1.1, 4.0.0)`)
- `Documents/ClosedXmlDocumentWriter.cs` *
- `Documents/CachedValueInjector.cs` *

**Tests (`tests/`)**
- `fixtures/sample-rows-1-43.expected.json`
- `RugsManagment.Tests/Documents/{ExpressionTests*, LayoutValidatorTests*, DocumentEngineMiscTests*, DocumentComposerGoldenTests, DocumentComposerSampleTests, PageBreakPlannerTests, SampleDocuments}.cs`. That is 88 `[Fact]`/`[Theory]` methods.
- `RugsManagment.IntegrationTests/Documents/{XlsxGoldenTests, XlsxSamples}.cs`. These 10 tests write real files and read them back with ClosedXML and OpenXml. They need no DB.

Deleted: nothing. The throwaway `rugs-plan2\work\p6-engine` (HEAD snapshot, tree copy, spike, bench, extracted sample) was deleted when done.

## 4. Verification commands and results

Other agents' half-done files break the real working tree, so I verified in three places:

**(a) Isolated tree = `git archive HEAD` (d45368b) + this package**
- `dotnet build RugsManagment_Service.slnx -c Release -warnaserror -p:NuGetAudit=false` → **Build succeeded**, 0 warnings.
- `dotnet test tests/RugsManagment.Tests` → **746/746 passed** (all unit tests).
- `dotnet test tests/RugsManagment.IntegrationTests --filter FullyQualifiedName~Documents` → **10/10 passed**.

**(b) The real working tree (all packages' current files)**
- `dotnet build RugsManagment_Service.slnx -p:NuGetAudit=false`: all src projects and the IntegrationTests project build.
- `dotnet test …IntegrationTests --filter FullyQualifiedName~Documents` → **10/10 passed**.
- The unit-test project fails to compile only in `tests/RugsManagment.Tests/P2Fix/P2ReviewFixTests.cs` (CS0103 `RugFieldKeys`; P2-review-fix, in progress).

**(c) Copy of the current working tree minus the half-done `tests/RugsManagment.Tests/P2Fix`**
- `dotnet test tests/RugsManagment.Tests` → **880/880 passed**. This includes the Documents tests and `MessagesTests` (no duplicate codes across all packages' message files).
- `-c Release -warnaserror`: the only error is in P2Fix's `IntegrationTests/P2Fix/P2ReviewFixLedgerTests.cs:243` (CS4034, not mine). Nothing in this package.

**Other checks**
- Fixture vs real sample (node re-transcription): NO DIFFERENCES.
- OpenXML validator (Office 2019) on all 7 produced xlsx: 0 errors.
- Excel COM checks: see §2. Log: `reports\p6-engine-samples\excel-check.txt`.

## 5. Acceptance (K.2/P6 engine items, E.10)

| Item | Result | Evidence |
|---|---|---|
| **Golden E.10 #1:** lot-free bales 1–10 match `sample-rows-1-43.expected.json` cell for cell, under the E.10 rules | **PASS. Golden diff is empty.** | Checked at model level (`DocumentComposerGoldenTests`, 5 tests) and on the real xlsx read back with ClosedXML + OpenXml (`XlsxGoldenTests.LotFree_*`, 3 tests). Covers values, `<v>` text, formulas, merges and outer borders, fonts, colours, fills, alignment, number format, exact `<col>` widths, row heights, sheetFormat, sheetView, pageSetup, margins, and the Calibri 11 Normal font. Only the E.10 intentional differences remain: `=ROWS(B…)` E-footer, explicit heights, sheet name, print-titles name, exact cached values, plain formulas, uniform borders in merges, shrinkToFit on data cells. |
| Full sample: 36 bales / 161 lines / 242 pieces / 464.74 m² / 20,322.50 USD; 13 columns; H = 1 on single rugs | PASS | `Full_HasTheSamplesTotals`, `Full_QtyIsOneOnSingleRugs…`; Excel summary totals 242 / 464.74 / 20322.5 |
| Bale 29: one row, no A merge; H=50, K=0.16, M=7.2; footer 50 / 8.00 / 360.00 (`SUM(H)`, `SUMPRODUCT(H,K)`, `SUMPRODUCT(H,M)`) | PASS | `Bale29_…`; Excel footer29 = 50 / 8 / 360 |
| Bale 30: 24 & 10, 0.16 & 0.24, 7.2 & 10.8; footer 34 / 6.24 / 280.80 | PASS | `Bale30_…`; Excel |
| Bale 31 starts with 4004; bale 33 footer C = 33 | PASS | `Bale31_…`; Excel footer33 |
| Midpoint caches 3789→2.9, 3816→3.8 (J57), 3853→0.76, 3882→0.39, 3901→0.75, 4009→0.4 | PASS | Composer theory test, the xlsx `<v>`=3.8 test, and Excel before-recalc |
| Serials numeric (all digits, no leading 0, ≤ 15 digits); «0123», 16 digits and «A-12» are text; quote prefix on data text starting with `= + - @`; template literals («=») have none | PASS | `Serials_…`, `DataText_StartingWithAFormulaCharacter…`, summary «=» checks |
| Number formats General; single-row bale has no A merge; empty bale skipped with `W_EMPTY_BALE` («نگلهٔ ۵ خالی بود و در خروجی نیامد.»), no reversed range | PASS | `EmptyBale_…` with a range regex over every formula |
| Print titles `$1:$2` on the main sheet only; no row break inside a bale | PASS | `Workbook_HasPrintTitles…`, `Full_RowBreaks_AreManual_AndFallOnlyBetweenBales`, Excel HPageBreaks |
| `PageBreakPlanner` worked numbers: 734 px → s=1, C=43; 772 px → 579 pt, s=0.9787, C=44; fallback s=0.97, C=45; pages 1–10 / 11–19 / 20–22 / 23–27 (fallback page 2 = 11–20); `W_BALE_TOO_LONG` | PASS | `PageBreakPlannerTests` (11) |
| Summary sheet «خلاصه»: C1:H1 title, yellow, no border, sample spacing; D2:G2 headers; 36 links using `'اظهار'!C/E/J/L{n}` with lots (C/E/I/K without); «=» row with no border and no quote prefix; totals `SUM(E3:E38)` etc. with a bordered blank D | PASS | `Summary_…`, `LotFree_SummaryLinks_UseIAndK`, `Full_ReadBack_GivesTheTotals_WithoutEvaluating`, Excel summary PDF |
| Adl fixture renders «عدل» everywhere (title, A2, footers «عدل شماره», summary D2; no «نگله» anywhere) | PASS | `Adl_RendersAdlEverywhere`, `Adl_File_SaysAdlEverywhere` |
| `checkAgainst` never fires on the sample; fires `W_CHECK_MISMATCH` on a mismatch | PASS | Tests |
| Finalized renders from the snapshot | PASS (engine side) | A `DocumentData` round-trip through System.Text.Json composes identically. Mapping `SnapshotJson` → `DocumentData` is P6-wiring's job. |
| Draft header «پیش‌نویس»; final has none | PASS | Tests + XML + Excel `CenterHeader` |
| File opens in Excel with values shown before recalculation | PASS | §2 #3 |
| File opens in LibreOffice with values shown before recalculation | NOT VERIFIED | LibreOffice is not installed (§7) |
| Validator: every E.2 rule with Persian message + JSON path, including perPiece-requires-qty, letter footer `col`, unknown `footer:<id>`, overlapping span, unknown style, cycles (incl. self-reference), the errata E8 summary-letters exception, 30 columns, `UsesMoneyFields`; both defaults validate | PASS | `LayoutValidatorTests` (32) |
| Expressions: precedence, unary minus, blank semantics, `/0` → blank, if/and/or/not, `&`, text comparison rejected, Bool column rejected, limits 1000/32. Compile: `ROUND(H3*I3, 2)` ↔ `ROUND(I3*J3, 2)`, `SUMPRODUCT(H3:H6,K3:K6)`, `SUM(H3:H6)`, perLine `ROUND(I3*J3, 2)*H3` folded to `ROUND(H3*I3, 2)`, coalesce → `IF(a="", b, a)`, `""` escape, >255-char literal → value-only | PASS | `ExpressionTests` (18) |
| Download name per E.7 / RFC 5987 with the draft suffix | PASS | `DocumentEngineMiscTests` |

## 6. The `DocumentData` contract P6-wiring must fill

Namespace `RugsManagment.Application.Features.Documents.Data`. All records are positional; new members will only ever be appended as trailing optionals. The graph round-trips through System.Text.Json.

```csharp
DocumentData(DocumentShipment? Shipment, IReadOnlyList<DocumentBale> Bales, IReadOnlyList<DocumentLine> Lines,
             BaleTermWords Terms, DocumentTenant Tenant, bool IsDraft)
  // HasLots (computed): any line with Rug.Qty != 1 → the qty column (visibleWhen: hasLots) appears.
DocumentShipment(Guid? Id, int Number, string? Title, DateOnly? DocumentDate, string? Destination, string? BuyerName,
                 string? DeclarationNo, string? CustomsOffice, decimal? UsdRate)
DocumentBale(int Number, IReadOnlyList<DocumentLine> Lines, int? Pieces = null, decimal? AreaM2 = null, decimal? Usd = null)
DocumentLine(int Seq, DocumentRug Rug, decimal? Length, decimal? Width, decimal? Area = null, decimal? UsdPerM2 = null,
             decimal? PieceUsd = null, string? RateSource = null)
DocumentRug(Guid? Id, string? Serial, int Qty, string? Title = null, string? Pattern = null, string? Origin = null,
            string? Material = null, int? KnotDensity = null, decimal? IntakeLength = null, decimal? IntakeWidth = null,
            decimal? FinalLength = null, decimal? FinalWidth = null, string? Sku = null, string? Notes = null,
            string? Location = null, IReadOnlyDictionary<string, string?>? Custom = null,
            IReadOnlyDictionary<string, decimal?>? Money = null)
DocumentTenant(string? Name = null, string? ExporterName = null, string? ExporterCode = null, string? ExporterAddress = null,
               string? ExporterPhone = null, string? CustomsOffice = null)
```

How to fill each part:

**Shipment declaration (Open)**
- `Shipment` comes from the header. `DocumentDate` drives the title via `jdot` (2026-09-06 → «1405.06.15»).
- `Bales`: all bales of the shipment in any order (the composer sorts by `Number`). Each bale carries all its lines (sorted by `Seq`).
- **Empty bales may be passed.** They are skipped with `W_EMPTY_BALE`.
- `Lines = []`.
- `IsDraft = true`.

**Bale ledger totals** (`Pieces`, `AreaM2`, `Usd`)
- These are the ledger values that `checkAgainst` compares (`bale.pieces/area/usd`).
- Pass `null` to skip the check. The fields then compute from the lines.
- They must follow the D.3 lot rule, Σ (ROUND(ROUND(L×W,2)×rate,2) × qty), or `W_CHECK_MISMATCH` fires.

**Each line**
- `Length` / `Width` = effective size (final, otherwise intake), snapped to 2 dp.
- `UsdPerM2` = the line's declared value: null means unknown, and the cell stays blank.
- `Area`, `PieceUsd` and `RateSource` are optional ledger/catalogue values (`line.area`, `line.pieceUsd`, `line.rateSource`). The default template computes area and USD itself with live formulas.

**Each rug**
- `Serial` is the display text («3778» or «3911 - 3960»). The engine decides between a numeric and a text cell.
- `Qty` is 1 for a single rug, never 0.
- `Custom` is keyed by the custom-field key without the `rug.custom.` prefix (`bg_color`). The value is the stored text; the engine converts it with the catalogue type.
- `Money` is keyed by the full catalogue key (`rug.purchaseCost`, `purchase.price`, …) and **is filled for Business only** (A.5). The engine hides nothing itself.
- `Location` is the display text for `rug.location`.

**Other inputs**
- `Terms` = `BaleTerms.For(preferences.BaleTerm)`.
- `Tenant` comes from preferences: exporter name, code, address, phone and customs office. Tenant name is optional.

**Finalized shipment**
- Build the same graph from `SnapshotJson` (P4a's `SnapshotBuilder` shape):
  - `shipment{…}` → `DocumentShipment` (`documentDate` is `yyyy-MM-dd`);
  - `exporter{name, code, address, phone, customsOffice}` → `DocumentTenant`;
  - `terms{bale, bales, baleOf, baleFooter}` → `new BaleTermWords(...)`, or `BaleTerms.For`;
  - `bales[].{number, pieces, areaM2, usdTotal}` → `DocumentBale(Number, …, Pieces, AreaM2, Usd)`;
  - `bales[].rows[]` → `DocumentLine` / `DocumentRug` (`custom{}` → `Custom`).
- `IsDraft = false`.

**Rug list** (`POST /documents/rugs.xlsx`): `Shipment = null`, `Bales = []`, `Lines` = the selected rugs (`Seq` 1..n; `Length`/`Width` = the rug's effective size), `IsDraft = false`.

**Engine API to call**
- `LayoutJson.Defaults()` gives (SeedKey, Version, Json) for every embedded `Defaults/*.v*.json`, for the seed contributor. `LayoutJson.Default("shipment-declaration")` and `LayoutJson.Parse(json)` are also available.
- `LayoutValidator.Validate(jsonOrLayout, fields)` returns `Errors` (`LayoutIssue` Code / Path / Persian Message) and `UsesMoneyFields`.
- `DocumentComposer.Compose(layout, data, new ComposeOptions(MaxGroups: preview ? n : null, Fields: <tenant Document catalogue incl. custom fields>))` returns a `RenderModel`. Pass `Fields` so custom-field types resolve. `FallbackScale` stays false.
- `IXlsxDocumentWriter.WriteAsync(model, stream)` writes the file.
- `DocumentFileName.Ascii("declaration", number, date)`, `.Persian("اظهار", number, date, isDraft)` and `.ContentDisposition(ascii, persian)` build the download name.

**RenderModel → `RenderedDocumentDto`**
- `Page` → `PageSetupDto`.
- `RenderSheet` → `RenderedSheetDto`: Name, Role, Columns (Index, Letter, Key, Label, Width, Format), Rows, PrintTitleRows, RowBreaksAfter.
- `RenderRow` → `RenderedRowDto`: RowId, Kind, plus `Group` for one `<tbody class="bale">` per bale.
- `RenderCell` → `RenderedCellDto`:
  - Col, ColSpan, RowSpan;
  - `Display`: General format with Latin digits; the print page converts them to Persian;
  - Style name;
  - `Formula`: **no leading "="**;
  - `ValueOnly`, `IsNumber`.
- Style names resolve through `RenderModel.Styles` (`RenderStyle`: font, size, colour, fill, border, align, shrink). The print page and preview need that dictionary too (see §8).
- `RenderModel.Warnings` (`DocumentIssue` Code / Message / Group) → `WarningDto(Code, Message, Count)`.
- `Truncated` is true when `MaxGroups` cut the preview.

**Message codes and catalogue keys**
- The engine's message codes live in `DocumentMessages.cs`. Do not repeat them in `Messages.fa.Documents.cs`.
- Catalogue keys the engine resolves are in `DocumentFieldKeys` and `RugFieldKeys`.
- P4a's `ShipmentFieldContributor` already contributes the line/bale/shipment/tenant/terms keys for `FieldPurpose.Document`, with the same types and Money flags. `DocumentFieldContributor` only needs `tenant.name`, `rug.length`, `rug.width` and `rug.location`, from `DocumentFields.Builtin()`.

## 7. Could not do

- **LibreOffice check (E.6 #3/#5):** LibreOffice is not installed, and downloading or installing software needs the owner's permission. Excel was verified fully. Someone should open the files in `reports\p6-engine-samples\*.xlsx` in LibreOffice and confirm two things: values show before recalculation, and page 2 starts at bale 11.
- **Full unit-test run in the real working tree:** blocked by P2-review-fix's half-done `tests/RugsManagment.Tests/P2Fix/P2ReviewFixTests.cs`. I ran the same suite on a copy of the current tree without that folder: 880/880.

## 8. Needs from others

1. **`tests/fixtures/README.md`** (P1 file; orchestrator or whoever owns it next). The README still says `sample-shipment.json` is the only real-data exception. Rename the heading `## تنها استثنا: \`sample-shipment.json\`` to `## استثناها: \`sample-shipment.json\` و \`sample-rows-1-43.expected.json\``, then add this section after the `sample-shipment.json` section:
   ```
   ### `sample-rows-1-43.expected.json` (P6-engine)

   رونویسی ردیف‌های ۱ تا ۴۳ برگهٔ همان فایل نمونه از XML خودِ فایل (spec E.10، XL-1)، با همان تأیید صاحب کار: برای هر خانه مقدار ذخیره‌شده (تا ۱۵ رقم معنادار، بی نویز ممیز شناور)، متن فرمول با فرمول‌های مشترکِ باز شده، و سبک (قلم، اندازه، رنگ، زمینه، کادر، تراز)؛ به‌اضافهٔ ادغام‌ها و لبهٔ بیرونی کادرشان، پهنای دقیق ستون‌ها، `pageSetup`، `pageMargins` و `sheetView`. آزمون طلایی سند اظهار (نگله‌های ۱ تا ۱۰) خانه به خانه با آن مقایسه می‌شود. **هیچ نام شخص و هیچ مبلغ تومانی ندارد.**
   ```
2. **P6-wiring:**
   - In `Infra/Features/Documents/DocumentsModule.cs`, register `services.AddSingleton<IXlsxDocumentWriter, ClosedXmlDocumentWriter>();` (the writer is stateless).
   - Fill `DocumentData` as in §6.
   - Pass the tenant's Document catalogue as `ComposeOptions.Fields`.
   - Add a styles member to the rendered DTO, or render Razor from `RenderModel` directly. I.3's `RenderedDocumentDto` has no style dictionary, and the preview and print page need fonts, fills, borders and the title colour.
   - Add a trailing optional `IReadOnlyDictionary<string, RenderStyleDto>? Styles = null`, or hand Razor the `RenderModel`.
3. **P6-wiring / P7 frontend:** mirror the engine's codes in `CA/lib/messages/documents.ts`: `E_LAYOUT_*`, `E_EXPR_*`, `W_EMPTY_BALE`, `W_BALE_TOO_LONG`, `W_CHECK_MISMATCH`, with the same Persian text as `DocumentMessages.cs`.

## 9. Saved for inspection

`docs/plan2/reports\p6-engine-samples\`:

**Workbooks**
- `full-sample.xlsx`
- `full-sample-draft.xlsx`
- `golden-bales-1-10.xlsx`
- `adl-bales-1-3.xlsx`
- `quote-prefix.xlsx`
- `spike-raw-closedxml.xlsx` (what raw ClosedXML writes)

**Excel prints**
- PDFs: `full-sample.pdf`, `full-sample-draft.pdf`, `full-sample-summary.pdf`, `golden-bales-1-10.pdf`
- Page PNGs: `full-sample-p1…p6.png`, `full-sample-draft-p1.png`, `full-sample-summary-p1.png`, `golden-bales-1-10-p1.png`

**Logs**
- `excel-check.txt` (COM log)
- `spike-report.txt`
- `verify-fixture.txt`

**Scripts** (`scripts\`)
- `verify-fixture.js`
- `excel-check.ps1`
- `excel-summary-pdf.ps1`
- `pdf-pages.ps1`
- `spike-Program.cs` + `spike-Spike.csproj` (they referenced the deleted snapshot under `work\p6-engine\snap`; point them at the repo to re-run)