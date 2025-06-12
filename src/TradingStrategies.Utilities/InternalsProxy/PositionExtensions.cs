using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class PositionExtensions
{
    //public static void SetShares(this Position position, double shares) => position.Shares = shares;

    //public static double GetSplitFactor(this Position position) => position.SplitFactor;
    //public static void SetSplitFactor(this Position position, double SplitFactor) => position.SplitFactor = SplitFactor;

    extension(Position position)
    {
        public double SharesProxy
        {
            get => position.Shares;
            set => position.Shares = value;
        }

        public double SplitFactor
        {
            get => position.SplitFactor;
            set => position.SplitFactor = value;
        }

        public int CombinedPriority
        {
            get => position.CombinedPriority;
            set => position.CombinedPriority = value;
        }

        public void CalculateMfeMae() => position.method_2();
    }
}
