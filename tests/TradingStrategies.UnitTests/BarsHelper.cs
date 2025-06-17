using TradingStrategies.Backtesting.Utility;
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

    public static Bars FromRangeWithRandomPricesAndOneHourPeriod(DateTimeRange range)
    {
        var random = Random.Shared;

        var bars = new Bars(
            symbol: Guid.NewGuid().ToString(),
            scale: BarScale.Minute,
            barInterval: 60);

        var periods = PeriodSeparatorHelper.GetPeriodsByStep(range, (currentDate) => currentDate.AddHours(1));

        foreach (var period in periods)
        {
            bars.Add(
                dateTime_0: period.DateTime,
                open: random.Next(10, 100),
                high: random.Next(10, 100),
                double_0: random.Next(10, 100),
                close: random.Next(10, 100),
                volume: random.Next(10, 100)
            );
        }

        return bars;
    }
}
