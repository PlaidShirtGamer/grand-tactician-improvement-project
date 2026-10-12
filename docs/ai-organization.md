# AI organization diagnostics and Army integration (0.2.5)

## Enable logging

Create `Config/GTIP-ai-logging.txt` in the selected game installation containing:

```text
enabled=true
```

Restart the game, load a main campaign using GTIP's regimental scale, and advance
time. The next observed AI organization routine creates a CSV in `GTIP-Logs`
beside the game executable. Logging is disabled if the file is absent or the line
is `enabled=false`. Changes to this switch require a game restart. No debug.txt
changes are needed. The test installation has logging explicitly enabled.

Files have unique UTC timestamps and session identifiers. Returning to the main
menu closes the current file; the next campaign starts a new file. Previous files
are retained. Each session stops logging after approximately 100 MiB (checked
after each batch); restart to resume. Filesystem errors disable diagnostics and
emit one `[GTIP] AI logging stopped` warning to the Unity log. Originals still run
once and original game exceptions still propagate.

Snapshots occur on the first observed operation and on the first operation after
the campaign month/day changes. They are not a timer and do not run while paused.
The snapshot includes both sides for comparison. `ai_faction_index` identifies
the AI routine being observed, while each row's `faction` identifies its unit.
`campaign_date` is the game's native floating date, not an ISO calendar date.

Recruitment (`Recruit`), HQ consolidation (`Combine`), combat-unit consolidation
(`CombineCombat`), transfers (`Transfer`), and Army Group management (`ManageGroups`)
record changed rows as `before`/`after` or `created`/`removed`. Match them by event
and instance ID. No-op operations produce no event rows. IDs identify runtime
objects within that session, not persistent save-record IDs. Columns distinguish
direct infantry, cavalry, artillery, other combat units, Brigades, Divisions,
Corps, and Armies; child IDs capture swaps even when the counts are unchanged.
Army Group ID/name records membership on formations; Army Groups themselves are
separate native objects rather than Regiment rows.

Native HQ types: Brigade 14, Division 15, Corps 16, Army 18; 17 is a Fleet.
Combat-unit types 0/1/2 are infantry/cavalry/artillery. HQ strength uses the game's
cached active strength. Logs describe observed results, not internal rejection
reasons or every action outside those five routines. Consult `scenelog.dat` for
the game's own explanations. Names may be arbitrary user text; import CSV as text
when examining it in a spreadsheet.

## Army AI changes

The logging layer does not change decisions. Separately, this version repairs
and extends the existing GTIP Army integration:

- AI-created Army HQs use `ArmyInsertion`, the same insertion routine as player
  upgrades, including independent Corps movement and hierarchy/list refreshes.
- Newly raised independent Corps first seek a nearby available Army with fewer
  than five direct Corps. Forced-new requests retain separate Army creation.
- After native Division-to-Corps consolidation, one eligible independent Corps
  may join an existing Army, or two independent Corps may form a new Army. This
  pass requires the native Grand Army organization project.
- Reorganization is confined to AI-controlled main-campaign factions in GTIP
  regimental mode. Candidates must share faction and Army Group assignment,
  satisfy the native relocation distance and theater checks, and be independent.
- The entire candidate formation is checked for combat, retreat, transfers,
  movement, embarkation, garrisons, raids, invasions, fort construction, capital
  defense, depot construction, and offensive/defensive operation assignments.

Existing Armies are not dismantled, their Corps are not redistributed, and a
singleton Army from an older build is not automatically merged away. Armies can
still have only one Corps when no eligible neighbor exists. Five is a maximum
for these new attachment decisions, not a retroactive size repair. The native
lower-tier recruitment limits and minimum sizes are unchanged. This is not a
complete rewrite of the campaign AI.

## Validation

`tests/AiBehavior` covers Army reuse/cap, excluded assignments and factions,
two-Corps consolidation, Army Group preservation, failed HQ creation, CSV
snapshots/diffs, session rollover, disabled logging, native exceptions and file
errors using game stubs. `build.ps1` additionally compiles against the supported
pristine game assembly and verifies patch hook signatures and serialization.
Actual movement, save/reload, performance and long-running AI balance still need
campaign testing. Run a test save for several in-game weeks, retain its baseline,
and compare snapshots; record the date and formation name for unexpected cases.
