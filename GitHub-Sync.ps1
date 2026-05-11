[CmdletBinding()]
param(
    [string]$Remote = "origin",
    [string]$Branch = "phone-cloud-sync",
    [string]$MirrorFolder = "cloud_sync_mirror",
    [switch]$NoPush,
    [switch]$NoFetch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $repoRoot "logs"
$logPath = Join-Path $logDir "github-sync.log"
$mirrorRoot = Join-Path $repoRoot $MirrorFolder

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Write-Status {
    param(
        [string]$Message,
        [ConsoleColor]$Color = [ConsoleColor]::Gray
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "[{0}] {1}" -f $timestamp, $Message
    Write-Host $line -ForegroundColor $Color
    Add-Content -LiteralPath $logPath -Value $line
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [string]$WorkingDirectory = $repoRoot,
        [switch]$AllowFailure
    )

    $commandText = "git {0}" -f ($Arguments -join " ")
    Write-Status "RUN  $commandText" DarkGray

    Push-Location -LiteralPath $WorkingDirectory
    try {
        & git @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "Command failed ($exitCode): $commandText"
    }

    return $exitCode
}

function Ensure-MirrorRepository {
    param(
        [string]$RemoteUrl
    )

    if (-not (Test-Path -LiteralPath $mirrorRoot)) {
        New-Item -ItemType Directory -Path $mirrorRoot | Out-Null
    }

    $mirrorGitPath = Join-Path $mirrorRoot ".git"
    if (-not (Test-Path -LiteralPath $mirrorGitPath)) {
        Write-Status "Initializing cloud mirror repository..." Yellow
        Invoke-Git -Arguments @("init", "-b", $Branch) -WorkingDirectory $mirrorRoot
        Invoke-Git -Arguments @("remote", "add", $Remote, $RemoteUrl) -WorkingDirectory $mirrorRoot
    }
}

function Test-MirrorRemoteBranchExists {
    Push-Location -LiteralPath $mirrorRoot
    try {
        & git show-ref --verify --quiet ("refs/remotes/{0}/{1}" -f $Remote, $Branch)
        return ($LASTEXITCODE -eq 0)
    }
    finally {
        Pop-Location
    }
}

function Get-DesiredRelativeFiles {
    $tracked = @(& git -c core.quotepath=false ls-files)
    $untracked = @(& git -c core.quotepath=false ls-files --others --exclude-standard)
    $combined = @($tracked + $untracked) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    return $combined | Sort-Object -Unique
}

function Sync-FilesToMirror {
    param(
        [string[]]$RelativeFiles
    )

    $desired = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $RelativeFiles) {
        [void]$desired.Add($file.Replace("/", "\"))
    }

    $currentMirrorFiles = Get-ChildItem -LiteralPath $mirrorRoot -Recurse -File -Force |
        Where-Object { $_.FullName -notlike (Join-Path $mirrorRoot ".git*") }

    foreach ($mirrorFile in $currentMirrorFiles) {
        $relativePath = $mirrorFile.FullName.Substring($mirrorRoot.Length).TrimStart("\")
        if (-not $desired.Contains($relativePath)) {
            Remove-Item -LiteralPath $mirrorFile.FullName -Force
            Write-Status ("Removed mirror-only file: {0}" -f $relativePath) DarkYellow
        }
    }

    foreach ($relativeFile in $RelativeFiles) {
        $normalized = $relativeFile.Replace("/", "\")
        $sourcePath = Join-Path $repoRoot $normalized
        $targetPath = Join-Path $mirrorRoot $normalized
        $targetDir = Split-Path -Parent $targetPath
        if (-not (Test-Path -LiteralPath $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }

        if ((Test-Path -LiteralPath $sourcePath) -and -not (Get-Item -LiteralPath $sourcePath).PSIsContainer) {
            Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
        }
    }

    Get-ChildItem -LiteralPath $mirrorRoot -Recurse -Directory -Force |
        Sort-Object FullName -Descending |
        Where-Object { $_.FullName -notlike (Join-Path $mirrorRoot ".git*") } |
        ForEach-Object {
            if (-not (Get-ChildItem -LiteralPath $_.FullName -Force | Select-Object -First 1)) {
                Remove-Item -LiteralPath $_.FullName -Force
            }
        }
}

Ensure-Directory -Path $logDir
Set-Location -LiteralPath $repoRoot

Write-Status "==================================================" Cyan
Write-Status "GitHub auto-sync start" Cyan
Write-Status ("Repo   : {0}" -f $repoRoot) Cyan
Write-Status ("Target : {0}/{1}" -f $Remote, $Branch) Cyan
Write-Status ("Mirror : {0}" -f $mirrorRoot) Cyan
Write-Status "==================================================" Cyan

$insideWorkTree = (& git rev-parse --is-inside-work-tree 2>$null)
if ($LASTEXITCODE -ne 0 -or $insideWorkTree.Trim() -ne "true") {
    throw "Current folder is not a git work tree."
}

$currentBranch = (& git branch --show-current).Trim()
if ([string]::IsNullOrWhiteSpace($currentBranch)) {
    throw "Detached HEAD is not supported for auto-sync."
}

$gitUserName = (& git config user.name).Trim()
$gitUserEmail = (& git config user.email).Trim()
if ([string]::IsNullOrWhiteSpace($gitUserName)) {
    & git config user.name "Lena Auto Sync"
}
if ([string]::IsNullOrWhiteSpace($gitUserEmail)) {
    & git config user.email "lena-auto-sync@local"
}

$mergeHeadExists = Test-Path -LiteralPath (Join-Path $repoRoot ".git\MERGE_HEAD")
$rebaseApplyExists = Test-Path -LiteralPath (Join-Path $repoRoot ".git\rebase-apply")
$rebaseMergeExists = Test-Path -LiteralPath (Join-Path $repoRoot ".git\rebase-merge")
if ($mergeHeadExists -or $rebaseApplyExists -or $rebaseMergeExists) {
    throw "Merge or rebase is already in progress. Resolve it before auto-sync continues."
}

$remoteUrl = (& git remote get-url $Remote).Trim()
if ([string]::IsNullOrWhiteSpace($remoteUrl)) {
    throw "Remote '$Remote' does not have a URL."
}

Ensure-MirrorRepository -RemoteUrl $remoteUrl

if (-not $NoFetch) {
    Write-Status "Fetching mirror branch from GitHub..." Yellow
    Invoke-Git -Arguments @("fetch", $Remote, $Branch) -WorkingDirectory $mirrorRoot -AllowFailure

    $remoteBranchExists = Test-MirrorRemoteBranchExists
    if ($remoteBranchExists) {
        Invoke-Git -Arguments @("checkout", "-B", $Branch, ("{0}/{1}" -f $Remote, $Branch)) -WorkingDirectory $mirrorRoot
    }
    else {
        Invoke-Git -Arguments @("checkout", "-B", $Branch) -WorkingDirectory $mirrorRoot
    }
}
else {
    Write-Status "Skipping remote fetch by request." DarkYellow
    Invoke-Git -Arguments @("checkout", "-B", $Branch) -WorkingDirectory $mirrorRoot
}

$relativeFiles = Get-DesiredRelativeFiles
Write-Status ("Syncing {0} files into cloud mirror..." -f $relativeFiles.Count) Yellow
Sync-FilesToMirror -RelativeFiles $relativeFiles

Invoke-Git -Arguments @("add", "-A") -WorkingDirectory $mirrorRoot

$mirrorHasChanges = $false
Push-Location -LiteralPath $mirrorRoot
try {
    & git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        $mirrorHasChanges = $true
    }
}
finally {
    Pop-Location
}

if ($mirrorHasChanges) {
    $commitMessage = "cloud-sync: {0}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    Write-Status ("Committing mirror snapshot as '{0}'" -f $commitMessage) Yellow
    Invoke-Git -Arguments @("commit", "-m", $commitMessage) -WorkingDirectory $mirrorRoot
}
else {
    Write-Status "Mirror already matches local folder." DarkYellow
}

if (-not $NoPush) {
    Write-Status "Pushing mirror branch to GitHub..." Yellow
    Invoke-Git -Arguments @("push", $Remote, "$Branch") -WorkingDirectory $mirrorRoot
}
else {
    Write-Status "Skipping push by request." DarkYellow
}

Write-Status "GitHub auto-sync completed successfully." Green
Write-Status "==================================================" Cyan
