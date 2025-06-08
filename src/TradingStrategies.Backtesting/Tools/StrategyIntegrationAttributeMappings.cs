using System.Reflection;
using TradingStrategies.Backtesting.Core;

namespace TradingStrategies.Backtesting.Tools;

internal static class StrategyIntegrationAttributeMappings
{
    public static readonly IReadOnlyDictionary<string, Type> StrategyNameToStrategyTypeMapping;

    static StrategyIntegrationAttributeMappings()
    {
        StrategyNameToStrategyTypeMapping = GetMapping();
    }

    private static IReadOnlyDictionary<string, Type> GetMapping()
    {
        var mapping = new Dictionary<string, Type>();

        var strategyTypes = GetStrategyTypes();

        foreach (var strategyType in strategyTypes)
        {
            var strategyAttribute = strategyType.GetCustomAttribute<StrategyIntegrationAttribute>();

            if (strategyAttribute is null ||
                mapping.ContainsKey(strategyAttribute.StrategyName))
            {
                continue;
            }

            mapping[strategyAttribute.StrategyName] = strategyType;
        }

        return mapping;
    }

    private static IReadOnlyCollection<Type> GetStrategyTypes()
    {
        var type = typeof(IStrategyExecuter);

        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(type.IsAssignableFrom)
            .ToArray();

        return types;
    }

    //meet problems with loading Systems.Collections.Immutable
    public static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null);
        }
    }
}
