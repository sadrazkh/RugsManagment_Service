export const meta = {
  name: 'rugs-wave3',
  description: 'Wave 3: review of wave 2 in parallel with P4b (baling UI) and P5 (cost ledger) then P6-wiring (documents from DB); global integration; fix review findings',
  phases: [
    { title: 'Packages', detail: 'P4b ui/web and P5 domain/web in parallel; then P6w server/web after P5' },
    { title: 'Lane checks', detail: 'per-lane integration and acceptance walk' },
    { title: 'Global', detail: 'whole-solution build, all tests, cross-lane glue, smoke test' },
    { title: 'Fix review', detail: 'fix confirmed wave-2 review findings with tests' },
  ],
}
const S = 'C:\\Users\\sadra\\source\\repos\\RugsManagment_Service\\docs\\plan2'
const RESUME = `IMPORTANT — RESUMED RUN: a previous attempt of THIS task died before it could report, and part of its work may already be on disk. Do NOT start over and do NOT blindly overwrite: first audit (git status/diff against HEAD, read the files, and ${S}\\reports\\) what exists for your scope, then finish what is missing or half-done, and verify everything. Report what you found already done vs what you completed now.`
async function run(prompt, opts) {
  for (let attempt = 0; attempt < 3; attempt++) {
    const r = await agent(attempt === 0 ? prompt : `${prompt}\n\n${RESUME}`, { ...opts, label: attempt === 0 ? opts.label : `${opts.label} (retry ${attempt})` })
    if (r) return r
    log(`${opts.label} returned nothing (attempt ${attempt + 1}); retrying`)
  }
  return `${opts.label} FAILED 3 times — no report`
}
const WAVE = `Wave 3 lanes running in parallel in the same working tree: P4b (baling UI; brief p4b-brief.md) and P5 (cost ledger + allocation; brief p5-brief.md), and after P5 finishes, P6-wiring (documents from the DB; brief p6w-brief.md). Waves 0–2 are committed (HEAD 82e2383: P0–P2, P3 grid, P4a shipments, P6 engine, P1/P2 review fixes); all earlier agent reports are in ${S}\\reports\\ (read the ones for the code you build on, e.g. p3-*, p4a-*, p6-engine.md, wave2-global.md — its "open problems" list items for P4b and P6-wiring).`
const pre = (brief, who) => `You are a senior .NET 10 / Vue 3 engineer. You are agent "${who}". Read FIRST and follow exactly: ${S}\\hard-rules.md and ${S}\\${brief} ("scratchpad" in every brief means ${S}). ${WAVE} Never edit files owned by another running package; transient build errors from their half-done files are expected (wait and retry). Write your report also to ${S}\\reports\\${who}.md.`

async function lane(key, brief, pkgs, integ) {
  const reports = await parallel(pkgs.map(p => () =>
    run(`${pre(brief, p)}\n\nImplement the "${p}" package of your brief completely; touch only the files it owns.`, { label: p, phase: 'Packages' })
      .then(r => ({ key: p, report: r }))))
  const integration = await run(`${pre(brief, integ)}\n\nImplement the integration section of your brief for lane ${key}. You may fix glue ONLY in files owned by lane ${key} (its packages); report anything in other lanes' files under "needs from others". Package reports:\n${JSON.stringify(reports, null, 1)}`, { label: integ, phase: 'Lane checks' })
  return { lane: key, reports, integ: integration }
}

const [review, lanes] = await parallel([
  () => workflow({ scriptPath: `${S}\\wf-review.js` }, args.review).catch(e => ({ error: String(e) })),
  () => parallel([
    () => lane('P4b', 'p4b-brief.md', ['p4b-ui', 'p4b-web'], 'p4b-integrate'),
    async () => {
      const p5 = await lane('P5', 'p5-brief.md', ['p5-domain', 'p5-web'], 'p5-integrate')
      const p6w = await lane('P6w', 'p6w-brief.md', ['p6w-server', 'p6w-web'], 'p6w-integrate')
      return [p5, p6w]
    },
  ]),
])

phase('Global')
const global = await run(`You are a senior .NET 10 / Vue 3 engineer, agent "wave3-global". Read ${S}\\hard-rules.md ("scratchpad" means ${S}). P4b (baling UI), P5 (cost ledger, migration M4) and P6-wiring (documents from DB, migration M5) — briefs ${S}\\p4b-brief.md, ${S}\\p5-brief.md, ${S}\\p6w-brief.md — have just been implemented by parallel agents in the same working tree on top of HEAD 82e2383; their reports are in ${S}\\reports\\. Now nobody else is editing. Do the whole-solution integration: (1) \`dotnet build RugsManagment_Service.slnx -c Release -warnaserror -p:NuGetAudit=false\`, fix every error/warning (minimal changes, report each with file:line and which package it belonged to); (2) \`dotnet ef migrations has-pending-model-changes\` must be clean; migration order must be M3 Shipments → M4 CostLedger → M5 Documents (apply the B.15 regenerate rule if needed; never change M2 or M3 — they ran on the real DB); (3) all unit, architecture and integration tests (RUGS_TEST_PG set) — fix cross-package failures; (4) \`npm run build\`, \`npx vitest run\`; (5) run the app on a throwaway DB seeded by the demo seeder and smoke-test the whole merchant flow in the browser pane: intake 3 rugs (incl. a lot) in the grid → create a shipment → bale them on the baling board with final sizes → add a freight cost for the shipment and a washing cost for the rugs (cost panel, Simple mode) → see the cost split on the rugs and the shipment summary → export the xlsx and open the print page; CSP-clean console; Persian data only through the UI or the browser fetch path; drop the DB after; (6) apply every "needs from others" item from all wave-3 reports that is still open. Write your report to ${S}\\reports\\wave3-global.md and return it: what you fixed, final command outputs (pass counts), open problems.\n\nLANE RESULTS:\n${JSON.stringify(lanes, null, 1)}`, { label: 'wave3-global', phase: 'Global' })

phase('Fix review')
const confirmed = (review && review.confirmed) || []
let fix = 'no confirmed findings'
if (confirmed.length) {
  fix = await run(`You are a senior .NET 10 / Vue 3 engineer, agent "wave2-review-fix". Read ${S}\\hard-rules.md and ${S}\\fix-brief.md and follow them ("scratchpad" means ${S}). The review was of the wave-2 commit 82e2383 (P3 grid, P4a shipments, P6 engine, P1/P2 review fixes); since then P4b, P5 and P6-wiring landed in the working tree (reports in ${S}\\reports\\, see wave3-global.md). Nobody else is editing now. Never change migrations M2/M3 (they ran on the real DB). Write your report to ${S}\\reports\\wave2-review-fix.md.\n\nCONFIRMED FINDINGS:\n${JSON.stringify(confirmed, null, 1)}`, { label: 'wave2-review-fix', phase: 'Fix review' })
}
return { review, lanes, global, fix }
