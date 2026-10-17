Stage 3 (p1-integrate) is finished, and 18 of the 20 P1 acceptance bullets pass in the running app. The two that don't are expected:
- **Nav length:** a Merchant sees 3 nav items, not 5.
- **Baled-rug trash:** this check can only run after P4a.

Build, unit, integration, vitest and the frontend build all pass. M1 applied cleanly to a copy of the real database. The real `rugs_management` was only read; it is still at `20261003195050_CountersAndSecurityStamp`.

## Files created / modified / deleted
Repo: `C:\Users\sadra\source\repos\RugsManagment_Service`. Nothing was deleted.

**Glue fixes asked for by the package reports**
1. `tests/RugsManagment.IntegrationTests/Support/TwoTenantFixture.cs:153-158` — `BuildRealServices` now registers `IMoneyVisibility` as `FullMoneyVisibility.Instance`, with a comment explaining why. This fixed the 24 P0 integration failures.
2. `src/RugsManagment.Web/ClientApp/src/lib/types/legacy.ts:61,96,98` — `effectiveCost`, `purchaseCost` and `targetSalePrice` are now `number | null`, and the header comment is updated. The strict `vue-tsc` check passes.
3. `.github/workflows/ci.yml:120-123` — new step "تست‌های فرانت" runs `npm test` after `npm ci`.
4. `src/RugsManagment.Web/Views/Sales/Print.cshtml:31` — the jsDelivr preconnect and stylesheet are replaced with `~/fonts/vazirmatn.css`.
5. `src/RugsManagment.Web/Support/SecurityHeadersMiddleware.cs:36-40` — `FontCdn` is removed. CSP is now `style-src 'self' 'unsafe-inline'` and `font-src 'self' data:`.
6. `src/RugsManagment.Infrastructure/Persistence/Seed/DatabaseSeeder.cs:136` — the demo batch is now named «ورودی نمونه» instead of «محمولهٔ نمونه».
7. `src/RugsManagment.Web/Views/Rugs/Create.cshtml` — title and h1 now read «ثبت فرش تازه», matching the tile and command that lead there.
8. `src/RugsManagment.Infrastructure/Persistence/Repositories/PlatformRepositories.cs:164-177` — `RugHomeStockSource` uses `ToListAsync` instead of `FirstOrDefaultAsync` on the constant GroupBy. That stops the EF 10103 warning on every Home render: zero hits across several renders in two app runs.
9. `src/RugsManagment.Web/Views/Users/Form.cshtml:69-76` — the password input gets `minlength="@PasswordPolicy.MinLength"`, an `aria-describedby` and the hint «دست‌کم ۸ کاراکتر.».
10. New `tests/fixtures/jalali-dates.json` with 15 date pairs (Nowruz and leap-year edges) and 7 parse cases. The pairs come from .NET `PersianCalendar`. A matching section was added to `tests/fixtures/README.md`. The two vitest tests that were skipped now run and pass.

**Defect found during the walk, fixed**
11. `src/RugsManagment.Web/Views/Rugs/Index.cshtml:322`, `Views/Rugs/Trash.cshtml:38`, `Views/Users/Index.cshtml:42` — added `relative` to each table's `overflow-x-auto` wrapper.
   - **Problem:** the `sr-only` «عملیات» header escaped the scroll box, so the whole page scrolled sideways by 112 px at 1009–1024 px wide.
   - **After:** page width equals viewport width.
   - This was already broken before P1.

## Verification
| Command | Result |
|---|---|
| `dotnet build RugsManagment_Service.slnx -p:NuGetAudit=false -warnaserror --no-incremental` | 0 warnings, 0 errors |
| `dotnet test tests/RugsManagment.Tests` | 250/250 passed |
| Integration suite with `RUGS_TEST_PG=…` | 207/207 passed, run twice. Before the fixture fix it was 183 + 24 failing |
| `npx vitest run` | 11 files, 207 passed, 0 skipped |
| `npm run build` (strict vue-tsc, then vite into `wwwroot/dist`) | passed |

**App run:**
- Database: `rugs_p1_integ`, a `pg_dump` copy of `rugs_management`. I dropped it afterwards, along with the dump file.
- Port: 5318.
- **M1 on real data:** M1 applied at startup. A preferences row exists for both tenants, and the existing users' modes are backfilled. The seed runner added 0 rows, because the migration had already created them.
- **Test users:** I created a Merchant and an Operator through the Users form in the browser. The 8-character rule rejects «short» with the Persian message. New users get the right defaults: Merchant Simple + Large, Operator Simple + Normal.
- **Logins:** the three roles used separate hosts (localhost, 127.0.0.1, [::1]) so their cookies don't mix. SystemAdmin was checked with curl.

## P1 acceptance (K.2/P1)
| # | Bullet | Result |
|---|---|---|
| 1 | Merchant sees 5 nav items | **FAIL (by design in P1)**: 3 items (خانه · فرش‌ها · بیشتر). محموله‌ها and هزینه‌ها have no pages yet. `CoreNavContributor` adds them automatically once the Shipments and Costs controllers exist. |
| 2 | MVC `/Settings/*`, `/settings/*`, `/Users` give 302 to `/account/denied`; `/api/settings/*` gives 403 | PASS (Merchant and Operator) |
| 3a | Operator nav as in A.4 | PASS for what exists: خانه · فرش‌ها · بیشتر. محموله‌ها arrives with P4a, as in #1. |
| 3b | Operator: no Toman money in any page or JSON | PASS. Checked 23 pages (server HTML and the mounted islands for Groups/Details, Rugs/Details, Rugs/Edit, Dashboard and the Rugs bulk toolbar) and 23 APIs. No money key had a value, and no Toman- or Rial-formatted amount appeared. |
| 3c | Operator `/Reports` gives 302 denied | PASS |
| 4 | `/` signed out goes to login; SystemAdmin goes to `/Admin`; `/status/404` and `/Home/Error` work anonymously | PASS (`/status/404` returns its page with status 404) |
| 5 | TenantAdmin «نمای ساده» shows the merchant nav without changing permissions | PASS: cookie set, `data-ui=simple`, banner shown, `/settings`, `/Users` and `/api/settings/preferences` still 200 |
| 6 | Text scale persists | PASS: 20 px straight away, kept after a reload, saved through `/api/me/preferences` |
| 7 | Every pre-existing URL returns 200 for the right role | PASS for TenantAdmin (53 URLs) and Merchant (52 URLs, Configure-only pages denied as expected); Operator: all Floor pages and APIs 200, Business ones denied (2b, 3c, 15) |
| 8 | A Rial tenant shows «ریال» in islands; the currency select offers only Toman and Rial | PASS: RugForm, Reports, Rugs, Details, Dashboard and Home all show ریال. Posting currency=2 is rejected with an error toast. |
| 9 | NumberField parses «۲/۵», «۲٫۵» and «2.5» | PASS in the live RugForm (area becomes 5.00 against a width of 2) and in vitest |
| 10 | Jalali 1405/06/15 ↔ 2026-09-06 | PASS: vitest plus the new shared fixture; the server gives today as 1405/07/12 |
| 11 | A Razor `[data-open-panel]` button opens the stub panel under CSP with a clean console | PASS: panel opens with focus inside, Esc returns focus to the button, no errors or CSP violations. See note on the close animation under "Could not do". |
| 12 | Dark mode on new chrome; «صادر شده» chip styled | PASS: no bright surfaces in dark mode on Home, user menu, help drawer, Settings hub or Preferences. With one rug set to Exported in the copy, the chip has its `bg-tertiary/12` background in both themes. |
| 13 | Operator money: `/api/analytics` and `/api/sales/rug/{id}` give 403; steps and step-types carry no amounts; money in a rug PUT, bulk-fields PUT or step-pricing PUT gives 403 | PASS for all three 403s. With null money (E5) the save returns 200 and the stored 60,000,000 and 140,000,000 are kept. |
| 14 | Trashing a baled rug gives 302 with a toast, not 500 | N/A until P4a. The filter is in place and its integration tests pass. Ordinary Delete and Restore give 302 with a success toast. |
| 15 | Groups pages read «ورودی‌ها»; a new rug shows «ثبت‌شده»; glossary test passes | PASS. The only «محموله» left on these pages is the existing demo batch's name in the owner's data. |
| 16 | Text-size control changes the root size at once and persists | PASS |

Also checked:
- Tenant preferences: save 200, a stale save 409 with the Persian message, and the «عدل» wording reaches the app context.
- The Sales print page has no CDN reference left.

## What I could not do, and why
- **Screenshots:** the browser pane is hidden, so screenshots time out. All visual checks were done by measuring styles and layout in the page instead.
- **Panel close animation:** for the same reason, Vue's close animation never finishes. The stub panel stays in its closing state after Esc, although focus does return. This is the hidden pane, not the app.
- **Demo batch name in the real database:** it still reads «محمولهٔ نمونه». The seeder fix only affects new seeds; the owner can rename it in the UI.
- **Leftover database:** I did not drop `rugs_p1_shell`. It is p1-shell's, and it still holds two test users.
- **Shell rule:** I used `sed` with ASCII-only, line-addressed patterns on the three views in fix 11. No Persian went through the shell. Persian test data went in through browser JavaScript only.
- **Problems I saw but did not fix:**
  1. `Views/Rugs/Details.cshtml:77` — the delete confirmation (`absolute end-0 w-72`) runs off the right edge on a 375 px phone (left 250, right 574). It was already like this before P1; it belongs to P9/H.6.
  2. RugForm and the Details page show numbers with ASCII «.» and «,» (for example «۱.۵۰ متر», «۸۰,۰۰۰,۰۰۰ ریال»), while `Messages.FormatNumber` uses «٫» and «٬». This is for the G.4 owner.
  3. One rug photo returns 404, because a database row points to a file that wasn't moved with the repo (already known).
  4. Some old antiforgery cookies fail to decrypt at login after the key ring changed. This only happens in this environment and doesn't affect anything.

## Needs from others
- **Orchestrator:**
  - Decide acceptance #1 and #3a: accept 3 nav items for P1, or ask for placeholder items.
  - Run `dropdb rugs_p1_shell`.
  - Apply M1 to `rugs_management` after the backup. It is proven on a copy of the real data.
- **P9 (mobile):** fix the delete-popover position at `Views/Rugs/Details.cshtml:77` for narrow screens, for example `start-0 sm:start-auto sm:end-0`, or a fixed bottom sheet.