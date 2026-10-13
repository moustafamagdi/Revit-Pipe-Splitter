$ErrorActionPreference = 'Stop'
$revitFolder = 'C:\Program Files\Autodesk\Revit 2024'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:library /platform:x64 "/out:$PSScriptRoot\PipeSplitter2024.dll" "/reference:$revitFolder\RevitAPI.dll" "/reference:$revitFolder\RevitAPIUI.dll" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\Planner.cs" "$PSScriptRoot\PipeSplitter.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host 'Built PipeSplitter2024.dll'
