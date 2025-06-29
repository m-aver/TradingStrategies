using System.Windows.Forms;
using TradingStrategies.Backtesting.Utility;
using WealthLab;
using WealthLab.Visualizers;

//идея:
//оптимизация на обычной эквити искажает статистику
//выбираются стратегии, которые имеют бурный рост вначале периода, а затем их эффективность падает
//так проще набрать высокий NetProfit и создать визуально красивую эквити
//такие стратегии статистически эффективны только на начальном периоде, но не в конце или в будущем
//чтобы стратегия на актуальном рынке была столь же эффективной как на тестировании, надо подбирать ее так, чтобы на всей истории был равномерный рост
//такая стратегия должна стремится к экспоненциальной эквити и равномерной доходности в процентах
//но равномерность утопична, пожалуй не должно быть выбросов доходности присущих только определенному периоду

namespace TradingStrategies.Backtesting.Optimizers.Scorecards
{
    internal class BasicExScorecard : BasicScorecard
    {
        public const string DisplayName = "Basic Extended Scorecard";

        //LE - Logarithmic Equity
        //расхождение между логарифмической эквити и ее линейной регрессией
        //показывает насколько сильно эквити отличается от экспоненты

        //квадратичная ошибка
        protected const string LeSquaredError = "LE Squared Error";
        //квадратичная ошибка скорректированная средней доходностью
        protected const string LeSquaredErrorCorrected = "LE Squared Error Corrected";
        //модуль линейной ошибки
        protected const string LeLinearModuleError = "LE Linear Module Error";

        protected const string Sharpe = "Sharpe";

        //MR - Month Return
        //месячная доходность в процентах
        protected const string AvgMr = "Avg MR";
        protected const string MaxMr = "Max MR";
        protected const string MinMr = "Min MR";
        protected const string AvgMrDelta = "Avg MR Delta";
        protected const string MaxMrDelta = "Max MR Delta";

        //просадки
        protected const string MaxDrawdownPercent = "Max Drawdown %";
        protected const string LongestDrawdownInDays = "Longest Drawdown (days)";
        protected const string SumDrawdownDensity = "SDD";

        private static readonly string[] columnNames =
        [
            LeSquaredError,
            LeSquaredErrorCorrected,
            LeLinearModuleError,
            Sharpe,
            AvgMr,
            MaxMr,
            MinMr,
            AvgMrDelta,
            MaxMrDelta,
            MaxDrawdownPercent,
            LongestDrawdownInDays,
            SumDrawdownDensity,
        ];

        private static readonly string[] columnTypes = columnNames.Select(_ => "N").ToArray();

        private const string NumbersFormat = "N2";

        public override string FriendlyName => DisplayName;

        public override IList<string> ColumnHeadersRawProfit => [.. base.ColumnHeadersRawProfit, .. columnNames];
        public override IList<string> ColumnHeadersPortfolioSim => [.. base.ColumnHeadersPortfolioSim, .. columnNames];
        public override IList<string> ColumnTypesRawProfit => [.. base.ColumnTypesRawProfit, .. columnTypes];
        public override IList<string> ColumnTypesPortfolioSim => [.. base.ColumnTypesPortfolioSim, .. columnTypes];

        private readonly IPeriodicalSeriesCalculator _periodicalSeriesCalculator;

        public BasicExScorecard() //создается через активатор
        {
            _periodicalSeriesCalculator = PeriodicalSeriesCalculatorFactory.CreateAlignedSingleton();
        }

        public override void PopulateScorecard(ListViewItem resultRow, SystemPerformance performance)
        {
            base.PopulateScorecard(resultRow, performance);

            var equitySeries = performance.Results.EquityCurve;

            if (equitySeries is null || equitySeries.Count == 0)
            {
                PopulateUndefined(resultRow);
                return;
            }

            var errorSeries = CalculateError(equitySeries);
            var monthReturnSeries = CalculateMonthReturns(equitySeries);
            var sharpe = CalculateSharpeRatio(monthReturnSeries, performance.CashReturnRate);

            var avgReturn = monthReturnSeries.GetValues().Average();
            var maxReturn = monthReturnSeries.GetValues().Max();
            var minReturn = monthReturnSeries.GetValues().Min();
            var avgReturnDelta = monthReturnSeries.GetValues().Average(x => Math.Abs(x - avgReturn));
            var maxReturnDelta = maxReturn - avgReturnDelta;

            var squaredError = Math.Sqrt(errorSeries.GetValues().Sum(MathHelper.Sqr) / errorSeries.Count);
            var squaredErrorCorrected = 100 * squaredError / avgReturn;
            var moduleError = errorSeries.GetValues().Sum(Math.Abs) / errorSeries.Count;

            var drawdownSeries = CalculateDrawdown(equitySeries);
            var longestDrawdown = IndicatorsCalculator.LongestDrawdown(drawdownSeries.ToPoints()).Days;
            var drawdownDensity = IndicatorsCalculator.SumDrawdownDensity(drawdownSeries.ToPoints());
            var maxDrawdown = drawdownSeries.ToPoints().Max(x => x.Value);

            //populate ui
            resultRow.SubItems.Add(squaredError.ToString(NumbersFormat));
            resultRow.SubItems.Add(squaredErrorCorrected.ToString(NumbersFormat));
            resultRow.SubItems.Add(moduleError.ToString(NumbersFormat));
            resultRow.SubItems.Add(sharpe.ToString(NumbersFormat));
            resultRow.SubItems.Add(avgReturn.ToString(NumbersFormat));
            resultRow.SubItems.Add(maxReturn.ToString(NumbersFormat));
            resultRow.SubItems.Add(minReturn.ToString(NumbersFormat));
            resultRow.SubItems.Add(avgReturnDelta.ToString(NumbersFormat));
            resultRow.SubItems.Add(maxReturnDelta.ToString(NumbersFormat));
            resultRow.SubItems.Add(maxDrawdown.ToString(NumbersFormat));
            resultRow.SubItems.Add(longestDrawdown.ToString(NumbersFormat));
            resultRow.SubItems.Add(drawdownDensity.ToString(NumbersFormat));
        }

        private static DataSeries CalculateError(DataSeries equitySeries)
        {
            return IndicatorsCalculator.LogError(equitySeries.ToPoints()).ToSeries("error-series");
        }

        private DataSeries CalculateMonthReturns(DataSeries equitySeries)
        {
            return _periodicalSeriesCalculator.CalculatePercentDiff(equitySeries, PeriodInfo.Monthly);
        }

        private static double CalculateSharpeRatio(DataSeries monthReturnSeries, double cashReturnRate)
        {
            return IndicatorsCalculator.SharpeRatio(monthReturnSeries, cashReturnRate);
        }

        private static DataSeries CalculateDrawdown(DataSeries equitySeries)
        {
            return IndicatorsCalculator.DrawdownPercentage(equitySeries.ToPoints()).ToSeries("drawdown");
        }

        private void PopulateUndefined(ListViewItem resultRow)
        {
            foreach (var _ in columnNames)
            {
                resultRow.SubItems.Add(UndefinedLabel);
            }
        }
        private static readonly string UndefinedLabel = double.NaN.ToString(NumbersFormat);
    }
}
