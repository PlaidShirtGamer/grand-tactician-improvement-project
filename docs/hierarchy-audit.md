# Hierarchy audit of the supplied test install

Status: **research and traversal prototype; full six-tier mod unfinished**.

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

## Prototype boundary

The prototype changes **only** the group-command dispatcher. It retains the
original method and delegates to it for DLC scenarios and campaigns with a
hierarchy shift other than -1. The replacement uses a visited set and a stack,
so it sends once per recipient and has no fixed depth limit.

Filtering retains the original distinction between the root recipient and
descendants, along with level, active-state, detachment, routed/retreating, and
AI-group filters. Descendants are visited in the original order. This does not
enable recruitment of new tiers and does not change scenario data.

The staged DLL is structurally verified after writing. Stub tests exercise
six levels, cycle protection, duplicate references, recipient filters, and
fallback behavior. The user reported successful test-save loading and a battle
with the initial prototype. This is a smoke test, not complete gameplay or
save/load validation. Version 0.1.1 adds a main-menu runtime-loaded indicator;
its hook is structurally verified but its appearance still needs in-game confirmation.

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
