# Hierarchy audit of the supplied test install

Status: **v0.2.2 experimental six-tier implementation; gameplay acceptance pending**.

The target is Regiment → Brigade → Division → Corps → Army → Army Group
in the main campaign, without activating Whiskey & Lemons career mechanics.

## Verified baseline

- Unity Mono managed assembly, `Assembly-CSharp.dll`, SHA-256
  `AE15E3E479F4D79B70E40CED42EB93A953569B8185E075B3873C7410462AE8C4`.
- `Config/campaignprefs.txt` already specifies `commandhierarchyshift = -1`.
- `Config/unitprefs.txt` already caps infantry at 1,000 and cavalry at 600.
- There are 19 unit types. Types 14, 15, and 16 are land HQs; type 17 is
  a **fleet**. Type 18 is already involved in army-group naming and ranks.
  Simply incrementing 16 to 17 creates a naval type, not a new land HQ.
- Visible names and symbols are shifted independently of physical nesting.
  Setting the hierarchy shift does not create a sixth tier.
- Army groups are separate `ArmyGroup` objects referencing regiments;
  they are not simply another `Regiment` parent in the transform tree.

## Findings and required changes

| Area | Evidence from local inspection | Required implementation |
| --- | --- | --- |
| Group commands | `BattleUnits.PerformActionForGroupForAllUnitsOnLevel(Regiment, ...)` visits root and three descendant levels | Unbounded traversal preserving filters and order; implemented in prototype |
| Formation placement | The hierarchy-tree preparation inside `BattleUnits` uses four explicitly nested levels | Postorder recursive formation layout; validate movement and rotations at every tier |
| OOB | `OOB.UpdateOOB` constructs icons and positions through three child levels; `GetUnitDimensions` assumes specific types | Recursive layout, icons, connectors, selection, drag/drop, and recruit buttons |
| HQ recruitment | `CampaignMainPanel.CheckArmyStateSelection` raises types 15/16; `RecruitGroup` decrements the parent ID | Explicit land-tier mapping that skips fleet, with new army HQ creation |
| Campaign visibility | `BattleUnits.GetUnitVisibilityStateOnCampaignMap(Regiment)` explicitly handles 14–17 and otherwise returns zero | Ensure new army HQs appear and participate in movement/combat/supply |
| Unit symbols | `Regiment.GetUnitSymbolData` maps land HQs through type 16 | Add Army symbols independently of fleet handling |
| Commander effects | `Regiment.CalculateFightingSpirit` indexes coefficient rows using `unittyp - 13` | Explicit mapping or expanded coefficient arrays; prevent out-of-bounds indexes |
| Unit names | `Regiment.UpdateUnitNameDefault` has special Army naming for type 16 | Preserve appropriate corps naming and assign Army naming at the new tier |
| AI | `AICampaign` has explicit type-16 checks for recruitment and grouping | Teach AI the full mapping and new HQ; preserve naval behavior |
| Army groups | `ArmyGroup.CreateNewArmyGroup` uses override level 18 and special naming under shift -1 | Keep Army Group above the new Army and verify bonuses, command assignment, and UI |
| Save/load | `ImportExportUnitData` stores hierarchy shift and unit records; army-group references are separate | Round-trip a complete six-tier tree, commander references, and fleet controls |
| Initial campaign OOBs | Existing scenarios already have their own hierarchy and sizes | Add a deliberate scenario conversion strategy; smaller recruitment caps alone do not split existing units |

## Implementation boundary in 0.2.2

The new runtime uses land HQ IDs 14 (Brigade), 15 (Division), 16 (Corps), and
18 (Army), with combat units below Brigade and native ArmyGroup objects above
Army. Fleet ID 17 is preserved. The findings above describe the original game;
recruitment, OOB, formation, visibility, symbols, naming, array capacity,
save-type restoration, and AI integration now have experimental patches.

All replacements retain the original method and delegate outside the shifted
main-campaign mode. Ordinary Corps formations and OOBs retain native behavior;
Army-rooted trees use the new traversal. Recruitment skips Fleet. Army Group
project/commander requirements remain native. The importer restores type 18
from its persisted override marker, including for empty Armies. Historical
starting forces are not automatically split or restructured.

The patcher checks the original assembly hash and exact hook signatures, expands
short IL branches affected by insertions, then verifies all 18 preserved-method
wrappers after serialization. It also rejects native call sites bypassing those
wrappers. Nineteen hierarchy checks and eight formation checks use stubs; they
cover the real ID chain, fleet preservation, empty Army import mapping, movement
to deepest branches, and restoration of detached units after a layout failure.
They do not establish successful Unity execution or actual save-file round trips.

Version 0.1.2's menu placement was confirmed by the user. Version 0.2.2 retains
that placement and updates the version text. The earlier successful campaign
and battle smoke tests predate the new Army tier and do not validate this build.
See [testing-six-level.md](testing-six-level.md) for the acceptance procedure.

## Acceptance criteria for a playable release

1. Start a new main campaign without career mode; create and manage every tier.
2. Verify regiment sizes for infantry/cavalry and battery behavior; deliberately
   convert initial forces rather than only changing recruitment caps.
3. Attach/detach units at each tier, appoint commanders, and inspect the whole OOB.
4. Move an Army and Army Group on the campaign map; verify orders, supply,
   readiness, engagements, and reinforcement arrival.
5. Fight a battle: deployment, formation changes, rotation, pathfinding,
   attack, retreat, and all descendants receiving orders exactly once.
6. Observe AI recruiting and maintaining six-tier forces over multiple months.
7. Save, restart the game, reload, and compare hierarchy and commander assignments.
8. Verify fleets and existing DLC scenarios; no naval type may be repurposed.
9. Decide and implement a migration policy before loading existing saves.

Until these pass, there is no supported installation for six-tier gameplay.

## 0.2.2 regression fixes

The reported commander-panel failure was traced to an out-of-range rank index:
the native dummy-commander constructor assigns rank 10 to type 18, and
BattleUI.UpdateCommanderPanel indexes rankabbrev with that value. The resulting
exception interrupts UI updates, leaving commander names and unit details stale.
The runtime repairs invalid Army commander ranks on creation/loading and before
panel rendering, without replacing commander IDs or changing valid ranks.

The upgrade tooltip now distinguishes Division-to-Corps from Corps-to-Army.
Army OOBs stack combat units under Brigades and draw orthogonal connectors in
the actual icon coordinate system. The campaign sidebar visits parents before
children, indents by depth, and refreshes after Army insertion.

Validation: 33 stub checks pass (19 hierarchy/rank, 8 formation, 6 insertion),
and 18 preserved-method wrappers pass serialized assembly verification. Unity
layout and campaign movement still require in-game testing. The interrupted UI
update is a likely cause of blocked map input; movement is not yet confirmed.

## 0.2.3 campaign Army movement

Native Regiment.Initialize marks every visible campaign unit permanentlydetached,
including a top-level Army. The custom Army formation handler incorrectly applied
its subordinate detachment filter to the order's root as well, skipping both
RegimentSetPath and immediate placement for that Army. The order UI could still
start its delay even though no pending movement path had been assigned.

The root now receives its path/placement just as it does in native SetGroupFormation.
Detached or routed subordinate units retain their native exclusions. This does
not bypass order delays, force movement speed, or change independent Corps orders.
Three regression checks cover a detached campaign Army receiving a pending path,
preserved Corps detachment, and immediate placement. Total: 36 stub checks
(19 hierarchy/rank, 11 formation, 6 insertion). Live movement remains to be retested.

## 0.2.4 Army commander replacement

The native CheckSuitableRank method treats type 18 as Army Group eligibility.
Besides rank bounds, it requires minimumexpcommanderarmygroup when an officer is
at the minimum rank. The added Army HQ shares that ID, so suitable-rank MGs can
be excluded by the Army Group experience check. For an actual type-18 Regiment
in shifted main-campaign mode, the hook supplies zero for that experience
threshold. Checks without an Army Regiment (including Army Group selection),
other modes, minimum/maximum ranks, availability, branch, and state restrictions
retain native behavior. Union Config/ranks0.txt maps the shifted Army to type 16,
which requires rank 7 (Major General) both before and after organization reform;
Brigadier Generals are below that threshold.

39 stub checks pass (22 hierarchy/rank, 11 formation, 6 insertion); serialization
also verifies the new eligibility hook. This identifies a code-level exclusion,
not the complete eligibility state of the specific officers in the user's save.
