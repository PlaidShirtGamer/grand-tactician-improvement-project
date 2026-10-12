param([string]$GameInstall = (Split-Path -Parent $PSScriptRoot), [string]$Output = (Join-Path $PSScriptRoot 'artifacts/traversal'), [string]$OriginalAssembly = '')
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameInstall 'The Civil War (1861-1865)_Data/Managed'
if (!$OriginalAssembly) { $OriginalAssembly = Join-Path $managed 'Assembly-CSharp.dll' }
$OriginalAssembly = (Resolve-Path -LiteralPath $OriginalAssembly).Path
dotnet build (Join-Path $PSScriptRoot 'src/Patcher/Patcher.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Patcher build failed' }
dotnet build (Join-Path $PSScriptRoot 'src/Runtime/Runtime.csproj') -c Release "-p:GameManagedDir=$managed" "-p:OriginalAssembly=$OriginalAssembly" -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Runtime build failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/Traversal/Traversal.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Traversal tests failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/Formation/Formation.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Formation tests failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/ArmyInsertion/ArmyInsertion.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Army insertion tests failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/AiBehavior/AiBehavior.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'AI behavior tests failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/LiveEditor/LiveEditor.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Live editor tests failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/EditorData/EditorData.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Editor data tests failed' }
$patcherDir = Join-Path $PSScriptRoot 'src/Patcher/bin/Release/net8.0'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/Runtime/bin/Release/netstandard2.0/GTIP.Runtime.dll') -Destination $patcherDir
dotnet (Join-Path $patcherDir 'Patcher.dll') stage $GameInstall $Output $OriginalAssembly
if ($LASTEXITCODE -ne 0) { throw 'Staging failed' }
