param([switch]$Open)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$release = Join-Path $projectRoot 'artifacts\release'
$install = Join-Path $env:LOCALAPPDATA 'Programs\Autoklicker'
$exe = Join-Path $install 'Autoklicker.exe'
$marker = Join-Path $install '.autoklicker-install'
$identity = 'Autoklicker.8B18A3D2'
if (-not (Test-Path -LiteralPath (Join-Path $release 'Autoklicker.exe'))) { throw 'Zuerst scripts\Build.ps1 ausführen.' }
if (Test-Path -LiteralPath $install) {
    if (-not (Test-Path -LiteralPath $marker) -or (Get-Content -LiteralPath $marker -Raw).Trim() -ne $identity) {
        throw "Der Zielordner enthält keine von diesem Projekt verwaltete Installation: $install"
    }
}
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
$shortcutPath = Join-Path $desktop 'Autoklicker.lnk'
$shell = New-Object -ComObject WScript.Shell
if (Test-Path -LiteralPath $shortcutPath) {
    $existing = $shell.CreateShortcut($shortcutPath)
    if ($existing.TargetPath -ne $exe) { throw 'Die vorhandene Desktop-Verknüpfung gehört zu einer anderen App.' }
}
$running = Get-Process -Name Autoklicker -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
if ($running) { throw 'Die installierte App muss vor dem Aktualisieren geschlossen werden.' }
New-Item -ItemType Directory -Path $install -Force | Out-Null
Get-ChildItem -LiteralPath $release | Copy-Item -Destination $install -Recurse -Force
Set-Content -LiteralPath $marker -Value $identity -Encoding UTF8
foreach ($name in @('Autoklicker.exe', 'Autoklicker.dll')) {
    if ((Get-FileHash -LiteralPath (Join-Path $release $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $install $name)).Hash) {
        throw "Dateiprüfung fehlgeschlagen: $name"
    }
}
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $install
$shortcut.IconLocation = "$exe,0"
$shortcut.Description = 'Autoklicker – kompakte Klicksteuerung'
$shortcut.WindowStyle = 1
$shortcut.Save()
Write-Output "Installiert: $exe"
Write-Output "Verknüpfung: $shortcutPath"
if ($Open) { Start-Process -FilePath $shortcutPath }
