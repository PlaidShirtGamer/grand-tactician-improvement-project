# Native multiplayer architecture and acceptance gates

Target: host-authoritative campaign and standalone-battle co-op/versus using native maps, UI and visuals. This is the target, not the current supported feature set. Work stays on `mp-test`; base GTIP releases remain separate.

## Boundaries

1. Host executes simulation and validates all remote intents. Native host UI still executes directly today; routing both players through a shared command boundary remains work.
2. Guest applies replicated state on Unity's main thread. Networking threads never touch Unity objects.
3. Presentation updates run independently of simulation: cameras, panels, selection, models, animation, dispatch display. Do not re-enable whole simulation-bearing Update methods to repair rendering. Native `GetUnitTooltipInfo` calls `UpdateGroupStrength`, which advances subordinate morale/readiness/condition; replicas suppress that aggregate routine and receive its outputs from the host. Recruiting previews remain within their existing save/restore transaction.
4. Join/reconnect loads an isolated native checkpoint. Normal object creation/deletion uses lifecycle adapters, not a reload.
5. Revisioned transactions carry base revision, changed records, explicit removals and target checksum. A guest reconstructs and validates the complete registered state before application. Host commits its baseline only after application acknowledgement.
6. Scalar readback checks registered native fields after application. A mismatch reports revision, record kind, identity and field. This does not verify omitted objects, visuals or reference graphs.
7. Short-lived notices are events, not sampled durable state. Publish at the accepted native creation boundary and deduplicate fast delivery against later snapshots. Local read/dismiss state is separate from host history.
8. Local input/presentation needs its own native contract: owning UI collection, eligibility, object ancestry, native callback and cleanup. Structural adapters finish native references before refreshing roster panels. Redraw failures restore authoritative scalars and are reported separately; restoration or readback failures remain fatal.

The [native interaction coverage and connected acceptance matrix](multiplayer-coverage.md) records these boundaries and their evidence. A protocol/model assertion count is not a measure of complete gameplay coverage.

## Current adapters and gaps

| Domain | Current implementation | Remaining requirements |
|---|---|---|
| Clock | Host clock/pause stream | Long-running pacing and panel behavior tests |
| Formations | Monotonic session IDs, lifecycle, parent graph, Army Groups, native status/retreat/battle flags, paths and destination markers; absolute pose readback; timestamped 10 Hz visual pose stream | Complete reference audit, perks, detached objects, rendering and long-running tests |
| Map visibility | Host fog result and remembered sighting records; guest presentation only | Live same-area comparisons across zoom/FOW changes, hidden-information design for versus |
| Officers | Attributes, dates, command references; command identity precondition | Stable IDs beyond native indices, creation/deletion edge cases |
| Economy/world | Alliance/state/resource/chart fields; building output/demand, produced goods, sales ranking and five histories; selected Town/IIP data, building lifecycle, railroad progress, weapon orders | Full nested industry/transport/queues, native derived caches and command coverage audit |
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

Delta generation still compares full captures. Cached canonical row encodings now feed SHA-256 directly, with typed array comparisons avoiding per-element boxing; checksum bytes and commit validation remain unchanged. Full-state capture/application remains a material cost. Logs report capture, prepare, receipt and application timing; managed synthetic benchmarks are not live Unity measurements.

A 10 Hz presentation-only pose stream runs independently of full transactions. The host sends only changed/recently moving visible formations matching the exact guest-acknowledged lifecycle. The receiver uses host real-time timestamps, eight poses and a 150 ms jitter buffer, holding the last sample if starved. Reliable transactions carry capture timestamps and cannot rewind newer visual samples. No local pathfinding, prediction or logical-transform writes are added. Rendered-symbol picking uses temporary offsets restored in a finally block. The stream shares bounded reliable TCP queues and yields when the outgoing queue is busy; it does not provide an independent low-latency transport or guarantee a latency bound. Connected high-compression pacing and end-to-end performance comparisons remain required.
