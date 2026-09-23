# Publishes VibesboxKiosk as a self-contained, Native-AOT win-x64 build.
# Output: <repo>/publish/
#
# Usage:
#   .\build\publish.ps1              # clean publish
#   .\build\publish.ps1 -SkipClean   # incremental (faster, for iteration)
#   .\build\publish.ps1 -Version 1.2.0

param(
    [switch]$SkipClean,
    [string]$Version = '0.0.0'
)

$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$project    = Join-Path $repoRoot 'src\VibesboxKiosk\VibesboxKiosk.csproj'
$publishDir = Join-Path $repoRoot 'publish'

if (-not $SkipClean -and (Test-Path $publishDir)) {
    Write-Host "Cleaning $publishDir ..."
    Remove-Item $publishDir -Recurse -Force
}

Write-Host "Publishing VibesboxKiosk (Release, win-x64, AOT, self-contained) ..."
dotnet publish $project -c Release -r win-x64 --self-contained -o $publishDir -p:Version=$Version

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Publish complete: $publishDir\VibesboxKiosk.exe"
