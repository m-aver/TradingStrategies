using TradingStrategies.Backtesting.Optimizers.Utility;
using WealthLab;

namespace TradingStrategies.UnitTests;

public partial class OptimizationResultMapTests
{
    [Theory]
    [MemberData(nameof(GetTestData))]
    public void FindResult_Success(string caseName, OptimizationResultList results)
    {
        //arrange
        var map = new OptimizationResultMap(results);
        var symbols = results.Symbols;
        var paramValues = results.Results.Select(x => x.ParameterValues);

        //act && assert
        foreach ( var values in paramValues)
        {
            foreach (var symbol in symbols)
            {
                var fromMap = map.FindResult(symbol, values);
                var fromResult = results.FindResult(symbol, values);

                Assert.Equal(fromMap, fromResult);
            }
        }

        Assert.NotEmpty(caseName);
    }

    [Theory]
    [MemberData(nameof(GetTestData))]
    public void FindMetric_Success(string caseName, OptimizationResultList results)
    {
        //arrange
        var map = new OptimizationResultMap(results);
        var symbols = results.Symbols;
        var metrics = results.Names;
        var paramValues = results.Results.Select(x => x.ParameterValues);

        //act && assert
        foreach (var values in paramValues)
        {
            foreach (var symbol in symbols)
            {
                foreach (var metric in metrics)
                {
                    var fromMap = map.FindMetric(symbol, metric, values);
                    var fromResult = results.FindMetric(symbol, metric, values);

                    Assert.Equal(fromMap, fromResult);
                }
            }
        }

        Assert.NotEmpty(caseName);
    }
}
