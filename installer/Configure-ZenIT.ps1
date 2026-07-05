param()

$ErrorActionPreference = 'Stop'

$ProgramDataRoot = 'C:\ProgramData\ZenIT'
$ConfigDir = Join-Path $ProgramDataRoot 'Config'
$PolicyDir = Join-Path $ProgramDataRoot 'Policy'
$LogsDir = Join-Path $ProgramDataRoot 'Logs'
$ReportsDir = Join-Path $ProgramDataRoot 'Reports'
$ConfigPath = Join-Path $ConfigDir 'appsettings.json'
$PolicyPath = Join-Path $PolicyDir 'itpolicy.json'
$InstallLog = Join-Path $LogsDir 'Install.log'

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

function Set-JsonProperty {
    param(
        [object]$Object,
        [string]$Name,
        [object]$Value
    )

    if ($Object.PSObject.Properties[$Name]) {
        $Object.$Name = $Value
    }
    else {
        $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
}

function Write-JsonAtomic {
    param(
        [object]$Value,
        [string]$Path
    )

    $tempPath = "$Path.$([Guid]::NewGuid().ToString('N')).tmp"
    $backupPath = "$Path.bak"
    $Value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $tempPath -Encoding UTF8
    if (Test-Path -LiteralPath $Path) {
        Copy-Item -LiteralPath $Path -Destination $backupPath -Force
    }

    Move-Item -LiteralPath $tempPath -Destination $Path -Force
}

function Update-AppSettings {
    if (Test-Path -LiteralPath $ConfigPath) {
        $settings = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    }
    else {
        $settings = [pscustomobject]@{}
    }

    Set-JsonProperty $settings 'AppMode' 'Production'
    Set-JsonProperty $settings 'CompanyName' 'ZenHR'
    Set-JsonProperty $settings 'UpdateChannel' 'Production'
    Set-JsonProperty $settings 'Theme' 'Dark'
    Set-JsonProperty $settings 'Language' 'en'
    Set-JsonProperty $settings 'ITSupportEmail' 'it@zenhr.com'
    Set-JsonProperty $settings 'EnableExperimentalActions' $false
    Set-JsonProperty $settings 'EnableTestMode' $false
    Set-JsonProperty $settings 'LogRetentionDays' 30
    Set-JsonProperty $settings 'ReportRetentionDays' 14

    foreach ($protectedName in @('EnableITMode', 'ITModeUsername', 'ITModePasswordHash', 'AllowITCredentialChanges')) {
        if ($settings.PSObject.Properties[$protectedName]) {
            $settings.PSObject.Properties.Remove($protectedName)
        }
    }

    Write-JsonAtomic -Value $settings -Path $ConfigPath
}

function Update-ITPolicy {
    $policy = [pscustomobject]@{
        EnableITMode = $true
        ITModeUsername = 'Ghaith'
        ITModePasswordHash = '95FAB1FCF914BB5E3D56891BD2B1D03B40DD6066D3ED1327798A9673BB0A30FC'
        AllowITCredentialChanges = $false
        ContactITUrl = 'https://zenhr.slack.com/team/U09CGMUGV6K'
        ITModeSessionTimeoutMinutes = 15
        AllowedITWorkflows = @()
    }

    Write-JsonAtomic -Value $policy -Path $PolicyPath
}

function Set-ZenITPermissions {
    foreach ($folder in @($ConfigDir, $LogsDir, $ReportsDir)) {
        if (-not $folder.StartsWith($ProgramDataRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to change permissions outside $ProgramDataRoot"
        }

        icacls $folder /inheritance:e /grant:r 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)F' 'Users:(OI)(CI)M' /T | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to apply data folder permissions to $folder"
        }
    }

    icacls $PolicyDir /inheritance:r /grant:r 'SYSTEM:(OI)(CI)F' 'Administrators:(OI)(CI)F' 'Users:(OI)(CI)RX' /T | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to apply policy folder permissions to $PolicyDir"
    }

    if (Test-Path -LiteralPath $PolicyPath) {
        icacls $PolicyPath /inheritance:r /grant:r 'SYSTEM:F' 'Administrators:F' 'Users:R' | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to apply policy file permissions to $PolicyPath"
        }
    }
}

try {
    Write-InstallLog 'Configuring ZenIT ProgramData folders.'
    foreach ($folder in @($ConfigDir, $PolicyDir, $LogsDir, $ReportsDir)) {
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
    }

    Update-AppSettings
    Update-ITPolicy
    Set-ZenITPermissions

    $iconRefreshTool = Join-Path $env:SystemRoot 'System32\ie4uinit.exe'
    if (Test-Path -LiteralPath $iconRefreshTool) {
        & $iconRefreshTool -show | Out-Null
    }

    Write-InstallLog 'ZenIT installer configuration completed successfully.'
    exit 0
}
catch {
    Write-InstallLog "ZenIT installer configuration failed: $($_.Exception.Message)"
    Write-Error $_.Exception.Message
    exit 1
}
