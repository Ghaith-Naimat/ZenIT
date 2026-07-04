param()

$ErrorActionPreference = 'Stop'
$failures = New-Object System.Collections.Generic.List[string]

$InstallExe = 'C:\Program Files\ZenIT\ZenIT.exe'
$DesktopShortcut = 'C:\Users\Public\Desktop\ZenIT.lnk'
$StartMenuShortcut = 'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\ZenIT.lnk'
$ConfigDir = 'C:\ProgramData\ZenIT\Config'
$PolicyDir = 'C:\ProgramData\ZenIT\Policy'
$LogsDir = 'C:\ProgramData\ZenIT\Logs'
$ReportsDir = 'C:\ProgramData\ZenIT\Reports'
$LogoDir = 'C:\ZenIT\assets\logo'
$ConfigPath = 'C:\ProgramData\ZenIT\Config\appsettings.json'
$PolicyPath = 'C:\ProgramData\ZenIT\Policy\itpolicy.json'
$StandardITModeUsername = 'Ghaith'
$StandardITModePasswordHash = '95FAB1FCF914BB5E3D56891BD2B1D03B40DD6066D3ED1327798A9673BB0A30FC'
$ExpectedEmployeeWorkflowCount = 13
$ExpectedITWorkflowCount = 21
$ContactITUrl = 'https://zenhr.slack.com/team/U09CGMUGV6K'

function Test-PathPass {
    param([string]$Label, [string]$Path)
    if (Test-Path -LiteralPath $Path) {
        Write-Host "[PASS] $Label - $Path"
    }
    else {
        Write-Host "[FAIL] $Label - $Path"
        $failures.Add($Label)
    }
}

function Test-Writable {
    param([string]$Label, [string]$Folder)
    try {
        $testFile = Join-Path $Folder "ZenIT-write-test-$([Guid]::NewGuid()).tmp"
        Set-Content -LiteralPath $testFile -Value 'test'
        Remove-Item -LiteralPath $testFile -Force
        Write-Host "[PASS] $Label writable - $Folder"
    }
    catch {
        Write-Host "[FAIL] $Label writable - $Folder - $($_.Exception.Message)"
        $failures.Add("$Label writable")
    }
}

try {
    Test-PathPass 'ZenIT executable' $InstallExe
    Test-PathPass 'Public Desktop shortcut' $DesktopShortcut
    Test-PathPass 'Start Menu shortcut' $StartMenuShortcut
    Test-PathPass 'Config folder' $ConfigDir
    Test-PathPass 'Policy folder' $PolicyDir
    Test-PathPass 'Logs folder' $LogsDir
    Test-PathPass 'Reports folder' $ReportsDir
    Test-PathPass 'Config file' $ConfigPath
    Test-PathPass 'IT policy file' $PolicyPath
    Test-Writable 'Config folder' $ConfigDir
    Test-Writable 'Logs folder' $LogsDir
    Test-Writable 'Reports folder' $ReportsDir

    if (Test-Path -LiteralPath $ConfigPath) {
        $configText = Get-Content -LiteralPath $ConfigPath -Raw
        $config = $configText | ConvertFrom-Json

        $forbiddenPolicyProperties = @('ITModeUsername', 'ITModePasswordHash', 'AllowITCredentialChanges', 'EnableITMode')
        $presentForbidden = @($forbiddenPolicyProperties | Where-Object { $config.PSObject.Properties[$_] })
        if ($presentForbidden.Count -gt 0) {
            Write-Host "[FAIL] User appsettings excludes protected IT policy fields: $($presentForbidden -join ', ')"
            $failures.Add('Appsettings excludes protected IT policy')
        }
        else {
            Write-Host '[PASS] User appsettings excludes protected IT policy fields'
        }
    }

    if (Test-Path -LiteralPath $PolicyPath) {
        $policy = $null
        try {
            $policyText = Get-Content -LiteralPath $PolicyPath -Raw
            $policy = $policyText | ConvertFrom-Json
        }
        catch {
            Write-Host '[FAIL] IT policy file is readable by the current user'
            $failures.Add("IT policy file readable: $($_.Exception.Message)")
        }

        if ($null -eq $policy) {
            # Access or parse failure already recorded above.
        }
        elseif ($policy.ITModeUsername -ne $StandardITModeUsername) {
            Write-Host "[FAIL] ITModeUsername equals standard username"
            $failures.Add('ITModeUsername standard value')
        }
        else {
            Write-Host '[PASS] ITModeUsername equals standard username'
        }

        if ($null -eq $policy) {
            # Access or parse failure already recorded above.
        }
        elseif ($policy.ITModePasswordHash -ne $StandardITModePasswordHash) {
            Write-Host '[FAIL] ITModePasswordHash equals standard SHA256 hash'
            $failures.Add('ITModePasswordHash standard value')
        }
        elseif ($policy.ITModePasswordHash -notmatch '^[A-Fa-f0-9]{64}$') {
            Write-Host '[FAIL] ITModePasswordHash is not a SHA256 hex hash'
            $failures.Add('ITModePasswordHash format')
        }
        else {
            Write-Host '[PASS] ITModePasswordHash equals standard SHA256 hash'
        }

        if ($null -eq $policy) {
            # Access or parse failure already recorded above.
        }
        elseif ($policy.PSObject.Properties['AllowITCredentialChanges'] -and $policy.AllowITCredentialChanges -eq $false) {
            Write-Host '[PASS] Change Password UI/config marker is disabled'
        }
        else {
            Write-Host '[FAIL] Change Password UI/config marker is disabled'
            $failures.Add('AllowITCredentialChanges disabled')
        }

        $plaintextPasswordProperties = if ($null -eq $policy) {
            @()
        }
        else {
            $policy.PSObject.Properties |
                Where-Object { $_.Name -match 'Password' -and $_.Name -ne 'ITModePasswordHash' }
        }
        if ($plaintextPasswordProperties) {
            Write-Host '[FAIL] Plaintext password-shaped config property detected'
            $failures.Add('No plaintext password property')
        }
        else {
            Write-Host '[PASS] No plaintext password property found in config'
        }

        if ($null -eq $policy) {
            # Access or parse failure already recorded above.
        }
        elseif ($policy.EnableITMode -eq $true) {
            Write-Host '[PASS] EnableITMode is true'
        }
        else {
            Write-Host '[FAIL] EnableITMode is true'
            $failures.Add('EnableITMode true')
        }

        if ($null -eq $policy) {
            # Access or parse failure already recorded above.
        }
        elseif ($policy.ITModeSessionTimeoutMinutes -eq 15) {
            Write-Host '[PASS] IT Mode session timeout is 15 minutes'
        }
        else {
            Write-Host '[FAIL] IT Mode session timeout is 15 minutes'
            $failures.Add('IT Mode session timeout')
        }

        if ($null -eq $policy) {
            # Access or parse failure already recorded above.
        }
        elseif ($policy.ContactITUrl -eq $ContactITUrl) {
            Write-Host '[PASS] Protected Contact IT URL is standard'
        }
        else {
            Write-Host '[FAIL] Protected Contact IT URL is standard'
            $failures.Add('Contact IT URL policy')
        }
    }

    if (Test-Path -LiteralPath $ConfigPath) {
        $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json

        if ($config.UpdateChannel -eq 'Production') {
            Write-Host '[PASS] UpdateChannel is Production'
        }
        else {
            Write-Host '[FAIL] UpdateChannel is Production'
            $failures.Add('UpdateChannel Production')
        }

        if ($config.Language -in @('en', 'ar')) {
            Write-Host "[PASS] Language is supported ($($config.Language))"
        }
        else {
            Write-Host '[FAIL] Language is supported'
            $failures.Add('Language supported')
        }

        if ($config.Theme -in @('Dark', 'Light', 'HighContrast')) {
            Write-Host "[PASS] Theme is supported ($($config.Theme))"
        }
        else {
            Write-Host '[FAIL] Theme is supported'
            $failures.Add('Theme supported')
        }
    }

    Test-PathPass 'Logo asset folder' $LogoDir

    $workflowRegistryPath = 'C:\ZenIT\src\ZenIT.Core\Workflows\WorkflowRegistry.cs'
    if (Test-Path -LiteralPath $workflowRegistryPath) {
        $workflowRegistryText = Get-Content -LiteralPath $workflowRegistryPath -Raw
        $employeeWorkflowCount = ([regex]::Matches($workflowRegistryText, '\[WorkflowId\.[^\r\n]+CreateEmployee\(')).Count
        $itWorkflowCount = ([regex]::Matches($workflowRegistryText, '\[WorkflowId\.[^\r\n]+CreateIT\(')).Count
        if ($employeeWorkflowCount -eq $ExpectedEmployeeWorkflowCount) {
            Write-Host "[PASS] Employee workflow count is $ExpectedEmployeeWorkflowCount"
        }
        else {
            Write-Host "[FAIL] Employee workflow count expected $ExpectedEmployeeWorkflowCount but found $employeeWorkflowCount"
            $failures.Add('Employee workflow count')
        }

        if ($itWorkflowCount -eq $ExpectedITWorkflowCount) {
            Write-Host "[PASS] IT workflow count is $ExpectedITWorkflowCount"
        }
        else {
            Write-Host "[FAIL] IT workflow count expected $ExpectedITWorkflowCount but found $itWorkflowCount"
            $failures.Add('IT workflow count')
        }
    }
    else {
        Write-Host '[INFO] Workflow registry source not available on this device; skipping source count check.'
    }

    $sourceRoot = 'C:\ZenIT'
    if (Test-Path -LiteralPath $sourceRoot) {
        $sourceFiles = Get-ChildItem -Path $sourceRoot -Recurse -File -Include *.cs,*.xaml,*.ps1,*.md |
            Where-Object { $_.FullName -notmatch '\\(bin|obj|publish)\\' }
        $contactMatches = $sourceFiles | Select-String -Pattern ([regex]::Escape($ContactITUrl)) -SimpleMatch:$false
        if ($contactMatches) {
            Write-Host '[PASS] Contact IT Slack link exists in source'
        }
        else {
            Write-Host '[FAIL] Contact IT Slack link exists in source'
            $failures.Add('Contact IT link')
        }

        $removedFeedbackToken = 'Pilot' + 'Feedback'
        $pilotFeedbackMatches = $sourceFiles |
            Where-Object { $_.Name -ne 'Test-ZenITInstall.ps1' } |
            Select-String -Pattern $removedFeedbackToken
        if ($pilotFeedbackMatches) {
            Write-Host '[FAIL] Removed feedback token is absent from active source/docs'
            $failures.Add('Removed feedback token absent')
        }
        else {
            Write-Host '[PASS] Removed feedback token is absent from active source/docs'
        }
    }

    if (Test-Path -LiteralPath $InstallExe) {
        $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($InstallExe).ProductVersion
        if ([string]::IsNullOrWhiteSpace($version)) {
            $version = 'Not available'
        }

        Write-Host "[INFO] ZenIT version: $version"
    }

    if ($failures.Count -gt 0) {
        Write-Host "[RESULT] ZenIT validation failed: $($failures -join ', ')"
        exit 1
    }

Write-Host '[RESULT] ZenIT validation passed.'

$localizationScript = Join-Path $PSScriptRoot 'Test-ZenITLocalization.ps1'
if (Test-Path $localizationScript) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File $localizationScript
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
exit 0
}
catch {
    Write-Error "ZenIT validation failed. $($_.Exception.Message)"
    exit 1
}
