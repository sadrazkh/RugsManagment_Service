## p1-web report (WP1-A2), resumed run

The p1-web package is complete. The build passes with `-warnaserror`, all 250 unit tests pass, and all 142 P1 and web-host integration tests pass. 24 older P0 integration tests now fail because their test fixture does not register `IMoneyVisibility`. That fixture is not my file; the exact fix is under "Needs from others".

Repo root: `C:\Users\sadra\source\repos\RugsManagment_Service`. Paths below are relative to `src/RugsManagment.Web/` unless they start with `tests/`.

### Already done by the killed attempt (audited file by file against the brief and spec)

**Wiring and auth**
- `Program.cs`:
  - `AddAuthorization(o => o.AddRugsPolicies())`.
  - The `heavy` rate limiter, per user (policy names in `RateLimitPolicies`).
  - `DomainExceptionPageFilter` registered globally.
  - `IMoneyVisibility` → `MoneyVisibility`, via `Replace`.
  - `IUserPreferencesAccessor` → `UserPreferencesAccessor`.
  - Automatic scoped registration of every `INavContributor`, `IHomeCard` and `ISettingsLink` in the Web assembly.
  - API 401/403 return JSON instead of a redirect; model-binding errors return `{message, errors}`.
- `Auth/Policies.cs`: the three policies, their role lists, the `Is*` helpers and `RateLimitPolicies`.
- `Filters/DomainExceptionPageFilter.cs`: error toast plus a 302 back to the referring page (same-site only), with `PageToast` levels.
- `Controllers/Api/ApiControllerBase.cs`: defaults to Floor. Validation errors → 400 `{message, errors}`; conflicts → 409 `{message, field, …Extensions}`.

**Support classes**
- `UserPreferencesAccessor`: the «نمای ساده» preview cookie only counts for TenantAdmin.
- `RoleLabels`: delegates to `Application.Common.RoleLabels`.
- `MoneyVisibility`.
- `DisplayHelpers`: «ثبت‌شده»; «صادر شده» with tertiary colour and the truck icon; a Simple-mode status variant.
- `PersianFormat`: `JalaliDotted` and `Usd`.
- `TenantSettingsAccessor`: `CurrencyCode` (IRT or IRR), `CanSeeMoney`, and a `Money()` helper that shows «—» to anyone who may not see money.

**Controllers and views**
- The J.2 sweep covers every controller. `PricingApiController` is also set to Business, because it returns amounts.
- New endpoints: `MeApiController`, `SettingsPreferencesApiController`, `HomeApiController`.
- New controllers: `MoreController`, `HelpController`, `Settings/SettingsHubController`, `Settings/PreferencesController`.
- `Settings/CoreSettingsLinks.cs`.
- `Views/Settings/{Hub, Preferences, Activity, Tenant, CustomFields, StepTypes}`: currency select limited to Toman and Rial.
- `Views/Users/*`: role radio buttons with a one-line hint for each role.
- `Views/Groups/*`: renamed to «ورودی».
- `Views/Rugs/{Index, Details}`: money guards and Simple-mode hiding.
- `Views/Dashboard/Index`: money guards. `Views/Admin/*`: role label.
- `appsettings*.json`: `Storage:DataRoot`; `Seed:Demo` (false by default, true in Development).
- Integration tests in `tests/RugsManagment.IntegrationTests/P1/`: `P1TestSupport`, `P1AuthMatrixTests`, `P1MoneyVisibilityTests` (Operator JSON and HTML money-leak checks, the E5 null-versus-non-null money writes, and 403s), and `P1DomainExceptionPageFilterTests`.

### Completed in this run
- `Views/Account/Profile.cshtml`: the old «مدیر کارگاه» wording now says «تنظیم‌کنندهٔ کارگاه».
- `Views/Account/Denied.cshtml`: clearer wording for the 302 destination, and a lock icon.
- `Support/DisplayHelpers.cs`: added `RugStatus(string?, bool simple)`.
- `Views/Dashboard/Index.cshtml`: in Simple mode, the recent-rugs list shows the Simple status and hides the workshop step name.
- `Controllers/RugsController.cs`: a `DomainConflictException` from Delete now always gives an error toast and a 302 to the rug's Details page, even without a Referer header. The same from Restore returns to Trash.
- `Controllers/LabelsController.cs`: comment wording only.
- New `tests/.../P1/P1PolicyMapTests.cs` (no database). It checks, by reflection:
  - every action has a known gate, and only an allow-list is anonymous or plain `[Authorize]`;
  - every API controller derives from `ApiControllerBase`;
  - everything under `/api/settings/*` and `Controllers/Settings` requires Configure;
  - every MVC POST validates the anti-forgery token;
  - the full J.2 table, as a Theory.
- `tests/.../P1/P1AuthMatrixTests.cs`: added the pre-existing pages that need an id. Groups/Details and Labels/Print are open to all three roles, and `/Help` redirects to `/Help/Card`. Sales Print/CSV and the provider statement are Business-only, so Operator gets 403. Workflow Edit and Label Design are Configure-only, so Merchant and Operator get 403.

### Verification
- `dotnet build RugsManagment_Service.slnx -p:NuGetAudit=false -warnaserror`: 0 warnings, 0 errors.
- `dotnet test tests/RugsManagment.Tests`: 250 of 250 passed.
- Integration tests were run with `RUGS_TEST_PG=Host=localhost;Port=5432;Username=postgres;Password=7082`. Each test class makes its own throwaway `rugs_it_*` database and drops it; none were left behind. The real `rugs_management` database was not touched.
  - P1 tests plus the web-host smoke and cookie tests: 142 of 142 passed.
  - Full integration suite: 183 passed, 24 failed. All 24 have the same cause: `TwoTenantFixture.BuildRealServices` cannot resolve `IMoneyVisibility`.
- Frontend: `npx vue-tsc --noEmit` passed. `vite build` passed into a scratch folder, not `wwwroot/dist`, so it would not clash with other agents. The CSS contains the Tailwind classes these files return from C# and use in views, such as `bg-tertiary/12`, `bg-primary/12` and `has-[:checked]:bg-primary/5`.

### What I could not do, and why
- **The `[IsolationSample]` tenant-isolation test (K.1 and L.2) is not built.** That is the test that calls every action by reflection with another tenant's ids and expects 404. The brief's p1-web scope does not list it, and doing it properly needs samples for about 124 actions, so it needs an owner.
- **P0 integration failures**: the fix belongs in a P0 support file that I am not allowed to edit.
- **Test database names**: the P0 harness names throwaway databases `rugs_it_<hex>`, not `rugs_p1_<x>`. They are still created and dropped per class.
- **Hard-rule note**: I ran read-only `grep` searches with Persian patterns in the shell. No Persian was ever written through the shell; every Persian edit went through Write or Edit.

### Needs from others (exact edits)
- **p1-integrate**: in `tests/RugsManagment.IntegrationTests/Support/TwoTenantFixture.cs`, inside `BuildRealServices`, after `services.AddInfrastructure(configuration);`, add:
  `services.AddScoped<RugsManagment.Application.Common.IMoneyVisibility>(_ => RugsManagment.Application.Common.FullMoneyVisibility.Instance);`
  This fixes all 24 failures. p1-core deliberately leaves `IMoneyVisibility` unregistered outside the web host so that a missing registration fails loudly instead of showing money, so do not add a default in Application DI.
- **p1-core (or P2, which owns the seeder)**: the demo seed in `DatabaseSeeder.cs:136` names its RugBatch «محمولهٔ نمونه». It shows on the «ورودی‌ها» screen and breaks the glossary as data. Rename it to «ورودی نمونه».
- **Unowned in P1, for p1-integrate to decide**: `Views/Rugs/Create.cshtml` still has the title «ثبت فرش» and the heading «ثبت فرش جدید». The glossary rename calls the action «ثبت فرش تازه», which `Rugs/Index` and `Dashboard` already use.