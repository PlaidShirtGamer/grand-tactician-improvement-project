# GTIP MP Test v0.38 — campaign status symbols and newspaper dismissal

Both copies require **v0.38 / protocol 29**. Restart both games before joining.

The crown-like marks in the October 25 comparison are native entrenchment status textures (23–25); the guest's side marks are campaign stance textures (17–18). Native status selection requires a non-null entrenchment building as well as the replicated entrenchment level. `Regiment.UpdateCampsAndEntrenchments` creates these buildings through `CBuilding.Place` with `newlycreated=false`, while that method adds them to the global building list only for `newlycreated=true`. Until a native rescan, the host can therefore hold an entrenchment reference outside the global list used by world replication. The guest received the level but lost that reference and selected a different status symbol.

The host now captures its native status choice and the guest applies it to the existing unit status button. The selector is extracted from the original game's status-selection instructions, preserving the full precedence for stance, cavalry orders, camps, entrenchment, double quick, garrison, low supplies, fleets, combat and siege. Its UI gate and texture-writing tail are excluded. Offline/host rendering still uses the original method; the replica render wrapper uses the authoritative choice through later native redraws. This adds one validated formation field, with no synthetic buildings or guest simulation. Both screenshots also contain overlapping Smith's Division and 2nd Division labels; they do not establish that one formation changed alliance or color.

The October 25 guest log records the policy newspaper visibly displayed at `05:41:40.638 UTC`, then visibility restored from native panel state 2 at `05:41:46.114 UTC`, with no dismissal acknowledgement. The actual X uses `ButtonTooltip.CloseThisPanelIfPressed` and a generic panel fade, bypassing `Newspaper.Close`. The v0.37 visibility repair interpreted that fade as an accidental hide and reopened the undismissed issue. The guest close router now records explicit newspaper dismissal before native cleanup. It handles the native tooltip and persistent callback routes, requires the control to belong to the newspaper, and consumes only the frontmost graphic hit so a covered X cannot dismiss a paper. Passive hides remain recoverable; duplicate acknowledgements, next-issue identity and the two-player pause barrier remain enforced.

Verification: **46 map-status**, **49 newspaper-presentation** and **32 replica-boundary** checks pass. The selector checks interpret the actual native selection instructions, verify serialized clone parity and reject a deliberately changed entrenchment choice. The **736 multiplayer model/protocol/TCP checks** pass. Native checks bind **43 schemas / 775 fields**, including **8 map-status** and **10 newspaper-contract** checks. Staging in `artifacts/mp038-final` verifies **128 hooks/changes and 114 preserved methods**. These checks exercise production adapters and native metadata; actual Unity rendering/input still requires the connected comparison below.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `264B7CD358EE545B1186C15806944A4F4A160A3152F8DD373645610DDEE9AAE7`; runtime `254E58E5137E0D95D9B40FBDDA73A8FDAC1F116C7F741C43C63E0DA941E45E66`. Existing installer backups are retained.

## Connected test for v0.38

1. Compare the same formation and campaign time on host and guest. Check stance, fresh camps and entrenchments, cavalry orders, low supplies, combat/siege and fleet status symbols. Include loading a checkpoint and creating a new entrenchment afterward.
2. Complete a policy with the guest military/recruiting panel open. Close the newspaper on the guest first, then the host; repeat in the other order. Confirm it stays closed locally and campaign speed resumes only after both dismissals. Repeat for a later paper and a battle report.
3. Cover an unread paper with options/session UI and click there. Confirm the covered X does not dismiss it, the same issue returns after the covering panel closes, and the local deactivate-newspapers option still acknowledges without displaying.

## Earlier build: v0.37

# GTIP MP Test v0.37 — commander labels, quieter recruitment and newspaper visibility

Both copies require **v0.37 / protocol 28**. Restart both games before joining.

Existing military OOB labels now update when authoritative state arrives, using native `GetUnitSymbolData` formatting for commander names and abbreviations. The v0.36 detail pane and portrait could update while an already-created division icon retained its old text until the OOB was rebuilt. The new path updates only existing, matching native text objects; it preserves selection, filters, scroll and hierarchy layout. The symbol getter's campaign cache write is restored in `finally`, and structural transactions retain their existing full refresh.

The generic “Recruitment request sent. Waiting for the host...” popup is removed. Pending ownership, duplicate-click protection and matching-result handling remain; actual refusal reasons still appear and retain the editable draft.

The October 22, 12:08–12:19 a.m. CDT session (`20261022-050806` host / `20261022-050813` guest) records issue 1 opening on the host at 12:14:43 and a guest “displayed” log at 12:14:47. That old message only proved the native display method returned. Both saved newspaper settings are enabled, and source inspection did not establish a specific Military/recruiting callback that hid this issue. The exact observed visibility failure remains unproven.

Guest newspaper presentation now binds the controller on the actual newspaper panel, distinguishes receipt from completed native content and visible panel state, and checks again after native UI late updates. Initialization failures remain pending and retry without advancing the native edition counter. An undismissed prepared issue can restore its native panel without rebuilding its article. Explicit dismissal remains acknowledged once and cannot reopen; local newspaper suppression skips rendering and acknowledges the issue so it cannot leave the host waiting. Session/options menus retain priority. Diagnostics now include article identity, panel/parent path, state, alpha and canvas order. The generic native policy/historical/battle newspaper path remains in use; no policy-title allowlist was added.

Verification: **21 OOB-label**, **32 newspaper-presentation**, **98 recruitment/appearance/name** and **23 portrait** checks pass using production adapters and native/UI doubles. The **736 multiplayer model/protocol/TCP checks** pass. Native checks bind **43 schemas / 774 fields**, including **9 OOB-label** and **8 newspaper-contract** checks against actual original/compiled metadata. Staging in `artifacts/mp037` verifies **126 hooks/changes and 113 preserved methods**. Actual connected Unity rendering remains the acceptance comparison below.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `7BB8F2EF9C7D05F9A25901A28BF7EC6188BD8918B3369524D2013B3225FE9C9E`; runtime `BF6820BA2396FEC85CA10C24B3D1F4051A77513B17E31DF6F6A9FB2EEDFE5FDC`. The installed runtime passes the complete native schema, graph, snapshot and presentation checks. Existing installer backups are retained.

## Connected test for v0.37

1. Replace Wessels with Runyon (or another officer) on the guest while viewing the division in the OOB. Confirm its label updates alongside the detail pane without reopening, and selection, filters and scroll stay unchanged.
2. Recruit consecutive units on the guest. Confirm the waiting popup is absent, repeated clicks do not create duplicates, and genuine refusals still show their reason without losing choices.
3. Complete a policy with both games connected, including with the guest military/recruiting panel open. Confirm the policy newspaper is visible, can be dismissed once, and the host resumes only after the required acknowledgements. Also test the local deactivate-newspapers option and subsequent issues.

## Earlier build: v0.36

# GTIP MP Test v0.36 — native uniform lookup, recruiting isolation and commander portraits

Both copies require **v0.36 / protocol 27**. Restart both games before joining. This build follows the October 20, 10:47–10:57 p.m. CDT session (`20261021-034740` host and `20261021-034745` guest log names use UTC).

Uniform clicks reached the host, but requests 1–5 were rejected with null-reference errors rather than accepted and later lost. Native `Regiment.AdjustUniformSet` constructs `CampaignMainPanel.UniformSet`, whose constructor finds `UI/Panel_Main` through Unity's active-only `GameObject.Find`. A closed host military panel is inactive, although its initialized component and uniform sprite tables remain available. A single constructor call-site hook now supplies that cached native panel during a live session when the original lookup is null. It preserves a successful native lookup and never opens or activates the host panel. Native eligible-uniform selection still performs the actual edit. The latest logs contain no `uniform-color` requests, so they do not establish a separate new palette failure; palette persistence remains part of the connected comparison.

The recruiting pane now reapplies native `ObjectsToDeactivateForRecruiting` after its protected unit-pane redraw. Native `ShowPanel` reactivates panel buttons, and `UpdateReinforcementButtons` immediately returns for recruiting content. The v0.35 extra pane switch therefore revived existing-unit controls after the original recruiting mask had run. Reusing the exact serialized mask hides replenishment/reinforcement controls after state changes, roster refreshes and rejected drafts, including redraw failure cleanup.

The accepted commander replacement updated the guest's commander ID/name but left `Regiment.commanderpic` cached from the previous officer. Native host `AssignCommando` refreshes that cache; scalar replication does not call that operation. Replicas now refresh the native picture cache when the formation object, commander ID, portrait filename or dummy-photo choice changes, before panel refresh. Unchanged identities avoid repeated image loads. Preview commander changes also refresh their local native picture. The image operation writes only the presentation cache; command assignment/rank/eligibility remain host-owned.

The first recruitment request 12 succeeded, creating formation 1973 under parent 521. No second recruitment request reached the host before disconnection. At 10:57:15 p.m. CDT, the guest's revision 343 position check stopped on existing formation 18, `34th pennsylvania`, under parent 543: expected `(1273.537, 493.841522, -1075)`, actual `(1272.90637, 493.841064, -1075)`. Its ancestors still matched, and the previous revision-330 CSVs agreed. This is a guest state-verification failure, followed by host EOF, rather than a transport timeout.

The new failure detail led to a concrete preview-boundary bug: `buildingPreview` previously made `IsRecruitPreview` return true for every formation during native draft creation. An unrelated native tooltip/list refresh could then enter `UpdateGroupStrength`, which calls `UpdateTransferTime` for inactive descendants and moves them toward their parent. The observed displacement has that direction. Restoring registered scalar fields after preview creation cannot undo a Transform write. Preview permission now requires an unregistered temporary object, both during construction and after the draft is assigned. Production-boundary regressions reproduce the observed transfer displacement and scalar-restoration gap, then verify that registered units cannot enter that native path. Host/offline transfer behavior and temporary-draft calculation remain available. Strict position tolerance and verification remain enabled; rejected-command logs now retain full exception stacks.

Verification passes **98 recruitment/appearance/name**, **32 replica-boundary**, **30 appearance** and **23 commander-portrait** checks, plus the existing cursor, bridge, camera, motion, hierarchy, editor and AI suites. Native checks cover **43 schemas / 774 fields**, including **22 appearance-graph**, **11 recruiting-contract**, **13 transfer-boundary** and **9 portrait-cache** checks. The **736 multiplayer model/protocol/TCP checks** pass with local loopback access; the restricted environment could not establish the test socket, so that suite was rerun outside it. Staging in `artifacts/mp036` verifies **126 hooks/changes and 113 preserved methods**. No live Unity gameplay is exercised by those checks.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `2605D776A1016750B9EF331391F5D256F9E071C16BD8A6AFD18512C46A729106`; runtime `4EAC688AC699A4BEED95EF46D71335B85D0EA1D4D1C114C3F70BD433EA6C0B59`. The installed runtime passes the complete native schema, graph, snapshot and presentation-boundary checks. Existing installer backups are retained.

## Connected test for v0.36

1. Keep the host military panel closed. On the guest, cycle an attached regiment's uniforms, recolor both components, close/reopen and compare both clients. Repeat with the host palette open, and with the host viewing another unit. Preserve both logs if color requests or accepted changes are absent.
2. Open recruiting, rapidly switch states/types and change the preview commander. Confirm only recruiting controls appear, the portrait matches the officer and the earlier commander-pane corruption stays fixed.
3. Replace an existing unit's commander on the guest. Confirm both name and portrait update without leaving the page, including after another state revision.
4. While other inactive regiments are transferring, create and recruit multiple guest drafts. Keep an unrelated army visible in the OOB. Confirm each valid request creates once and the session remains connected. Retain both MP logs and `.pose-failure.csv` on any failure.

## Earlier build: v0.35

# GTIP MP Test v0.35 — existing-unit appearance, repeated recruitment and pose ownership

Both copies require **v0.35 / protocol 26**. Restart both games before joining. This build addresses the v0.34 session recorded on October 19 at 10:09–10:23 p.m. CDT (`20261020-030938` host and `20261020-030944` guest log names use UTC).

The host rejected every existing-unit `uniform-color`/`uniform-step` request as unavailable. The bridge incorrectly required an active campaign-map GameObject, although native military management can edit inactive attached regiments. That restriction is removed for appearance, and the same misplaced gate is removed for managed-unit naming/equipment before the separate movement eligibility check. Roster/side/type checks and native uniform/palette eligibility remain. When the host has the same unit's palette open, an accepted remote color updates that palette selection before native redraw can overwrite it with an old local choice. A stale `lastusedunit` pointer alone is insufficient to refresh the host panel; actual selection must match too. Guest uniform arrows show the native eligible result while awaiting authority without changing replicated fields, and pending previews expire. The caption remains native-controlled; exact prefab text/layout visibility has not been reproduced outside Unity, and diagnostics now record its active state.

Rapid recruiting-state changes could leave the native commander attributes/history pane behind the recruiting overlay. Native `SwitchCommanderPanel(0)` falls back to commander mode when the chosen officer has no current command; preview transactions intentionally restore that officer reference. The bridge now restores the complete unit pane in scoped preview context. The Unity Player.log additionally records repeated `UnitIsInsideRadius` null references through commander influence/stat tooltips: a tooltip-only ancestry scope was too narrow. Both the parent hierarchy and officer command context now span the read-only commander redraw and restore in finally, while the preview remains isolated between calls. A temporary selection reference also prevents native fallback selection from scanning the attached preview into real selection lists; the exact prior reference is restored even if drawing fails. The full roster-updating `UpdateUnitData` remains outside this scope. A failed native preview redraw previously left its change flag set, causing repeated preview replacement on subsequent frames.

Only recruitment request 19 succeeded (formation 1973 under parent 493); requests 26, 29 and 34 were rejected by a generic native-requirements check. Those old messages do not identify the failing predicate, so it would be incorrect to claim a particular pool or credit rating caused all refusals. A definite bridge mismatch was found: the host prohibited home states with no on-map average position even though native single-unit recruitment supports off-map states by default. That extra restriction is removed. Valid state, size, pool, credit, horse-artillery policy and commander checks remain, with specific refusal messages and state/type/size/pool/rating diagnostics. The draft remains open pending its own host result, repeated clicks cannot submit it twice, and a refusal retains the selected state, colors and uniform while showing its reason. Success closes the matching draft; a late response cannot close a replacement draft.

The guest disconnected at 10:23:06 p.m. CDT after strict position readback failed for formation 1973 at revision 575. The host then observed EOF. This was a replication check failure, not a transport timeout. The previous CSVs retained revision 570, where both positions agreed, so the exact failed displacement is unknown. A reproducible competing-writer bug was identified: a legacy map view sampled positions after the acknowledged data capture, then wrote them into the guest's logical transforms under the same roster revision. A later scalar-only delta could leave that newer pose in place while readback expected the verified baseline. Map views now update visibility and control/UI metadata only; campaign data owns logical positions and timestamped motion remains visual only. The existing 0.1 X/Z tolerance stays enabled. A future failure records expected/actual coordinates and ancestor/native-cache/block details in `.pose-failure.csv` before stopping.

Verification: the full build passed **736 multiplayer model/protocol/TCP checks** and the existing boundary, cursor, editor, hierarchy and AI suites. Final affected checks pass **88 recruitment/appearance/name**, **23 appearance**, and **25 snapshot-presentation** cases. Native checks cover **43 schemas / 774 fields**, including **18 appearance-graph**, **8 recruitment-contract**, **13 commander-context** and the existing **20 preview-graph** checks. Staging in `artifacts/mp035-final` verifies **125 hooks/changes and 113 preserved methods**. These checks exercise production bridges with native-operation doubles or actual assembly metadata; live Unity input and visuals remain pending in the connected comparison below.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `BC4B590C0FFC81A7B1757ACC7311D90BDF69F3F3615C2AF186128852C8F0AD0B`; runtime `0307BB95F74CE129BAD193AF11C9F1955E4AB37B3EF274071CB347CCE4490D7A`. The installed runtime passes the complete native schema, graph, snapshot and presentation-boundary checks. Existing installer backups are retained.

## Connected test for v0.35

1. Edit an attached, inactive regiment from the military list. Recolor both components, cycle uniforms, close/reopen and compare on both clients. Repeat with the host palette open on the same unit and on a different unit. Check the native Uniform caption separately.
2. Rapidly alternate recruiting states and unit types. Confirm the unit preview replaces the commander attributes/history pane, with no exceptions in Player.log. Recruit twice in succession. A valid request should create once; a refused request should retain choices and show the exact reason.
3. Select a supported off-map state where recruits and funding are available. Confirm it follows the same native eligibility as the host. Verify low-credit/insufficient-pool refusals remain enforced.
4. Recruit, move and pause units while state updates continue, including scalar-only updates after newer map views. Confirm the session survives; preserve both logs and any `.pose-failure.csv` if strict readback still fails.

## Earlier build: v0.34

# GTIP MP Test v0.34 — native UI contracts, transient events and presentation cost

Both copies require **v0.34 / protocol 25**. Restart both games before joining. The [coverage matrix](multiplayer-coverage.md) separates authoritative-state verification, transient-event delivery and native presentation/input acceptance.

The v0.33 guest ended at `2026-10-19 06:01:13 UTC` with `NullReferenceException` in `Regiment.GetUnitTooltipInfo` at native offset `0x001ea`, through the commander preview and lifecycle roster refresh. The host then reported EOF. This was not a socket timeout or state-divergence rejection. Player.log also shows the uniform click entering `GuestRecruitPreviewAction` before the same tooltip exception interrupted its redraw. Native tooltip code walks parents until the literal Units folder; the isolated recruiting dummy lived under the multiplayer object. The new tooltip bridge temporarily supplies real native ancestry only for that synchronous getter and restores the dummy's isolated parent in finally. Keeping it permanently under real units would incorrectly re-register it during native descendant scans, while creating a second Units folder would make native global lookups ambiguous; neither workaround is used. Commander current-command context is also restored in finally.

Structural unit/ship adapters now queue roster refresh until all world, officer, battle and dispatch references are applied. A presentation fence restores authoritative scalar fields even if the redraw throws, then strict native readback and transaction checks still run. Display failures are logged separately without converting valid state into a transport disconnect; restoration/application/readback failures retain the existing stop behavior.

Keyboard rotation bypassed the native campaign lock gate used by mouse rotation. The camera wrapper enforces the native preference at rotation consumption, clears angular momentum and pending compass yaw and retains north orientation. The original method still handles pan, zoom and camera pitch. Host, guest and offline campaign cameras share this local option; unlocked/tactical behavior is preserved.

Battle, flank, routed and other rolling notices now publish immediately from the common accepted native creation hook. Previously only construction/remote-command notices received immediate delivery; other notices could expire before the next state capture. Native duplicate/rate suppression stays upstream. A bounded 2,048-ID receipt window deduplicates fast delivery against delayed snapshots and rejects evicted history. Expired components are cleaned, checkpoint history is consumed without replay, local popup visibility settings remain local, and notice logging is throttled. The compiled audit enumerates 16 original producer methods rather than maintaining a popup-text allowlist.

Left-side dispatch notifications now search their actual native `PanelHandler.symbol` collection and validate message number, icon, sender and referenced unit. Clicking uses native `InteractScreenMessage` for open/dismiss and removal, independently of the dispatch-log route. The frontmost native UI hit must belong to that notification.

Motion remains **10 Hz / 150 ms buffer**. Latest logs showed guest main-thread applications of 324.6 and 436.8 ms; no motion-cadence telemetry existed, so changing packet rate would not establish a remedy for those stalls. Visual roots now cache after native initialization, each unit pose evaluates once per frame unless state changes, and temporary symbol picking runs only on left clicks. The production drawing harness reports zero steady allocations and hierarchy searches for its synthetic 2,000-unit/60-frame loop; this is not a live Unity FPS measurement. New 15-second motion telemetry records source/arrival gaps, queue deferrals and evaluation counts. Periodic apply timings now separate building, lifecycle, adapter, roster-UI, readback and motion work. Use these measurements for the next optimization rather than increasing traffic or prediction blindly.

Verification: **736 multiplayer model/protocol/TCP checks**, **65 recruitment/appearance/name checks**, **13 camera-lock checks**, **14 motion-rendering checks**, and the existing cursor/boundary/bridge/editor/hierarchy/AI suites pass. Native checks cover **43 schemas / 774 fields**, including **17 transient-event boundary checks** and **20 recruiting-preview graph checks** against the actual original and compiled assemblies. Staging in `artifacts/mp034` verifies **125 hooks/changes and 113 preserved methods**. Live Unity interaction and pacing remain the connected acceptance gate described in the coverage matrix.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `253561D721730E4B00A05E14439CE21455BB1148B7C9C41EA8F6BA3282862522`; runtime `C8468447BA8F97816E05FB4A39EE8B5670D1D88C4993E62CD0E39BBF57821E38`. The installed runtime passes the native schema, snapshot, event, graph and presentation-boundary checks. Existing installer backups are retained.

## Earlier build: v0.33

# GTIP MP Test v0.33 — guest appearance, production, map displays and movement cadence

Both copies require **v0.33 / protocol 24**. Restart both games before joining. Work remains on `mp-test`; base GTIP versioning is unchanged.

Guest uniform arrows, recoloring and palette controls now route directly to the appropriate native control. Recruitment changes act on the local dummy; existing-unit appearance changes execute on the host and replicate back. Native unit/state/policy uniform eligibility remains in effect. Rendering an existing soldier picture uses a disposable palette copy, so a guest redraw cannot overwrite host colors or normalize the authoritative uniform. Renaming now enables TMP input delivery, preserves typed drafts through native refreshes, submits on focus loss as well as end-edit, and updates the native visible label alongside its input field. Pending names remain visible until acknowledged or expired.

The production comparison exposed omitted display inputs: `CBuilding.producedpcspa`, `demandedpcspa`, produced-good references, native sales ranking and the five building profit/volume/tax/efficiency/workforce histories. These now replicate, with readback verification. Existing production rows refresh their values/icons without resetting filters, selection or scroll. A single-pass sales ranking was compared against the original managed `GetHighestTradeIDs` on 100 synthetic trade histories. The previous economy audit could report equality while these omitted display values differed; the audit now includes building values and the new records.

Destination squares were absent because guest path records omitted native final block waypoint positions/facings and did not run the display-only `ShowWaypoints`. Both are now included, with cleanup on stop, deselection and changed block counts. Selected-unit supply highlights now use the host's regrouping-area references and connected-depot list, then call native display routines. They do not recalculate guest supply or readiness. The gold Movement Options panel remains local to the player issuing the order; destination squares are shared map presentation.

The latest v0.32 logs showed approximately 0.7–1.8 seconds in the full state receipt phase, with revisions roughly 1.2–2 seconds apart. Positions were coupled to that transaction and the old renderer restarted a catch-up blend at each endpoint. A separate **10 Hz** presentation stream now sends changed/recently moving positions, independently of full-state preparation and transfer. It includes only visible formations whose exact parent/type/faction identity the guest has acknowledged. An eight-sample history renders host wall-clock timestamps through a **150 ms** jitter buffer, without extrapolation. Time compression changes source velocity rather than tween duration. Pauses display the latest received endpoint. Reliable captures carry their source timestamp, so an older completed transaction cannot rewind a newer visual sample. Logical transforms and exact readback remain authoritative; temporary symbol offsets during picking are restored in `finally`. The sender skips a sample when its outgoing queue is busy and retains unsent endpoints for the next opportunity.

Fingerprint verification now hashes cached canonical row bytes directly and compares common 2D arrays without boxing every element. The checksum format, numeric handling, revision rules and failed-commit behavior are unchanged. A retained synthetic 18,000-row benchmark measured full encoding at 233.6 ms / 113.72 MiB allocated versus warm cached verification at 40.9 ms / 6.61 MiB, with identical SHA-256. A separate before/after run of the same unchanged-fingerprint fixture measured 159.4 ms / 161.72 MiB before versus 27.7 ms / 4.41 MiB after. These are managed synthetic measurements, **not measured Unity frame-rate or network improvements**.

The first full-suite run exposed an intermittent `ObjectDisposedException` on `Microsoft.Win32.SafeHandles.SafeWaitHandle` during TCP connection completion: the old APM wait handle could be disposed before `EndConnect`. Joining now uses the task-based bounded connection operation. Twenty-four rapid production TCP joins pass, as do the existing stalled-consumer, heartbeat, timeout and reliable-ordering regressions.

Verification: **710 multiplayer model/protocol/TCP checks**, **51 recruitment/appearance/name bridge checks**, **20 replica-boundary**, **26 cursor**, **21 native-bridge** checks and the editor/hierarchy/AI suites pass. Native schemas cover **43 schemas / 774 fields**, including compiled display-only motion boundaries and native production/map inputs. Staging in `artifacts/mp033` verifies **123 hooks/changes and 111 preserved methods**. Actual connected Unity input, map drawing and movement pacing require the following manual comparison.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `E428650DFB353822C20A9ABD7B37CCBC23B858E59ACB6A680D7542F5052FA213`; runtime `CE93F99128D1757F89A4EE3651AAA86EF70EFEEF82120981826CD7EBE8B9FCD4`. The installed runtime passes the native schema/snapshot/display-boundary checks. Existing installer backups are retained.

## Connected test for v0.33

1. On the guest, change recruitment uniform type and both palette colors; recruit the unit and compare it on both clients. Recolor an existing unit, close/reopen its panel and confirm persistence. Rename units, fleets and ships with Enter and by clicking elsewhere, including while the campaign updates.
2. Compare the Dover farms and several other companies at the same paused date: production amounts/icons, workforce, tax/value, efficiency and detail histories. Leave Production open while advancing the campaign and compare again.
3. Issue army/fleet movement from each client. Compare route endpoints, stop/deselect cleanup and supply circles when selecting the same unit. Check paper and 3D maps.
4. Compare movement at low and high compression, then pause/resume, recruit/reparent during movement and change direction. Check selection at the visible interpolated symbol. Retain both logs for cadence, capture/worker/application timing and queue-depth comparison; do not infer live performance from synthetic benchmark numbers.

## Earlier build: v0.32

# GTIP MP Test v0.32 — live battle presentation and recruitment UI

Both copies require **v0.32 / protocol 23**. The latest v0.31 guest successfully applied new formations, including guest recruitment request 45 (formation 1947). It subsequently ended revision 269 at `01:39:54 UTC` with `Native state divergence: 1:9 field=menfielded expected=25611 actual=25612`; the host then observed EOF. This was a local campaign-cache recalculation after a lifecycle/UI refresh. Native `RecruitingList.Initialize` calls `Nation.UpdateUnitStatistics`, which scans the local hierarchy, and `UpdateRecruitData`, which also changes population/recruit pools. Both operations now remain host/offline only. Structural UI refresh also reinstalls the authoritative nation/alliance/officer/unit scalar records before strict readback; divergence verification remains enabled.

Live battle monuments, their native autoresolve presentation state, participant references, status icons and logs now replicate after the unit/world lifecycle. Native unit selection can therefore find battles started after the checkpoint. Guests initialize native ambient battle audio and consume bounded host sound/explosion events once; they do not run the native combat cycles. Raid/skirmish/interception sources without monuments receive separate presentation objects and expire with the host source. Ongoing loop state also replicates, so joining during an assault does not depend on another sound transition. Native sound throttling, effect prefabs and local audio settings remain in use. Host battle-result auto-open calls now carry an event serial, displaying the matching guest telegram alongside its newspaper once. Existing reports are not automatically reopened, and guest read/hide state stays local. Dispatch references can resolve replicated battle monuments.

The three guest recruitment controls for previous/next uniform and volunteers/draftees now act directly on the active local preview and refresh native eligibility information. Uniform eligibility remains native; switching to an insufficient pool disables actual recruitment through native UI/host validation. Live roster updates refresh the recruitment view without reinitializing its filters or clearing the selected recruiting state.

Native naval battle statistics also force a fleet-cache recalculation through a display getter. Guests now retain the host fleet/vessel scalars during that read. Only the validated naval structural transaction can rebuild those caches locally, before restoring received scalars; host/offline calculations retain their native behavior.

The new ongoing-battle records support viewing. Ongoing autoresolve stance, retreat and manual-battle actions remain host controlled; guest callbacks cannot mutate combat locally. The existing synchronized pre-battle engagement choices retain their prior behavior. Connected Unity battle rendering, audio and recruitment interaction still need manual verification.

Verification: **591 multiplayer model/protocol/TCP checks** pass, including **29 battle presentation/source identity checks** and **13 dispatch auto-open checks**. The production recruitment preview bridge passes **24 checks** with native-operation doubles; the simulation boundary passes **20 checks**, including nation and fleet UI recalculation protection. Native compiled battle ABI/boundary checks pass **38 assertions**, and actual schemas plus synthetic snapshot/commit checks cover **40 schemas / 745 fields**. Cursor, native bridge, editor/hierarchy/AI suites also pass. Staging in `artifacts/mp032-verified` verifies **122 hooks/changes and 110 preserved methods**. These checks do not exercise live Unity battle rendering, sound output, mouse input or recruitment.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `914B59B740965D17A8AB69B8D74BC70DA801A0892A88F23E6A7690EE67BB5D42`; runtime `AB87F9A8DAEE5B0F346447CD306342FFE0905FBA99445B27C5A2290D285FBE77`. The installed runtime passes native battle/schema/snapshot checks; existing installer backups were retained.

## Connected test for v0.32

1. Restart both games and join **GT MP Test**. Select units in an existing battle, then initiate a new battle and select its participants on the guest. Confirm the native autoresolve view appears and its strength, casualties and status track the host.
2. With matching local audio settings and nearby cameras, allow the campaign to run and compare battle ambience/explosions. Repeated state updates must not replay old event batches. End a battle and check the guest battle ambience stops with it.
3. Finish a major land/naval battle. Confirm the guest receives the native result telegram alongside the newspaper, can close it locally, and can reopen it from its dispatch log. Later revisions must not reopen a closed report.
4. On the guest, select a recruitment state and change uniforms and volunteers/draftees. Check immediate preview/eligibility updates. Leave that view open while the campaign creates/removes another formation, then complete recruitment and confirm the selected uniform/recruit type on both clients. Repeat while paused and running; retain both logs if a state check fails.

## Earlier build: v0.31

# GTIP MP Test v0.31 — acknowledge recruited formations before map snapshots

Both copies require **v0.31 / protocol 22**. The latest v0.30 session successfully loaded the guest checkpoint, synchronized revision 1, and continued through revision 270. At `01:02:29 UTC` the host accepted guest recruitment request 38 and created formation 1940 under parent 535. At `01:02:30 UTC` the guest ended the session with `Snapshot formation does not match the loaded checkpoint: 1940`; the host then observed EOF. This was a strict map-snapshot validation failure after successful recruitment, rather than a transport timeout.

Host session IDs were refreshed immediately during recruitment and during campaign capture. That made the local roster appear current while the guest still had an older applied lifecycle revision. Both map-snapshot send paths checked local registration, allowing a snapshot containing the new formation to be labeled with the earlier revision. The host now keeps a separate copy of formation IDs, sides, tiers and parents from the exact lifecycle records acknowledged by the guest. Map snapshots wait until the complete live roster matches that acknowledged roster and use its revision number. A receipt for an older in-flight revision cannot acknowledge a later recruitment. Deletions, replacements, tier/side changes and reparenting use the same barrier; ordinary movement and scalar changes remain eligible. Guest snapshot validation remains strict. A deferred map update is recorded in the host log.

Verification: **549 multiplayer model/protocol/TCP checks** pass, including **28 acknowledged-roster checks** reproducing recruitment before an older receipt and another recruitment while a later revision is in flight. The checks also cover removal, equal-sized replacement, reparenting, tier/side changes, hidden opponents, input ownership, revision rejection and session reset. Native schema/synthetic snapshots cover **34 schemas / 628 fields**; cursor, native bridge/boundary, editor/hierarchy/AI suites also pass. Staging verifies **110 hooks/changes**. These checks do not exercise live Unity recruitment.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `B8385AF957A1F57ABB55ADAE79656C03280A2D7245970BBF193FCA541F0F2817`; runtime `48A94E10517A05ECD71AF2CB79F57EAD1F1EA45AC6AF42ACE51786583A4D000D`. The installed runtime passes native schema/snapshot checks; existing installer backups were retained.

## Connected test for v0.31

1. Restart both games and join **GT MP Test**. Recruit a unit from the guest, then recruit one from the host. Confirm each appears in both military lists and on the native map after its lifecycle update, with no disconnect.
2. Recruit several units while the campaign is running and repeat while paused. Rename, move and reassign the resulting units; check both clients receive the hierarchy and movement. Check a normal removal/disband also synchronizes.
3. Retain both new logs if a disconnect occurs. A live Unity recruitment test remains required; the model/transport checks do not exercise native prefab creation or rendering.

## Earlier build: v0.30

# GTIP MP Test v0.30 — transport liveness while Unity loads a checkpoint

Both copies require **v0.30 / protocol 22**. In the latest v0.29 attempt, the guest verified the complete checkpoint and began loading at `23:52:25 UTC`. The host closed first at `23:52:59 UTC` with a socket read timeout; the guest subsequently observed EOF. Neither side reached the loaded/ready handshake. Queue peaks were only 2 on the host and 14 on the guest, and the Unity log contained no relevant exception. This failure occurred during native campaign loading, before initial state replication.

The previous keepalive was emitted by `BootstrapTick` on Unity's main thread. A loading operation that held that thread could stop outgoing keepalives long enough to hit the transport's 15-second read timeout, even while the process and its receiver were still running. The transport writer now emits a protocol-versioned heartbeat after one second without an application write, using monotonic elapsed time and no Unity calls. The receiver recognizes only the exact valid heartbeat frame after the existing bounded frame read and consumes it without adding to the application queue. Other frames retain normal ordering and validation. Main-thread bootstrap heartbeat generation was removed.

The 15-second no-traffic socket timeout, five-second send timeout, 300-second campaign transfer/load deadline, and 30-second campaign revision deadline remain enabled. A connected peer must also submit its initial join request within 30 seconds, so transport heartbeats cannot keep an unstarted host handshake open indefinitely. Heartbeats indicate transport liveness, not successful campaign loading. Session-end diagnostics now include the application phase, heartbeat counts and socket error code; embedded NUL padding is removed from native error messages.

Verification: **521 multiplayer model/protocol/TCP checks** pass, including **19 heartbeat checks** that leave both application callers idle for 18 seconds, beyond the existing socket timeout. Both workers keep the connection alive without filling application queues; a silent raw peer still times out, reliable checkpoint/readiness messages remain ordered, modified heartbeat frames retain normal validation, and peer closure still reports EOF. The **26 cursor checks**, native bridge/boundary, editor/hierarchy/AI suites and native schema/synthetic snapshots covering **34 schemas / 628 fields** also pass. Staging verifies **110 hooks/changes**. These checks do not exercise native Unity campaign loading.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `281182598C01AD6B8E976DBB1203D85AAB5653289A07F714D0A4D3810696BEB1`; runtime `8D186F7F93335D5192545D21FDFEA3B8F8EFB83558B312B5C24722B487E60AA8`. The installed runtime passes native schema/snapshot checks; existing installer backups were retained.

## Connected test for v0.30

1. Restart both games and join **GT MP Test** from the guest main menu. Allow the guest to finish loading while switching focus between the game windows as needed. Confirm the host logs `Guest loaded and matched host checkpoint` and the revision 1 receipt, then keep the campaign paused for a minute.
2. Check both players can resume/pause and interact after loading. If loading genuinely stalls, its existing five-minute deadline still applies; a transport heartbeat does not accept an incomplete campaign or bypass its checksums/manifest.
3. If the session closes again, retain both new logs. The phase, socket error and heartbeat counts distinguish missing network traffic from an application progress deadline. Connected Unity loading remains unverified.

## Earlier build: v0.29

# GTIP MP Test v0.29 — guest cursor focus and menu recovery

Both copies require **v0.29 / protocol 21**. The latest v0.28 session stayed connected through revision 150 and continued accepting clicks/time controls. It ended when the host explicitly disconnected; the guest receive queue peaked at 31, with no queue-overflow failure. The latest Unity log contained no relevant exception. Existing logs did not record cursor lock/focus, so they cannot establish exactly which event centered the cursor.

Native `BattleUI.Update` releases `Cursor.lockState` when focused and requests a lock when unfocused. The guest replacement skipped that routine, including its recovery, and the previous multiplayer end/menu/input cleanup did not release global Unity cursor state. This supplies a likely path for the reported center-lock that persisted into the main menu. Campaign camera movement itself contains no lock writes. The other native writer is an orbit component's Escape toggle.

Guest cursor ownership now starts at join and remains active across session end and return to the menu. Focus changes, frame updates, F9/input cleanup, disconnect and menu activation recover a locked or hidden guest cursor. Both native UI focus setters and the orbit setter pass through the ownership guard, preventing a late native callback from immediately relocking the guest. It releases ownership when a later local campaign or host session starts. Native lock behavior passes through while guest ownership is inactive. Native cursor textures and camera controls are retained. Connection logs now record focus changes, repaired lock/visibility state and the source of prevented native lock requests, with duplicate request logging suppressed.

Verification: **26 production cursor-guard checks with Unity state doubles**, plus **502 multiplayer model/protocol/TCP checks**, native bridge/boundary checks, the editor/hierarchy/AI suites, and native schema/synthetic snapshots covering **34 schemas / 628 fields**. Staging verifies **110 hooks/changes and 98 preserved methods**, including all three native cursor writer redirections. These checks do not exercise real window focus or mouse hardware in Unity.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `A5B4D6EEF1B8CCFFA0EC1A5B0B46AB40FDDD8F7DD12257E9D8EB4EE4C1CD12AE`; runtime `7E6028E27AFF0ADE2DDE75DF17BA3FF3F8F24A55022ACCE37ED9227DE5842652`. The installed runtime passes native schema/snapshot checks; existing installer backups were retained.

## Connected test for v0.29

1. Restart both games and join **GT MP Test**. Move and click the guest pointer, then switch focus between the two game windows and another application. Repeat while paused, with a native UI panel open and with F9/Escape menus open. Check the pointer remains freely movable after returning to the guest.
2. Leave the guest window out of focus while the host ends the session. Return to the guest main menu and verify cursor movement, clicking and visibility. Repeat with the guest focused at disconnect.
3. Rejoin, then leave multiplayer and start a local campaign or host session; verify its native input behavior. If centering recurs, retain the new guest log with its cursor/focus diagnostics. Connected v0.29 behavior remains unverified.

## Earlier build: v0.28

# GTIP MP Test v0.28 — clock-queue disconnect during initial synchronization

Both copies require **v0.28 / protocol 21**. The October 18 v0.27 guest log applied revision 1 successfully (23,540 records, approximately 3.6 seconds on Unity's main thread), then reported `Peer exceeded receive queue`. The host reported EOF after the guest closed. The guest's economy readback and initial building binding had succeeded. The transport disconnected when more than 64 messages accumulated while the main thread was busy; continuous clock packets could reach that limit even in a paused campaign.

Incoming and outgoing queues now replace consecutive, valid clock envelopes for the same session with the newest sequence. Ordered commands, state chunks, results, session boundaries and malformed messages remain barriers and are never coalesced. The incoming queue still holds at most 64 pending frames; when full of reliable traffic, its network reader waits for the main thread to make space rather than throwing a queue-overflow disconnect. Closing the connection wakes a waiting reader. Socket timeouts and frame-size limits remain enabled. Unchanged paused clocks send once per second; running clocks retain the existing update cadence and pause/speed changes send immediately. Connection logs record peak queue depth and coalesced-clock counts when a session ends.

Regression verification deliberately leaves the guest application consumer idle for roughly four seconds while the production TCP transport receives 120 native clock packets, then checks delivery of the newest clock and a reliable barrier. A separate test fills the real receive queue to 64 and verifies all 180 reliable messages arrive in order after consumption resumes. Pure queue checks cover 1,000 clocks, stale sequences, different sessions, malformed clock envelopes, state-chunk barriers, producer wake-up and disconnect cancellation. **502 multiplayer model/protocol/TCP checks** pass. Native schema/snapshot checks still cover **34 schemas / 628 fields**; native-bridge, replica-boundary, editor and hierarchy/AI suites pass. Staging verifies **108 hooks/changes and 98 preserved methods**. This is a transport regression test, not a live Unity campaign test.

Installed in both desktop test copies with matching SHA-256 hashes: patched game `6153BB8A6C75E4BED3864B857F6CAF79BFF1507764FF92C278B70DE7EB632D06`; runtime `EFBC66C488FED83F9121DF0AF6EC47ACF5CDE1A0CA66A9FE5C407E0682ED4312`. The installed runtime passes native schema/snapshot checks; existing installer backups were retained.

## Connected test for v0.28

1. Restart both games, host **GT MP Test**, and join from the guest main menu. Leave the campaign paused for at least a minute after the initial synchronization. Confirm the guest stays connected and the host log receives the revision 1 application receipt.
2. Unpause briefly, pause again, and verify time, units and economy screens continue updating. Exercise the v0.27 construction/upgrade checks below once connection stability is confirmed.
3. If the connection closes, retain both new connection logs; their final transport statistics distinguish a queue backlog from another failure. Connected v0.28 gameplay remains unverified.

## Earlier build: v0.27

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
