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
            //LE - Logarithmic Equity
            //расхождение между логарифмической эквити и ее линейной регрессией
            //показывает насколько сильно эквити отличается от экспоненты
            "LE Squared Error", //квадратичная ошибка
            "LE Linear Module Error", //модуль линейной ошибки

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

            var equitySeries = performance.Results.EquityCurve;

            var errorSeries = CalculateError(equitySeries);
            var monthReturnSeries = CalculateMonthReturns(equitySeries);
            var sharpe = CalculateSharpeRatio(monthReturnSeries, performance.CashReturnRate);

            var avgReturn = monthReturnSeries.GetValues().Average();
            var maxReturn = monthReturnSeries.GetValues().Max();
            var minReturn = monthReturnSeries.GetValues().Min();
            var avgReturnDelta = monthReturnSeries.GetValues().Average(x => Math.Abs(x - avgReturn));
            var maxReturnDelta = maxReturn - avgReturnDelta;

            var squaredError = errorSeries.GetValues().Sum(MathHelper.Sqr);
            var moduleError = errorSeries.GetValues().Sum(Math.Abs);

            //populate ui
            resultRow.SubItems.Add(squaredError.ToString(NumbersFormat));
            resultRow.SubItems.Add(moduleError.ToString(NumbersFormat));
            resultRow.SubItems.Add(sharpe.ToString(NumbersFormat));
            resultRow.SubItems.Add(avgReturn.ToString(NumbersFormat));
            resultRow.SubItems.Add(maxReturn.ToString(NumbersFormat));
            resultRow.SubItems.Add(minReturn.ToString(NumbersFormat));
            resultRow.SubItems.Add(avgReturnDelta.ToString(NumbersFormat));
            resultRow.SubItems.Add(maxReturnDelta.ToString(NumbersFormat));
        }

        private static DataSeries CalculateError(DataSeries equitySeries)
        {
            var startingCapital = equitySeries.Count > 0 ? equitySeries[0] : 0;
            var normalizedEquity = equitySeries.ToPoints()
                .Select(x => x - startingCapital)
                .Select(static x => x.WithValue(Math.Max(1, Math.Abs(x)))); //костыли чтобы ln(x) не ругался
            
            var logEquity = normalizedEquity
                .Select(static x => x.WithValue(MathHelper.NaturalLog(x)))
                .ToArray();
            var linearRegComponents = MathHelper.LinearRegression(logEquity, out var linearReg);
            var error = logEquity.Zip(linearReg, static (eq, lr) => (eq - lr));

            //уменьшаем ошибку при повышении экспоненты (фильтрация 0 наклона, скоринг более успешных)
            //error = error.Select(x => x * (1 / linearRegComponents.slope)); 
            return error.ToSeries();
        }

        private DataSeries CalculateMonthReturns(DataSeries equitySeries)
        {
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

            //sma - похоже на среднюю годовую доходность, stdDev - это ошибка
            //cashReturnRate - конфигурируемая величина, видимо какой процент средств планируется выводить из стратегии, пока всегда 0
            //итого: sma отвечает за доходность и знак sharpe, stdDev за скоринг - чем больше скачет доходность, тем меньше sharpe
        }
    }
}
