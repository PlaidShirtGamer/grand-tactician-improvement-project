> **mp-test branch: GTIP MP Test v0.27.** Multiplayer versioning is independent of base GTIP.
> Guests join from the main menu and receive a host checkpoint. Experimental campaign adapters cover formations, sightings, construction, ships, weapons and native engagement choices, with guest actions validated on the host.
> See [multiplayer test instructions](docs/multiplayer-test.md). Complete state replication and versus remain unfinished.

> Development branch: native in-game Aide-de-Camp integration preview (GTIP 0.3.8).
> Press F8 in a loaded regimental main campaign. Full standalone feature parity is
> still in progress: see [coverage and implementation checklist](docs/in-game-aide-de-camp.md).

# Grand Tactician Improvement Project

Experimental support for **Regiment → Brigade → Division → Corps → Army → Army Group**
in the main campaign, without enabling Whiskey & Lemons career mechanics.

Version **0.2.4** implements the additional Army HQ using the existing type-18
data slot. Fleet remains type 17; Army Groups remain native, separate ArmyGroup
objects above Army HQs. This is a test build, not a validated campaign release.

## What the patch changes

- Raise Army creates a type-18 Army; Raise Corps creates a type-16 Corps.
- Upgrading an existing Corps inserts an Army above it, preserving the Corps
  and any Army Group membership. Assigning a standalone Corps to an Army Group
  also inserts the missing Army HQ. These paths were missing in 0.2.0.
- Group recruitment follows Army → Corps → Division → Brigade, skipping Fleet.
- Army OOBs use recursive layout and the game's existing selection, recruitment,
  drag/drop, and Army Group controls.
- Group orders traverse the entire tree. Army formation measurement and placement
  also traverse the extra depth, retaining native offset calculations and paths.
- Army HQs receive campaign visibility, Army symbols/names, safe statistics arrays,
  and fighting-spirit coefficients inherited from the previous highest HQ tier.
- Save import recognizes the persisted Army symbol instead of mistaking a
  four-HQ hierarchy for a Fleet. No save format is added.
- AI recruitment can wrap newly raised standalone Corps in Army HQs; its
  fighting-force lookup recognizes the new HQ. Long-term AI behavior remains unverified.
- Newly created Army Groups receive distinct Army Group names. Existing project
  and commander requirements for Army Groups remain in effect.

The menu displays **GTIP v0.2.4**. That confirms the runtime is loaded; it does
not prove the current save contains a complete six-tier chain. Hooks activate
when the command hierarchy shift is -1 and the DLC career scenario is inactive.
Other scenarios retain original implementations. Existing saves and historical
OOBs are not automatically reorganized or split into historical regiments.

See [in-game testing](docs/testing-six-level.md) and [the technical audit](docs/hierarchy-audit.md).

## Build and stage

Requires .NET SDK 8 and the supported game build. NuGet restores Mono.Cecil
0.11.6 and .NET Standard references; proprietary game assemblies stay local.

```powershell
./build.ps1 -GameInstall 'C:\path\to\Grand Tactician' -Output './artifacts/six-level-0.2.4'
```

For an already patched installation, supply the pristine original backup:

```powershell
./build.ps1 -GameInstall 'C:\path\to\Grand Tactician' -Output './artifacts/six-level-0.2.4' -OriginalAssembly 'C:\path\to\backup\Assembly-CSharp.dll'
```

Choose a new output folder each time. The script builds, runs 22 hierarchy and
11 formation and 6 Army insertion checks using stubs, and stages two DLLs plus a SHA-256 manifest.
The patcher rejects unknown original assemblies and verifies serialized hooks.
These checks cannot validate Unity UI, terrain/pathfinding, campaign AI, or an
actual campaign-to-battle-to-save round trip. Building does not modify the install.

To install, close the game, back up both existing DLLs, then copy the staged
`Assembly-CSharp.dll` and `GTIP.Runtime.dll` into
`The Civil War (1861-1865)_Data/Managed`. Keep both DLLs from the same build.
Use a separate test save. Restoring the old DLLs also requires returning to a
save made before the new Army HQs were created.

## Source layout

- `src/Patcher`: hash-guarded offline Mono.Cecil patcher.
- `src/Runtime`: hierarchy traversal, recruitment, OOB, formation, and integration hooks.
- `tests/Traversal`, `tests/Formation`: isolated behavioral checks with game/Unity stubs.
- `docs`: implementation boundary and gameplay acceptance steps.

Decompiled source, proprietary assemblies, tools, backups, and build artifacts
are ignored. This repository distributes original mod source only.

## Releases

See [release packaging and installation](docs/releases.md). Version tags create a draft prerelease containing a self-contained Windows installer, a source build-kit ZIP, and checksums. The installer bundles the three test-install preferences and compiles GTIP against the player's game DLLs automatically; no SDK is required.


AI diagnostics and experimental Army organization improvements in 0.2.5: [setup, behavior, and testing](docs/ai-organization.md).
