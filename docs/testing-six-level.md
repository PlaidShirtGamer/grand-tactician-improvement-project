# Testing GTIP 0.2.1

## Existing Corps attached directly to an Army Group

Version 0.2.0 missed the upgrade and group-assignment paths. In 0.2.1, select
an existing top-level Corps (including one already assigned to an Army Group)
and use the Corps-to-Army upgrade control. This inserts a new Army HQ above
the Corps, retaining the Corps, its commander and subordinates, and its Army
Group membership through the new Army. In-battle and garrison upgrades are
not offered. A Division uses the original upgrade to Corps first.

Assigning a standalone Corps to an Army Group also inserts the missing Army
HQ. Old saves are not silently reorganized during loading. For the reported
XX Corps example, the expected result after upgrading is Army Group → new
Army HQ → XX Corps → Divisions → Brigades → Regiments.

## Full acceptance test

This build implements the extra Army HQ. It still requires an in-game acceptance
test; successfully loading an older save alone does not exercise the new tier.

1. Launch the test installation and confirm **GTIP v0.2.1** on the main menu.
2. Start or load a disposable main-campaign save with hierarchy shift -1,
   outside the Whiskey & Lemons career scenario. Save under a new name.
3. Use **Raise Army** and select a state. It should create an Army, not a Corps.
   Use its group-recruitment control to create a Corps, then a Division under
   that Corps, then a Brigade under that Division. Recruit an infantry regiment
   under the Brigade. The group tooltip should name the correct next tier,
   and the Army OOB should display all five nested unit levels.
4. Use the Army's existing Army Group control to create or join an Army Group.
   The campaign must have the normal Army Group project unlock and an eligible
   commander. The group is managed by the native Army Group UI, not a sixth
   Regiment icon in the Army OOB. Confirm the Army remains subordinate to it.
5. Add a second regiment and, if practical, a second Corps. Select and appoint
   commanders at each tier; drag units between appropriate parents. Confirm
   names, unit counts, strengths, and symbols remain consistent. Also confirm
   Raise Corps and a naval Fleet still create their respective types.
6. Save, return to the main menu, reload, and inspect the same chain. An Army
   must remain an Army, with the same commander, children, and Army Group.
   Include an empty Army in this check, since depth inference differs for it.
7. Enter a battle involving that force. Inspect the tactical OOB. Test deployment,
   movement, rotation, line/column changes, queued orders, and detached units.
   Orders from the Army should reach all eligible regiments; independently
   detached campaign forces retain normal detachment rules. Check both branches
   of the tree, not just the first regiment.
8. Complete the battle, return to the campaign, save and reload again. Verify
   surviving units, commander references, hierarchy, Army Group membership,
   and strength totals. Advance campaign time to observe AI-created forces.

Keep the test-save name, campaign date/scenario, and the exact first failing
action when reporting a problem. `[GTIP] Army HQ created/loaded` log entries
help distinguish the new tier from old, renamed units. A menu label by itself
only establishes runtime loading.

Known validation gaps: historical starting OOB conversion, long-running AI and
supply behavior, formation spacing on real terrain, and full save/battle round
trips. The automated checks cover type mapping, dispatch, formation traversal,
fallbacks, cleanup on failure, and assembly wiring; they do not run the game.
