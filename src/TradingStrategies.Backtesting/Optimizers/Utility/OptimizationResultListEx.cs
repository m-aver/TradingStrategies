using TradingStrategies.Backtesting.Utility;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Utility;

internal class OptimizationResultListEx : OptimizationResultList
{
    public List<OptimizationResultEx> ResultsEx { get; set; } = new();
    public List<string> ParameterNames { get; set; } = new();

    public OptimizationResultListEx()
    {
    }

    public static OptimizationResultListEx Create(
        OptimizationResultList source,
        IEnumerable<StrategyParameter> parameters,
        IEnumerable<OptimizationResultEx> results)
    {
        var resultList = new OptimizationResultListEx()
        {
            Names = source.Names,
            StrategyID = source.StrategyID,
            Scorecard = source.Scorecard,
            OptimizationMethod = source.OptimizationMethod,
            ParameterNames = parameters.Select(x => x.Name).ToList(),
        };

        foreach (var result in results)
        {
            resultList.Add(result);
        }

        return resultList;
    }

    public void Add(OptimizationResultEx optimizationResult)
    {
        base.Add(optimizationResult);
        ResultsEx.Add(optimizationResult);

        optimizationResult.ParameterNames = ParameterNames;
        optimizationResult.Names = Names;
    }

    public DateTimeRange GetDatesRange()
    {
        var allBars = ResultsEx.SelectMany(r => r.Performance.Bars).Where(b => b.Count > 0);

        if (!allBars.Any())
        {
            throw new InvalidOperationException("No bars found in datasets");
        }

        var from = DateTime.MaxValue;
        var to = DateTime.MinValue;

        foreach (var bars in allBars)
        {
            //считаем, что свечи в датасетах всегда отсортированны, других кейсов быть не должно
            var currMin = bars.Date.First();
            var currMax = bars.Date.Last();

            from = currMin < from ? currMin : from;
            to = currMax > to ? currMax : to;
        }

        if (from > to)
        {
            throw new InvalidOperationException($"Unsupported dataset");
        }

        return new DateTimeRange(from, to);
    }
}
