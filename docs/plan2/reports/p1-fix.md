All 16 confirmed Phase 1 defects (17 entries in `p1-review.json`; #2 and #5 are the same) are fixed or already gone, and the checks pass apart from failures that belong to other lanes. Most of the work was already on disk from the earlier stopped attempts; this run checked it all and added two small hardening fixes.

**What was already on disk (checked in the current code, tests pass):**
- **#1 money guard:** an Operator's empty `pricingConfigJson` now gets 403 on the pricing, advance and bulk-advance routes, and the stored step price is untouched.
- **#2/#5 last-admin race:** an edit that could remove an active admin now runs under a per-tenant Postgres lock inside one transaction. A test runs two admins demoting each other at the same moment and always ends with exactly one admin.
- **#3 tenant-creation password:** the 8-character `PasswordPolicy` is now applied in the service, the form model and `AdminController`.
- **#4 activity paging:** any `?page=` value now returns 200; past the end it shows the last page.
- **#6, #9 size field:** the «۲۵ متر» answer is remembered, a stored 13–30 m side is no longer cleared just by focusing, and the tenant's «بزرگ‌ترین ضلع معمول» now reaches the page and the parser.
- **#7 thousands separators:** «,»/«٬» are accepted only in correct three-digit groups. «۲٬۵م», «45,55» and «1,23» are now rejected with a message instead of silently becoming 10× or 100× larger.
- **#8 out-of-range years:** `isoToJalali` returns null and `faJalali` shows «—» instead of throwing.
- **#10 offline queue:** on 401, 403 or an antiforgery 400 the replay stops, keeps the queue and shows «دوباره وارد شوید». Items are dropped only on errors that can never succeed, with a visible message, and the queue owner is checked against `/api/me` first.
- **#11–#13 Combobox and date field:** Combobox shows the label when options arrive late, and Enter on an untouched empty field submits the form. In the date-field calendar, Esc closes it from anywhere inside, and tabbing out closes it.
- **#14 help drawer:** Business-only steps are hidden from Operators.
- **#16 offline page font:** `offline.html` now loads the precached Vazirmatn font directly, and the service-worker cache version is bumped to v5.
- **#17 hamburger button:** it now stays 44 px at «آ++»; this is checked by a source test only, not measured in a browser.
- **#15 «گروه» wording:** already gone. Phase 2 fixed `RugManagementService`, and P3 fixed `RugBatchService`. A test now scans all user-facing strings in Application and Web for it.

**Added in this run:**
- **Combobox:** typing and then clearing the text no longer highlights the first option, so Enter still submits.
- **Date field:** the calendar can no longer navigate outside 1200–1500, the years it can display.
- **Each comes with a new test.** Both new tests fail against the pre-fix components from d45368b and pass now, as do 14 of the earlier component tests.
- I also corrected a comment in `sw.js` that named a test that does not exist.

**Verification:**
- **Release build with `-warnaserror`:** succeeded, 0 warnings.
- **Unit tests:** 878 of 879 pass. The one failure is the timing test `Lr_TenThousandMembers_IsFast` under load from the parallel agents; it passes when run alone.
- **Architecture tests:** 15 of 15 pass.
- **Integration tests:** the 19 P1 fix and related tests all pass. The full suite has 326 of 330 passing; none of the 4 failures is in my area:
  - `XlsxGoldenTests` (P6, still in progress)
  - `P4aWebTests` (P4a, still in progress)
  - `HomeStock` (P2 review finding #5, still in progress)
  - `DiGraph`, which still expects the old empty home shipment source that P4a replaced
- **`npx vitest run`:** 432 tests in 27 files pass.
- **`npm run build`:** succeeds.

**Needs from others:**
1. **p3-grid-ui:** in `ClientApp/src/islands/rugs/RugsGrid.island.vue` line 42, change `maxPlausibleSideM: () => 12` to `() => ctx.maxPlausibleSideM`. Until then the rugs grid ignores the tenant's «بزرگ‌ترین ضلع معمول».
2. **P4a or the owner of `PlatformServicesIntegrationTests.cs`:** in `DiGraph_ResolvesEveryPlatformService_AndRegistersTheSeedTaskAt100`, change the expected type from `EmptyHomeShipmentSource` to `HomeShipmentSource` (spec errata E3).

I did not open the app in a browser: the 375 px header at «آ++» and the offline page under a real service worker are covered only by source-level tests. No migrations were added, the real `rugs_management` database was not touched, no git commands that change state were run, and the scratch folder was deleted.

The full report is at `docs/plan2/reports\p1-fix.md`.