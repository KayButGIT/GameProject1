param([string]$EditorData)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $EditorData) {
    $versionLine = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Select-Object -First 1
    $editorVersion = ($versionLine -split ': ', 2)[1]
    $EditorData = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$editorVersion/Editor/Data"
}
$outputDirectory = Join-Path $projectRoot 'Temp/MovementTests'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$framework = Join-Path $EditorData 'MonoBleedingEdge/lib/mono/4.8-api'
$unityManaged = Join-Path $EditorData 'Managed/UnityEngine'
$output = Join-Path $outputDirectory 'MovementTests.exe'
$compilerArguments = @(
    '-nologo', '-noconfig', '-nostdlib+', '-target:exe', '-langversion:9.0',
    "-out:$output",
    "-r:$framework/mscorlib.dll",
    "-r:$framework/System.dll",
    "-r:$framework/Facades/netstandard.dll",
    "-r:$unityManaged/UnityEngine.CoreModule.dll",
    (Join-Path $projectRoot 'Assets/Scripts/GridMovement.cs'),
    (Join-Path $PSScriptRoot 'GridMovementRegression.cs')
)
& (Join-Path $EditorData 'NetCoreRuntime/dotnet.exe') (Join-Path $EditorData 'DotNetSdkRoslyn/csc.dll') @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Movement regression compilation failed.' }

$previousMonoPath = $env:MONO_PATH
try {
    $env:MONO_PATH = "$unityManaged;$framework/Facades"
    & (Join-Path $EditorData 'MonoBleedingEdge/bin/mono.exe') $output
    if ($LASTEXITCODE -ne 0) { throw 'Movement regression checks failed.' }
}
finally {
    $env:MONO_PATH = $previousMonoPath
}
