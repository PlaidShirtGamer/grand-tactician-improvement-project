param([string]$Tag = '', [string]$Output = (Join-Path $PSScriptRoot '../artifacts/releases'))
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content (Join-Path $root 'src/Runtime/Runtime.csproj') -Raw
$version = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
if ($Tag -and $Tag -cne "v$version") { throw "Tag $Tag must match runtime version v$version" }
foreach ($file in @('src/Runtime/MenuStatus.cs', 'src/Patcher/PatchEngine.cs')) {
    if (!(Get-Content (Join-Path $root $file) -Raw).Contains('"' + $version + '"')) { throw "Version mismatch in $file" }
}
$Output = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$name = "GTIP-v$version-build-kit"
$stage = Join-Path $Output $name
$zip = Join-Path $Output "$name.zip"
if ((Test-Path $stage) -or (Test-Path $zip)) { throw 'Package output exists; use a new output directory.' }
New-Item -ItemType Directory -Path $stage | Out-Null
# Explicit extensions exclude local build products and proprietary game assemblies.
foreach ($folder in @('src', 'tests', 'docs', 'scripts', 'licenses')) {
    Get-ChildItem (Join-Path $root $folder) -File -Recurse | Where-Object {
        $_.Extension -in @('.cs', '.csproj', '.md', '.ps1', '.manifest', '.txt') -and $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
    } | ForEach-Object {
        $relative = [IO.Path]::GetRelativePath($root, $_.FullName)
        $target = Join-Path $stage $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $target
    }
}
Copy-Item (Join-Path $root 'README.md'),(Join-Path $root 'build.ps1') -Destination $stage
New-Item -ItemType Directory -Path (Join-Path $stage 'release-config') | Out-Null
foreach ($file in @('campaignprefs.txt', 'unitprefs.txt', 'battleprefs.txt')) {
    Copy-Item -LiteralPath (Join-Path $root "release-config/$file") -Destination (Join-Path $stage 'release-config')
}
$hashes = @(Get-ChildItem $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), [IO.Path]::GetRelativePath($stage, $_.FullName).Replace('\','/')
})
$hashes | Set-Content (Join-Path $stage 'SHA256SUMS.txt') -Encoding utf8
Compress-Archive -Path $stage -DestinationPath $zip
'{0}  {1}' -f (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $zip -Leaf) | Set-Content "$zip.sha256" -Encoding utf8
Write-Output "Packaged $zip"
