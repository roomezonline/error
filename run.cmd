@echo off
echo ========================================
echo   ErrorService - Unified Startup
echo ========================================
echo.
echo Starting WebForms on port 5001 ...
start "WebForms" cmd /c "dotnet run --project ErrorService.WebForms\ErrorService.WebForms.csproj"
timeout /t 3 /nobreak >nul
echo Starting Main API on http://localhost:5318 ...
dotnet run --project ErrorService\Server\ErrorService.Server.csproj
