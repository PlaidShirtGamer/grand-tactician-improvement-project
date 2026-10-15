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
