using Vaxi;

const string serverPoint = "127.0.0.1:5000";

Grpc.Core.Channel canal = new Grpc.Core.Channel(serverPoint, Grpc.Core.ChannelCredentials.Insecure);

canal.ConnectAsync().ContinueWith((task) =>
{
    if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
    {
        Console.WriteLine("El cliente se conecto al servidor GRPC correctamente");
    }
});

var client = new Operaciones.OperacionesClient(canal);

canal.ShutdownAsync().Wait();
Console.ReadKey();