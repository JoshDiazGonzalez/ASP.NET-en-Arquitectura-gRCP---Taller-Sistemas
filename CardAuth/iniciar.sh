#!/usr/bin/env bash
set -e
echo "Compilando..."
dotnet build

echo "Levantando los tres servicios..."
dotnet run --project src/GrpcBank.FraudService  & PID1=$!
sleep 3
dotnet run --project src/GrpcBank.LedgerService & PID2=$!
sleep 3
dotnet run --project src/GrpcBank.Gateway       & PID3=$!

trap "kill $PID1 $PID2 $PID3 2>/dev/null" EXIT
echo ""
echo "Listo. Prueba: curl -k https://localhost:7100/api/benchmark?n=500"
echo "Ctrl+C para detener todo."
wait
