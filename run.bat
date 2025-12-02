@echo off
cls

REM Kill any running instances first (silently)
taskkill /F /IM Microkernel. exe >nul 2>&1
taskkill /F /IM MetricsLogger.exe >nul 2>&1
taskkill /F /IM EventGenerator.exe >nul 2>&1
timeout /t 1 /nobreak >nul

echo ========================================
echo   Building Microkernel Solution... 
echo ========================================
echo.

REM Build all projects
dotnet build Contracts/Contracts.csproj -c Debug -v q
if %ERRORLEVEL% NEQ 0 goto :error

dotnet build Plugins/MetricsLoggerProcess/MetricsLoggerProcess.csproj -c Debug -v q
if %ERRORLEVEL% NEQ 0 goto :error

dotnet build Plugins/EventGeneratorPlugin/EventGeneratorPlugin.csproj -c Debug -v q
if %ERRORLEVEL% NEQ 0 goto :error

dotnet build Microkernel/Microkernel.csproj -c Debug -v q
if %ERRORLEVEL% NEQ 0 goto :error

REM Copy plugin executables (silently)
copy /Y "Plugins\MetricsLoggerProcess\bin\Debug\net8.0\MetricsLogger.exe" "Microkernel\bin\Debug\net8. 0\" >nul 2>&1
copy /Y "Plugins\MetricsLoggerProcess\bin\Debug\net8.0\MetricsLogger.dll" "Microkernel\bin\Debug\net8.0\" >nul 2>&1
copy /Y "Plugins\MetricsLoggerProcess\bin\Debug\net8.0\MetricsLogger.runtimeconfig.json" "Microkernel\bin\Debug\net8.0\" >nul 2>&1
copy /Y "Plugins\MetricsLoggerProcess\bin\Debug\net8.0\MetricsLogger.deps.json" "Microkernel\bin\Debug\net8.0\" >nul 2>&1

copy /Y "Plugins\EventGeneratorPlugin\bin\Debug\net8.0\EventGenerator.exe" "Microkernel\bin\Debug\net8.0\" >nul 2>&1
copy /Y "Plugins\EventGeneratorPlugin\bin\Debug\net8.0\EventGenerator.dll" "Microkernel\bin\Debug\net8.0\" >nul 2>&1
copy /Y "Plugins\EventGeneratorPlugin\bin\Debug\net8.0\EventGenerator.runtimeconfig.json" "Microkernel\bin\Debug\net8.0\" >nul 2>&1
copy /Y "Plugins\EventGeneratorPlugin\bin\Debug\net8. 0\EventGenerator. deps.json" "Microkernel\bin\Debug\net8.0\" >nul 2>&1

echo Build successful!
echo. 
cls

cd Microkernel\bin\Debug\net8.0
Microkernel.exe
goto :end

:error
echo. 
echo ========================================
echo   BUILD FAILED!
echo ========================================
pause

:end