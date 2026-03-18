using BusinessLogic.Services.Models;

namespace BusinessLogic.Services.Interfaces;

public interface IWeightedTotalCalculator
{
    decimal CalculateTotal(IEnumerable<WeightedScoreInput> inputs);
}
