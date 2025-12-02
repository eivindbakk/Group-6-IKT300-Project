@echo off
echo Building Microkernel Solution... 

REM Build Contracts first
dotnet build Contracts/Contracts.csproj -c Debug

REM Build MetricsLogger (separate process plugin)
dotnet build Plugins/MetricsLoggerProcess/MetricsLoggerProcess.csproj -c Debug

REM Build Microkernel
dotnet build Microkernel/Microkernel.csproj -c Debug

REM Copy MetricsLogger to Microkernel output
copy /Y "Plugins\MetricsLoggerProcess\bin\Debug\net8.0\MetricsLogger.exe" "Microkernel\bin\Debug\net8.0\"
copy /Y "Plugins\MetricsLoggerProcess\bin\Debug\net8.0\MetricsLogger.dll" "Microkernel\bin\Debug\net8.0\"
copy /Y "Plugins\MetricsLoggerProcess\bin\Debug\net8.0\MetricsLogger.runtimeconfig.json" "Microkernel\bin\Debug\net8.0\"

echo. 
echo Starting Microkernel... 
cd Microkernel\bin\Debug\net8.0
Microkernel.exe