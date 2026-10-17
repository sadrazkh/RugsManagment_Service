## p1-shell (WP1-B2): report for the resumed run

The shell package is complete. The previous attempt had already built almost all of it. This run audited each file against the brief and spec, fixed six defects or gaps, added a test file, and checked everything in the running app. One acceptance bullet is not met in P1 by design: a Merchant sees 3 nav items, not 5, because the محموله‌ها and هزینه‌ها pages don't exist yet (see "Could not do").

### Already done by the previous attempt (audited, kept)
All under `C:\Users\sadra\source\repos\RugsManagment_Service\src\RugsManagment.Web\`:
- **`Views\Shared\_Layout.cshtml`**: app-context JSON per I.6, `data-ui` and `data-text-scale` on `<html>`, nav from NavBuilder with an explicit bottom nav, skip link, user menu with the text-size radiogroup, the «نمای ساده» preview switch and banner, TempData toast with level.
- **`Navigation\`**:
  - `NavBuilder.cs`: merges contributors by key, keeps contributor failures from breaking the page, caches per request.
  - `SettingsNavContributor.cs`: «تنظیمات» plus one Ctrl+K command per `ISettingsLink`.
  - `AppShell.cs` (extra): builds the app-context payload, gives Iran-time `today`, Jalali dates and the enum names.
- **`Home\`**:
  - `HomeCardsViewComponent.cs`.
  - `Cards\TilesCard.cs`: handles the 0, 1 and ≥2 open-shipment cases, the Operator wording, and the one-tap «محمولهٔ تازه».
  - `Cards\StockCard.cs`.
  - Extras: `Cards\RecentCard.cs` and `MerchantSteps.cs` (one text for the home coach and the help card).
- **Views**:
  - `Views\Home\Index.cshtml`, `Views\More\Index.cshtml` (the stub-panel test button shows only for Configure users with `?diag=1`), `Views\Help\Card.cshtml` (one A4 page, no layout).
  - `Views\Shared\Components\HomeCards\{Default, Tiles, Stock, Recent}.cshtml`.
- **`ClientApp\src\`**:
  - `islands\registry.ts`: the `*.island.vue` glob plus the 19 legacy islands, with duplicate-name detection.
  - `lib\ui.ts`: panel host, `blockReload` / `requestReload`, toast levels, `openHelp`, `setTextScale`, `window.rugsUI`.
  - `main.ts`, `islands\UiShell.vue`, `components\OfflineIndicator.vue`.
  - `panels\Stub.panel.vue`, `islands\platform\HomeCoach.island.vue`.
  - `styles\app.css`: the `@source` lines for the C# folders, root sizes 16/18/20 px, and the `simple:` / `full:` variants.
- **`wwwroot\`**:
  - `icons.svg`: 70 symbols, the full J.2 list.
  - `fonts\`: Vazirmatn v33.003 variable woff2 (signature `wOF2`, SHA-256 matches the official release zip), plus `vazirmatn.css` and `OFL.txt`.
  - `offline.html` (uses the self-hosted font) and `js\site.js` (the `[data-open-panel]` delegate and the other `data-*` behaviours).

### Completed or fixed in this run
1. **`Navigation\CoreNavContributor.cs`**: the full A.4 nav table is now in one place.
   - ورود فرش, محموله‌ها, هزینه‌ها and ورود از اکسل appear only once their controllers (Intake, Shipments, Costs, Import) exist, each in the right slots and under the right policy.
   - Once P4a and P5 land, the Merchant gets 5 items and the TenantAdmin's bottom nav is exactly the A.4 five, with no edit here.
   - New `ShellFeatures` constants hold the controller names; `TilesCard.cs` now uses them too.
2. **`_Layout.cshtml`**:
   - The desktop sidebar is sticky, so the nav no longer scrolls away on long pages.
   - At XLarge text between 768 and 1023 px the header overflowed the page; the help label and user name now hide in that range. Measured page width now equals viewport width.
   - On phones the menu chevron is hidden, which leaves room for the page title.
   - Toast keys now come from the `PageToast` constants.
3. **`components\CommandPalette.vue`**:
   - Bug: the hidden «کدام محموله؟» sheets on Home carry `aria-modal`, which would have switched off the `n` and `/` hotkeys whenever there are 2+ open shipments. Only visible modals now count.
   - Added a focus trap (checked: Tab wraps to the first item).
   - `n` also works on a Persian keyboard layout.
   - The workflow step is hidden in Simple mode (A.3).
   - Text no smaller than `text-xs`.
4. **`wwwroot\js\site.js`**: a one-tap create button whose reply has no id now reloads, instead of going to a literal `{id}` URL.
5. **`HomeCoach.island.vue` and `Views\Home\Index.cshtml`**: the help tip is a separate unnumbered line (new `tip` prop), not a numbered sixth step.
6. **New `ClientApp\src\islands\registry.spec.ts`** (9 tests):
   - the island registry and that it has no duplicate names;
   - `openPanel` finds panels through the glob, and an unknown name fails gracefully;
   - open/close results and replacing an open panel;
   - `blockReload` counting;
   - toast levels.

No files were deleted.

### Verification
- `dotnet build RugsManagment_Service.slnx -p:NuGetAudit=false -warnaserror`: 0 warnings, 0 errors.
- `dotnet test tests/RugsManagment.Tests`: 250 of 250 passed, glossary test included.
- `npm run build` (vue-tsc strict, then vite): passed. The built CSS contains `bg-tertiary/12` from `DisplayHelpers.cs`, the «صادر شده» chip class.
- `npx vitest run`: 11 files, 205 passed, 2 skipped (the skips are not in my files).
- App run on the throwaway DB `rugs_p1_shell`, port 5317, as each role:
  - **TenantAdmin**: Full sidebar with groups, 20 Ctrl+K commands, Iran date 1405/07/12.
    - «نمای ساده» preview: the merchant's menu and `data-ui=simple`, with permissions unchanged.
    - Text size: changes the root to 20 px at once and survives a reload.
    - Stub panel from a Razor `data-open-panel` button: opens with title and focus set; Esc closes it and focus returns to the button.
  - **Merchant**: Simple, Large, money visible, tiles in the 0-open state.
  - **Operator**:
    - Simple, no money in the page or app-context, no «تومان» anywhere on Home.
    - Tile reads «…از بازرگان بخواهید یکی بسازد.»
    - «بیشتر» has no Business pages; `/Reports` redirects.
  - **Rial tenant**: app-context currency switches to `IRR` / «ریال».
  - Dark and light mode both checked, plus a 375 px phone view; the help card renders.
- The only console errors were service-worker registration failures. The browser pane blocks service workers; `/sw.js` itself returns 200.

### Could not do / why
- **«A Merchant sees 5 nav items»** shows 3 in P1 (خانه · فرش‌ها · بیشتر). The /Shipments and /Costs pages don't exist yet, and dead links would be worse. Both items appear automatically when those pages arrive.
- **`lib/ui.spec.ts`** (the name L.3 uses) is in p1-kit's `lib/*.spec.ts` area, so the same tests went into `islands/registry.spec.ts`, next to my own `registry.ts`.
- **I did not drop `rugs_p1_shell`**, because that is a permanent deletion. It holds two test users (`merchant.shell@test.local`, `operator.shell@test.local`) whose password I set to the seed demo password, only in that throwaway DB. Run `dropdb rugs_p1_shell` when you're done with it.

### Needs from others
- **p1-integrate** (files nobody owns):
  - `Views\Sales\Print.cshtml:31-32` still loads Vazirmatn from jsDelivr. Replace both lines with `<link rel="stylesheet" href="~/fonts/vazirmatn.css" asp-append-version="true" />`.
  - Then remove `FontCdn` (jsDelivr) from `style-src` and `font-src` in `Support\SecurityHeadersMiddleware.cs:21,41-42`.
  - Optionally rename `islands/registry.spec.ts` to `lib/ui.spec.ts` if the exact L.3 file name matters.
- **p1-core**: every Home render logs EF warning 10103 ("First/FirstOrDefault without OrderBy"). It comes from a query run by `IHomeSummaryService`.
- **Later phases** (contract, no P1 action):
  - Nav and Home light up when controllers named `Intake`, `Shipments`, `Costs` and `Import` exist.
  - The shipment workspace must honour `?tab=bales` and `?tab=output`.
  - `POST /api/shipments` must return `{id}`.
  - The cost panel must be named `cost-entry` and accept `{scope: 2, shipmentId}`.