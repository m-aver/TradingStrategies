using System.Globalization;

//разбивает исходный диапазон дат на периоды

namespace TradingStrategies.Backtesting.Utility
{
    public interface IPeriodSeparator
    {
        IEnumerable<DateTimeRange> GetPeriods(DateTimeRange range, PeriodInfo periodInfo);
    }

    public static class PeriodSeparatorFactory
    {
        public static IPeriodSeparator Create(PeriodCalcType calcType)
        {
            return calcType switch
            {
                PeriodCalcType.Simple => new PeriodSeparator(),
                PeriodCalcType.Aligned => new AlignedPeriodSeparator(),
                _ => throw new ArgumentException(nameof(calcType)),
            };
        }
    }

    internal readonly struct PeriodSeparator : IPeriodSeparator
    {
        public IEnumerable<DateTimeRange> GetPeriods(DateTimeRange range, PeriodInfo periodInfo)
        {
            var periods = periodInfo.PeriodType switch
            {
                Period.Day => GetPeriodsByDay(range, periodInfo.PeriodUnit),
                Period.Week => GetPeriodsByDay(range, periodInfo.PeriodUnit * DateTimeConsts.DaysInWeek),
                Period.Month => GetPeriodsByMonth(range, periodInfo.PeriodUnit),
                Period.Quarter => GetPeriodsByMonth(range, periodInfo.PeriodUnit * DateTimeConsts.MonthsInQuarter),
                Period.Year => GetPeriodsByYear(range, periodInfo.PeriodUnit),
                _ => throw new ArgumentOutOfRangeException(nameof(periodInfo.PeriodType)),
            };

            return periods;
        }

        private static IEnumerable<DateTimeRange> GetPeriodsByDay(DateTimeRange range, int days)
        {
            return GetPeriodsByStep(range, (currentDate) => currentDate + TimeSpan.FromDays(days));
        }

        private static IEnumerable<DateTimeRange> GetPeriodsByMonth(DateTimeRange range, int months)
        {
            return GetPeriodsByStep(range, (currentDate) => currentDate.AddMonths(months));
        }

        private static IEnumerable<DateTimeRange> GetPeriodsByYear(DateTimeRange range, int years)
        {
            return GetPeriodsByStep(range, (currentDate) => currentDate.AddYears(years));
        }

        private static IEnumerable<DateTimeRange> GetPeriodsByStep(DateTimeRange range, Func<DateTime, DateTime> nextDateGenerator)
        {
            return PeriodSeparatorHelper.GetPeriodsByStep(range, nextDateGenerator);
        }
    }

    internal readonly struct AlignedPeriodSeparator : IPeriodSeparator
    {
        public IEnumerable<DateTimeRange> GetPeriods(DateTimeRange range, PeriodInfo periodInfo)
        {
            var periods = periodInfo.PeriodType switch
            {
                Period.Day => GetPeriodsByDay(range, periodInfo.PeriodUnit),
                Period.Week => GetPeriodsByWeek(range, periodInfo.PeriodUnit),
                Period.Month => GetPeriodsByMonth(range, periodInfo.PeriodUnit),
                Period.Quarter => GetPeriodsByQuarter(range, periodInfo.PeriodUnit),
                Period.Year => GetPeriodsByYear(range, periodInfo.PeriodUnit),
                _ => throw new ArgumentOutOfRangeException(nameof(periodInfo.PeriodType)),
            };

            return periods;
        }

        private static IEnumerable<DateTimeRange> GetPeriodsByDay(DateTimeRange range, int days)
        {
            return GetPeriodsByStep(range, (currentDate) =>
            {
                var next = currentDate.AddDays(days);
                return new DateTime(next.Year, next.Month, next.Day, 0, 0, 0);
            });
        }

        private static IEnumerable<DateTimeRange> GetPeriodsByWeek(DateTimeRange range, int weeks)
        {
            var firstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
            var oneDaySpan = TimeSpan.FromDays(1);

            return GetPeriodsByStep(range, (currentDate) =>
            {
                var days = weeks * DateTimeConsts.DaysInWeek;
                var next = currentDate.AddDays(days);
                DescendToFirstDayOfWeek(ref next);
                return new DateTime(next.Year, next.Month, next.Day, 0, 0, 0);
            });

            void DescendToFirstDayOfWeek(ref DateTime date)
            {
                while (date.DayOfWeek != firstDay)
                {
                    date -= oneDaySpan;
                }
            }
        }

        private static IEnumerable<DateTimeRange> GetPeriodsByMonth(DateTimeRange range, int months)
        {
            return GetPeriodsByStep(range, (currentDate) =>
            {
                var next = currentDate.AddMonths(months);
                return new DateTime(next.Year, next.Month, 1);
            });
        }

        private static IEnumerable<DateTimeRange> GetPeriodsByQuarter(DateTimeRange range, int quarters)
        {
            return GetPeriodsByStep(range, (currentDate) =>
            {
                var months = quarters * DateTimeConsts.MonthsInQuarter;
                var next = currentDate.AddMonths(months);
                DescendToQuarterStart(ref next);
                return new DateTime(next.Year, next.Month, 1);
            });

            static void DescendToQuarterStart(ref DateTime date)
            {
                while (((date.Month - 1) % DateTimeConsts.MonthsInQuarter) != 0)
                {
                    date -= TimeSpan.FromDays(date.Day + 1);
                }
            }
        }

        private static IEnumerable<DateTimeRange> GetPeriodsByYear(DateTimeRange range, int years)
        {
            return GetPeriodsByStep(range, (currentDate) =>
            {
                var next = currentDate.AddYears(years);
                return new DateTime(next.Year, 1, 1);
            });
        }

        private static IEnumerable<DateTimeRange> GetPeriodsByStep(DateTimeRange range, Func<DateTime, DateTime> nextDateGenerator)
        {
            return PeriodSeparatorHelper.GetPeriodsByStep(range, nextDateGenerator);
        }
    }

    internal static class PeriodSeparatorHelper
    {
        public static IEnumerable<DateTimeRange> GetPeriodsByStep(DateTimeRange range, Func<DateTime, DateTime> nextDateGenerator)
        {
            var currentDate = range.DateTime;
            var nextDate = nextDateGenerator(currentDate);
            var endDate = range.DateTime + range.Offset;

            while (nextDate < endDate)
            {
                yield return new DateTimeRange(currentDate, nextDate - currentDate);
                currentDate = nextDate;
                nextDate = nextDateGenerator(currentDate);
            }

            yield return new DateTimeRange(currentDate, endDate - currentDate);
        }
    }
}
