# Tactical co-op implementation

The campaign co-op remains the playable baseline. Work on local branch `battle-mp` targets two players sharing a campaign faction against host-controlled AI. The host owns gameplay; camera, selection and presentation remain local.

The first milestone is a verified **paused** campaign → battle → campaign round trip. The local v0.40 / protocol 31 candidate now wires native Play Battle into this controller when the host enables **Enable shared battle loading test** in F9. Current game installations retain the campaign build; this candidate has not passed connected Unity acceptance.

Expected behavior is shared entry: selecting Play Battle on the campaign engagement panel, by either host or guest, requests the same host-approved transition. Both peers lose campaign control and show an opaque persistent loading screen. A faster computer waits for the slower one; neither battlefield is revealed until both initialized native worlds match. Exact simultaneous CPU loading is unnecessary, but one player must never be left playing the campaign while the other plays a battle. Native loading-screen callbacks and fades cannot reveal a partially loaded world.

## Implemented foundation

| Component | Behavior implemented | Evidence and limits |
|---|---|---|
| `MpTacticalLifecycle` | Explicit campaign, preparing/loading/verifying battle, paused battle, returning/verifying campaign and fault phases; host-owned monotonically increasing battle generations; matching native-baseline readiness barrier; fail-paused state | Production-model tests cover repeated round trips, delayed/duplicate receipts, context mismatch, timeouts and loss. No combat-running phase exists yet. |
| `MpTacticalCheckpoint` | Native campaign-save files plus their `Battle/` save; bounded data-only archive; checksum validation; atomic extraction into a fresh isolated folder | Synthetic malformed archive cases and exact byte/hash round trips of existing native battle saves. The runtime load plan additionally checks native file completeness and installed content. File equality does not establish initialized Unity world equality. |
| `MpTacticalSession` | Production `MpCodec` envelopes and chunked `MpTransport` exchange; separate archive and native-readiness acknowledgements; scoped scene-load requests; retry handling; stage/generation rejection; receive deadlines and cleanup | Protocol tests exercise complete entry/return exchanges, corrupt and stale data, asynchronous completion identity, and actual localhost TCP. Native loading is supplied as an explicit callback and is not implemented by this layer. |
| Native prerequisite audit | Checks actual native metadata for saved battle loading, initialization order, simulation-bearing updates and host-only result processing | `TacticalContracts` runs with the existing native schema suite. These checks verify the integration targets, not successful game interaction. |
| Runtime scene adapter | Immediate shared hold, final FIFO campaign fence, isolated native battle capture, both-peer operation-8 loads, initialized-state readiness, guarded pre-battle campaign return | Production adapter tests run with native dependencies doubled. Real save preflight/loader queue tests and pristine-assembly compilation supplement them; Unity scene creation is not exercised. |
| Paused authority boundary | Preserve native regiment/object/controller initialization; hold both peers' simulation, orders, physics damage/ownership, weather and result processing | Production authority bridge tests cover startup stages and native forwarding. Camera movement is local after readiness. |
| Initialized native fingerprint | Canonical unit ancestry, participants, poses/formation blocks, key combat/order values, officers/objectives/side totals/clock, plus installed gameplay configuration/scene descriptors | Excludes local camera/music/UI preferences. Does not fully hash terrain-bundle bytes or prove complete campaign economy/logistics parity. |

No tactical unit commands or live combat replication are enabled. Both peers must use v0.40 / protocol 31; previous campaign clients are incompatible with this development candidate. Campaign messages carry a world generation so pre-battle frames cannot alter the restored campaign.

The host first constructs the native battle while both players remain behind the loading screen, captures it, then both reload that same saved battlefield. This extra host load is acceptable for the first correctness test; later work can reduce it after the actual native initialization is understood. Return currently **discards the test battle** and restores the captured pre-battle campaign on both peers. It never runs native randomized battle results. The host can restore that campaign offline after a connection failure; a disconnected guest remains held until returning to the main menu.

## Native integration gate

The supported [checkpoint/load audit](tactical-native-checkpoint-audit.md) identifies operation 8 as the saved campaign-battle loading path. The opt-in adapter replaces the previous end-on-tactical-entry behavior with these boundaries:

1. Drain and acknowledge campaign replication before replacing any native objects. Keep TCP heartbeats alive independently of Unity loading. Old campaign messages and old battle generations must not reach the new scene.
2. Hold both clients' simulation and input while preserving required native initialization. Native `Regiment.Update`, `AIBattle.Update`, `BattleUnits.Update` and `BattleUI.Update` can mutate the battlefield even when the game is paused. `BattleController.Update` must still finish its staged load, including native state restore at frame 29 and final UI/loading initialization through frame 40. Preserve regiment initialization separately from combat updates.
3. Capture the initialized host battle into an immutable save copy. Validate the native scenario identifiers and installed battlefield/configuration compatibility. Extract the guest archive into a fresh isolated campaign save, then run the native saved-battle loader.
4. Compute a live initialized-state digest covering participants, hierarchy, positions/facing and formation sections, strength, equipment, ammunition and other essential starting state. Only the current native load request can attest readiness. Matching archive bytes alone cannot satisfy this gate.
5. Keep this proof paused and block battle commands, time changes and native result/quit actions until the return path is guarded too. Guest cameras can be enabled separately from mutation-bearing native UI.
6. Have the host supply the authoritative campaign checkpoint for restoration. The guest must never invoke native randomized result processing. Rebuild campaign identities and caches, verify both restored worlds, then resume campaign command acceptance after both readiness receipts.

The first two-PC acceptance scenario is entry into the same small battle, matching paused battlefield state, then safe synchronized campaign restoration. Repeat with the guest loading slowly, disconnects at each transition, repeated battles, and delayed native callbacks. No connected acceptance result has been recorded yet.

Enable the test on the host only, then choose Play Battle from a campaign engagement on either peer. The guest can also request manual play from an existing supported land battle's autoresolve panel; the host rechecks native readiness, battle identity and eligibility before launching the same shared transition. After both see the paused battlefield, use the host F9 **Return both players to pre-battle campaign** action. Active battle retreat/assault commands retain their existing host-only restrictions. Debug army setup is rejected. Different native sprite-density settings may generate different block geometry and fail readiness; matching the relevant battle graphics settings is required for this first proof. A mismatch remains held and is logged, rather than silently entering divergent worlds.

## Subsequent milestones

After the paused round trip passes in Unity, implement host-only live battle simulation and guest presentation, then guest orders, full combat/event coverage, result application and large-battle/recovery testing. Audit commands by their actual mouse/keyboard/native handler paths. Tactical formation sections and facing need explicit replication; the campaign icon position stream does not provide them.

Keep movement/state traffic compact, deliver confirmed transient events once, and measure frame time, command response, update age, bandwidth and queueing. The campaign's existing 10 Hz pose stream and 150 ms interpolation delay are starting observations rather than battle targets. Single-stream TCP can hold fast updates behind bulk transfers; decide on an additional channel only after measurement.

## Verification commands

```powershell
dotnet run --project tests/TacticalLifecycle/TacticalLifecycle.csproj -c Release
dotnet run --project tests/TacticalCheckpoint/TacticalCheckpoint.csproj -c Release
dotnet run --project tests/TacticalProtocol/TacticalProtocol.csproj -c Release
dotnet run --project tests/TacticalDrain/TacticalDrain.csproj -c Release
dotnet run --project tests/TacticalAuthority/TacticalAuthority.csproj -c Release
dotnet run --project tests/TacticalLoadPlan/TacticalLoadPlan.csproj -c Release
dotnet run --project tests/TacticalFingerprint/TacticalFingerprint.csproj -c Release
dotnet run --project tests/TacticalRuntime/TacticalRuntime.csproj -c Release
dotnet run --project tests/TacticalCommands/TacticalCommands.csproj -c Release
```

The protocol suite opens localhost TCP sockets. It needs local network access outside a sandbox that blocks loopback. The optional checkpoint test argument accepts an existing native campaign save containing `Battle/`; it reads that save and writes only disposable output under ignored `artifacts/`. The normal build includes these suites and the native prerequisite audit before staging.

## Recorded foundation verification — October 25, 2026

- Lifecycle: 71 checks passed.
- Checkpoint: 71 checks passed including byte-identical extraction of an actual native campaign/battle save. A second native save was also round-tripped during development.
- Session/transport: 44 protocol checks passed, including actual TCP. With the optional native-save argument, 47 checks passed: a 4,298,642-byte tactical archive and 4,036,500-byte campaign archive crossed the real TCP/framing/session pipeline and extracted without changing their source files.
- Native prerequisites: 21 checks passed against the pristine supported game assembly. The existing 43-schema / 775-field native suite also passed.
- The full `build.ps1` pipeline passed, including the existing 736 multiplayer checks, and staged 131 hooks/changes under ignored `artifacts/battle-foundation-verified`. Neither game installation was updated and no branch was published.

The protocol test's load callbacks extract files and simulate completion notifications; they do not initialize a Unity battlefield. No connected battlefield entry, deployment, combat, visual parity or campaign return has passed yet. The next acceptance step is the opt-in paused scene test; live spectator simulation and guest battle orders follow only after that passes.

## Recorded adapter verification — October 26, 2026

- Campaign drain/final FIFO fence and codec: 153 checks. Paused native authority bridge: 59 checks. Production runtime/session orchestration: 47 checks. Existing-battle manual request admission/dispatch: 44 checks.
- Fingerprint format: 46 checks; production native capture with graph doubles: 21 more. Native load preflight/queue orchestration: 65 checks, plus 2 passing read-only checks against an actual installed native campaign/battle save.
- Compiled native contracts: 75 checks, including hook ABI, guard reachability, readiness identity, shared reveal and no-result return. The existing 43-schema / 775-field suite passes.
- All regression suites invoked by `build.ps1` passed, including the existing 736 multiplayer checks with real localhost TCP. Patcher staging initially exposed ambiguous native type lookup and an outdated preserved-method count; both were corrected. The corrected patcher then serialized and verified the candidate in `artifacts/battle-native-verified-final`: 148 hooks/changes, 134 preserved methods, and explicit verification of every new global tactical hook target.
- The installed game/runtime assembly hashes still match the campaign baseline. Neither installation was updated; no branch was published. The local candidate is v0.40 / protocol 31.

These are automated source/native-metadata/dependency-double checks, plus real save files and network transport. They are not a recorded two-player Unity acceptance run. First acceptance must check Play Battle from each peer, slow loading, paused visual parity, camera, safe return, repeat entry and interruption/recovery before enabling live combat.
