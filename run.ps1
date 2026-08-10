param(
    [switch]$NoWebForms
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ErrorService - Unified Startup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not $NoWebForms) {
    Write-Host "[WebForms] Starting on port 5001 ..." -ForegroundColor Green
    $job = Start-Job -ScriptBlock {
        Set-Location $using:PWD
        dotnet run --project ErrorService.WebForms\ErrorService.WebForms.csproj
    }
    Start-Sleep -Seconds 3
    Write-Host "[WebForms] Started (background job)" -ForegroundColor Green
}

Write-Host "[Main API] Starting on http://localhost:5318 ..." -ForegroundColor Green
Write-Host ""
dotnet run --project ErrorService\Server\ErrorService.Server.csproj

if (-not $NoWebForms) {
    Write-Host "[WebForms] Stopping ..." -ForegroundColor Yellow
    Stop-Job $job -ErrorAction SilentlyContinue
    Remove-Job $job -ErrorAction SilentlyContinue
}
