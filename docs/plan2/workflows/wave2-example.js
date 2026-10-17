export const meta = {
  name: 'rugs-wave2c',
  description: 'Resume wave 2 (again): P1 fixes + P2 review fixes + Wave A (P3 grid, P4a shipments, P6 engine) in parallel from partial work on disk; then lane checks and global integration',
  phases: [
    { title: 'Fixes', detail: 'P1 review fixes (17) and P2 review fixes (25), each with tests' },
    { title: 'Packages', detail: 'P3 grid-ui/grid-server, P4a domain/web, P6 engine — resumed in parallel' },
    { title: 'Lane checks', detail: 'per-lane integration and acceptance walk' },
    { title: 'Global', detail: 'whole-solution build, all tests, cross-lane glue, smoke test' },
  ],
}
const S = 'C:\\Users\\sadra\\source\\repos\\RugsManagment_Service\\docs\\plan2'
const RESUME = `IMPORTANT — RESUMED RUN: earlier attempts of THIS task were stopped mid-work (twice), and part of the work is already on disk, uncommitted (\`git -C C:\\Users\\sadra\\source\\repos\\RugsManagment_Service status\` against commit d45368b — about 150 changed files from all packages of this wave). Also look in ${S}\\reports\\ for notes (e.g. p3-grid-server.md, p3-grid-server-contract.md). The old Temp scratchpad was wiped by Windows, so any scratch/spike folders you made there are gone — redo only what you still need, under ${S}\\work\\<your-label>\\. Do NOT start over and do NOT blindly overwrite: first audit what exists for YOUR scope, then finish what is missing or half-done, and verify everything. Report what you found already done vs what you completed now.`
async function run(prompt, opts) {
  for (let attempt = 0; attempt < 3; attempt++) {
    const r = await agent(prompt, { ...opts, label: attempt === 0 ? opts.label : `${opts.label} (retry ${attempt})` })
    if (r) return r
    log(`${opts.label} returned nothing (attempt ${attempt + 1}); retrying`)
  }
  return `${opts.label} FAILED 3 times — no report`
}
const WAVE = `Running in parallel in the same working tree right now: P1-fix (Phase 1 review fixes; brief p1fix-brief.md), P2-review-fix (25 confirmed Phase 2 findings in p2-review.json; brief fix-brief.md), P3 (intake grid; brief p3-brief.md), P4a (shipments domain + workspace shell; brief p4a-brief.md), P6-engine (document engine; brief p6e-brief.md). Phase 2 is committed (d45368b); its reports are in ${S}\\reports\\p2-*.md.`
const pre = (brief, who) => `You are a senior .NET 10 / Vue 3 engineer. You are agent "${who}". Read FIRST and follow exactly: ${S}\\hard-rules.md and ${S}\\${brief} ("scratchpad" in every brief means ${S}). ${WAVE} Never edit files owned by another running package; transient build errors from their half-done files are expected (wait and retry). Write your report also to ${S}\\reports\\${who}.md.`

const LANES = [
  { key: 'P3', brief: 'p3-brief.md', pkgs: ['p3-grid-ui', 'p3-grid-server'], integ: 'p3-integrate' },
  { key: 'P4a', brief: 'p4a-brief.md', pkgs: ['p4a-domain', 'p4a-web'], integ: 'p4a-integrate' },
  { key: 'P6e', brief: 'p6e-brief.md', pkgs: ['p6-engine'], integ: null },
]

const [p1fix, p2fix, laneResults] = await parallel([
  () => run(`${pre('p1fix-brief.md', 'p1-fix')}\n\nFix every confirmed Phase 1 finding (${S}\\p1-review.json, field "confirmed") per the brief. Phase 2 is already integrated, so re-locate each defect in the current code. You own the P1 files these fixes need (components/ui/*, lib/num.ts, lib/jalali.ts, lib/offlineQueue.ts, lib/help.ts, Messages.fa core, offline.html, sw.js, the admin/tenant/user/activity services and controllers). If a fix needs a file a Wave A lane owns, write the change under "needs from others".\n\n${RESUME}`, { label: 'p1-fix', phase: 'Fixes' }),
  () => run(`${pre('fix-brief.md', 'p2-review-fix')}\n\nFix the 25 CONFIRMED Phase 2 review findings in ${S}\\p2-review.json (field "confirmed"; each has file, line, scenario, suggested fix and verifier evidence). You own the Phase 2 files they point at (RugFieldWriter, RugManagementService, RugSaleService, StepTypeService, LedgerSession, CostCalculationService, PlatformRepositories (stock card only), WorkflowEngine, AnalyticsService, CostCategorySeedContributor, the M2 migration's Down (hand edit + HandEdits note; NEVER change Up — it already ran on the real DB), RugForm.vue, RugFinalSize/PurchaseLinesEditor/RugCostBreakdown/RugSplit islands, RugSalePanel.vue). Do NOT edit P1 shared libs being fixed in parallel by p1-fix (components/ui/*, lib/num.ts, lib/jalali.ts): use their existing API; if you need a new helper there, put it in a new file or report it. Do NOT edit P3/P4a/P6 files. Fix the high-severity Operator money bypass first.\n\n${RESUME}`, { label: 'p2-review-fix', phase: 'Fixes' }),
  () => parallel(LANES.map(l => async () => {
    const reports = await parallel(l.pkgs.map(p => () =>
      run(`${pre(l.brief, p)}\n\nImplement the "${p}" package of your brief completely; touch only the files it owns.\n\n${RESUME}`, { label: p, phase: 'Packages' })
        .then(r => ({ key: p, report: r }))))
    let integ = null
    if (l.integ) {
      integ = await run(`${pre(l.brief, l.integ)}\n\nImplement the integration section of your brief for lane ${l.key}. You may fix glue ONLY in files owned by lane ${l.key} (its packages); report anything in other lanes' files under "needs from others". Package reports:\n${JSON.stringify(reports, null, 1)}`, { label: l.integ, phase: 'Lane checks' })
    }
    return { lane: l.key, reports, integ }
  })),
])

phase('Global')
const global = await run(`You are a senior .NET 10 / Vue 3 engineer, agent "wave2-global". Read ${S}\\hard-rules.md ("scratchpad" means ${S}). P1-fix, P2-review-fix and Wave A (P3 intake grid, P4a shipments domain, P6 document engine; briefs ${S}\\p1fix-brief.md, ${S}\\fix-brief.md, ${S}\\p3-brief.md, ${S}\\p4a-brief.md, ${S}\\p6e-brief.md) have just been implemented by parallel agents in the same working tree; their reports are in ${S}\\reports\\. Now nobody else is editing. Do the whole-solution integration: (1) \`dotnet build RugsManagment_Service.slnx -c Release -warnaserror -p:NuGetAudit=false\`, fix every error/warning (minimal changes, report each with file:line and which package it belonged to); (2) \`dotnet ef migrations has-pending-model-changes\` must be clean (if migrations conflict, apply the spec B.15 regenerate rule; M2 20261004063353 already ran on the real DB — never change its Up); (3) all unit, architecture and integration tests (RUGS_TEST_PG set) — fix failures caused by cross-package interaction; (4) \`npm run build\`, \`npx vitest run\`; (5) run the app on a throwaway DB seeded by the demo seeder and smoke-test in the browser pane: Home, Intake grid (type 3 rows incl. a lot), Rugs list/details (purchase lines, final size), Shipments list/workspace (create a shipment, add a bale, add rugs by serial), Settings hub, Declared values, a CSP-clean console; drop the DB after; (6) apply every "needs from others" item from all reports that is still open. Write your report to ${S}\\reports\\wave2-global.md and return it: what you fixed, final command outputs (pass counts), open problems.\n\nP1-FIX REPORT:\n${p1fix}\n\nP2-REVIEW-FIX REPORT:\n${p2fix}\n\nLANE RESULTS:\n${JSON.stringify(laneResults, null, 1)}`, { label: 'wave2-global', phase: 'Global' })
return { p1fix, p2fix, laneResults, global }
