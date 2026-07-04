param(
    [switch]$RemoveData
)

$ErrorActionPreference = 'Stop'

$InstallDir = 'C:\Program Files\ZenIT'
$ProgramDataRoot = 'C:\ProgramData\ZenIT'
$LogsDir = Join-Path $ProgramDataRoot 'Logs'
$UninstallLog = Join-Path $LogsDir 'Uninstall.log'
$DesktopShortcut = 'C:\Users\Public\Desktop\ZenIT.lnk'
$StartMenuShortcut = 'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\ZenIT.lnk'

function Write-UninstallLog {
    param([string]$Message)
    try {
        New-Item -ItemType Directory -Force -Path $LogsDir | Out-Null
        Add-Content -LiteralPath $UninstallLog -Value "[$((Get-Date).ToString('O'))] $Message"
    }
    catch {
        Write-Host $Message
    }
}

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

try {
    if (-not (Test-IsAdmin)) {
        throw 'JumpCloud uninstall must run as Administrator or SYSTEM.'
    }

    Write-UninstallLog 'Starting ZenIT uninstall.'

    $runningZenIT = Get-Process ZenIT -ErrorAction SilentlyContinue
    if ($runningZenIT) {
        foreach ($process in $runningZenIT) {
            if ($process.MainWindowHandle -ne 0) {
                [void]$process.CloseMainWindow()
            }
        }

        Start-Sleep -Seconds 3
        $runningZenIT = Get-Process ZenIT -ErrorAction SilentlyContinue
        if ($runningZenIT) {
            $runningZenIT | Stop-Process -Force
        }
    }

    foreach ($path in @($DesktopShortcut, $StartMenuShortcut, $InstallDir)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
            Write-UninstallLog "Removed $path"
        }
    }

    if ($RemoveData -and (Test-Path -LiteralPath $ProgramDataRoot)) {
        Remove-Item -LiteralPath $ProgramDataRoot -Recurse -Force
        Write-Host 'ZenIT app and ProgramData removed.'
    }
    else {
        Write-UninstallLog 'ProgramData retained.'
        Write-Host 'ZenIT app removed. ProgramData retained.'
    }

    exit 0
}
catch {
    Write-UninstallLog "Uninstall failed: $($_.Exception.Message)"
    Write-Error "ZenIT uninstall failed. $($_.Exception.Message)"
    exit 1
}
