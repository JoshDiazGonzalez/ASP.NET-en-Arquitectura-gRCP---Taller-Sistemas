using MiPrimerGrpc.Services;

var builder = WebApplication.CreateBuilder(args);

// Registra todo lo que gRPC necesita en el contenedor de dependencias.
builder.Services.AddGrpc(o => o.EnableDetailedErrors = builder.Environment.IsDevelopment());

var app = builder.Build();

// Publica el servicio. Sin esta linea el servidor arranca pero no atiende nada.
app.MapGrpcService<SaludadorService>();

// Un navegador NO puede hablar gRPC: por eso este mensaje y no un error raro.
app.MapGet("/", () =>
    "Servidor gRPC activo. Un navegador no puede llamarlo. Ejecuta el proyecto Cliente.");

app.Run();
