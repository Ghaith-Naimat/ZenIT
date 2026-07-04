param(
    [ValidateSet('SelfContained', 'FrameworkDependent')]
    [string]$Mode = 'SelfContained',
    [switch]$Zip
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

    $sourceExe = Join-Path $publishPath 'ZenIT.exe'
    $packageRoot = Join-Path $repoRoot 'publish\jumpcloud'
    $packageExe = Join-Path $packageRoot 'ZenIT.exe'
    $packageZip = Join-Path $packageRoot 'ZenIT.zip'

    if (-not (Test-Path -LiteralPath $sourceExe)) {
        throw "Published ZenIT.exe was not found: $sourceExe"
    }

    if (Test-Path -LiteralPath $packageRoot) {
        Remove-Item -LiteralPath $packageRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
    Copy-Item -LiteralPath $sourceExe -Destination $packageExe -Force

    $exe = Get-Item -LiteralPath $packageExe
    $exeMiB = [Math]::Round($exe.Length / 1MB, 2)
    $exeMB = [Math]::Round($exe.Length / 1000000, 2)
    Write-Host "JumpCloud EXE package: $packageExe"
    Write-Host "EXE size: $exeMiB MiB ($exeMB MB)"
    if ($exe.Length -lt 150MB) {
        Write-Host 'EXE is under 150 MiB.'
    }
    else {
        Write-Warning 'EXE is over 150 MiB. Use FrameworkDependent mode or ZIP only if JumpCloud accepts the resulting upload.'
    }

    if ($Zip) {
        Compress-Archive -LiteralPath $packageExe -DestinationPath $packageZip -Force
        $zipFile = Get-Item -LiteralPath $packageZip
        $zipMiB = [Math]::Round($zipFile.Length / 1MB, 2)
        $zipMB = [Math]::Round($zipFile.Length / 1000000, 2)
        Write-Host "JumpCloud ZIP package: $packageZip"
        Write-Host "ZIP size: $zipMiB MiB ($zipMB MB)"
        if ($zipFile.Length -lt 150MB) {
            Write-Host 'ZIP is under 150 MiB.'
        }
        else {
            Write-Warning 'ZIP is over 150 MiB.'
        }
    }

    exit 0
}
catch {
    Write-Error "ZenIT JumpCloud package failed. $($_.Exception.Message)"
    exit 1
}
