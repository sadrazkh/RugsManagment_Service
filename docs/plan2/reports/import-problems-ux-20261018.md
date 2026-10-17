# Import problems and explicit row decisions — frontend

Date: 2026-10-18. Changes limited to the isolated `work/repo` frontend; backend work and live browser verification coordinated by root.

The old optional, paginated editor could hide the two blockers among157 rows, and filtered out rows with blank serials. The preview now automatically renders every error row in a separate, unpaginated problems panel. Each card identifies the exact Excel row, serial or empty-serial label, bale, field, reason and exact related Excel rows with their serials/bales. Inline corrections use the existing NumberField and text controls; unrelated fields remain in a per-row disclosure. The optional full list remains closed initially and supports all/issues/skipped filters with50-row pagination.

Each row has an explicit skip decision with a visible undo. Decisions are staged until the primary, sticky “بازبینی و اعمال تصمیم‌ها” action completes. The commit button explains why it is disabled and names the accepted row count. Edits, skips, undo and options are covered by the preview signature; stale in-flight responses are discarded. Policy skips are explained separately without a meaningless per-row undo. Footer control rows are read-only and are not labelled as missing-serial rugs.

Warnings are separate/collapsible and do not block commit. Reconciliation shows calculated included-row totals alongside the file footer and summary values, explaining that excluded rows change the calculated totals. A named, keyboard-focusable scroll region contains the table.

Persisted lastPreview restores sheet, mapping, options, corrected values and explicit skips. The canonical job query id is written with history.replaceState so refresh resumes that job. Switching worksheets synchronously clears row-based corrections and decisions, clears the old preview and explains the reset; row2 on one sheet can never silently affect row2 on another. Restored decisions are hydrated only after assigning their saved sheet.

Accessibility includes44px controls, responsive field layouts, row-specific accessible labels, unique generated NumberField ids even when the optional list duplicates a problem card, and focus transferred to the recheck button when a decision removes its original card.

Verification:

- Full frontend suite: **566/566**,36 files.
- Import-specific helper/SSR suite: **14/14** including existing preview-gate cases.
- Strict Vue typecheck and production build: **passed**.
- New cases cover discovery beyond rows50/100 in a157-row dataset, blank serials, exact related references, sorted/deduplicated skip/undo, stale response gating, explicit null/blank correction values, persisted multi-sheet decisions, cross-sheet isolation, policy skips, footer read-only rendering and duplicate-card id uniqueness.
- Root's live CUA test reported both blockers at Excel rows70/151 visible immediately in a160-row workbook; skip151 → recheck produced zero errors and enabled commit for159 accepted rows. Root owns final refresh/mobile/browser evidence and backend test results.

No real database, git mutations or extra frontend dependency were used by this lane.
