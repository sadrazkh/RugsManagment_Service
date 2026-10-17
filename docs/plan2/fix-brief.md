# Review-fix brief (applies to every "fix-review" agent)

Read first: `scratchpad\hard-rules.md` (scratchpad = `docs/plan2`). You receive a list of CONFIRMED defects from an adversarial review of a committed snapshot (each: title, file, line, severity, scenario, fix, votes with evidence). Since that snapshot, later phases may have changed the code — re-locate each defect in the CURRENT working tree before fixing, and skip (with a note) any that no longer exists.

For each finding:
1. Reproduce or trace it in the current code.
2. Fix it at the root (not a symptom patch), consistent with `final-spec.md` + `spec-errata.md` (errata override).
3. Add a test that fails before and passes after (C# unit/integration or vitest).
4. Keep behaviour for legitimate users unchanged.

Edit only the files the findings need (other agents may be working in parallel — see your prompt for what they own). Then verify: `dotnet build RugsManagment_Service.slnx -c Release -warnaserror -p:NuGetAudit=false`, `dotnet ef migrations has-pending-model-changes` (clean), the unit/architecture/integration tests you touched plus the whole suite if the tree is quiet, `npm run build`, `npx vitest run`.

Report per finding: fixed (with the test name) / already gone / not reproducible (with evidence). Plus final command outputs with pass counts.
