# Autorización de transacciones con gRPC — .NET 10

Proyecto mínimo que reproduce cómo un banco autoriza una compra: un gateway REST hacia afuera, dos microservicios gRPC hacia adentro, y un contrato compartido.

Es pequeño a propósito. La idea es que lo entiendas completo y lo puedas usar como base para proponer algo real.

---

## Cómo correrlo

### Paso 0

```bash
dotnet --version                  # 10.x
dotnet dev-certs https --trust    # solo la primera vez
```

### Paso 1 — Compilar

```bash
cd CardAuth
dotnet build
```

Aquí `Grpc.Tools` genera las clases C# desde los dos `.proto`. Ninguna clase de mensaje ni de cliente está escrita a mano.

### Paso 2 — Levantar los tres servicios

Necesitas **tres terminales abiertas**. El orden importa: los servicios internos primero.

```bash
# Terminal 1
dotnet run --project src/GrpcBank.FraudService     # https://localhost:7201

# Terminal 2
dotnet run --project src/GrpcBank.LedgerService    # https://localhost:7202

# Terminal 3
dotnet run --project src/GrpcBank.Gateway          # https://localhost:7100
```

### Paso 3 — Probar

Con `pruebas.http` (VS Code REST Client o Rider), o con curl:

```bash
# Compra normal -> APROBADA
curl -k -X POST https://localhost:7100/api/authorizations \
  -H "Content-Type: application/json" \
  -d '{"account":"EC0000000001","cardToken":"tok_9911","amountMinor":8000,"merchant":"SUPERMAXI","country":"EC","channel":"POS"}'

# Fondos insuficientes (esa cuenta tiene $8.00)
curl -k -X POST https://localhost:7100/api/authorizations \
  -H "Content-Type: application/json" \
  -d '{"account":"EC0000000003","cardToken":"tok_9911","amountMinor":50000,"merchant":"SUPERMAXI","country":"EC","channel":"POS"}'

# Bloqueo por fraude
curl -k -X POST https://localhost:7100/api/authorizations \
  -H "Content-Type: application/json" \
  -d '{"account":"EC0000000002","cardToken":"tok_9911","amountMinor":150000,"merchant":"TIENDA SOSPECHOSA","country":"XX","channel":"ECOM"}'
```

### Paso 4 — El benchmark

```bash
curl -k "https://localhost:7100/api/benchmark?n=500"
```

Devuelve latencia promedio y tamaño de payload de la misma consulta por gRPC y por REST/JSON, contra el mismo servicio y la misma lógica. **Ese JSON es tu diapositiva.**

---

## Correspondencia con el diagrama

| En el dibujo | En el código | Puerto |
|---|---|---|
| POS / cajero | curl, Postman, `pruebas.http` | — |
| Gateway | `GrpcBank.Gateway` | 7100 |
| Antifraude | `GrpcBank.FraudService` | 7201 |
| Saldos | `GrpcBank.LedgerService` | 7202 |
| Contratos | `GrpcBank.Contracts` | — |

La frontera del dibujo es literal en el código: el gateway es el **único** proyecto que expone REST. Los otros dos solo hablan gRPC.

---

## Las cinco ideas que este proyecto demuestra

### 1. La frontera protocolo-por-capa

Hacia afuera REST, porque el POS, Postman y una app móvil lo consumen sin fricción. Hacia adentro gRPC, porque ahí tú controlas ambos extremos y el volumen es alto. No es "gRPC vs REST": es cada uno donde corresponde.

### 2. Fan-out en paralelo

```csharp
var fraudTask = fraud.ScoreAsync(...).ResponseAsync;
var balanceTask = ledger.GetBalanceAsync(...).ResponseAsync;
await Task.WhenAll(fraudTask, balanceTask);
```

Secuencial serían 15 ms + 5 ms. En paralelo es max(15, 5). Sobre millones de transacciones diarias esa diferencia es infraestructura que no compras.

### 3. Deadline que viaja con la llamada

```csharp
var deadline = DateTime.UtcNow.AddMilliseconds(300);
```

No es un timeout local: se propaga a cada servicio. Si el antifraude se cuelga, la llamada se corta sola y el POS recibe una respuesta. **Pruébalo**: sube el `Task.Delay` del antifraude a 500 ms y mira cómo el gateway devuelve `TIMEOUT_INTERNO` en vez de quedarse esperando.

### 4. Errores tipados, no JSON de error

```csharp
catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
```

Cada situación se maneja distinto sin parsear nada. `Unavailable` significa "el servicio está caído"; `DeadlineExceeded` significa "está vivo pero lento". Son problemas diferentes y merecen respuestas diferentes.

### 5. El channel se reutiliza

`AddGrpcClient` registra un channel compartido. Si hicieras `new GrpcChannel(...)` dentro del endpoint, abrirías una conexión HTTP/2 por petición y perderías justamente la ventaja del protocolo. Es el error más común al empezar.

---

## Ejercicios, en orden de dificultad

1. **Rompe el contrato.** Renombra `risk_score` a `score` en `fraud.proto` y cambia solo el servidor. El gateway **no compila**. Ese error de compilación es el argumento central de gRPC en una sola pantalla.
2. **Provoca el timeout.** Sube el `Task.Delay` del antifraude a 500 ms.
3. **Mata un servicio** a mitad de una prueba y observa que el gateway responde `SERVICIO_NO_DISPONIBLE` en vez de reventar.
4. **Corre el benchmark con n=50, 500 y 2000** y compara. Verás que la ventaja crece con el volumen.
5. **Agrega un tercer servicio** de límites de tarjeta (cupo diario, cupo por transacción) y súmalo al fan-out. Vas a notar que el `.proto` te obliga a pensar el contrato antes de codificar — eso es una virtud, no un estorbo.
6. **Interceptors**: son el middleware de gRPC. Agrega uno que registre latencia de cada llamada interna. Es lo que en producción alimenta tus dashboards.
7. **Persistencia**: cambia `AccountStore` por EF Core. Fíjate dónde se mueve el cuello de botella.

---

## Si lo vas a presentar en el banco

**No abras con el protocolo.** Abre con un problema que ellos ya tienen.

Un guion que funciona:

1. **El problema del contrato.** Pregunta cuántos incidentes del último año fueron "el equipo X cambió un campo y no avisó". Después muestra el ejercicio 1: el cambio se convierte en error de compilación. Esto convence a gente que no se emociona con milisegundos.
2. **El número.** Corre el benchmark en vivo. Mismo servicio, misma lógica, solo cambia el protocolo. No argumentes: mide.
3. **El alcance correcto.** Muestra el diagrama y sé explícito en que la app móvil y las APIs de terceros **siguen en REST**. Que no parezca que propones reescribir todo. Un banco desconfía —con razón— de quien llega proponiendo cambiarlo todo.
4. **La propuesta concreta.** No propongas migrar el switch de autorización; nadie te va a firmar eso. Propón **una** llamada interna de alto volumen que ya exista, migrada en paralelo con la versión REST viva, y medida. Con los números en la mano, la siguiente conversación ya no es sobre gustos.

**Lo que este demo NO prueba**, y conviene que lo digas tú antes de que lo pregunten: los servicios corren en la misma máquina, sin red real, sin carga concurrente, sin base de datos. Los números son indicativos, no un benchmark de producción. Decirlo tú mismo te da credibilidad; que te lo señalen te la quita.
