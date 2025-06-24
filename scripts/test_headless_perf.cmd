@echo off
rem Wrapper for test_headless_perf.ps1 that avoids profile loading and execution policy restrictions.

set SCRIPT_DIR=%~dp0

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%test_headless_perf.ps1" %*

exit /b %ERRORLEVEL% 