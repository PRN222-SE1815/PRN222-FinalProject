namespace BusinessLogic.DTOs.Requests.Transactions;

public sealed class TransactionHistoryQueryRequest
{
    public DateTime? FromDateUtc { get; set; }

    public DateTime? ToDateUtc { get; set; }

    public string? SourceType { get; set; }

    public string? Status { get; set; }

    public string? Keyword { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
