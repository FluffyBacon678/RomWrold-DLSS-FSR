[CmdletBinding()]
param(
    [string] $UnityEditor,
    [switch] $SkipGpuValidation
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectPath = Join-Path $repoRoot 'UnityProject'
$scratchPath = Join-Path $repoRoot '.scratch'
$logPath = Join-Path $scratchPath 'unity-shader-build.log'
$resultPath = Join-Path $scratchPath 'shader-build-result.json'

if (-not $UnityEditor) {
    $candidates = @(
        (Join-Path $repoRoot '.tools\UnityEditor-2022.3.35f1\Editor\Unity.exe'),
        (Join-Path $repoRoot '.tools\Unity-2022.3.35f1\Editor\Unity.exe'),
        'C:\Program Files\Unity\Hub\Editor\2022.3.35f1\Editor\Unity.exe'
    )
    $UnityEditor = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (-not $UnityEditor -or -not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
    throw 'Unity 2022.3.35f1 is required. Pass -UnityEditor with the full path to its Editor\Unity.exe.'
}

New-Item -ItemType Directory -Path $scratchPath -Force | Out-Null
if (Test-Path -LiteralPath $resultPath) {
    Remove-Item -LiteralPath $resultPath
}

# Keep graphics enabled: validation executes both shader passes and reads GPU output.
$arguments = @(
    '-batchmode', '-quit', '-force-d3d11',
    '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'RimWorldUpscaler.EditorTools.BuildShaders.BuildWindows',
    '-logFile', ('"' + $logPath + '"')
)
if ($SkipGpuValidation) {
    $arguments += '-upscalerSkipGpuValidation'
}

$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -PassThru -WindowStyle Hidden
$process.WaitForExit()
$process.Refresh()
if ($process.ExitCode -ne 0) {
    throw "Unity exited with code $($process.ExitCode). See $logPath. If licensing failed, activate your eligible Unity license in Unity Hub and rerun."
}
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    throw "Unity did not produce a verified build result. See $logPath."
}
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if (-not $result.success) {
    throw "Shader build did not pass validation: $($result.error). See $logPath."
}
Write-Host "Shader bundle: $($result.bundlePath)"
Write-Host "GPU validation: $($result.gpuValidation)"
Write-Host "Build report: $resultPath"
