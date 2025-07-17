using System.Buffers;
using System.Collections;
using WealthLab;
using WealthLab.Indicators;

namespace TradingStrategies.Backtesting.Utility;

public static class IndicatorsCalculator
{
    //wrapper for points
    public static DataSeriesPoint[] LinearRegression(DataSeriesPoint[] points)
    {
        double[] values = new double[points.Length];
        double[] ticks = new double[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i];
            values[i] = point.Value;
            ticks[i] = point.Date.Ticks;
        }

        var components = MathHelper.LinearRegression(ticks, values);

        var regression = new DataSeriesPoint[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i];
            var regValue = components.CalculatePrediction(point.Date.Ticks);
            regression[i] = point.WithValue(regValue);
        }

        return regression;
    }

    public static IEnumerable<DataSeriesPoint> LinearRegressionThroughStartPoint(IEnumerable<DataSeriesPoint> points)
    {
        var first = points.First();

        var dataset = points.Select(point => ((double)(point.Date.Ticks - first.Date.Ticks), point.Value - first.Value));

        var slope = MathHelper.LinearRegressionThroughOrigin(dataset);

        var regression = points.Select(point =>
        {
            var regValue = (point.Date.Ticks - first.Date.Ticks) * slope;
            return point.WithValue(regValue + first.Value);
        });

        return regression;
    }

    public static IEnumerable<DataSeriesPoint> LogError(IEnumerable<DataSeriesPoint> equitySeries)
    {
        return LogError(equitySeries, buffer: null);
    }

    public static IEnumerable<DataSeriesPoint> LogError(DataSeries equitySeries)
    {
        using var iterator = new BufferedLogErrorIterator(equitySeries);

        while (iterator.MoveNext())
        {
            yield return iterator.Current;
        }
    }

    //вычисляет расхождения логарифма переданной серии от линии его линейной регресии
    private static IEnumerable<DataSeriesPoint> LogError(IEnumerable<DataSeriesPoint> equitySeries, DataSeriesPoint[]? buffer)
    {
        var logEquity = equitySeries.Select(x => x.WithValue(MathHelper.NaturalLog(x))); //Log довольно затратная операция
        logEquity = buffer is null ? logEquity : logEquity.ToBuffer(buffer);
        var linearReg = IndicatorsCalculator.LinearRegressionThroughStartPoint(logEquity);
        var error = logEquity.Zip(linearReg, (eq, lr) => (eq - lr));

        return error;
    }

    //to manage buffer lifetime
    private struct BufferedLogErrorIterator : IEnumerator<DataSeriesPoint>
    {
        private readonly DataSeriesPoint[] _buffer;
        private readonly IEnumerator<DataSeriesPoint> _errorEnumerator;

        public BufferedLogErrorIterator(DataSeries equitySeries)
        {
            _buffer = ArrayPool<DataSeriesPoint>.Shared.Rent(equitySeries.Count);
            var error = IndicatorsCalculator.LogError(equitySeries.ToPoints(), _buffer);
            _errorEnumerator = error.GetEnumerator();
        }

        public DataSeriesPoint Current => _errorEnumerator.Current;
        object IEnumerator.Current => Current;
        public bool MoveNext() => _errorEnumerator.MoveNext();
        public void Reset() => _errorEnumerator.Reset();
        public void Dispose()
        {
            ArrayPool<DataSeriesPoint>.Shared.Return(_buffer);
            _errorEnumerator.Dispose();
        }
    }

    public static IEnumerable<DataSeriesPoint> CalculateExponentialRegression(DataSeries equitySeries)
    {
        var logErr = IndicatorsCalculator.LogError(equitySeries);

        var expReg = equitySeries.ToPoints().Zip(logErr, (eq, err) =>
            eq.WithValue(Math.Exp(MathHelper.NaturalLog(eq) - err)));

        return expReg;
    }

    //from wealthlab
    public static double SharpeRatio(DataSeries monthReturnSeries, double cashReturnRate)
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

    //for points
    public static double SharpeRatio(IEnumerable<DataSeriesPoint> monthReturnSeries)
    {
        var values = monthReturnSeries.Select(x => x.Value);
        var avg = values.Average();
        var stdDev = StdDevs(values);
        var sharpe = Math.Sqrt(12.0) * avg / stdDev;

        return sharpe;
    }

    public static double StdDevs(IEnumerable<double> values)
    {
        double sum = 0.0;
        double sumSq = 0.0;
        var cnt = 0;

        foreach (var val in values)
        {
            sum += val;
            sumSq += val * val;
            cnt++;
        }

        if (cnt == 0)
        {
            return 0.0;
        }

        double err = Math.Sqrt((sumSq - sum * sum / cnt) / cnt);

        if (double.IsNaN(err))
        {
            return 0.0;
        }

        return err;
    }

    public static IEnumerable<DataSeriesPoint> DrawdownPercentage(IEnumerable<DataSeriesPoint> equitySeries)
    {
        using var enumerator = equitySeries.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            yield break;
        }

        var pivot = enumerator.Current.Value;

        do
        {
            var point = enumerator.Current;

            if (point.Value < pivot)
            {
                var drawndown = 100 * (pivot - point.Value) / pivot;
                yield return point.WithValue(drawndown);
            }
            else
            {
                pivot = point.Value;
                yield return point.WithValue(0);
            }
        }
        while (enumerator.MoveNext());
    }

    public static TimeSpan LongestDrawdown(IEnumerable<DataSeriesPoint> drawdownSeries)
    {
        var drawdowns = IndicatorsCalculator.SeparateDrawdown(drawdownSeries);

        var maxSpan = TimeSpan.Zero;

        foreach (var drawdown in drawdowns)
        {
            var span = GetSpan(drawdown);

            if (span > maxSpan)
            {
                maxSpan = span;
            }
        }

        return maxSpan;

        static TimeSpan GetSpan(IEnumerable<DataSeriesPoint> drawdown)
        {
            using var enumerator = drawdown.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                return TimeSpan.Zero;
            }

            var first = enumerator.Current;

            while (enumerator.MoveNext()) ;

            var last = enumerator.Current;

            return last.Date - first.Date;
        }
    }

    //величина просадки умноженная на ее продолжительность, просуммированные по всем просадкам
    public static double SumDrawdownDensity(IEnumerable<DataSeriesPoint> drawdownSeries)
    {
        var drawdowns = IndicatorsCalculator.SeparateDrawdown(drawdownSeries);

        var density = 0.0;

        foreach (var drawdown in drawdowns)
        {
            density += GetDensity(drawdown);
        }

        return density;

        static double GetDensity(IEnumerable<DataSeriesPoint> drawdown)
        {
            var magnitude = 0d;
            var width = 0;

            foreach (var point in drawdown)
            {
                if (point.Value > magnitude)
                {
                    magnitude = point.Value;
                }

                width++;
            }

            return magnitude * width;
        }
    }

    //WARN: в текущей реализации необходимо итерироваться последовательно по всем просадкам, пропуски не поддерживаются
    private static IEnumerable<IEnumerable<DataSeriesPoint>> SeparateDrawdown(IEnumerable<DataSeriesPoint> drawdownSeries)
    {
        using var enumerator = drawdownSeries.GetEnumerator();

        while (enumerator.MoveNext())
        {
            var point = enumerator.Current;

            if (point.Value > 0)
            {
                yield return GetDrawdownIterator(enumerator);
            }
        }

        static IEnumerable<DataSeriesPoint> GetDrawdownIterator(IEnumerator<DataSeriesPoint> seriesIterator)
        {
            var point = seriesIterator.Current;

            while (point.Value != 0 && seriesIterator.MoveNext())
            {
                yield return point;

                point = seriesIterator.Current;
            }
        }
    }

    public static IEnumerable<DataSeriesPoint> CalculateClosedEquity(SystemResults results)
    {
        using var equityEnumerator = results.EquityCurve.ToPoints().GetEnumerator();
        using var positionsEnumerator = results.Positions.Where(x => !x.Active).GetEnumerator();

        //начальная точка
        if (!equityEnumerator.MoveNext())
        {
            yield break;
        }

        yield return equityEnumerator.Current;

        var equity = equityEnumerator.Current;

        while (positionsEnumerator.MoveNext())
        {
            var position = positionsEnumerator.Current;

            //пересечение позиций
            if (equity.Date >= position.ExitDate)
            {
                continue;
            }

            while (equityEnumerator.MoveNext())
            {
                equity = equityEnumerator.Current;

                if (equity.Date >= position.ExitDate)
                {
                    yield return equity;
                    break;
                }
            }
        }

        //конечная точка
        if (equityEnumerator.MoveNext())
        {
            while (equityEnumerator.MoveNext()) ;

            yield return new DataSeriesPoint(equity.Value, equityEnumerator.Current.Date);
        }
    }
}
