# M4 CostLedger — manual changes

- Generated `20261017044805_CostLedger` with `dotnet ef migrations add CostLedger --project src/RugsManagment.Infrastructure --startup-project src/RugsManagment.Web --no-build` after a single-process build (`-m:1 -nr:false -p:NuGetAudit=false`).
- Removed the generated `CostEntries.xmin` column from `CreateTable`. PostgreSQL already supplies this system column. The designer/model snapshot keep the xmin concurrency mapping.
- M4 creates only CostEntries, CostEntryTargets, CostEntryPins, CostAllocations and CostEntryResults. The new Documents/Import configurations were temporarily excluded from compilation during generation and restored immediately afterward, preserving M4 → M5 → M6 migration order.
- No real database was accessed or migrated. The disposable integration-test database exercises this migration chain.
