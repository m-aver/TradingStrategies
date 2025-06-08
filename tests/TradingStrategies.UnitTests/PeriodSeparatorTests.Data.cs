using TradingStrategies.Backtesting.Utility;

namespace TradingStrategies.UnitTests
{
    public record PeriodSeparatorTestData(
        DateTimeRange RangeToSeparate,
        PeriodInfo PeriodInfo,
        IEnumerable<DateTimeRange> ResultRanges
    )
    {
        public PeriodSeparatorTestData(
            string rangeToSeparate,
            PeriodInfo periodInfo,
            IEnumerable<string> resultRanges) : this(
                RangeToSeparate: DateTimeRange.Parse(rangeToSeparate),
                PeriodInfo: periodInfo,
                ResultRanges: resultRanges.Select(DateTimeRange.Parse))
        {
        }
    };

    public record PeriodSeparatorTestData_Aligned_Week : PeriodSeparatorTestData
    {
        public DayOfWeek FirstDayOfWeek { get; }

        public PeriodSeparatorTestData_Aligned_Week(
            string rangeToSeparate,
            PeriodInfo periodInfo,
            IEnumerable<string> resultRanges,
            DayOfWeek firstDayOfWeek) : base(
                rangeToSeparate: rangeToSeparate,
                periodInfo: periodInfo,
                resultRanges: resultRanges)
        {
            FirstDayOfWeek = firstDayOfWeek;
        }
    };

    //period separator
    public partial class PeriodSeparatorTests
    {
        public static IEnumerable<object[]> GetPeriodSeparatorTestData_Simple()
        {
            var oneMonthPeriod = new PeriodSeparatorTestData(
                rangeToSeparate: "01.05.2010 - 15.05.2010",
                periodInfo: PeriodInfo.Monthly,
                resultRanges: ["01.05.2010 - 15.05.2010"]
            );

            yield return [oneMonthPeriod];

            var multiMonthPeriod = new PeriodSeparatorTestData(
                rangeToSeparate: "01.02.2010 - 15.04.2010",
                periodInfo: PeriodInfo.Monthly,
                resultRanges: ["01.02.2010 - 01.03.2010", "01.03.2010 - 01.04.2010", "01.04.2010 - 15.04.2010"]
            );

            yield return [multiMonthPeriod];

            var shiftedMultiMonthPeriod = new PeriodSeparatorTestData(
                rangeToSeparate: "10.02.2010 - 15.04.2010",
                periodInfo: PeriodInfo.Monthly,
                resultRanges: ["10.02.2010 - 10.03.2010", "10.03.2010 - 10.04.2010", "10.04.2010 - 15.04.2010"]
            );

            yield return [shiftedMultiMonthPeriod];

            var multiDoubleYearPeriod = new PeriodSeparatorTestData(
                rangeToSeparate: "01.05.2010 - 15.05.2012",
                periodInfo: PeriodInfo.ByYear(2),
                resultRanges: ["01.05.2010 - 01.05.2012", "01.05.2012 - 15.05.2012"]
            );

            yield return [multiDoubleYearPeriod];

            var multiTenDayPeriod = new PeriodSeparatorTestData(
                rangeToSeparate: "15.12.2024 - 15.01.2025",
                periodInfo: PeriodInfo.ByDay(10),
                resultRanges: ["15.12.2024 - 25.12.2024", "25.12.2024 - 04.01.2025", "04.01.2025 - 14.01.2025", "14.01.2025 - 15.01.2025"]
            );

            yield return [multiTenDayPeriod];
        }

        public static IEnumerable<object[]> GetPeriodSeparatorTestData_Aligned()
        {
            var oneMonthPeriod = new PeriodSeparatorTestData(
                rangeToSeparate: "01.05.2010 - 15.05.2010",
                periodInfo: PeriodInfo.Monthly,
                resultRanges: ["01.05.2010 - 15.05.2010"]
            );

            yield return [oneMonthPeriod];

            var multiMonthPeriod = new PeriodSeparatorTestData(
                rangeToSeparate: "01.02.2010 - 15.04.2010",
                periodInfo: PeriodInfo.Monthly,
                resultRanges: ["01.02.2010 - 01.03.2010", "01.03.2010 - 01.04.2010", "01.04.2010 - 15.04.2010"]
            );

            yield return [multiMonthPeriod];

            var shiftedMultiMonthPeriod = new PeriodSeparatorTestData(
                rangeToSeparate: "10.02.2010 - 15.04.2010",
                periodInfo: PeriodInfo.Monthly,
                resultRanges: ["10.02.2010 - 01.03.2010", "01.03.2010 - 01.04.2010", "01.04.2010 - 15.04.2010"]
            );

            yield return [shiftedMultiMonthPeriod];

            var shiftedMultiDoubleYearPeriod = new PeriodSeparatorTestData(
                rangeToSeparate: "10.05.2010 - 15.05.2012",
                periodInfo: PeriodInfo.ByYear(2),
                resultRanges: ["10.05.2010 - 01.01.2012", "01.01.2012 - 15.05.2012"]
            );

            yield return [shiftedMultiDoubleYearPeriod];

            var multiTenDayPeriod = new PeriodSeparatorTestData(
                rangeToSeparate: "15.12.2024 12:00:00 - 15.01.2025 16:00:00",
                periodInfo: PeriodInfo.ByDay(10),
                resultRanges: ["15.12.2024 12:00:00 - 25.12.2024", "25.12.2024 - 04.01.2025", "04.01.2025 - 14.01.2025", "14.01.2025 - 15.01.2025 16:00:00"]
            );

            yield return [multiTenDayPeriod];

            var quarterPeriod = new PeriodSeparatorTestData(
                rangeToSeparate: "01.01.2010 - 15.11.2010",
                periodInfo: PeriodInfo.Quarterly,
                resultRanges: ["01.01.2010 - 01.04.2010", "01.04.2010 - 01.07.2010", "01.07.2010 - 01.10.2010", "01.10.2010 - 15.11.2010"]
            );

            yield return [quarterPeriod];

            var shiftedQuarterPeriodAcrossYears = new PeriodSeparatorTestData(
                rangeToSeparate: "10.05.2010 - 15.01.2011",
                periodInfo: PeriodInfo.Quarterly,
                resultRanges: ["10.05.2010 - 01.07.2010", "01.07.2010 - 01.10.2010", "01.10.2010 - 01.01.2011", "01.01.2011 - 15.01.2011"]
            );

            yield return [shiftedQuarterPeriodAcrossYears];
        }

        public static IEnumerable<object[]> GetPeriodSeparatorTestData_Aligned_Week()
        {
            var crossYearsWeekPeriod = new PeriodSeparatorTestData_Aligned_Week(
                rangeToSeparate: "28.12.2024 - 13.01.2025",
                periodInfo: PeriodInfo.Weekly,
                resultRanges: ["28.12.2024 - 02.01.2025", "02.01.2025 - 09.01.2025", "09.01.2025 - 13.01.2025"],
                firstDayOfWeek: DayOfWeek.Thursday
            );

            yield return [crossYearsWeekPeriod];
        }
    }
}
