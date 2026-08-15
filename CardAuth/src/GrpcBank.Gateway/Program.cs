using System.Diagnostics;
using Grpc.Core;
using GrpcBank.Contracts;

var builder = WebApplication.CreateBuilder(args);

const string FraudAddress  = "https://localhost:7201";
const string LedgerAddress = "https://localhost:7202";

// =====================================================================
//  CLIENTES gRPC REGISTRADOS EN EL CONTENEDOR
//
//  AddGrpcClient crea un channel COMPARTIDO y reutilizado. Nunca hagas
//  "new GrpcChannel" dentro de un endpoint: abririas una conexion HTTP/2
//  por peticion y perderias justamente la ventaja del protocolo.
// =====================================================================
builder.Services
    .AddGrpcClient<FraudScoring.FraudScoringClient>(o => o.Address = new Uri(FraudAddress))
    .ConfigurePrimaryHttpMessageHandler(CreateHandler);

builder.Services
    .AddGrpcClient<Ledger.LedgerClient>(o => o.Address = new Uri(LedgerAddress))
    .ConfigurePrimaryHttpMessageHandler(CreateHandler);

// HttpClient para el gemelo REST del servicio de saldos (solo el benchmark).
builder.Services.AddHttpClient("ledger-rest", c => c.BaseAddress = new Uri(LedgerAddress))
    .ConfigurePrimaryHttpMessageHandler(CreateHandler);

var app = builder.Build();

// =====================================================================
//  ENDPOINT REAL: autorizar una transaccion
//  Hacia AFUERA es REST/JSON porque el POS, Postman o una app lo
//  consumen. Hacia ADENTRO todo es gRPC. Esa es la frontera del dibujo.
// =====================================================================
app.MapPost("/api/authorizations", async (
    AuthorizationRequest req,
    FraudScoring.FraudScoringClient fraud,
    Ledger.LedgerClient ledger,
    ILogger<Program> logger) =>
{
    var sw = Stopwatch.StartNew();

    // Presupuesto TOTAL de la operacion. El deadline VIAJA con cada
    // llamada gRPC: si el antifraude se cuelga, la llamada se corta sola
    // y el POS no queda esperando para siempre.
    var deadline = DateTime.UtcNow.AddMilliseconds(300);

    try
    {
        // ---- FAN-OUT: las dos consultas salen AL MISMO TIEMPO ----
        // Secuencial seria 15 ms + 5 ms = 20 ms.
        // En paralelo es max(15, 5) = 15 ms. Sobre millones de
        // transacciones diarias, esa diferencia es infraestructura.
        var fraudTask = fraud.ScoreAsync(new ScoreRequest
        {
            CardToken = req.CardToken,
            AmountMinor = req.AmountMinor,
            Merchant = req.Merchant ?? "",
            Country = req.Country ?? "EC",
            Channel = req.Channel ?? "POS"
        }, deadline: deadline).ResponseAsync;

        var balanceTask = ledger.GetBalanceAsync(new BalanceRequest
        {
            Account = req.Account
        }, deadline: deadline).ResponseAsync;

        await Task.WhenAll(fraudTask, balanceTask);

        var score = fraudTask.Result;
        var balance = balanceTask.Result;

        // ---- DECISION ----
        if (!balance.Found)
            return Decline("CUENTA_INEXISTENTE", "La cuenta no existe.", score, balance, sw);

        if (score.Block)
            return Decline("RECHAZO_FRAUDE", score.Reason, score, balance, sw);

        if (balance.AvailableMinor < req.AmountMinor)
            return Decline("FONDOS_INSUFICIENTES", "Saldo disponible menor al monto.", score, balance, sw);

        // ---- Solo si todo paso: reservar los fondos ----
        var reserve = await ledger.ReserveAsync(new ReserveRequest
        {
            Account = req.Account,
            AmountMinor = req.AmountMinor,
            Reference = Guid.NewGuid().ToString()[..8]
        }, deadline: deadline);

        if (!reserve.Reserved)
            return Decline("RESERVA_FALLIDA", reserve.Reason, score, balance, sw);

        sw.Stop();
        logger.LogInformation("Autorizada {Account} por {Amount} en {Ms} ms",
            req.Account, req.AmountMinor, sw.ElapsedMilliseconds);

        return Results.Ok(new
        {
            decision = "APROBADA",
            riskScore = score.RiskScore,
            riskReason = score.Reason,
            reservedMinor = req.AmountMinor,
            remainingMinor = reserve.RemainingMinor,
            totalLatencyMs = sw.ElapsedMilliseconds
        });
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
    {
        // Un servicio interno tardo demasiado. En un motor de autorizacion
        // real esto se resuelve con una politica (aprobar bajo riesgo,
        // rechazar el resto), nunca dejando al POS esperando.
        return Results.Json(new { decision = "RECHAZADA", reason = "TIMEOUT_INTERNO" }, statusCode: 504);
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
    {
        return Results.Json(new
        {
            decision = "RECHAZADA",
            reason = "SERVICIO_NO_DISPONIBLE",
            detail = "Verifica que FraudService y LedgerService esten corriendo."
        }, statusCode: 503);
    }
    catch (RpcException ex)
    {
        return Results.Json(new { decision = "ERROR", status = ex.StatusCode.ToString(), detail = ex.Status.Detail },
                            statusCode: 400);
    }
});

// =====================================================================
//  BENCHMARK: la misma consulta por gRPC y por REST/JSON
//  Este endpoint es tu diapositiva. Mide, no argumenta.
// =====================================================================
app.MapGet("/api/benchmark", async (
    int? n,
    Ledger.LedgerClient ledger,
    IHttpClientFactory httpFactory) =>
{
    var iterations = Math.Clamp(n ?? 300, 10, 5000);
    const string account = "EC0000000001";

    var http = httpFactory.CreateClient("ledger-rest");

    // Calentamiento: la primera llamada paga JIT y handshake TLS.
    // Medir sin calentar da numeros basura.
    await ledger.GetBalanceAsync(new BalanceRequest { Account = account });
    await http.GetAsync($"/api/balance/{account}");

    var grpcSw = Stopwatch.StartNew();
    for (var i = 0; i < iterations; i++)
        await ledger.GetBalanceAsync(new BalanceRequest { Account = account });
    grpcSw.Stop();

    var restSw = Stopwatch.StartNew();
    for (var i = 0; i < iterations; i++)
    {
        var res = await http.GetAsync($"/api/balance/{account}");
        await res.Content.ReadAsStringAsync();
    }
    restSw.Stop();

    // Tamano del payload de UNA respuesta en cada formato.
    var grpcBytes = new BalanceReply
    {
        Account = account, AvailableMinor = 45_000, Currency = "USD", Found = true
    }.CalculateSize();

    var restBody = await (await http.GetAsync($"/api/balance/{account}")).Content.ReadAsStringAsync();

    return Results.Ok(new
    {
        iterations,
        grpc = new
        {
            totalMs = grpcSw.ElapsedMilliseconds,
            avgMs = Math.Round(grpcSw.Elapsed.TotalMilliseconds / iterations, 3),
            payloadBytes = grpcBytes
        },
        rest = new
        {
            totalMs = restSw.ElapsedMilliseconds,
            avgMs = Math.Round(restSw.Elapsed.TotalMilliseconds / iterations, 3),
            payloadBytes = restBody.Length
        },
        nota = "Mismo servicio, misma logica. Solo cambia el protocolo."
    });
});

app.MapGet("/", () => "Gateway activo. POST /api/authorizations | GET /api/benchmark?n=300");

app.Run();

// ---------------------------------------------------------------------
static IResult Decline(string reason, string detail, ScoreReply score, BalanceReply balance, Stopwatch sw)
{
    sw.Stop();
    return Results.Ok(new
    {
        decision = "RECHAZADA",
        reason,
        detail,
        riskScore = score.RiskScore,
        availableMinor = balance.AvailableMinor,
        totalLatencyMs = sw.ElapsedMilliseconds
    });
}

// El certificado de desarrollo no siempre esta en el almacen de confianza
// (sobre todo en Linux y en contenedores). SOLO PARA DESARROLLO.
static HttpMessageHandler CreateHandler() => new SocketsHttpHandler
{
    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
    {
        RemoteCertificateValidationCallback = (_, _, _, _) => true
    },
    PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
    EnableMultipleHttp2Connections = true
};

record AuthorizationRequest(
    string Account,
    string CardToken,
    long AmountMinor,
    string? Merchant,
    string? Country,
    string? Channel);
