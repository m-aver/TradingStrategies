using WealthLab;

namespace TradingStrategies.Backtesting.Utility;

public static class PositionExtensions
{
    extension(Position position)
    {
        public TradeType EntryTradeType => position.PositionType switch
        {
            PositionType.Long => TradeType.Buy,
            PositionType.Short => TradeType.Short,
            _ => throw new ArgumentException($"Invalid {nameof(position.PositionType)}: {position.PositionType}"),
        };

        public TradeType ExitTradeType => position.PositionType switch
        {
            PositionType.Long => TradeType.Sell,
            PositionType.Short => TradeType.Cover,
            _ => throw new ArgumentException($"Invalid {nameof(position.PositionType)}: {position.PositionType}"),
        };
    }
}
