using Grpc.Core;
using GrpcBank.Contracts;

namespace GrpcBank.LedgerService.Services;

public sealed class LedgerServiceImpl : Ledger.LedgerBase
{
    private readonly AccountStore _accounts;
    private readonly ILogger<LedgerServiceImpl> _logger;

    public LedgerServiceImpl(AccountStore accounts, ILogger<LedgerServiceImpl> logger)
    {
        _accounts = accounts;
        _logger = logger;
    }

    public override Task<BalanceReply> GetBalance(BalanceRequest request, ServerCallContext context)
    {
        var found = _accounts.TryGetBalance(request.Account, out var minor);

        return Task.FromResult(new BalanceReply
        {
            Account = request.Account,
            AvailableMinor = found ? minor : 0,
            Currency = "USD",
            Found = found
        });
    }

    public override Task<ReserveReply> Reserve(ReserveRequest request, ServerCallContext context)
    {
        var (reserved, remaining, reason) = _accounts.Reserve(request.Account, request.AmountMinor);

        _logger.LogInformation("Reserva {Reference}: {Reserved} ({Reason})",
            request.Reference, reserved, reason);

        return Task.FromResult(new ReserveReply
        {
            Reserved = reserved,
            RemainingMinor = remaining,
            Reason = reason
        });
    }
}
