namespace HiveSpace.BuildingBlocks.Money;

/// <summary>A currency the platform knows (FR-CUR-001). Adding one is configuration only (NFR-MNT-009).</summary>
public sealed record Currency(string Code, string Symbol, int MinorUnitExponent, bool Enabled);

/// <summary>The currency table. VND is the only enabled currency in the MVP (exponent 0, whole dong).</summary>
public static class Currencies
{
    public static readonly Currency Vnd = new("VND", "₫", 0, Enabled: true);

    private static readonly Dictionary<string, Currency> Table = new(StringComparer.Ordinal)
    {
        [Vnd.Code] = Vnd,
    };

    public static Currency Get(string code) =>
        Table.TryGetValue(code, out var currency) && currency.Enabled
            ? currency
            : throw new ArgumentException($"Currency '{code}' is not enabled.", nameof(code));
}

/// <summary>An amount in minor units plus a currency code (ADR-0010, NFR-MNT-008). Never a bare number.</summary>
public readonly record struct Money(long Amount, string Currency)
{
    public static Money Of(long amount, string currency)
    {
        Currencies.Get(currency);
        return new Money(amount, currency);
    }

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(checked(left.Amount + right.Amount), left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(checked(left.Amount - right.Amount), left.Currency);
    }

    public Money Add(Money other) => this + other;

    public Money Subtract(Money other) => this - other;

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot combine {left.Currency} with {right.Currency}.");
        }
    }
}
