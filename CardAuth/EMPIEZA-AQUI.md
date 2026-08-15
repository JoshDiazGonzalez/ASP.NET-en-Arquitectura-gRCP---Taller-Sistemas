# Empieza aquí

## Requisito

```
dotnet --list-sdks
```

Debe aparecer un **10.x**. Si solo tienes 9.x, abre `Directory.Build.props`
y cambia `net10.0` por `net9.0`. Todo el código funciona igual.

## Primera vez

```
dotnet dev-certs https --trust
```

## Arrancar

**Windows:** doble clic en `iniciar.cmd`

**Linux / macOS:** `./iniciar.sh`

**Visual Studio:** abre `CardAuth.sln`, clic derecho en la solución →
*Configurar proyectos de inicio* → *Varios proyectos de inicio*, y pon
en **Iniciar** estos tres, en este orden:

1. GrpcBank.FraudService
2. GrpcBank.LedgerService
3. GrpcBank.Gateway

(GrpcBank.Contracts queda en *Ninguno*: es una biblioteca, no se ejecuta.)

## Probar

Abre `pruebas.http` en Visual Studio y dale a *Send request*, o:

```
curl -k https://localhost:7100/api/benchmark?n=500
```

## Si algo sale rojo al abrir la solución

Es normal. Las clases de los mensajes (`ScoreRequest`, `LedgerClient`,
etc.) **no existen hasta el primer build**: las genera `Grpc.Tools` a
partir de los archivos `.proto`. Compila primero y los errores
desaparecen.

Si persisten, corre `dotnet build` en la terminal (no en Visual Studio)
y lee el primer error: suele explicar todos los demás.

El detalle completo del proyecto está en `README.md`.
