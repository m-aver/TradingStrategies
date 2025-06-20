using TradingStrategies.Backtesting.Optimizers.Own;
using TradingStrategies.Backtesting.Utility;
using TradingStrategies.Utilities.InternalsProxy;
using WealthLab;

namespace TradingStrategies.UnitTests;

public partial class SystemResultsTests
{
    [Theory]
    [CombinatorialData]
    //[MemberData(nameof(GetData))]
    public void BuildEquityCurve_MatchNative(
        [CombinatorialMemberData(nameof(GetTestData))]TestData testData,
        [CombinatorialValues(null, -1, 0, 1, 2, 999999)] int? positionSize,
        [CombinatorialValues(null, 0, 10, 999999)] int? commision,
        [CombinatorialValues(null, 0, 10, 999999)] int? cashRate,
        [CombinatorialValues(1, 100)] int marginFactor,
        [CombinatorialValues(true, false)] bool callbackToSizePositions)
    {
        //arrange
        var strategy = new Strategy();

        var posSizeMode = positionSize is null
            ? PosSizeMode.ScriptOverride //override share size
            : positionSize < 0
                ? PosSizeMode.RawProfitShare //raw profit mode
                : PosSizeMode.SimuScript; //pos sizer value
        var posSizer = positionSize > 0 ? new ConstantPosSizer(positionSize.Value) : null;
        var posSize = new PositionSize(posSizeMode, positionSize ?? 0)
        {
            StartingCapital = 100_000,
            MarginFactor = marginFactor,
        };

        var sysPerfOwn = new SystemPerformanceOwn(strategy)
        {
            PositionSize = posSize,
        };
        var sysPerf = new SystemPerformance(strategy)
        {
            PositionSizeProxy = posSize,
        };

        var executor = new TradingSystemExecutor()
        {
            PosSize = posSize,
            ApplyDividends = false,
            ApplyInterest = false,
            ApplyCommission = false,
        };
        if (commision.HasValue)
        {
            executor.ApplyCommission = true;
            executor.Commission = new ConstantCommision(commision.Value);
        }
        if (cashRate.HasValue)
        {
            executor.ApplyInterest = true;
            executor.CashRate = cashRate.Value;
            executor.MarginRate = cashRate.Value;
        }

        var own = new SystemResultsOwn(sysPerfOwn)
        {
            CurrentCash = posSize.StartingCapital,
            CurrentEquity = posSize.StartingCapital,
        };
        var native = new SystemResults(sysPerf)
        {
            CurrentCash = posSize.StartingCapital,
            CurrentEquity = posSize.StartingCapital,
        };

        foreach (var position in testData.Positions.Order(executor))
        {
            if (callbackToSizePositions == false)
            {
                position.SharesProxy = 1;
            }

            executor.AddPosition(position);
            own.AddPosition(position);
            native.AddPosition(position);
        }

        //act
        own.BuildEquityCurve(testData.BarsSet.ToList(), executor, callbackToSizePositions, posSizer);
        native.BuildEquityCurve(testData.BarsSet.ToList(), executor, callbackToSizePositions, posSizer);

        //assert
        Assert.Equal(native.EquityCurve.ToPoints(), own.EquityCurve.ToPoints());
        Assert.Equal(native.CashCurve.ToPoints(), own.CashCurve.ToPoints());
        Assert.Equal(native.NetProfit, own.NetProfit);
        Assert.Equal(native.DividendsPaid, own.DividendsPaid);
        Assert.Equal(native.CashReturn, own.CashReturn);
        Assert.Equal(native.MarginInterest, own.MarginInterest);
        Assert.Equal(native.TotalCommission, own.TotalCommission);
    }

    private class ConstantPosSizer(int size) : PosSizer
    {
        public override string FriendlyName => nameof(CalcPositionSize);

        public override double SizePosition(Position currentPos, Bars bars, int int_0, double basisPrice, PositionType positionType_0, double riskStopLevel, double equity, double cash)
        {
            return size;
        }
    }

    private class ConstantCommision(int commision) : Commission
    {
        public override string FriendlyName => nameof(ConstantCommision);
        public override string Description => nameof(ConstantCommision);

        public override double Calculate(TradeType tradeType, OrderType orderType, double orderPrice, double shares, Bars bars)
        {
            return commision;
        }
    }
}
