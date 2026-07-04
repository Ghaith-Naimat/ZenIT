Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$resourcePath = Join-Path $repoRoot 'src\ZenIT.Core\Localization\LocalizedStrings.cs'
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure {
    param([string]$Message)
    Write-Host "[FAIL] $Message"
    $failures.Add($Message)
}

function Add-Pass {
    param([string]$Message)
    Write-Host "[PASS] $Message"
}

if (-not (Test-Path $resourcePath)) {
    Add-Failure "Localization resource file exists"
}
else {
    $content = Get-Content -Path $resourcePath -Raw -Encoding UTF8
    $keyMatches = [regex]::Matches($content, '\["(?<key>[^"]+)"\]\s*=\s*"(?<value>(?:[^"\\]|\\.)*)"')
    $allKeys = @($keyMatches | ForEach-Object { $_.Groups['key'].Value })

    $duplicateKeys = $allKeys | Group-Object | Where-Object { $_.Count -gt 2 }
    if ($duplicateKeys) {
        Add-Failure "No duplicate localization keys beyond en/ar pairs: $($duplicateKeys.Name -join ', ')"
    }
    else {
        Add-Pass "No duplicate localization keys beyond en/ar pairs"
    }

    $emptyValues = @($keyMatches | Where-Object { [string]::IsNullOrWhiteSpace($_.Groups['value'].Value) })
    if ($emptyValues.Count -gt 0) {
        Add-Failure "No empty localization values"
    }
    else {
        Add-Pass "No empty localization values"
    }

    $enBlock = [regex]::Match($content, '\["en"\]\s*=\s*new Dictionary<string, string>.*?\{(?<block>.*?)\},\s*\["ar"\]', [System.Text.RegularExpressions.RegexOptions]::Singleline).Groups['block'].Value
    $arBlock = [regex]::Match($content, '\["ar"\]\s*=\s*new Dictionary<string, string>.*?\{(?<block>.*?)\}\s*\};', [System.Text.RegularExpressions.RegexOptions]::Singleline).Groups['block'].Value
    $enKeys = @([regex]::Matches($enBlock, '\["(?<key>[^"]+)"\]') | ForEach-Object { $_.Groups['key'].Value } | Sort-Object -Unique)
    $arKeys = @([regex]::Matches($arBlock, '\["(?<key>[^"]+)"\]') | ForEach-Object { $_.Groups['key'].Value } | Sort-Object -Unique)

    if ($enKeys.Count -eq 0 -or $arKeys.Count -eq 0) {
        Add-Failure "English and Arabic resource keys exist"
    }
    else {
        Add-Pass "English and Arabic resource keys exist"
    }

    $missingArabic = @($enKeys | Where-Object { $_ -notin $arKeys })
    $missingEnglish = @($arKeys | Where-Object { $_ -notin $enKeys })
    if ($missingArabic.Count -gt 0) {
        Add-Failure "No missing Arabic keys: $($missingArabic -join ', ')"
    }
    else {
        Add-Pass "No missing Arabic keys"
    }

    if ($missingEnglish.Count -gt 0) {
        Add-Failure "No missing English keys: $($missingEnglish -join ', ')"
    }
    else {
        Add-Pass "No missing English keys"
    }

    $requiredKeys = @(
        'Nav.Home','Nav.QuickFixes','Nav.MyDevice','Nav.ITMode','Nav.About',
        'Header.HomeTitle','Header.HomeSubtitle','QuickFixes.Question',
        'Device.Title','IT.Title','IT.AdministratorAccess','IT.AdministratorAccessSubtitle','IT.Username','IT.Password',
        'IT.ShowPassword','IT.CredentialsProtected','IT.NeedAccess','IT.Authenticating','IT.AdministratorVerified',
        'IT.SignedInAs','IT.LockedInactivity','IT.WhatUnlocks','IT.SecurityHeading','IT.Invalid',
        'IT.Capability.AdvancedRepairs','IT.Capability.Diagnostics','IT.Capability.NetworkTools','IT.Capability.WindowsRepair',
        'IT.Capability.SecurityChecks','IT.Capability.SystemLogs','IT.Capability.Reports','IT.Capability.DeploymentTools',
        'IT.Security.AdminAuth','IT.Security.Logged','IT.Security.NoPasswords','IT.Security.Auditable',
        'IT.PolicyUnavailable','IT.PolicyUnavailableInstall','IT.UnlockedSession','IT.Locked',
        'Device.HealthScoreTooltip','HealthScore.Excellent','HealthScore.Good','HealthScore.NeedsAttention','HealthScore.ContactIT',
        'Message.DeviceOptimizationComplete','Message.DeviceOptimizationNoMajorIssues','Message.OptionalAppRefreshSkipped','Message.SomeIssuesNeedIT',
        'Time.Today','Time.Yesterday','Time.SecondsShort','Time.MinutesShort',
        'Logs.Search','Logs.Filter','Logs.Time','Logs.Workflow','Logs.Result','Logs.Duration',
        'Reports.LastSupportPackage','Button.CreateSupportPackage','Button.ContactIT',
        'About.Title','About.Privacy',
        'Workflow.Internet.Title','Workflow.Speed.Title','Workflow.Everything.Title',
        'Workflow.CameraMic.Title','Workflow.Package.Title','Workflow.Contact.Title'
    )

    foreach ($key in $requiredKeys) {
        if ($key -notin $enKeys -or $key -notin $arKeys) {
            Add-Failure "Required localization key exists in en/ar: $key"
        }
    }

    if ($failures.Count -eq 0) {
        Add-Pass "Required page, workflow, logs, reports, IT Mode, and About labels are localized"
    }

    $allowedVisibleLiterals = @(
        'ZenIT','ZenHR','Slack','Zoom','Chrome','Google Drive','Kaspersky','JumpCloud','Windows',
        'EN','/100','HM','QF','DV','AB','OK','IT'
    )
    $xamlPath = Join-Path $repoRoot 'src\ZenIT.App\MainWindow.xaml'
    if (Test-Path $xamlPath) {
        $xamlContent = Get-Content -Path $xamlPath -Raw -Encoding UTF8
        $literalMatches = [regex]::Matches($xamlContent, '(Text|Content|ToolTip)="(?<value>[A-Za-z][^"{Binding][^"]*)"')
        $hardcoded = @()
        foreach ($match in $literalMatches) {
            $value = $match.Groups['value'].Value.Trim()
            if ($value -and $value -notin $allowedVisibleLiterals) {
                $hardcoded += $value
            }
        }

        if ($hardcoded.Count -gt 0) {
            Add-Failure "No obvious hardcoded English visible strings in XAML: $($hardcoded | Sort-Object -Unique -join ', ')"
        }
        else {
            Add-Pass "No obvious hardcoded English visible strings in XAML"
        }
    }

    $viewModelPath = Join-Path $repoRoot 'src\ZenIT.App\ViewModels'
    if (Test-Path $viewModelPath) {
        $viewModelText = Get-ChildItem -Path $viewModelPath -Filter '*.cs' |
            Get-Content -Raw -Encoding UTF8
        foreach ($forbidden in @('Ready to help','DNS text is still not localized')) {
            if ($viewModelText -match [regex]::Escape($forbidden)) {
                Add-Failure "Forbidden hardcoded UI text is absent from ViewModels: $forbidden"
            }
        }

        if ($failures.Count -eq 0) {
            Add-Pass "No known forbidden hardcoded English UI strings in ViewModels"
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "[RESULT] ZenIT localization validation failed: $($failures -join '; ')"
    exit 1
}

Write-Host '[RESULT] ZenIT localization validation passed.'
exit 0
