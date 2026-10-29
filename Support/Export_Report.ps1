#requires -Version 5.1
[CmdletBinding()]
param([string]$GamePath, [switch]$NonInteractive, [switch]$NoOpen, [string]$OutputDirectory)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
try {
    $game = Select-Game
    $script:GameRoot = $game
    if ($OutputDirectory) { $out = Full-Path $OutputDirectory }
    else {
        $package = Split-Path -Parent $PSScriptRoot
        if (Test-Path -LiteralPath (Join-Path $package 'Payload\Mods\DevourMonsterFreeze.dll') -PathType Leaf) {
            $out = Join-Path $package 'Reports'
        } else {
            $recordPath = Join-Path $game 'DEVOURFreeze\installation.json'
            if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) { throw 'Run INSTALL.cmd from the extracted package to set its Reports folder.' }
            $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
            if (-not $record.PSObject.Properties['PackageRoot'] -or -not (Test-Path -LiteralPath $record.PackageRoot -PathType Container)) {
                throw 'The extracted package folder moved. Run INSTALL.cmd from its new location, or use EXPORT_REPORT.cmd there.'
            }
            $out = Join-Path (Full-Path $record.PackageRoot) 'Reports'
        }
    }
    $stamp = (Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
    $stage = Join-Path $out ('DEVOUR_Freeze_' + $stamp)
    [void][IO.Directory]::CreateDirectory($stage)
    $notes = New-Object 'System.Collections.Generic.List[string]'
    $logs = @('DEVOURFreeze/freeze.log', 'DEVOURFreeze/freeze.log.previous', 'DEVOURFreeze/status.json', 'DEVOURFreeze/installation.json', 'MelonLoader/Latest.log', 'MelonLoader/Latest-Bootstrap.log')
    $recentDir = Join-Path $game 'MelonLoader\Logs'
    if (Test-Path -LiteralPath $recentDir -PathType Container) {
        foreach ($f in @(Get-ChildItem -LiteralPath $recentDir -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 2)) {
            $logs += Relative-Path $f.FullName $game
        }
    }
    foreach ($rel in @($logs | Select-Object -Unique)) {
        $p = Join-Path $game $rel
        if (Test-Path -LiteralPath $p -PathType Leaf) {
            try {
                if (-not (Is-RegularPath $p)) { throw 'Linked file skipped.' }
                Write-TextFile (Join-Path $stage $rel) (Protect-Text (Read-SharedText $p))
            } catch { [void]$notes.Add((Protect-Text ($rel + ': ' + $_.Exception.Message))) }
        }
    }
    $n = 0
    foreach ($folder in @(Get-LogFolders)) {
        $n++
        foreach ($name in @('Player.log', 'Player-prev.log')) {
            $p = Join-Path $folder $name
            if (Test-Path -LiteralPath $p -PathType Leaf) {
                try { Write-TextFile (Join-Path $stage ('Unity_' + $n + '/' + $name)) (Protect-Text (Read-SharedText $p)) }
                catch { [void]$notes.Add((Protect-Text $_.Exception.Message)) }
            }
        }
    }
    $ids = @()
    foreach ($rel in @('GameAssembly.dll', 'DEVOUR_Data/il2cpp_data/Metadata/global-metadata.dat', 'version.dll', 'Mods/DevourMonsterFreeze.dll')) {
        $p = Join-Path $game $rel
        if (Test-Path -LiteralPath $p -PathType Leaf) { $ids += [pscustomobject]@{ Path = $rel; SHA256 = (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash; Bytes = (Get-Item -LiteralPath $p).Length } }
    }
    $info = [ordered]@{ ExporterVersion = '0.4.0'; CreatedUtc = [DateTime]::UtcNow.ToString('o'); Fingerprints = $ids; Notes = @($notes.ToArray()); Running = (@(Get-Process -Name DEVOUR -ErrorAction SilentlyContinue).Count -gt 0) }
    Write-TextFile (Join-Path $stage 'report.json') (ConvertTo-Json -InputObject $info -Depth 8)
    Write-TextFile (Join-Path $stage 'READ_ME.txt') "DEVOUR Monster Freeze test report. Upload this ZIP in our chat.`r`nContains mod status, logs, and build fingerprints. No game binaries or saves included.`r`nCommon user paths, Steam IDs, IPv4 addresses, and auth lines are redacted in text.`r`nLogs can still contain player names/session details.`r`n"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = Join-Path $out ('UPLOAD_THIS_DEVOUR_Freeze_' + $stamp + '.zip')
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip + '.partial', [IO.Compression.CompressionLevel]::Optimal, $false)
    $check = [IO.Compression.ZipFile]::OpenRead($zip + '.partial')
    try { if ($null -eq $check.GetEntry('report.json')) { throw 'Report ZIP verification failed.' } } finally { $check.Dispose() }
    Move-Item -LiteralPath ($zip + '.partial') -Destination $zip
    # Keep one report ZIP per export; staging text is already inside the verified ZIP.
    Remove-Item -LiteralPath $stage -Recurse -Force
    Write-Host ('REPORT READY: ' + $zip) -ForegroundColor Green
    if (-not $NoOpen -and [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        Start-Process explorer.exe -ArgumentList ('/select,"' + $zip + '"')
    }
    exit 0
} catch { Write-Host ('REPORT ERROR: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }
