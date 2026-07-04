param(
    [string]$SourceExe = 'C:\ZenIT\publish\win-x64\ZenIT.exe',
    [switch]$RefreshExplorer
)

$ErrorActionPreference = 'Stop'

$InstallDir = 'C:\Program Files\ZenIT'
$InstallExe = Join-Path $InstallDir 'ZenIT.exe'
$ProgramDataRoot = 'C:\ProgramData\ZenIT'
$ConfigDir = Join-Path $ProgramDataRoot 'Config'
$PolicyDir = Join-Path $ProgramDataRoot 'Policy'
$LogsDir = Join-Path $ProgramDataRoot 'Logs'
$ReportsDir = Join-Path $ProgramDataRoot 'Reports'
$ConfigPath = Join-Path $ConfigDir 'appsettings.json'
$PolicyPath = Join-Path $PolicyDir 'itpolicy.json'
$InstallLog = Join-Path $LogsDir 'Install.log'
$DesktopShortcut = 'C:\Users\Public\Desktop\ZenIT.lnk'
$StartMenuShortcut = 'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\ZenIT.lnk'

function Write-InstallLog {
    param([string]$Message)
    try {
        New-Item -ItemType Directory -Force -Path $LogsDir | Out-Null
        Add-Content -LiteralPath $InstallLog -Value "[$((Get-Date).ToString('O'))] $Message"
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

function Set-ConfigProperty {
    param(
        [object]$Config,
        [string]$Name,
        [object]$Value
    )

    if ($Config.PSObject.Properties[$Name]) {
        $Config.$Name = $Value
    }
    else {
        $Config | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
}

function Write-JsonAtomic {
    param([object]$Value, [string]$Path)
    $tempPath = "$Path.$([Guid]::NewGuid().ToString('N')).tmp"
    $backupPath = "$Path.bak"
    $Value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $tempPath -Encoding UTF8
    if (Test-Path -LiteralPath $Path) {
        Copy-Item -LiteralPath $Path -Destination $backupPath -Force
        Move-Item -LiteralPath $tempPath -Destination $Path -Force
    }
    else {
        Move-Item -LiteralPath $tempPath -Destination $Path -Force
    }
}

function Update-ZenITConfig {
    if (Test-Path -LiteralPath $ConfigPath) {
        $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    }
    else {
        $config = [pscustomobject]@{}
    }

    Set-ConfigProperty $config 'AppMode' 'Pilot'
    Set-ConfigProperty $config 'ITSupportEmail' 'it@zenhr.com'
    Set-ConfigProperty $config 'CompanyName' 'ZenHR'
    Set-ConfigProperty $config 'UpdateChannel' 'Production'
    Set-ConfigProperty $config 'Language' 'en'
    Set-ConfigProperty $config 'Theme' 'Dark'
    Set-ConfigProperty $config 'EnableExperimentalActions' $false
    Set-ConfigProperty $config 'EnableTestMode' $false
    Set-ConfigProperty $config 'LogRetentionDays' 30
    Set-ConfigProperty $config 'ReportRetentionDays' 14

    Write-JsonAtomic -Value $config -Path $ConfigPath
}

function Update-ZenITPolicy {
    $policy = [pscustomobject]@{
        EnableITMode = $true
        ITModeUsername = 'Ghaith'
        ITModePasswordHash = '95FAB1FCF914BB5E3D56891BD2B1D03B40DD6066D3ED1327798A9673BB0A30FC'
        AllowITCredentialChanges = $false
        ITModeSessionTimeoutMinutes = 15
        ContactITUrl = 'https://zenhr.slack.com/team/U09CGMUGV6K'
        AllowedITWorkflows = @()
    }

    Write-JsonAtomic -Value $policy -Path $PolicyPath
}

try {
    if (-not (Test-IsAdmin)) {
        throw 'JumpCloud deploy must run as Administrator or SYSTEM.'
    }

    if (-not (Test-Path -LiteralPath $SourceExe)) {
        throw "Source ZenIT.exe was not found at $SourceExe"
    }

    foreach ($folder in @($InstallDir, $ConfigDir, $PolicyDir, $LogsDir, $ReportsDir)) {
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
    }

    Update-ZenITConfig
    Update-ZenITPolicy

    Write-InstallLog "Starting ZenIT deployment from $SourceExe"

    $runningZenIT = Get-Process ZenIT -ErrorAction SilentlyContinue
    if ($runningZenIT) {
        Write-InstallLog 'ZenIT is running. Requesting close before deployment.'
        foreach ($process in $runningZenIT) {
            if ($process.MainWindowHandle -ne 0) {
                [void]$process.CloseMainWindow()
            }
        }

        $deadline = (Get-Date).AddSeconds(10)
        do {
            Start-Sleep -Milliseconds 500
            $runningZenIT = Get-Process ZenIT -ErrorAction SilentlyContinue
        } while ($runningZenIT -and (Get-Date) -lt $deadline)

        if ($runningZenIT) {
            Write-InstallLog 'ZenIT did not close in time. Stopping process for managed deployment.'
            $runningZenIT | Stop-Process -Force
        }
    }

    foreach ($folder in @($ConfigDir, $LogsDir, $ReportsDir)) {
        if (-not $folder.StartsWith($ProgramDataRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to change permissions outside $ProgramDataRoot"
        }

        icacls $folder /grant 'Users:(OI)(CI)M' /T | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to apply permissions to $folder"
        }
    }

    icacls $PolicyDir /inheritance:r /grant:r 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)F' 'Users:(OI)(CI)RX' /T | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to apply permissions to $PolicyDir"
    }

    icacls $PolicyPath /inheritance:r /grant:r 'SYSTEM:F' 'Administrators:F' 'Users:R' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to apply permissions to $PolicyPath"
    }

    Copy-Item -LiteralPath $SourceExe -Destination $InstallExe -Force
    Write-InstallLog "Copied ZenIT.exe to $InstallExe"

    $shell = New-Object -ComObject WScript.Shell
    foreach ($shortcutPath in @($DesktopShortcut, $StartMenuShortcut)) {
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $InstallExe
        $shortcut.WorkingDirectory = $InstallDir
        $shortcut.Description = 'ZenIT Self-Service IT Assistant'
        $shortcut.IconLocation = "$InstallExe,0"
        $shortcut.Save()
        Write-InstallLog "Created shortcut $shortcutPath"
    }

    $iconRefreshTool = Join-Path $env:SystemRoot 'System32\ie4uinit.exe'
    if (Test-Path -LiteralPath $iconRefreshTool) {
        & $iconRefreshTool -show | Out-Null
        Write-InstallLog 'Requested Windows shell icon refresh.'
    }

    if ($RefreshExplorer) {
        Write-InstallLog 'Restarting Explorer to refresh cached icons.'
        Get-Process explorer -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Process explorer.exe
    }

    Write-InstallLog 'ZenIT deployment completed successfully.'
    Write-Host 'ZenIT deployment completed successfully.'
    exit 0
}
catch {
    Write-InstallLog "Deployment failed: $($_.Exception.Message)"
    Write-Error "ZenIT deployment failed. $($_.Exception.Message)"
    exit 1
}
