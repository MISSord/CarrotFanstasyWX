@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0regen-game-network.ps1"
if errorlevel 1 (
  echo.
  pause
)
