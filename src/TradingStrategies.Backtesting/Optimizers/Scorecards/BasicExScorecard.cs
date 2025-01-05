using System.Windows.Forms;
using TradingStrategies.Backtesting.Utility;
using WealthLab;
using WealthLab.Indicators;
using WealthLab.Visualizers;

//такое впечатление, что идея более менее удалась
//но пока результаты грязные, тк наброшено чисто чтобы посмотреть
//надо вникать детальнее и допиливать расчет ошибки, особенно нормализацию и скоринг

//замечаю тендецию, что на 2022г. эквити колбасит сильнее, потом становится мягче, мб из-за политики, а мб из-за расчета
//еще одна тендеция, но слабее - в середине (2023г.) эквити выходит на хорошую экспоненту, а под конец (2024г.) снова на плато, это уже наверняка проблема расчета
//мб гасить в скоринге резкие скачки
//при ручном разборе можно еще смотреть на кол-во сделок, хорошие показатели при слишком низком числе пожалуй статистически вырожденны
//найти бы такой островок параметров на котором результаты не сильно варьируются (и удовлетворительны), думаю в статистическом плане это будет самая приятная почва, на которой можно будет со временнем подстраиваться

//надо бы оверрайднуть Equty+ визуализер и убрать starting capital, чтобы не мешал анализировать характер кривой
//LinearReg там еще какой-то кривой, нелинейный

//как будто нужна метрика по месячому (или другой период) доходу в процентах
//степень равномерности + среднее
//доработать визуализер ByPeriod - добавить фильтр по рэнжу дат, сейчас средний доход не меняется даже если выделить на графике меньший диапазон

//приглянувшиеся сеты
//14-0.3-260-0.6 - 554k
//14-0.3-290-0.7 - 711k
//10-0.4-200-1.3 - 647к
//19-1.0-260-0.6 - 951к - пример херовой эквити
//2-(0.3;0.4)-300-1.3 - 1238k - пример хорошего кэфа при старой проблеме - бурный рост вначале, медленный в конце
//4-0.3-300-1.3 - 1254k - пожалуй одно из лучших

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
            "Log Eq Linear Error", //линейная ошибка

            "Sharpe",
            "Log Eq Sharpe" //sharpe от логарифмической эквити
        ];

        private static readonly string[] columnTypes =
        [
            "N", "N", "N", "N", "N"
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
            var linearError = error.Sum();  //всегда 0

            resultRow.SubItems.Add(squaredError.ToString(NumbersFormat));
            resultRow.SubItems.Add(moduleError.ToString(NumbersFormat));
            resultRow.SubItems.Add(linearError.ToString(NumbersFormat));

            var sharpe = CalculateSharpeRatio(performance);
            resultRow.SubItems.Add(sharpe.ToString(NumbersFormat));

            var logSharpe = CalculateSharpeRatioOnLogEquity(performance);
            resultRow.SubItems.Add(logSharpe.ToString(NumbersFormat));
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

        private static double CalculateSharpeRatioOnLogEquity(SystemPerformance performance)
        {
            var equitySeries = performance.Results.EquityCurve;
            var startingCapital = equitySeries.Count > 0 ? equitySeries[0] : 0;
            var logEquitySeries = equitySeries
                .ToPoints()
                //.Select(x => x.Transform(v => v - startingCapital)) //нормализация (какие-то ниоч результаты с нормализацией)
                //.Select(static x => x.Transform(static v => Math.Max(1, Math.Abs(v)))) //костыли для ln(x)
                .Select(static x => x.Transform(MathHelper.NaturalLog)) //логарифм
                .Select(static x => x.Transform(static v => Math.Max(0.01, Math.Abs(v)))) //костыли для sharpe, избавляемся от 0
                .ToSeries();

            //подмена эквити на логарифмическую
            var results = CloneUtil<SystemResults>.ShallowClone(performance.Results);
            results.GetType().GetProperty(nameof(results.EquityCurve)).SetValue(results, logEquitySeries);

            performance = CloneUtil<SystemPerformance>.ShallowClone(performance);
            performance.GetType().GetProperty(nameof(performance.Results)).SetValue(performance, results);

            //масштаб для читаемости, тк на выходе маленькое значение
            const int scaleFactor = 1000;
            var sharpe = CalculateSharpeRatio(performance);
            return sharpe * scaleFactor;
        }

        //from WealthLab.Visualizers.PVReport
        private static double CalculateSharpeRatio(SystemPerformance performance)
        {
            var results = performance.Results;
            var timeSpan = results.EquityCurve.Date[results.EquityCurve.Count - 1] - results.EquityCurve.Date[0];

            double value8 = 0.0;
            if (timeSpan.Days > 31 && results.Positions.Count > 0)
            {
                DataSeries dataSeries = new DataSeries("Returns");
                double num21 = results.EquityCurve[0];
                DateTime dateTime_ = results.EquityCurve.Date[0];
                int k;
                double num22;
                double value9;
                for (k = 1; k < results.EquityCurve.Count - 1; k++)
                {
                    DateTime dateTime = results.EquityCurve.Date[k];
                    if (dateTime.Month != dateTime_.Month || dateTime.Year != dateTime_.Year)
                    {
                        num22 = results.EquityCurve[k - 1] - num21;
                        value9 = num22 * 100.0 / num21;
                        dataSeries.Add(value9, dateTime_);
                        num21 = results.EquityCurve[k - 1];
                        dateTime_ = dateTime;
                    }
                }

                k = results.EquityCurve.Count - 1;
                num22 = results.EquityCurve[k] - num21;
                value9 = num22 * 100.0 / num21;
                dataSeries.Add(value9, dateTime_);
                double num23 = SMA.Value(dataSeries.Count - 1, dataSeries, dataSeries.Count) * 12.0;
                double num24 = StdDev.Value(dataSeries.Count - 1, dataSeries, dataSeries.Count, StdDevCalculation.Population) * Math.Sqrt(12.0);
                value8 = (num23 - performance.CashReturnRate) / num24;
            }

            return value8;
        }
    }
}
