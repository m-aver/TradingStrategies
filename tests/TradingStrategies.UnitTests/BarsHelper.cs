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
        var bars = new Bars(
            symbol: $"bars: {range}",
            scale: BarScale.Minute,
            barInterval: 60);

        bars = GenerateBarsWithRandomPrices(bars, range);

        return bars;
    }

    public static Bars FromRangeWithRandomPricesAndOneDayPeriod(DateTimeRange range)
    {
        var bars = new Bars(
            symbol: $"bars: {range}",
            scale: BarScale.Daily,
            barInterval: 1);

        bars.BarInterval = 1; //в конструкторе BarInterval почему-то обнуляется, если не Intraday

        bars = GenerateBarsWithRandomPrices(bars, range);

        return bars;
    }

    private static Bars GenerateBarsWithRandomPrices(Bars bars, DateTimeRange range)
    {
        var random = Random.Shared;

        foreach (var period in GetBarsDates(bars, range))
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

    private static IEnumerable<DateTimeRange> GetBarsDates(Bars bars, DateTimeRange range)
    {
        return PeriodSeparatorHelper.GetPeriodsByStep(range, (currentDate) => bars.Scale switch
        {
            BarScale.Tick => currentDate.AddTicks(bars.BarInterval),
            BarScale.Second => currentDate.AddSeconds(bars.BarInterval),
            BarScale.Minute => currentDate.AddMinutes(bars.BarInterval),
            BarScale.Daily => currentDate.AddDays(bars.BarInterval),
            BarScale.Weekly => currentDate.AddDays(bars.BarInterval * DateTimeConsts.DaysInWeek),
            BarScale.Monthly => currentDate.AddDays(bars.BarInterval),
            BarScale.Quarterly => currentDate.AddMonths(bars.BarInterval * DateTimeConsts.MonthsInYear),
            BarScale.Yearly => currentDate.AddYears(bars.BarInterval),
            _ => throw new ArgumentOutOfRangeException(nameof(bars.Scale), bars.Scale, "bars scale out of range"),
        });
    }
}
