# P5 resumed — cost ledger

Owner: costs agent, 2026-10-17. This report is updated after verification; no real database operations were performed.

## Implementation

- Added the missing five EF configurations (explicit names, precision, foreign-key deletion semantics, target XOR, unique client key/number/targets/pins, allocation indexes, xmin concurrency on CostEntry).
- Added CostEntryRepository and CostLedgerRepository; the latter supplies the real ICostSourceProvider, tenant-filtered purchase/expense sums, exact shipment revenue shares, affected-entry lookup, before-delete target capture and one-statement parameterized allocation/result diff writes.
- Added CostMembership: batches all affected memberships into one read, preserves purchase-batch snapshots, excludes trashed rugs from current membership, reads current line USD and purchase-cost weights. Old shares are read with IgnoreQueryFilters for removal during trash/reallocation.
- Completed CostsInfrastructureModule registrations, split-target/pin repository seam, money probe, and derived-type exclusions/detachment for CostAllocation/CostEntryResult.
- Generated M4 `20261017044805_CostLedger`; only the five cost tables are included. Removed generated xmin DDL and documented it in HandEdits/CostLedger.md. Later-phase configuration files were restored immediately after generation. No existing M0–M3 migration was modified.

## Audit fixes

- Receipt freeze/refreeze and purchase snapshot/resnapshot now acquire the tenant ledger lock before reading membership and calculating the stored amount, inside the same UnitOfWork transaction.
- Floor cost-category responses redact DefaultRate; category metadata must not expose business money to Operators.
- Live foreign-currency entries calculate current original-currency amounts when read/listed; membership changes no longer leave the displayed original total stale.
- P5 D8 validation errors return their stable machine codes using the root-owned optional AppValidationException.Code seam.
- Cost-category UI uses «دستهٔ هزینه» to distinguish category grouping from an intake batch («ورودی»).

## Tests added

- Pure allocator tests: all twelve D7 steps, Simple total-washing equivalence, frozen receipt redistribution, negative/zero/missing weights, negative amounts, pins/manual/all-pinned residual, FX and Rial per-piece rounding, warning refresh and diff idempotence, ordinal UUID ties and 10,000 members.
- 1,000 randomized pure allocations with up to 2,000 members, negative purchase weights, amounts up to ±10^15, optional pins, exact conservation, sign/bounds and order-independence/determinism checks.
- Shared D7 allocation vectors added to math-vectors.json, plus costVectors rows using the existing entryPiece/baseAmount schema.
- PostgreSQL API tests: D7 full workflow, provider receipt, same-request final-size reallocation, trash/restore, declared-value customs, shipment revenue/profit, finalization and late cost with stable snapshot hash; cross-tenant access/auth, D8 validation codes, idempotency, delete/restore; E4 dynamic expense vs purchase snapshot after batch membership changes and deletion; 200 random operations with repeated zero-mismatch consistency checks.

## Verification so far

- `dotnet build src/RugsManagment.Web -p:NuGetAudit=false -m:1 -nr:false --no-restore`: passed, 0 warnings/errors at first complete P5 infrastructure checkpoint.
- `dotnet test tests/RugsManagment.Tests --filter FullyQualifiedName~P5 -p:NuGetAudit=false -m:1 -nr:false --no-restore`: 10/10 passed before the additional shared-vector test.
- `npm exec -- vitest run src/components/costs/costText.spec.ts src/lib/num.spec.ts`: 67/67 passed; Vite needed approved escalation after sandbox child-process EPERM.
- Final Release build (root): passed, 0 warnings/errors, including M4–M7.
- `RUGS_TEST_PG=<local disposable-db base> dotnet test tests/RugsManagment.IntegrationTests --filter FullyQualifiedName~P5 --no-build -c Release --no-restore -p:NuGetAudit=false -m:1 -nr:false`: **4/4 passed, 0 skipped, 14 seconds**. This includes D7 #1–#12, same-request freight/total changes, frozen receipt redistribution and provider amount, finalization/late-cost hash preservation, validation codes/tenancy/idempotency/delete/restore, E4 snapshots and 200 random operations with cost-consistency = 0.
- Initial integration helper failures were corrected (`length`/`width` are the final-size endpoint member names; randomized sizes respect the 0.05m lower bound). The final Release run above verifies these corrections.
- After the shared-fixture schema correction, repeated current frontend costs/math consumers: **67/67 passed**.
- Final Release pure run: **11/11 passed**, including corrected shared D7 vectors and 1,000 randomized allocation trials. The complete ordinal-tie + 10,000-member test (setup, warmup and measured allocation together) took **29ms** in xUnit's detailed output on this host.

## Remaining verification / needs from others

- Root owns the full final unit/architecture/integration runs. The corrected costVectors operations are handled by CostVectorsTests.cs (`entryPiece` at line 91; `baseAmount` at line 101). The earlier four unknown-operation failures used the pre-correction rows.
- Root owns final browser workflow/375px/1024px/a11y and application-wide tests. Human father-usability acceptance cannot be claimed by an agent.
- No DbContext or pass-core edits were needed. Shared validation Code support was requested explicitly and implemented by root.

## Acceptance evidence and limits

- D7 math/API, negative largest remainder, frozen receipts, pins residual, Rial/FX ordering, tenant/auth gates, idempotency and same-request consistency: passed in the tests above.
- The random pass test creates/changes purchase lines, final sizes, purchase/expense entries and deletes/restores costs, with a permanent ByPurchaseCost expense observing propagation; the D7 test separately covers rug trash/restore and the batch test covers dynamic membership/deletion.
- Unit property tests cover 1,000 random allocations. The 10,000-member regression uses a broad CI safety bound (2 seconds); the observed complete-test duration was 29ms (including two 10,000-member allocations), satisfying the <50ms target on this host. This is not an isolated benchmark certificate.
- The scripted UI/father usability checkpoint, live browser a11y and real-database migration are orchestrator/human acceptance items and are not claimed by this report.

## Final performance regression work

- Baseline isolated Release P4aPerformance test passed with finalize 16 SQL commands / reopen 17; measured operations 211, 179, 237, 202, 188ms (best 179ms, unchanged <200ms requirement).
- Consolidated real ICostSourceProvider.GetAsync into one parameterized SQL rowset: purchase/expense sums and revenue siblings with currency preferences travel in one roundtrip. Previously it issued two queries plus two settings queries for revenue. Exact allocation/revenue calculations remain in CostAllocator; no new formula site was introduced. Lookups now use dictionaries instead of scanning allocation sums repeatedly per rug.
- Re-ran P5 after optimization under concurrent full-suite contention: all four API/random consistency tests passed. The separate performance host competed with another complete integration host and migration stress and failed the latency threshold (442/468/436/482/800ms). This is recorded as a failed run, not accepted performance evidence.
- Root requested an isolated xUnit collection for the existing performance test. Added DisableParallelization=true, preserving its exact <200ms threshold and <20-command assertions; measurement output now records SQL durations/counts for diagnosis.
- **Final complete Release integration run: 387/387 passed, 0 skipped, 59 seconds.** Read the generated TRX counters and performance stdout directly. Finalize **16** SQL commands, reopen **17**; measurement operations **178, 194, 238, 183, 192ms**, so the unchanged best-of-five <200ms assertion passes (**178ms**). Each measured request issues 27 SQL activities including transaction/savepoints; the <20 assertion applies to finalize/reopen only.
- SQL instrumentation shows each batched cost-source call takes about 1.6–2.2ms. Most measured request time is the existing final ListBales aggregate (156–216ms). No further unrelated query changes were made after the complete suite passed.
- Corrected the costs client message mirror E_COST_CATEGORY_GROUP_LOCKED; targeted parity plus import-preview gate tests: 7/7 passed.
