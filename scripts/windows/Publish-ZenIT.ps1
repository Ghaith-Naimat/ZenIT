param(
    [ValidateSet('SelfContained', 'FrameworkDependent')]
    [string]$Mode = 'SelfContained',
    [switch]$Sign,
    [string]$CertificateThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

try {
    $repoRoot = 'C:\ZenIT'
    $publishPath = if ($Mode -eq 'FrameworkDependent') {
        Join-Path $repoRoot 'publish\win-x64-framework-dependent'
    }
    else {
        Join-Path $repoRoot 'publish\win-x64'
    }
    $projectPath = Join-Path $repoRoot 'src\ZenIT.App\ZenIT.App.csproj'
    $exePath = Join-Path $publishPath 'ZenIT.exe'
    $selfContained = if ($Mode -eq 'FrameworkDependent') { 'false' } else { 'true' }

    Set-Location -LiteralPath $repoRoot

    $runningPublishedZenIT = @()
    $runningZenIT = Get-Process ZenIT -ErrorAction SilentlyContinue
    foreach ($process in $runningZenIT) {
        $processPath = $null
        try {
            $processPath = $process.MainModule.FileName
        }
        catch {
            Write-Host "ZenIT is running as PID=$($process.Id), but its executable path is not accessible from this shell. Continuing publish validation."
            continue
        }

        if ([string]::IsNullOrWhiteSpace($processPath)) {
            Write-Host "ZenIT is running as PID=$($process.Id), but its executable path is not available from this shell. Continuing publish validation."
            continue
        }

        if ($processPath.StartsWith($publishPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            $runningPublishedZenIT += "PID=$($process.Id)"
        }
    }

    if ($runningPublishedZenIT.Count -gt 0) {
        $processList = $runningPublishedZenIT -join ', '
        throw "Please close the published ZenIT.exe first, then publish again. Running process: $processList"
    }

    Write-Host 'Cleaning previous publish output...'
    if (Test-Path -LiteralPath $publishPath) {
        Remove-Item -LiteralPath $publishPath -Recurse -Force
    }

    Write-Host 'Cleaning Release build output...'
    dotnet clean .\ZenIT.sln -c Release
    if ($LASTEXITCODE -ne 0) {
        throw 'Release clean failed.'
    }

    Write-Host 'Building ZenIT Release...'
    dotnet build .\ZenIT.sln -c Release
    if ($LASTEXITCODE -ne 0) {
        throw 'Release build failed.'
    }

    Write-Host "Publishing ZenIT for win-x64 ($Mode)..."
    $publishArguments = @(
        'publish', $projectPath,
        '-c', 'Release',
        '-r', 'win-x64',
        '--self-contained', $selfContained,
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:DebugType=None',
        '-p:DebugSymbols=false',
        '-o', $publishPath
    )

    if ($Mode -eq 'SelfContained') {
        $publishArguments += '-p:EnableCompressionInSingleFile=true'
    }

    dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) {
        throw 'Publish failed.'
    }

    Get-ChildItem -LiteralPath $publishPath -Filter '*.pdb' -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue

    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Publish completed but ZenIT.exe was not found at $exePath"
    }

    if ($Sign) {
        if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
            throw 'Signing was requested but -CertificateThumbprint was not provided.'
        }

        $signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
        if (-not $signtool) {
            throw 'Signing was requested but signtool.exe was not found. Install the Windows SDK or run from a Developer PowerShell.'
        }

        $certificate = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My |
            Where-Object Thumbprint -eq $CertificateThumbprint |
            Select-Object -First 1
        if (-not $certificate) {
            throw "Signing certificate was not found: $CertificateThumbprint"
        }

        Write-Host 'Signing ZenIT.exe...'
        & $signtool.Source sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $exePath
        if ($LASTEXITCODE -ne 0) {
            throw 'ZenIT.exe signing failed.'
        }

        $scriptFiles = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'scripts\windows') -Filter '*.ps1'
        foreach ($script in $scriptFiles) {
            Set-AuthenticodeSignature -FilePath $script.FullName -Certificate $certificate -TimestampServer $TimestampUrl | Out-Null
        }
    }
    else {
        Write-Warning 'Unsigned build. Sign before broad enterprise rollout.'
    }

    $exe = Get-Item -LiteralPath $exePath
    $sizeMiB = [Math]::Round($exe.Length / 1MB, 2)
    $sizeMB = [Math]::Round($exe.Length / 1000000, 2)
    Write-Host "ZenIT publish completed successfully: $exePath"
    Write-Host "Mode: $Mode"
    Write-Host "Size: $sizeMiB MiB ($sizeMB MB)"
    exit 0
}
catch {
    Write-Error "ZenIT publish failed. $($_.Exception.Message)"
    exit 1
}
