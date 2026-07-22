@echo off
chcp 65001 >nul
title ShortLink - URL Shortener
cd /d "%~dp0"

echo.
echo   ============================================
echo     ShortLink - starting on http://localhost:5080
echo   ============================================
echo.
echo   Wait a few seconds, then the browser opens.
echo   To stop: close this window or press Ctrl+C.
echo.

REM Run with SQLite (no SQL Server needed). Add --Seed=true for demo data.
set UseSqlite=true

REM Open the browser (server needs a few seconds; refresh if it shows an error).
start "" http://localhost:5080/

dotnet run --urls http://localhost:5080

echo.
echo   Server stopped. Press any key to close.
pause >nul
