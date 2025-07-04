using System.Reflection;
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

        public double CashReturnProxy { get => results.CashReturn; set => results.CashReturn = value; }
        public double MarginInterestProxy { get => results.MarginInterest; set => results.MarginInterest = value; }
        public double DividendsPaidProxy { get => results.DividendsPaid; set => results.DividendsPaid = value; }
        public DataSeries EquityCurveProxy { get => results.EquityCurve; set => results.EquityCurve = value; }
        public DataSeries CashCurveProxy { get => results.CashCurve; set => results.CashCurve = value; }
    }

    private static readonly BindingFlags PrivateFlags = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly FieldInfo TotalCommissionField = typeof(SystemResults).GetField("double_2", PrivateFlags);

    extension(SystemResults results)
    {
        public double TotalCommissionProxy { get => results.TotalCommission; set => TotalCommissionField.SetValue(results, value); }
    }
}
