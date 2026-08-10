@echo off
chcp 65001 >nul
if /i "%1"=="-hashonly" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy.ps1" -HashOnly
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy.ps1"
)
pause
