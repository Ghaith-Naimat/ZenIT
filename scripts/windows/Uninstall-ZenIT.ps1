param(
    [switch]$RemoveData
)

$ErrorActionPreference = 'Stop'

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

try {
    if (-not (Test-IsAdmin)) {
        throw 'Uninstall-ZenIT.ps1 must be run as Administrator.'
    }

    $installDir = 'C:\Program Files\ZenIT'
    $desktopShortcut = 'C:\Users\Public\Desktop\ZenIT.lnk'
    $startMenuShortcut = 'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\ZenIT.lnk'
    $programDataRoot = 'C:\ProgramData\ZenIT'

    Write-Host 'Removing ZenIT application files and shortcuts...'
    foreach ($path in @($desktopShortcut, $startMenuShortcut, $installDir)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }

    if ($RemoveData) {
        Write-Host 'Removing ZenIT ProgramData folder...'
        if (Test-Path -LiteralPath $programDataRoot) {
            Remove-Item -LiteralPath $programDataRoot -Recurse -Force
        }
    }
    else {
        Write-Host 'Keeping C:\ProgramData\ZenIT. Use -RemoveData to remove logs, reports, and config.'
    }

    Write-Host 'ZenIT uninstalled successfully.'
    exit 0
}
catch {
    Write-Error "ZenIT uninstall failed. $($_.Exception.Message)"
    exit 1
}
