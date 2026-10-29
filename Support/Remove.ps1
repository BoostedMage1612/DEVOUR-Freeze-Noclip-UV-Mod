#requires -Version 5.1
[CmdletBinding()]
param([string]$GamePath, [switch]$NonInteractive)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    $game = Select-Game
    if (@(Get-Process -Name DEVOUR -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Close DEVOUR before removing the mod.' }
    $marker = Join-Path $game 'DEVOURFreeze\installation.json'
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) { throw 'No installation record found. No files were removed.' }
    $record = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ($record.Version -ne '0.4.0') { throw 'Use the remover that came with the installed version.' }
    $thirdParty = @()
    foreach ($folder in @('Mods', 'Plugins')) {
        $dir = Join-Path $game $folder
        if (Test-Path -LiteralPath $dir -PathType Container) {
            $thirdParty += @(Get-ChildItem -LiteralPath $dir -Filter '*.dll' -File -Recurse | Where-Object { $_.Name -ne 'DevourMonsterFreeze.dll' })
        }
    }
    $preserved = 0
    foreach ($f in $record.Files) {
        $p = Join-Path $game $f.Path
        if (-not (Is-Within $p $game)) { throw 'Invalid installation record path.' }
        if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { continue }
        if ($thirdParty.Count -gt 0 -and $f.Path -notlike 'DEVOURFreeze/*' -and $f.Path -ne 'Mods/DevourMonsterFreeze.dll') { continue }
        if (-not (Is-RegularPath $p) -or (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $f.SHA256) {
            Write-Host ('Preserved changed file: ' + $f.Path); $preserved++; continue
        }
        Remove-Item -LiteralPath $p -Force
    }
    if ($preserved -eq 0) { Remove-Item -LiteralPath $marker -Force }
    if ($thirdParty.Count -gt 0) { Write-Host 'Freeze mod removed. The shared loader was kept for your other mods.' -ForegroundColor Green }
    elseif ($preserved -eq 0) { Write-Host 'Freeze mod and its loader entry point removed. Launch DEVOUR normally.' -ForegroundColor Green }
    else { Write-Host 'Some changed files were preserved. Export a report before making further changes.' -ForegroundColor Yellow }
    Write-Host 'Generated loader caches and reports may remain; they do not load without the loader entry point.'
    if ($preserved -gt 0) { exit 2 }
    exit 0
} catch { Write-Host ('REMOVE ERROR: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }
