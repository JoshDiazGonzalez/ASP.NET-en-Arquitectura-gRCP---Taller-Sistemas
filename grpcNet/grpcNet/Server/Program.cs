const int Port = 5000;
Grpc.Core.Server server = null;
try 
{
    server = new Grpc.Core.Server()
    {
        Ports = { new Grpc.Core.ServerPort("localhost", Port, Grpc.Core.ServerCredentials.Insecure)}
    };
    server.Start();
    Console.WriteLine("El servidor se esta ejecutando en el puerto :" + Port);
    Console.ReadKey();
}
catch (IOException e)
{
    Console.WriteLine("Errores en el servidor" + e.Message);
}
finally
{
    if(server != null)
        server.ShutdownAsync().Wait();
}