using TradingStrategies.Backtesting.Utility;
using WealthLab;

namespace TradingStrategies.UnitTests
{
    public record PeriodicalSeparatorTestData(
        DataSeries SeriesToSeparate,
        PeriodInfo PeriodInfo,
        DataSeries SeparatedSeries
    )
    {
        public PeriodicalSeparatorTestData(
            IEnumerable<(double value, string date)> seriesToSeparate,
            PeriodInfo periodInfo,
            IEnumerable<(double value, string date)> separatedSeries) : this(
                SeriesToSeparate: seriesToSeparate
                    .Select(x => new DataSeriesPoint(x.value, DateTime.Parse(x.date)))
                    .ToSeries("series-to-separate"),
                PeriodInfo: periodInfo,
                SeparatedSeries: separatedSeries
                    .Select(x => new DataSeriesPoint(x.value, DateTime.Parse(x.date)))
                    .ToSeries("separated-series"))
        {
        }
    };

    //periodical calculator
    public partial class PeriodicalSeriesCalculatorTests
    {
        public static IEnumerable<object[]> GetPeriodicalCalculatorTestData()
        {
            var onePointSeries = new PeriodicalSeparatorTestData(
                seriesToSeparate: [(1, "01.05.2010")],
                periodInfo: PeriodInfo.Monthly,
                separatedSeries: [(1, "01.05.2010")]
            );

            yield return [onePointSeries];

            var oneMonthSeparation = new PeriodicalSeparatorTestData(
                seriesToSeparate:
                [
                    (1, "01.05.2010"), (2, "05.05.2010"), (3, "10.05.2010"), (4, "15.05.2010"),
                ],
                periodInfo: PeriodInfo.Monthly,
                separatedSeries: [(4, "15.05.2010")]
            );

            yield return [oneMonthSeparation];

            var monthSeparation = new PeriodicalSeparatorTestData(
                seriesToSeparate:
                [
                    (1, "01.05.2010"), (2, "05.05.2010"), (3, "10.05.2010"), (4, "15.05.2010"),
                    (5, "01.06.2010"), (6, "05.06.2010"), (7, "10.06.2010"), (8, "15.06.2010"),
                    (9, "01.07.2010"), (10, "05.07.2010"), (11, "10.07.2010"), (12, "15.07.2010"),
                ],
                periodInfo: PeriodInfo.Monthly,
                separatedSeries: [(4, "15.05.2010"), (8, "15.06.2010"), (12, "15.07.2010")]
            );

            yield return [monthSeparation];

            var shiftedMonthSeparation = new PeriodicalSeparatorTestData(
                seriesToSeparate:
                [
                    (1, "02.05.2010"), (2, "05.05.2010"), (3, "10.05.2010"), (4, "15.05.2010"),
                    (5, "02.06.2010"), (6, "05.06.2010"), (7, "10.06.2010"), (8, "15.06.2010"),
                    (9, "02.07.2010"), (10, "05.07.2010"), (11, "10.07.2010"), (12, "15.07.2010"),
                ],
                periodInfo: PeriodInfo.Monthly,
                separatedSeries: [(4, "15.05.2010"), (8, "15.06.2010"), (12, "15.07.2010")]
            );

            yield return [shiftedMonthSeparation];

            var sparsedShiftedMonthSeparation = new PeriodicalSeparatorTestData(
                seriesToSeparate:
                [
                    (1, "02.05.2010"), (2, "05.05.2010"), (3, "10.05.2010"), (4, "15.05.2010"),
                    //06.2010, 07.2010 is skipped
                    (9, "03.08.2010"), (10, "05.08.2010"), (11, "10.08.2010"), (12, "15.08.2010"),
                    //09.2010 is skipped
                    (13, "03.10.2010"),
                ],
                periodInfo: PeriodInfo.Monthly,
                separatedSeries: 
                [
                    (4, "15.05.2010"), (4, "02.07.2010"), (4, "02.08.2010"), 
                    (12, "15.08.2010"), (12, "02.10.2010"), 
                    (13, "03.10.2010")
                ]
            );

            yield return [sparsedShiftedMonthSeparation];
        }

        public record InvalidPeriodicalSeparatorTestData(
            DataSeries SeriesToSeparate,
            PeriodInfo PeriodInfo,
            Type TypeOfExeption
        )
        {
            public InvalidPeriodicalSeparatorTestData(
                IEnumerable<(double value, string date)> seriesToSeparate,
                PeriodInfo periodInfo,
                Type typeOfExeption) : this(
                    SeriesToSeparate: seriesToSeparate
                        .Select(x => new DataSeriesPoint(x.value, DateTime.Parse(x.date)))
                        .ToSeries("series-to-separate"),
                    PeriodInfo: periodInfo,
                    TypeOfExeption: typeOfExeption)
            {
            }
        };

        public static IEnumerable<object[]> GetPeriodicalCalculatorTestData_Invalid()
        {
            var emptySeries = new InvalidPeriodicalSeparatorTestData(
                seriesToSeparate: [],
                periodInfo: PeriodInfo.Monthly,
                typeOfExeption: typeof(ArgumentException)
            );

            yield return [emptySeries];

            var unorderedSeries = new InvalidPeriodicalSeparatorTestData(
                seriesToSeparate: [(1, "20.05.2020"), (2, "19.05.2020"), (3, "21.05.2020")],
                periodInfo: PeriodInfo.Monthly,
                typeOfExeption: typeof(ArgumentException)
            );

            yield return [unorderedSeries];
        }
    }
}
