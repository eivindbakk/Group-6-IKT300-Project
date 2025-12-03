@echo off
cls

taskkill /F /IM Microkernel.exe >nul 2>&1
timeout /t 1 /nobreak >nul

echo Building solution...
echo.

REM Just build the solution file - it builds everything in the right order
dotnet build Microkernel.sln -c Debug -v q
if %ERRORLEVEL% NEQ 0 (
    echo. 
    echo BUILD FAILED!
    pause
    exit /b 1
)

echo.
echo Starting Microkernel... 
echo.

cd Microkernel\bin\Debug\net8.0
Microkernel.exe