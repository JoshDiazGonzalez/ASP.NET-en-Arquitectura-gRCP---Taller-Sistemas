using GrpcBank.LedgerService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc(o => o.EnableDetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddGrpcReflection();
builder.Services.AddSingleton<AccountStore>();

var app = builder.Build();

// ---------------------------------------------------------------------
// Este servicio expone LA MISMA logica por dos caminos:
//   gRPC (HTTP/2, binario)  y  REST/JSON (HTTP/1.1)
// No es lo que harias en produccion. Esta aqui para que puedas MEDIR
// la diferencia entre ambos con la misma logica de negocio detras.
// Ese numero es lo que vas a mostrar en el banco.
// ---------------------------------------------------------------------
app.MapGrpcService<LedgerServiceImpl>();
if (app.Environment.IsDevelopment()) app.MapGrpcReflectionService();

app.MapGet("/api/balance/{account}", (string account, AccountStore accounts) =>
{
    var found = accounts.TryGetBalance(account, out var minor);
    return Results.Ok(new
    {
        account,
        availableMinor = found ? minor : 0,
        currency = "USD",
        found
    });
});

app.MapGet("/", () => "Servicio de saldos: gRPC en /banking.ledger.v1.Ledger, REST en /api/balance/{cuenta}");

app.Run();
