using System.Diagnostics.CodeAnalysis;

namespace TradingStrategies.UnitTests
{
    internal record IterationResult(DateTime Date, IList<int> Iterations)
    {
        public IterationResult(DateTime date) : this(date, [])
        {
        }
    };

    internal class IterationResultsComparer : IEqualityComparer<IterationResult>
    {
        public static readonly IterationResultsComparer Instance = new IterationResultsComparer();

        public bool Equals(IterationResult? first, IterationResult? second) =>
            (first != null && second != null) &&
            (first.Date == second.Date) &&
            first.Iterations.SequenceEqual(second.Iterations);

        public int GetHashCode([DisallowNull] IterationResult obj)
        {
            throw new NotImplementedException();
        }
    }
}