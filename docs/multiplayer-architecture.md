# Native multiplayer architecture and acceptance gates

Target: host-authoritative campaign and standalone-battle co-op/versus using native maps, UI and visuals. This is the target, not the current supported feature set. Work stays on `mp-test`; base GTIP releases remain separate.

## Boundaries

1. Host executes simulation and validates all remote intents. Native host UI still executes directly today; routing both players through a shared command boundary remains work.
2. Guest applies replicated state on Unity's main thread. Networking threads never touch Unity objects.
3. Presentation updates run independently of simulation: cameras, panels, selection, models, animation, dispatch display. Do not re-enable whole simulation-bearing Update methods to repair rendering.
4. Join/reconnect loads an isolated native checkpoint. Normal object creation/deletion uses lifecycle adapters, not a reload.
5. Revisioned transactions carry base revision, changed records, explicit removals and target checksum. A guest reconstructs and validates the complete registered state before application. Host commits its baseline only after application acknowledgement.
6. Scalar readback checks registered native fields after application. A mismatch reports revision, record kind, identity and field. This does not verify omitted objects, visuals or reference graphs.

## Current adapters and gaps

| Domain | Current implementation | Remaining requirements |
|---|---|---|
| Clock | Host clock/pause stream | Long-running pacing and panel behavior tests |
| Formations | Monotonic session IDs, lifecycle, parent graph, Army Groups, native status/retreat/battle flags and paths; absolute pose readback | Complete reference audit, perks, detached objects, rendering and long-running tests |
| Map visibility | Host fog result and remembered sighting records; guest presentation only | Live same-area comparisons across zoom/FOW changes, hidden-information design for versus |
| Officers | Attributes, dates, command references; command identity precondition | Stable IDs beyond native indices, creation/deletion edge cases |
| Economy/world | Alliance/state/resource/chart fields; selected building/Town/IIP data, building lifecycle, railroad progress, weapon orders | Full nested industry/transport/queues, native derived caches and command coverage audit |
| Recruitment | Native guest recruiting dialog and host-validated unit creation/cancellation; ship/fleet lifecycle | Full queue/return-cost coverage and live recruiting/shipyard tests |
| Dispatches | Delivered records, formation references, native notifications; local read/remove/view state | Non-formation references, session archive retention bounds, guest interaction tests |
| Guest commands | Movement, hierarchy/recruitment, policies/projects/finances, commanders, construction/railroads, army orders, ships/weapons | Remaining native actions and derived effects; live validation for every command boundary |
| Engagements | Native host prompt text/choices/rows, reinforcement selection, first accepted co-op decision | Live simultaneous-input tests; tactical transition still ends session; versus rules |
| Versus | Disabled | Per-side simulation control, filtered initial checkpoint and state, hidden-information policy |
| Tactical battles | Disabled; session ends at transition | Entity/weapon/projectile state, order routing, visibility, resolution, campaign handoff |

Do not enable versus using the current co-op checkpoint: it contains both factions' data. Do not interpret successful packet delivery or equal formation counts as full replication.

## Acceptance gates

Campaign co-op must pass a repeatable two-client scenario: join from main menu; pause/resume and change speed; move units from each client; create/delete/reparent HQs and combat units; replace/create officers; recruit/cancel/reinforce; spend/produce/construct; receive dispatches; view each native panel; zoom between map modes; disconnect/rejoin. Check authoritative values and native presentation after every operation. Record observed results, not just absence of exceptions.

Before versus: repeat the scenario with separate factions and prove the opponent's hidden state is neither presented nor disclosed by the replication stream/checkpoint. Define ownership disputes and battle participation.

Before battles: build standalone scenario co-op first, then opposing sides. Verify movement, targeting, firing, casualties, morale, routing, reinforcements and outcomes. Campaign battle transitions follow only after standalone battles are stable.

## Recovery and performance

Current revision mismatch/application failure ends the session; reconnect obtains a fresh checkpoint. Targeted resynchronization without reload remains unimplemented. Native application is validated first but not rollback-atomic; an exception must never leave the guest independently playable.

Current delta generation compares full captures; checksums compress canonical full state. This reduces transmitted records, not necessarily CPU cost. Profile capture, diff, serialization, bytes, native application and frame time before increasing update rates. Position interpolation and separate priority streams remain work.
