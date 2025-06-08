using System;

namespace TradingStrategies.Backtesting.Utility
{
    public enum Period
    {
        Day,
        Week,
        Month,
        Quarter,
        Year,
    }

    public record struct PeriodInfo(Period PeriodType, int PeriodUnit)
    {
        public static PeriodInfo Dayly => ByDay(1);
        public static PeriodInfo ByDay(int days) => new PeriodInfo(Period.Day, days);

        public static PeriodInfo Weekly => ByWeek(1);
        public static PeriodInfo ByWeek(int weeks) => new PeriodInfo(Period.Week, weeks);

        public static PeriodInfo Monthly => ByMonth(1);
        public static PeriodInfo ByMonth(int months) => new PeriodInfo(Period.Month, months);

        public static PeriodInfo Quarterly => ByQuarter(1);
        public static PeriodInfo ByQuarter(int quarters) => new PeriodInfo(Period.Quarter, quarters);

        public static PeriodInfo Yearly => ByYear(1);
        public static PeriodInfo ByYear(int years) => new PeriodInfo(Period.Year, years);
    };
}
