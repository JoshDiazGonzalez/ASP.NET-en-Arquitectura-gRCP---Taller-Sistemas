using System.Collections.Concurrent;

namespace GrpcBank.LedgerService.Services;

/// <summary>
/// Cuentas en memoria. En produccion seria la base del core bancario.
/// Todo en unidades menores (centavos) como entero exacto: jamas double.
/// </summary>
public sealed class AccountStore
{
    private readonly ConcurrentDictionary<string, long> _balances = new();
    private readonly object _reserveLock = new();

    public AccountStore()
    {
        _balances["EC0000000001"] = 45_000;    // $450.00
        _balances["EC0000000002"] = 1_250_000; // $12,500.00
        _balances["EC0000000003"] = 800;       // $8.00  -> ideal para probar rechazo
    }

    public bool TryGetBalance(string account, out long minor)
        => _balances.TryGetValue(account, out minor);

    /// <summary>
    /// Verificar-y-descontar debe ser atomico. Si lo haces en dos pasos
    /// sin lock, dos transacciones simultaneas pueden sobregirar la cuenta.
    /// Este es el bug clasico de un motor de autorizacion.
    /// </summary>
    public (bool reserved, long remaining, string reason) Reserve(string account, long amountMinor)
    {
        if (amountMinor <= 0) return (false, 0, "El monto debe ser positivo.");

        lock (_reserveLock)
        {
            if (!_balances.TryGetValue(account, out var current))
                return (false, 0, "Cuenta inexistente.");

            if (current < amountMinor)
                return (false, current, "Fondos insuficientes.");

            var remaining = current - amountMinor;
            _balances[account] = remaining;
            return (true, remaining, "OK");
        }
    }
}
