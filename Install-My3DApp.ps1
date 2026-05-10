param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [string]$UiHost = "Avalonia",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SelfContained,
    [switch]$NoShortcuts,
    [switch]$CloseRunningInstances,
    [int]$MaxAttempts = 4,
    [int]$StepTimeoutSeconds = 240
)

$ErrorActionPreference = "Stop"

if ($UiHost -ne "Avalonia") {
    throw "Only Avalonia UI host is supported."
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $root "My3DApp.csproj"
$installDir = Join-Path $root "dist\My3DApp"
$installExePath = Join-Path $installDir "My3DApp.exe"
$selfContainedValue = if ($SelfContained.IsPresent) { "true" } else { "false" }

# NuGet network retries (applies to dotnet restore/publish)
$env:NUGET_ENHANCED_MAX_NETWORK_TRY_COUNT = "8"
$env:NUGET_ENHANCED_NETWORK_RETRY_DELAY_MILLISECONDS = "1500"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = "1"

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

$runningInstalledInstances = @(Get-Process My3DApp -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $installExePath })
if ($runningInstalledInstances.Count -gt 0) {
    if (-not $CloseRunningInstances.IsPresent) {
        throw "My3DApp is currently running from '$installExePath'. Close it first or rerun with -CloseRunningInstances."
    }

    Write-Host "Closing running My3DApp instances..." -ForegroundColor Yellow
    foreach ($process in $runningInstalledInstances) {
        Stop-Process -Id $process.Id -Force
    }
    Start-Sleep -Milliseconds 300
}

function Invoke-DotnetStep {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$StepName
    )

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

function Invoke-PublishForHost {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Avalonia")]
        [string]$HostName
    )

    Invoke-DotnetStep -StepName "restore" -Arguments @(
        "restore", $projectPath,
        "--disable-parallel",
        "-r", $RuntimeIdentifier,
        "-p:NuGetAudit=false",
        "-p:RestoreIgnoreFailedSources=true",
        "-p:StudioUiHost=$HostName"
    )

    Invoke-DotnetStep -StepName "publish" -Arguments @(
        "publish", $projectPath,
        "-c", $Configuration,
        "-r", $RuntimeIdentifier,
        "--self-contained", $selfContainedValue,
        "--no-restore",
        "--disable-build-servers",
        "-p:UseSharedCompilation=false",
        "-p:BuildInParallel=false",
        "-nodeReuse:false",
        "-maxcpucount:1",
        "-o", $installDir,
        "-p:StudioUiHost=$HostName"
    )
}

Invoke-PublishForHost -HostName $UiHost

$exePath = $installExePath
if (-not (Test-Path $exePath)) {
    throw "Published executable not found: $exePath"
}

if (-not $NoShortcuts.IsPresent) {
    Write-Host "Creating shortcuts..." -ForegroundColor Cyan

    $shell = New-Object -ComObject WScript.Shell
    $desktopPath = [Environment]::GetFolderPath("DesktopDirectory")
    $programsPath = [Environment]::GetFolderPath("Programs")
    $startFolder = Join-Path $programsPath "My3DApp"

    if (-not (Test-Path $startFolder)) {
        New-Item -Path $startFolder -ItemType Directory | Out-Null
    }

    $shortcutTargets = @(
        (Join-Path $desktopPath "My3DApp Studio.lnk"),
        (Join-Path $startFolder "My3DApp Studio.lnk")
    )

    foreach ($shortcutPath in $shortcutTargets) {
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $exePath
        $shortcut.WorkingDirectory = $installDir
        $shortcut.IconLocation = "$exePath,0"
        $shortcut.Description = "My3DApp Solid CAD Studio"
        $shortcut.Save()
    }
}

Write-Host ""
Write-Host "UI host: $UiHost" -ForegroundColor DarkGray
Write-Host "My3DApp Studio is ready." -ForegroundColor Green
Write-Host "Executable: $exePath"
if ($NoShortcuts.IsPresent) {
    Write-Host "Shortcuts were skipped by -NoShortcuts."
} else {
    Write-Host "Shortcuts created on Desktop and Start Menu."
}
