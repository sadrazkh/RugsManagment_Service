# P4b, wave-2 review fixes and P9 resumed work

Date: 2026-10-17. Workspace: isolated `work/repo`, branch `codex/finish-export-workflow`. No git mutations or real-database access by this agent. The acceptance app applied the team's migrations only to the disposable `rugs_accept_review_1017` database.

## Implemented

- Reviewed P4b WIP, reused the existing board/pack components and API contracts. Fixed the dimension-pair result normalization found by its existing Vitest suite.
- Seller/batch resolution precedes field writes; failed seller validation cannot leave a tracked purchase line or persist a rejected edit. Retried creates return the current row without writing stale values.
- Grid auto-save excludes Invalid rows until edited or explicitly retried. Lost-response/new-row deletion resolves the original ClientRowId, waits for an in-flight create, and prevents retry resurrection.
- Intake day changes await current writes and refuse a switch with unsaved rows. The destination day loads before its context changes; a failed load retains the old day. The old day's default seller is cleared on successful switch.
- Grid price parsing preserves existing/sticky units in paste and first-cell typing. Area display/copy/footer use final dimensions when both exist; purchase preview still uses intake dimensions.
- Keyboard grid Escape has a focusable fallback; keyboard row menus are positioned/focused; column reordering restores focus to the moved column's enabled control.
- Label printing is selected by a bounded `date` URL; the server resolves that tenant's intake-day rug ids.
- Splitting a baled lot preserves both parts in the same bale, shifts following Seq values and preserves a manual USD rate. Split runs inside the tenant lock/transaction. Final measurement metadata is protected by finalized-shipment guards.
- Bale create/update/renumber and line batches take the tenant ledger lock before allocating numbers/Seq. Root owns commit-time unique-violation translation.
- Document compiler: literal coalesce folds; blank branches remain text blanks; nested numeric division uses zero; emission has a bounded work/length budget. Evaluator MIN/MAX ignore blank/text references. Page breaks honor explicit scale when fit-to-width is off. Weighted footers select a visible quantity column. XML-invalid text is stripped while valid surrogate pairs survive.
- P9: native Vue date controls replaced with JalaliDateField; new `date-filter` island and Rugs date-filter partial cover server forms. Workflow modal uses shared Modal/focus trap. Requested white surfaces use tokens; gallery buttons use contrast-safe surface/text tokens. Label HTML data substitutions are escaped separately from raw QR substitutions.
- GroupDetail loads all pages instead of silently stopping at 100. Analytics has a set-based repository (aging, grouped step costs, Jalali trend buckets) with tenant filters and CostAtSale/current ledger totals. DatabaseSeeder honors Seed:Demo; integration fixture explicitly opts into demo content.
- Help includes pack, output, import and setup; Business-only steps are filtered for Operators with safe generic guidance for inaccessible screens. Existing printable card/HomeCoach already share role-aware MerchantSteps and remain one source.

## Wave-2 finding map

| Findings | Resolution | Regression evidence |
|---|---|---|
| 1 | Seller resolved before any writer mutation | `LongSeller_DoesNotLeavePurchaseOrMutateInvalidRow_AndOtherRowsSave` |
| 2 | Existing ClientRowId is read-only retry | `RetriedCreate_ReturnsCurrentRow_WithoutOverwritingLaterEdit` |
| 3 | Sent creates deleted on server by ClientRowId | Vitest `deletes a create whose response was lost...` |
| 4, 22 | Await in-flight writes, block unsaved switch, load target before switching context | Source audit; targeted Vitest suite green |
| 5, 18 | Invalid rows omitted from automatic collect | Vitest `does not resend rejected rows until explicit retry or editing`, advances 60 s |
| 6, 8 | Split preserves membership/Seq/override under lock | `SplittingBaledLot_KeepsBothPartsInTheSameBale_AndPreservesTotals` |
| 7 | FinalMeasuredAt/By in tracked and unknown-value guard paths | `EachLockedRugField_IsRejected_WithAnOtherwiseEmptyImpact` additional metadata cases |
| 9 | Effective display size; intake purchase size retained | Vitest `prefers final dimensions for display while purchase uses intake dimensions` |
| 10, 11 | Context-sensitive blank/division formula emission | `Wave2_BlankConstants_AndNestedDivision_MatchCachedSemantics` |
| 12 | Formula emission budget, ValueOnly fallback | `Wave2_FormulaGrowth_IsBounded_AndControlCharactersAreRemoved` |
| 13 | Explicit scale passed into planner | `Wave2_ExplicitScale_ControlsCapacity_WhenFitToWidthIsOff` |
| 14 | Excel MIN/MAX reference semantics | `Wave2_MinMax_IgnoreBlankAndTextReferences_ButKeepLiteralZero` |
| 15 | XML-safe formulas/cached text; surrogate pairs preserved | `Wave2_FormulaGrowth_IsBounded_AndControlCharactersAreRemoved`; documents owner's writer tests green |
| 16 | First visible quantity selected | `Wave2_WeightedFooter_UsesVisibleQuantity_WhenAnotherQtyColumnIsHidden` |
| 17 | Snapshot tracked collection while clearing BatchId; no redundant Update | Source audit; full integration suite green (no dedicated regression) |
| 19 | Reparse paste with row/sticky current purchase basis | Vitest `pasting price retains total and sticky per-piece units` |
| 20, 21, 25 | Focus/keyboard menu fixes | Source audit; CUA grid Escape returns focus; full frontend suite green |
| 23 | Print endpoint resolves ids by tenant/day | Source audit; bounded date query implemented |
| 24 | Drop page-load seller after day switch | Source audit; target context set only after successful load |
| Unverified concurrency | Confirmed by trace: allocations preceded lock; fixed early locks + root commit translation | `ConcurrentBaleCreation_AllocatesDistinctNumbers_WithoutServerErrors` |

## Final verification

- `npm run build` (outside sandbox, required for Vite native/spawn support): passes, strict Vue typecheck and production assets.
- `npm test -- --run`: **554/554**, 35 files. Last run after all functional/accessibility fixes; strict Vue typecheck and production build passed afterwards.
- `dotnet test tests/RugsManagment.Tests --no-restore -m:1 -nr:false -p:NuGetAudit=false --filter 'FullyQualifiedName~Documents|FullyQualifiedName~ShipmentLockGuard|FullyQualifiedName~RugSplitService'`: **197/197** at the targeted run; root's final full unit run includes the subsequent XML-safety assertion.
- PostgreSQL regression filter `P3.Wave2GridRegression|P4b.AnalyticsRead|P4b.Wave2BaleRegression|P4bWeb`: **10/10**, Release, no skipped tests. Root independently completed the full integration suite **387/387** and architecture suite **15/15**; see integration report for those executions.
- Lighthouse 13.5 accessibility scores: Home **100**, Intake **100**, Shipment **100**, Costs **100**. Authenticated final URLs were checked to exclude login/interstitial results.
- Standalone axe-core: Home, Intake, Shipment/Bales, Costs, Output, Pack, Import, Setup × light/dark × 375/1024 pixels = **32 cases, zero violations**. All 32 cases have `documentElement.scrollWidth === innerWidth` and `body.scrollWidth === innerWidth`.
- Axe's remaining `incomplete` entries are contrast checks for clipped/offscreen table text or single-character numeric cells, not confirmed violations. Preview's generic labelled div was given a region role. Visual inspection of the saved viewport screenshots confirms correct rendering. Chromium's full-page RTL screenshots sometimes shift content; final evidence uses viewport captures.
- Audit CLI and packages live outside the repository (`../a11y/audit.cjs`, npm cache), with no new app dependency. Only the isolated audit browser bypassed CSP for the local axe payload; app CSP was unchanged. Authenticated headers were local disposable-demo credentials, removed after the audit.

## Browser acceptance and fixes

- CUA interactive walk used a disposable demo tenant. Entered nine single rugs plus lot `3911 - 3960` (50 pieces), packed into two bales, measured final sizes, applied USD45.55/m² and added freight10,000,000. Output and print preview showed **59 pieces,17.00m²,USD774.45**. The lot remained one row with quantity50 and8m². Table/cards and Pack have explicit navigation after island mount.
- Fast keyboard length→Tab→width→Enter exposed duplicate measurement writes/409s. Added a per-rug measurement queue that coalesces identical in-flight values, serializes changed values with the latest row version, and has two regression tests. Retested rapid measurements of7006–7009 without409.
- Verified grid Escape returns focus, cost modal closes with focus handling, native output preview/print route renders, mobile cards and tablet grid work, and light/dark tokens render. Keyboard movement/reordering controls provide alternatives to dragging. Camera scanning/real printer were not exercised.
- The scripted walk reached Output in approximately251 seconds; extra debugging and first-time preset setup followed before print preview (roughly6minutes total). This was an engineering walk with interruptions, not a formal timed older-user trial. A precise action count was not captured, so no formal20-minute user-acceptance pass is claimed.
- Initial fresh default Output failed because optional custom color keys were absent. Documents owner corrected the fallback and proved fresh-template export in PostgreSQL tests. Latest runtime contains that fix.
- Raw mobile axe found unsupported `aria-readonly` on card divs, invalid article/listitem roles, faint hint contrast, a preview scroll region without keyboard focus, skipped heading levels, and the Pack title lost when Vue replaced Razor fallback content. All were fixed and the full32-case audit rerun clean.
- Mobile document overflow had two sources: closed off-canvas navigation remained laid out; table screen-reader text lacked a positioned containing block. Closed sidebar now uses `hidden`/`md:block`; the table scroll wrapper is `relative`. Final375px pages have no document horizontal overflow; table/tab strips scroll locally.

## Evidence

Durable summary and representative viewport images are in `reports/evidence/p9/` beside this report. Raw Lighthouse JSON, all32 axe JSON reports/screenshots and the audit script remain in the task's `work/a11y/` directory. No reports contain auth headers.

## Boundaries / remaining human acceptance

- Root registered IAnalyticsReadRepository → AnalyticsReadRepository.
- Root: commit-time 23505 translation in UnitOfWork/LedgerSession (root reports done).
- Formal older-user timed acceptance, physical camera/keyboard/printer checks remain human acceptance. CUA became unavailable after the usage-limit resume; final read-only audits/screenshots used P9's authorized CLI workflow.
- M7 production migration/backup and git/PR handling belong to root. Acceptance app is left running from copied Release binaries on `http://localhost:5189` at root's request; its disposable database is retained for handoff. No real database was accessed.
