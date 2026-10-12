# Release packages

The Package release workflow runs regression checks and compiles the patcher on
GitHub-hosted Windows runners. It packages original source, build scripts, docs,
tests, and the three preferences copied from the test installation's Config folder.
Game DLLs, decompiled source, saves, local tools, and backups are excluded.

## Windows installer

Download **GTIP-v0.2.4-Setup.exe**, run it,
select the game installation folder, and click **Install / Update**. For protected folders, use **Run as administrator** if Windows denies access. No SDK,
.NET installation, or network connection is needed on the player's machine.
The installer contains GTIP source, Roslyn, .NET, the patcher, and all three
preference files. It compiles against the selected game's own DLLs and verifies
the patch before replacing files. No proprietary game assembly is distributed.

The installer refuses unsupported game versions and running games. It backs up
both DLLs (where present) and all three preferences in `.gtip-installer` inside
the game folder. Updates retain the original baseline. Run the same installer
and choose **Uninstall / Restore** to restore that baseline; backups are retained.
If files were edited afterward, the installer refuses to overwrite those edits.
A journal rolls back a failed copy and recovers an interrupted operation on the
next run. Saves are never modified; use a compatible pre-mod save after removal.

For a previous manual GTIP installation, use the optional pristine assembly
field to select its original Assembly-CSharp.dll backup. Uninstall then restores
that manual installation, which was the baseline when this installer first ran.

The current EXE is unsigned. Windows may show an unknown-publisher/SmartScreen
prompt. The artifact includes a SHA-256 checksum. Third-party license notices
are embedded and available through the installer's **Third-party notices** button.

The hosted runner builds and smoke-tests the self-contained Windows x64 EXE,
and exercises installer transactions with synthetic fixtures. Real-game compile,
patch, update, and restore integration tests are run locally on a disposable copy.
The GUI's interactive folder-picking and elevated desktop flow still require a
manual smoke test. Build it locally with `./scripts/package-installer.ps1`.
## Source build kit

The ZIP is a build kit, not a ready-to-copy DLL release. Install .NET SDK 8,
extract the ZIP, and run PowerShell from the extracted folder:

```powershell
./build.ps1 -GameInstall 'C:\Games\Grand Tactician' -Output './artifacts/local-build'
```

If the game is already patched, also pass `-OriginalAssembly` pointing to its
pristine Assembly-CSharp.dll backup. The patcher validates the supported hash.
Close the game and back up Assembly-CSharp.dll and GTIP.Runtime.dll (if present)
from `The Civil War (1861-1865)_Data/Managed`, plus the three existing preferences
from `Config`. Copy both staged DLLs into Managed, and copy the three files in
`release-config` into the game's `Config` folder. These are complete preference
files from the test install, so they replace any existing customizations.
Scenario-specific preference overrides can supersede the global Config files.
Use a test save and follow [gameplay checks](testing-six-level.md).

The archive contains a per-file SHA256SUMS.txt; a separate .zip.sha256 verifies
the download. Building and packaging alone do not modify an installation.

## Maintainer workflow

Merge the release workflow and intended mod changes before tagging. Keep the
Runtime.csproj, MenuStatus.cs, and Patcher/PatchEngine.cs version strings in sync.
Push a tag matching the version exactly, for example `v0.2.4`. A successful tag
run creates a draft prerelease with the installer EXE, source ZIP, and checksums attached. Review and
publish the draft in GitHub Releases. Existing releases are never overwritten.

PR runs and Actions → Package release → Run workflow produce downloadable
artifacts without creating a release. Manual dispatch becomes available after
the workflow is on the default branch. No personal access token or game DLL
upload is required; publishing uses the workflow's scoped GITHUB_TOKEN.

For local packaging, run `./scripts/package-release.ps1`. Use a new `-Output`
directory for each attempt. To refresh release preferences, copy the three
files from the test install into `release-config` and commit them before tagging.
