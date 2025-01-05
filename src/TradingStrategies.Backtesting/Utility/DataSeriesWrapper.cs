using System.Collections;
using WealthLab;

namespace TradingStrategies.Backtesting.Utility
{
    public readonly struct DataSeriesPoint
    {
        public DataSeriesPoint(double value, DateTime date) : this()
        {
            Value = value;
            Date = date;
        }

        public double Value { get; }
        public DateTime Date { get; }

        public DataSeriesPoint Transform(Func<double, double> transformer) => new(transformer(this.Value), this.Date);
    }

    public static class DataSeriesPointExtensions
    {
        public static IEnumerator<DataSeriesPoint> GetEnumerator(this DataSeries series) => new DataSeriesPointEnumerator(series);

        public static IEnumerable<DataSeriesPoint> ToPoints(this DataSeries series)
        {
            using var enumerator = series.GetEnumerator();

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
    }

    //не реагирует на изменения исходной DataSeries
    public struct DataSeriesPointEnumerator : IEnumerator<DataSeriesPoint>
    {
        private readonly DataSeries _series;
        private readonly IEnumerator<int> _indexEnumerator;

        public DataSeriesPointEnumerator(DataSeries series)
        {
            _series = series;
            _indexEnumerator = Enumerable.Range(0, series.Count).GetEnumerator();
        }

        public DataSeriesPoint Current
        {
            get
            {
                var index = _indexEnumerator.Current;
                return new DataSeriesPoint(_series[index], _series.Date[index]);
            }
        }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
            _indexEnumerator.Dispose();
        }

        public bool MoveNext()
        {
            return _indexEnumerator.MoveNext();
        }

        public void Reset()
        {
            _indexEnumerator.Reset();
        }
    }
}
