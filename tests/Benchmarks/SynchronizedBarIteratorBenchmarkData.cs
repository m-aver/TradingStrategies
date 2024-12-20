using WealthLab;

namespace TradingStrategies.Benchmarks;

internal static class SynchronizedBarIteratorBenchmarkData
{
    public static Bars[] GetLinearBars(int seriesCount, int barsCount = 1000)
    {
        return SynchronizedBarIteratorBenchmarkData
            .GetLinearBarDates(seriesCount, barsCount)
            .Select(BarsHelper.FromDates)
            .ToArray();
    }

    public static DateTime[][] GetLinearBarDates(int seriesCount, int barsCount = 1000)
    {
        var linear = Enumerable
            .Range(0, seriesCount)
            .Select(x => Enumerable
                .Range(0, barsCount)
                .Select(y => DateTime.Now + TimeSpan.FromDays(x) + TimeSpan.FromSeconds(y))
                .ToArray())
            .ToArray();

        return linear;
    }

    public static IEnumerable<DateTime[][]> GetRandomBarDates(int testsCount)
    {
        for (int i = 0; i < testsCount; i++)
        {
            var seriesCount = new Random().Next(1, 10);
            var barsCount = () => new Random().Next(10, 100);
            var hoursOffset = () => new Random().Next(1, 1000);

            var random = Enumerable
                .Range(0, seriesCount)
                .Select(x => Enumerable
                    .Range(0, barsCount())
                    .Select(y => DateTime.Now + TimeSpan.FromHours(hoursOffset()))
                    .OrderBy(x => x)
                    .ToArray())
                .ToArray();

            yield return random;
        }
    }
}
