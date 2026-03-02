@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0unity_test_worktree.ps1" %*
set EXITCODE=%ERRORLEVEL%
endlocal & exit /b %EXITCODE%
