namespace DataAccess.Repositories.Models;

public sealed class PaymentTransactionHistoryRow
{
    public long TransactionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string MoMoOrderId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? LocalMessage { get; set; }

    public string? OrderInfo { get; set; }
}
