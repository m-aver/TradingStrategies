using TradingStrategies.Backtesting.Optimizers.Own;
using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

//for consistent naming with natives

public static class SystemPerformanceExtensions
{
    extension(SystemPerformanceOwn performance)
    {
        public BarScale ScaleProxy { get => performance.Scale; set => performance.Scale = value; }
        public int BarIntervalProxy { get => performance.BarInterval; set => performance.BarInterval = value; }
        public PositionSize PositionSizeProxy { get => performance.PositionSize; set => performance.PositionSize = value; }
        public List<Position> RawTradesProxy { get => performance.RawTrades; set => performance.RawTrades = value; }
    }
}
