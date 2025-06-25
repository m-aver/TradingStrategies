using TradingStrategies.Backtesting.Utility;
using TradingStrategies.Utilities.InternalsProxy;
using WealthLab;

namespace TradingStrategies.UnitTests;

//test data
public partial class SystemResultsTests
{
    public class TestData
    {
        public IEnumerable<Position> Positions { get; set; } = [];
        public IEnumerable<Bars> BarsSet { get; set; } = [];

        public TestData Add(Bars bars, params IEnumerable<Position> positions)
        {
            BarsSet = BarsSet.Append(bars);
            Positions = positions.Concat(positions);
            return this;
        }

        public TestData AddLong(string activeRange, int shares)
        {
            var bars = BarsSet.Last();
            var pos = GetPosition(bars, PositionType.Long, activeRange);
            pos.OverrideShareSize = shares;
            Positions = Positions.Append(pos);
            return this;
        }

        public TestData AddShort(string activeRange, int shares)
        {
            var bars = BarsSet.Last();
            var pos = GetPosition(bars, PositionType.Short, activeRange);
            pos.OverrideShareSize = shares;
            Positions = Positions.Append(pos);
            return this;
        }

        public TestData Materialize()
        {
            BarsSet = BarsSet.ToArray();
            Positions = Positions.ToArray();
            return this;
        }

        public TestData AssignRandomPositionsExitType()
        {
            var random = Random.Shared;
            Positions = Positions.Select(x =>
            {
                x.Close(x.ExitBar, x.ExitPrice, GetRandomOrderType());
                return x;
            });
            OrderType GetRandomOrderType() => (OrderType)random.Next((int)OrderType.Market, (int)OrderType.AtClose);
            return this;
        }
    }

    public static IEnumerable<object[]> GetData() => GetTestData().Select(x => new object[] { x });

    public static IEnumerable<TestData> GetTestData() => GetTestData(false).Concat(GetTestData(true));

    public static IEnumerable<TestData> GetTestData(bool intraday) => GetTestDataInternal(intraday)
        .Select(x => x.AssignRandomPositionsExitType().Materialize());

    private static IEnumerable<TestData> GetTestDataInternal(bool intraday)
    {
        //single bars, no positions
        yield return new TestData().Add(
            GetBars("01.01.2020 - 01.02.2020", intraday),
            []
        );

        //single bars, single position
        yield return new TestData()
            .Add(
            GetBars("01.01.2020 - 01.01.2021", intraday))
                .AddLong("01.01.2020 - 02.01.2020", 1);

        //single bars, non overlapped positions
        yield return new TestData()
            .Add(
            GetBars("01.01.2020 - 01.01.2021", intraday))
                .AddLong("01.01.2020 - 02.01.2020", 1)
                .AddLong("01.02.2020 - 02.02.2020", 2)
                .AddShort("01.03.2020 - 02.03.2020", 1)
                .AddShort("01.04.2020 - 02.04.2020", 10)
                .AddShort("01.05.2020 - 02.05.2020", 1)
                .AddLong("01.06.2020 - 02.06.2020", 1);

        //single bars, non overlapped positions, with zero positions
        yield return new TestData()
            .Add(
            GetBars("01.01.2020 - 01.01.2021", intraday))
                .AddLong("01.01.2020 - 02.01.2020", 1)
                .AddLong("01.02.2020 - 02.02.2020", 2)
                .AddShort("01.03.2020 - 02.03.2020", 1)
                .AddShort("01.04.2020 - 02.04.2020", 0)
                .AddShort("01.05.2020 - 02.05.2020", 1)
                .AddLong("01.06.2020 - 02.06.2020", 0)
                .AddLong("01.07.2020 - 02.07.2020", 1);

        //multi non overlapperd bars, non overlapped positions, with zero positions
        yield return new TestData()
            .Add(
            GetBars("01.01.2020 - 01.01.2021", intraday))
                .AddLong("01.01.2020 - 02.01.2020", 1)
                .AddLong("01.02.2020 - 02.02.2020", 2)
                .AddShort("01.03.2020 - 02.03.2020", 1)
                .AddShort("01.04.2020 - 02.04.2020", 0)
                .AddShort("01.05.2020 - 02.05.2020", 1)
                .AddLong("01.06.2020 - 02.06.2020", 0)
                .AddLong("01.07.2020 - 02.07.2020", 1)
            .Add(
            GetBars("01.01.2021 - 01.01.2022", intraday))
                .AddLong("01.01.2021 - 02.01.2021", 2)
                .AddLong("01.02.2021 - 02.02.2021", 21)
                .AddShort("01.03.2021 - 02.03.2021", 0)
                .AddShort("01.04.2021 - 02.04.2021", 3)
                .AddShort("01.05.2021 - 02.05.2021", 10)
                .AddLong("01.06.2021 - 02.06.2021", 0)
                .AddLong("01.07.2021 - 02.07.2021", 1);

        //multi overlapper bars, overlapped positions, with zero positions
        yield return new TestData()
            .Add(
            //non overlapped
            GetBars("01.01.2020 - 01.01.2021", intraday))
                .AddLong("01.01.2020 - 02.01.2020", 1)
                .AddLong("01.02.2020 - 02.02.2020", 2)
                .AddShort("01.03.2020 - 02.03.2020", 1)
                .AddShort("01.04.2020 - 02.04.2020", 0)
                .AddShort("01.05.2020 - 02.05.2020", 1)
                .AddLong("01.06.2020 - 02.06.2020", 0)
                .AddLong("01.07.2020 - 02.07.2020", 1)
            .Add(
            GetBars("01.01.2021 - 01.01.2022", intraday))
                .AddLong("01.01.2021 - 02.01.2021", 2)
                .AddLong("01.02.2021 - 02.02.2021", 21)
                .AddShort("01.03.2021 - 02.03.2021", 0)
                .AddShort("01.04.2021 - 02.04.2021", 3)
                .AddShort("01.05.2021 - 02.05.2021", 10)
                .AddLong("01.06.2021 - 02.06.2021", 0)
                .AddLong("01.07.2021 - 02.07.2021", 1)
            .Add(
            //full duplicate of previous
            GetBars("01.01.2021 - 01.01.2022", intraday))
                .AddLong("01.01.2021 - 02.01.2021", 2)
                .AddLong("01.02.2021 - 02.02.2021", 21)
                .AddShort("01.03.2021 - 02.03.2021", 0)
                .AddShort("01.04.2021 - 02.04.2021", 3)
                .AddShort("01.05.2021 - 02.05.2021", 10)
                .AddLong("01.06.2021 - 02.06.2021", 0)
                .AddLong("01.07.2021 - 02.07.2021", 1)
            .Add(
            //partially overlapped
            GetBars("01.06.2021 - 01.06.2022", intraday))
                .AddLong("01.06.2021 - 01.07.2021", 2)
                .AddLong("01.07.2021 - 02.07.2021", 21)
                .AddShort("01.06.2021 - 02.06.2021", 3)
                .AddShort("01.07.2021 - 01.08.2021", 3)
                .AddShort("02.08.2021 - 01.09.2021", 10)
                .AddLong("01.10.2021 - 10.11.2021", 5)
                .AddLong("09.11.2021 - 01.12.2021", 1)
                .AddLong("01.12.2021 - 01.02.2022", 1)
                .AddShort("01.01.2022 - 01.03.2022", 1)
                .AddLong("02.03.2022 - 01.04.2022", 3);

        //overlapperd bars, overlapped positions, large positions (overflow cash)
        const int largeShares = 999999999;
        yield return new TestData()
            .Add(
            GetBars("01.01.2020 - 01.01.2021", intraday))
                .AddLong("01.01.2020 - 02.01.2020", 1)
                .AddLong("01.02.2020 - 02.02.2020", 2)
                .AddShort("01.03.2020 - 02.03.2020", 1)
                .AddShort("01.04.2020 - 02.04.2020", 0)
                .AddShort("01.05.2020 - 02.05.2020", largeShares)
                .AddLong("01.06.2020 - 02.06.2020", 0)
                .AddLong("01.07.2020 - 02.07.2020", 1)
            .Add(
            GetBars("01.01.2020 - 01.01.2021", intraday))
                .AddLong("11.01.2020 - 02.02.2020", largeShares)
                .AddLong("11.02.2020 - 02.03.2020", largeShares)
                .AddShort("11.03.2020 - 02.04.2020", 1)
                .AddShort("11.04.2020 - 02.05.2020", largeShares)
                .AddShort("11.05.2020 - 02.06.2020", 1)
                .AddLong("11.06.2020 - 02.07.2020", 0)
                .AddLong("11.07.2020 - 02.08.2020", largeShares);
    }

    private static Bars GetBars(string range, bool intraday) => intraday 
        ? BarsHelper.FromRangeWithRandomPricesAndOneHourPeriod(DateTimeRange.Parse(range))
        : BarsHelper.FromRangeWithRandomPricesAndOneDayPeriod(DateTimeRange.Parse(range));

    private static Position GetPosition(Bars bars, PositionType type, string activeRange) =>
        GetPosition(bars, type, DateTimeRange.Parse(activeRange));
    private static Position GetPosition(Bars bars, PositionType type, DateTimeRange activeRange) =>
        GetPosition(bars, type, activeRange.DateTime, activeRange.EndDateTime);

    //create position opened and closed by bars close prices at specified dates
    private static Position GetPosition(Bars bars, PositionType type, DateTime entry, DateTime exit) => new Position(
        bars: bars,
        positionType_1: type,
        basisPrice: 0,
        entryBar: bars.ConvertDateToBar(entry, exactMatch: false),
        entryPrice: bars.Close[bars.ConvertDateToBar(entry, exactMatch: false)],
        exitBar: bars.ConvertDateToBar(exit, exactMatch: false),
        exitPrice: bars.Close[bars.ConvertDateToBar(exit, exactMatch: false)]
    );
}
