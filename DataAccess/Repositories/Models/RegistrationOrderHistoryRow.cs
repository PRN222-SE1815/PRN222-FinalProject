namespace DataAccess.Repositories.Models;

public sealed class RegistrationOrderHistoryRow
{
    public long OrderId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public string OrderStatus { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public string? FailureReason { get; set; }
}
