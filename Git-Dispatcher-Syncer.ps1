[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Command = "status",

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Args
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
if ($null -eq $Args) {
    $Args = @()
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$remote = if ([string]::IsNullOrWhiteSpace($env:GIT_DISPATCH_REMOTE)) { "origin" } else { $env:GIT_DISPATCH_REMOTE }
$branchPrefix = if ([string]::IsNullOrWhiteSpace($env:GIT_DISPATCH_BRANCH_PREFIX)) { "agentops" } else { $env:GIT_DISPATCH_BRANCH_PREFIX }
$branchPrefix = $branchPrefix.Trim().Trim([char[]]@('/'))
if ([string]::IsNullOrWhiteSpace($branchPrefix)) {
    $branchPrefix = "agentops"
}

$maxParallel = 4
$parsedMaxParallel = 0
if ([int]::TryParse($env:GIT_DISPATCH_MAX_PARALLEL, [ref]$parsedMaxParallel) -and $parsedMaxParallel -gt 0) {
    $maxParallel = $parsedMaxParallel
}

$autoPush = ($env:GIT_DISPATCH_AUTO_PUSH -eq "1" -or $env:GIT_DISPATCH_AUTO_PUSH -ieq "true")
$taskNameDefault = if ([string]::IsNullOrWhiteSpace($env:GIT_DISPATCH_TASK_NAME)) { "My3DApp Git Dispatcher Syncer" } else { $env:GIT_DISPATCH_TASK_NAME }

function Format-CommandArgument {
    param([string]$Value)

    if ($null -eq $Value) {
        return '""'
    }

    if ($Value -match '[\s"&<>|^]') {
        return '"' + ($Value -replace '"', '\"') + '"'
    }

    return $Value
}

function Get-RepoRoot {
    Push-Location -LiteralPath $scriptRoot
    try {
        $output = @(& git rev-parse --show-toplevel 2>$null)
        if ($LASTEXITCODE -ne 0 -or $output.Count -eq 0) {
            throw "Git-Dispatcher-Syncer must be run from inside a git work tree."
        }

        return ([string]$output[0]).Trim()
    }
    finally {
        Pop-Location
    }
}

$repoRoot = Get-RepoRoot
$repoName = Split-Path -Leaf $repoRoot
$rawWorktreeRoot = if ([string]::IsNullOrWhiteSpace($env:GIT_DISPATCH_WORKTREE_ROOT)) {
    Join-Path (Split-Path -Parent $repoRoot) ("{0}-worktrees" -f $repoName)
}
else {
    $env:GIT_DISPATCH_WORKTREE_ROOT
}

$worktreeRoot = if ([System.IO.Path]::IsPathRooted($rawWorktreeRoot)) {
    [System.IO.Path]::GetFullPath($rawWorktreeRoot)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $rawWorktreeRoot))
}

$stateDir = Join-Path $repoRoot "logs"
$statePath = Join-Path $stateDir "git-dispatcher-syncer.state.json"
$logPath = Join-Path $stateDir "git-dispatcher-syncer.log"
$lockPath = Join-Path $stateDir "git-dispatcher-syncer.lock"

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Write-Status {
    param(
        [string]$Message,
        [ConsoleColor]$Color = [ConsoleColor]::Gray
    )

    Write-Host $Message -ForegroundColor $Color

    try {
        if (-not [string]::IsNullOrWhiteSpace($script:logPath)) {
            Ensure-Directory -Path (Split-Path -Parent $script:logPath)
            $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            Add-Content -LiteralPath $script:logPath -Value ("[{0}] {1}" -f $timestamp, $Message)
        }
    }
    catch {
        # Logging must never break git orchestration.
    }
}

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [string]$WorkingDirectory = $repoRoot,
        [switch]$Capture,
        [switch]$AllowFailure,
        [switch]$Quiet
    )

    if (-not $Quiet) {
        $displayArgs = ($Arguments | ForEach-Object { Format-CommandArgument $_ }) -join " "
        Write-Status ("RUN  git {0}" -f $displayArgs) DarkGray
    }

    Push-Location -LiteralPath $WorkingDirectory
    try {
        if ($Capture) {
            $previousErrorActionPreference = $ErrorActionPreference
            try {
                $ErrorActionPreference = "Continue"
                $rawOutput = @(& git @Arguments 2>&1)
                $exitCode = $LASTEXITCODE
            }
            finally {
                $ErrorActionPreference = $previousErrorActionPreference
            }

            $output = @($rawOutput | ForEach-Object { $_.ToString() })

            if ($exitCode -ne 0 -and -not $AllowFailure) {
                $message = "Command failed ({0}): git {1}" -f $exitCode, ($Arguments -join " ")
                if ($output.Count -gt 0) {
                    $message = $message + [Environment]::NewLine + ($output -join [Environment]::NewLine)
                }

                throw $message
            }

            return [pscustomobject]@{
                ExitCode = $exitCode
                Output = $output
            }
        }

        & git @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw ("Command failed ({0}): git {1}" -f $exitCode, ($Arguments -join " "))
    }

    return $exitCode
}

function Get-TextOutput {
    param([object]$Result)

    return (@($Result.Output) -join [Environment]::NewLine).Trim()
}

function Get-GitWorktrees {
    $result = Invoke-Git -Arguments @("worktree", "list", "--porcelain") -Capture -Quiet
    $items = New-Object System.Collections.Generic.List[object]
    $current = @{}

    foreach ($lineObject in @($result.Output)) {
        $line = [string]$lineObject
        if ([string]::IsNullOrWhiteSpace($line)) {
            if ($current.ContainsKey("Path")) {
                $branch = if ($current.ContainsKey("Branch")) { [string]$current["Branch"] } else { "(detached)" }
                $head = if ($current.ContainsKey("Head")) { [string]$current["Head"] } else { "" }
                $detached = $current.ContainsKey("Detached")
                $bare = $current.ContainsKey("Bare")

                $items.Add([pscustomobject]@{
                    Path = [string]$current["Path"]
                    Branch = $branch
                    Head = $head
                    Detached = $detached
                    Bare = $bare
                }) | Out-Null
            }

            $current = @{}
            continue
        }

        $parts = $line -split " ", 2
        $key = $parts[0]
        $value = if ($parts.Count -gt 1) { $parts[1] } else { "" }

        switch ($key) {
            "worktree" { $current["Path"] = $value }
            "HEAD" { $current["Head"] = $value }
            "branch" {
                if ($value.StartsWith("refs/heads/", [System.StringComparison]::OrdinalIgnoreCase)) {
                    $current["Branch"] = $value.Substring("refs/heads/".Length)
                }
                else {
                    $current["Branch"] = $value
                }
            }
            "detached" { $current["Detached"] = $true }
            "bare" { $current["Bare"] = $true }
        }
    }

    if ($current.ContainsKey("Path")) {
        $branch = if ($current.ContainsKey("Branch")) { [string]$current["Branch"] } else { "(detached)" }
        $head = if ($current.ContainsKey("Head")) { [string]$current["Head"] } else { "" }
        $detached = $current.ContainsKey("Detached")
        $bare = $current.ContainsKey("Bare")

        $items.Add([pscustomobject]@{
            Path = [string]$current["Path"]
            Branch = $branch
            Head = $head
            Detached = $detached
            Bare = $bare
        }) | Out-Null
    }

    return $items.ToArray()
}

function Test-WorktreeClean {
    param([string]$Path)

    $result = Invoke-Git -Arguments @("status", "--porcelain") -WorkingDirectory $Path -Capture -Quiet
    return (@($result.Output).Count -eq 0)
}

function Get-CurrentBranch {
    param([string]$Path)

    $result = Invoke-Git -Arguments @("branch", "--show-current") -WorkingDirectory $Path -Capture -Quiet
    return (Get-TextOutput -Result $result)
}

function Get-UpstreamBranch {
    param([string]$Path)

    $result = Invoke-Git -Arguments @("rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}") -WorkingDirectory $Path -Capture -AllowFailure -Quiet
    if ($result.ExitCode -ne 0) {
        return ""
    }

    return (Get-TextOutput -Result $result)
}

function Test-GitRef {
    param([string]$Ref)

    $exitCode = Invoke-Git -Arguments @("show-ref", "--verify", "--quiet", $Ref) -AllowFailure -Quiet
    return ($exitCode -eq 0)
}

function Test-GitOperationPath {
    param(
        [string]$Path,
        [string]$Name
    )

    $result = Invoke-Git -Arguments @("rev-parse", "--git-path", $Name) -WorkingDirectory $Path -Capture -AllowFailure -Quiet
    if ($result.ExitCode -ne 0) {
        return $false
    }

    $operationPath = Get-TextOutput -Result $result
    if ([string]::IsNullOrWhiteSpace($operationPath)) {
        return $false
    }

    if (-not [System.IO.Path]::IsPathRooted($operationPath)) {
        $operationPath = Join-Path $Path $operationPath
    }

    return (Test-Path -LiteralPath $operationPath)
}

function Get-WorktreeHealth {
    param([object]$Worktree)

    $reasons = New-Object System.Collections.Generic.List[string]
    $statusLines = @()

    if ($Worktree.Bare) {
        $reasons.Add("bare") | Out-Null
    }

    if ($Worktree.Detached -or $Worktree.Branch -eq "(detached)") {
        $reasons.Add("detached") | Out-Null
    }

    if (-not $Worktree.Bare) {
        $statusResult = Invoke-Git -Arguments @("status", "--porcelain") -WorkingDirectory $Worktree.Path -Capture -AllowFailure -Quiet
        if ($statusResult.ExitCode -ne 0) {
            $reasons.Add("status-failed") | Out-Null
        }
        else {
            $statusLines = @($statusResult.Output)
            foreach ($statusLine in $statusLines) {
                if ($statusLine -match '^(DD|AU|UD|UA|DU|AA|UU)') {
                    $reasons.Add("unmerged") | Out-Null
                    break
                }
            }
        }

        foreach ($operationName in @("MERGE_HEAD", "rebase-apply", "rebase-merge", "CHERRY_PICK_HEAD", "REVERT_HEAD")) {
            if (Test-GitOperationPath -Path $Worktree.Path -Name $operationName) {
                $reasons.Add($operationName.ToLowerInvariant()) | Out-Null
            }
        }
    }

    $isWorking = ($reasons.Count -eq 0)
    $isClean = ($isWorking -and $statusLines.Count -eq 0)
    $state = if (-not $isWorking) { "blocked" } elseif ($isClean) { "clean" } else { "dirty" }

    return [pscustomobject]@{
        IsWorking = $isWorking
        IsClean = $isClean
        State = $state
        Reasons = @($reasons)
    }
}

function ConvertTo-WorktreeFolderName {
    param([string]$Branch)

    $safe = $Branch -replace '[\\/:*?"<>|]+', '-'
    $safe = $safe -replace '\s+', '-'
    $safe = $safe.Trim('-')
    if ([string]::IsNullOrWhiteSpace($safe)) {
        throw "Branch name cannot be converted to a folder name."
    }

    return $safe
}

function Resolve-TargetPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function ConvertTo-Slug {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return "task"
    }

    $normalized = $Text.Normalize([System.Text.NormalizationForm]::FormD)
    $builder = New-Object System.Text.StringBuilder
    foreach ($character in $normalized.ToCharArray()) {
        $category = [System.Globalization.CharUnicodeInfo]::GetUnicodeCategory($character)
        if ($category -ne [System.Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$builder.Append($character)
        }
    }

    $slug = $builder.ToString().Normalize([System.Text.NormalizationForm]::FormC).ToLowerInvariant()
    $slug = $slug -replace '[^a-z0-9]+', '-'
    $slug = $slug.Trim('-')
    if ([string]::IsNullOrWhiteSpace($slug)) {
        $slug = "task"
    }

    if ($slug.Length -gt 54) {
        $slug = $slug.Substring(0, 54).Trim('-')
    }

    return $slug
}

function Get-TaskBranchName {
    param(
        [int]$Number,
        [string]$Title
    )

    return ("{0}/task-{1:D2}-{2}" -f $branchPrefix, $Number, (ConvertTo-Slug -Text $Title))
}

function Get-AgentOpsOpenTasks {
    $queuePath = Join-Path $repoRoot "AgentOps\TASK_QUEUE.md"
    if (-not (Test-Path -LiteralPath $queuePath)) {
        return @()
    }

    $tasks = New-Object System.Collections.Generic.List[object]
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $queuePath -Encoding UTF8) {
        $lineNumber++
        if ($line -notmatch '^\s*-\s+\[\s\]\s+(?<Number>\d+)\s+(?:-|\u2013|\u2014)\s+(?<Rest>.+)$') {
            continue
        }

        $number = [int]$Matches.Number
        $rest = ([string]$Matches.Rest).Trim()
        $separator = [regex]::Match($rest, '\s+(?:->|\u2192)\s+')
        if ($separator.Success) {
            $title = $rest.Substring(0, $separator.Index).Trim()
            $spec = $rest.Substring($separator.Index + $separator.Length).Trim()
        }
        else {
            $title = $rest
            $spec = ""
        }

        $branch = Get-TaskBranchName -Number $number -Title $title
        $folderName = ConvertTo-WorktreeFolderName -Branch $branch
        $tasks.Add([pscustomobject]@{
            Number = $number
            Title = $title
            Spec = $spec
            Branch = $branch
            WorktreePath = Join-Path $worktreeRoot $folderName
            QueueLine = $lineNumber
        }) | Out-Null
    }

    return @($tasks | Sort-Object Number)
}

function Get-ManagedWorktrees {
    $prefix = "$branchPrefix/"
    return @(Get-GitWorktrees | Where-Object {
        -not $_.Bare -and $_.Branch.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
    })
}

function Get-BaseRef {
    if (-not [string]::IsNullOrWhiteSpace($env:GIT_DISPATCH_BASE)) {
        return $env:GIT_DISPATCH_BASE
    }

    $branch = Get-CurrentBranch -Path $repoRoot
    if ([string]::IsNullOrWhiteSpace($branch)) {
        return "HEAD"
    }

    return $branch
}

function Save-DispatcherState {
    param([object[]]$Tasks)

    Ensure-Directory -Path $stateDir
    $worktreeStates = New-Object System.Collections.Generic.List[object]
    foreach ($worktree in @(Get-GitWorktrees | Where-Object { -not $_.Bare })) {
        $health = Get-WorktreeHealth -Worktree $worktree
        $worktreeStates.Add([pscustomobject]@{
            Path = $worktree.Path
            Branch = $worktree.Branch
            State = $health.State
            Reasons = @($health.Reasons)
        }) | Out-Null
    }

    $state = [pscustomobject]@{
        LastRunUtc = (Get-Date).ToUniversalTime().ToString("o")
        RepoRoot = $repoRoot
        Remote = $remote
        BranchPrefix = $branchPrefix
        MaxParallel = $maxParallel
        WorktreeRoot = $worktreeRoot
        AutoPush = $autoPush
        OpenTasks = @($Tasks | ForEach-Object {
            [pscustomobject]@{
                Number = $_.Number
                Title = $_.Title
                Spec = $_.Spec
                Branch = $_.Branch
                WorktreePath = $_.WorktreePath
                QueueLine = $_.QueueLine
            }
        })
        Worktrees = @($worktreeStates.ToArray())
    }

    $state | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $statePath -Encoding UTF8
}

function Show-Help {
    Write-Host "My3DApp Git Dispatcher Syncer"
    Write-Host ""
    Write-Host "Usage:"
    Write-Host "  Git-Dispatcher-Syncer.bat status"
    Write-Host "  Git-Dispatcher-Syncer.bat tasks"
    Write-Host "  Git-Dispatcher-Syncer.bat smart"
    Write-Host "  Git-Dispatcher-Syncer.bat scheduled-run"
    Write-Host "  Git-Dispatcher-Syncer.bat install-schedule [hours] [task-name]"
    Write-Host "  Git-Dispatcher-Syncer.bat uninstall-schedule [task-name]"
    Write-Host "  Git-Dispatcher-Syncer.bat sync"
    Write-Host "  Git-Dispatcher-Syncer.bat sync-all"
    Write-Host "  Git-Dispatcher-Syncer.bat worktree <branch> [path]"
    Write-Host "  Git-Dispatcher-Syncer.bat push-current"
    Write-Host "  Git-Dispatcher-Syncer.bat push-managed"
    Write-Host "  Git-Dispatcher-Syncer.bat dispatch <safe-git-args>"
    Write-Host ""
    Write-Host "Defaults:"
    Write-Host "  Remote: $remote (override with GIT_DISPATCH_REMOTE)"
    Write-Host "  Branch prefix: $branchPrefix (override with GIT_DISPATCH_BRANCH_PREFIX)"
    Write-Host "  Max parallel tasks: $maxParallel (override with GIT_DISPATCH_MAX_PARALLEL)"
    Write-Host "  Worktree root: $worktreeRoot (override with GIT_DISPATCH_WORKTREE_ROOT)"
    Write-Host "  Auto-push: $autoPush (set GIT_DISPATCH_AUTO_PUSH=1 to enable)"
    Write-Host "  Schedule: every 2 hours by default, running smart mode"
    Write-Host ""
    Write-Host "Smart mode:"
    Write-Host "  Reads open AgentOps/TASK_QUEUE.md items."
    Write-Host "  Creates one branch/worktree per open task until max parallel is reached."
    Write-Host "  Refuses new branches when any worktree is blocked by merge/rebase/conflicts."
    Write-Host "  Pulls only clean worktrees with --ff-only; dirty worktrees are left alone."
    Write-Host ""
    Write-Host "Safety:"
    Write-Host "  dispatch refuses broad destructive git commands unless GIT_DISPATCHER_ALLOW_DANGEROUS=1."
}

function Show-RepositoryStatus {
    Write-Status "Repository:" Cyan
    Write-Host "  $repoRoot"
    Write-Host ""

    $statusResult = Invoke-Git -Arguments @("status", "-sb") -Capture -Quiet
    foreach ($statusLine in @($statusResult.Output)) {
        Write-Host $statusLine
    }
    Write-Host ""

    Write-Status "Worktrees:" Cyan
    $worktrees = @(Get-GitWorktrees)
    foreach ($worktree in $worktrees) {
        $health = Get-WorktreeHealth -Worktree $worktree
        $reason = if (@($health.Reasons).Count -gt 0) { ": " + (@($health.Reasons) -join ",") } else { "" }
        Write-Host ("  {0,-46} {1} [{2}{3}]" -f $worktree.Branch, $worktree.Path, $health.State, $reason)
    }
}

function Show-AgentOpsTasks {
    $tasks = @(Get-AgentOpsOpenTasks)
    if ($tasks.Count -eq 0) {
        Write-Status "No open AgentOps tasks found." DarkYellow
        return
    }

    Write-Status ("Open AgentOps tasks: {0}" -f $tasks.Count) Cyan
    foreach ($task in $tasks) {
        Write-Host ("  {0:D2}  {1}" -f $task.Number, $task.Title)
        Write-Host ("      branch  : {0}" -f $task.Branch)
        Write-Host ("      worktree: {0}" -f $task.WorktreePath)
    }
}

function Sync-Worktree {
    param(
        [string]$Path,
        [switch]$SkipDirty
    )

    $worktree = @(Get-GitWorktrees | Where-Object { $_.Path -ieq $Path } | Select-Object -First 1)
    if ($worktree.Count -eq 0) {
        $branch = Get-CurrentBranch -Path $Path
        $worktree = @([pscustomobject]@{
            Path = $Path
            Branch = if ([string]::IsNullOrWhiteSpace($branch)) { "(detached)" } else { $branch }
            Head = ""
            Detached = [string]::IsNullOrWhiteSpace($branch)
            Bare = $false
        })
    }

    $branchLabel = $worktree[0].Branch
    Write-Host ""
    Write-Status ("Sync: {0} [{1}]" -f $Path, $branchLabel) Cyan

    $health = Get-WorktreeHealth -Worktree $worktree[0]
    if (-not $health.IsWorking) {
        $message = "Worktree is blocked: {0} ({1})" -f $Path, (@($health.Reasons) -join ",")
        if ($SkipDirty) {
            Write-Status $message DarkYellow
            return
        }

        throw $message
    }

    if (-not $health.IsClean) {
        $message = "Worktree has uncommitted changes: $Path"
        if ($SkipDirty) {
            Write-Status ("Skipping dirty worktree: {0}" -f $Path) DarkYellow
            return
        }

        throw $message
    }

    if ($SkipDirty) {
        $fetchResult = Invoke-Git -Arguments @("fetch", $remote, "--prune") -WorkingDirectory $Path -Capture -AllowFailure
        if ($fetchResult.ExitCode -ne 0) {
            Write-Status ("Fetch failed in {0}; skipping remote sync for now." -f $Path) DarkYellow
            return
        }
    }
    else {
        Invoke-Git -Arguments @("fetch", $remote, "--prune") -WorkingDirectory $Path | Out-Null
    }

    $upstream = Get-UpstreamBranch -Path $Path
    if ([string]::IsNullOrWhiteSpace($upstream)) {
        Write-Status ("No upstream configured for {0}; leaving it local." -f $branchLabel) DarkYellow
        return
    }

    Write-Status ("Upstream: {0}" -f $upstream) DarkGray
    Invoke-Git -Arguments @("pull", "--ff-only") -WorkingDirectory $Path | Out-Null
}

function Sync-AllWorktrees {
    $worktrees = @(Get-GitWorktrees | Where-Object { -not $_.Bare })
    foreach ($worktree in $worktrees) {
        Sync-Worktree -Path $worktree.Path -SkipDirty
    }
}

function New-BranchWorktree {
    param(
        [string]$Branch,
        [string]$TargetPath,
        [string]$BaseRef = "HEAD",
        [switch]$CreateParent
    )

    if ([string]::IsNullOrWhiteSpace($Branch)) {
        throw "Missing branch name."
    }

    $existingWorktree = @(Get-GitWorktrees | Where-Object { $_.Branch -ieq $Branch } | Select-Object -First 1)
    if ($existingWorktree.Count -gt 0) {
        Write-Status ("Branch already has a worktree: {0}" -f $existingWorktree[0].Path) DarkYellow
        return
    }

    if ([string]::IsNullOrWhiteSpace($TargetPath)) {
        $safeBranch = ConvertTo-WorktreeFolderName -Branch $Branch
        $TargetPath = Join-Path (Split-Path -Parent $repoRoot) ("{0}-{1}" -f $repoName, $safeBranch)
    }
    else {
        $TargetPath = Resolve-TargetPath -Path $TargetPath
    }

    $parent = Split-Path -Parent $TargetPath
    if (-not (Test-Path -LiteralPath $parent)) {
        if ($CreateParent) {
            Ensure-Directory -Path $parent
        }
        else {
            throw "Parent folder does not exist: $parent"
        }
    }

    if (Test-Path -LiteralPath $TargetPath) {
        $firstItem = Get-ChildItem -LiteralPath $TargetPath -Force -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $firstItem) {
            throw "Target path already exists and is not empty: $TargetPath"
        }
    }

    $fetchResult = Invoke-Git -Arguments @("fetch", $remote, "--prune") -Capture -AllowFailure
    if ($fetchResult.ExitCode -ne 0) {
        Write-Status "Fetch failed; continuing with local refs only." DarkYellow
    }

    $localRef = "refs/heads/$Branch"
    $remoteRef = "refs/remotes/$remote/$Branch"

    if (Test-GitRef -Ref $localRef) {
        Invoke-Git -Arguments @("worktree", "add", $TargetPath, $Branch) | Out-Null
    }
    elseif (Test-GitRef -Ref $remoteRef) {
        Invoke-Git -Arguments @("worktree", "add", "--track", "-b", $Branch, $TargetPath, "$remote/$Branch") | Out-Null
    }
    else {
        Invoke-Git -Arguments @("worktree", "add", "-b", $Branch, $TargetPath, $BaseRef) | Out-Null
    }

    Write-Status ("Worktree ready: {0}" -f $TargetPath) Green
}

function Push-WorktreeBranch {
    param(
        [string]$Path,
        [switch]$AllowFailure
    )

    $branch = Get-CurrentBranch -Path $Path
    if ([string]::IsNullOrWhiteSpace($branch)) {
        Write-Status ("Skipping detached worktree push: {0}" -f $Path) DarkYellow
        return
    }

    $worktree = @(Get-GitWorktrees | Where-Object { $_.Path -ieq $Path } | Select-Object -First 1)
    if ($worktree.Count -gt 0) {
        $health = Get-WorktreeHealth -Worktree $worktree[0]
        if (-not $health.IsWorking) {
            Write-Status ("Skipping blocked branch push: {0}" -f $branch) DarkYellow
            return
        }
        if (-not $health.IsClean) {
            Write-Status ("Skipping dirty branch push: {0}" -f $branch) DarkYellow
            return
        }
    }

    $upstream = Get-UpstreamBranch -Path $Path
    $pushArgs = if ([string]::IsNullOrWhiteSpace($upstream)) { @("push", "-u", $remote, $branch) } else { @("push") }
    $invokeParams = @{
        Arguments = $pushArgs
        WorkingDirectory = $Path
    }
    if ($AllowFailure) {
        $invokeParams.AllowFailure = $true
    }

    $exitCode = Invoke-Git @invokeParams
    if ($exitCode -ne 0 -and $AllowFailure) {
        Write-Status ("Push failed for {0} with exit code {1}" -f $branch, $exitCode) DarkYellow
    }
}

function Push-CurrentBranch {
    Push-WorktreeBranch -Path $repoRoot
}

function Push-ManagedBranches {
    param([switch]$AllowFailure)

    foreach ($worktree in @(Get-ManagedWorktrees)) {
        Write-Host ""
        Write-Status ("Push managed branch: {0}" -f $worktree.Branch) Cyan
        Push-WorktreeBranch -Path $worktree.Path -AllowFailure:$AllowFailure
    }
}

function Ensure-TaskWorktree {
    param([object]$Task)

    $existingWorktree = @(Get-GitWorktrees | Where-Object { $_.Branch -ieq $Task.Branch } | Select-Object -First 1)
    if ($existingWorktree.Count -gt 0) {
        Write-Status ("Task {0:D2} already has a worktree: {1}" -f $Task.Number, $existingWorktree[0].Path) DarkGray
        return
    }

    Ensure-Directory -Path $worktreeRoot
    $baseRef = Get-BaseRef
    Write-Host ""
    Write-Status ("Creating task branch {0} from {1}" -f $Task.Branch, $baseRef) Cyan
    New-BranchWorktree -Branch $Task.Branch -TargetPath $Task.WorktreePath -BaseRef $baseRef -CreateParent

    if ($autoPush) {
        Push-WorktreeBranch -Path $Task.WorktreePath -AllowFailure
    }
}

function Invoke-SmartDispatcher {
    Ensure-Directory -Path $stateDir
    Write-Status "Smart dispatcher run started." Cyan

    $tasks = @(Get-AgentOpsOpenTasks)
    Write-Status ("Open AgentOps tasks: {0}" -f $tasks.Count) Cyan

    Sync-AllWorktrees

    $blockedWorktrees = New-Object System.Collections.Generic.List[object]
    foreach ($worktree in @(Get-GitWorktrees | Where-Object { -not $_.Bare })) {
        $health = Get-WorktreeHealth -Worktree $worktree
        if (-not $health.IsWorking) {
            $blockedWorktrees.Add([pscustomobject]@{
                Branch = $worktree.Branch
                Path = $worktree.Path
                Reasons = @($health.Reasons)
            }) | Out-Null
        }
    }

    if ($blockedWorktrees.Count -gt 0) {
        foreach ($blocked in $blockedWorktrees) {
            Write-Status ("Blocked worktree: {0} [{1}] {2}" -f $blocked.Path, $blocked.Branch, (@($blocked.Reasons) -join ",")) Red
        }

        Write-Status "No new task branches created until blocked worktrees are fixed." DarkYellow
        Save-DispatcherState -Tasks $tasks
        return
    }

    $openBranches = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($task in $tasks) {
        [void]$openBranches.Add($task.Branch)
    }

    $openWorktrees = @(Get-GitWorktrees | Where-Object { -not $_.Bare -and $openBranches.Contains($_.Branch) })
    $slotsAvailable = [Math]::Max(0, $maxParallel - $openWorktrees.Count)
    Write-Status ("Active open task branches: {0}; available slots: {1}" -f $openWorktrees.Count, $slotsAvailable) Cyan

    foreach ($task in $tasks) {
        if ($slotsAvailable -le 0) {
            break
        }

        $hasWorktree = @(Get-GitWorktrees | Where-Object { $_.Branch -ieq $task.Branch }).Count -gt 0
        if ($hasWorktree) {
            continue
        }

        Ensure-TaskWorktree -Task $task
        $slotsAvailable--
    }

    if ($autoPush) {
        Push-ManagedBranches -AllowFailure
    }

    Save-DispatcherState -Tasks $tasks
    Write-Status "Smart dispatcher run completed." Green
}

function Invoke-WithDispatcherLock {
    param([scriptblock]$Action)

    Ensure-Directory -Path $stateDir
    $stream = $null
    try {
        $stream = [System.IO.File]::Open($lockPath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    }
    catch [System.IO.IOException] {
        Write-Status "Another dispatcher run is already active; skipping this scheduled tick." DarkYellow
        return
    }

    try {
        $content = "pid={0}; started={1:o}" -f $PID, (Get-Date)
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($content)
        $stream.SetLength(0)
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush()
        & $Action
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Install-DispatcherSchedule {
    param(
        [int]$EveryHours = 2,
        [string]$TaskName = $taskNameDefault
    )

    if ($EveryHours -lt 1) {
        throw "Schedule interval must be at least 1 hour."
    }

    $scriptPath = Join-Path $repoRoot "Git-Dispatcher-Syncer.ps1"
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw "Dispatcher script not found: $scriptPath"
    }

    $escapedScriptPath = $scriptPath.Replace('"', '\"')
    $taskCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$escapedScriptPath`" scheduled-run"

    Write-Status ("Installing scheduled task '{0}' every {1} hour(s)." -f $TaskName, $EveryHours) Cyan
    & schtasks /Create /SC HOURLY /MO $EveryHours /TN $TaskName /TR $taskCommand /F | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create scheduled task '$TaskName'."
    }

    Write-Status ("Scheduled task '{0}' installed." -f $TaskName) Green
}

function Uninstall-DispatcherSchedule {
    param([string]$TaskName = $taskNameDefault)

    Write-Status ("Removing scheduled task '{0}'." -f $TaskName) Cyan
    & schtasks /Delete /TN $TaskName /F | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to remove scheduled task '$TaskName'."
    }

    Write-Status ("Scheduled task '{0}' removed." -f $TaskName) Green
}

function Dispatch-GitCommand {
    param([string[]]$GitArguments)

    if ($GitArguments.Count -eq 0) {
        throw "dispatch requires git arguments, for example: dispatch status -sb"
    }

    $subcommand = $GitArguments[0].ToLowerInvariant()
    $dangerous = @("reset", "clean", "restore", "checkout", "switch", "rebase", "merge", "commit")
    if ($dangerous -contains $subcommand -and $env:GIT_DISPATCHER_ALLOW_DANGEROUS -ne "1") {
        throw "Refusing to dispatch '$subcommand' to every worktree. Set GIT_DISPATCHER_ALLOW_DANGEROUS=1 if this is intentional."
    }

    $failures = 0
    foreach ($worktree in @(Get-GitWorktrees | Where-Object { -not $_.Bare })) {
        Write-Host ""
        Write-Status ("Dispatch: {0} [{1}]" -f $worktree.Path, $worktree.Branch) Cyan
        $exitCode = Invoke-Git -Arguments $GitArguments -WorkingDirectory $worktree.Path -AllowFailure
        if ($exitCode -ne 0) {
            $failures++
            Write-Status ("Command failed in {0} with exit code {1}" -f $worktree.Path, $exitCode) Red
        }
    }

    if ($failures -gt 0) {
        throw ("Dispatch completed with {0} failing worktree(s)." -f $failures)
    }
}

try {
    switch ($Command.ToLowerInvariant()) {
        "status" { Show-RepositoryStatus }
        "tasks" { Show-AgentOpsTasks }
        "smart" { Invoke-SmartDispatcher }
        "scheduled-run" { Invoke-WithDispatcherLock { Invoke-SmartDispatcher } }
        "sync" { Sync-Worktree -Path $repoRoot }
        "sync-all" { Sync-AllWorktrees }
        "worktree" {
            if ($Args.Count -lt 1) {
                throw "worktree requires a branch name."
            }

            $target = if ($Args.Count -gt 1) { $Args[1] } else { "" }
            New-BranchWorktree -Branch $Args[0] -TargetPath $target
        }
        "branch" {
            if ($Args.Count -lt 1) {
                throw "branch requires a branch name."
            }

            $target = if ($Args.Count -gt 1) { $Args[1] } else { "" }
            New-BranchWorktree -Branch $Args[0] -TargetPath $target
        }
        "new" {
            if ($Args.Count -lt 1) {
                throw "new requires a branch name."
            }

            $target = if ($Args.Count -gt 1) { $Args[1] } else { "" }
            New-BranchWorktree -Branch $Args[0] -TargetPath $target
        }
        "push-current" { Push-CurrentBranch }
        "push-managed" { Push-ManagedBranches }
        "install-schedule" {
            $hours = if ($Args.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($Args[0])) { [int]$Args[0] } else { 2 }
            $taskName = if ($Args.Count -gt 1 -and -not [string]::IsNullOrWhiteSpace($Args[1])) { $Args[1] } else { $taskNameDefault }
            Install-DispatcherSchedule -EveryHours $hours -TaskName $taskName
        }
        "uninstall-schedule" {
            $taskName = if ($Args.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($Args[0])) { $Args[0] } else { $taskNameDefault }
            Uninstall-DispatcherSchedule -TaskName $taskName
        }
        "dispatch" { Dispatch-GitCommand -GitArguments $Args }
        "help" { Show-Help }
        "-h" { Show-Help }
        "--help" { Show-Help }
        default {
            Show-Help
            throw "Unknown command: $Command"
        }
    }
}
catch {
    Write-Host ""
    Write-Status ("ERROR: {0}" -f $_.Exception.Message) Red
    if ($env:GIT_DISPATCH_DEBUG -eq "1") {
        Write-Status ($_.ScriptStackTrace) DarkYellow
    }
    exit 1
}
