@echo off
cd /d "%~dp0"

REM Check if Microkernel is already running
tasklist /FI "IMAGENAME eq Microkernel.exe" 2>NUL | find /I /N "Microkernel. exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo WARNING: Microkernel is already running! 
    echo Please close it before building.
    pause
    exit /b 1
)

echo Building Microkernel... 

REM Build without clean (much faster)
dotnet build Microkernel.sln --configuration Debug --verbosity quiet
if %ERRORLEVEL% neq 0 (
    echo. 
    echo ERROR: Build failed!  Run with verbose output:
    echo   dotnet build Microkernel.sln
    pause
    exit /b 1
)

REM Ensure plugins are copied
set PLUGIN_DIR=Microkernel\bin\Debug\net8.0\Plugins
if not exist "%PLUGIN_DIR%" mkdir "%PLUGIN_DIR%"

if exist "Plugins\EventGeneratorPlugin\bin\Debug\net8.0\EventGeneratorPlugin.dll" (
    copy /y "Plugins\EventGeneratorPlugin\bin\Debug\net8.0\EventGeneratorPlugin. dll" "%PLUGIN_DIR%\" >nul
)

if exist "Plugins\MetricsLoggerPlugin\bin\Debug\net8. 0\MetricsLoggerPlugin.dll" (
    copy /y "Plugins\MetricsLoggerPlugin\bin\Debug\net8. 0\MetricsLoggerPlugin.dll" "%PLUGIN_DIR%\" >nul
)

REM Clear screen and run
cls
Microkernel\bin\Debug\net8.0\Microkernel.exe