param([string]$Output = (Join-Path $PSScriptRoot '../artifacts/releases'))
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$Output = [IO.Path]::GetFullPath($Output)
[xml]$project = Get-Content (Join-Path $root 'src/Runtime/Runtime.csproj') -Raw
$version = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
$publish = Join-Path $root ('artifacts/installer-' + [guid]::NewGuid().ToString('N'))
dotnet publish (Join-Path $root 'src/Installer/Installer.csproj') -c Release -o $publish -p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed' }
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$exe = Join-Path $Output "GTIP-v$version-Setup.exe"
if (Test-Path $exe) { throw 'Installer output exists; use a new output directory.' }
Copy-Item (Join-Path $publish 'GTIP-Setup.exe') $exe
'{0}  {1}' -f (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $exe -Leaf) | Set-Content "$exe.sha256" -Encoding utf8
Write-Output "Packaged $exe"
