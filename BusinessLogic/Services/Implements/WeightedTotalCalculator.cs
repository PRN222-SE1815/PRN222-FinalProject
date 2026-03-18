using BusinessLogic.Services.Interfaces;
using BusinessLogic.Services.Models;

namespace BusinessLogic.Services.Implements;

public sealed class WeightedTotalCalculator : IWeightedTotalCalculator
{
    public decimal CalculateTotal(IEnumerable<WeightedScoreInput> inputs)
    {
        if (inputs is null)
        {
            return 0m;
        }

        var total = 0m;
        foreach (var input in inputs)
        {
            var score = input?.Score ?? 0m;
            var weight = input?.Weight ?? 0m;
            total += score * weight;
        }

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }
}
