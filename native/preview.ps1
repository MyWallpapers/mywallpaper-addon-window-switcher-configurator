param([string]$SettingsPath, [switch]$PreviewOnly)
$ErrorActionPreference = 'Stop'
$companion = Join-Path $PSScriptRoot 'out/windows-x86_64/companion.exe'
if (-not (Test-Path -LiteralPath $companion)) { throw 'Build first with native/build.ps1.' }
$launchArgs = @($(if ($PreviewOnly) { '--preview' } else { '--standalone' }))
if ($SettingsPath) { $launchArgs += @('--settings', ('"' + (Resolve-Path -LiteralPath $SettingsPath).Path + '"')) }
Start-Process -FilePath $companion -ArgumentList $launchArgs -WindowStyle Hidden

