# Build project
Write-Host "start compilation..." -ForegroundColor Green
& 'C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe' CaptureShipsOnVictory.csproj /p:Configuration=Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "compilated" -ForegroundColor Green

    # Export mod
    Write-Host "export" -ForegroundColor Green
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
    & .\export_mod.ps1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "done" -ForegroundColor Green
    } else {
        Write-Host "export err" -ForegroundColor Red
    }
} else {
    Write-Host "err" -ForegroundColor Red
}

