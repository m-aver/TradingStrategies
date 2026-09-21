using TradingStrategies.Backtesting.Optimizers.Own;
using TradingStrategies.Backtesting.Utility;
using TradingStrategies.Utilities.InternalsProxy;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Utility;

//расширение встроенного OptimizationResult, предоставляющее больше данных для построения графиков

internal class OptimizationResultEx : OptimizationResult
{
    public SystemPerformance Performance { get; }

    public List<string> Names { get; set; }
    public List<string> ParameterNames { get; set; }

    //custom helping data
    public SystemPerformance PerformanceShifted { get; set; }
    public List<double> OriginalResults { get; set; }
    public DataSeriesPoint[] MonthReturnsEquity { get; set; }

    public OptimizationResultEx(SystemPerformance performance)
    {
        Performance = new(performance.Strategy);
        CopyPerformance(Performance, performance);
    }

    public OptimizationResultEx(SystemPerformance performance, string symbol) : base(symbol)
    {
        Performance = new(performance.Strategy);
        CopyPerformance(Performance, performance);
    }

    public OptimizationResultEx Copy()
    {
        var performance = new SystemPerformance(Performance.Strategy);
        CopyPerformance(performance, Performance);
        var performanceShifted = new SystemPerformance(Performance.Strategy);
        CopyPerformance(performanceShifted, PerformanceShifted);

        return new OptimizationResultEx(performance, Symbol)
        {
            Names = Names.ToList(),
            ParameterNames = ParameterNames.ToList(),
            PerformanceShifted = performanceShifted,
            ParameterValues = ParameterValues.ToList(),
            Results = Results.ToList(),
            AverageProfitAcrossTotalTimeSpan = AverageProfitAcrossTotalTimeSpan,
        };
    }

    //выполняет поверхностное копирование
    //SystemPerformance и SystemResults сразу после создания имеют свои пустые коллекции и датасерии, поэтому можно оперированить над ними, не боясь заафектить коллекции в исходных результатах
    //но объекты в этих коллекций (например Position) нельзя безбоязненно менять, т.к. это повлият на оба результата оптимизации
    internal static void CopyPerformance(SystemPerformance to, SystemPerformance from)
    {
        to.PositionSizeProxy = from.PositionSizeProxy;
        to.Strategy = from.Strategy;
        to.ScaleProxy = from.ScaleProxy;
        to.BarIntervalProxy = from.BarIntervalProxy;
        to.BenchmarkSymbolbars = from.BenchmarkSymbolbars;
        to.CashReturnRate = from.CashReturnRate;

        if (to.RawTrades is null)
            to.RawTradesProxy = new();
        to.RawTrades.Clear();
        to.RawTrades!.InsertRange(0, from.RawTrades);

        to.Bars.Clear();
        to.Bars.InsertRange(0, from.Bars);

        CopyResults(to.Results, from.Results);
        CopyResults(to.ResultsLong, from.ResultsLong);
        CopyResults(to.ResultsShort, from.ResultsShort);
    }

    internal static void CopyResults(SystemResults to, SystemResults from)
    {
        if (to.IsEmpty() == false)
        {
            to.EquityCurve.ClearFull();
            to.CashCurve.ClearFull();
            to.OpenPositionCount?.ClearFull();
            to.RawPositions.Clear();
            to.Alerts.Clear();
        }
        if (from.IsEmpty())
        {
            return;
        }

        to.EquityCurve.GetRawValues().InsertRange(0, from.EquityCurve.GetRawValues());
        to.EquityCurve.GetRawDates().InsertRange(0, from.EquityCurve.GetRawDates());

        to.CashCurve.GetRawValues().InsertRange(0, from.CashCurve.GetRawValues());
        to.CashCurve.GetRawDates().InsertRange(0, from.CashCurve.GetRawDates());

        if (from.OpenPositionCount is not null)
        {
            to.OpenPositionCount ??= new DataSeries(nameof(to.OpenPositionCount));
            to.OpenPositionCount.GetRawValues().InsertRange(0, from.OpenPositionCount.GetRawValues());
            to.OpenPositionCount.GetRawDates().InsertRange(0, from.OpenPositionCount.GetRawDates());
        }

        to.CashReturnProxy = from.CashReturn;
        to.MarginInterestProxy = from.MarginInterest;
        to.DividendsPaidProxy = from.DividendsPaid;
        to.TradesNSF = from.TradesNSF;
        to.TotalCommissionProxy = from.TotalCommission;

        to.RawPositions.InsertRange(0, from.RawPositions);
        to.Alerts.InsertRange(0, from.Alerts);
    }
}
