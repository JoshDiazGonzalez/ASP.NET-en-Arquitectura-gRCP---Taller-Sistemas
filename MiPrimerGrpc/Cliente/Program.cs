using Grpc.Core;
using Grpc.Net.Client;
using MiPrimerGrpc;   // este namespace viene del "option csharp_namespace" del .proto

Console.OutputEncoding = System.Text.Encoding.UTF8;

// El puerto sale de Servidor/Properties/launchSettings.json
const string Direccion = "https://localhost:7042";

Console.WriteLine("=== Mi primer cliente gRPC ===\n");

// ---------------------------------------------------------------------
// 1) EL CHANNEL: la conexion HTTP/2. Es caro de crear y thread-safe,
//    asi que se crea UNO por aplicacion y se reutiliza. Nunca uno por
//    llamada: ese es el error mas comun al empezar.
// ---------------------------------------------------------------------
using var channel = GrpcChannel.ForAddress(Direccion, new GrpcChannelOptions
{
    // El certificado de desarrollo a veces no esta en el almacen de
    // confianza (sobre todo en Linux). SOLO PARA DESARROLLO.
    HttpHandler = new SocketsHttpHandler
    {
        SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (_, _, _, _) => true
        }
    }
});

// ---------------------------------------------------------------------
// 2) EL CLIENTE (stub). Esta clase la genero el compilador desde el
//    .proto. Llamar un metodo remoto se ve igual que uno local.
// ---------------------------------------------------------------------
var cliente = new Saludador.SaludadorClient(channel);

try
{
    // ---- Llamada normal ----
    Console.Write("Tu nombre: ");
    var nombre = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(nombre)) nombre = "Ana";

    var respuesta = await cliente.SaludarAsync(new SaludoRequest
    {
        Nombre = nombre,
        Veces = 2
    });

    Console.WriteLine();
    Console.WriteLine($"  Mensaje      : {respuesta.Mensaje}");
    Console.WriteLine($"  Atendido por : {respuesta.AtendidoPor}");
    Console.WriteLine($"  Tardo        : {respuesta.Milisegundos} ms");

    // ---- Ahora provocamos un error A PROPOSITO ----
    // Mandamos el nombre vacio: el servidor lanza InvalidArgument.
    Console.WriteLine("\n=== Prueba de error (nombre vacio) ===");

    try
    {
        await cliente.SaludarAsync(new SaludoRequest { Nombre = "" });
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
    {
        // Fijate: NO parseamos ningun JSON de error.
        // El codigo de estado llega tipado y lo filtramos con "when".
        Console.WriteLine($"  Capturado [{ex.StatusCode}]: {ex.Status.Detail}");
    }
}
catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
{
    Console.WriteLine($"\nNo hay servidor en {Direccion}.");
    Console.WriteLine("Levanta primero el proyecto Servidor.");
}

Console.WriteLine("\nListo. Presiona una tecla para salir.");
Console.ReadKey();
