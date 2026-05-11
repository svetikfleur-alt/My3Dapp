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
$queueFile = Join-Path $agentOpsRoot 'TASK_QUEUE.md'
$doneFile = Join-Path $agentOpsRoot 'DONE.md'
$logFile = Join-Path $agentOpsRoot 'LOG.md'
$currentTaskFile = Join-Path $agentOpsRoot 'CURRENT_TASK.md'
$handoffFile = Join-Path $agentOpsRoot 'HANDOFF.md'
$verificationFile = Join-Path $agentOpsRoot 'VERIFICATION.md'
$testResultFile = Join-Path $agentOpsRoot 'LAST_TEST_RESULT.json'
$verifyResultFile = Join-Path $agentOpsRoot 'LAST_VERIFY_RESULT.json'
$behaviorFile = Join-Path $agentOpsRoot 'APP_BEHAVIOR.md'
$behaviorJsonFile = Join-Path $agentOpsRoot 'LAST_APP_BEHAVIOR.json'
$healthFile = Join-Path $agentOpsRoot 'HEALTH.md'
$healthResultFile = Join-Path $agentOpsRoot 'LAST_HEALTH_RESULT.json'

function Add-Note($list, $prefix, $text) {
  $list.Add(("- {0}: {1}" -f $prefix, $text)) | Out-Null
}

$good = New-Object System.Collections.Generic.List[string]
$meh = New-Object System.Collections.Generic.List[string]
$bad = New-Object System.Collections.Generic.List[string]
$notes = New-Object System.Collections.Generic.List[string]

$queueLines = Get-Content $queueFile -Encoding UTF8
$donePattern = "^- \[x\] $TaskNumber\b"
$markedDone = @($queueLines | Where-Object { $_ -match $donePattern }).Count -gt 0
if ($markedDone) {
  $good.Add("Task $TaskNumber is marked [x] in TASK_QUEUE.md") | Out-Null
} else {
  $bad.Add("Task $TaskNumber is not marked [x] in TASK_QUEUE.md") | Out-Null
}

$currentTaskText = if (Test-Path $currentTaskFile) { Get-Content $currentTaskFile -Raw -Encoding UTF8 } else { '' }
if ([string]::IsNullOrWhiteSpace($currentTaskText)) {
  $bad.Add('CURRENT_TASK.md is missing or empty') | Out-Null
} else {
  $good.Add('CURRENT_TASK.md exists') | Out-Null
}

$handoffText = if (Test-Path $handoffFile) { Get-Content $handoffFile -Raw -Encoding UTF8 } else { '' }
if ([string]::IsNullOrWhiteSpace($handoffText)) {
  $meh.Add('HANDOFF.md is missing or empty') | Out-Null
} elseif ($handoffText -match 'Done:' -and $handoffText -match 'Not done:' -and $handoffText -match 'Broken:' -and $handoffText -match 'Next:') {
  $good.Add('HANDOFF.md follows the executor template') | Out-Null
} else {
  $meh.Add('HANDOFF.md exists but does not fully match the executor template') | Out-Null
}

$doneText = if (Test-Path $doneFile) { Get-Content $doneFile -Raw -Encoding UTF8 } else { '' }
if ($doneText -match "\b$TaskNumber\b") {
  $good.Add("DONE.md already references task $TaskNumber") | Out-Null
} else {
  $good.Add('DONE.md will be written by the runner after verifier acceptance') | Out-Null
}

$logText = if (Test-Path $logFile) { Get-Content $logFile -Raw -Encoding UTF8 } else { '' }
if ($logText -match "\b$TaskNumber\b") {
  $good.Add("LOG.md references task $TaskNumber") | Out-Null
} else {
  $meh.Add("LOG.md does not clearly reference task $TaskNumber") | Out-Null
}

$testResult = $null
if (Test-Path $testResultFile) {
  $testResult = Get-Content $testResultFile -Raw -Encoding UTF8 | ConvertFrom-Json
  if ([bool]$testResult.success) {
    $good.Add("Tester passed with profile '$($testResult.profile)'") | Out-Null
  } else {
    $bad.Add("Tester failed with exit code $($testResult.exit_code)") | Out-Null
  }
} else {
  $bad.Add('LAST_TEST_RESULT.json is missing') | Out-Null
}

$behavior = $null
if (Test-Path $behaviorJsonFile) {
  $behavior = Get-Content $behaviorJsonFile -Raw -Encoding UTF8 | ConvertFrom-Json
  $good.Add('App behavior snapshot exists') | Out-Null
  if ([bool]$behavior.viewport_ready) {
    $good.Add('Latest runtime log includes viewport ready signal') | Out-Null
  }
  if ([bool]$behavior.scene_applied) {
    $good.Add('Latest runtime log includes scene application') | Out-Null
  }
  if ([bool]$behavior.software_fallback) {
    $meh.Add('Latest runtime evidence used the software viewport fallback') | Out-Null
  }
  if ([int]$behavior.exception_markers -gt 0) {
    $meh.Add("Latest runtime log still contains $($behavior.exception_markers) exception/error marker(s)") | Out-Null
  }
} elseif (Test-Path $behaviorFile) {
  $meh.Add('APP_BEHAVIOR.md exists, but LAST_APP_BEHAVIOR.json is missing') | Out-Null
} else {
  $meh.Add('App behavior snapshot is missing') | Out-Null
}

if (Test-Path $healthResultFile) {
  $healthResult = Get-Content $healthResultFile -Raw -Encoding UTF8 | ConvertFrom-Json
  switch ($healthResult.result) {
    'pass' { $good.Add('AgentOps health check passed') | Out-Null }
    'warn' { $meh.Add('AgentOps health check reported warnings') | Out-Null }
    default { $bad.Add('AgentOps health check reported failure') | Out-Null }
  }
} elseif (Test-Path $healthFile) {
  $meh.Add('HEALTH.md exists, but LAST_HEALTH_RESULT.json is missing') | Out-Null
}

$buildLine = 'pending'
if ($null -ne $testResult) {
  if ([bool]$testResult.build_success) {
    $buildLine = "ok ($($testResult.errors) errors, $($testResult.warnings) warnings)"
  } else {
    $buildLine = "failed ($($testResult.errors) errors, $($testResult.warnings) warnings)"
  }
}

$agentOpsOnly = $currentTaskText -match 'Work only on AgentOps scripts|Do not modify CAD application code|Do not modify application code|Only fix automation scripts'
$uiBehaviorSensitive = $currentTaskText -match '\bviewport\b|\btoolbar\b|\bUI\b|\bvisual\b|\blayout\b|\bbehavior\b|\bassistant\b'
$relevantFiles = if ($null -ne $testResult) { @($testResult.relevant_files) } else { @() }

if ($agentOpsOnly) {
  $outsideAgentOps = @($relevantFiles | Where-Object { $_ -notmatch '^AgentOps/' })
  if ($outsideAgentOps.Count -gt 0) {
    $bad.Add('AgentOps-only task changed files outside AgentOps: ' + ($outsideAgentOps -join ', ')) | Out-Null
  } else {
    $good.Add('AgentOps-only scope respected') | Out-Null
  }
}

if ($uiBehaviorSensitive) {
  $meh.Add('Task includes UI/behavior expectations; fully automatic visual verification is not available yet') | Out-Null
  if ($null -ne $behavior -and -not [bool]$behavior.viewport_ready -and -not [bool]$behavior.scene_applied) {
    $meh.Add('UI/behavior-sensitive task has no fresh runtime signals beyond build/test output') | Out-Null
  }
}

$result = 'accept'
if ($bad.Count -gt 0) {
  $result = 'reject'
} elseif ($meh.Count -gt 0) {
  $result = 'partial'
}

foreach ($item in $good) { Add-Note $notes 'Good' $item }
foreach ($item in $meh) { Add-Note $notes 'Meh' $item }
foreach ($item in $bad) { Add-Note $notes 'Bad' $item }
$notes.Add('- Blueprint conflicts: none') | Out-Null
switch ($result) {
  'accept' { $notes.Add('- Next: commit this task and continue to the next queue item') | Out-Null }
  'partial' { $notes.Add('- Next: manual review recommended before commit because behavior/UI proof is incomplete') | Out-Null }
  default { $notes.Add('- Next: send back to executor / fix the failing condition before commit') | Out-Null }
}

$verification = @(
  'Task:'
  "- $TaskNumber - $TaskTitle"
  ''
  'Result:'
  "- $result"
  ''
  "Build: $buildLine"
  ''
  'Notes:'
) + @($notes)

$verification -join [Environment]::NewLine | Set-Content -Path $verificationFile -Encoding UTF8

$verifyResult = [pscustomobject]@{
  task_number = $TaskNumber
  task_title = $TaskTitle
  result = $result
  build = $buildLine
  changed_files = @($relevantFiles)
  completed_at = (Get-Date).ToString('o')
}
$verifyResult | ConvertTo-Json -Depth 6 | Set-Content -Path $verifyResultFile -Encoding UTF8

$color = switch ($result) {
  'accept' { 'Green' }
  'partial' { 'Yellow' }
  default { 'Red' }
}

Write-Host ("  Verifier result: {0}" -f $result) -ForegroundColor $color
Write-Host ("  Build summary: {0}" -f $buildLine) -ForegroundColor DarkGray
Write-Host ("  Verification file: {0}" -f $verificationFile) -ForegroundColor DarkGray

$highSignalReasons = @()
if ($bad.Count -gt 0) {
  $highSignalReasons += @($bad | Select-Object -First 3)
} elseif ($meh.Count -gt 0) {
  $highSignalReasons += @($meh | Select-Object -First 3)
} else {
  $highSignalReasons += @($good | Select-Object -First 2)
}

if ($highSignalReasons.Count -gt 0) {
  Write-Host '  Key verifier notes:' -ForegroundColor $color
  foreach ($reason in $highSignalReasons) {
    Write-Host ("    - {0}" -f $reason) -ForegroundColor $color
  }
}

switch ($result) {
  'accept' { exit 0 }
  'partial' { exit 2 }
  default { exit 1 }
}
