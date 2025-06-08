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

    //вычисляет расхождения логарифма переданной серии от линии его линейной регресии
    public static DataSeries LogError(DataSeries equitySeries)
    {
        var logEquity = equitySeries
            .ToPoints()
            .Select(static x => x.WithValue(MathHelper.NaturalLog(x)))
            .ToArray();
        var linearReg = IndicatorsCalculator.LinearRegression(logEquity);
        var error = logEquity.Zip(linearReg, static (eq, lr) => (eq - lr));

        return error.ToSeries("log-error");
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

            while (enumerator.MoveNext());

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
}
