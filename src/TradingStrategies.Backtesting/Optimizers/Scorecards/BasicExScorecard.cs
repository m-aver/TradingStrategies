using System.Windows.Forms;
using TradingStrategies.Backtesting.Utility;
using WealthLab;
using WealthLab.Indicators;
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

        private static readonly string[] columnNames =
        [
            //расхождение между логарифмической эквити и ее линейной регрессией
            //показывает насколько сильно эквити отличается от экспоненты

            "Log Eq Squared Error", //квадратичная ошибка
            "Log Eq Linear Module Error", //модуль линейной ошибки

            "Sharpe",

            //MR - Month Return
            //месячная доходность в процентах
            "Avg MR",
            "Max MR",
            "Min MR",
            "Avg MR Delta", //усредненное отклонение от среднего
            "Max MR Delta", //Max MR - Avg MR Delta //отклонение сильнейшего выброса
        ];

        private static readonly string[] columnTypes = columnNames.Select(static _ => "N").ToArray();

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

            var equity = performance.Results.EquityCurve.ToPoints().Select(x => x.Value).ToArray();
            var error = CalculateError(equity);

            var squaredError = error.Sum(MathHelper.Sqr);
            var moduleError = error.Sum(Math.Abs);

            resultRow.SubItems.Add(squaredError.ToString(NumbersFormat));
            resultRow.SubItems.Add(moduleError.ToString(NumbersFormat));

            var monthReturnSeries = CalculateMonthReturns(performance);

            var sharpe = CalculateSharpeRatio(monthReturnSeries, performance.CashReturnRate);
            resultRow.SubItems.Add(sharpe.ToString(NumbersFormat));

            var avgReturn = monthReturnSeries.ToPoints().Average(static p => p.Value);
            resultRow.SubItems.Add(avgReturn.ToString(NumbersFormat));

            var maxReturn = monthReturnSeries.ToPoints().Max(static p => p.Value);
            resultRow.SubItems.Add(maxReturn.ToString(NumbersFormat));

            var minReturn = monthReturnSeries.ToPoints().Min(static p => p.Value);
            resultRow.SubItems.Add(minReturn.ToString(NumbersFormat));

            var avgReturnDelta = monthReturnSeries.ToPoints().Average(p => Math.Abs(p.Value - avgReturn));
            resultRow.SubItems.Add(avgReturnDelta.ToString(NumbersFormat));

            var maxReturnDelta = maxReturn - avgReturnDelta;
            resultRow.SubItems.Add(maxReturnDelta.ToString(NumbersFormat));
        }

        private static double[] CalculateError(double[] equity)
        {
            var startingCapital = equity.Length > 0 ? equity[0] : 0;
            var normalizedEquity = equity.Select(x => x - startingCapital);
            normalizedEquity = normalizedEquity.Select(static x => Math.Max(1, Math.Abs(x))); //костыли чтобы ln(x) не ругался

            var logEquity = MathHelper.NaturalLog(normalizedEquity).ToArray();
            var indexes = Enumerable.Range(0, logEquity.Length).Select(Convert.ToDouble).ToArray();
            var linearRegComponents = MathHelper.LinearRegression(indexes, logEquity);
            var linearReg = indexes.Select(linearRegComponents.CalculatePrediction);
            var error = logEquity.Zip(linearReg, static (eq, lr) => (eq - lr));

            //уменьшаем ошибку при повышении экспоненты (фильтрация 0 наклона, скоринг более успешных)
            var scoredError = error.Select(x => x * (1 / linearRegComponents.slope)); 
            return scoredError.ToArray();
        }

        private DataSeries CalculateMonthReturns(SystemPerformance performance)
        {
            var equitySeries = performance.Results.EquityCurve;
            var monthReturnSeries = _periodicalSeriesCalculator.CalculatePercentDiff(equitySeries, PeriodInfo.Monthly);
            return monthReturnSeries;
        }

        private static double CalculateSharpeRatio(DataSeries monthReturnSeries, double cashReturnRate)
        {
            var months = monthReturnSeries.Count;
            var sma = SMA.Value(months - 1, monthReturnSeries, months) * 12.0;
            var stdDev = StdDev.Value(months - 1, monthReturnSeries, months, StdDevCalculation.Population) * Math.Sqrt(12.0);
            var sharpe = (sma - cashReturnRate) / stdDev;

            return sharpe;

            //sma похоже на среднюю годовую доходность, stdDev - это ошибка
            //cashReturnRate - конфигурируемая величина, видимо какой процент средств планируется выводить из стратегии, пока всегда 0
            //итого: sma отвечает за доходность и знак sharpe, stdDev за скоринг - чем больше скачет доходность, тем меньше sharpe
        }
    }
}
