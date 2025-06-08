using System.Reflection;
using TradingStrategies.Backtesting.Core;

namespace TradingStrategies.Backtesting.Tools;

internal static class GeneratedStrategyFactory
{
    public static IStrategyExecuter? CreateStrategyInstance(GeneratedWealthScriptWrapper wrapper)
    {
        var strategyAttribute = wrapper.GetType().GetCustomAttribute<StrategyIntegrationAttribute>();
        if (strategyAttribute is null)
        {
            return null;
        }
        var strategyName = strategyAttribute.StrategyName;

        var typeMapping = StrategyIntegrationAttributeMappings.StrategyNameToStrategyTypeMapping;
        if (typeMapping.TryGetValue(strategyName, out var strategyType))
        {
            var strategy = Activator.CreateInstance(strategyType, wrapper);
            return strategy as IStrategyExecuter ?? throw InvalidStrategy(strategyName, strategyType);
        }
        return null;
    }

    private static InvalidOperationException InvalidStrategy(string name, Type type) => new InvalidOperationException(
        $"Strategy is not {nameof(IStrategyExecuter)}, strategy name: {name}, strategy type: {type.Name}");
}
