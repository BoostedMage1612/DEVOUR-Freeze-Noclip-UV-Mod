# Shared discovery and read-only report helpers.
$script:AppId = '1274570'
$script:GameRoot = $null
function Protect-Text([string]$Text) {
    if ($null -eq $Text) { return '' }
    foreach ($pair in @(@($script:GameRoot, '<GAME>'), @($env:USERPROFILE, '<USERPROFILE>'))) {
        if (-not [string]::IsNullOrWhiteSpace([string]$pair[0])) {
            $forward = ([string]$pair[0]).Replace('\', '/')
            foreach ($variant in @(([string]$pair[0]).Replace('\', '\\'), [string]$pair[0], $forward)) {
                $Text = [regex]::Replace($Text, [regex]::Escape($variant), [string]$pair[1], 'IgnoreCase')
            }
        }
    }
    $Text = [regex]::Replace($Text, '(?i)\b[A-Z]:[\\/]+Users[\\/]+[^\\/\r\n"<>]+', '<USERPROFILE>')
    $Text = [regex]::Replace($Text, '\b7656119\d{10}\b', '<STEAM_ID>')
    $Text = [regex]::Replace($Text, '\b(?:\d{1,3}\.){3}\d{1,3}\b', '<IP_ADDRESS>')
    $Text = [regex]::Replace($Text, '(?im)^.*\b(?:authorization|access[_ -]?token|auth[_ -]?ticket|session[_ -]?token|password)\b.*$', '<AUTH_LINE_REDACTED>')
    return $Text
}

function Write-TextFile([string]$Path, [string]$Text) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, $Text, (New-Object Text.UTF8Encoding($false)))
}

function Full-Path([string]$Path) {
    return [IO.Path]::GetFullPath($Path).TrimEnd([char[]]@('\', '/'))
}

function Is-Within([string]$Path, [string]$Parent) {
    $p = Full-Path $Path
    $r = Full-Path $Parent
    return ($p.Equals($r, [StringComparison]::OrdinalIgnoreCase) -or
        $p.StartsWith($r + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))
}

function Relative-Path([string]$Path, [string]$Root) {
    if (-not (Is-Within $Path $Root)) { throw 'Source path is outside its expected folder.' }
    return (Full-Path $Path).Substring((Full-Path $Root).Length).TrimStart([char[]]@('\', '/')).Replace('\', '/')
}

function Is-RegularPath([string]$Path) {
    # Do not follow file symlinks or directory junctions out to unrelated data.
    try {
        $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
        while ($null -ne $item) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }
            if ($item -is [IO.FileInfo]) { $item = $item.Directory } else { $item = $item.Parent }
        }
        return $true
    } catch { return $false }
}

function Read-SharedText([string]$Path, [int]$MaxBytes = 8388608) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        $truncated = $stream.Length -gt $MaxBytes
        if ($truncated) { [void]$stream.Seek(-$MaxBytes, [IO.SeekOrigin]::End) }
        $reader = New-Object IO.StreamReader($stream, [Text.Encoding]::UTF8, $true)
        try { $value = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($truncated) { return "[Earlier log text omitted; last $MaxBytes bytes follow.]`r`n$value" }
        return $value
    } finally { $stream.Dispose() }
}

function Get-VdfValue([string]$Text, [string]$Key) {
    $m = [regex]::Match($Text, '"' + [regex]::Escape($Key) + '"\s*"([^"\r\n]*)"', 'IgnoreCase')
    if ($m.Success) { return $m.Groups[1].Value.Replace('\\', '\') }
    return $null
}

function Get-LibraryPaths([string]$Text) {
    foreach ($m in [regex]::Matches($Text, '"(?:path|[0-9]+)"\s*"((?:[A-Za-z]:[\\/]|\\\\)[^"\r\n]*)"', 'IgnoreCase')) {
        $m.Groups[1].Value.Replace('\\', '\')
    }
}

function Test-GameFolder([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Container)) { return $false }
    if (-not (Is-RegularPath $Path)) { return $false }
    $exe = Join-Path $Path 'DEVOUR.exe'
    $unity = Join-Path $Path 'UnityPlayer.dll'
    $data = Join-Path $Path 'DEVOUR_Data'
    return ((Test-Path -LiteralPath $exe -PathType Leaf) -and
        ((Test-Path -LiteralPath $unity -PathType Leaf) -or (Test-Path -LiteralPath $data -PathType Container)))
}

function Find-GameFolders {
    $roots = New-Object 'System.Collections.Generic.List[string]'
    foreach ($reg in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam')) {
        try {
            $value = Get-ItemProperty -LiteralPath $reg -ErrorAction Stop
            foreach ($key in @('SteamPath', 'InstallPath')) {
                if ($value.PSObject.Properties[$key] -and $value.$key) { [void]$roots.Add([string]$value.$key) }
            }
        } catch { }
    }
    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if ($base) { [void]$roots.Add((Join-Path $base 'Steam')) }
    }
    if ($env:LOCALAPPDATA) { [void]$roots.Add((Join-Path $env:LOCALAPPDATA 'Steam')) }
    foreach ($drive in @(Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue)) {
        if ($drive.Root -match '^[A-Za-z]:\\$') {
            foreach ($suffix in @('Steam', 'SteamLibrary', 'Games\Steam', 'Games\SteamLibrary')) {
                [void]$roots.Add((Join-Path $drive.Root $suffix))
            }
        }
    }
    $initial = @($roots.ToArray())
    foreach ($root in $initial) {
        foreach ($vdf in @('steamapps\libraryfolders.vdf', 'config\libraryfolders.vdf')) {
            $file = Join-Path $root $vdf
            if (Test-Path -LiteralPath $file -PathType Leaf) {
                try { foreach ($path in @(Get-LibraryPaths (Read-SharedText $file))) { [void]$roots.Add($path) } } catch { }
            }
        }
    }
    $seen = @{}
    $found = @{}
    foreach ($root in $roots) {
        try { $normal = Full-Path $root } catch { continue }
        if ($seen.ContainsKey($normal)) { continue }
        $seen[$normal] = $true
        $manifest = Join-Path $normal ('steamapps\appmanifest_' + $script:AppId + '.acf')
        if (Test-Path -LiteralPath $manifest -PathType Leaf) {
            try {
                $text = Read-SharedText $manifest
                $install = Get-VdfValue $text 'installdir'
                if ((Get-VdfValue $text 'appid') -eq $script:AppId -and $install -and
                    [IO.Path]::GetFileName($install) -eq $install) {
                    $candidate = Join-Path (Join-Path $normal 'steamapps\common') $install
                    if (Test-GameFolder $candidate) { $found[(Full-Path $candidate)] = $true }
                }
            } catch { }
        }
        $candidate = Join-Path $normal 'steamapps\common\DEVOUR'
        if (Test-GameFolder $candidate) { $found[(Full-Path $candidate)] = $true }
    }
    return @($found.Keys | Sort-Object)
}

function Select-Game {
    if ($GamePath) {
        $p = $GamePath.Trim().Trim('"')
        if (Test-Path -LiteralPath $p -PathType Leaf) { $p = Split-Path -Parent $p }
        if (-not (Test-GameFolder $p)) { throw 'That folder does not contain a recognized DEVOUR installation (DEVOUR.exe plus UnityPlayer.dll or DEVOUR_Data).' }
        return (Full-Path $p)
    }
    $found = @(Find-GameFolders)
    if ($found.Count -eq 1) { return $found[0] }
    if ($NonInteractive) { throw ('Expected one DEVOUR installation; found ' + $found.Count + '. Supply -GamePath.') }
    if ($found.Count -gt 1) {
        Write-Host 'More than one DEVOUR installation was found:'
        for ($i = 0; $i -lt $found.Count; $i++) { Write-Host ('  {0}. {1}' -f ($i + 1), $found[$i]) }
        while ($true) {
            $choice = Read-Host 'Enter installation number, or Q to quit'
            if ($choice -eq 'q') { throw 'Cancelled.' }
            $n = 0
            if ([int]::TryParse($choice, [ref]$n) -and $n -ge 1 -and $n -le $found.Count) { return $found[$n - 1] }
        }
    }
    Write-Host 'Steam did not reveal the DEVOUR folder.'
    Write-Host 'In Steam: DEVOUR > gear icon > Manage > Browse local files. Copy that folder path.'
    while ($true) {
        $p = (Read-Host 'Paste the folder path, or Q to quit').Trim().Trim('"')
        if ($p -eq 'q') { throw 'Cancelled.' }
        if (Test-Path -LiteralPath $p -PathType Leaf) { $p = Split-Path -Parent $p }
        if (Test-GameFolder $p) { return (Full-Path $p) }
        Write-Host 'DEVOUR.exe and its Unity files were not found there. Please try again.'
    }
}

function Get-LogFolders {
    if (-not $env:USERPROFILE) { return }
    $base = Join-Path $env:USERPROFILE 'AppData\LocalLow'
    if (-not (Test-Path -LiteralPath $base -PathType Container)) { return }
    $found = @{}
    foreach ($company in @(Get-ChildItem -LiteralPath $base -Directory -ErrorAction SilentlyContinue)) {
        if (-not (Is-RegularPath $company.FullName)) { continue }
        if ($company.Name -eq 'DEVOUR') { $found[$company.FullName] = $true }
        foreach ($product in @(Get-ChildItem -LiteralPath $company.FullName -Directory -ErrorAction SilentlyContinue)) {
            if ($product.Name -eq 'DEVOUR' -and (Is-RegularPath $product.FullName)) { $found[$product.FullName] = $true }
        }
    }
    return @($found.Keys | Sort-Object)
}

