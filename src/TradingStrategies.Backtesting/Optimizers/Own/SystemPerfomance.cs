using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Own;

public class SystemPerformanceOwn
{
    public Strategy Strategy { get; set; }
    public SystemResultsOwn Results { get; internal set; }
    public SystemResultsOwn ResultsLong { get; internal set; }
    public SystemResultsOwn ResultsShort { get; internal set; }
    public SystemResultsOwn ResultsBuyHold { get; internal set; }
    public BarScale Scale { get; internal set; }
    public int BarInterval { get; internal set; }
    public PositionSize PositionSize { get; internal set; } = new PositionSize();
    public bool IsIntraday => Scale is BarScale.Minute or BarScale.Second or BarScale.Tick;
    public List<Bars> Bars { get; } = new List<Bars>();
    public Bars BenchmarkSymbolbars { get; set; }
    public double CashReturnRate { get; set; }
    public List<Position> RawTrades { get; internal set; }

    public SystemPerformanceOwn(Strategy strategy)
    {
        Strategy = strategy;
        Results = new SystemResultsOwn(this);
        ResultsLong = new SystemResultsOwn(this);
        ResultsShort = new SystemResultsOwn(this);
        ResultsBuyHold = new SystemResultsOwn(this);
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
}