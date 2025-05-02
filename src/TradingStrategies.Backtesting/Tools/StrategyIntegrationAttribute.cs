using TradingStrategies.Backtesting.Core;

namespace TradingStrategies.Backtesting.Tools;

/// <summary>
/// Marks strategy to generate code for WealthLab integration.
/// Strategy must implement <see cref="IStrategyExecuter"/> and have constructor with one <see cref="WealthScriptWrapper"/>
/// </summary>
/// /// <remarks>
/// WARN: don't rename or move namespace, used for code generation
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class StrategyIntegrationAttribute(string StrategyName) : Attribute
{
    public string StrategyName { get; } = StrategyName;
}
