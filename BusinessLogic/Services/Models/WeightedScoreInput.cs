namespace BusinessLogic.Services.Models;

public sealed class WeightedScoreInput
{
    public decimal? Score { get; init; }

    public decimal? Weight { get; init; }
}
