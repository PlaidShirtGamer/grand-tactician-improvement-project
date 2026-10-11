# Release packages

The Package release workflow runs regression checks and compiles the patcher on
GitHub-hosted Windows runners. It packages original source, build scripts, docs,
tests, and the three preferences copied from the test installation's Config folder.
Game DLLs, decompiled source, saves, local tools, and backups are excluded.

## Download and install

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
Runtime.csproj, MenuStatus.cs, and Patcher/Program.cs version strings in sync.
Push a tag matching the version exactly, for example `v0.2.4`. A successful tag
run creates a draft prerelease with the ZIP and checksum attached. Review and
publish the draft in GitHub Releases. Existing releases are never overwritten.

PR runs and Actions → Package release → Run workflow produce downloadable
artifacts without creating a release. Manual dispatch becomes available after
the workflow is on the default branch. No personal access token or game DLL
upload is required; publishing uses the workflow's scoped GITHUB_TOKEN.

For local packaging, run `./scripts/package-release.ps1`. Use a new `-Output`
directory for each attempt. To refresh release preferences, copy the three
files from the test install into `release-config` and commit them before tagging.
