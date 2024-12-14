using TradingStrategies.Backtesting.Optimizers;
using TradingStrategies.Backtesting.Optimizers.Utility;
using WealthLab;

namespace TradingStrategies.UnitTests
{
    public partial class StrategyParametersIteratorTests
    {
        [Theory]
        [MemberData(nameof(GetParameters), parameters: 1)]
        [MemberData(nameof(GetParameters), parameters: 5)]
        public void Success_IterateEach(StrategyParameter[] parameters)
        {
            //arrange
            var results = parameters.ToDictionary(x => x.Name, x => new ParametersIterationResult(x.Name, []));

            var iterator = new StrategyParametersIterator(parameters);

            //act
            //while (iterator.MoveNext())
            do
            {
                var current = iterator.CurrentParameters;

                foreach (var parameter in current)
                {
                    results[parameter.Name].ParamValues.Add((decimal)parameter.Value);
                }
            }
            while (iterator.MoveNext());

            //assert
            var expetedResults = parameters
                .Select(param =>
                {
                    //TODO: мб вынести в обертку при получении данных
                    var values = Enumerable
                        .Range(0, 1 + (int)Math.Floor(((decimal)param.Stop - (decimal)param.Start) / (decimal)param.Step))
                        .Select(i => (decimal)param.Start + ((decimal)param.Step * i))
                        .Order()
                        .ToList();
                    return new ParametersIterationResult(param.Name, values);
                })
                .Combine()
                .OrderBy(x => x.ParamName);

            var resultValues = results.Values
                .Select(x => new ParametersIterationResult(x.ParamName, x.ParamValues.Order().ToList()))
                .OrderBy(x => x.ParamName);

            Assert.Equal(expetedResults, resultValues, ParametersIterationResultsComparer.Instance);
        }

        [Theory]
        [MemberData(nameof(GetParameters_ManualResult))]
        public void Success_IterateEach_ManualResult((StrategyParameter parameter, double[] results)[] data)
        {
            //arrange
            var results = data.ToDictionary(x => x.parameter.Name, x => new ParametersIterationResult(x.parameter.Name, []));

            var iterator = new StrategyParametersIterator(data.Select(x => x.parameter).ToArray());

            //act
            //while (iterator.MoveNext())
            do
            {
                var current = iterator.CurrentParameters;

                foreach (var parameter in current)
                {
                    results[parameter.Name].ParamValues.Add((decimal)parameter.Value);
                }
            }
            while (iterator.MoveNext());

            //assert
            var expetedResults = data
                .Select(x => new ParametersIterationResult(x.parameter.Name, x.results.Select(Convert.ToDecimal).Order().ToList()))
                .OrderBy(x => x.ParamName);
            var resultValues = results.Values
                .Select(x => new ParametersIterationResult(x.ParamName, x.ParamValues.Order().ToList()))
                .OrderBy(x => x.ParamName);

            Assert.Equal(expetedResults, resultValues, ParametersIterationResultsComparer.Instance);
        }
    }
}
