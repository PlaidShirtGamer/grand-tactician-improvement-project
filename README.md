# Grand Tactician Improvement Project

Work toward regimental gameplay in the main campaign with the full hierarchy:
**Regiment → Brigade → Division → Corps → Army → Army Group**.

**Current status: unfinished.** The repository contains a build-specific,
experimental patch for one fixed-depth group-command traversal, plus an audit
of the remaining hierarchy limits. It does not yet provide playable six-tier
gameplay. No game files or saves are changed by the build script.

See [the hierarchy audit](docs/hierarchy-audit.md) for confirmed findings,
remaining work, and the gameplay acceptance criteria.

## Build and stage the traversal prototype

Requires .NET SDK 8 and a legally obtained copy of the tested game build.
The first build restores Mono.Cecil 0.11.6 and .NET Standard references from NuGet.
The game assemblies are referenced locally and must not be committed.

```powershell
./build.ps1 -GameInstall 'C:\path\to\Grand Tactician' -Output './artifacts/traversal'
```

The script builds the patcher/runtime, runs isolated traversal tests, then
stages `Assembly-CSharp.dll`, `GTIP.Runtime.dll`, and a hash manifest in the
output folder. Choose a new output folder for each run. The patcher refuses
unknown game builds and output inside the game's Managed directory.

The original dispatcher is preserved inside the staged assembly. Ten internal
call sites are rerouted through the wrapper in the inspected build. DLC
scenarios and an unshifted command hierarchy call the original dispatcher.

**Staged artifacts have not been tested in Unity. Do not treat this prototype
as a ready-to-install mod.** Formation placement, recruitment, OOB layout,
AI, and save/load still require implementation and validation.

## Source layout

- `src/Patcher`: hash-guarded offline assembly patcher; game installation is read only.
- `src/Runtime`: original mod code implementing iterative hierarchy traversal.
- `tests/Traversal`: isolated behavioral tests using small game/Unity stubs.
- `docs/hierarchy-audit.md`: investigation findings and completion criteria.

Decompiled game source, proprietary assemblies, downloaded tools, and local
build artifacts are ignored. This repository distributes original mod source
only; it does not distribute the game's code or DLLs.
