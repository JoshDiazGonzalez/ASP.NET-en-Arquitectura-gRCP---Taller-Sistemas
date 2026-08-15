@echo off
echo ============================================
echo  Autorizacion gRPC - iniciando los servicios
echo ============================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: no se encontro 'dotnet'. Instala el SDK de .NET 10.
  pause
  exit /b 1
)

echo Compilando...
dotnet build
if errorlevel 1 (
  echo.
  echo La compilacion fallo. Revisa los errores de arriba.
  pause
  exit /b 1
)

echo.
echo Abriendo tres ventanas. Cierralas para detener los servicios.
start "Antifraude  :7201" cmd /k dotnet run --project src\GrpcBank.FraudService
timeout /t 3 >nul
start "Saldos      :7202" cmd /k dotnet run --project src\GrpcBank.LedgerService
timeout /t 3 >nul
start "Gateway     :7100" cmd /k dotnet run --project src\GrpcBank.Gateway

echo.
echo Listo. Espera unos segundos y prueba con pruebas.http
echo o abre https://localhost:7100/api/benchmark?n=500
pause
