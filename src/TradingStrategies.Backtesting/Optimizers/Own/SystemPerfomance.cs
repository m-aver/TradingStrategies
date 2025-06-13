using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using TradingStrategies.Backtesting.Optimizers.Own;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Own;

public class SystemPerformanceOwn
{
    private EventHandler<EventArgs> eventHandler_0;

    private SystemResults systemResults_0;

    private SystemResults systemResults_1;

    private SystemResults systemResults_2;

    private SystemResults systemResults_3;

    private BarScale barScale_0;

    private int int_0;

    private PositionSize positionSize_0 = new PositionSize();

    private List<Bars> list_0 = new List<Bars>();

    private List<PlottedIndicator> list_1 = new List<PlottedIndicator>();

    private double double_0;

    private List<Position> list_2;

    private Bars bars_0;

    [CompilerGenerated]
    private Strategy strategy_0;

    public Strategy Strategy
    {
        [CompilerGenerated]
        get
        {
            return strategy_0;
        }
        [CompilerGenerated]
        set
        {
            strategy_0 = value;
        }
    }

    public SystemResults Results
    {
        get
        {
            return systemResults_0;
        }
        internal set
        {
            systemResults_0 = value;
        }
    }

    public SystemResults ResultsLong
    {
        get
        {
            return systemResults_1;
        }
        internal set
        {
            systemResults_1 = value;
        }
    }

    public SystemResults ResultsShort
    {
        get
        {
            return systemResults_2;
        }
        internal set
        {
            systemResults_2 = value;
        }
    }

    public SystemResults ResultsBuyHold
    {
        get
        {
            return systemResults_3;
        }
        internal set
        {
            systemResults_3 = value;
        }
    }

    public BarScale Scale
    {
        get
        {
            return barScale_0;
        }
        internal set
        {
            barScale_0 = value;
        }
    }

    public int BarInterval
    {
        get
        {
            return int_0;
        }
        internal set
        {
            int_0 = value;
        }
    }

    public PositionSize PositionSize
    {
        get
        {
            return positionSize_0;
        }
        internal set
        {
            positionSize_0 = value;
        }
    }

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

    public List<Bars> Bars => list_0;

    public Bars BenchmarkSymbolbars
    {
        get
        {
            return bars_0;
        }
        set
        {
            bars_0 = value;
        }
    }

    public double CashReturnRate
    {
        get
        {
            return double_0;
        }
        set
        {
            double_0 = value;
        }
    }

    public List<Position> RawTrades
    {
        get
        {
            return list_2;
        }
        internal set
        {
            list_2 = value;
        }
    }

    public event EventHandler<EventArgs> Signal
    {
        add
        {
            EventHandler<EventArgs> eventHandler = eventHandler_0;
            EventHandler<EventArgs> eventHandler2;
            do
            {
                eventHandler2 = eventHandler;
                EventHandler<EventArgs> value2 = (EventHandler<EventArgs>)Delegate.Combine(eventHandler2, value);
                eventHandler = Interlocked.CompareExchange(ref eventHandler_0, value2, eventHandler2);
            }
            while ((object)eventHandler != eventHandler2);
        }
        remove
        {
            EventHandler<EventArgs> eventHandler = eventHandler_0;
            EventHandler<EventArgs> eventHandler2;
            do
            {
                eventHandler2 = eventHandler;
                EventHandler<EventArgs> value2 = (EventHandler<EventArgs>)Delegate.Remove(eventHandler2, value);
                eventHandler = Interlocked.CompareExchange(ref eventHandler_0, value2, eventHandler2);
            }
            while ((object)eventHandler != eventHandler2);
        }
    }

    public void SignalEvent(string string_0)
    {
        if (eventHandler_0 != null)
        {
            eventHandler_0(string_0, null);
        }
    }

    public SystemPerformanceOwn(Strategy strategy)
    {
        Strategy = strategy;
        systemResults_0 = new SystemResults(this);
        systemResults_1 = new SystemResults(this);
        systemResults_2 = new SystemResults(this);
        systemResults_3 = new SystemResults(this);
    }

    internal void method_0()
    {
        systemResults_0.method_0();
        systemResults_1.method_0();
        systemResults_2.method_0();
        systemResults_3.method_0();
    }

    internal void method_1(Bars bars_1)
    {
        list_0.Add(bars_1);
    }

    internal void method_2()
    {
        systemResults_0.method_6();
        systemResults_1.method_6();
        systemResults_2.method_6();
        systemResults_3.method_6();
        list_0.Clear();
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

        systemPerformance.Results.BuildEquityCurve(list_0, tradingSystemExecutor_0, callbackToSizePositions: false, tradingSystemExecutor_0.PosSizer);
        systemPerformance.ResultsLong.BuildEquityCurve(list_0, tradingSystemExecutor_0, callbackToSizePositions: false, tradingSystemExecutor_0.PosSizer);
        systemPerformance.ResultsShort.BuildEquityCurve(list_0, tradingSystemExecutor_0, callbackToSizePositions: false, tradingSystemExecutor_0.PosSizer);
        if (BenchmarkSymbolbars == null)
        {
            foreach (Position position2 in ResultsBuyHold.Positions)
            {
                if (position2.StrategyID == combinedStrategyInfo_0.StrategyID.ToString())
                {
                    systemPerformance.ResultsBuyHold.method_4(position2);
                }
            }

            systemPerformance.ResultsBuyHold.BuildEquityCurve(list_0, tradingSystemExecutor_0, callbackToSizePositions: false, tradingSystemExecutor_0.PosSizer);
        }
        else
        {
            systemPerformance.ResultsBuyHold = ResultsBuyHold;
        }

        systemPerformance.BenchmarkSymbolbars = BenchmarkSymbolbars;
        return systemPerformance;
    }
}