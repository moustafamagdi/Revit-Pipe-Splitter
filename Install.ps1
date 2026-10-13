$ErrorActionPreference = 'Stop'
$targetFolder = Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024'
$assemblyPath = Join-Path $PSScriptRoot 'PipeSplitter2024.dll'
if (!(Test-Path -LiteralPath $assemblyPath)) { throw 'DLL not found beside installer.' }
New-Item -ItemType Directory -Force -Path $targetFolder | Out-Null
$escapedPath = [System.Security.SecurityElement]::Escape($assemblyPath)
$manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Command">
    <Name>Pipe Splitter 2024</Name>
    <Assembly>$escapedPath</Assembly>
    <AddInId>CB443DC3-96DC-4D25-B4EA-89C03F0B7FAA</AddInId>
    <FullClassName>PipeSplitter2024.Command</FullClassName>
    <Text>Pipe Splitter</Text>
    <VendorId>PS24</VendorId>
    <VendorDescription>Pipe splitting with minimum length and routing unions</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
$manifestPath = Join-Path $targetFolder 'PipeSplitter2024.addin'
if (Test-Path -LiteralPath $manifestPath) {
    Copy-Item -LiteralPath $manifestPath -Destination ($manifestPath + '.' + (Get-Date -Format 'yyyyMMddHHmmss') + '.bak')
}
Set-Content -LiteralPath $manifestPath -Value $manifest -Encoding UTF8
Write-Host 'Installed. Keep this folder in its current location. Restart Revit, then use Add-Ins > External Tools > Pipe Splitter.'
