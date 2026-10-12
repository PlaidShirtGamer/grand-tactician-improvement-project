# In-game Aide-de-Camp integration

Branch: `feature/in-game-aide-de-camp`, forked from GTIP `test` at
`3e206bd837300f46daa3947f80fbd6c05bc9abf9`.

**Status: integration preview, not standalone feature parity.** Runtime version
0.3.0 identifies this development branch; no production release tag is implied.
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
| Corps/Army/Army Group transfers | Pending native move validation and reversible bookkeeping |
| Create combat units and HQs at verified towns | Pending |
| Delete formations while reparenting subordinates | Pending; native DeleteUnit destroys descendants unless moved first |
| Commander replacement, rank and branch editing | Pending rank/experience eligibility adapter |
| Officer names, attributes and background traits | Implemented with review/undo/redo |
| Officer creation, random names, dates and portraits | Pending |
| Add officers to base game versus loaded save | Pending; requires separate explicit file transaction |
| Weapon stock | Live stock editing implemented; equipment/order/standardization handling pending |
| Treasury, population, recruits, support, projects, policies and subsidies | Read-only overview; editing pending |
| Ship condition, construction, repair and assignment | Pending |
| Validation and jump to affected record | Field validation implemented; campaign-wide review/jump pending |
| Save browser and save backup/conflict checks | Game's own save interface currently used; parity pending |
| UI settings and error reports | Responsive overlay and Unity log reporting; preferences pending |

Items marked pending must not be advertised as working or exposed as unchecked
raw-field writes. Feature parity requires the remaining adapters and game tests.

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
2. Add reversible hierarchy transfers, HQ/unit creation and deletion. Validate
   AI ownership lists, Army Group membership, commander ownership, campaign paths,
   recruitment pools, equipment stocks, and save/reload for each operation.
3. Port officer creation/dates/portraits and separate base-game writes with backups.
4. Port ships, state/nation economics, projects, subsidies and policies.
5. Complete graphical tree interactions, naming/clipboard, validation navigation,
   save backup workflow and persistent UI settings.
6. Run the parity checklist on copied campaigns, including return to menu, reload,
   battle transitions, resolutions and long-running campaign behavior.

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
