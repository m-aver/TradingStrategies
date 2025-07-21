using System.Buffers;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Utility;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Scorecards
{
    internal class CustomScorecard : StrategyScorecard
    {
        public const string DisplayName = "Custom Scorecard";

        //basic
        protected const string NetProfit = "Net Profit";
        protected const string TradesCount = "Trades";
        protected const string NsfTradesCount = "Trades NSF";
        protected const string WinRate = "Winning %";

        //LE - Logarithmic Equity
        //расхождение между логарифмической эквити и ее линейной регрессией
        //показывает насколько сильно эквити отличается от экспоненты
        //чем меньше ошибка, тем равномернее доходности
        protected const string LeSquaredError = "LE Squared Error";
        protected const string LeLinearModuleError = "LE Linear Module Error";

        //квадратичная ошибка, скорректированная средней доходностью
        //чем больше, тем равномернее рост эквити. чем больше в отрицательном диапазоне, тем равномернее убытки
        //кажется получилось неплохо
        protected const string LeFactor = "LE Factor";

        protected const string Sharpe = "Sharpe";

        //MR - Month Return
        //месячные доходности в процентах
        protected const string AvgMr = "Avg MR";
        protected const string MaxMr = "Max MR";
        protected const string MinMr = "Min MR";
        protected const string AvgMrDelta = "Avg MR Delta";
        protected const string MaxMrDelta = "Max MR Delta";

        //просадки
        protected const string MaxDrawdownPercent = "Max Drawdown %";
        protected const string LongestDrawdownInDays = "Longest Drawdown (days)";
        protected const string SumDrawdownDensity = "SDD";

        //CE - Closed Equity
        //эквити на момент закрытия позиций, выполняет роль фильтра, удаляются скачки котировок
        //скачки могут сильно завышать значения просадок
        protected const string CeMaxDrawdownPercent = "CE Max Drawdown %";
        protected const string CeLongestDrawdownInDays = "CE Longest Drawdown (days)";

        private static readonly string[] columnNames =
        [
            NetProfit,
            TradesCount,
            NsfTradesCount,
            WinRate,
            LeSquaredError,
            LeLinearModuleError,
            LeFactor,
            Sharpe,
            AvgMr,
            MaxMr,
            MinMr,
            AvgMrDelta,
            MaxMrDelta,
            MaxDrawdownPercent,
            LongestDrawdownInDays,
            SumDrawdownDensity,
            CeMaxDrawdownPercent,
            CeLongestDrawdownInDays,
        ];

        private static readonly string[] columnTypes = columnNames.Select(_ => "N").ToArray();

        private const string IntNumbersFormat = "N0";
        private const string RealNumbersFormat = "N2";

        public override string FriendlyName => DisplayName;

        public override IList<string> ColumnHeadersRawProfit => columnNames;
        public override IList<string> ColumnHeadersPortfolioSim => columnNames;
        public override IList<string> ColumnTypesRawProfit => columnTypes;
        public override IList<string> ColumnTypesPortfolioSim => columnTypes;

        private readonly IPeriodicalSeriesCalculator _periodicalSeriesCalculator;

        private readonly ArrayPool<DataSeriesPoint> pool = ArrayPool<DataSeriesPoint>.Shared;

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

            //basic
            var netProfit = results.NetProfit;
            var trades = results.Positions.Count;
            var tradesNsf = results.TradesNSF;
            var winRate = results.Positions.Count == 0 ? 0 :
                100.0 * results.Positions.Count(x => x.NetProfit > 0) / results.Positions.Count;

            var buffer = pool.Rent(equitySeries.Count);

            //month-returns
            var monthReturnSeries = CalculateMonthReturns(equitySeries).ToBuffer(buffer);
            var sharpe = CalculateSharpeRatio(monthReturnSeries);

            var maxReturn = double.MinValue;
            var minReturn = double.MaxValue;
            var monthReturnCount = 0;
            var monthReturnSum = 0d;
            foreach (var monthReturn in monthReturnSeries)
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
            var avgReturnDelta = monthReturnSeries.Average(x => Math.Abs(x - avgReturn));
            var maxReturnDelta = maxReturn - avgReturnDelta;

            //log-error
            var errorSeries = CalculateError(equitySeries);

            var squaredError = 0d;
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
            var leFactor = avgReturn / squaredError;

            //drawdowns
            var drawdownSeries = CalculateDrawdown(equitySeries.ToPoints()).ToBuffer(buffer);
            var longestDrawdown = IndicatorsCalculator.LongestDrawdown(drawdownSeries).Days;
            var drawdownDensity = IndicatorsCalculator.SumDrawdownDensity(drawdownSeries);
            var maxDrawdown = drawdownSeries.MaxOrNaN(x => x.Value);

            var closedEquity = CalculateClosedEquity(results);
            var closedDrawdownSeries = CalculateDrawdown(closedEquity).ToBuffer(buffer);
            var closedLongestDrawdown = IndicatorsCalculator.LongestDrawdown(closedDrawdownSeries).Days;
            var closedMaxDrawdown = closedDrawdownSeries.MaxOrNaN(x => x.Value);

            pool.Return(buffer);

            //populate ui
            resultRow.SubItems.Add(netProfit.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(trades.ToString(IntNumbersFormat));
            resultRow.SubItems.Add(tradesNsf.ToString(IntNumbersFormat));
            resultRow.SubItems.Add(winRate.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(squaredError.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(moduleError.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(leFactor.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(sharpe.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(avgReturn.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(maxReturn.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(minReturn.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(avgReturnDelta.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(maxReturnDelta.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(maxDrawdown.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(longestDrawdown.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(drawdownDensity.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(closedMaxDrawdown.ToString(RealNumbersFormat));
            resultRow.SubItems.Add(closedLongestDrawdown.ToString(RealNumbersFormat));
        }

        private static IEnumerable<DataSeriesPoint> CalculateError(DataSeries equitySeries)
        {
            return IndicatorsCalculator.LogError(equitySeries);
        }

        private IEnumerable<DataSeriesPoint> CalculateMonthReturns(DataSeries equitySeries)
        {
            return _periodicalSeriesCalculator.CalculatePercentDiff(equitySeries, PeriodInfo.Monthly);
        }

        private static double CalculateSharpeRatio(IEnumerable<DataSeriesPoint> monthReturnSeries)
        {
            return IndicatorsCalculator.SharpeRatio(monthReturnSeries);
        }

        private static IEnumerable<DataSeriesPoint> CalculateDrawdown(IEnumerable<DataSeriesPoint> equitySeries)
        {
            return IndicatorsCalculator.DrawdownPercentage(equitySeries);
        }

        private static IEnumerable<DataSeriesPoint> CalculateClosedEquity(SystemResults results)
        {
            return IndicatorsCalculator.CalculateClosedEquity(results);
        }

        private void PopulateUndefined(ListViewItem resultRow)
        {
            foreach (var _ in columnNames)
            {
                resultRow.SubItems.Add(UndefinedLabel);
            }
        }
        private static readonly string UndefinedLabel = double.NaN.ToString(RealNumbersFormat);
    }
}
