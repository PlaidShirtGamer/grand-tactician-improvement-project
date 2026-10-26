param([string]$GameInstall = (Split-Path -Parent $PSScriptRoot), [string]$Output = (Join-Path $PSScriptRoot 'artifacts/traversal'), [string]$OriginalAssembly = '')
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameInstall 'The Civil War (1861-1865)_Data/Managed'
if (!$OriginalAssembly) { $OriginalAssembly = Join-Path $managed 'Assembly-CSharp.dll' }
$OriginalAssembly = (Resolve-Path -LiteralPath $OriginalAssembly).Path

foreach ($suite in @('TacticalLifecycle', 'TacticalCheckpoint', 'TacticalProtocol', 'TacticalDrain', 'TacticalAuthority', 'TacticalLoadPlan', 'TacticalFingerprint', 'TacticalRuntime', 'TacticalCommands')) {
    dotnet run --project (Join-Path $PSScriptRoot "tests/$suite/$suite.csproj") -c Release -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw "$suite checks failed" }
}
dotnet build (Join-Path $PSScriptRoot 'src/Patcher/Patcher.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Patcher build failed' }
dotnet build (Join-Path $PSScriptRoot 'src/Runtime/Runtime.csproj') -c Release "-p:GameManagedDir=$managed" "-p:OriginalAssembly=$OriginalAssembly" -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Runtime build failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/NativeSchemas/NativeSchemas.csproj') -c Release -p:UseSharedCompilation=false -- (Join-Path $PSScriptRoot 'src/Runtime/bin/Release/netstandard2.0/GTIP.Runtime.dll') $OriginalAssembly $managed
if ($LASTEXITCODE -ne 0) { throw 'Native schema ABI checks failed' }

dotnet run --project (Join-Path $PSScriptRoot 'tests/NativeBridge/NativeBridge.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Native bridge and management rule checks failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/ReplicaBoundary/ReplicaBoundary.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Replica simulation boundary checks failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/ReplicaCursor/ReplicaCursor.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Guest cursor recovery checks failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/RecruitPreview/RecruitPreview.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Guest recruitment preview checks failed' }

dotnet run --project (Join-Path $PSScriptRoot 'tests/CameraRotation/CameraRotation.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Campaign camera rotation lock checks failed' }

dotnet run --project (Join-Path $PSScriptRoot 'tests/MotionPresentation/MotionPresentation.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Guest motion presentation checks failed' }

dotnet run --project (Join-Path $PSScriptRoot 'tests/Appearance/Appearance.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Guest existing-unit appearance checks failed' }

dotnet run --project (Join-Path $PSScriptRoot 'tests/CommanderPortraits/CommanderPortraits.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Guest commander portrait checks failed' }

dotnet run --project (Join-Path $PSScriptRoot 'tests/OobLabels/OobLabels.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Guest OOB commander label checks failed' }

dotnet run --project (Join-Path $PSScriptRoot 'tests/NewspaperPresentation/NewspaperPresentation.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Guest newspaper presentation checks failed' }

dotnet run --project (Join-Path $PSScriptRoot 'tests/SnapshotPresentation/SnapshotPresentation.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Snapshot pose ownership checks failed' }
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
dotnet run --project (Join-Path $PSScriptRoot 'tests/EditorFormation/EditorFormation.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Editor formation tests failed' }
$patcherDir = Join-Path $PSScriptRoot 'src/Patcher/bin/Release/net8.0'

dotnet run --project (Join-Path $PSScriptRoot 'tests/Multiplayer/Multiplayer.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Multiplayer tests failed' }
dotnet run --project (Join-Path $PSScriptRoot 'tests/EditorPresentation/EditorPresentation.csproj') -c Release -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Editor presentation tests failed' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/Runtime/bin/Release/netstandard2.0/GTIP.Runtime.dll') -Destination $patcherDir
dotnet (Join-Path $patcherDir 'Patcher.dll') stage $GameInstall $Output $OriginalAssembly
if ($LASTEXITCODE -ne 0) { throw 'Staging failed' }
