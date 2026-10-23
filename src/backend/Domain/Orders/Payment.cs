namespace backend.Domain.Orders
{
    using NUlid;
    public sealed record Payment
    {
        public Ulid Id { get; private init; } = Ulid.NewUlid();
        public Ulid OrderId { get; private init; }
        public Order Order { get; private init; }
        public decimal Amount { get; private init; }
        public string Currency { get; private init; }
        public decimal? AmountUA { get; private init; }
        public decimal? ExchangeRate { get; private init; }
        public string? Provider { get; private init; }
        public string? Invoice { get; private init; }
        public int TotalInstallments { get; private init; }
        public int? CurrentInstallment { get; private init; }

        public string Status { get; private set; }
        public DateTime? PaidAt { get; private set; }
        public DateTime? PaidUntil { get; private set; }


        private Payment() { }

        public static void ValidateTerms(decimal amount, string? currency, string? status,
            int totalInstallments, int? currentInstallment, decimal? amountUA, decimal? exchangeRate,
            string? provider, string? invoice, DateTime? paidAt, DateTime? paidUntil)
        {
            if (amount <= 0 || amount > 9999999999.99m || decimal.Round(amount, 2) != amount)
                throw new ArgumentException("Amount must be positive, fit decimal(12,2), and have at most two decimal places.", nameof(amount));
            if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length is < 3 or > 10 ||
                !currency.Trim().All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
                throw new ArgumentException("Currency must contain 3 to 10 letters.", nameof(currency));
            if (!new[] { "Pending", "Paid", "Cancelled" }.Contains(status?.Trim(), StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("Status must be Pending, Paid or Cancelled.", nameof(status));
            if (totalInstallments is < 1 or > 120)
                throw new ArgumentException("Total installments must be between 1 and 120.", nameof(totalInstallments));
            if (currentInstallment is { } installment && (installment < 1 || installment > totalInstallments))
                throw new ArgumentException("Current installment must be between 1 and total installments.", nameof(currentInstallment));
            if (amountUA is { } local && (local < 0 || local > 9999999999.99m || decimal.Round(local, 2) != local))
                throw new ArgumentException("Local amount must fit decimal(12,2) and cannot be negative.", nameof(amountUA));
            if (exchangeRate is { } rate && (rate <= 0 || rate > 99999999.9999m || decimal.Round(rate, 4) != rate))
                throw new ArgumentException("Exchange rate must be positive and fit decimal(12,4).", nameof(exchangeRate));
            if (provider?.Length > 100 || invoice?.Length > 100)
                throw new ArgumentException("Provider and invoice must not exceed 100 characters.");
            if (paidAt is { Kind: not DateTimeKind.Utc } || paidUntil is { Kind: not DateTimeKind.Utc })
                throw new ArgumentException("Payment timestamps must be UTC.", nameof(paidAt));
            if (paidAt > paidUntil)
                throw new ArgumentException("PaidUntil must not precede PaidAt.", nameof(paidUntil));
        }

        public void MarkAsPaid(DateTime paidAt, DateTime? paidUntil = null)
        {
            ValidateTerms(Amount, Currency, "Paid", TotalInstallments, CurrentInstallment,
                AmountUA, ExchangeRate, Provider, Invoice, paidAt, paidUntil);
            Status = "Paid";
            PaidAt = paidAt;
            PaidUntil = paidUntil;
        }

        public void Cancel()
        {
            Status = "Cancelled";
        }

        // Factory method to create a new Payment instance

        public static Payment Create(
        Ulid orderId,
        decimal amount,
        string currency,
        string status = "Pending",
        int totalInstallments = 1,
        int? currentInstallment = null,
        decimal? amountUA = null,
        decimal? exchangeRate = null,
        string? provider = null,
        string? invoice = null,
        DateTime? paidAt = null,
        DateTime? paidUntil = null)
        {
            ValidateTerms(amount, currency, status, totalInstallments, currentInstallment,
                amountUA, exchangeRate, provider, invoice, paidAt, paidUntil);

            return new Payment
            {
                OrderId = orderId,
                Amount = amount,
                Currency = currency.Trim().ToUpperInvariant(),
                AmountUA = amountUA,
                ExchangeRate = exchangeRate,
                Provider = provider,
                Invoice = invoice,
                TotalInstallments = totalInstallments,
                CurrentInstallment = currentInstallment,
                Status = new[] { "Pending", "Paid", "Cancelled" }.First(x => string.Equals(x, status.Trim(), StringComparison.OrdinalIgnoreCase)),
                PaidAt = paidAt,
                PaidUntil = paidUntil
            };
        }
        public static Payment Update(
            Payment payment,
            decimal? amount = null,
            string? currency = null,
            string? status = null,
            int? totalInstallments = null,
            int? currentInstallment = null,
            decimal? amountUA = null,
            decimal? exchangeRate = null,
            string? provider = null,
            string? invoice = null,
            DateTime? paidAt = null,
            DateTime? paidUntil = null)
        {
            ValidateTerms(amount ?? payment.Amount, currency ?? payment.Currency, status ?? payment.Status,
                totalInstallments ?? payment.TotalInstallments, currentInstallment ?? payment.CurrentInstallment,
                amountUA ?? payment.AmountUA, exchangeRate ?? payment.ExchangeRate, provider ?? payment.Provider,
                invoice ?? payment.Invoice, paidAt ?? payment.PaidAt, paidUntil ?? payment.PaidUntil);
            return payment with
            {
                Amount = amount ?? payment.Amount,
                Currency = (currency ?? payment.Currency).Trim().ToUpperInvariant(),
                AmountUA = amountUA ?? payment.AmountUA,
                ExchangeRate = exchangeRate ?? payment.ExchangeRate,
                Provider = provider ?? payment.Provider,
                Invoice = invoice ?? payment.Invoice,
                TotalInstallments = totalInstallments ?? payment.TotalInstallments,
                CurrentInstallment = currentInstallment ?? payment.CurrentInstallment,
                Status = new[] { "Pending", "Paid", "Cancelled" }.First(x => string.Equals(x, (status ?? payment.Status).Trim(), StringComparison.OrdinalIgnoreCase)),
                PaidAt = paidAt ?? payment.PaidAt,
                PaidUntil = paidUntil ?? payment.PaidUntil
            };
        }
    }
}
