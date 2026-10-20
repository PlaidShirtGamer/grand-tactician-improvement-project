# Native interaction coverage and acceptance

The host-authoritative design remains appropriate: a guest submits intents and displays host state rather than running a second simulation. The recurring v0.33 failures exposed insufficient coverage at the interfaces between that design and the native game. Field equality cannot prove that a native tooltip has valid ancestry, that an input handler searches the right UI collection, or that a brief event survives between captures.

Keep three contracts explicit:

1. **Authoritative state:** capture/apply/reference completeness, exact readback, revision/checksum receipt and native operation eligibility. Failures here end the guest session; display-error isolation must not hide them.
2. **Transient events:** accepted native producer, reliable delivery, stable identity, bounded once-only receipts, expiry and session cleanup. Events must not depend on polling long enough to observe them.
3. **Local presentation/input:** actual native owner/control, pointer/focus delivery, graph preconditions, original callback, and cleanup. Exercise the production bridge with realistic contracts and inspect the native producer/consumer metadata. Follow with a connected game comparison.

## Coverage added for v0.34

| Interaction | Native contract and implementation | Automated evidence | Connected evidence still required |
|---|---|---|---|
| Recruiting uniforms and commander preview | Native tooltip walks to the literal Units ancestor; dummy remains outside the real roster except within one synchronous tooltip getter, restored in finally | Reproduced invalid ancestry; unit/commander redraws; failure restoration; repeated native-style descendant scans; no aggregate update on hierarchy-only getters | Cycle uniforms and colors while another unit/officer is created; recruit and compare appearance |
| Roster redraw during lifecycle | All references apply first; display errors restore registered scalars, then strict readback runs; host recruiting refresh unchanged | Injected redraw exception, successful subsequent revision, real divergence still detected; compiled call-order/finally checks | Keep recruitment/fleet panels open across create/delete/reparent operations |
| Battle/map notices | All successful RollingText.AddNew calls publish immediately; native Add suppression stays upstream; bounded receipts cover fast delivery and delayed state | Enumerated 16 original producer methods; receipt reorder/expiry/reset tests; native creation/display ABI | Compare combat, flanking, routed and construction notices with matched local popup settings |
| Left dispatch notifications | Native notifications belong to PanelHandler.symbol; identify message, icon, sender and reference before InteractScreenMessage | Native producer/consumer collection audit and symbol identity cases | Left-open/right-dismiss before fade; confirm log reopening and local read state |
| Campaign rotation lock | Consume the native lock at update_scrolls, after keyboard/mouse/compass input, clearing angular momentum and pending yaw target | Production wrapper tests and native field/call audit; unlocked/tactical path preserved | With lock on, try Q/E, edge rotation and compass; verify north, pan and zoom on both clients |
| Motion presentation | 10 Hz host poses, 150 ms timestamp buffer; cached visual roots and per-frame pose; logical objects remain authoritative | Production rendering harness; bounded history/wire/generation tests; compiled display-only boundary; pacing telemetry | Same camera/speed/unit on both clients; pause, reverse, recruit and vary compression; compare pacing and apply-phase logs |

These checks cover the named boundaries, not every native control. The connected column is deliberately pending until observed in Unity. Record failures by contract and source method, then fix the shared boundary and add a regression for that contract rather than adding another button-name exception.

## Follow-up coverage for v0.35

The v0.34 connected report confirms recruiting uniform selection works, while existing-unit appearance, rapid state changes, repeat recruitment and a later position check exposed additional contracts:

| Contract | Added coverage | Remaining connected comparison |
|---|---|---|
| Military management is broader than active map units | Inactive owned regiment appearance; native eligible uniforms; palette feedback with matching/stale host selection; authority restored after guest render | Recolor/cycle attached regiments with both host palette states; caption visibility still needs visual confirmation |
| Commander statistics need native ancestry too | `UnitIsInsideRadius` parent/influence lookup within the preview commander context; unit-pane normalization; parent/officer/selection restoration; null-selection fallback cannot retain the attached preview | Rapid state/type switches, including different unassigned officers; inspect Player.log as well as MP logs |
| A recruiting click is an intent awaiting a result | Pending draft retained, duplicate click suppression, matching result only, rejected draft retained, visible refusal, supported off-map-state eligibility | Repeated recruitment and legitimate pool/credit failures with choices retained |
| One writer owns logical poses | Production legacy-view adapter tested against the observed 1973/493 case and a newer same-roster view followed by scalar-only state; map visibility/metadata retained | Recruited-unit movement/pauses across multiple revisions; failed coordinates are now preserved |

## Repeatable connected scenario

Use the same paused save and matching local display settings. Join, exercise the six rows above, then leave the recruiting UI open while the campaign creates an officer and unit and resolves a battle. Compare state and visible behavior immediately and after one update. Repeat at low and high compression, switch paper/3D maps, disconnect and reconnect. Preserve both logs, including the first exception stack and the new motion/apply-phase diagnostics. A release passes this scenario only when those observations are recorded; synthetic tests do not substitute for it.
