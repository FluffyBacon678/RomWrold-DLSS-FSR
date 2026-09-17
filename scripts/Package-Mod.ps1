[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$modPath = Join-Path $repoRoot 'Mod'
$required = @('About\About.xml', 'LoadFolders.xml', '1.6\Assemblies\RimWorldUpscaler.dll', 'AssetBundles\rimworldupscaler_win')
foreach ($relative in $required) {
    $path = Join-Path $modPath $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing release file: $relative. Build runtime and shaders first." }
}
$unexpected = Get-ChildItem -LiteralPath (Join-Path $modPath '1.6\Assemblies') -File | Where-Object { $_.Name -ne 'RimWorldUpscaler.dll' }
if ($unexpected) { throw 'Unexpected files in Assemblies. Do not distribute game or Unity libraries.' }
[xml]$metadata = Get-Content -LiteralPath (Join-Path $modPath 'About\About.xml') -Raw
if ($metadata.ModMetaData.packageId -ne 'fluffybacon.rimworldupscaler') { throw 'Unexpected package ID; review the package script.' }
$stream = [IO.File]::OpenRead((Join-Path $modPath 'AssetBundles\rimworldupscaler_win'))
try {
    $header = New-Object byte[] 7
    if ($stream.Read($header, 0, 7) -ne 7 -or [Text.Encoding]::ASCII.GetString($header) -ne 'UnityFS') { throw 'Invalid shader bundle header.' }
} finally { $stream.Dispose() }
$licensePath = Join-Path $modPath 'Licenses'
New-Item -ItemType Directory -Path $licensePath -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'ThirdParty\FidelityFX-FSR\LICENSE.txt') -Destination (Join-Path $licensePath 'AMD-FidelityFX-FSR.txt')
Copy-Item -LiteralPath (Join-Path $repoRoot 'ThirdParty\FidelityFX-FSR\NOTICE.md') -Destination (Join-Path $licensePath 'AMD-FidelityFX-FSR-NOTICE.md')
$distPath = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Path $distPath -Force | Out-Null
$archivePath = Join-Path $distPath ('RimWorldUpscaler-preview-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip')
Compress-Archive -Path $modPath -DestinationPath $archivePath
Write-Output "Created preview package: $archivePath"
Write-Output 'Local preview only. Check VALIDATION.md before distribution.'
