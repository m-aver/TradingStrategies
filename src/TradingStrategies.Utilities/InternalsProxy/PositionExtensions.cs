using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class PositionExtensions
{
    extension(Position position)
    {
        public double SharesProxy { get => position.Shares; set => position.Shares = value; }
        public double SplitFactor { get => position.SplitFactor; set => position.SplitFactor = value; }
        public int CombinedPriority { get => position.CombinedPriority; set => position.CombinedPriority = value; }

        public void CalculateMfeMae() => position.method_2();
    }
}
