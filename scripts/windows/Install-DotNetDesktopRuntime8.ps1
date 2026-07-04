param(
    [string]$InstallerUrl = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe'
)

$ErrorActionPreference = 'Stop'

$logDirectory = 'C:\ProgramData\ZenIT\Logs'
$logPath = Join-Path $logDirectory 'DotNetRuntimeInstall.log'

function Write-InstallLog {
    param([string]$Message)

    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    "$timestamp $Message" | Out-File -FilePath $logPath -Append -Encoding UTF8
    Write-Host $Message
}

try {
    Write-InstallLog 'Checking for .NET 8 Desktop Runtime x64.'

    $runtimeInstalled = $false
    $dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
    if ($dotnet) {
        $runtimes = & $dotnet.Source --list-runtimes 2>$null
        $runtimeInstalled = $runtimes -match '^Microsoft\.WindowsDesktop\.App 8\.'
    }

    if ($runtimeInstalled) {
        Write-InstallLog '.NET 8 Desktop Runtime is already installed.'
        exit 0
    }

    $tempInstaller = Join-Path $env:TEMP 'ZenIT-windowsdesktop-runtime-8-win-x64.exe'
    Write-InstallLog "Downloading .NET 8 Desktop Runtime from $InstallerUrl"
    Invoke-WebRequest -Uri $InstallerUrl -OutFile $tempInstaller -UseBasicParsing

    Write-InstallLog 'Installing .NET 8 Desktop Runtime silently.'
    $process = Start-Process -FilePath $tempInstaller -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Runtime installer failed with exit code $($process.ExitCode)."
    }

    Write-InstallLog '.NET 8 Desktop Runtime installation completed.'
    exit 0
}
catch {
    Write-InstallLog "ERROR: $($_.Exception.Message)"
    exit 1
}

