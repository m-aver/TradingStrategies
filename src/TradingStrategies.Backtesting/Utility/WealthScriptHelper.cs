using System.Reflection;
using WealthLab;

namespace TradingStrategies.Backtesting.Utility;

internal static class WealthScriptHelper
{
    private static readonly FieldInfo tseField = typeof(WealthScript)
        .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
        .First(static x => x.FieldType == typeof(TradingSystemExecutor));

    public static TradingSystemExecutor? ExtractExecutor(WealthScript script)
    {
        var tsExecutor = tseField.GetValue(script) as TradingSystemExecutor;
        return tsExecutor;
    }
}
