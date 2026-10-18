# GTIP MP Test v0.27 — construction, upgrades, economy histories and visual movement

Both copies require **v0.27 / protocol 21**. The October 18 v0.26 logs show the guest remaining connected for approximately 25 minutes, with no exceptions in the latest Unity log. Guest depot placement reached the host and was rejected by an extra civilian debt-rating gate. Upgrade requests were rejected through new-construction policy checks or a missing map-icon requirement. These findings explain specific failures; they do not establish complete native UI parity.

Army placement of forts, supply depots and telegraph stations now follows the native army placement route without the civilian debt-rating gate. The host still validates the caller, building policy, map bounds, terrain, enemy proximity and native placement restrictions. Civilian construction retains its funding rules. Building upgrades use native next-level funding at the actual building location and the native ownership, maximum level, active-upgrade, fort and supply-reform restrictions. They no longer require the policies for constructing a new building. Buildings & Production runs after guest hover resolution and sends the selected building directly, including buildings without a map icon. The iconless host transaction retains the native upgrade duration, treasury/subsidy charges, construction spending and resource consumption; a native-metadata check compares those operations with the original implementation.

Checkpoint buildings are initially matched by native building type, map location and name where available, then assigned host session identities. Loaded list ordering cannot silently attach a same-prefab building ID to another location. Later updates validate location as well as type. A connection log records the initial binding count.

The snapshot now includes all finance-chart datasets, bilateral per-good import/export histories, and alliance construction/weapon expense histories. The latter feed native trailing-year expense calculations and were previously absent. Existing price, demand, production, market/depot stock, trade-volume, tax, tariff and value fields remain replicated. Both clients' open Goods & Trade tables refresh current values in place, preserving scroll/selection; native host rows otherwise only refreshed when opened. Finance values and the selected building's information refresh while open. Guest goods/finance charts explicitly redraw after received history changes. Native trade selection is driven after current-frame guest hover resolution.

Guest map icons, flags, names, infantry sprite targets and ship models now interpolate between received host positions using real elapsed time, with a bounded interval and no extrapolation. A new endpoint starts at the current displayed position. Pauses and large discontinuities snap to the received coordinates. Only drawing reads and explicit visual children use the blend; logical formation/block coordinates, movement orders, construction range checks and per-revision map readback remain authoritative. This adds bounded visual delay and does not increase the frequency of campaign-state delivery. Smoothness and rendering performance still require connected testing.

At revision 1 and every 30 revisions, each connection log's `.economy.csv` companion records host-source or guest-readback goods fields, charts, bilateral flows and expense histories. Compare matching revisions. Existing `.state.csv`, scalar checksums and map-position readback remain enabled.

Verification: **289 multiplayer model/protocol/TCP checks**, 21 native-bridge checks, 9 replica-boundary checks, 42 editor-presentation checks, and the existing hierarchy/editor/AI suites. Native metadata and synthetic snapshot checks cover **34 schemas / 628 fields**. Staging verifies **108 hooks/changes and 98 preserved methods**. These checks do not execute Unity gameplay.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `75DCD4B81DC542B25D8EBC6D53B8C5F91D4033FBFE2095D45E67FA70C71B07A2`; runtime `0FA8372FCD41E9576697047658626E681C8DD0ACDBE4902A12C177D6450DAAA1`. The installed runtime passes native-metadata and synthetic snapshot checks. Existing installer backups were preserved.

## Connected test for v0.27

1. Restart both games and join a fresh session from **GT MP Test**. At the current low debt rating, place a guest army supply depot at an otherwise valid site. Compare the building, native cost notice and one treasury charge on both copies. Repeat a host placement. Check civilian construction still respects native rating/subsidy restrictions.
2. On the guest **Buildings & Production** screen (image 4 in the report), upgrade an eligible Union Bank or company. Keep the page open and compare its upgrade timer and funding deduction with the host. Also test an eligible map depot upgrade, the supply-reform restriction, maximum level and an already-running upgrade. Repeat an upgrade from the host.
3. Open Goods & Trade on both copies with the same selected product and filters. Compare average price, demand, production, market/depot stock, trade volume, sales taxes, tariffs, value and every import/export partner. Run briefly, pause, and compare again without reopening either screen.
4. Select the same finance dataset and alliance checkboxes on both copies. Compare graph dates and points, including treasury and construction expense graphs, and compare current construction expenses. Run across another chart sample and check the guest graph refreshes while open.
5. Watch a moving army and fleet at the same zoom on both maps, replace an order, then pause. Check icons, names, flags, models and arrows stay aligned, motion is smoother, clicking the drawn icon selects the right unit, and paused positions settle exactly. Check native single-player/host rendering remains normal.
6. If a discrepancy remains, retain both new connection logs and their `.state.csv` / `.economy.csv` companions, plus Unity `Player.log`. Connected v0.27 verification remains pending.

## Earlier build: v0.26

# GTIP MP Test v0.26 — repeated ship transfers and native movement feedback

Both copies require **v0.26 / protocol 20**. The latest v0.25 logs show guest orders reaching the host quickly, while some complete campaign revisions take roughly 1–2 seconds to prepare, transfer and apply. This candidate addresses repeated ship dragging, fleet Stop/Return to Port, native Movement Options and missing native movement-refusal text.

After an authoritative vessel attachment change, the visible native Fleets page now refreshes its fleet and ship-pool lists after any active drag finishes. Native initialization restores the ship drag targets that an OOB refresh had cleared. Fleet Stop now maps to `680_STOP`; Return to Port maps to `681_PORT` and executes the native closest-harbor search with that fleet's sea/river capabilities and the native no-order-delay return action.

Guest movement calls the host's native waypoint method with movement options enabled. The native popup request is delivered to the guest, including its location, minimized/full choice, active buttons, disabled buttons and lifetime. The host retains separate native route-edit context for guest controls; guest edits preserve the host's selected units and another unit's route context. Popup controls forward accept/cancel, timed movement, forced march, retreat mode and available rail/sea/river changes to the host. A popup carries its originating order identity, so stale controls cannot operate on a newer guest route; a subsequent host waypoint or stop invalidates the affected guest context. These are native UI panels, with host-side mutations.

Native rolling-text messages created during a guest request now travel immediately, including low-readiness movement refusals, garrison/prewar restrictions and disabled-fleet feedback. The text/type/position come from the native call, and the same notice is deduplicated when it later appears in a campaign snapshot.

Host waypoint, stop and route-edit actions also send small priority route records. The guest redraws native path arrows on receipt rather than waiting for the whole campaign revision. Per-unit command generations prevent an older in-flight snapshot from restoring a superseded route; ordinary updates within the same generation continue to apply. These records update path presentation, without guest pathfinding or simulation. Positions and campaign statistics still arrive through validated campaign revisions.

Full-state fingerprinting now caches unchanged encoded records while reproducing the exact original byte stream and SHA-256. Checksums, numeric validation and transactional commit are retained. Signed zero, NaN, arrays, insertions/deletions and changed commits have regression coverage. A synthetic 18,000-row .NET test measured about 78 ms for full encoding versus 35 ms with a warm cache; this is not a measurement of live Unity or network latency.

Verification: 271 multiplayer model/protocol/TCP checks; 21 native-bridge/pause/promotion checks; 9 replica-boundary checks; 42 editor presentation checks; existing hierarchy/editor/AI suites. Native schema/snapshot validation covers 31 compiled schemas / 620 fields. Staging verifies 102 hooks/changes and 95 preserved methods. Live Unity input, native popup interactions and connected latency still need manual verification.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `33AAA8DE07CE42008351CB97387E676840D0A593B8280A99771D68B88FB3B092`; runtime `6389A169FA29F6458B89326503477B9CF7F50AB601CEFB51014A769DA1902F68`. The installed runtime passes the compiled-schema snapshot checks. Installer backups were preserved. Connected v0.26 testing is pending.

## Connected test for v0.26

1. Restart both games and join a fresh session from **GT MP Test**. On the guest Fleets page, transfer several different ships, then move one back, including a fleet-to-pool/pool-to-fleet transfer where allowed. Wait for each authoritative update and continue dragging without leaving the page. Check both players' ship assignments and fleet totals after every transfer.
2. Select a moving guest fleet and use **Stop**. Its route should clear on both maps. Issue another move, then use **Return to Port**; compare the destination and suitability of the selected harbor on both copies. Repeat with sea-only/river-capable fleets where available.
3. Give guest army and fleet movement orders. Check the native Movement Options marker appears at the destination, expands normally, and offers appropriate enabled/disabled controls. Test accept/cancel and a permitted transport or timed-movement option. Keep a different unit selected on the host and verify guest interaction does not change that selection or overwrite that unit's route controls.
4. Replace a destination several times while paused and then stop. Arrows should update promptly and remain on the newest order as campaign snapshots arrive. Run briefly, pause again and compare unit positions/status. Retain both multiplayer logs if arrows regress, units diverge or noticeable movement delay remains; priority-route and popup send/receive entries are now logged.
5. Move a low-readiness army into hostile territory and compare the guest's native refusal text with the host's behavior. Also try an allowed friendly-territory move so a refused order is distinguishable from a missing response. The warning should fade while paused and should not appear twice from one native notice.

## Earlier build: v0.25

# GTIP MP Test v0.25 — guest Military panels, promotions and local controls

Both copies require **v0.25 / protocol 19**. The latest v0.24 session remained connected for roughly 22 minutes. Unity nevertheless recorded repeated `TargetParameterCountException` errors in the guest's formation-name bridge: native `GetUsedUnit` takes two optional parameters, and the bridge supplied neither. This interrupted `CampaignMainPanel.UpdateContent` before native filters and lists were initialized. A second repeated exception came from querying an already destroyed UI object during input-focus handling, interrupting later guest view/control updates.

The bridge now supplies declared optional defaults, rejects missing required arguments, and explicitly supplies both unit-lookup arguments. Input-focus checks use Unity's destroyed-object checks. Native Military setup can complete; weapon views are driven only after initialization. Fleet Orders now uses the native `651_FLEET` button name. The guest draws the native path mesh and order-delay symbols, preserving the authoritative delay scalar around the native display callback and invalidating the cached mesh when a host route changes.

Promotion requests carry the officer's expected rank. The host enforces the same native maximum-rank, active-duty and career restrictions as the native promotion button before applying promotion effects once. Repeat requests with an outdated rank are rejected. An authoritative rank change refreshes the visible officer card/list. Previously saved promotions are not reversed.

The guest dispatch toggle and close controls operate locally. A shared pause tracker across native Panel/GameObject modals prevents closing a log from resuming a campaign already paused before it opened; a campaign running on entry retains native resume behavior. Guest viewing still cannot change the host clock. Construction rolling text now also travels immediately as a reliable host event, using the native text/cost and deduplicating against ordinary snapshot notices.

Verification: 248 multiplayer protocol/model/TCP checks; 21 production native-bridge, modal-pause and promotion-rule checks; 9 replica simulation-boundary checks; 42 editor presentation checks; existing hierarchy/editor/AI suites. All 31 compiled schemas / 619 fields pass native metadata and synthetic snapshot checks. The native renderer's direct writes to registered campaign fields were audited, and staging verifies 99 hooks/changes and 92 preserved methods. These checks do not execute live Unity input/rendering.

Installed in both desktop test copies, with matching SHA-256 hashes: patched game `803CB4293AC7B181E1E843E2032F0239525F203F1DDBA05513A508EBEE04B84D`; runtime `304D1415DC6E7B280959521215CCC8582826B32E6B97EF4DA79E33D18C4DE954`. The installed runtime also passes the compiled-schema snapshot checks. Installer backups were preserved. Connected v0.25 testing is pending.

## Connected test for v0.25

1. Restart both copies, host **GT MP Test**, and join from the guest main menu. While paused, open guest Military and switch Armies → Fleets → Officers → Weapons several times. Check that native lists, filters and counters populate, with distinct weapon rows matching the host's filters. Select a weapon, place an eligible order, and compare stock, cost and order quantity on both copies.
2. Promote an eligible guest officer once and keep the page open. The card and rank list should refresh after the host update. Rapid clicks must not queue several promotions from one displayed rank. Check the promotion limit against the host's native disabled button. Existing officers already promoted too far are not automatically downgraded; use an unaffected officer or an earlier save for the limit check.
3. On each player independently, open/close and hide/show dispatches while already paused. Time must remain paused and the other player's log visibility must not change. Repeat starting while running: the host's log should retain native pause/resume behavior, while guest viewing does not control campaign time. Also open a second modal before closing the log and check it cannot release the other modal's pause.
4. Place a permitted building from each player in the 3D map view. Both should receive the native construction/cost notice once; it should fade normally, including while paused. Confirm one building and one payment on each copy.
5. Select a guest fleet and open Stance, Fleet Orders and Movement. Issue an eligible order, compare the native order-delay marker/path arrow with the host, replace the destination while paused, then deselect/reselect. Check the arrow redraws and disappears when stopped.
6. Keep both new multiplayer logs and Unity `Player.log` if a panel is still blank, white bars remain, an order is rejected unexpectedly, or a connection fails. This candidate needs a connected manual test; complete native feature parity, versus and tactical battles remain unfinished.

## Earlier build: v0.24

# GTIP MP Test v0.24 — native readiness estimates and snapshot validation

Both copies require **v0.24 / protocol 18**. The latest v0.23 logs show a different failure: the host cannot encode its first state because Regiment `3:16` (19th wisconsin) has a non-finite `fullreadinessreached` value. The guest has matched the save but receives no first state update. Pausing cannot avoid this because the value already exists in the loaded native state.

Native `UpdateCampaignUnitStats` calculates the estimate as `1 / ((readiness - previousReadiness) / elapsed) * (1 - readiness)`. With no readiness change, this can produce infinity or NaN, and `UpdateGroupStrength` propagates those values. `CampaignArmyPanel` already treats very large positive estimates as slowly recovering readiness. The replication schema now preserves NaN and signed infinity for this specific estimate. All other unit floats retain finite-value validation; the existing nation-history NaN exception still rejects infinity. Native numbers are transmitted rather than replaced with invented finite ETAs. Allowed NaN payloads are canonicalized for stable checksums.

Before transferring a checkpoint, the host now captures and validates the full registered snapshot. Unregistered non-finite fields are logged together (up to 64), and an invalid host state gives the guest a validation rejection before it spends time loading. Serialization failures during later updates also report all invalid numeric fields found in that snapshot. The v0.23 tooltip simulation guard and scalar divergence checks remain enabled.

New regression coverage reproduces both zero-progress readiness calculations and exercises encoding, decoding, baseline reconstruction, checksums and commit with finite/infinite/NaN estimates. A separate test uses all 31 actual compiled game schemas with synthetic values, including the special readiness values. These tests do not load or execute the user's live campaign.

Verification: 248 multiplayer checks, 9 replica-boundary checks, 42 editor presentation checks and the existing hierarchy/editor/AI suites pass. Native snapshot checks cover all 31 compiled schemas and 619 fields. Staging verifies 96 hooks/changes and 89 preserved methods. Connected gameplay remains unverified.

The v0.24 candidate is installed in both desktop test copies with matching patched-game/runtime hashes. Installed runtime files also pass the compiled-schema snapshot check.

## Connected test for v0.24

1. Restart both copies and create a fresh session from **GT MP Test**. Join from the guest's main menu and keep the host paused for a minute. The host log should report `Host state preflight passed`, followed by revision 1 preparation and an application receipt from the guest.
2. Hover enemy sightings and friendly icons, inspect readiness, and open Military. Then unpause for several minutes and pause again. The v0.23 morale issue and the v0.24 readiness serialization issue should both remain absent.
3. If a new validation error occurs, retain both new multiplayer logs. Preflight now reports all rejected numeric fields together. Broader native feature parity, versus and tactical battles remain unfinished.

## Earlier build: v0.23

# GTIP MP Test v0.23 — guest tooltip simulation boundary

Both installs require **v0.23 / protocol 17**. This fixes the v0.22 disconnect during the first campaign state update. The two October 17 guest logs show `Native state divergence at revision 1`, on the `morale` field of 76th North Carolina and 74th Missouri respectively. The checkpoint transfer/load succeeds; the host's subsequent end-of-stream message is a consequence of the guest stopping.

Native `SpottedEnemy.UpdateTooltips` calls `Regiment.GetUnitTooltipInfo`, which calls `UpdateGroupStrength`. Despite its name, that method advances subordinate morale, campaign stats/readiness and condition. The v0.22 sighting adapter therefore let native simulation overwrite received host values before readback. The bridge now suppresses `UpdateGroupStrength` for replicas, while preserving host/offline execution and the existing isolated recruiting-preview transaction. Its public group-stat outputs and campaign morale breakdowns are explicitly replicated. The divergence check remains enabled, with formation name and expected/actual values added to future error reports.

Regression checks exercise the production bridge with mutating native-operation doubles: guest tooltips preserve authoritative values, previews remain isolated, and host/offline calls and arguments remain intact. Native metadata checks confirm the real tooltip-to-simulation call chain and require every public direct field write in `UpdateGroupStrength` to have a registered host field. These tests do not run Unity gameplay.

Verification: 9 production-bridge regression checks, 230 multiplayer model/protocol/TCP checks, 42 editor presentation checks and the existing hierarchy/editor/AI suites pass. Native checks bind 31 schemas / 619 fields and cover all 57 public direct group-stat field writes. Staging verifies 96 hooks/changes and 89 preserved methods. Connected join/hover testing remains pending.

The v0.23 candidate is installed in both desktop test copies with matching patched-game and runtime hashes. A fresh connection is required; the failed v0.22 guest session should not be continued.

## Connected test for v0.23

1. Restart both copies. Load **GT MP Test** on the host and join from the guest's main menu. Leave the host paused for at least a minute; the guest should finish the first update and remain connected.
2. Pan/zoom around Washington and Alexandria and hover friendly/enemy icons and remembered sightings. Open Military and select several formations. Check no morale/readiness/condition divergence occurs while paused.
3. Resume at normal speed for several minutes, pause again, and compare formation values. Open a guest recruitment preview and cancel it, then repeat the hover test.
4. If either copy disconnects, retain both new `GTIP-Logs/mp-*.log` files. The previous v0.22 checklist below still covers the broader unverified features. Tactical battles and versus remain unfinished.

## Earlier build: v0.22

# GTIP MP Test v0.22 — world objects, native controls and map-state audit

Both installs require **v0.22 / protocol 16** and must be restarted. This is an experimental co-op candidate. Full native parity, versus and tactical battle synchronization remain unfinished. The checks below are required live tests, not recorded passes.

The small status strip is fixed near the top-right, left of the compass. It stays in that location when selecting units or opening panels. Native popups may overlap it. The guest map unit card is suppressed while the Military management window is open. Fleets, Officers, Weapons and the bottom construction/order submenus now use their native viewing controls; their registered mutations are sent as host-validated requests.

Formation replication now includes native `inbattle`, retreat/participation flags, the cached `unitstatus`, order-delay paths and additional group/transport values. Co-op visibility comes from the host's rendered fog state rather than guest fog calculations. Remembered enemy sightings have a separate adapter for their observed positions, timestamps and native icons; the guest no longer creates its own sightings. Visible child units are not hidden by deactivating a fogged parent object. Absolute poses are restored parent-first to prevent parent movement displacing unchanged children.

Buildings have stable session IDs, native list ordering, lifecycle/level/condition/stock/construction fields and garrison links. Town/IIP ownership and selected economy fields, railroad construction, ship lifecycle/assignments, fleet fields and weapon-order records have explicit adapters. Guest manual/automatic construction, depot stock/upgrade, railroad construction, fleet/ship creation and transfer/deletion, weapon assignments/orders, officer promotion, army stance/orders, forced march, stopping and unit/ship names use host validation and native execution. These adapters do not establish complete nested economy, logistics or battle-state coverage. Native placement/pathfinding and costs require live verification.

Dispatch delivery is shared, while log visibility and read/remove choices are local. The host retains delivery records for the session even after locally removing a message; guest removals are remembered so incoming state does not restore them. Non-formation dispatch object references remain incomplete.

The native engagement window receives the host's text, totals, reinforcement entries and enabled choices. In co-op, the first valid choice accepted on the host resolves that engagement; delayed requests cannot choose for a later engagement. Reinforcement selection is host-validated. Auto-resolve, defend, withdraw and retreat remain native host operations whose registered results replicate. **Play Battle still transitions the host into a tactical battle and ends this campaign multiplayer session. It does not start a synchronized tactical battle.** Versus decision rules are not implemented.

Unit position readback runs after every applied revision. At revision 1 and every 30 revisions, each log's companion `.state.csv` records the revision, unit identity/parent, visibility, active flag, battle/retreat/routed/stance/status and map coordinates. Compare matching revisions, not simultaneous file timestamps. This provides evidence for missing/misplaced icons; scalar equality alone cannot prove rendering parity. Existing capture/worker/apply timing diagnostics remain available. No performance improvement is claimed without a connected measurement.

Verification: 230 multiplayer model/protocol/TCP checks; 31 native schemas with 591 fields and private field/path-cache ABI checks against the original game assembly; 42 editor presentation checks; hierarchy/editor/AI regression suites; 95 structurally verified hooks/changes with 88 preserved methods. These do not exercise Unity gameplay or prove the native UI fixes.

The candidate is installed in both desktop test copies with matching patched-game/runtime hashes and installer backups. Connected gameplay verification is pending.

## Connected test for v0.22

Use a separate copy of **GT MP Test** (originally Regi Test 3). Host the loaded campaign, join the guest from the main menu, and keep it paused for the first comparisons.

1. Select/deselect units and open Military on each copy. The status strip should stay fixed; the guest map unit card should not overlap Military. Open Fleets, Officers and Weapons, then return to Armies.
2. Compare the same camera area/zoom around Washington, Alexandria and the resting formations in your report. Check Keitt's/Miller's divisions, visible versus remembered sightings, names, coordinates, stance and battle status. Repeat after running briefly and pausing. Retain both `.log` and `.log.state.csv` files if they differ.
3. On the guest, change stance, scouting/raiding/guarding orders where eligible, forced march and stop. Check the host accepts the request and both show native order delay/readiness. Test Manage Army and an OOB transfer without an overlapping map card.
4. Open Federal Buildings, Company Applications and Railroads. Place an eligible building from each player, check one payment and one object on each map, and test upgrade/depot storage. Start a permitted railroad and verify its progress/completion. Record any rejected action and the native tooltip explaining eligibility.
5. Test one fleet/ship creation, ship transfer to/from a pool, renaming/deletion, officer promotion and weapon assignment/order. Check costs, counts and results on both copies; creating/deleting a native object should not require a campaign reload.
6. Open/read/mark/remove/hide dispatches separately. The other player's delivered log should stay intact and retain its own read state; new messages should reach both.
7. At an engagement, compare text, totals, choices and reinforcement lists. Select a valid non-tactical choice on the guest, then repeat with the host choosing first. Each engagement should resolve once, and resulting unit status should agree. Do not use Play Battle to test campaign continuity.
8. Run for several campaign days with pause/speed changes, construction, recruitment, policies and projects. Note host/guest stutters and any disconnect; keep both session logs. Rejoin from a fresh host checkpoint after a failure rather than continuing a disconnected replica.

## Earlier build: v0.21

# GTIP MP Test v0.21 — host-owned campaign presentation and Goods & Trade

Both installs require v0.21 / protocol 15. This remains an incomplete co-op prototype. Versus, tactical battles, full building/economic-object replication and complete native command coverage remain unfinished.

Newspaper dismissal retains the stored native speed rather than selecting a speed. Both players must dismiss the issue; native dontcontinue, other blocking panels and the remain-paused preference still prevent resuming. A manual pause while waiting cancels automatic resume. Logs now include the captured and final speeds. Native Newspaper.Close normally calls SetGameSpeed() with its default -1 argument, which resumes GameVars.gamespeed; it has no hard-coded 50x choice.

All eleven top-bar alert categories are captured after one complete native host refresh, including count, tooltip, blink and navigation positions. The replica preserves the native lazy initialization of nested alert-button references and reasserts host values in LateUpdate rather than letting local refreshes replace them. Alert count changes are logged on both sides for comparison. Guest Autocalc.Update is now suppressed: campaign auto-resolve was another independent local simulation path, capable of modifying units and generating local combat notices.

Host rolling map notices now replicate their text, position, type and age. The guest runs native RollingText cleanup using real elapsed time, including while paused. Disabled ButtonTooltip components are excluded from manual hover activation, preserving native non-interactive map messages. Notice expiry is a presentation deletion and no longer triggers an expensive formation lifecycle rebuild.

Guest map picking no longer accepts company/range blocks. Native campaign icon cards use their normal UI dispatcher; fallback physics picking is limited to visible UnitNameText/paper-map UnitSymbol renderer bounds. Open native management panels and UI under the pointer block camera wheel input without blocking their scroll views. The building/depot ring click is now an allowed native viewing operation and opens/closes its existing info panel. Building upgrades, storage changes and new building lifecycle replication are not added by this fix.

Goods & Trade now receives explicit per-resource/per-alliance prices, current/previous demand and production, market/depot stocks, volume, tax, tariffs, value and stock ratios. Goods chart histories replicate as bounded numeric arrays. Incoming updates change existing native rows and the selected information pane without rebuilding the list or resetting its scroll position. This does not yet replicate every nested trade-flow or building dependency.

Verification: runtime/patcher compile; 203 multiplayer model/protocol/TCP checks; 42 editor presentation checks; existing hierarchy/editor/AI regression suites; 64 structurally verified hooks/changes. These do not prove the live Unity input or rendering fixes. Both copies must be restarted before testing.

## Connected test for v0.21

1. Start a fresh host session and join the guest from the main menu. Confirm both say v0.21.
2. Trigger a newspaper at 1x, 5x and 20x. Dismiss in both orders. Check the speed resumes as native settings allow; pause manually while waiting and verify it remains paused.
3. Pause after alerts appear. Compare every populated alert's count and tooltip on both clients after the pending update arrives; leave it paused and verify the guest does not independently change counts.
4. Observe combat/routed map notices on the guest: they should expire normally and show no blank hover tooltip.
5. Click empty space within a command radius, then the unit icon. Only the icon should select the formation. Open a depot/building ring and verify its native detail panel opens.
6. Open Military, Finances, Policies, Projects and Goods & Trade on the guest. Wheel-scroll each and verify the map stays still. Close the panel and verify map zoom works again.
7. Compare Goods & Trade prices, demand, production, stocks, trade volume, tax and tariffs with the host, including an open table while new host data arrives.

## Earlier build: v0.20

# GTIP MP Test v0.20 — native recruitment, finance controls and campaign presentation

Both installs require v0.20 / protocol 14. This remains an incomplete co-op prototype. Versus, tactical battles, full economy objects and complete UI command coverage remain unfinished.

The host now samples and sends a sequenced clock in LateUpdate, after native simulation updates, and forces its native time label to match that sample. This addresses the native timer panel's throttled display remaining behind the actual paused time. Guests reject stale/duplicate clock messages, and position snapshots no longer overwrite a newer clock. The host/guest stream still has network latency; the candidate needs an in-game paused-clock check.

The selection/Stop/Replace commander overlay has been removed. A small status strip seeks an unoccupied screen edge and hides if native panels cover the available locations. Commander replacement now uses the native officer list and a validated host request. Map selection accepts the same Coy blocks, UnitNameText and paper-map UnitSymbol categories as the native selector, excluding command-range/root colliders.

Guest Add new unit now opens the native recruitment screen. Its normal state, branch, size, recruit-type and uniform controls edit a temporary native preview. Registered alliance/nation/officer/unit values, roster lists, officer references and dates are preserved around native preview creation. The preview is detached from the campaign roster, its world renderers/colliders stay disabled, and it is destroyed on cancel/disconnect. Confirmation sends the draft to the host, which revalidates and performs native paid recruitment. This preview isolation and native UI flow have not yet passed live validation. Independent command creation and ship recruitment are not added here.

Finances tax/subsidy sliders now distinguish user edits from incoming authoritative data. Guest changes and the finance automation checkbox go to the host; incoming values update an open finance view without being written back as stale local edits. Political subsidies retain native capacity-step rounding. Native calculation/formatting still has unreplicated nested dependencies, so this is financial-control synchronization, not complete economic equality.

Campaign alerts now carry the host's per-button counts, tooltip text, blink state and navigation positions. The guest renders that data instead of independently pruning/creating alerts from its incomplete local simulation. Clicking an alert cycles its host-supplied positions. Tactical battle panels/resolution remain unsupported.

Policy correction: the native `Policies.AddResearch` / `CanBeActivated` code permits multiple available policies to be researched at once. `UpdatePolicyResearch` divides research speed by the number of unfinished policies. This build retains those rules. Guest clicks are matched against the current UI raycast to prevent stale hover flags submitting more than one policy for one click. Chapter changes now replicate explicitly along with policy progress/membership; prerequisites and capacity remain host-validated.

Verification: 190 multiplayer model/protocol/TCP checks, 42 editor presentation checks, existing regression suites, and 63 verified hooks. The automated checks do not exercise Unity's full native recruitment/finance UI. The user will perform the next connected test after installing both copies.

The v0.20 candidate is installed in both test copies. Installed runtime and patched game assemblies match between copies. No connected UI, paused-clock, finance or recruitment fix is claimed live-verified yet. Retest with a fresh connection: repeated pauses at different speeds, native guest recruitment/cancellation, native commander replacement, finances edited from both clients while both panels remain open, single policy clicks, and alert counts after updates settle.

## Earlier build: v0.19

# GTIP MP Test v0.19 — campaign actions and notifications

Both installs require v0.19 / protocol 13. This remains an incomplete co-op prototype. Versus and tactical battles are not supported.

The v0.18 guest log identified the policy disconnect in `Policies.UpdateActivations`: that native routine searches for the active Policies title even when the panel is closed. State application now refreshes policy artwork only when that panel is visible. Policy-only updates also no longer force a full hierarchy application.

Projects now have an explicit list adapter, including repeated IDs for multiple levels and the monthly report list. Guest project appointments use native host funding/availability checks. The guest installs the resulting lists, derived unlock caches and authoritative scalar balances; it does not replay one-time project payments or effects.

Newspaper content, artwork ID and event date replicate as a numbered issue. Both players must dismiss that issue before the host can resume. Duplicate, stale or future acknowledgements cannot release the current barrier. The native host newspaper queue is held while waiting. After both dismiss, the host resumes only if the native notification would have resumed. Disconnect leaves the host paused. A newspaper already open before the session starts is not yet imported into this barrier.

Guest native OOB **Add new unit** opens an MP recruitment form for infantry, cavalry, artillery or horse artillery, three native sizes, recruiting state, and volunteers/draftees. The host rechecks the parent, state, recruit pool, debt rating and horse-artillery availability, then uses native creation, recruitment costs and transfer rules. This initial form uses native default name, commander, weapon and uniform selection. It does not provide the complete native recruiting dialog or independent command creation.

Native guest dispatch-log rows and Options actions are permitted. Options temporarily enables Unity input for its local controls. Load/Save and Exit open a session file dialog: the guest can request a fresh checkpoint on the host or disconnect to select another campaign. Local replica saving/loading is not enabled. Requested checkpoints use a fresh GUID folder and restore the host's previous save target.

Both current test-install hotkey files had reverted to 100 `None` entries. The existing desktop backup contains WASD, Esc, P, keypad speed controls and number-row speed selection. The new loader consumes every file row independently of which native Options panel supplies a corresponding UI object; native panels share the global key table. Session logs now record the relevant resolved mappings. The repair preserves hotkeys as user configuration rather than packaging them in releases.

Position/visibility-only formation updates now use the incremental path instead of the full lifecycle rebuild. Structural parent/type changes and initial sync still use the full adapter. Host capture and baseline work still need further profiling.

Validation: 169 multiplayer protocol/model/loopback checks, 42 editor presentation checks, existing regression suites, and 51 verified hooks. Connected gameplay verification is pending; compilation and model tests do not establish native feature parity or a measured performance improvement.

The candidate is installed in both test copies with byte-identical assemblies. Both hotkey files have 19 configured bindings after restoration; the pre-repair files are preserved as `Config/hotkeys.before-mp019.dat`. The user is performing the connected gameplay test.

Test a fresh connection, then: (1) Esc/WASD/P and number-row 1–5 on each client; (2) guest Options/dispatch rows; (3) host policy selection while the guest policy panel is closed, then guest policy selection; (4) funded project purchases from each side, checking level and one payment; (5) guest Add new unit, checking native host recruitment and guest creation; (6) a newspaper dismissed in each order, verifying time stays paused until both acknowledge; (7) guest checkpoint request, then paused/running stutter observations. Retain both per-install logs if any step fails.

## Earlier build: v0.18

# GTIP MP Test v0.18 — live campaign records and lifecycle replication

Both installs require v0.18 / protocol 12. This remains an incomplete co-op prototype; versus and tactical battles are not supported.

The host sends campaign records in bounded, checksummed batches while the campaign stays loaded. Formation and officer creation/deletion no longer intentionally trigger a pause, reload or disconnect. The guest constructs native formation objects, updates parent and Army Group membership, and reconnects officer references. A full native checkpoint is still used at initial join/reconnect. Disconnecting after live data has been applied returns the guest to the main menu without saving.

Current adapters include alliance/state scalar and numeric-array data (including treasury and recruitment pools), officer attributes and dates, unit strength/condition/supply fields, formation metadata and Army Group membership. The native support-history field can contain NaN; only that explicitly configured field permits it. All other numeric fields reject non-finite values.

This is not complete economic or recruitment replication: nested industry, buildings, production queues, ships, perks, AI references, visual caches and other native data still need dedicated adapters and verification. Revisioned batches now send changed records and explicit deletions against the last acknowledged baseline. Each batch reconstructs a full state before native application; the guest reads registered scalar fields back before acknowledging. This verifies those adapters, not the whole game. Hashing and native application still process full registered state and need profiling. The clock is sent separately at up to 30 Hz. Network and frame time limit actual delivery rates.

Guest requests currently cover movement, stopping, commander replacement, policy research selection, and pause/speed hotkeys. Policy selection is validated and executed on the host; policy progress and membership replicate back. Native OOB drag-and-drop between land headquarters now routes a regroup request to the host. Policy cancellation, recruitment dialogs, Army Group drag targets and sibling-order editing remain incomplete. Host-side recruitment/creation adapters do not imply that guests can issue those commands yet. Saving and battle resolution remain host responsibilities; entering a tactical battle ends this prototype session.

v0.17 moved host delta preparation, compression, checksum calculation and baseline commit to a single background worker pipeline. Unity data capture remains on the game thread; workers receive cloned records only. Canonical checksums no longer compress full state. Diagnostics report `captureMs`, `workerMs`, receipt latency and guest apply time at revision 1 and every 30 revisions. These changes target host frame stalls; no frame-rate improvement has yet been measured in a live campaign.

The guest now invokes native hover enter/leave artwork updates, shares native UI formation selection with movement requests, and excludes only graphic UI raycasts from map input (physics hits are map objects). Pause, speed selection/increase/decrease and stop hotkeys send host requests. Escape now toggles the native guest menu; F9 remains the multiplayer session menu. Native hotkey coverage is not complete. Cohesion is captured alongside group cohesion, and authoritative formation fields are restored after structural/UI initialization callbacks before the readback audit.

The guest presentation adapter adds native top-panel viewing, OOB refreshes, unit visual updates and delivered dispatch replication. Native mutation callbacks remain disabled; only explicitly supported viewing actions are dispatched. Non-formation dispatch object references are not yet synchronized. These presentation changes require live verification.

Validation: 147 multiplayer checks, including canonical checksum and captured-array isolation checks, plus existing regression suites; 45 structural hooks. Automated checks do not prove correct native initialization or complete campaign equality. See [architecture and acceptance gates](multiplayer-architecture.md).

Next live check: join from the guest main menu, select a native map formation and request movement, select a policy, exercise pause/speed keys, and create a formation on the host. Check guest policy progress, both clocks, commander/parent links and hover reset. Keep both logs to compare the replication timing lines against the reported host slowdown. Do not treat this candidate as complete multiplayer.

## v0.18 candidate and current verification

The v0.17 logs measured roughly 73–83 ms of host capture work and 480–535 ms of guest application work, including while paused. v0.18 compiles field accessors once, decodes/checksums/commits guest batches on a background worker, and applies only changed native scalar/date records for ordinary updates. Initial sync, policy updates and structural/formation-metadata updates still use the full lifecycle adapter. The guest acknowledges only after native application, changed-field readback and baseline commit. These optimizations do not eliminate all allocation or full-state work.

The guest waits until native campaign initialization has passed frame 60, refreshes the avatar visibility and runs modular UI window close operations. Paper-map tooltip refreshes are restored. Map selection avoids the native collider helper's unchecked parent/component assumptions. Menu, map information, field book, overview, unit/garrison/fleet lists and map-filter buttons are added to the permitted local viewing actions. Timer buttons send host requests. In multiplayer, shortcut suppression follows focused text fields instead of stale native panel flags.

Native land OOB drag/drop is enabled with `BattleUnits.MoveUnit` intercepted on guests. The request carries stable source/destination IDs; the host checks ownership, existence, hierarchy/cycles and native transfer rules before applying it. It does not enable guest recruitment, ship transfer, combat-unit merging or all Army Group operations.

Full exceptions are now written to each install's separate multiplayer log. The reported v0.17 null reference could not be conclusively traced: that session log had only the message, and the shared Unity log contained a truncated stack. It is not confirmed fixed.

Build and regression verification passed (147 multiplayer checks, 42 editor presentation checks, 45 structural hooks). The candidate is installed in both test copies. Main-menu launch was verified; automated save selection did not work, so connected gameplay and performance validation await loading the test checkpoint. No live stutter, button, regroup or crash fix is claimed verified yet.

## Earlier builds
# GTIP MP Test v0.13 — host-supplied campaign loading

This is an intermediate architecture build, **not complete campaign multiplayer**.

The guest now joins from the main menu. The host pauses, writes a new native campaign checkpoint,
compresses the explicit campaign-data files, and sends bounded chunks. The guest verifies the SHA-256
checksum, extracts to a new `SaveGTIPGuest...` folder, and invokes the native campaign loader.
Existing saves are never overwritten. Both installations must use v0.13 / protocol 7.

Host AI and time changes are held during checkpoint transfer/loading. The guest acknowledges a matching
formation manifest before streaming begins. Reconnect therefore starts from the current host campaign;
the guest no longer has to manually find the same saved date. Return to the main menu before rejoining.

Guest commands: left-click a friendly formation on the map, right-click terrain to request movement,
or use Stop formation. Requests carry session, sequence and formation IDs; the host checks ownership,
availability and destination bounds before invoking native orders. Native order delay still applies.
Other guest native UI mutations remain disabled. Escape opens the session panel without disconnecting.

## Remaining replication work

Initial campaign persistence is transferred, but ongoing replication still covers only the clock,
formation parents and positions. Economy, officer/strength changes, recruitment, deletion, transfers,
construction, policy commands and tactical battles need live state adapters and host command handlers.
A changed formation roster still ends the session rather than creating an inconsistent guest object.
Versus remains disabled until faction initialization and visibility are validated.

Test flow: host loads a campaign, opens F9 and hosts. Guest opens F9 at the main menu, enters the host
address and key, and joins. Wait for both to finish synchronization, close the host panel and resume.
Compare the native clock and selected formation movement, then test a guest movement request.
The host retains saving authority. Do not use guest replica saves as authoritative campaigns.

Automated verification: 98 protocol/archive/loopback checks plus existing regression suites and 37
structural hooks.

Live verification on October 13, 2026 used two installs on one machine and Regi Test 3.
The guest joined from the main menu, verified a roughly 5 MB host archive, loaded the native map,
and matched all 1,940 formation identities. The session remained connected across a campaign day.
The native guest clock followed the host (including pause), a guest movement request created a
native route on the host, and a guest Stop request removed that route and changed the host formation
to Awaiting Orders. This verifies command delivery and native order execution, not completed movement,
complete state equality, different-machine networking, or an extended campaign.

Known limitation observed during this test: native strength/sidebar data can diverge. Do not infer
complete synchronization from matching time and positions. The next architectural work is live data
replication and a broader audit of guest simulation entry points.

## Earlier builds

# GTIP MP Test v0.12 — session identities and authoritative clock

Formation identities are captured at join and stay fixed for that session. Renaming a formation or
replacing its commander no longer invalidates the live hierarchy. New/deleted formations still require
a new shared checkpoint; those are not replicated yet. Rejection messages distinguish invalid IDs,
duplicates, changed identity tokens and unknown parents.

The guest receives native year/month/day/hour, elapsed campaign time, speed and pause state from the
host up to 30 times per second and immediately on pause/speed changes (subject to network/update latency).
The guest's native clock/date and speed indicators follow these values, including unpausing.
The host remains authoritative: guest local simulation/AI and independent clock progression stay
suppressed. Original guest clock/hierarchy are restored on disconnect. Guest saves are blocked while
connected because other campaign state is not fully replicated. This is not yet complete multiplayer:
economy, recruitment, full combat state and guest native orders remain incomplete.

The v0.11 blanket paused-AI guard has been removed: native AI initialization must finish even during
paused loading. Guest AI stays suppressed while connected. Host hierarchy updates handle divergence.

Keyboard diagnosis: both test installations had all hotkeys set to None. Their files were backed up
and restored from the user's existing desktop backup; hotkeys are not distributed in the mod package.
The backup binds Esc to the native menu and WASD to camera movement. Arrow keys keep native unit navigation.

Protocol 6 requires both installs to update. Validation includes production codec/transport regression
checks for pause/resume, date rollover and immutable identities. Live clock/rendering still needs testing.

## Previous build notes

# GTIP MP Test v0.11 — parent synchronization and input recovery

Formation identity no longer includes parent commands. The actual v0.10 failure logs had
1,940 uniquely matching formations and the same date, with only the 65th Georgia's parent differing.
Native campaign AI processes transfer jobs without a pause guard; this is a plausible source of the
live divergence, not a captured trace of that particular move. MP builds now stop campaign AI updates
while paused after regiments initialize.

The host sends a complete, validated parent map during welcome and subsequent updates. The guest
applies it for rendering and restores its original parents, sibling order, positions and active states
on disconnect. Unit names/commander identities, faction and time must still match. Protocol 5 requires
both copies to update. This remains an observation prototype, not a full campaign-state replica.

Esc closes F9; failed sessions close the blocking panel and restore controls. On a connected guest,
Esc from the map disconnects/restores the checkpoint, then opens the native menu. Camera controls
remain local. Native campaign editing stays blocked while connected.

Validation: 76 multiplayer checks, full regression build and patch verification, plus unique matching
of all 1,940 formations from the actual host/guest diagnostic files. Live two-instance testing remains
necessary for rendering and keyboard behavior.

## Previous build notes

# GTIP MP Test v0.10 — connection matching fix

The multiplayer prototype now has separate versioning from base GTIP (0.3.8).
Both installations must use MP v0.10 / wire protocol 4; older MP builds cannot connect.

Checkpoint matching uses formation faction, tier, commander index, name, and formation ancestry.
It no longer uses Unity sibling order or non-formation scene container names. Ambiguous duplicate
identities are rejected rather than assigned to arbitrary units. Campaign time is checked separately
and still must match exactly. This is a roster compatibility check, not a full save-content hash.

Host rejections now reach the guest with the specific faction, campaign-time, or formation difference.
Each install writes checkpoint date/count/hash and a `.log.formations.txt` companion file into
`GTIP-Logs` to diagnose remaining differences. Session keys are not logged.

Validation: production codec/loopback tests plus identity ordering, ambiguity and mismatch cases.
A live two-client connection still needs verification. This remains native-map co-op observation;
guest orders, versus gameplay, complete campaign state and tactical battles are not synchronized.

## Previous milestone notes

# Native campaign map milestone â€” GTIP 0.4.2

Experimental `mp-test` work. This is co-op native-map observation, not complete multiplayer.

Both instances must load and pause the same saved checkpoint with the same player faction before connecting. No save transfer is implemented. Formation identities are assigned from the initial complete hierarchy; a manifest check rejects differing campaign dates or formation layouts. It is a compatibility check, not a hash of every saved field.

1. Load the checkpoint in the host and guest installs. Pause both immediately.
2. Host: F9, Host campaign. Share the displayed session key.
3. Guest: F9, enter the key, Join host. On one PC use `127.0.0.1` and port `18615`.
4. Close F9 in both. Resume only the host, then move an existing host formation.
5. Observe the same formation on the guest's native map. The guest camera remains independent.

The guest receives absolute native formation positions and facing once per second. Its campaign-controller, formation-simulation, BattleUnits and AI updates are suppressed; native unit visibility and sprite rendering remain active. Time-setting calls are forced to pause. Native guest orders and panel input are blocked for this milestone. Closing F9 does not unblock those controls.

Disconnect restores the guest's original formation transforms and activation states, then releases input. Both campaigns remain paused. Do not save the temporary replica. Economy, calendar display, officer changes, creation/deletion, hierarchy changes, tactical battles, native guest orders and versus faction initialization remain unimplemented. A changed formation roster ends the connection rather than guessing how to create native objects. Reorganization without a roster change is also outside this test's scope.

Transport uses bounded packets and a session key on trusted LAN/VPN only (no encryption). No firewall changes are made. Logs are per install in `GTIP-Logs/mp-*.log`; keys and full packets are not logged.

Validation: protocol/codec/loopback regression tests and compilation/structural hook checks. These do not prove Unity rendering or simulation isolation in a live campaign; verify two running instances before considering this milestone complete.
