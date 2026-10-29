#requires -Version 5.1
[CmdletBinding()]
param([string]$GamePath, [switch]$NonInteractive)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$package = Split-Path -Parent $PSScriptRoot
$created = New-Object 'System.Collections.Generic.List[object]'
$createdDirs = New-Object 'System.Collections.Generic.List[string]'
$staging = $null
$marker = $null
function Update-ExistingInstallation($prior, $expected) {
    if ($prior.Version -notin @('0.1.0', '0.2.0', '0.2.1', '0.2.2', '0.3.0', '0.3.1', '0.4.0')) { throw 'This installer upgrades our v0.1.0 through v0.4.0 package. Export a report for the other version.' }
    if (-not (Is-RegularPath $marker)) { throw 'The installation record is a linked file.' }
    $known = $expected
    if ($prior.Version -eq '0.1.0') {
        $known = Get-Content -LiteralPath (Join-Path $package 'Payload\previous_v0.1.0_hashes.json') -Raw | ConvertFrom-Json
    }
    if ($prior.Version -eq '0.2.0') {
        $known = Get-Content -LiteralPath (Join-Path $package 'Payload\previous_v0.2.0_hashes.json') -Raw | ConvertFrom-Json
    }
    if ($prior.Version -eq '0.2.1') {
        $known = Get-Content -LiteralPath (Join-Path $package 'Payload\previous_v0.2.1_hashes.json') -Raw | ConvertFrom-Json
    }
    if ($prior.Version -eq '0.2.2') {
        $known = Get-Content -LiteralPath (Join-Path $package 'Payload\previous_v0.2.2_hashes.json') -Raw | ConvertFrom-Json
    }
    if ($prior.Version -eq '0.3.1') {
        $known = Get-Content -LiteralPath (Join-Path $package 'Payload\previous_v0.3.1_hashes.json') -Raw | ConvertFrom-Json
    }
    if ($prior.Version -eq '0.3.0') {
        $known = Get-Content -LiteralPath (Join-Path $package 'Payload\previous_v0.3.0_hashes.json') -Raw | ConvertFrom-Json
    }
    foreach ($loader in @('version.dll', 'MelonLoader/net6/MelonLoader.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $game $loader) -PathType Leaf)) { throw 'The existing loader is incomplete. Export a report before updating.' }
    }
    $updates = @()
    foreach ($f in $expected) {
        $rel = if ($f.Path.StartsWith('Payload/')) { $f.Path.Substring(8) } else { 'DEVOURFreeze/' + $f.Path }
        $p = Join-Path $game $rel
        $old = @($prior.Files | Where-Object { $_.Path -eq $rel })
        $knownOld = @($known | Where-Object { $_.Path -eq $f.Path })
        if ($old.Count -ne 1 -or $knownOld.Count -ne 1 -or $old[0].SHA256 -ne $knownOld[0].SHA256 -or
            -not (Is-RegularPath $p) -or (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $old[0].SHA256) {
            throw ('Our installed file has changed or is incomplete: ' + $rel + '. Export a report before updating.')
        }
        $updates += [pscustomobject]@{ Path = $rel; Target = $p; Source = (Join-Path $package $f.Path); SHA256 = $f.SHA256 }
    }
    $backup = Join-Path ([IO.Path]::GetTempPath()) ('DEVOURFreeze_upgrade_' + [Guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($backup)
    $changed = $false
    $recovered = $true
    $pending = $marker + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    $swapBackup = $pending + '.previous'
    try {
        [IO.File]::Copy($marker, (Join-Path $backup 'installation.json'), $false)
        for ($i = 0; $i -lt $updates.Count; $i++) { [IO.File]::Copy($updates[$i].Target, (Join-Path $backup ([string]$i)), $false) }
        $changed = $true
        foreach ($f in $updates) {
            [IO.File]::Copy($f.Source, $f.Target, $true)
            if ((Get-FileHash -LiteralPath $f.Target -Algorithm SHA256).Hash -ne $f.SHA256) { throw ('Upgrade verification failed: ' + $f.Path) }
        }
        $records = @()
        foreach ($f in $prior.Files) {
            if (-not (Is-Within (Join-Path $game $f.Path) $game)) { throw 'Invalid prior installation path.' }
            $replacement = @($updates | Where-Object { $_.Path -eq $f.Path })
            $hash = if ($replacement.Count -eq 1) { $replacement[0].SHA256 } else { $f.SHA256 }
            $records += [pscustomobject]@{ Path = $f.Path; SHA256 = $hash }
        }
        $record = [ordered]@{ Version = '0.4.0'; CreatedUtc = $prior.CreatedUtc; UpdatedUtc = [DateTime]::UtcNow.ToString('o');
            PackageRoot = (Full-Path $package); GameAssemblySHA256 = $native; Files = $records }
        Write-TextFile $pending (ConvertTo-Json -InputObject $record -Depth 6)
        [IO.File]::Replace($pending, $marker, $swapBackup)
    } catch {
        $failure = $_
        if ($changed) {
            for ($i = 0; $i -lt $updates.Count; $i++) {
                try { [IO.File]::Copy((Join-Path $backup ([string]$i)), $updates[$i].Target, $true) }
                catch { $recovered = $false }
            }
            try { [IO.File]::Copy((Join-Path $backup 'installation.json'), $marker, $true) }
            catch { $recovered = $false }
            if ($recovered) { Write-Host 'Upgrade failed; the previous mod files were restored.' }
            else { Write-Host ('Some files could not be restored. Recovery copies are in: ' + $backup) }
        }
        throw $failure
    } finally {
        if (Test-Path -LiteralPath $pending) { Remove-Item -LiteralPath $pending -Force -ErrorAction SilentlyContinue }
        if ($recovered -and (Test-Path -LiteralPath $swapBackup)) { Remove-Item -LiteralPath $swapBackup -Force -ErrorAction SilentlyContinue }
        if ($recovered) { Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue }
    }
    Write-Host 'UPDATED to v0.4.0. Launch DEVOUR from Steam.' -ForegroundColor Green
    Write-Host 'VR: both stick clicks = freeze; right-only click/release = noclip; left = run.'
    Write-Host 'F6 = freeze toggle, F7 = noclip toggle, F9 = export report.'
    Write-Host 'Unlimited UV is automatic for your local player in all difficulties.'
    Write-Host ('Reports: ' + (Join-Path $package 'Reports'))
}
try {
    Write-Host 'DEVOUR Monster Freeze v0.4.0 - installer' -ForegroundColor Cyan
    $game = Select-Game
    $script:GameRoot = $game
    if (@(Get-Process -Name DEVOUR -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Close DEVOUR before installing.' }
    $native = (Get-FileHash -LiteralPath (Join-Path $game 'GameAssembly.dll') -Algorithm SHA256).Hash
    $meta = (Get-FileHash -LiteralPath (Join-Path $game 'DEVOUR_Data\il2cpp_data\Metadata\global-metadata.dat') -Algorithm SHA256).Hash
    if ($native -ne 'B57399A48DFFFA243A837589516260C7842314EC3B373755027C39845A8F7292' -or
        $meta -ne 'ED8FF8264BECC4D3B2D33F20F0840C3435ACFC9C12D0BECBD4D175F3A57ED45E') {
        throw 'Your game files differ from the collected DEVOUR 6.0.34 build. Run the collector again before installing this test.'
    }
    $expected = Get-Content -LiteralPath (Join-Path $package 'Payload\payload_hashes.json') -Raw | ConvertFrom-Json
    foreach ($f in $expected) {
        $p = Join-Path $package $f.Path
        if (-not (Is-Within $p $package) -or (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $f.SHA256) { throw ('Package file failed validation: ' + $f.Path) }
    }
    if ($expected.Count -ne 3) { throw 'The package manifest is incomplete.' }
    [void][IO.Directory]::CreateDirectory((Join-Path $package 'Reports'))
    $marker = Join-Path $game 'DEVOURFreeze\installation.json'
    if (Test-Path -LiteralPath $marker) {
        $prior = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
        Update-ExistingInstallation $prior $expected
        exit 0
    }
    foreach ($name in @('BepInEx', 'MelonLoader/net6/MelonLoader.dll', 'MelonLoader/Bootstrap.dll', 'version.dll', 'winhttp.dll', 'dobby.dll', 'doorstop_config.ini')) {
        if (Test-Path -LiteralPath (Join-Path $game $name)) { throw ('An existing loader file/folder is present: ' + $name + '. Export a report so it can be integrated without overwriting your setup.') }
    }
    $archive = Join-Path $package 'Payload\MelonLoader.x64.zip'
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '5B2B2F3D1CD42B59EC886C5BDC2663EDAE87A0097A4F4A8F58C0965A99DDA416') { throw 'The bundled loader archive is incomplete or changed. Extract the original package again.' }
    $staging = Join-Path ([IO.Path]::GetTempPath()) ('DEVOURFreeze_install_' + [Guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($staging)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, $staging)
    $files = New-Object 'System.Collections.Generic.List[object]'
    foreach ($f in @(Get-ChildItem -LiteralPath $staging -Recurse -File)) {
        [void]$files.Add([pscustomobject]@{ Source = $f.FullName; Path = Relative-Path $f.FullName $staging })
    }
    [void]$files.Add([pscustomobject]@{ Source = (Join-Path $package 'Payload\Mods\DevourMonsterFreeze.dll'); Path = 'Mods/DevourMonsterFreeze.dll' })
    foreach ($name in @('Common.ps1', 'Export_Report.ps1')) {
        [void]$files.Add([pscustomobject]@{ Source = (Join-Path $PSScriptRoot $name); Path = ('DEVOURFreeze/Support/' + $name) })
    }
    # Complete the collision/path check before writing any payload into the game.
    foreach ($f in $files) {
        $target = Join-Path $game $f.Path
        if (-not (Is-Within $target $game)) { throw 'Invalid payload path.' }
        if (Test-Path -LiteralPath $target) { throw ('A destination already exists: ' + $f.Path) }
        $ancestor = Split-Path -Parent $target
        while (-not (Test-Path -LiteralPath $ancestor)) { $ancestor = Split-Path -Parent $ancestor }
        if (-not (Is-RegularPath $ancestor)) { throw ('Destination is a linked folder: ' + $f.Path) }
    }
    Write-Host ('Installing into ' + $game)
    foreach ($f in $files) {
        $target = Join-Path $game $f.Path
        $digest = (Get-FileHash -LiteralPath $f.Source -Algorithm SHA256).Hash
        $parent = [IO.Path]::GetDirectoryName($target)
        $missing = New-Object 'System.Collections.Generic.List[string]'
        while (-not (Test-Path -LiteralPath $parent -PathType Container)) {
            [void]$missing.Add($parent); $parent = Split-Path -Parent $parent
        }
        for ($j = $missing.Count - 1; $j -ge 0; $j--) {
            [void][IO.Directory]::CreateDirectory($missing[$j]); [void]$createdDirs.Add($missing[$j])
        }
        # Record before copying so a partial write can also be rolled back.
        [void]$created.Add([pscustomobject]@{ Path = $f.Path; SHA256 = $digest })
        [IO.File]::Copy($f.Source, $target, $false)
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $digest) { throw ('Copy verification failed: ' + $f.Path) }
    }
    $record = [ordered]@{ Version = '0.4.0'; CreatedUtc = [DateTime]::UtcNow.ToString('o'); PackageRoot = (Full-Path $package); GameAssemblySHA256 = $native; Files = @($created.ToArray()) }
    Write-TextFile $marker (ConvertTo-Json -InputObject $record -Depth 6)
    Write-Host ''
    Write-Host 'INSTALLED. Launch DEVOUR normally from Steam.' -ForegroundColor Green
    Write-Host 'The first launch generates support files and may take several minutes.'
    Write-Host 'MelonLoader may offer to install the official Microsoft .NET 6 Desktop Runtime if missing.'
    Write-Host 'Look for the DEVOUR MONSTER FREEZE box at the upper right.'
    Write-Host 'VR: both stick clicks = freeze; right-only click/release = noclip; left = run.'
    Write-Host 'F6 = freeze toggle, F7 = noclip toggle, F9 = export report.'
    Write-Host 'Unlimited UV is automatic for your local player in all difficulties.'
    Write-Host ('Reports: ' + (Join-Path $package 'Reports'))
    exit 0
} catch {
    Write-Host ('INSTALL ERROR: ' + $_.Exception.Message) -ForegroundColor Red
    if ($created.Count -gt 0 -and $marker -and (Test-Path -LiteralPath $marker -PathType Leaf)) {
        Remove-Item -LiteralPath $marker -Force -ErrorAction SilentlyContinue
    }
    for ($i = $created.Count - 1; $i -ge 0; $i--) {
        $target = Join-Path $game $created[$i].Path
        if (Test-Path -LiteralPath $target -PathType Leaf) {
            try { Remove-Item -LiteralPath $target -Force } catch { Write-Host ('Rollback could not remove: ' + $created[$i].Path) }
        }
    }
    for ($i = $createdDirs.Count - 1; $i -ge 0; $i--) {
        $dir = $createdDirs[$i]
        if ((Test-Path -LiteralPath $dir -PathType Container) -and @(Get-ChildItem -LiteralPath $dir -Force).Count -eq 0) {
            Remove-Item -LiteralPath $dir -Force -ErrorAction SilentlyContinue
        }
    }
    if ($created.Count -gt 0) { Write-Host 'The new payload files were rolled back. Original game files were not replaced.' }
    exit 1
} finally {
    if ($staging -and (Test-Path -LiteralPath $staging)) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue }
}
