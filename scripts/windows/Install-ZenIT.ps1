param(
    [switch]$RefreshExplorer
)

$ErrorActionPreference = 'Stop'

function Test-IsAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Set-ConfigProperty {
    param([object]$Config, [string]$Name, [object]$Value)
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
    param([string]$ConfigPath)
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
    param([string]$PolicyPath)
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
        throw 'Install-ZenIT.ps1 must be run as Administrator.'
    }

    $repoRoot = 'C:\ZenIT'
    $sourceExe = Join-Path $repoRoot 'publish\win-x64\ZenIT.exe'
    $installDir = 'C:\Program Files\ZenIT'
    $installExe = Join-Path $installDir 'ZenIT.exe'
    $desktopShortcut = 'C:\Users\Public\Desktop\ZenIT.lnk'
    $startMenuShortcut = 'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\ZenIT.lnk'
    $configPath = 'C:\ProgramData\ZenIT\Config\appsettings.json'
    $policyDir = 'C:\ProgramData\ZenIT\Policy'
    $policyPath = Join-Path $policyDir 'itpolicy.json'
    $programDataFolders = @(
        'C:\ProgramData\ZenIT\Config',
        'C:\ProgramData\ZenIT\Logs',
        'C:\ProgramData\ZenIT\Reports'
    )

    if (-not (Test-Path -LiteralPath $sourceExe)) {
        throw "Published ZenIT.exe was not found at $sourceExe. Run Publish-ZenIT.ps1 first."
    }

    $runningZenIT = Get-Process ZenIT -ErrorAction SilentlyContinue
    if ($runningZenIT) {
        Write-Host 'ZenIT is currently running. Asking it to close before install...'
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
            $processList = ($runningZenIT | ForEach-Object { "PID=$($_.Id)" }) -join ', '
            throw "ZenIT is still running ($processList). Please close ZenIT and run the installer again."
        }
    }

    Write-Host 'Creating ZenIT folders...'
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null
    foreach ($folder in $programDataFolders) {
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
    }
    New-Item -ItemType Directory -Force -Path $policyDir | Out-Null
    Update-ZenITConfig -ConfigPath $configPath
    Update-ZenITPolicy -PolicyPath $policyPath

    Write-Host 'Granting standard user Modify access to ZenIT data folders...'
    foreach ($folder in $programDataFolders) {
        if (-not $folder.StartsWith('C:\ProgramData\ZenIT\', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to change permissions outside C:\ProgramData\ZenIT: $folder"
        }

        icacls $folder /grant 'Users:(OI)(CI)M' /T | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to update permissions for $folder"
        }
    }

    Write-Host 'Applying read-only standard user access to ZenIT policy folder...'
    icacls $policyDir /inheritance:r /grant:r 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)F' 'Users:(OI)(CI)RX' /T | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to update permissions for $policyDir"
    }

    icacls $policyPath /inheritance:r /grant:r 'SYSTEM:F' 'Administrators:F' 'Users:R' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to update permissions for $policyPath"
    }

    foreach ($logFile in @('C:\ProgramData\ZenIT\Logs\ZenIT.log', 'C:\ProgramData\ZenIT\Logs\ZenIT-crash.log')) {
        if (Test-Path -LiteralPath $logFile) {
            icacls $logFile /grant 'Users:M' | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to update permissions for $logFile"
            }
        }
    }

    Write-Host 'Copying ZenIT.exe...'
    Copy-Item -LiteralPath $sourceExe -Destination $installExe -Force

    Write-Host 'Creating shortcuts...'
    $shell = New-Object -ComObject WScript.Shell
    foreach ($shortcutPath in @($desktopShortcut, $startMenuShortcut)) {
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $installExe
        $shortcut.WorkingDirectory = $installDir
        $shortcut.Description = 'ZenIT Self-Service IT Assistant'
        $shortcut.IconLocation = "$installExe,0"
        $shortcut.Save()
    }

    $iconRefreshTool = Join-Path $env:SystemRoot 'System32\ie4uinit.exe'
    if (Test-Path -LiteralPath $iconRefreshTool) {
        & $iconRefreshTool -show | Out-Null
    }

    if ($RefreshExplorer) {
        Write-Host 'Restarting Explorer to refresh cached icons...'
        Get-Process explorer -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Process explorer.exe
    }

    Write-Host 'ZenIT installed successfully.'
    Write-Host "Application: $installExe"
    exit 0
}
catch {
    Write-Error "ZenIT install failed. $($_.Exception.Message)"
    exit 1
}
