namespace DataAccess.Repositories.Models;

public sealed class WalletTransactionHistoryRow
{
    public long WalletTransId { get; set; }

    public DateTime CreatedAt { get; set; }

    public decimal Amount { get; set; }

    public string TransactionType { get; set; } = string.Empty;

    public decimal? BalanceBefore { get; set; }

    public decimal? BalanceAfter { get; set; }

    public string? Description { get; set; }
}
