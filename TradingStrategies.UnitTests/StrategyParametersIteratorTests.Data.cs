using System.Linq;
using WealthLab;

namespace TradingStrategies.UnitTests
{
    public partial class StrategyParametersIteratorTests
    {
        public static IEnumerable<object[]> GetParameters(int count)
        {
            var integerDuplicating = Enumerable
                .Range(1, count)
                .Select(x => new StrategyParameter(
                    name: x.ToString(),
                    value: 0,
                    start: 0,
                    stop: 10,
                    step: 1))
                .ToArray();

            yield return [integerDuplicating];

            var integer = Enumerable
                .Range(1, count)
                .Select(x => new StrategyParameter(
                    name: x.ToString(),
                    value: 0 + x,
                    start: 0 + x,
                    stop: 10 * x,
                    step: 1 * x))
                .ToArray();

            yield return [integer];

            var real = Enumerable
                .Range(1, count)
                .Select(x => new StrategyParameter(
                    name: x.ToString(),
                    value: 0.1 + x,
                    start: 0.1 + x,
                    stop: 10.1 * x,
                    step: 1.1 * x))
                .ToArray();

            yield return [real];

            var realHighPrecision = Enumerable
                .Range(1, count)
                .Select(x => new StrategyParameter(
                    name: x.ToString(),
                    value: 0.110289 + x,
                    start: 0.110289 + x,
                    stop: 10.101293 * x,
                    step: 1.161273 * x))
                .ToArray();

            yield return [realHighPrecision];

            var realFromPositiveToNegative = Enumerable
                .Range(1, count)
                .Select(x => new StrategyParameter(
                    name: x.ToString(),
                    value: 10.1 * x,
                    start: 10.1 * x,
                    stop: -10.1 * x,
                    step: -2.1 * x))
                .ToArray();

            yield return [realFromPositiveToNegative];
        }

        public static IEnumerable<object[]> GetParameters_ManualResult()
        {
            var allEnabled = new (StrategyParameter parameter, double[] result)[]
            {
                //int
                (parameter: new StrategyParameter(
                    name: "1",
                    value: 0,
                    start: 0,
                    stop: 10,
                    step: 1),
                result: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]),

                //double
                (parameter: new StrategyParameter(
                    name: "2",
                    value: 0.1,
                    start: 0.1,
                    stop: 10.5,
                    step: 1.1),
                result: [0.1, 1.2, 2.3, 3.4, 4.5, 5.6, 6.7, 7.8, 8.9, 10]),

                //negative int
                (parameter: new StrategyParameter(
                    name: "3",
                    value: 0,
                    start: 0,
                    stop: -10,
                    step: -1),
                result: [0, -1, -2, -3, -4, -5, -6, -7, -8, -9, -10]),

                //mixed with negative double
                (parameter: new StrategyParameter(
                    name: "4",
                    value: 10.5,
                    start: 10.5,
                    stop: -10.5,
                    step: -2.5),
                result: [10.5, 8, 5.5, 3, 0.5, -2, -4.5, -7, -9.5]),
            };

            yield return [allEnabled.Combine()];

            var withDisabled = new (StrategyParameter parameter, double[] result)[]
            {
                //int
                (parameter: new StrategyParameter(
                    name: "1",
                    value: 0,
                    start: 0,
                    stop: 10,
                    step: 1),
                result: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]),

                //double
                (parameter: new StrategyParameter(
                    name: "2",
                    value: 0.1,
                    start: 0.1,
                    stop: 10.5,
                    step: 1.1),
                result: [0.1, 1.2, 2.3, 3.4, 4.5, 5.6, 6.7, 7.8, 8.9, 10]),

                //negative int
                //disabled
                (parameter: new StrategyParameter(
                    name: "3",
                    value: -5,
                    start: 0,
                    stop: -10,
                    step: -1)
                {
                    IsEnabled = false,
                },
                result: [-5]),
            };

            yield return [withDisabled.Combine()];
        }
    }

    internal static class StrategyParametersIteratorTestsHelpers
    {
        public static (StrategyParameter parameter, double[] result)[] Combine(
            this (StrategyParameter parameter, double[] result)[] results)
        {
            return results.CombineInner().ToArray();
        }

        //duplicate parameter result values to emulate their combination with other parameters values
        public static IEnumerable<(StrategyParameter parameter, double[] result)> CombineInner(
            this IEnumerable<(StrategyParameter parameter, double[] result)> results)
        {
            var multiplier = results.Select(r => r.result.Length).Aggregate((x, y) => x * y);

            return results
                .Select(x => (x.parameter, x.result
                    .SelectMany(v =>
                        Enumerable.Range(0, multiplier / x.result.Length).Select(_ => v))
                    .ToArray()));
        }

        public static IEnumerable<ParametersIterationResult> Combine(
            this IEnumerable<ParametersIterationResult> results)
        {
            return results
                .Select(x => (new StrategyParameter() { Name = x.ParamName }, x.ParamValues.Select(Convert.ToDouble).ToArray()))
                .CombineInner()
                .Select(x => new ParametersIterationResult(x.parameter.Name, x.result.Select(Convert.ToDecimal).ToList()));
        }
    }
}
