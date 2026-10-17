# Phase 1 review fixes (run after Phase 2 is integrated)

Read first: `scratchpad\hard-rules.md`. The confirmed findings with full reproduction evidence are in `scratchpad\p1-review.json` (field `confirmed`; #2 and #5 are the same defect). They were found at commit `2b5870c`; Phase 2 has since changed some of these files (e.g. `RugManagementService.cs`, step pricing, `money.ts`) — re-locate each defect in the CURRENT code before fixing, and skip (with a note) any that Phase 2 already removed.

For each finding: fix it at the root, add a test that fails before and passes after (C# unit/integration or vitest), and keep behaviour for legitimate users unchanged.
Specific guidance:
- **pricingConfigJson ""** — the money guard must treat any non-null PricingConfigJson (including empty/whitespace) from a non-Business user as a money member → 403; 403 tests on pricing, advance and bulk-advance routes.
- **Last-admin guard race** — make the check atomic: take a per-tenant advisory transaction lock (or SELECT … FOR UPDATE on the tenant's admin rows) inside the same transaction as the write, via the existing UnitOfWork/ExecuteInTransactionAsync; integration test with two concurrent demotions.
- **Tenant-creation password** — apply `PasswordPolicy` to `CreateTenantViewModel` / `AdminController.Create` / `TenantManagementService`.
- **Activity paging overflow** — clamp page/pageSize and compute OFFSET in long; never 500.
- **NumberField dim** — keep the metres answer for 13–30 (per G.4: integers between MaxPlausibleSideM and 30 ask «متر یا سانت؟» once and remember the answer for that value), never clear an existing stored value on focus/blur without user input; read `MaxPlausibleSideM` from the app context (tenant preferences) instead of the hard-coded 12.
- **parseDecimal grouping** — accept «,»/«٬» only as valid thousands grouping (groups of exactly 3 digits after the first, integer part only); otherwise reject (invalid input), never silently scale; vitest rows for «۲٬۵م», «45,55», «1,234,567», «1,23».
- **jalali range** — `isoToJalali`/`faJalali` return null/«—» for out-of-range years; JalaliDateField never throws.
- **offline replay** — on 401/403/antiforgery-400 stop replaying and keep the queue (show the «دوباره وارد شوید» toast) instead of dropping items; only drop on validation 4xx that can never succeed, with a visible message.
- **Combobox** — show the label when options arrive after mount; Enter on an untouched empty field submits the form (no implicit selection).
- **JalaliDateField popover** — Esc closes and Tab moves out from anywhere inside the popover.
- **Help per role** — `lib/help.ts` content must follow the viewer's policies (Operator never sees money/Business-only steps).
- **«گروه» wording** — use «ورودی» for RugBatch in the activity line and batch error messages (`Messages.fa`).
- **offline.html font** — reference the cached, versioned font CSS (or inline @font-face pointing at a precached woff2) and make sure `sw.js` precaches what offline.html needs; bump the CACHE version.
- **XLarge hamburger** — keep the menu button ≥ 44×44 px at XLarge on 375 px.

Verify: Release build `-warnaserror`, all unit/architecture/integration tests, `npm run build`, `npx vitest run`. Report per finding: fixed / already gone after P2 / not reproducible now, with the test name.

