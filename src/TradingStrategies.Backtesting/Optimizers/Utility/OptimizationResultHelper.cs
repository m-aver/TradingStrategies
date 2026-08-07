using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Utility;

internal static class OptimizationResultHelper
{
    //восстанавливает отсортированные значения параметров оптимизации
    public static List<double>[] GetParameterValues(OptimizationResultList results)
    {
        if (results.Results.Count == 0)
        {
            return [];
        }

        List<HashSet<double>> values = new(results.Results[0].ParameterValues.Count);

        foreach (var result in results.Results)
        {
            for (int i = 0; i < result.ParameterValues.Count; i++)
            {
                if (i >= values.Count)
                {
                    values.Add(new(results.Results.Count));
                }

                values[i].Add(result.ParameterValues[i]);
            }
        }

        List<double>[] output = new List<double>[values.Count];

        for (int i = 0; i < values.Count; i++)
        {
            (output[i] = values[i].ToList()).Sort();
        }

        return output;
    }
}
