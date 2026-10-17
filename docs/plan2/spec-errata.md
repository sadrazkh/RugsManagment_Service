# Spec errata (binding; these override final-spec.md where they conflict)

Found by the round-2 recheck after the in-place revision. Each item names the phase that must apply it.

## E1
Serial edits skip the overlap check. D.5 #8 makes a serial change 'lock guards only' with no contributor, and D.4 step 7 says 'With an empty impact (a guard-only save) no stage runs', so SerialOverlapStage never runs on a rug PUT or grid edit that only changes the serial. The partial unique index catches only identical SerialNo text, not overlapping ranges. A serial change also moves SerialSort, which is an LR tie-break (D.3), so allocations can drift without a re-run. Fix: RugImpact emits the rug for a SerialNo/SerialFrom/SerialTo change; SerialOverlapStage always runs for it; entries containing the rug re-run.

## E2
Auto serial mode cannot sit inside SerialOverlapStage. G.4 (and the XL-4 entry in the Round 2 log) assigns the server serial 'inside SerialOverlapStage under the ledger lock', but D.4 now runs stages after the flush, as set-based SQL. The stage would have to write the Rugs row by SQL after the flush. The tracked Rug would then keep SerialNo = null and the old xmin, so GridOpResult/RugDto return a stale serial and version and the next edit gets a 409. This also contradicts B.1 / L.2, which say stage writes never touch Rugs.xmin. Fix: assign Auto serials before the flush, under the lock (D.4 steps 4–5), on the tracked entity; SerialOverlapStage only checks.

## E3
HomeSummaryDto breaks the build-phase order. I.3 lists it as a Platform DTO, and P1 (acceptance and auth-matrix) serves /api/home/summary. But the ux-open-shipment fix types its first member as IReadOnlyList<ShipmentListItemDto>, a P4a DTO that has no table before M3, so P1 cannot compile it. No seam fills it later. Fix: a P1-owned HomeShipmentDto plus a contributor seam that P4a/P4b implement, or let the P4b OpenShipmentCard query /api/shipments?status=Open itself.

## E4
Purchase snapshots contradict batch delete. LM-11 makes Purchase-group IntakeBatch entries snapshots (explicit CostEntryTarget.RugId rows; later batch changes do not move purchase money). But B.13, D.5 #12 and M.2 #31 still say that deleting a RugBatch SET NULLs BatchId and makes the entry Unallocated with W_TARGET_DELETED. Deleting the ورودی would then strip the lump purchase price («خرید یکجا») from rugs that were bought in it. Fix: a snapshot entry keeps allocating over its RugId targets; only Expense (dynamic) IntakeBatch entries become Unallocated.

## E5
Between P1 and P2, an Operator saving the legacy RugForm either always fails or loses data. From P1, RugDto.PurchaseCost and TargetSalePrice are redacted to null for Operators, while RugForm keeps resending both until P2. A.5 returns 403 for any non-Business request that 'carries' these members. If 'carries' includes null, every Operator rug edit is a 403 in P1. If not, the pre-P2 legacy service writes null into Rug.PurchaseCost, and M2 later finds nothing to move into a purchase line. Fix: null money members from a non-Business user are ignored (the stored values are kept); 403 only for non-null values.

## E6
An Operator cannot undo deleting his own rug. G.3 and G.6 let Floor delete its own rug from the last 24 h, and the undo toast «حذف شد [برگردان]» 'calls restore'. But J.2 moves the Rugs restore action (:113) to Business, and I.2 defines no Floor restore endpoint. Fix: name the restore route used by the grid toast and give Floor the same 24 h own-rug rule.

## E7
«ورودی امروز» is ambiguous once a batch is keyed by (day, seller). Several sellers on one day are several batches. Yet the cost-panel chip «ورودی امروز» (H.2), the Home pre-fill (A.4) and the Intake button «هزینه یا خرید یکجای این ورودی» (H.1, openPanel with a single batchId) all assume one batch per day. Fix: one chip/button per seller group (in the footer group), and a picker when there are several.

## E8
Minor: the LayoutValidator rule 'title.fromCol / toCol … are column keys (or last)' would reject the summary sheet's letter range (fromCol "C", toCol "H") that E.2/E.9 require. Scope that rule to role = main.

## E9
Minor: B.10 says pins are 'Total-basis only', and E_PINS_BASIS reads «مبلغ ثابت فقط برای هزینهٔ «کل» است», while D.3 allows pins on frozen PerM2/PerPiece receipts. Align the wording to 'Total or frozen'.

## E10
Minor: some H.3b templates hard-code example values. «{baleOf} ۵ خالی است» and «{baleOf} ۳ شد ۱۵٫۱ متر؛ از ۱۳ متر هدف» should use tokens such as {bale}, {area} and {target}. W_TARGET_DELETED uses {bale}, which is missing from the declared token list.

## E11
Minor: the messages mirror path disagrees. H.3b has CA/lib/messages.ts; K.1/K.2 have CA/lib/messages/index.ts plus lib/messages/{feature}.ts; L.3 tests lib/messages.spec.ts.

## E12
Minor: the cm/metre question is inconsistent. B.3 says any side above MaxPlausibleSideM asks «متر یا سانت؟», but G.4 hard-codes 'an integer from 13 to 30'. Non-integers between MaxPlausibleSideM and 30 (e.g. 25.5) are unspecified, as are integers when the preference is not 12.

## E13
Minor: H.4's Simple-mode Rial toggle ('Rial is divided by 10 on save') conflicts with D.3. D.3 stores Currency = Rial and converts per piece (fx 0.1), and at MoneyScale 0 a Rial amount not divisible by 10 gives a fractional Toman. State that the entry is saved as Rial and converted by the D.3 rule.

## E14
Minor (XL-1 residual): L.1 'All 161 sample lines … reproduce the file's J and L', but sample-shipment.json carries no per-line J/L values (only rows 1–43 are in the expected file). Add the sample's cached area and USD per line to the fixture.

## E15
Minor: P8's tests/Fixtures/SampleSheetBuilder.cs sits outside every test project, and the test csproj files are frozen after P0, so it will not compile. 'Fixtures' vs 'fixtures' casing also differs on Linux CI. Place it under a test project folder.

## E16
Minor: K.1's owner list for Views/Rugs/Details.cshtml (P1 → P2 → P9) leaves out P4b, which edits the Details partials for location chips and Simple-mode status hiding.

## E17 (from P0b re-verification; applies in P2 WP2-B and P5)
Templates saved before P0b may still hold another tenant's step-type/provider ids, and the rug side never re-checks them: `WorkflowEngine.MapTemplateStep` and the template paths in `RugManagementService` (create + ApplyTemplate) must validate template step ids against the tenant (system or own) when instantiating, and drop/refuse foreign ones. `ServiceProviderRepository.ListBalancesAsync` must filter steps by `s.Rug.TenantId == tenantId` (ListWorkAsync already does).

## E18 (applies in P1 WP1-A1)
`tests/fixtures/README.md` says every fixture is fabricated; the spec puts the owner-approved transcription of the sample (`sample-shipment.json`, real USD/m² values, no names, no Toman money) there. Fix the README when adding the fixture.

