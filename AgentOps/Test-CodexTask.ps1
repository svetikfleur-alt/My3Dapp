[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$TaskNumber,

  [Parameter(Mandatory = $true)]
  [string]$TaskTitle
)

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$agentOpsRoot = Join-Path $root 'AgentOps'
$currentTaskFile = Join-Path $agentOpsRoot 'CURRENT_TASK.md'
$resultFile = Join-Path $agentOpsRoot 'LAST_TEST_RESULT.json'
$buildLogFile = Join-Path $agentOpsRoot 'LAST_TEST_BUILD.log'
$behaviorFile = Join-Path $agentOpsRoot 'APP_BEHAVIOR.md'
$behaviorJsonFile = Join-Path $agentOpsRoot 'LAST_APP_BEHAVIOR.json'

function Get-GitStatusPaths {
  Push-Location $root
  try {
    $status = @(& git status --porcelain=1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }

    $items = @()
    foreach ($line in $status) {
      if ([string]::IsNullOrWhiteSpace($line)) { continue }
      if ($line.Length -lt 4) { continue }

      $pathPart = $line.Substring(3).Trim()
      if ($pathPart -match ' -> ') {
        $pathPart = ($pathPart -split ' -> ')[-1]
      }

      if (-not [string]::IsNullOrWhiteSpace($pathPart)) {
        $items += $pathPart.Replace('\', '/')
      }
    }

    return @($items)
  } finally {
    Pop-Location
  }
}

function Test-IsGeneratedPath($path) {
  $normalized = ($path -replace '\\', '/').Trim()
  $generatedPatterns = @(
    '^bin/',
    '^obj/',
    '^AgentOps/CURRENT_TASK\.md$',
    '^AgentOps/STATUS\.md$',
    '^AgentOps/PROGRESS\.md$',
    '^AgentOps/RUN_SUMMARY\.md$',
    '^AgentOps/HEARTBEAT\.md$',
    '^AgentOps/HEARTBEAT_STATE\.json$',
    '^AgentOps/HEARTBEAT\.log$',
    '^AgentOps/HEARTBEAT\.lock$',
    '^AgentOps/HEARTBEAT_LAST_WORKER\.log$',
    '^AgentOps/HEARTBEAT_LAST_WORKER\.err$',
    '^AgentOps/APP_BEHAVIOR\.md$',
    '^AgentOps/HEALTH\.md$',
    '^AgentOps/RUNNER\.lock$',
    '^AgentOps/LAST_CODEX_PROMPT\.md$',
    '^AgentOps/LAST_CODEX_STDOUT\.log$',
    '^AgentOps/LAST_CODEX_STDERR\.log$',
    '^AgentOps/LAST_TEST_RESULT\.json$',
    '^AgentOps/LAST_TEST_BUILD\.log$',
    '^AgentOps/LAST_VERIFY_RESULT\.json$',
    '^AgentOps/LAST_APP_BEHAVIOR\.json$',
    '^AgentOps/LAST_HEALTH_RESULT\.json$',
    '^AgentOps/LAST_QUEUE_AUTOWRITE\.md$',
    '^AgentOps/build_result\.json$',
    '^AgentOps/current_task_prompt\.md$',
    '^AgentOps/last_agent_failure\.log$',
    '^AgentOps/spawner\.state\.json$',
    '^AgentOps/codex-home/',
    '^AgentOps/runner/.*\.log(\.err)?$',
    '^bin/.*/logs/runtime-errors\.log$',
    '^dist/.*/logs/runtime-errors\.log$',
    '^bin/.*/webview2-data/'
  )

  foreach ($pattern in $generatedPatterns) {
    if ($normalized -match $pattern) {
      return $true
    }
  }

  return $false
}

function Get-RelevantGitPaths {
  return @(Get-GitStatusPaths | Where-Object { -not (Test-IsGeneratedPath $_) })
}

function Get-TaskProfile($currentTaskText, $changedFiles) {
  $changedFiles = @($changedFiles)
  $isAgentOpsOnly = $currentTaskText -match 'Work only on AgentOps scripts|Do not modify CAD application code|Do not modify application code|Only fix automation scripts'
  if ($isAgentOpsOnly) { return 'agentops' }

  if ($changedFiles.Count -eq 0) { return 'unknown' }

  $hasAgentOps = @($changedFiles | Where-Object { $_ -match '^AgentOps/' }).Count -gt 0
  $hasUi = @($changedFiles | Where-Object { $_ -match '^(AvaloniaApp|Assets)/' }).Count -gt 0
  $hasBackend = @($changedFiles | Where-Object { $_ -match '^(Engine|Core|Backends)/' }).Count -gt 0

  if ($hasAgentOps -and -not $hasUi -and -not $hasBackend -and (@($changedFiles | Where-Object { $_ -notmatch '^AgentOps/' }).Count -eq 0)) {
    return 'agentops'
  }
  if ($hasUi -and -not $hasBackend) { return 'ui' }
  if ($hasBackend -and -not $hasUi) { return 'backend' }
  if ($hasUi -and $hasBackend) { return 'mixed-app' }

  return 'mixed'
}

function Test-PowerShellSyntax($path) {
  try {
    [void][scriptblock]::Create((Get-Content $path -Raw -Encoding UTF8))
    return @{ path = $path; ok = $true; message = 'syntax ok' }
  } catch {
    return @{ path = $path; ok = $false; message = $_.Exception.Message }
  }
}

function Get-LatestRuntimeLog {
  $logs = @(Get-ChildItem -Path $root -Recurse -File -Filter 'runtime-errors.log' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\\.claude\\' } |
    Sort-Object LastWriteTimeUtc -Descending)

  return ($logs | Select-Object -First 1)
}

function Get-RuntimeBehaviorSnapshot {
  $log = Get-LatestRuntimeLog
  if ($null -eq $log) {
    return [pscustomobject]@{
      has_log = $false
      path = ''
      modified_at = ''
      viewport_ready = $false
      scene_applied = $false
      scene_with_bodies = $false
      software_fallback = $false
      exception_markers = 0
      recent_lines = @()
    }
  }

  $lines = @(Get-Content $log.FullName -Encoding UTF8)
  $meaningful = @($lines | Where-Object { $_ -and $_ -notmatch '^-{10,}$' })
  $recent = @($meaningful | Select-Object -Last 12 | ForEach-Object { [string]$_ })
  $exceptionMarkers = @($meaningful | Where-Object { $_ -match 'Exception|Unhandled|failed\.' }).Count
  $sceneWithBodies = @($meaningful | Where-Object { $_ -match 'Scene applied\..*bodies=(?!0)\d+' }).Count -gt 0

  return [pscustomobject]@{
    has_log = $true
    path = $log.FullName
    modified_at = $log.LastWriteTime.ToString('o')
    viewport_ready = @($meaningful | Where-Object { $_ -match 'Viewport reported ready' }).Count -gt 0
    scene_applied = @($meaningful | Where-Object { $_ -match 'Scene applied\.' }).Count -gt 0
    scene_with_bodies = $sceneWithBodies
    software_fallback = @($meaningful | Where-Object { $_ -match 'software viewport' }).Count -gt 0
    exception_markers = $exceptionMarkers
    recent_lines = @($recent)
  }
}

function Write-AppBehaviorReport($profile, $relevantFiles, $buildSuccess, $behavior) {
  $changedLine = if (@($relevantFiles).Count -gt 0) { $relevantFiles -join ', ' } else { '(none)' }
  $buildLine = if ($buildSuccess) { 'pass' } else { 'fail' }
  $recentLines = if ($behavior.has_log -and @($behavior.recent_lines).Count -gt 0) {
    @($behavior.recent_lines | ForEach-Object { "- $_" }) -join [Environment]::NewLine
  } else {
    '- No runtime log evidence captured yet.'
  }

  $confidenceNotes = New-Object System.Collections.Generic.List[string]
  if ($behavior.viewport_ready) { $confidenceNotes.Add('Viewport reached the ready signal in the latest runtime log.') | Out-Null }
  if ($behavior.scene_applied) { $confidenceNotes.Add('A CAD scene was applied in the latest runtime log.') | Out-Null }
  if ($behavior.scene_with_bodies) { $confidenceNotes.Add('At least one rendered body was observed in the latest runtime log.') | Out-Null }
  if ($behavior.software_fallback) { $confidenceNotes.Add('The app used the software viewport fallback in the latest captured run.') | Out-Null }
  if ($behavior.exception_markers -gt 0) { $confidenceNotes.Add(("The latest runtime log still contains {0} exception/error marker(s)." -f $behavior.exception_markers)) | Out-Null }
  if ($confidenceNotes.Count -eq 0) { $confidenceNotes.Add('No strong app-behavior evidence was found beyond the local build/test pass.') | Out-Null }

  $content = @(
    "# App behavior snapshot - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    ''
    "Task: $TaskNumber - $TaskTitle"
    "Profile: $profile"
    "Build: $buildLine"
    "Changed files: $changedLine"
    ''
    'Latest runtime evidence:'
    ("- Has runtime log: {0}" -f $behavior.has_log)
    ("- Runtime log path: {0}" -f $(if ($behavior.path) { $behavior.path } else { '(none)' }))
    ("- Runtime log modified: {0}" -f $(if ($behavior.modified_at) { $behavior.modified_at } else { '(none)' }))
    ("- Viewport ready seen: {0}" -f $behavior.viewport_ready)
    ("- Scene applied seen: {0}" -f $behavior.scene_applied)
    ("- Body render seen: {0}" -f $behavior.scene_with_bodies)
    ("- Software fallback seen: {0}" -f $behavior.software_fallback)
    ("- Exception markers: {0}" -f $behavior.exception_markers)
    ''
    'What this tells us:'
  )

  foreach ($note in $confidenceNotes) {
    $content += "- $note"
  }

  $content += ''
  $content += 'Recent runtime lines:'
  $content += $recentLines
  $content += ''
  $content += 'Note: this is a behavior log, not pixel-perfect visual proof.'

  $content -join [Environment]::NewLine | Set-Content -Path $behaviorFile -Encoding UTF8
}

Write-Host ("  Tester: validating task {0} - {1}" -f $TaskNumber, $TaskTitle) -ForegroundColor Yellow
Write-Host ("  Tester log: {0}" -f $buildLogFile) -ForegroundColor DarkGray

$currentTaskText = if (Test-Path $currentTaskFile) { Get-Content $currentTaskFile -Raw -Encoding UTF8 } else { '' }
$relevantFiles = @(Get-RelevantGitPaths)
$profile = Get-TaskProfile $currentTaskText $relevantFiles

Write-Host ("  Task profile: {0}" -f $profile) -ForegroundColor DarkCyan
Write-Host ("  Relevant files: {0}" -f ($(if ($relevantFiles.Count -gt 0) { $relevantFiles -join ', ' } else { '(none)' }))) -ForegroundColor DarkGray

$psFilesToParse = @($relevantFiles | Where-Object { $_ -match '\.ps1$' } | ForEach-Object { Join-Path $root $_ })
$syntaxChecks = @()
foreach ($path in $psFilesToParse) {
  $check = Test-PowerShellSyntax $path
  $syntaxChecks += [pscustomobject]$check
  if ($check.ok) {
    Write-Host ("  Syntax OK: {0}" -f $path) -ForegroundColor DarkGray
  } else {
    Write-Host ("  Syntax FAIL: {0}" -f $path) -ForegroundColor Red
    Write-Host ("    {0}" -f $check.message) -ForegroundColor Red
  }
}

Push-Location $root
try {
  $output = @(& dotnet build --nologo -v minimal 2>&1)
  $exitCode = $LASTEXITCODE
} finally {
  Pop-Location
}

$output | Set-Content -Path $buildLogFile -Encoding UTF8
foreach ($line in $output) {
  Write-Host $line
}

$outputText = ($output -join [Environment]::NewLine)
$errorCount = 0
$warningCount = 0

if ($outputText -match 'Ошибок:\s*(\d+)') {
  $errorCount = [int]$matches[1]
} elseif ($outputText -match 'Errors:\s*(\d+)') {
  $errorCount = [int]$matches[1]
}

if ($outputText -match 'Предупреждений:\s*(\d+)') {
  $warningCount = [int]$matches[1]
} elseif ($outputText -match 'Warnings:\s*(\d+)') {
  $warningCount = [int]$matches[1]
}

$syntaxSuccess = @($syntaxChecks | Where-Object { -not $_.ok }).Count -eq 0
$buildSuccess = ($exitCode -eq 0)
$success = $buildSuccess -and $syntaxSuccess
$behavior = Get-RuntimeBehaviorSnapshot
Write-AppBehaviorReport $profile $relevantFiles $buildSuccess $behavior

$result = [pscustomobject]@{
  task_number = $TaskNumber
  task_title = $TaskTitle
  profile = $profile
  success = $success
  build_success = $buildSuccess
  syntax_success = $syntaxSuccess
  exit_code = $exitCode
  errors = $errorCount
  warnings = $warningCount
  relevant_files = @($relevantFiles)
  syntax_checks = @($syntaxChecks)
  behavior = $behavior
  log_file = $buildLogFile
  completed_at = (Get-Date).ToString('o')
}

$result | ConvertTo-Json -Depth 6 | Set-Content -Path $resultFile -Encoding UTF8
$behavior | ConvertTo-Json -Depth 6 | Set-Content -Path $behaviorJsonFile -Encoding UTF8

if ($success) {
  Write-Host ("  Tester passed: build ok, syntax ok, profile={0}" -f $profile) -ForegroundColor Green
  exit 0
}

Write-Host ("  Tester failed: build={0}, syntax={1}, profile={2}" -f $buildSuccess, $syntaxSuccess, $profile) -ForegroundColor Red
exit 1
