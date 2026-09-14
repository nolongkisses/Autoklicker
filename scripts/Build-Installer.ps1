param(
    [switch]$Test,
    [switch]$Open
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $env:LOCALAPPDATA 'AutoklickerBuild\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$makensisCandidates = @(
    (Get-Command makensis -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    'C:\Program Files\NSIS\makensis.exe',
    'C:\Program Files (x86)\NSIS\makensis.exe'
)
$makensis = $makensisCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $makensis) { throw 'NSIS makensis.exe wurde nicht gefunden.' }

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $root
try {
    if ($Test) {
        & "$root\scripts\Build.ps1" -Test
    }
    else {
        & "$root\scripts\Build.ps1"
    }
    if ($LASTEXITCODE -ne 0) { throw 'Release-Build fehlgeschlagen.' }

    $release = (Resolve-Path -LiteralPath (Join-Path $root 'artifacts\release')).Path
    $outFile = Join-Path $root 'artifacts\Autoklicker-Setup-1.0.0-x64.exe'
    $nsi = Join-Path $root 'installer\Autoklicker.nsi'
    & $makensis "/DAPP_VERSION=1.0.0" "/DRELEASE_DIR=$release" "/DOUT_FILE=$outFile" $nsi
    if ($LASTEXITCODE -ne 0) { throw 'NSIS-Installer konnte nicht erstellt werden.' }
    if (-not (Test-Path -LiteralPath $outFile)) { throw 'NSIS-Ausgabe fehlt.' }

    $sha = (Get-FileHash -LiteralPath $outFile -Algorithm SHA256).Hash
    $size = [math]::Round((Get-Item -LiteralPath $outFile).Length / 1MB, 2)
    [pscustomobject]@{ Installer = $outFile; SizeMiB = $size; SHA256 = $sha } | ConvertTo-Json -Compress
    if ($Open) { Start-Process explorer.exe -ArgumentList "/select,`"$outFile`"" }
}
finally { Pop-Location }
