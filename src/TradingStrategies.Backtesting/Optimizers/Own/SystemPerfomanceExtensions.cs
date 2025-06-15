using System.Reflection;
using TradingStrategies.Backtesting.Optimizers.Own;
using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

//for consistent naming with natives

public static class SystemPerformanceExtensions
{
    extension(SystemPerformanceOwn performance)
    {
        public void AddBars(Bars bars) => performance.method_1(bars);
        public void Clear() => performance.method_2();
        public void CalculateMfeMae() => performance.method_0();

        public BarScale ScaleProxy { get => performance.Scale; set => performance.Scale = value; }
        public int BarIntervalProxy { get => performance.BarInterval; set => performance.BarInterval = value; }
        public PositionSize PositionSizeProxy { get => performance.PositionSize; set => performance.PositionSize = value; }
        public List<Position> RawTradesProxy { get => performance.RawTrades; set => performance.RawTrades = value; }
    }

    extension(SystemResultsOwn results)
    {
        public void Clear(bool avoidClearingEquity) => results.method_7(avoidClearingEquity);
        public void AddPosition(Position position) => results.method_4(position);
        public void AddAlert(Alert alert) => results.method_5(alert);
        public void SetPosSizerPositions(PosSizer posSizer) => results.method_9(posSizer);

        public double CurrentCash { get => results.CurrentCash; set => results.CurrentCash = value; }
        public double CurrentEquity { get => results.CurrentEquity; set => results.CurrentEquity = value; }
    }

    private static readonly Type SystemResultsType = typeof(SystemResults);
    private static readonly BindingFlags PrivateFlags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FieldInfo TotalCommissionField = SystemResultsType.GetField("double_2", PrivateFlags);

    extension(SystemResults results)
    {
        public double TotalCommissionProxy { get => results.TotalCommission; set => TotalCommissionField.SetValue(results, value); }
    }
}
