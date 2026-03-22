namespace BusinessLogic.DTOs.Responses;

public sealed class WalletBalanceResponse
{
    public int WalletId { get; set; }
    public decimal Balance { get; set; }
    public DateTime LastUpdated { get; set; }
}
