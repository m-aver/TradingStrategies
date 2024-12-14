using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TradingStrategies.UnitTests
{
    internal record ParametersIterationResult(string ParamName, IList<decimal> ParamValues)
    {
        //for test output
        public override string ToString()
        {
            return $"(name: {ParamName}, values: {String.Join(',', ParamValues.Select(x => x.ToString(CultureInfo.InvariantCulture)))}";
        }
    };

    internal class ParametersIterationResultsComparer : IEqualityComparer<ParametersIterationResult>
    {
        public static readonly ParametersIterationResultsComparer Instance = new ParametersIterationResultsComparer();

        public bool Equals(ParametersIterationResult? first, ParametersIterationResult? second) =>
            (first != null && second != null) &&
            (first.ParamName == second.ParamName) &&
            first.ParamValues.SequenceEqual(second.ParamValues);

        public int GetHashCode([DisallowNull] ParametersIterationResult obj)
        {
            throw new NotImplementedException();
        }
    }
}
