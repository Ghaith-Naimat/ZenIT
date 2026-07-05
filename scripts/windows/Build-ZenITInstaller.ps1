param(
    [switch]$Sign,
    [string]$CertificateThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

function Find-InnoCompiler {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe',
        'C:\Program Files (x86)\Inno Setup 5\ISCC.exe',
        'C:\Program Files\Inno Setup 5\ISCC.exe'
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    return $null
}

function Find-CodeSigningCertificate {
    param([string]$Thumbprint)

    $normalizedThumbprint = ($Thumbprint -replace '\s', '').ToUpperInvariant()
    Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.Thumbprint -replace '\s', '').ToUpperInvariant() -eq $normalizedThumbprint -and
            $_.HasPrivateKey
        } |
        Select-Object -First 1
}

try {
    $repoRoot = 'C:\ZenIT'
    $sourceExe = Join-Path $repoRoot 'publish\jumpcloud\ZenIT.exe'
    $installerScript = Join-Path $repoRoot 'installer\ZenIT.iss'
    $outputDir = Join-Path $repoRoot 'publish\installer'
    $outputExe = Join-Path $outputDir 'ZenIT-Setup.exe'

    $compiler = Find-InnoCompiler
    if (-not $compiler) {
        throw "Inno Setup compiler was not found. Install Inno Setup 6 from https://jrsoftware.org/isdl.php, then rerun this script. Expected ISCC.exe in PATH or C:\Program Files (x86)\Inno Setup 6\ISCC.exe."
    }

    if (-not (Test-Path -LiteralPath $sourceExe)) {
        throw "Source app EXE was not found at $sourceExe. Run Publish-ZenIT.ps1 and Package-ZenIT-JumpCloud.ps1 first."
    }

    if (-not (Test-Path -LiteralPath $installerScript)) {
        throw "Installer script was not found at $installerScript"
    }

    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
    if (Test-Path -LiteralPath $outputExe) {
        Remove-Item -LiteralPath $outputExe -Force
    }

    Write-Host "Using Inno Setup compiler: $compiler"
    Write-Host "Compiling installer: $installerScript"
    & $compiler $installerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup compiler failed with exit code $LASTEXITCODE"
    }

    if (-not (Test-Path -LiteralPath $outputExe)) {
        throw "Installer compile finished but output was not found at $outputExe"
    }

    if ($Sign) {
        if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
            throw 'Signing was requested but -CertificateThumbprint was not provided.'
        }

        $signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
        if (-not $signtool) {
            throw 'Signing was requested but signtool.exe was not found. Install the Windows SDK or run from a Developer PowerShell.'
        }

        $certificate = Find-CodeSigningCertificate -Thumbprint $CertificateThumbprint
        if (-not $certificate) {
            throw "Signing certificate with private key was not found: $CertificateThumbprint"
        }

        Write-Host 'Signing ZenIT installer...'
        & $signtool.Source sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $outputExe
        if ($LASTEXITCODE -ne 0) {
            throw 'ZenIT installer signing failed.'
        }

        $signature = Get-AuthenticodeSignature -LiteralPath $outputExe
        if ($signature.Status -ne 'Valid') {
            throw "ZenIT installer signature validation failed. Status=$($signature.Status); Message=$($signature.StatusMessage)"
        }

        Write-Host "ZenIT installer signature is valid. Signed by: $($signature.SignerCertificate.Subject)"
    }
    else {
        Write-Warning 'Unsigned installer build. JumpCloud Custom Apps may reject unsigned EXE installers; sign before deployment.'
    }

    $installer = Get-Item -LiteralPath $outputExe
    $sizeMiB = [Math]::Round($installer.Length / 1MB, 2)
    $sizeMB = [Math]::Round($installer.Length / 1000000, 2)
    Write-Host "ZenIT installer created: $outputExe"
    Write-Host "Installer size: $sizeMiB MiB ($sizeMB MB)"
    exit 0
}
catch {
    Write-Error "ZenIT installer build failed. $($_.Exception.Message)"
    exit 1
}
