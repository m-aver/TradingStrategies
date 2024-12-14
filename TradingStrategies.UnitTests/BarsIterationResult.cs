using System.Diagnostics.CodeAnalysis;

namespace TradingStrategies.UnitTests
{
    internal record BarsIterationResult(DateTime Date, IList<int> Iterations)
    {
        public BarsIterationResult(DateTime date) : this(date, [])
        {
        }
    };

    internal class BarsIterationResultsComparer : IEqualityComparer<BarsIterationResult>
    {
        public static readonly BarsIterationResultsComparer Instance = new BarsIterationResultsComparer();

        public bool Equals(BarsIterationResult? first, BarsIterationResult? second) =>
            (first != null && second != null) &&
            (first.Date == second.Date) &&
            first.Iterations.SequenceEqual(second.Iterations);

        public int GetHashCode([DisallowNull] BarsIterationResult obj)
        {
            throw new NotImplementedException();
        }
    }
}