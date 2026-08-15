using GrpcBank.FraudService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc(o => o.EnableDetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddGrpcReflection();

var app = builder.Build();

app.MapGrpcService<FraudScoringServiceImpl>();
if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

app.MapGet("/", () => "Servicio de antifraude (gRPC) activo.");

app.Run();
