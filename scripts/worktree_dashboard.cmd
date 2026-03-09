@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "GIT_BASH=C:\Program Files\Git\bin\bash.exe"

if not exist "%GIT_BASH%" (
  echo Git Bash not found at "%GIT_BASH%".
  exit /b 1
)

"%GIT_BASH%" "%SCRIPT_DIR%worktree_dashboard.sh" %*
