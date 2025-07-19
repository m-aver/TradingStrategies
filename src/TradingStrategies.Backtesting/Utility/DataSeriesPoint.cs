using System.Collections;
using WealthLab;

namespace TradingStrategies.Backtesting.Utility
{
    public readonly struct DataSeriesPoint
    {
        public static DataSeriesPoint Faraway => new(default, DateTime.MaxValue);

        public DataSeriesPoint(double value, DateTime date) : this()
        {
            Value = value;
            Date = date;
        }

        public double Value { get; }
        public DateTime Date { get; }

        public DataSeriesPoint Transform(Func<double, double> transformer) => new(transformer(this.Value), this.Date);
        public DataSeriesPoint WithValue(double newValue) => new(newValue, this.Date);

        public static DataSeriesPoint operator *(DataSeriesPoint point, double value) => point.WithValue(point.Value * value);
        public static DataSeriesPoint operator -(DataSeriesPoint point, double value) => point.WithValue(point.Value - value);
        public static DataSeriesPoint operator +(DataSeriesPoint point, double value) => point.WithValue(point.Value + value);

        public static implicit operator double(DataSeriesPoint point) => point.Value;

        //for tests, debug visualizing
        public override string ToString()
        {
            return $"({Value}, {Date})";
        }
    }

    public static class DataSeriesPointExtensions
    {
        public static IEnumerator<DataSeriesPoint> GetEnumerator(this DataSeries series) => new DataSeriesPointEnumerator(series);

        public static IEnumerable<DataSeriesPoint> ToPoints(this DataSeries series)
        {
            var enumerator = new DataSeriesPointEnumerator(series);

            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }

        //использовать с острожностью, кроме дат и значений остальные внутренние параметры исходной DateSeries не проставляются
        public static DataSeries ToSeries(this IEnumerable<DataSeriesPoint> points, string description = "from points")
        {
            var series = new DataSeries(description);

            foreach (var point in points)
            {
                series.Add(point.Value, point.Date);
            }

            return series;
        }

        public static IEnumerable<DataSeriesPoint> EnsureOrdered(this IEnumerable<DataSeriesPoint> points)
        {
            var previousPoint = new DataSeriesPoint(0, DateTime.MinValue);

            foreach (var point in points)
            {
                if (point.Date > previousPoint.Date)
                {
                    yield return point;
                }
                else
                {
                    throw new ArgumentException("points are not ordered");
                }

                previousPoint = point;
            }
        }
    }

    public static class DataSeriesExtensions
    {
        public static void AddPoint(this DataSeries series, DataSeriesPoint point)
        {
            series.Add(point.Value, point.Date);
        }

        public static void AddPoints(this DataSeries series, IEnumerable<DataSeriesPoint> points)
        {
            foreach (var point in points)
            {
                series.AddPoint(point);
            }
        }

        public static IEnumerable<double> GetValues(this DataSeries series)
        {
            return series.ToPoints().Select(x => x.Value);
        }
    }

    //не реагирует на изменения исходной DataSeries
    public struct DataSeriesPointEnumerator : IEnumerator<DataSeriesPoint>
    {
        private readonly DataSeries series;
        private readonly int count;
        private int index;

        public DataSeriesPointEnumerator(DataSeries series)
        {
            if (series.Count != series.Date.Count)
            {
                throw new ArgumentException($"source series has inconsistent number of date and value points, series: {series.Description}");
            }

            this.series = series;
            count = series.Count;
            index = -1;
        }

        public DataSeriesPoint Current => new(series[index], series.Date[index]);
        object IEnumerator.Current => Current;

        public bool MoveNext() => ++index < count;
        public void Reset() => index = -1;
        public void Dispose() { }
    }
}
