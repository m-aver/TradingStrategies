using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using TradingStrategies.Backtesting.Optimizers.Own;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Own;

public class SystemPerformanceOwn
{
    private List<PlottedIndicator> list_1 = new List<PlottedIndicator>();

    public Strategy Strategy { get; set; }
    public SystemResults Results { get; internal set; }
    public SystemResults ResultsLong { get; internal set; }
    public SystemResults ResultsShort { get; internal set; }
    public SystemResults ResultsBuyHold { get; internal set; }
    public BarScale Scale { get; internal set; }
    public int BarInterval { get; internal set; }
    public PositionSize PositionSize { get; internal set; } = new PositionSize();
    public bool IsIntraday
    {
        get
        {
            if (Scale != BarScale.Minute && Scale != BarScale.Second)
            {
                return Scale == BarScale.Tick;
            }

            return true;
        }
    }

    public List<Bars> Bars { get; } = new List<Bars>();
    public Bars BenchmarkSymbolbars { get; set; }
    public double CashReturnRate { get; set; }
    public List<Position> RawTrades { get; internal set; }

    public SystemPerformanceOwn(Strategy strategy)
    {
        Strategy = strategy;
        Results = new SystemResults(this);
        ResultsLong = new SystemResults(this);
        ResultsShort = new SystemResults(this);
        ResultsBuyHold = new SystemResults(this);
    }

    internal void method_0()
    {
        Results.method_0();
        ResultsLong.method_0();
        ResultsShort.method_0();
        ResultsBuyHold.method_0();
    }

    internal void method_1(Bars bars_1)
    {
        Bars.Add(bars_1);
    }

    internal void method_2()
    {
        Results.method_6();
        ResultsLong.method_6();
        ResultsShort.method_6();
        ResultsBuyHold.method_6();
        Bars.Clear();
    }

    public SystemPerformance GenerateChildStrategyPerformance(CombinedStrategyInfo combinedStrategyInfo_0, TradingSystemExecutor tradingSystemExecutor_0)
    {
        new List<Position>();
        SystemPerformance systemPerformance = new SystemPerformance(Strategy);
        systemPerformance.PositionSize = combinedStrategyInfo_0.PositionSize;
        foreach (Position position in Results.Positions)
        {
            Bars bars = position.Bars;
            if (!systemPerformance.Bars.Contains(bars))
            {
                systemPerformance.Bars.Add(bars);
            }

            if (position.CSI == combinedStrategyInfo_0)
            {
                systemPerformance.Results.method_4(position);
                if (position.PositionType == PositionType.Long)
                {
                    systemPerformance.ResultsLong.method_4(position);
                }
                else
                {
                    systemPerformance.ResultsShort.method_4(position);
                }
            }
        }

        systemPerformance.RawTrades = new List<Position>();
        foreach (Position rawTrade in RawTrades)
        {
            if (rawTrade.CSI == combinedStrategyInfo_0)
            {
                systemPerformance.RawTrades.Add(rawTrade);
            }
        }

        systemPerformance.Results.BuildEquityCurve(Bars, tradingSystemExecutor_0, callbackToSizePositions: false, tradingSystemExecutor_0.PosSizer);
        systemPerformance.ResultsLong.BuildEquityCurve(Bars, tradingSystemExecutor_0, callbackToSizePositions: false, tradingSystemExecutor_0.PosSizer);
        systemPerformance.ResultsShort.BuildEquityCurve(Bars, tradingSystemExecutor_0, callbackToSizePositions: false, tradingSystemExecutor_0.PosSizer);
        if (BenchmarkSymbolbars == null)
        {
            foreach (Position position2 in ResultsBuyHold.Positions)
            {
                if (position2.StrategyID == combinedStrategyInfo_0.StrategyID.ToString())
                {
                    systemPerformance.ResultsBuyHold.method_4(position2);
                }
            }

            systemPerformance.ResultsBuyHold.BuildEquityCurve(Bars, tradingSystemExecutor_0, callbackToSizePositions: false, tradingSystemExecutor_0.PosSizer);
        }
        else
        {
            systemPerformance.ResultsBuyHold = ResultsBuyHold;
        }

        systemPerformance.BenchmarkSymbolbars = BenchmarkSymbolbars;
        return systemPerformance;
    }
}