param([string]$GameInstall = (Split-Path -Parent $PSScriptRoot), [string]$Output = (Join-Path $PSScriptRoot 'artifacts/traversal'))
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameInstall 'The Civil War (1861-1865)_Data/Managed'
dotnet build (Join-Path $PSScriptRoot 'src/Patcher/Patcher.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Patcher build failed' }
dotnet build (Join-Path $PSScriptRoot 'src/Runtime/Runtime.csproj') -c Release "-p:GameManagedDir=$managed" -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Runtime build failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/Traversal/Traversal.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Traversal tests failed' }
$patcherDir = Join-Path $PSScriptRoot 'src/Patcher/bin/Release/net8.0'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/Runtime/bin/Release/netstandard2.0/GTIP.Runtime.dll') -Destination $patcherDir
dotnet (Join-Path $patcherDir 'Patcher.dll') stage $GameInstall $Output
if ($LASTEXITCODE -ne 0) { throw 'Staging failed' }
