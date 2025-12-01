
$tempDir = "temp_export"
$exportDir = "export"
$modName = "CaptureShipsOnVictory"

New-Item -ItemType Directory -Force -Path $tempDir/$modName/bin/Win64_Shipping_Client | Out-Null
New-Item -ItemType Directory -Force -Path $exportDir | Out-Null

Copy-Item "bin/Win64_Shipping_Client/CaptureShipsOnVictory.dll" -Destination "$tempDir/$modName/bin/Win64_Shipping_Client/" -ErrorAction SilentlyContinue
Copy-Item "SubModule.xml" -Destination "$tempDir/$modName/"

$date = Get-Date -Format "yyyy-MM-dd_HH-mm"
$zipPath = "$exportDir/${modName}_$date.zip"

if (Test-Path $zipPath) {
    Remove-Item $zipPath
}

Compress-Archive -Path "$tempDir/*" -DestinationPath $zipPath

Remove-Item -Recurse -Force $tempDir

Write-Host "Mod successfully packaged to $zipPath"

