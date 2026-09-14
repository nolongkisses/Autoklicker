param([switch]$Test)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$localSdk = Join-Path $env:LOCALAPPDATA 'AutoklickerBuild\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet).Source }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $root
try {
    & $dotnet build 'src\Autoklicker\Autoklicker.csproj' -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }
    if ($Test) {
        & $dotnet run --project 'tests\Autoklicker.Tests' -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Tests fehlgeschlagen.' }
    }
    & $dotnet publish 'src\Autoklicker\Autoklicker.csproj' -c Release -r win-x64 --self-contained true -p:DebugType=None -o 'artifacts\release' --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Veröffentlichung fehlgeschlagen.' }
    Copy-Item -LiteralPath 'README.md' -Destination 'artifacts\release\LIESMICH.md'
} finally { Pop-Location }
