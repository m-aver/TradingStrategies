using System.Windows.Forms;
using TradingStrategies.Backtesting.Utility;
using WealthLab;

//идея:
//оптимизация на обычной эквити искажает статистику
//выбираются стратегии, которые имеют бурный рост вначале периода, а затем их эффективность падает
//так проще набрать высокий NetProfit и создать визуально красивую эквити
//такие стратегии статистически эффективны только на начальном периоде, но не в конце или в будущем
//чтобы стратегия на актуальном рынке была столь же эффективной как на тестировании, надо подбирать ее так, чтобы на всей истории был равномерный рост
//такая стратегия должна стремится к экспоненциальной эквити и равномерной доходности в процентах
//но равномерность утопична, пожалуй не должно быть выбросов доходности присущих только определенному периоду

//TODO:
//на тестах заметил, что очень сильно работает GC с этим скорекардом
//с прошлым такой херни не было, надо разобраться
//так же работа ЦП более разреженная
//solved: 
//дело в материализации drawdownSeries.ToArray()

namespace TradingStrategies.Backtesting.Optimizers.Scorecards
{
    internal class CustomScorecard : StrategyScorecard
    {
        public const string DisplayName = "Custom Scorecard";

        protected const string NetProfit = "Net Profit";
        protected const string TradesCount = "Trades";
        protected const string NsfTradesCount = "Trades NSF";
        protected const string WinRate = "Winning %";

        //LE - Logarithmic Equity
        //расхождение между логарифмической эквити и ее линейной регрессией
        //показывает насколько сильно эквити отличается от экспоненты
        //чем меньше ошибка, тем равномернее доходности

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
            NetProfit,
            TradesCount,
            NsfTradesCount,
            WinRate,
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

        public override IList<string> ColumnHeadersRawProfit => columnNames;
        public override IList<string> ColumnHeadersPortfolioSim => columnNames;
        public override IList<string> ColumnTypesRawProfit => columnTypes;
        public override IList<string> ColumnTypesPortfolioSim => columnTypes;

        private readonly IPeriodicalSeriesCalculator _periodicalSeriesCalculator;

        public CustomScorecard() //создается через активатор
        {
            _periodicalSeriesCalculator = PeriodicalSeriesCalculatorFactory.CreateAlignedSingleton();
        }

        public override void PopulateScorecard(ListViewItem resultRow, SystemPerformance performance)
        {
            var results = performance.Results;
            var equitySeries = results.EquityCurve;

            if (equitySeries is null || equitySeries.Count == 0)
            {
                PopulateUndefined(resultRow);
                return;
            }

            //legacy
            var netProfit = results.NetProfit;
            var trades = results.Positions.Count;
            var tradesNsf = results.TradesNSF;
            var winRate = results.Positions.Count == 0 ? 0 :
                100.0 * results.Positions.Count(x => x.NetProfit > 0) / results.Positions.Count;

            //month-returns
            var monthReturnSeries = CalculateMonthReturns(equitySeries);
            var sharpe = CalculateSharpeRatio(monthReturnSeries, performance.CashReturnRate);

            var maxReturn = double.MinValue;
            var minReturn = double.MaxValue;
            var monthReturnCount = 0;
            var monthReturnSum = 0d;
            foreach (var monthReturn in monthReturnSeries.GetValues())
            {
                if (minReturn > monthReturn)
                {
                    minReturn = monthReturn;
                }
                if (maxReturn < monthReturn)
                {
                    maxReturn = monthReturn;
                }
                monthReturnCount++;
                monthReturnSum += monthReturn;
            }
            var avgReturn = monthReturnSum / monthReturnCount;
            var avgReturnDelta = monthReturnSeries.GetValues().Average(x => Math.Abs(x - avgReturn));
            var maxReturnDelta = maxReturn - avgReturnDelta;

            //log-error
            var errorSeries = CalculateError(equitySeries.ToPoints());

            var squaredError = 0d;
            var squaredErrorCorrected = 0d;
            var moduleError = 0d;
            var errorCount = 0;
            foreach (var error in errorSeries)
            {
                squaredError += MathHelper.Sqr(error);
                moduleError += Math.Abs(error);
                errorCount++;
            }
            squaredError = Math.Sqrt(squaredError / errorCount);
            moduleError = moduleError / errorCount;
            squaredErrorCorrected = 100 * squaredError / avgReturn;

            //drawdowns
            var drawdownSeries = CalculateDrawdown(equitySeries.ToPoints());
            var longestDrawdown = IndicatorsCalculator.LongestDrawdown(drawdownSeries).Days;
            var drawdownDensity = IndicatorsCalculator.SumDrawdownDensity(drawdownSeries);
            var maxDrawdown = drawdownSeries.Max(x => x.Value);

            //populate ui
            resultRow.SubItems.Add(netProfit.ToString(NumbersFormat));
            resultRow.SubItems.Add(trades.ToString(NumbersFormat)); //TODO: format
            resultRow.SubItems.Add(tradesNsf.ToString(NumbersFormat));
            resultRow.SubItems.Add(winRate.ToString(NumbersFormat));
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

        private static IEnumerable<DataSeriesPoint> CalculateError(IEnumerable<DataSeriesPoint> equitySeries)
        {
            return IndicatorsCalculator.LogError(equitySeries);
        }

        private DataSeries CalculateMonthReturns(DataSeries equitySeries)
        {
            return _periodicalSeriesCalculator.CalculatePercentDiff(equitySeries, PeriodInfo.Monthly);
        }

        private static double CalculateSharpeRatio(DataSeries monthReturnSeries, double cashReturnRate)
        {
            return IndicatorsCalculator.SharpeRatio(monthReturnSeries, cashReturnRate);
        }

        private static IEnumerable<DataSeriesPoint> CalculateDrawdown(IEnumerable<DataSeriesPoint> equitySeries)
        {
            return IndicatorsCalculator.DrawdownPercentage(equitySeries);
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
