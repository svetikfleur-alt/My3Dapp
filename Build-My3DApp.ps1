param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$UiHost = "Avalonia",
    [int]$MaxAttempts = 4,
    [int]$StepTimeoutSeconds = 240
)

$ErrorActionPreference = "Stop"

if ($UiHost -ne "Avalonia") {
    throw "Only Avalonia UI host is supported."
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $root "My3DApp.csproj"

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

# NuGet network retries (applies to dotnet restore/publish)
$env:NUGET_ENHANCED_MAX_NETWORK_TRY_COUNT = "8"
$env:NUGET_ENHANCED_NETWORK_RETRY_DELAY_MILLISECONDS = "1500"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = "1"

function ConvertTo-ArgumentLine {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    return ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_ -replace '"', '\"') + '"'
        } else {
            $_
        }
    }) -join ' '
}

function Stop-ProcessTree {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId
    )

    try {
        $taskkill = Start-Process -FilePath "taskkill.exe" -ArgumentList @("/PID", $ProcessId, "/T", "/F") -PassThru -NoNewWindow -WindowStyle Hidden
        [void]$taskkill.WaitForExit(10000)
    }
    catch {
        try { Stop-Process -Id $ProcessId -Force -ErrorAction Stop } catch { }
    }
}

function Invoke-DotnetStep {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$StepName
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        Write-Host "$StepName (attempt $attempt/$MaxAttempts)..." -ForegroundColor Cyan
        & dotnet build-server shutdown | Out-Null

        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = "dotnet"
        $startInfo.Arguments = ConvertTo-ArgumentLine -Arguments $Arguments
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $false
        $startInfo.RedirectStandardError = $false

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        [void]$process.Start()

        $timedOut = -not $process.WaitForExit($StepTimeoutSeconds * 1000)

        if ($timedOut) {
            Stop-ProcessTree -ProcessId $process.Id
            Write-Warning "$StepName timed out after $StepTimeoutSeconds seconds."
        } else {
            $exitCode = [int]$process.ExitCode
            if ($exitCode -eq 0) {
                return
            }

            Write-Warning "$StepName failed with exit code $exitCode."
        }

        if ($attempt -lt $MaxAttempts) {
            Write-Host "Clearing NuGet caches before retry..." -ForegroundColor Yellow
            & dotnet nuget locals http-cache --clear | Out-Null
            & dotnet nuget locals temp --clear | Out-Null
            Start-Sleep -Seconds 2
        }
    }

    throw "$StepName failed after $MaxAttempts attempts."
}

function Invoke-BuildForHost {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Avalonia")]
        [string]$HostName
    )

    $runningApp = Get-Process -Name "My3DApp" -ErrorAction SilentlyContinue
    if ($runningApp) {
        Write-Host "Stopping running My3DApp processes to avoid file lock collisions..." -ForegroundColor Yellow
        $runningApp | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 300
    }

    Invoke-DotnetStep -StepName "restore" -Arguments @(
        "restore", $projectPath,
        "--disable-parallel",
        "-p:NuGetAudit=false",
        "-p:RestoreIgnoreFailedSources=true",
        "-p:StudioUiHost=$HostName"
    )

    Invoke-DotnetStep -StepName "build" -Arguments @(
        "build", $projectPath,
        "-c", $Configuration,
        "--no-restore",
        "--disable-build-servers",
        "-p:UseSharedCompilation=false",
        "-p:BuildInParallel=false",
        "-nodeReuse:false",
        "-maxcpucount:1",
        "-p:StudioUiHost=$HostName"
    )
}

Invoke-BuildForHost -HostName $UiHost

Write-Host ""
Write-Host "UI host: $UiHost" -ForegroundColor DarkGray
Write-Host "Build completed successfully." -ForegroundColor Green
