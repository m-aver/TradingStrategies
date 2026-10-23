using WealthLab;

namespace TradingStrategies.Backtesting.Utility;

public class NoCommission : Commission
{
    public static NoCommission Instance { get; } = new();

    public override string FriendlyName => nameof(NoCommission);
    public override string Description => "Yields zero commission";
    public override double Calculate(TradeType tradeType, OrderType orderType, double orderPrice, double shares, Bars bars) => 0;
}
