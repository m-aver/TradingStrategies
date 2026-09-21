using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Utility;

internal static class SystemResultsExtensions
{
    public static bool IsEmpty(this SystemResults results) => results.Positions.Count == 0; //нет позиций - нет результатов
}
