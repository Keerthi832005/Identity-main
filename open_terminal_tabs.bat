@echo off
REM ============================================================
REM Script to open multiple folders in Windows Terminal tabs
REM with Claude running in each tab (Native Version)
REM ============================================================

REM Claude Code native installation path
set "claudePath=%USERPROFILE%\.local\bin\claude.exe"

REM Get the directory where this script is located (removes trailing backslash)
set "scriptDir=%~dp0"
set "scriptDir=%scriptDir:~0,-1%"

REM Extract folder name for title
for %%F in ("%scriptDir%") do set "folderName=%%~nxF"

REM Verify Claude native installation exists
if not exist "%claudePath%" (
    echo ERROR: Claude Code native not found at: %claudePath%
    echo Please run 'claude install' first.
    pause
    exit /b 1
)

REM Ask user for number of windows
echo ============================================================
echo Current folder: %folderName%
echo ============================================================
echo.
set /p "numWindows=Enter number of Claude windows to open (1-10): "

REM Validate input (default to 3 if invalid)
if "%numWindows%"=="" set "numWindows=3"
set /a "numWindows=%numWindows%" 2>nul
if %numWindows% LSS 1 set "numWindows=1"
if %numWindows% GTR 10 set "numWindows=10"

echo.
echo Opening %numWindows% Claude windows...
echo.

REM Build the Windows Terminal command dynamically
setlocal enabledelayedexpansion
set "wtCmd=wt -w 0"

for /l %%i in (1,1,%numWindows%) do (
    if %%i==1 (
        set "wtCmd=!wtCmd! new-tab --title "%folderName%-%%i" --suppressApplicationTitle -d "%scriptDir%" cmd /k ""%claudePath%" --dangerously-skip-permissions""
    ) else (
        set "wtCmd=!wtCmd! ; new-tab --title "%folderName%-%%i" --suppressApplicationTitle -d "%scriptDir%" cmd /k ""%claudePath%" --dangerously-skip-permissions""
    )
)

REM Execute the command
%wtCmd%

echo.
echo ============================================================
echo Windows Terminal opened with %numWindows% tabs:
echo   - Tab 1-%numWindows%: %folderName%
echo   - Using: Claude Code Native
echo ============================================================
echo.
pause
