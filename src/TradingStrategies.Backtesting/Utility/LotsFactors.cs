using System.Runtime.InteropServices;

namespace TradingStrategies.Backtesting.Utility
{
    [StructLayout(LayoutKind.Auto)]
    public readonly struct LotsFactors
    {
        public double BuyFactor { get; }
        public double SellFactor { get; }

        public LotsFactors(double buyFactor, double sellFactor)
        {
            BuyFactor = buyFactor;
            SellFactor = sellFactor;
        }

        public static LotsFactors Neutral = new(1, 1);
    }
}
