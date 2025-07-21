using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class SystemperformanceExtensions
{
    extension(SystemPerformance performance)
    {
        public void AddBars(Bars bars) => performance.method_1(bars);
        public void Clear() => performance.method_2();
        public void CalculateMfeMae() => performance.method_0();

        public BarScale ScaleProxy { get => performance.Scale; set => performance.Scale = value; }
        public int BarIntervalProxy { get => performance.BarInterval; set => performance.BarInterval = value; }
        public PositionSize PositionSizeProxy { get => performance.PositionSize; set => performance.PositionSize = value; }
        public List<Position> RawTradesProxy { get => performance.RawTrades; set => performance.RawTrades = value; }
    }
}
