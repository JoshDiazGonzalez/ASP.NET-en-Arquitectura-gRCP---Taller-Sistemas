using System.Diagnostics;
using Grpc.Core;
using GrpcBank.Contracts;

namespace GrpcBank.FraudService.Services;

/// <summary>
/// Antifraude simulado. En un banco real aqui vive un modelo de ML
/// (normalmente en Python) y esta clase seria el adaptador gRPC.
/// La logica es de juguete a proposito: lo que importa es la FORMA
/// de la llamada, no el modelo.
/// </summary>
public sealed class FraudScoringServiceImpl : FraudScoring.FraudScoringBase
{
    private static readonly HashSet<string> HighRiskCountries =
        new(StringComparer.OrdinalIgnoreCase) { "XX", "ZZ" };

    private readonly ILogger<FraudScoringServiceImpl> _logger;

    public FraudScoringServiceImpl(ILogger<FraudScoringServiceImpl> logger) => _logger = logger;

    public override async Task<ScoreReply> Score(ScoreRequest request, ServerCallContext context)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.CardToken))
        {
            // Error tipado: el cliente lo recibe como StatusCode, no como
            // un JSON de error que haya que parsear.
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "card_token es obligatorio."));
        }

        // Simula el tiempo que tarda un modelo real de scoring.
        await Task.Delay(15, context.CancellationToken);

        var score = 5;
        var reasons = new List<string>();

        // Monto alto
        if (request.AmountMinor > 100_000) { score += 35; reasons.Add("monto elevado"); }
        else if (request.AmountMinor > 30_000) { score += 15; reasons.Add("monto medio-alto"); }

        // Pais de riesgo
        if (HighRiskCountries.Contains(request.Country)) { score += 45; reasons.Add("pais de riesgo"); }

        // Comercio electronico sin presencia de tarjeta
        if (request.Channel.Equals("ECOM", StringComparison.OrdinalIgnoreCase))
        {
            score += 12;
            reasons.Add("sin presencia de tarjeta");
        }

        // Comercio marcado
        if (request.Merchant.Contains("SOSPECHOSO", StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
            reasons.Add("comercio en lista negra");
        }

        score = Math.Clamp(score, 0, 100);
        var block = score >= 70;

        sw.Stop();

        _logger.LogInformation("Score {Score} para {Token} ({Amount})",
            score, request.CardToken, request.AmountMinor);

        return new ScoreReply
        {
            RiskScore = score,
            Block = block,
            Reason = reasons.Count == 0 ? "sin senales de riesgo" : string.Join(", ", reasons),
            LatencyMs = (int)sw.ElapsedMilliseconds
        };
    }
}
