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
//такая стратегия должна стремится к экспоненциальной эквити и равномерному росту доходности в процентах

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
        ];

        private static readonly string[] columnTypes =
        [
            "N", "N", "N"
        ];

        private const string NumbersFormat = "N2";

        public override string FriendlyName => DisplayName;

        public override IList<string> ColumnHeadersRawProfit => [.. base.ColumnHeadersRawProfit, .. columnNames];
        public override IList<string> ColumnHeadersPortfolioSim => [.. base.ColumnHeadersPortfolioSim, .. columnNames];
        public override IList<string> ColumnTypesRawProfit => [.. base.ColumnTypesRawProfit, .. columnTypes];
        public override IList<string> ColumnTypesPortfolioSim => [.. base.ColumnTypesPortfolioSim, .. columnTypes];

        public override void PopulateScorecard(ListViewItem resultRow, SystemPerformance performance)
        {
            base.PopulateScorecard(resultRow, performance);

            var equity = performance.Results.EquityCurve.ToPoints().Select(x => x.Value).ToArray();
            var error = CalculateError(equity);

            var squaredError = error.Sum(MathHelper.Sqr);
            var moduleError = error.Sum(Math.Abs);

            resultRow.SubItems.Add(squaredError.ToString(NumbersFormat));
            resultRow.SubItems.Add(moduleError.ToString(NumbersFormat));

            var sharpe = CalculateSharpeRatio(performance);
            resultRow.SubItems.Add(sharpe.ToString(NumbersFormat));
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

        //from WealthLab.Visualizers.PVReport
        private static double CalculateSharpeRatio(SystemPerformance performance)
        {
            var results = performance.Results;
            var equitySeries = results.EquityCurve;
            var equitySpan = equitySeries.Date.Last() - equitySeries.Date.First();

            var sharpe = 0.0;
            if (equitySpan.Days > 31 && results.Positions.Count > 0)
            {
                var returnsSeries = new DataSeries("Returns"); //прибыль/убыток по месяцам в процентах
                var previousMonthEquity = equitySeries[0];
                var previousMonthDate = equitySeries.Date[0];
                double monthlyEquityIncome;
                double monthlyEquityIncomePercent;

                int k;
                for (k = 1; k < equitySeries.Count - 1; k++)
                {
                    var currentDate = equitySeries.Date[k];
                    var newMonth = currentDate.Month != previousMonthDate.Month || currentDate.Year != previousMonthDate.Year;

                    if (newMonth)
                    {
                        monthlyEquityIncome = equitySeries[k - 1] - previousMonthEquity;
                        monthlyEquityIncomePercent = monthlyEquityIncome * 100.0 / previousMonthEquity;
                        returnsSeries.Add(monthlyEquityIncomePercent, previousMonthDate);
                        previousMonthEquity = equitySeries[k - 1];
                        previousMonthDate = currentDate;
                    }
                }

                k = equitySeries.Count - 1;
                monthlyEquityIncome = equitySeries[k] - previousMonthEquity;
                monthlyEquityIncomePercent = monthlyEquityIncome * 100.0 / previousMonthEquity;
                returnsSeries.Add(monthlyEquityIncomePercent, previousMonthDate);

                var sma = SMA.Value(returnsSeries.Count - 1, returnsSeries, returnsSeries.Count) * 12.0; //похоже на среднюю годовую доходность
                var stdDev = StdDev.Value(returnsSeries.Count - 1, returnsSeries, returnsSeries.Count, StdDevCalculation.Population) * Math.Sqrt(12.0);
                sharpe = (sma - performance.CashReturnRate) / stdDev;
            }

            return sharpe;
        }
    }
}
