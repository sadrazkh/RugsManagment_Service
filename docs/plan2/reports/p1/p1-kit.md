p1-kit (WP1-B1) is finished. Frontend type-check and build pass, all 11 vitest files pass (205 tests, 2 skipped on purpose), and the 250 .NET unit tests pass. All scratch work is cleaned up (harness server stopped, browser tab closed, copied node_modules removed).

## What the previous attempt had already done
Most of the scope was on disk and correct:
- **Libraries:** `api.ts` keeps `errors`, has `postForm`, `download` and `del` with a body. `offlineQueue.ts` is keyed by tenant and user from app context. `num.ts` does exact BigInt rounding, half away from zero. `money.ts` parses per G.4. `numWords.ts` matches C# `NumberWords`. `jalali.ts` maps 1405/06/15 to 2026-09-06. `format.ts`, `ids.ts`, `labels.ts`, `help.ts`, `bulkActions.ts` and `labelFields.ts` were in place.
- **Components:** NumberField, JalaliDateField, Modal, Drawer, Field, Button, Combobox, EmptyState, HelpDrawer, Stepper, plus two extras, `TextScaleControl.vue` and `useFocusTrap.ts`. MoneyInput and CostRuleEditor were updated.
- **Islands:** money is hidden for non-Business users; RugBulkToolbar hides its workshop actions in Simple mode and has the `[data-bulk-action]` slot; GroupDetail uses a dialog instead of `prompt()`; all label sites go through `lib/labels.ts`. `PreferencesForm.island.vue` exists.
- **Tests and tooling:** vitest is added to `package.json`, with `vitest.config.ts`, eight `lib/*.spec.ts` files and `lib/messages/messages.spec.ts`.

## What I completed or fixed now
- **Status labels (`labels.ts`):** Simple-mode rug status now matches the server's `DisplayHelpers` («در انبار» with the warehouse icon). Step-status text and colours now match the server exactly.
- **Server parity tests (`labels.spec.ts`):** status and role labels are read from the C# files and must match. Every help and status icon must exist in `icons.svg`.
- **Help icons (`help.ts`):** each step now has a fitting icon (bale, ruler, coins, calendar, keyboard, scan, lock, type, help).
- **Message parity test (`messages.spec.ts`):** core codes must match `Messages.fa*.cs` text exactly, and any kit code the server also uses must have the same text.
- **Invisible characters:** `money.ts`, `jalali.ts` and `Combobox.vue` had literal invisible characters in their regexes. They now use `\p{Cf}`, and tests were added for this.
- **NumberField:**
  - An invalid or still-ambiguous entry now sets the value to null while typing. Before, typing «208» left the value at 2, so the parent's live area was wrong.
  - Uses `text-end`, as H.4 asks.
- **JalaliDateField:**
  - **Bug fix:** an impossible date typed over a valid one showed no error. The field's own null update was clearing the message.
  - Day buttons are now 44 px.
  - New `label` prop for the "required" message.
- **Modal and Drawer:**
  - Modal now sits at z 68 and Drawer at z 67, above the shell's panel host (66) and below its confirm dialog (70).
  - The focus trap now reuses `trapFocus` / `focusableElements` from `ui.ts`.
  - Modal focuses the first form control instead of the close button.
- **PreferencesForm:** a 409 that names a field (`moneyScale`) now shows under that field.
- **RugForm:** the estimated margin is shown in the tenant's currency.
- **New `lib/ui.spec.ts`** (L.3): the panel glob resolves, an unknown panel name fails gracefully, the stub panel opens and closes, and `blockReload` works.
- **Optional shared date fixture:** `jalali.spec.ts` runs `tests/fixtures/jalali-dates.json` if it exists. Those are the 2 skipped tests.

## Verification
- `npx vitest run`: 11 files, 205 passed, 2 skipped.
- `npm run build` (strict vue-tsc, then vite): passed.
- `dotnet build RugsManagment_Service.slnx -p:NuGetAudit=false`: succeeded, 0 warnings. `dotnet test tests/RugsManagment.Tests`: 250 passed, including the glossary test.
- **Nullable money types:** I type-checked the whole client against a scratch copy of `legacy.ts` where `effectiveCost`, `purchaseCost` and `targetSalePrice` are `| null`. It compiles cleanly.
- **Number words:** the C# `NumberWords` output (about 450 values) matches `numWords.ts` exactly.
- **Browser harness checks:**
  - «۱۰م» gives 10,000,000 with the words line «ده میلیون تومان». «10.000» asks and Enter gives 10,000.
  - «10.5» shows «مبلغ تومان اعشار ندارد». «208» asks «۲۰۸ ← ۲٫۰۸ متر؟». «25» offers «۲۵ سانت» first. «۲۰۸×۲۱۰» fills both size fields. «۲/۵» gives 2.5.
  - The Rial toggle shows «یک میلیون ریال · صد هزار تومان».
  - The date field accepts 14050615, shows the impossible-date and Gregorian-year errors, and the امروز/دیروز chips, arrow keys and PageDown work.
  - The Combobox offers «افزودن …» and matches «ك» to «ک».
  - Modal and Drawer: focus trap, Esc, and focus returns to the opener.
  - The text-size control sends `PUT /api/me/preferences`, updates `data-text-scale`, and reverts if the save fails.

## What I could not do, and why
- **Full app walk:** not possible until p1-core adds migration M1. The components were tested in an isolated harness instead.
- **`legacy.ts` nullable types:** left as they are, because `lib/types/*` belongs to the contracts/integrate stages.
- **Hard rules:** no Persian text went through the shell. I did use `sed` on files that contain Persian, with ASCII-only, line-addressed patterns. One `sed` lost its backslashes; I fixed it with Edit and checked the final bytes.

## Needs from others
- **p1-integrate:** in `ClientApp/src/lib/types/legacy.ts`, set `RugWorkflowStep.effectiveCost: number | null`, `Rug.purchaseCost?: number | null` and `Rug.targetSalePrice?: number | null`. The type check above shows every island already handles null.
- **p1-integrate (or the ci.yml owner):** add `npm test` after `npm ci` in `.github/workflows/ci.yml`. L.3 asks for vitest in CI.
- **p1-core (optional):** add `tests/fixtures/jalali-dates.json` as `{ "dates":[{"iso":"2026-09-06","jalali":"1405/06/15"}], "parse":[{"text":"...","iso":null,"error":"E_DATE_IMPOSSIBLE"}] }`. vitest picks it up automatically.
- **p1-web and p1-shell:** no edits needed. Two dependencies to know about:
  - `labels.spec.ts` now fails if `Support/DisplayHelpers.cs` or `Application/Common/RoleLabels.cs` drift from `lib/labels.ts`.
  - Modal and Drawer rely on `trapFocus` and `focusableElements` in `lib/ui.ts`, and on the shell's z-order (panel 66 < confirm 70).

Files are under `C:\Users\sadra\source\repos\RugsManagment_Service\src\RugsManagment.Web\ClientApp\`:
- **New this run:** `src\lib\ui.spec.ts`
- **Changed this run:**
  - `src\lib\labels.ts`, `labels.spec.ts`, `help.ts`, `money.ts`, `money.spec.ts`, `jalali.ts`, `jalali.spec.ts`
  - `src\lib\messages\messages.spec.ts`
  - `src\components\ui\{NumberField, JalaliDateField, Modal, Drawer, Combobox}.vue`
  - `src\components\ui\useFocusTrap.ts`
  - `src\islands\settings\PreferencesForm.island.vue`
  - `src\islands\RugForm.vue`