namespace BusinessLogic.DTOs.Responses.Transactions;

public sealed class TransactionHistoryItemDto
{
    public DateTime OccurredAt { get; set; }

    public string SourceType { get; set; } = string.Empty;

    public string ReferenceCode { get; set; } = string.Empty;

    public string? TransactionType { get; set; }

    public string? Status { get; set; }

    public decimal Amount { get; set; }

    public decimal? BalanceBefore { get; set; }

    public decimal? BalanceAfter { get; set; }

    public string? Description { get; set; }

    public long? ReferenceId { get; set; }
}
