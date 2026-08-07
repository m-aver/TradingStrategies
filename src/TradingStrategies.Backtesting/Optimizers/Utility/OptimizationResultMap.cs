using TradingStrategies.Backtesting.Utility;
using TradingStrategies.Utilities;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Utility;

//оптимизирует поиск по результатам оптимизации
internal sealed class OptimizationResultMap
{
    private readonly OptimizationResultList _results;
    private readonly LiteDictionary<int, ArraySegment<OptimizationResult>> _resultsMap;
    private readonly OptimizationResult[] _resultsBuffer;
    private readonly bool _singleSymbol;
    private static readonly StringComparer _symbolComparer = StringComparer.OrdinalIgnoreCase;

    public OptimizationResultMap(OptimizationResultList results)
    {
        _results = results;

        _singleSymbol = results.Results
            .Select(x => x.Symbol)
            .Distinct(_symbolComparer)
            .Count() is 1;

        var valueGroups = (_singleSymbol
            ? results.Results.GroupBy(r => GetParamsHash(r.ParameterValues))
            : results.Results.GroupBy(r => GetParamsHash(r.Symbol, r.ParameterValues)));

        //build map
        _resultsBuffer = new OptimizationResult[results.Results.Count];
        var keys = new List<int>(results.Results.Count);
        var values = new List<ArraySegment<OptimizationResult>>(results.Results.Count);

        int curr = 0;
        foreach (var group in valueGroups)
        {
            var cnt = 0;
            foreach (var result in group)
            {
                _resultsBuffer[curr] = result;
                cnt++;
                curr++;
            }
            values.Add(new(_resultsBuffer, curr - cnt, cnt));
            keys.Add(group.Key);
        }

        _resultsMap = new(keys, values);
    }

    private static int GetParamsHash(string symbol, IEnumerable<double> paramValues)
    {
        return GetParamsHash(paramValues.Append(symbol.GetHashCode()));
    }

    private static int GetParamsHash(IEnumerable<double> paramValues)
    {
        int hash = 0;
        foreach (var value in paramValues)
            hash = value.GetHashCode() ^ hash;
        hash = Math.Abs(hash) + 1; //for lite
        return hash;
    }

    public OptimizationResult? FindResult(string symbol, IEnumerable<double> paramValues)
    {
        if (_singleSymbol == false && _symbolComparer.Equals(symbol, WealthLabConsts.AverageSymbolResultCode))
            return _results.FindResult(symbol, AsList(paramValues)); //пока нет нужды оптимизировать этот кейс

        var hash = _singleSymbol ? GetParamsHash(paramValues) : GetParamsHash(symbol, paramValues);
        if (_resultsMap.TryGetValue(hash, out var candidates) == false)
            return null;
        var result = candidates.FirstOrDefault(x =>
            x.ParameterValues.SequenceEqual(paramValues) &&
            (_singleSymbol || _symbolComparer.Equals(symbol, x.Symbol)));
        return result;
    }

    public double FindMetric(string symbol, string metric, IEnumerable<double> paramValues)
    {
        if (_singleSymbol == false && _symbolComparer.Equals(symbol, WealthLabConsts.AverageSymbolResultCode))
            return _results.FindMetric(symbol, metric, AsList(paramValues)); //пока нет нужды оптимизировать этот кейс

        var result = FindResult(symbol, paramValues);
        if (result is null)
            return double.NaN;
        var index = _results.Names.IndexOf(metric);
        var value = result.Results[index];
        return value;
    }

    private static List<double> AsList(IEnumerable<double> values) => (values as List<double>) ?? values.ToList();
}
