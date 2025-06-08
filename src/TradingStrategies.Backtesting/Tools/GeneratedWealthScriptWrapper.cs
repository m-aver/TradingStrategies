using TradingStrategies.Backtesting.Core;

namespace TradingStrategies.Backtesting.Tools;

/// <summary>
/// Used for create derived WealthScript types from <see cref="StrategyHelperGenerator"/> source generator
/// </summary>
/// <remarks>
/// WARN: don't rename or move namespace, used for code generation
/// </remarks>
public class GeneratedWealthScriptWrapper : WealthScriptWrapper
{
    //can't use any instance fields because of strategy created inside base ctor
};
