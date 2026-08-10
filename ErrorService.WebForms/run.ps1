# Start IIS Express for ErrorService.WebForms
$iisPath = "${env:ProgramFiles(x86)}\IIS Express\iisexpress.exe"
$projectPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$port = 5001

Write-Host "Starting IIS Express on port $port..." -ForegroundColor Green
Write-Host "Open http://localhost:$port in your browser" -ForegroundColor Cyan

& $iisPath /path:"$projectPath" /port:$port
