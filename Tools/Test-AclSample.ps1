param(
    [string]$Configuration = "Debug",
    [string]$TemplateInvocation = "template controller-box-kit boxWidth=140 boxDepth=95 boxHeight=48 wallThickness=3 lidThickness=3 standoffHeight=10 cableDiameter=7 fanSize=80",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

if (-not $SkipBuild) {
    Write-Host "Building My3DApp..." -ForegroundColor Cyan
    dotnet build .\My3DApp.csproj -c $Configuration -p:StudioUiHost=Avalonia --nologo -v minimal | Out-Host
    Write-Host "Building ACL smoke runner..." -ForegroundColor Cyan
    dotnet build .\Tools\AclSmokeRunner\AclSmokeRunner.csproj -c $Configuration --nologo -v minimal | Out-Host
}

$runnerDll = Join-Path $repoRoot "Tools\AclSmokeRunner\bin\$Configuration\net10.0-windows\AclSmokeRunner.dll"
if (-not (Test-Path $runnerDll)) {
    throw "Built runner not found: $runnerDll"
}

Write-Host "Running ACL smoke runner..." -ForegroundColor Cyan
dotnet exec $runnerDll $TemplateInvocation | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "ACL smoke test failed."
}
