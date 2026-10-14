param([string]$DotnetPath, [switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location -LiteralPath $repoRoot
try {
    $expectedSdk = (Get-Content -LiteralPath (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
    if (-not $DotnetPath) {
        $cacheRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'MyWallpaperAddonBuild/dotnet-8.0.425'))
        $cachedSdk = Join-Path $cacheRoot 'dotnet.exe'
        $systemSdk = (Get-Command dotnet.exe -ErrorAction SilentlyContinue).Source
        foreach ($candidate in @($cachedSdk, $systemSdk)) {
            if ($candidate -and (Test-Path -LiteralPath $candidate)) {
                try { $candidateVersion = & $candidate --version 2>$null } catch { $candidateVersion = $null }
                if ($LASTEXITCODE -eq 0 -and $candidateVersion -eq $expectedSdk) { $DotnetPath = $candidate; break }
            }
        }
        if (-not $DotnetPath) {
            if ($expectedSdk -ne '8.0.425') { throw 'Update the pinned SDK archive and hash before changing global.json.' }
            New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
            $archive = Join-Path $cacheRoot ('sdk-' + [Guid]::NewGuid().ToString('N') + '.zip')
            try {
                Write-Host 'Fetching Microsoft .NET SDK 8.0.425 for this build; verifying SHA-512.'
                Invoke-WebRequest -UseBasicParsing -Uri 'https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.425/dotnet-sdk-8.0.425-win-x64.zip' -OutFile $archive -TimeoutSec 180
                $expectedHash = 'f0b6f15bf6f1a0507205c0cb102ab99e1dee875c4682c8ed94665be1d580186a06b21455e83b3a01a0ff7f4cd887b67420f2e2fe09ed985534a4cea488ae1af9'
                if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $expectedHash) { throw 'Microsoft SDK archive checksum mismatch.' }
                Expand-Archive -LiteralPath $archive -DestinationPath $cacheRoot -Force
            } finally { if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force } }
            $DotnetPath = $cachedSdk
        }
    }
    $DotnetPath = (Resolve-Path -LiteralPath $DotnetPath).Path
    $sdkVersion = & $DotnetPath --version
    if ($LASTEXITCODE -ne 0) { throw '.NET SDK 8.0.425 is required; see global.json.' }
    if ($sdkVersion -ne $expectedSdk) { throw ('Exact .NET SDK required: ' + $expectedSdk + '; found ' + $sdkVersion) }
    Write-Host $sdkVersion
    & node tools/generate-manifest.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Manifest generation failed.' }
    & $DotnetPath run --project tests/Switcher.Tests/Switcher.Tests.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Core validation failed.' }
    $outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'native/out/windows-x86_64'))
    $expectedRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'out/windows-x86_64'))
    if ($outputRoot -ne $expectedRoot -or -not $outputRoot.StartsWith($repoRoot.TrimEnd('\','/') + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Output directory did not resolve within this add-on repository.'
    }
    if (Test-Path -LiteralPath $outputRoot) {
        if (((Get-Item -LiteralPath $outputRoot -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Refusing reparse-point output directory.' }
        Remove-Item -LiteralPath $outputRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    $selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
    & $DotnetPath publish src/Switcher.Windows/Switcher.Windows.csproj -c Release -r win-x64 --self-contained $selfContained `
        -p:DebugType=None -p:DebugSymbols=false -o $outputRoot --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE'),(Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination $outputRoot
    $sdkRoot = Split-Path -Parent $DotnetPath
    foreach ($notice in @('LICENSE.txt','ThirdPartyNotices.txt')) {
        $source = Join-Path $sdkRoot $notice
        if (-not (Test-Path -LiteralPath $source)) { throw ('Runtime notice missing: ' + $source) }
        Copy-Item -LiteralPath $source -Destination (Join-Path $outputRoot ('DOTNET-' + $notice))
    }
    $files = Get-ChildItem -LiteralPath $outputRoot -File -Recurse
    if (-not (Test-Path -LiteralPath (Join-Path $outputRoot 'companion.exe'))) { throw 'Companion executable missing.' }
    Write-Host ('Bundle ready: ' + $files.Count + ' files, ' + [Math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1) + ' MiB at ' + $outputRoot)
} finally { Pop-Location }

