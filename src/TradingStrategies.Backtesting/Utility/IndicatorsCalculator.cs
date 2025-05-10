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

    //TODO: кажется не учитывает просадку если она заканчивает датасет
    public static TimeSpan LongestDrawdown(IEnumerable<DataSeriesPoint> drawdownSeries)
    {
        using var enumerator = drawdownSeries.GetEnumerator();

        if (!enumerator.MoveNext())
        {
            return TimeSpan.Zero;
        }

        var pivot = enumerator.Current;
        var maxSpan = TimeSpan.Zero;

        do
        {
            var point = enumerator.Current;

            if (point.Value == 0)
            {
                var span = point.Date - pivot.Date;
                if (span > maxSpan)
                {
                    maxSpan = span;
                }

                pivot = point;
            }
        }
        while (enumerator.MoveNext());

        return maxSpan;
    }

    //величина просадки умноженная на ее продолжительность, просуммированные по всем просадкам
    public static double SumDrawdownDensity(IEnumerable<DataSeriesPoint> drawdownSeries)
    {
        var density = 0.0;
        var width = 0;
        var magnitude = 0.0;

        foreach (var point in drawdownSeries)
        {
            if (point.Value > magnitude)
            {
                magnitude = point.Value;
            }

            if (point.Value == 0 && magnitude > 0)
            {
                density += (magnitude * width);
                magnitude = 0;
                width = 0;
            }
            else
            {
                width++;
            }
        }

        return density;
    }
}
