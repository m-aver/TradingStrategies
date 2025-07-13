using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Own;

public class SystemPerformanceOwn
{
    public Strategy Strategy { get; set; }
    public SystemResultsOwn Results { get; internal set; }
    public SystemResultsOwn? ResultsLong { get; internal set; }
    public SystemResultsOwn? ResultsShort { get; internal set; }
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
        //Results = new SystemResultsOwn(this);
        //ResultsLong = new SystemResultsOwn(this);
        //ResultsShort = new SystemResultsOwn(this);
    }
    
    //method_0
    internal void CalculateMfeMae()
    {
        Results.CalculateMfeMae();
        ResultsLong?.CalculateMfeMae();
        ResultsShort?.CalculateMfeMae();
    }

    //method_1
    internal void AddBars(Bars bars)
    {
        Bars.Add(bars);
    }

    //method_2
    internal void Clear()
    {
        Results.FullClear();
        ResultsLong?.FullClear();
        ResultsShort?.FullClear();

        Bars.Clear();
    }
}