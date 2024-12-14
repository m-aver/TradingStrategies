using WealthLab;

namespace TradingStrategies.UnitTests;

internal static class BarsHelper
{
    public static Bars FromDates(IReadOnlyCollection<DateTime> dates)
    {
        var bars = new Bars(
            symbol: Guid.NewGuid().ToString(),
            scale: BarScale.Minute, //intraday
            barInterval: default);

        foreach (var date in dates)
        {
            bars.Add(date, 0, 0, 0, 0, 0);
        }

        return bars;
    }
}
