using System.Diagnostics;
using Grpc.Core;

namespace MiPrimerGrpc.Services;

/// <summary>
/// SaludadorBase NO existe en ningun archivo del proyecto.
/// La genera el compilador de protobuf a partir de saludos.proto,
/// en cada build. Si abres el .proto y agregas un rpc nuevo, aqui
/// aparecera un metodo virtual mas listo para sobreescribir.
/// </summary>
public sealed class SaludadorService : Saludador.SaludadorBase
{
    private readonly ILogger<SaludadorService> _logger;

    public SaludadorService(ILogger<SaludadorService> logger) => _logger = logger;

    // "override" porque estamos reemplazando el metodo de la clase generada.
    // ServerCallContext trae metadatos de la llamada: deadline, cancelacion,
    // headers, IP del cliente.
    public override Task<SaludoReply> Saludar(SaludoRequest request, ServerCallContext context)
    {
        var sw = Stopwatch.StartNew();

        // Validacion: los errores en gRPC se lanzan como RpcException con un
        // StatusCode. El cliente los recibe TIPADOS, sin parsear ningun JSON.
        if (string.IsNullOrWhiteSpace(request.Nombre))
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "El nombre no puede estar vacio."));
        }

        var veces = request.Veces <= 0 ? 1 : Math.Min(request.Veces, 5);

        var mensaje = string.Join(" ", Enumerable.Repeat($"Hola {request.Nombre}!", veces));

        _logger.LogInformation("Saludando a {Nombre} ({Veces} veces)", request.Nombre, veces);

        sw.Stop();

        // Devolvemos un objeto de la clase generada. El framework lo serializa
        // a binario y lo manda por HTTP/2. Nosotros no tocamos nada de eso.
        return Task.FromResult(new SaludoReply
        {
            Mensaje = mensaje,
            AtendidoPor = Environment.MachineName,
            Milisegundos = (int)sw.ElapsedMilliseconds
        });
    }
}
