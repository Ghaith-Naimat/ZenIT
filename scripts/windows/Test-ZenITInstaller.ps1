param(
    [switch]$InstallSilently,
    [switch]$RequireValidSignature
)

$ErrorActionPreference = 'Stop'
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Pass {
    param([string]$Message)
    Write-Host "[PASS] $Message"
}

function Add-Failure {
    param([string]$Message)
    Write-Host "[FAIL] $Message"
    $failures.Add($Message)
}

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

try {
    $repoRoot = 'C:\ZenIT'
    $installerPath = Join-Path $repoRoot 'publish\installer\ZenIT-Setup.exe'
    $rawAppPath = Join-Path $repoRoot 'publish\jumpcloud\ZenIT.exe'
    $installerScript = Join-Path $repoRoot 'installer\ZenIT.iss'
    $helperScript = Join-Path $repoRoot 'installer\Configure-ZenIT.ps1'
    $silentFlags = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'

    if (Test-Path -LiteralPath $installerPath) {
        Add-Pass "Installer exists: $installerPath"
        $installer = Get-Item -LiteralPath $installerPath
        $sizeMiB = [Math]::Round($installer.Length / 1MB, 2)
        Write-Host "[INFO] Installer size: $sizeMiB MiB"
        if ($installer.Length -lt 150MB) {
            Add-Pass 'Installer is under 150 MiB'
        }
        else {
            Add-Failure 'Installer is under 150 MiB'
        }

        if ((Test-Path -LiteralPath $rawAppPath) -and $installer.Length -ne (Get-Item -LiteralPath $rawAppPath).Length) {
            Add-Pass 'Installer is not the raw app EXE'
        }
        else {
            Add-Failure 'Installer is not the raw app EXE'
        }

        $signature = Get-AuthenticodeSignature -LiteralPath $installerPath
        Write-Host "[INFO] Installer signature status: $($signature.Status)"
        if ($RequireValidSignature) {
            if ($signature.Status -eq 'Valid') {
                Add-Pass 'Installer signature is valid'
            }
            else {
                Add-Failure "Installer signature is valid: $($signature.StatusMessage)"
            }
        }
    }
    else {
        Add-Failure "Installer exists: $installerPath"
    }

    foreach ($path in @($installerScript, $helperScript)) {
        if (Test-Path -LiteralPath $path) {
            Add-Pass "Installer source exists: $path"
        }
        else {
            Add-Failure "Installer source exists: $path"
        }
    }

    if (Test-Path -LiteralPath $installerScript) {
        $iss = Get-Content -LiteralPath $installerScript -Raw
        foreach ($required in @('AppName=ZenIT', 'PrivilegesRequired=admin', 'UninstallDisplayName=ZenIT', 'Compression=lzma2', 'SolidCompression=yes')) {
            if ($iss -like "*$required*") {
                Add-Pass "Inno metadata present: $required"
            }
            else {
                Add-Failure "Inno metadata present: $required"
            }
        }
    }

    Write-Host "[INFO] JumpCloud silent install flags: $silentFlags"
    Add-Pass 'Silent flags documented: /VERYSILENT /SUPPRESSMSGBOXES /NORESTART'

    if ($InstallSilently) {
        if (-not (Test-IsAdmin)) {
            Add-Failure 'Silent install test requires an elevated shell'
        }
        elseif (-not (Test-Path -LiteralPath $installerPath)) {
            Add-Failure 'Silent install test requires installer output'
        }
        else {
            $process = Start-Process -FilePath $installerPath -ArgumentList $silentFlags -Wait -PassThru
            if ($process.ExitCode -eq 0) {
                Add-Pass 'Silent installer exited successfully'
            }
            else {
                Add-Failure "Silent installer exit code was $($process.ExitCode)"
            }

            & (Join-Path $repoRoot 'scripts\windows\Test-ZenITInstall.ps1')
            if ($LASTEXITCODE -eq 0) {
                Add-Pass 'Installed ZenIT validation passed'
            }
            else {
                Add-Failure 'Installed ZenIT validation passed'
            }
        }
    }

    if ($failures.Count -gt 0) {
        Write-Host "[RESULT] ZenIT installer validation failed: $($failures -join '; ')"
        exit 1
    }

    Write-Host '[RESULT] ZenIT installer validation passed.'
    exit 0
}
catch {
    Add-Failure $_.Exception.Message
    Write-Host "[RESULT] ZenIT installer validation failed: $($failures -join '; ')"
    exit 1
}
