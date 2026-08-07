using WealthLab;

namespace TradingStrategies.UnitTests;

public partial class OptimizationResultMapTests
{
    public static IEnumerable<object[]> GetTestData()
    {
        return GetTestResults().Select(x => new object[] { x.name, x.result });
    }

    private static IEnumerable<(OptimizationResultList result, string name)> GetTestResults()
    {
        yield return (GetEmptyResult(), nameof(GetEmptyResult));
        yield return (GetSingleResult(), nameof(GetSingleResult));
        yield return (GetSingleSymbolResults(), nameof(GetSingleSymbolResults));
        yield return (GetManyResults(), nameof(GetManyResults));
    }

    private static OptimizationResultList GetEmptyResult()
    {
        return new OptimizationResultList()
        {
            Names = [],
            Results = [],
            Symbols = [],
        };
    }

    private static OptimizationResultList GetSingleResult()
    {
        var result = new OptimizationResult()
        {
            ParameterValues = [1, 2, 5],
            Results = [1, 1],
            Symbol = "test-symbol"
        };

        var results = new OptimizationResultList()
        {
            Names = ["test-metric-1", "test-metric-2"]
        };
        results.Add(result);

        return results;
    }

    private static OptimizationResultList GetSingleSymbolResults()
    {
        var optResults = new OptimizationResult[]
        {
            new()
            {
                ParameterValues = [1, 2, 5],
                Results = [1, 1],
                Symbol = "test-symbol"
            },
            new()
            {
                ParameterValues = [1, 4, 5],
                Results = [2, 2],
                Symbol = "test-symbol"
            },
            new()
            {
                ParameterValues = [1, 2, 3],
                Results = [3, 3],
                Symbol = "test-symbol"
            }
        };

        var results = new OptimizationResultList()
        {
            Names = ["test-metric-1", "test-metric-2"]
        };
        foreach ( var result in optResults)
        {
            results.Add(result);
        }

        return results;
    }

    private static OptimizationResultList GetManyResults()
    {
        const int count = 1000;
        var random = Random.Shared;

        var optResults = Enumerable
            .Range(0, count)
            .Select(i => new OptimizationResult()
            {
                ParameterValues = [random.Next(10), random.Next(10), random.Next(10), random.Next(10)],
                Results = [1, 1],
                Symbol = $"test-symbol-{i % 5}"
            })
            .ToArray();

        var results = new OptimizationResultList()
        {
            Names = ["test-metric-1", "test-metric-2"]
        };
        foreach (var result in optResults)
        {
            results.Add(result);
        }

        return results;
    }
}
