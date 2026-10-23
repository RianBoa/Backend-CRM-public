namespace backend.Domain.Clients;

public sealed record Financials
{
    public decimal InitialBudget { get; }
    public decimal TotalEarnings { get; }

    public static readonly Financials Empty = new(0, 0);

    public Financials(decimal initialBudget, decimal totalEarnings)
    {
        if (initialBudget < 0)
            throw new ArgumentOutOfRangeException(nameof(initialBudget), "Initial budget cannot be negative.");
        if (totalEarnings < 0)
            throw new ArgumentOutOfRangeException(nameof(totalEarnings), "Total earnings cannot be negative.");

        InitialBudget = decimal.Round(initialBudget, 2);
        TotalEarnings = decimal.Round(totalEarnings, 2);
    }


    public Financials AddEarnings(decimal amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        return new Financials(InitialBudget, TotalEarnings + decimal.Round(amount, 2));
    }

    public Financials WithInitialBudget(decimal newBudget)
    {
        return new Financials(newBudget, TotalEarnings);
    }

    public override string ToString() => $"Init: {InitialBudget}, Earned: {TotalEarnings}";
}
