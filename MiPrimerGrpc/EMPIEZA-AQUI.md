# Mi primer gRPC — el ejemplo mínimo

Dos proyectos, un contrato, una llamada. Todo lo demás en gRPC son capas encima de esto.

---

## Correrlo

**Primera vez en tu máquina:**

```
dotnet dev-certs https --trust
```

**Verifica el SDK:**

```
dotnet --list-sdks
```

Si no ves un **10.x**, abre `Directory.Build.props` y cambia `net10.0` por `net9.0`. Una línea, y todo funciona igual.

**Arrancar:**

```
dotnet build

# Terminal 1
dotnet run --project Servidor

# Terminal 2
dotnet run --project Cliente
```

**Desde Visual Studio:** abre `MiPrimerGrpc.sln`, clic derecho en la solución → *Configurar proyectos de inicio* → *Varios proyectos de inicio* → pon **Servidor** y **Cliente** en *Iniciar*, con Servidor arriba.

Si al abrir la solución todo aparece en rojo: es normal. Las clases (`SaludoRequest`, `SaludadorClient`) no existen hasta el primer build. Compila y desaparecen.

---

## Los 3 archivos que importan

### 1. `Servidor/Protos/saludos.proto` — el contrato

```proto
service Saludador {
  rpc Saludar (SaludoRequest) returns (SaludoReply);
}

message SaludoRequest {
  string nombre = 1;
  int32  veces  = 2;
}
```

De aquí sale **todo** el código de mensajería. Traducción a C#:

| En el .proto | En el servidor | En el cliente |
|---|---|---|
| `service Saludador` | `Saludador.SaludadorBase` (clase base) | `Saludador.SaludadorClient` (stub) |
| `rpc Saludar` | método virtual a sobreescribir | método `SaludarAsync` |
| `message SaludoRequest` | `class SaludoRequest` | la misma clase |
| `string nombre = 1` | `public string Nombre { get; set; }` | igual |
| `string atendido_por` | `AtendidoPor` (snake_case → PascalCase) | igual |

**Los números `= 1`, `= 2` no son valores.** Son el identificador del campo. Protobuf manda por la red ese número en lugar del nombre — por eso el mensaje pesa una fracción de lo que pesaría en JSON. Y por eso nunca se reutilizan ni se cambian: *son* el contrato.

### 2. `Servidor/Services/SaludadorService.cs` — la implementación

```csharp
public sealed class SaludadorService : Saludador.SaludadorBase
{
    public override Task<SaludoReply> Saludar(SaludoRequest request, ServerCallContext context)
```

`SaludadorBase` no existe en ningún archivo del proyecto. La genera el compilador de protobuf en cada build.

### 3. `Cliente/Cliente.csproj` — la idea central

```xml
<Protobuf Include="..\Servidor\Protos\saludos.proto" GrpcServices="Client" />
```

El cliente **no copia** el `.proto`: apunta al mismo archivo del servidor. Un contrato, dos compilaciones. Si el servidor lo cambia, el cliente deja de compilar.

En una empresa el `.proto` no se referencia por ruta relativa: se publica como paquete NuGet interno y cada equipo lo consume. La idea es idéntica.

---

## Qué hace el ejemplo al correr

1. Te pide tu nombre.
2. Llama a `SaludarAsync` — se ve como un método local, se ejecuta en otro proceso.
3. Imprime el mensaje, el nombre de la máquina que respondió y cuánto tardó.
4. Después manda un nombre **vacío a propósito** para que veas el manejo de errores:

```csharp
catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
```

Los errores en gRPC llegan **tipados**, con un código de estado del enum. No se parsea ningún JSON de error.

---

## Los ejercicios (en este orden)

**1. Agrega un método.** En el `.proto`:

```proto
rpc Despedir (SaludoRequest) returns (SaludoReply);
```

Compila. El compilador ya te ofrece `DespedirAsync` en el cliente sin que escribas nada, y puedes sobreescribir `Despedir` en el servidor.

**2. Rompe el contrato.** Renombra `nombre` a `nombre_completo` en el `.proto`. El cliente **deja de compilar**. Ese error rojo es el argumento central de gRPC en una sola pantalla: lo que en REST sería un bug en producción, aquí es un error de build.

**3. Apaga el servidor** y corre solo el cliente. Verás `StatusCode.Unavailable` capturado limpiamente.

**4. Agrega un campo nuevo** (`string idioma = 3;`) y compila **solo el servidor**, dejando el cliente viejo. Funciona igual: los campos nuevos con números nuevos son compatibles hacia atrás. Los campos *renombrados* no. Esa asimetría es la regla de oro del versionado en protobuf.

---

## Cuando esto te resulte natural

El ciclo *cambio el proto → compilo → ambos lados se enteran* es gRPC completo. Todo lo demás son capas encima:

- **Streaming** (los 4 modos): mismo `.proto`, agregando la palabra `stream`.
- **Interceptors**: el middleware de gRPC, para logging y autenticación.
- **Deadlines**: límites de tiempo que se propagan entre servicios.
- **Health checks y balanceo**: para producción en Kubernetes.
- **JSON transcoding**: un `.proto` que sirve gRPC y REST a la vez.
