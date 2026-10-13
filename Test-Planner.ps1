$ErrorActionPreference = 'Stop'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testOutput = Join-Path $PSScriptRoot 'tests\bin'
New-Item -ItemType Directory -Force -Path $testOutput | Out-Null
$testExe = Join-Path $testOutput 'PlannerTests.exe'
& $compiler /nologo /langversion:5 /target:exe "/out:$testExe" /reference:System.Core.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll "$PSScriptRoot\Planner.cs" "$PSScriptRoot\TakeoutMeasurement.cs" "$PSScriptRoot\PreviewSnapshot.cs" "$PSScriptRoot\SessionFiles.cs" "$PSScriptRoot\SessionInfo.cs" "$PSScriptRoot\Phase2Settings.cs" "$PSScriptRoot\tests\PlannerTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Planner harness compilation failed' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Planner harness failed' }
