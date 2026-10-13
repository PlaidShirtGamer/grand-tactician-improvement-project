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
