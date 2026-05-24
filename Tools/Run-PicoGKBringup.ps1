param(
    [string]$OutputPath = ".\\temp_verify_picogk\\picogk_enclosure_demo.stl",
    [switch]$SkipBuild
)

$projectPath = ".\\Tools\\PicoGKBringup\\PicoGKBringup.csproj"
$resolvedOutput = Resolve-Path "." | ForEach-Object {
    [System.IO.Path]::GetFullPath((Join-Path $_.Path $OutputPath))
}

if (-not $SkipBuild) {
    dotnet build $projectPath --nologo -v minimal
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$dllPath = ".\\Tools\\PicoGKBringup\\bin\\Debug\\net10.0-windows\\PicoGKBringup.dll"
dotnet $dllPath $resolvedOutput
exit $LASTEXITCODE
