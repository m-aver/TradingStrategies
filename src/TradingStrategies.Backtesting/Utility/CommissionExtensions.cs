using WealthLab;

namespace TradingStrategies.Backtesting.Utility;

internal static class CommissionExtensions
{
    public static double Calculate(this Commission commission, Position position)
    {
        var entryCommision = commission.Calculate(position.EntryTradeType, position.EntryOrderType, position.EntryPrice, position.Shares, position.Bars);
        var exitCommission = !position.Active ? commission.Calculate(position.ExitTradeType, position.ExitOrderType, position.ExitPrice, position.Shares, position.Bars) : 0.0;

        return entryCommision + exitCommission;
    }
}
