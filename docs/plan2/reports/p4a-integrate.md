# p4a-integrate: report

Lane P4a works end to end, and every K.2/P4a acceptance bullet that P4a owns passed. I checked each one twice: in the tests, and in a live run of the app on the throwaway database `rugs_p4a_int`, with Persian data sent through the browser `fetch` path. The full lock matrix after finalize also passed. I changed no source files, because no glue was needed in lane-P4a files. The only failing test in the tree is in a P1 file; its one-line fix is under "Needs from others".

## Files
- **Created:** `docs/plan2/reports\p4a-integrate.md` (same content as this report).
- **Modified or deleted in the repo:** none.
- **Throwaway resources, all removed:**
  - the snapshot copy `docs/plan2/work\p4a-integrate\` (I unlinked its `node_modules` junction first, so the shared `node_modules` is untouched);
  - the database `rugs_p4a_int`.
- I never touched `rugs_management` and ran no git command that changes state.

## Verification (shared tree)

| Check | Result |
|---|---|
| Debug build | succeeded, 0 warnings |
| Release build with `-warnaserror` | succeeded, 0 warnings |
| Unit tests | 909/909 |
| Architecture tests | 15/15 |
| Integration tests, P4a + P4aWeb | 13/13 |
| Integration tests, full suite (run twice) | 351/352; the only failure is `PlatformServicesIntegrationTests.DiGraph_…` at line 65 |
| `has-pending-model-changes` | no changes |
| M3 script | 0 xmin lines; exactly one `ADD CONSTRAINT "UX_Bales_Shipment_Number" … DEFERRABLE INITIALLY DEFERRED` |
| `vue-tsc --noEmit` | exit 0 |
| `vitest` | 460/460 |
| `vite build` (snapshot) | built |
| App start on an empty DB | all 20 migrations applied, M3 included |

## Acceptance (live app and tests)

| Bullet | Result | Evidence |
|---|---|---|
| Create shipment and bales; add by serial; final sizes | **pass** | One-tap create gives «اظهار ۱». Adds by serial work, including Persian digits. A serial inside the lot range prompts `Q_LINE_SERIAL_IN_RANGE`. `measure` and `sameAsIntake` work. `PUT /final-size` returns 200. |
| Enricher and `?shipmentId=` filter | **pass** | A baled rug's RugDto carries location, shipment, bale and USD values. The filter returns exactly the 4 baled rugs. |
| Declared value defaults, re-default, re-value, stale stamp | **pass** | Changing the origin moves 45 → 50. Changing the key field or the fallback re-values every open line. A stale stamp gives 409 `E_DECLARED_STALE`. |
| Merchant fixes a new origin in one action | **pass** | POST «كاشان» at 40 returns `affectedOpenLines:20`. An existing key gives 409, a negative rate 400. |
| An add with a new bale number creates the bale; no empty bale | **pass** | A failed add to a new bale number creates no bale. A move prompts `Q_LINE_MOVE` before anything happens. |
| Finalize blocked with the full list, then succeeds | **pass** | Blocked with `E_EMPTY_BALE` and `E_NO_FINAL_SIZE` ×20. After fixing, it succeeds; all 24 rugs become Exported with location 2. |
| Lock after finalize | **pass** | Every write path returns 409 `E_SHIPMENT_LOCKED` with the Persian message; see the matrix below. |
| Bale and shipment delete; renumber in one flush | **pass** | Delete is allowed only for an empty bale, or an Open shipment with no lines. Renumber turns bales {2,3} into {1,2} in one save. |
| Reopen re-values; snapshot hash | **pass** | Lines are frozen while Finalized and re-valued on reopen. The hash is identical across three finalizes. Re-hashing the stored text in SQL equals `SnapshotHash`. |
| Bale totals by the lot rule | **pass** | Lot: 0.16 m² / 7.20 per piece, 8.00 m² / 360.00 per line. Bale 15.50 m² / 727.50; shipment 53 pieces / 18.44 m² / 859.80. All match the hand computation. |
| Workspace shell | **pass** | Open and Finalized shipments render, an unknown tab gives 200, and a foreign id gives 404. |
| Home shipment source (erratum E3) | **pass** | `/api/home/summary` lists the Open shipments and a correct stock count. |
| Performance (L.2) | **pass** | The test passes: finalize and reopen of 300 lines stay under 20 SQL commands. |
| Cost entries still work after finalize | **n/a** | Needs the P5 tables. |

**Lock matrix after finalize.** Each of these returned 409 `E_SHIPMENT_LOCKED`:
- **Rug edits:** every locked rug field through rug PUT, final size (set and clear), and bulk fields (including Archived and an origin change).
- **Rug actions:** lot split, sale, every workflow path (advance, skip, activate, back, apply-template, path PUT, bulk advance and bulk back), and trash through the MVC form (a 302 with the flash message).
- **Shipment and bales:** each line op in its own result, every bale write (including the no-op renumber), and the locked header fields.
- **The grid batch** is also already refused per row, which is P4b's area.

These still worked: rug notes, shipment notes, the bale target area, bale notes, image upload and step pricing. The C.10 refusals for selling or trashing a baled rug on an Open shipment also show the right Persian messages.

## Could not do
- **Cost-entry checks after finalize:** they need the P5 tables.
- **Operator role:** I did not create an Operator account in the live run. `P4aLockTests` and `P4aWebTests` assert the Operator's lock message and the per-role access.

## Needs from others
1. **Orchestrator (P1-owned file; P1-fix and p4a-domain both handed it on):** in `C:\Users\sadra\source\repos\RugsManagment_Service\tests\RugsManagment.IntegrationTests\PlatformServicesIntegrationTests.cs`, line 65, replace
   `Assert.IsType<EmptyHomeShipmentSource>(sp.GetRequiredService<IHomeShipmentSource>());`
   with
   `Assert.IsType<RugsManagment.Application.Features.Shipments.HomeShipmentSource>(sp.GetRequiredService<IHomeShipmentSource>());`
   This is the only failing test in the tree.
2. **P4b:**
   - Register the `shipment-list` and `shipment-header` islands.
   - Add `BalesTab : IShipmentTab`.
   - Put «قطعی کن» and «باز کردن دوباره» with their confirm dialog in the header island.
   - Decide the grid-batch row status and code for a lock refusal.

## Notes for the orchestrator
- **Browser cookies:** the browser pane shares cookies across ports on `localhost`. Another agent's sign-in on its own port overwrote my `rugs.auth` cookie, and my requests started returning 401. Serving the app as `http://<label>.localhost:<port>` avoids this.
- **Minor, no change needed:**
  - A duplicate declared-value key returns 409 with `{message, field}` and no `code`. This follows the I.1 convention and nothing depends on a code.
  - `BulkUpdateWorkflowAsync` has no web route, so its lock is covered by unit tests only.