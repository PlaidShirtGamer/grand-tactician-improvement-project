# In-game Aide-de-Camp integration

Branch: `feature/in-game-aide-de-camp`, forked from GTIP `test` at
`3e206bd837300f46daa3947f80fbd6c05bc9abf9`.

**Status: integration preview, not standalone feature parity.** Runtime version
0.3.1 identifies this development branch; no production release tag is implied.
The target is the complete functionality of Aide-de-Camp 0.8.20, reviewed at fork
commit `191aa6de6e0df7ca642f81ca4f45db3cf5b00e11`. The standalone app remains usable
and is not modified by this branch.

## Use the preview

Load a main campaign with GTIP's regimental scale enabled, then press **F8**.
The game pauses and a native Unity overlay opens. F8, Escape, or Close dismisses
it and leaves the campaign paused. Both factions have separate views. Search
matches names, displayed details and runtime IDs. Select a record, edit its
fields, review, then apply. Undo/redo lasts for that open editor session.

Edits affect live memory. They persist through the game's normal save system;
close the editor and use the game's **Save As** into a test slot. This preview
does not overwrite a save directly and does not provide the standalone editor's
automatic save-file backup transaction. Unapplied form drafts are discarded when
changing selection, tabs, faction, search, refreshing, or closing.

## Parity checklist

| Standalone capability | Native integration status |
| --- | --- |
| Both factions, six-level land hierarchy and Army Groups | Live indented hierarchy and search implemented |
| Garrison and fleet lists | Live command lists implemented; ship-level rows pending |
| NATO cards, connectors, collapse, nudges and drag/drop | Pending native tree UI |
| Rename combat units and HQs | Implemented with review/undo/redo |
| Unit experience, training, morale, condition | Implemented; native fractions converted to percentages |
| Strength, casualties, contracts, state, weapons, perks, supplies, ETA | Pending live accounting/side-effect adapters |
| Multi-selection, batch editing, roster copy/paste | Pending |
| Mass naming and naming schemes | Pending |
| Corps/Army/Army Group transfers | Reviewed native transfers implemented; graphical drag/drop and structural undo pending |
| Create combat units and HQs at verified towns | HQs from Brigade through Army Group implemented; combat-unit creation pending |
| Delete formations while reparenting subordinates | Reviewed native deletion implemented for stationary land formations; structural undo pending |
| Commander replacement, rank and branch editing | Eligible commander replacement implemented; editing existing ranks/branches pending |
| Officer names, attributes and background traits | Implemented with review/undo/redo |
| Officer creation, random names, dates and portraits | Loaded-campaign land officer creation, faction name pools, dates and existing Avatar PNGs implemented; portrait import/preview pending |
| Add officers to base game versus loaded save | Pending; requires separate explicit file transaction |
| Weapon stock | Live stock editing implemented; equipment/order/standardization handling pending |
| Treasury, population, recruits, support, projects, policies and subsidies | Read-only overview; editing pending |
| Ship condition, construction, repair and assignment | Pending |
| Validation and jump to affected record | Field validation implemented; campaign-wide review/jump pending |
| Save browser and save backup/conflict checks | Game's own save interface currently used; parity pending |
| UI settings and error reports | Responsive overlay and Unity log reporting; preferences pending |

Items marked pending must not be advertised as working or exposed as unchecked
raw-field writes. Feature parity requires the remaining adapters and game tests.

## Formation operations in 0.3.1

Use **Create HQ** for Brigades, Divisions, Corps, Armies and Army Groups. A new
independent HQ spawns at a selected owned town; subordinate HQs spawn at their
parent. Army Groups start empty and acquire their map position from attached
commands. Select a formation for **Transfer / regroup**, **Replace commander**,
or **Delete formation**. Review identifies the destination and affected children.

Deleting an HQ reparents its immediate children to its parent. Deleting a root HQ
promotes its HQ children to independent commands and retains its Army Group
membership. A root HQ with direct combat children must first be attached under
another HQ. Deleting an Army Group preserves all attached commands. Empty Army
Groups are retained when their last command transfers out, preserving their
commander and identity. Combat-unit deletion uses native disbanding accounting.

Structural actions require stationary land formations outside battles, retreats,
existing transfers, garrisons, detached companies and active AI operations. Native
transfers may introduce a transfer delay. These operations clear field undo/redo
history and **cannot currently be undone**; save a separate checkpoint beforehand.
If a native operation fails after starting, reload that checkpoint. Creation and
replacement list only available officers who meet rank and branch requirements;
Army Groups additionally retain the game's experience requirement.

**New officer** adds an unassigned land officer to the loaded campaign. Random
names come from the faction's game name pools. Enter real dates as YYYY-MM-DD;
select an existing portrait using its filename in `Avatar`, without `.png`.
A blank portrait uses the silhouette. Starting attributes can be edited afterward
in Officers. This does not yet add officers to base-game files or import images.

## Implementation

`CampaignEditor` is a persistent Unity MonoBehaviour bootstrapped by the existing
menu hook. The patcher preserves and wraps BattleUI.Update, Camera_Battlefield.Update,
and AICampaign.Update. Native methods run unchanged when the overlay is closed.
The editor blocks the UI, map camera and campaign AI update while open, disables
the current Unity EventSystem input modules temporarily, and suppresses input through the frame
after closing to prevent click-through orders. Input module states are restored on
close/disable/destruction. Campaign clock, commander-list and unit-folder checks
invalidate the session on scene/load/time transitions. Battles and W&L scenarios
are outside this preview's scope.

`CampaignEditorData` reads Unity objects directly. It does not parse or rewrite
the live save behind the game's back. Regiment experience/training and commander
attributes are native fractions; converting save-editor values without this
adapter would multiply them by 100. Army Groups are separate native objects and
are represented above their attached commands.

`LiveEditSession` validates a whole transaction before writing, restores values
if a setter/refresh fails, keeps undo/redo, and refuses stale undo. Formatting an
unchanged field must not round its native value. Renamed commanders refresh their
combined name and command cache; edited formations refresh group summaries and
the sidebar. Unity/native APIs are used only on the main thread. There is no local
HTTP server, exposed remote command interface, or external WPF process.

## Remaining implementation sequence

1. Finish native unit/commander accounting adapters and multi-selection.
2. Add structural undo and combat-unit creation. Extend validation of
   AI ownership lists, Army Group membership, commander ownership, campaign paths,
   recruitment pools, equipment stocks, and save/reload for each operation.
3. Finish officer portrait importing/preview and separate base-game writes with backups.
4. Port ships, state/nation economics, projects, subsidies and policies.
5. Complete graphical tree interactions, naming/clipboard, validation navigation,
   save backup workflow and persistent UI settings.
6. Run the parity checklist on copied campaigns, including return to menu, reload,
   battle transitions, resolutions and long-running campaign behavior.

The 0.3.1 structural adapter suite covers Corps-to-Army transfer, rejection of
cycles/cross-faction/battle/AI-operation changes, retained empty Army Groups,
HQ child preservation on deletion, native combat disbanding delegation, commander
eligibility, and assignment during HQ creation. It uses native-operation doubles;
it cannot establish correctness of the game's own movement or serialization.

The standalone WPF/.NET 8 window cannot load in the game's Unity runtime. The
integration therefore ports behavior through native adapters rather than hosting
the standalone executable. Aide-de-Camp is MIT-licensed; its attribution is included
in `licenses/Aide-de-Camp.txt`. No game binaries, assets, saves, or decompiled source
are included in Git.

## Checks

`tests/LiveEditor` checks percent conversion, validation, exact preservation of
unchanged values, undo/redo, native-change conflicts, session invalidation, and
rollback after setter/refresh failures. Existing GTIP suites remain required.
`tests/EditorData` checks six-level nesting, faction/search filtering, descendant
strength totals, native training mapping, rank labels, and distinct weapon-stock
selection using synthetic game objects. Both suites run in release CI.
`build.ps1` compiles against the supported original game and verifies all patch
hooks after serialization. These checks alone do not establish UI or save parity.

The 0.3.0 preview was installed into the local test installation and smoke-tested
on a loaded campaign at 1920x1080: F8 opening/closing, corrected HQ strength totals,
unit name review/apply/undo, officer attribute inspection, and weapon-stock listing.
The campaign remained paused and the temporary rename was undone without saving.
The corrected build produced no exceptions in Player.log during this smoke test.
Save/reload persistence, battle transitions, and full standalone parity have not
yet been validated.

The 0.3.1 build passed all seven local suites (116 checks), compiled against the
supported original assembly, and passed serialized patch verification. It was
installed and tested at 1920x1080 in a loaded campaign: the randomizer generated
a new Major General, the officer appeared as available and unassigned, and Army
creation at an owned town assigned him correctly. The user saved and reloaded
that campaign and reported the result looked fine. The inspected Player.log
contained no exception/error matches. Live deletion, all transfer combinations,
portrait persistence, and battle transitions still need scenario testing; the
automated structural tests use native-operation doubles rather than Unity saves.
