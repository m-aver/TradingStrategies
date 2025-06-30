using WealthLab;

//разбивает исходную серию на периоды
//и расчитывает процентную разницу значений серии на конец каждого периода

namespace TradingStrategies.Backtesting.Utility
{
    public interface IPeriodicalSeriesCalculator
    {
        DataSeries CalculatePercentDiff(DataSeries series, PeriodInfo periodInfo);
    }

    public enum PeriodCalcType
    {
        Simple, //прибавление начиная от начала исходной даты
        Aligned, //выравнивание на начало указанного типа периода
    }

    public static class PeriodicalSeriesCalculatorFactory
    {
        private static readonly IPeriodicalSeriesCalculator AlignedSingleton;

        static PeriodicalSeriesCalculatorFactory()
        {
            AlignedSingleton = Create(PeriodCalcType.Aligned);
        }

        public static IPeriodicalSeriesCalculator CreateAlignedSingleton() => AlignedSingleton;

        public static IPeriodicalSeriesCalculator Create(PeriodCalcType calcType)
        {
            var separator = PeriodSeparatorFactory.Create(calcType);
            return new PeriodicalSeriesCalculator(separator);
        }
    }

    internal class PeriodicalSeriesCalculator : IPeriodicalSeriesCalculator
    {
        private readonly IPeriodSeparator _periodSeparator;

        public PeriodicalSeriesCalculator(IPeriodSeparator periodSeparator)
        {
            _periodSeparator = periodSeparator;
        }

        public DataSeries CalculatePercentDiff(DataSeries series, PeriodInfo periodInfo)
        {
            var byPercentSeries = new DataSeries("percent-diff-by-period");

            var byPeriodPoints = ByPeriod(series, periodInfo);

            var previousPoint = series.ToPoints().FirstOrDefault();

            foreach (var point in byPeriodPoints)
            {
                var income = (point.Value - previousPoint.Value) * 100 / previousPoint.Value;

                byPercentSeries.AddPoint(previousPoint.WithValue(income));

                previousPoint = point;
            }

            return byPercentSeries;
        }

        public DataSeries SeparateByPeriod(DataSeries series, PeriodInfo periodInfo)
        {
            var separatedSeries = new DataSeries("separated-by-period");

            var byPeriodPoints = ByPeriod(series, periodInfo);

            separatedSeries.AddPoints(byPeriodPoints);

            return separatedSeries;
        }

        private IEnumerable<DataSeriesPoint> ByPeriod(DataSeries series, PeriodInfo periodInfo)
        {
            if (series.Count == 0)
            {
                throw new ArgumentException($"source series is empty, series: {series.Description}");
            }

            var points = series.ToPoints();

            var start = series.Date.First();
            var end = series.Date.Last();

            //костыли, расширение границы, чтобы правильно обработать последнюю точку
            end += TimeSpan.FromTicks(1);
            points = points.Append(DataSeriesPoint.Faraway);

            var range = new DateTimeRange(start, end - start);
            var periods = _periodSeparator.GetPeriods(range, periodInfo);

            var byPeriodPoints = ByPeriod(points, periods);
            return byPeriodPoints;
        }

        //возвращает последние неграничные точки каждого периода
        //если период сквозной (не имеет точек исходной серии), возвращает значение последней точки с датой окончания периода
        private static IEnumerable<DataSeriesPoint> ByPeriod(IEnumerable<DataSeriesPoint> points, IEnumerable<DateTimeRange> periods)
        {
            using var periodsEnumerator = periods.GetEnumerator();

            if (!periodsEnumerator.MoveNext())
            {
                throw new ArgumentException("periods are empty", nameof(periods));
            }

            var previousPoint = points.FirstOrDefault();

            foreach (var point in points.EnsureOrdered())
            {
                var inNewPeriod = periodsEnumerator.Current.IsWithin(point.Date) == false;

                if (inNewPeriod)
                {
                    yield return previousPoint; //последняя точка предыдущего периода

                    while (MovePeriod(point.Date))
                    {
                        yield return new DataSeriesPoint(previousPoint.Value, periodsEnumerator.Current.EndDateTime); //последняя точка сквозного периода
                    }
                }

                previousPoint = point;
            }

            bool MovePeriod(DateTime date)
            {
                while (periodsEnumerator.MoveNext())
                {
                    if (periodsEnumerator.Current.IsWithin(date))
                    {
                        return false;
                    }

                    return true;
                }

                return false;
            }
        }
    }
}
