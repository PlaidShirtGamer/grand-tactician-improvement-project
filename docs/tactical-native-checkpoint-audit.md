# Native tactical checkpoint audit

The first tactical milestone uses the native **saved campaign battle** path. Generated `Battlefields/999/A` files are initial battle inputs; they do not represent a complete initialized battlefield. No game scene assets or assemblies belong in a multiplayer checkpoint.

## Host capture

The supported native `ImportExportUnitData.SaveBattle(false, name)` writes only while `SceneManagement.currentoperation == 3` for a campaign battle. Its destination is:

```text
Campaigns/<SceneManagement.currentcampaignscenario>/<Saving.latestfolder>/Battle
```

The parent campaign save is the pre-battle campaign created by `SceneManagement.PrepareLaunchingBattleFromCampaign`. Preserve it alongside the tactical save: the battle loader reads the parent nations/version data, and the host needs that campaign when applying the result. Capture the initialized battlefield while the tactical authority remains paused; a valid archive is not proof that loading or rendering it is synchronized.

`SaveBattle` writes scenario, objectives, entry points, pontoons, buildings, obstacles, artillery, additional unit paths, detailed tactical units, fronts, side information, fallen soldiers, version, the native unit roster, and commanders. `CreateUnitFiles` also writes army-group references; the army-group table itself is conditional on the allow-army-groups option. The non-DLC allowlist is explicit in `MpTacticalCheckpoint`.

The archive contains the same allowlisted root files as `MpCheckpoint`, plus exactly one `Battle/` directory containing these native data files:

```text
armygrouprefs.dat armygroups.dat artillery.dat attributes.dat battledata.dat
battleunits.dat buildings.dat commanders.txt entrypoints.dat fallenlayers.dat
frontlines.dat groups.dat objectives.dat obstacles.dat paths.dat pontoons.dat
regiments.dat scenario.dat sideinfo.dat version.dat
```

Only `Battle/armygroups.dat` is optional within the current supported battle-save set. Empty files such as `frontlines.dat` are valid native outputs. Files retain their original bytes; root and battle commander/attribute tables remain separate. DLC saves are outside this first milestone.

## Guest load

The existing native recipe is `Saving.LoadSave`'s campaign-battle load branch. Extract into a fresh isolated save under the agreed campaign scenario, then:

1. Set `Saving.latestfolder` to the new folder and `SceneManagement.currentoperation = 8`.
2. Call `ImportExportUnitData.LoadScenarioFile(<fresh folder>/Battle/, true)` and restore the agreed player alliance.
3. Queue black screen, scene rebuild, loading screen, `bo_loadcampaignbattlefromsave`, and black-screen hide using the native ordering.
4. Pass the saved battlefield scene ID, `A`, and the campaign scenario to `bo_loadcampaignbattlefromsave`. The native handler sets `battlefromcampaign`, `battlesubfromcampaign`, and `currentcampaignscenario` before loading `BattlefieldBase` with operation 8.

The tactical `scenario.dat` stores campaign scenario at zero-based line 12 and battlefield scene at zero-based line 13. Real inspected saves include campaign `001/A` and battlefield `212`; `999/A` is not the scene ID to load. These identifiers still need protocol-side syntax and installed-content validation.

`BattleUnits.InitBattleUnits` loads roster and battle data from the isolated `Battle` directory and overwrites nations from its parent. `BattleController.Update` performs staged initialization. At frame 29 it calls `ImportExportUnitData.LoadBattle`; at frame 30 operation 8 becomes operation 3. `LoadBattle` restores references, obstacles, artillery, side information, unit details, frontlines, fallen layers, and battle commanders. Both peers must finish this process and compare a live-state digest before the readiness barrier can be satisfied. Archive checksum equality alone establishes transfer integrity.

## Returning to campaign

Only the host may call native result processing. `SceneManagement.ExitCampaignBattleAndRebuildCampaign` calls `BattleUnits.SaveBattleResult`, saves changed units/officers, and reloads the campaign. Result calculation contains randomness. The guest must receive the host's completed campaign state instead of calculating its own result. A separate battle generation must prevent delayed tactical packets or orders from reaching the new campaign objects.

## Archive guarantees and limits

`MpTacticalCheckpoint` uses a versioned data-only GZip archive with at most 47 allowlisted paths, 64 MiB compressed and total file-content limits, bounded UTF-8 names, case-insensitive duplicate detection, SHA-256 transfer verification, and required native state files. It refuses nested/unknown paths, executables, DLC files, links/junctions, and existing extraction targets. It validates the complete payload before writing, stages beside the destination, then renames the fresh directory into place. A failed or invalid transfer cannot overwrite an existing save.

The codec does not interpret native numeric data, validate gameplay references, or prove the guest has matching terrain/configuration assets. Those are separate live bootstrap and state verification contracts. Native metadata inspection and byte-for-byte archive tests do not establish connected Unity loading or visual parity.

The minimum required campaign-root set matches the existing campaign checkpoint (`scenario.dat`, `groups.dat`, `regiments.dat`, `commanders.txt`, `nations.dat`, `AICampaign.dat`, and `version.dat`). Other allowlisted root files are preserved when present. This is not a native-load completeness guarantee: the scene adapter must preflight every file and semantic dependency required by the selected native load path before starting it.

Read-only pack/extract verification is available against existing native saves:

```powershell
dotnet run --project tests/TacticalCheckpoint/TacticalCheckpoint.csproj -c Release -- 'C:\path\to\Campaigns\001\A\SaveExample'
```

The optional native-save run reads the source and writes disposable test output under ignored `artifacts/`; it does not load a game, mutate that save, or modify either game installation.
