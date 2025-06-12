using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class SystemResultsExtensions
{
    extension(SystemResults results)
    {
        public void Clear(bool avoidClearingEquity) => results.method_7(avoidClearingEquity);
        public void AddPosition(Position position) => results.method_4(position);
        public void AddAlert(Alert alert) => results.method_5(alert);
        public void SetPosSizerPositions(PosSizer posSizer) => results.method_9(posSizer);

        public double CurrentCash { get => results.CurrentCash; set => results.CurrentCash = value; }
        public double CurrentEquity { get => results.CurrentEquity; set => results.CurrentEquity = value; }
    }
}
