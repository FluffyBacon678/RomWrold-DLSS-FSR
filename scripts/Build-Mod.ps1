param(
    [string]$RimWorldDir = 'E:\SteamLibrary\steamapps\common\RimWorld'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'Source\RimWorldUpscaler\RimWorldUpscaler.csproj'
& dotnet build $project --configuration Release "-p:RimWorldDir=$RimWorldDir"
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
