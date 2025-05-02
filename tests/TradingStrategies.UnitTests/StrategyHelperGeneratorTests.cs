using TradingStrategies.Generators;

namespace TradingStrategies.UnitTests;

public class StrategyHelperGeneratorTests
{
    [Theory]
    [InlineData("nameof(StrategyHelperGeneratorTests)", "\"StrategyHelperGeneratorTests\"")]
    [InlineData("nameof( StrategyHelperGeneratorTests )", "\"StrategyHelperGeneratorTests\"")]
    [InlineData("Nameof(StrategyHelperGeneratorTests)", "Nameof(StrategyHelperGeneratorTests)")]
    [InlineData("abc1 + nameof(StrategyHelperGeneratorTests) + xyz", "abc1 + \"StrategyHelperGeneratorTests\" + xyz")]
    public void NormalizeName(string inputExpression, string outputExpression)
    {
        var result = StrategyHelperGenerator.NormalizeName(inputExpression);
        Assert.Equal(outputExpression, result);
    }
}
