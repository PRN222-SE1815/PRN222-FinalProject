namespace BusinessLogic.DTOs.Response;

public sealed class MoMoCreatePaymentResponse
{
    public string PayUrl { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public long TransactionId { get; set; }
}
