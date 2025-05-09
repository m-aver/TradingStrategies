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
    public static DataSeries CalculateError(DataSeries equitySeries)
    {
        var startingCapital = equitySeries.Count > 0 ? equitySeries[0] : 0;
        var normalizedEquity = equitySeries.ToPoints()
            .Select(x => x - startingCapital)
            .Select(static x => x.WithValue(Math.Max(1, Math.Abs(x)))); //костыли чтобы ln(x) не ругался

        var logEquity = normalizedEquity
            .Select(static x => x.WithValue(MathHelper.NaturalLog(x)))
            .ToArray();
        var linearReg = IndicatorsCalculator.LinearRegression(logEquity);
        var error = logEquity.Zip(linearReg, static (eq, lr) => (eq - lr));

        return error.ToSeries();
    }
}
