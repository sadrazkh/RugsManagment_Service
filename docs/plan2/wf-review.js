export const meta = {
  name: 'rugs-phase-review',
  description: 'Adversarial multi-lens review of a committed phase snapshot of RugsManagment_Service, with per-finding refutation',
  phases: [
    { title: 'Review', detail: 'parallel reviewers, one lens each' },
    { title: 'Verify', detail: 'two independent skeptics try to refute each finding' },
  ],
}

// Child workflow: run it with workflow({ scriptPath: '<repo>\\docs\\plan2\\wf-review.js' }, { sha, baseSha, phase, intent, brief, lenses }).
// Adjust REPO / S / WORK if the repo lives elsewhere. WORK must be outside the repo and outside %TEMP%.
const REPO = 'C:\\Users\\sadra\\source\\repos\\RugsManagment_Service'
const S = `${REPO}\\docs\\plan2`
const WORK = 'C:\\Users\\sadra\\rugs-work'
const SHA = args.sha
const BASE_SHA = args.baseSha
const BASE = `You are reviewing ${args.phase} of ${REPO}, frozen at commit ${SHA}. Other engineers are editing the working tree right now, so NEVER read the working tree: read files with \`git -C "${REPO}" show ${SHA}:<path>\` and see the phase's changes with \`git -C "${REPO}" diff ${BASE_SHA} ${SHA} -- <paths>\` (use --stat first).
If you need to build/run/test, export your OWN snapshot: \`mkdir -p "${WORK}\\rv\\<your-label>" && git -C "${REPO}" archive ${SHA} | tar -x -C "${WORK}\\rv\\<your-label>"\`, then build there (\`dotnet build RugsManagment_Service.slnx -p:NuGetAudit=false\`; frontend needs \`npm ci\` in src/RugsManagment.Web/ClientApp). Tests: RUGS_TEST_PG="Host=localhost;Port=5432;Username=postgres;Password=7082". Delete your snapshot folder when done.
What the phase was supposed to do: ${args.intent}
Spec & briefs: ${S}\\final-spec.md, ${S}\\spec-errata.md, ${args.brief}; architecture map ${S}\\map.md.
READ-ONLY for the repo: never modify files in ${REPO}, never run git commands that change state, never write to the database rugs_management (throwaway DBs named rugs_rv_* are allowed and must be dropped). Never pass Persian text through the shell.
Report only REAL defects you can point to with file:line (at ${SHA}) and a concrete failure scenario (inputs/state -> wrong behaviour). No style nits, no speculative "consider" items.`

const FINDING_SCHEMA = {
  type: 'object',
  properties: {
    findings: { type: 'array', items: { type: 'object', properties: {
      title: { type: 'string' }, file: { type: 'string' }, line: { type: 'number' },
      severity: { type: 'string', enum: ['critical', 'high', 'medium', 'low'] },
      scenario: { type: 'string', description: 'concrete inputs/state -> observed wrong behaviour' },
      fix: { type: 'string' },
    }, required: ['title', 'file', 'line', 'severity', 'scenario', 'fix'] } },
  },
  required: ['findings'],
}
const VERDICT_SCHEMA = {
  type: 'object',
  properties: { real: { type: 'boolean' }, reason: { type: 'string' }, evidence: { type: 'string' } },
  required: ['real', 'reason'],
}

async function run(prompt, opts) {
  for (let attempt = 0; attempt < 3; attempt++) {
    const r = await agent(prompt, { ...opts, label: attempt === 0 ? opts.label : `${opts.label} (retry ${attempt})` })
    if (r) return r
  }
  return null
}

phase('Review')
const results = await pipeline(
  args.lenses,
  lens => run(`${BASE}\n\nYOUR LENS: ${lens.prompt}`, { label: `review:${lens.key}`, phase: 'Review', schema: FINDING_SCHEMA })
    .then(r => (r ? r.findings : []).map(f => ({ ...f, lens: lens.key }))),
  findings => parallel(findings.map(f => () =>
    parallel([0, 1].map(i => () => run(`${BASE}\n\nA reviewer reported this defect. Your job is to REFUTE it if you can: read the actual code at ${SHA}, trace the real behaviour, and decide whether the failure scenario can actually happen. ${i === 1 ? 'Try to reproduce it concretely (a small test in your own snapshot, a build, or a precise trace through the code).' : 'Check whether existing code elsewhere already prevents it.'} Default to real=false if the scenario cannot occur.\n\nFINDING: ${JSON.stringify(f)}`,
      { label: `verify:${f.file.split(/[\\/]/).pop()}:${f.line}#${i}`, phase: 'Verify', schema: VERDICT_SCHEMA })))
      .then(vs => {
        const v = vs.filter(Boolean)
        const yes = v.filter(x => x.real).length
        return { ...f, votes: v, confirmed: yes >= 1 && yes >= v.length / 2 }
      })))
)

const all = results.filter(Boolean).flat().filter(Boolean)
const confirmed = all.filter(f => f.confirmed)
log(`findings: ${all.length}, confirmed: ${confirmed.length}`)
return { confirmed, rejected: all.filter(f => !f.confirmed).map(f => ({ title: f.title, file: f.file, line: f.line, why: f.votes.map(v => v.reason).join(' | ') })) }
