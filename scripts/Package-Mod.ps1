[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$modPath = Join-Path $repoRoot 'Mod'
$required = @('About\About.xml', 'About\Preview.png', 'About\ModIcon.png', 'LoadFolders.xml', '1.6\Assemblies\RimWorldUpscaler.dll', 'AssetBundles\rimworldupscaler_win')
foreach ($relative in $required) {
    $path = Join-Path $modPath $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing release file: $relative. Build runtime and shaders first." }
}
$unexpected = Get-ChildItem -LiteralPath (Join-Path $modPath '1.6\Assemblies') -File | Where-Object { $_.Name -ne 'RimWorldUpscaler.dll' }
if ($unexpected) { throw 'Unexpected files in Assemblies. Do not distribute game or Unity libraries.' }
[xml]$metadata = Get-Content -LiteralPath (Join-Path $modPath 'About\About.xml') -Raw
if ($metadata.ModMetaData.packageId -ne 'fluffybacon.rimworldupscaler') { throw 'Unexpected package ID; review the package script.' }
$runtimePath = Join-Path $modPath '1.6\Assemblies\RimWorldUpscaler.dll'
$runtimeVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($runtimePath).FileVersion
if (-not $runtimeVersion.StartsWith([string]$metadata.ModMetaData.modVersion + '.')) { throw "Runtime version $runtimeVersion does not match metadata version $($metadata.ModMetaData.modVersion)." }
$bundlePath = Join-Path $modPath 'AssetBundles\rimworldupscaler_win'
$stream = [IO.File]::OpenRead($bundlePath)
try {
    $header = New-Object byte[] 7
    if ($stream.Read($header, 0, 7) -ne 7 -or [Text.Encoding]::ASCII.GetString($header) -ne 'UnityFS') { throw 'Invalid shader bundle header.' }
} finally { $stream.Dispose() }
$resultPath = Join-Path $repoRoot '.scratch\shader-build-result.json'
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw 'Missing shader build result. Run Build-Shaders.ps1 before packaging.' }
$buildResult = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (-not $buildResult.success) { throw 'The most recent shader build did not pass.' }
if ($buildResult.editorVersion -ne '2022.3.35f1') { throw "Shader bundle was built with Unity $($buildResult.editorVersion), not 2022.3.35f1." }
if ($buildResult.gpuValidation -notmatch 'Windows player bundle reload passed') { throw 'Shader build did not pass the Windows player-bundle reload check.' }
$bundleHash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash
if ([string]::IsNullOrWhiteSpace($buildResult.bundleSha256) -or $bundleHash -ne $buildResult.bundleSha256) { throw 'The shipped shader bundle does not match the last verified build.' }
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
