p1-core (WP1-A1) is finished. The previous attempt had already done almost all of it and I found nothing half-done. This run added one missing piece: the password rule for the user-admin pages. One cross-package fix is still open: 24 older integration tests now fail at setup. The fix is two lines in a file p1-core doesn't own, and I checked it works (below).

## Found already done (audited file by file)

**Domain** (`src/RugsManagment.Domain`): the Stage 1 contract files are complete. All Domain enums have explicit numbers.

**Migration M1 `PlatformPreferences`** (`Infrastructure/Persistence/Migrations/20261003234036_PlatformPreferences.cs`, plus its Designer, the snapshot and `HandEdits/PlatformPreferences.md`):
- Every B.3 table and column is there.
- The xmin DDL is removed. The generated script has no xmin lines.
- Existing users are back-filled exactly like `ApplyRoleDefaultPreferences`.
- Each existing tenant gets a preferences row (`ON CONFLICT DO NOTHING`).
- `Exportable` defaults to true.
- `has-pending-model-changes` reports no changes.

**Infrastructure:**
- `EntityConfigurations.cs` (User, AuditEntry, CustomFieldDefinition) and `PlatformConfigurations.cs` (TenantPreferences, TenantGridLayout).
- `Repositories/PlatformRepositories.cs` has three classes:
  - the preferences repository, with the version check and the "money exists" check;
  - the audit repository, which pages in SQL;
  - the home "stock" source, which uses the lot rule in SQL.
- `Seed/TenantSeedRunner.cs`: a startup task with Order 100, also called on tenant create.
- `AuditLog.cs` writes `DetailsJson`. `DatabaseSeeder.cs` applies the role defaults.
- `Features/*/…InfrastructureModule.cs` and `DependencyInjection.cs` (module calls only).

**Application:**
- `Features/Platform/`:
  - `PlatformModule`, `TenantPreferencesService`, `UserPreferencesService` and `HomeSummaryService`;
  - the preferences seed contributor;
  - the three home seams `IHomeShipmentSource`, `IHomeStockSource` and `IHomeMoneySource`.
- Empty module files for Ledger, Intake, Shipments, Costs, Documents and Import, wired from `DependencyInjection.cs`.
- `ITenantPreferencesRepository` and `IAuditEntryRepository`; `IAuditLog` now takes `details`.
- `MoneyRedaction`: the per-DTO redactors, the cost-sort guard and the step-pricing write rule.
- Money redaction in `RugManagementService`, `EntityMappers`, `WorkflowManagementService` (including `DashboardService`), `ServiceProviderService`, `StepTypeService` and `LookupService`.
- Money members are nullable in the rug, dashboard, provider and workflow DTOs.
- The E5 rule: a null money member is ignored and the stored value kept; a non-null one from a non-Business user gives 403.
- `ActivityLogService`: a label for every `AuditAction` and paging in SQL.
- `UserManagementService`: Merchant allowed, role defaults, labels from `RoleLabels`, and the last-admin and self-demotion guards.
- `TenantManagementService` calls the seed runner. `TenantSettingsService` only accepts Toman or Rial.

**Tests:**
- Unit tests in `tests/RugsManagment.Tests/`: Fakes, Fixtures, Glossary, Messages, MoneyRedaction, NumAreaMath, NumberWords, PlatformServices and SampleShipment.
- `tests/fixtures/sample-shipment.json` and `math-vectors.json`. The fixtures README is corrected per E18.
- Integration tests: `PlatformPreferencesMigrationTests.cs` and `PlatformServicesIntegrationTests.cs`, on throwaway `rugs_p1_core_*` databases.

## Completed now
- **New `src/RugsManagment.Application/Common/PasswordPolicy.cs`.** Section J.3 puts the user-admin password policy in P1, and before this an admin could set a 1-character password. The rule is now one place: at least 8 characters, with a Persian message.
- **`Services/UserManagementService.cs`:** creating a user, or an admin resetting a password, rejects a short password with a 400 on the `password` field. The check runs before anything is written or the session stamp changes.
- **`Services/ProfileService.cs`:** now uses the same rule.
- **`tests/RugsManagment.Tests/PlatformServicesTests.cs`:** 7 new tests for the rule.
- **Fixture check:** I compared all 161 lines of `sample-shipment.json` against `scratchpad\xlsx\dump.txt` with `scratchpad\verify-fixture.cjs`. Every value matches: serial, sizes, columns J, K and L, colours, pattern, origin, material and lot quantities. The 6 dump rows left out are repeated header rows. Totals: 36 bales, 242 pieces, 464.74 m², USD 20,322.50.

## Verification
| Check | Result |
|---|---|
| `dotnet build RugsManagment_Service.slnx -p:NuGetAudit=false -warnaserror` (also with `--no-incremental`) | 0 warnings, 0 errors |
| `dotnet test tests/RugsManagment.Tests` | 250/250 passed (was 243) |
| Integration tests `PlatformPreferencesMigrationTests` and `PlatformServicesIntegrationTests` | 16/16 passed (M1 from a pre-M1 database, Down then Up, xmin concurrency, cascade, DI graph, seeding, 409s, SQL paging, lot rule, Operator redaction) |
| The same plus the `P1` filter, after my change | 147/147 passed |
| Full integration suite | 116/140; all 24 failures are the `TwoTenantFixture` setup error below |
| `dotnet ef migrations has-pending-model-changes` | no changes |
| `dotnet ef migrations script` for M1, searched for xmin | 0 hits |

- **Databases:** no `rugs_p1_core_*` or `rugs_it_*` databases are left. `rugs_management` was only read; it is still at M0 (`20261003195050_CountersAndSecurityStamp`), so M1 is not applied.
- **`npm run build` / vitest:** not run. p1-core owns no frontend files.

## What I could not do, and why
- **I didn't apply the `TwoTenantFixture` fix.** It is a P0 test file and no P1 package owns it.
- **Tenant currency is not locked once money exists.** The MoneyScale lock is done. The currency lock is the P2 `PreferencesLockGuard` (L.1, D.5 #22), so I left it.
- **No test checks that seeded icons exist in `icons.svg`.** It doesn't fit P1: today's seeded step types use Material Symbols names such as `water_drop`, not sprite ids. The H.8 test belongs with the P2/P7 seeds.

## Needs from others (exact edits)
1. **p1-integrate:** in `tests/RugsManagment.IntegrationTests/Support/TwoTenantFixture.cs`, `BuildRealServices`, add this right after `services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<FakeCurrentUser>());`:
   ```csharp
   services.AddSingleton<RugsManagment.Application.Common.IMoneyVisibility>(RugsManagment.Application.Common.FullMoneyVisibility.Instance);
   ```
   - **Cause:** the services now require `IMoneyVisibility`, and this fixture never registers one, so its dependency-injection check fails before any test runs.
   - **Checked:** I ran a patched copy in the scratchpad. CrossTenantIsolation, StepPricing, SecurityStamp and UserLostUpdate: 24/24 pass, including the new password rule.
   - **Why not a default:** I kept the Application layer without a default `IMoneyVisibility` on purpose. A host that forgets to register it fails at startup instead of quietly showing money to Operators.
2. **p1-web (optional):** the server now enforces the password rule, and `UsersController` already shows the message on the form. On the client side, `Views/Users/Form.cshtml` could add `minlength="8"` and a hint to the password input, and `UserFormViewModel.Password` could take `[MinLength(8, ErrorMessage = "رمز عبور باید حداقل ۸ کاراکتر باشد.")]`.