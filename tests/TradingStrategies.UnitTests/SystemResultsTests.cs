using TradingStrategies.Backtesting.Optimizers.Own;
using TradingStrategies.Backtesting.Utility;
using TradingStrategies.Utilities.InternalsProxy;
using WealthLab;

namespace TradingStrategies.UnitTests;

public partial class SystemResultsTests
{
    [Theory]
    [MemberData(nameof(GetData))]
    public void BuildEquityCurve_MatchNative(TestData testData)
    {
        //arrange
        var strategy = new Strategy();
        var posSize = new PositionSize(PosSizeMode.ScriptOverride, 0)
        {
            StartingCapital = 100_000,
            MarginFactor = 1,
        };
        var sysPerfOwn = new SystemPerformanceOwn(strategy)
        {
            PositionSize = posSize,
        };
        var sysPerf = new SystemPerformance(strategy)
        {
            PositionSizeProxy = posSize
        };

        var executor = new TradingSystemExecutor()
        {
            PosSize = posSize,
            ApplyDividends = false,
            ApplyInterest = true,
            CashRate = 10,
            MarginRate = 10,
            ApplyCommission = true,
            Commission = new ConstantCommision(10),
        };

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
            executor.AddPosition(position);
            own.AddPosition(position);
            native.AddPosition(position);
        }

        //act
        own.BuildEquityCurve(testData.BarsSet.ToList(), executor, callbackToSizePositions: true, posSizer: null);
        native.BuildEquityCurve(testData.BarsSet.ToList(), executor, callbackToSizePositions: true, posSizer: null);

        //assert
        if (testData.Positions.Any()) //prevent wrong initializtion
        {
            Assert.NotEqual(0, own.NetProfit);
            Assert.NotEqual(0, native.NetProfit);
        }

        Assert.Equal(native.EquityCurve.ToPoints(), own.EquityCurve.ToPoints());
        Assert.Equal(native.CashCurve.ToPoints(), own.CashCurve.ToPoints());
        Assert.Equal(native.NetProfit, own.NetProfit);
        Assert.Equal(native.DividendsPaid, own.DividendsPaid);
        Assert.Equal(native.CashReturn, own.CashReturn);
        Assert.Equal(native.MarginInterest, own.MarginInterest);
        Assert.Equal(native.TotalCommission, own.TotalCommission);
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
