using System.Runtime.InteropServices;

namespace TradingStrategies.Backtesting.Utility
{
    [StructLayout(LayoutKind.Auto)]
    internal struct LotsFactors
    {
        public double BuyFactor { get; }
        public double SellFactor { get; }

        public LotsFactors(double buyFactor, double sellFactor)
        {
            BuyFactor = buyFactor;
            SellFactor = sellFactor;
        }
    }
}
