using WealthLab;
using TradingStrategies.Utilities.InternalsProxy;

namespace TradingStrategies.Backtesting.Optimizers.Own;

//WARN:
//оказывается я коментил участки кода с обработкой Long и Short резалтов в WealthLab.dll
//надо бы удостоверится что ничего важного не упускаю тут

//в этой реализации
//- убран код расчета BuyAndHold резалта
//- убран код обработки StrategyType.CombinedStrategy
//  (обработка CombinedStrategy использует статические переменные, поэтому ее пожалуй даже нельзя параллелить)
//- расчет Long и Short резалтов, MAE и MFE показателей сделан опциональным
//- убраны обработчики событий (смена параметров, ошибки WealthScript и т.п.) - в расчетах резалтов не участвуют
//по идее все возможные кейсы, кроме StrategyType.CombinedStrategy должны поддерживаться

public partial class TradingSystemExecutorOwn : IComparer<Position>
{
    /*
    public int StrategyWindowID;
    private Bars _barsBeingProcessed;
    private double _overrideShareSize;
    private bool _riskStopLevelNotSet;
    private double _cashRate;
    private double _marginRate;
    private double _cashAdjustmentFactor;
    private double _marginAdjustmentFactor;
    private double _autoProfitLevel;
    private PosSizer posSizer;
    private Position position_0;
    private bool _rawProfitMode = true;
    private WealthScript _wealthScriptExecuting;
    private List<Alert> _masterAlerts = new();
    private IList<Bars> _barsSet;

    private static PositionSize positionSize = new PositionSize(PosSizeMode.RawProfitShare, 1.0);
    public static List<PosSizer> PosSizers = null;

    public Strategy Strategy { get; set; }
    public string DividendItemName { get; set; }
    public FundamentalsLoader FundamentalsLoader { get; set; }
    public PositionSize PosSize { get; set; } = new PositionSize();
    public Commission Commission { get; set; }
    public bool ApplyCommission { get; set; }
    public bool ApplyInterest { get; set; }

    public double CashRate
    {
        get => _cashRate;
        set => _cashAdjustmentFactor = CalcAdjustmentFactor(_cashRate = value);
    }
    public double MarginRate
    {
        get => _marginRate; 
        set => _marginAdjustmentFactor = CalcAdjustmentFactor(_marginRate = value);
    }
    public double OverrideShareSize
    {
        get => _overrideShareSize;
        set
        {
            _overrideShareSize = value; 
            if (PosSize.Mode == PosSizeMode.ScriptOverride) 
                PosSize.OverrideShareSize = value;
        }
    }

    private static double CalcAdjustmentFactor(double rate) => Math.Exp(Math.Log(1.0 + rate / 100.0) / 365.25);

    public bool ApplyDividends { get; set; }
    public bool BuildEquityCurves { get; set; } = true;
    public DataSource DataSet { get; set; }
    public bool ReduceQtyBasedOnVolume { get; set; }
    public double RedcuceQtyPct { get; set; } = 10.0;
    public SystemPerformance Performance { get; set; }
    public bool WorstTradeSimulation { get; set; }
    public string BenchmarkSymbol { get; set; }
    public bool EnableSlippage { get; set; }
    public bool LimitOrderSlippage { get; set; }
    public double SlippageUnits { get; set; } = 1.0;
    public int SlippageTicks { get; set; } = 1;
    public bool LimitDaySimulation { get; set; }
    public bool RoundLots { get; set; }
    public bool RoundLots50 { get; set; }
    public int PricingDecimalPlaces { get; set; }
    public bool NoDecimalRoundingForLimitStopPrice { get; set; }
    internal double RiskStopLevel { get; set; }

    public List<Position> MasterPositions { get; } = new();
    internal List<Position> CurrentPositions { get; } = new();
    internal List<Alert> CurrentAlerts { get; } = new();
    internal List<Position> ActivePositions { get; } = new();
    //*/

    public bool CalcResultsLong { get; set; } = true;
    public bool CalcResultsShort { get; set; } = true;
    public bool CalcMfeMae { get; set; } = true;

    private SystemPerformanceOwn Performance { get; }
    public SystemPerformance PerfomanceNative { get; private set; }

    private readonly TradingSystemExecutor _nativeExecutor;

    public TradingSystemExecutorOwn(TradingSystemExecutor nativeExecutor)
    {
        _nativeExecutor = nativeExecutor;
        //Performance = new SystemPerformance(null);
        Performance = new SystemPerformanceOwn(null);
    }

    public void Initialize()
    {
        Performance.PositionSizeProxy = PosSize;
        Performance.Results.CurrentCash = PosSize.StartingCapital;
        Performance.Results.CurrentEquity = PosSize.StartingCapital;
    }

    //contract wrapper
    public void Execute(Strategy strategy, WealthScript wealthScript, Bars barsCharted, List<Bars> barsCollection, bool avoidClearingTradeList = false)
    {
        Execute(strategy, wealthScript, barsCollection, avoidClearingTradeList);
    }

    public void Execute(Strategy strategy, WealthScript wealthScript, List<Bars> barsCollection, bool avoidClearingTradeList = false)
    {
        if (strategy.StrategyType is StrategyType.CombinedStrategy)
        {
            throw new NotImplementedException($"{nameof(StrategyType.CombinedStrategy)} is not supported");
        }

        if (wealthScript != null)
        {
            wealthScript.StrategyWindowID = StrategyWindowID;
        }

        Strategy = strategy;
        Performance.Strategy = strategy;
        RiskStopLevel = 0.0;
        _autoProfitLevel = 0.0;
        _riskStopLevelNotSet = false;
        if (barsCollection == null || barsCollection.Count == 0)
        {
            return;
        }

        Clear(avoidClearingTradeList);
        _barsSet = barsCollection;
        foreach (Bars item2 in barsCollection)
        {
            Performance.AddBars(item2);
        }

        Performance.ScaleProxy = barsCollection[0].Scale;
        Performance.BarIntervalProxy = barsCollection[0].BarInterval;
        Performance.PositionSizeProxy = PosSize;

        _wealthScriptExecuting = wealthScript;
        PositionSize posSize = PosSize;
        _rawProfitMode = PosSize.RawProfitMode;
        if (!PosSize.RawProfitMode && PosSize.Mode != PosSizeMode.ScriptOverride)
        {
            PosSize = positionSize;
        }

        try
        {
            foreach (Bars bars in barsCollection)
            {
                ExecuteOneBars(bars, wealthScript);
            }
        }
        finally
        {
            PosSize = posSize;
        }

        MasterPositions.Sort(_nativeExecutor);

        if (BuildEquityCurves)
        {
            ApplyPositionSize();
        }

        FillNativePerfomance();
    }

    private void ExecuteOneBars(Bars bars, WealthScript wealthScript)
    {
        CurrentPositions.Clear();
        CurrentAlerts.Clear();
        ActivePositions.Clear();

        try
        {
            _barsBeingProcessed = bars;
            bars.Block();

            wealthScript.Execute(bars, null, _nativeExecutor, DataSet);
            wealthScript.RestoreScale();
        }
        finally
        {
            bars.Unblock();
        }
    }

    public void ApplyPositionSize()
    {
        Performance.BenchmarkSymbolbars = null;

        if (_barsSet == null)
        {
            return;
        }

        Performance.RawTradesProxy = MasterPositions;
        Performance.PositionSizeProxy = PosSize;

        Performance.Clear();

        if (ApplyInterest)
        {
            Performance.CashReturnRate = CashRate;
        }
        else
        {
            Performance.CashReturnRate = 0.0;
        }

        _riskStopLevelNotSet = false;
        posSizer = null;
        if (PosSize.Mode == PosSizeMode.SimuScript)
        {
            foreach (PosSizer posSizer in PosSizers)
            {
                if (!(posSizer.FriendlyName == PosSize.SimuScriptName))
                {
                    continue;
                }

                this.posSizer = (PosSizer)Activator.CreateInstance(posSizer.GetType());
                if (PosSize.PosSizerConfig != "" && (PosSize.SimuScriptName == PosSize.PosSizerThatWasConfigured || PosSize.PosSizerThatWasConfigured == ""))
                {
                    try
                    {
                        this.posSizer.ApplyConfigString(PosSizer.ParseConfigString(PosSize.PosSizerConfig));
                    }
                    catch
                    {
                    }
                }

                break;
            }
        }

        //добавить бар во внутренний лист
        foreach (Bars bars in _barsSet)
        {
            Performance.AddBars(bars);
        }

        Performance.Results.BuildEquityCurve(_barsSet, _nativeExecutor, callbackToSizePositions: true, posSizer);
        Performance.Results.Clear(avoidClearingEquity: true);

        foreach (Position position in MasterPositions)
        {
            if (position.Shares > 0.0)
            {
                Performance.Results.AddPosition(position);
            }
            else
            {
                Performance.Results.TradesNSF++;
            }
        }

        foreach (Alert alert in _masterAlerts)
        {
            SystemPerformanceOwn systemPerformance = Performance;
            if (alert.AlertType != 0 && alert.AlertType != TradeType.Short)
            {
                if (alert.Position != null && alert.Position.Shares > 0.0)
                {
                    systemPerformance.Results.AddAlert(alert);
                }
            }
            else
            {
                systemPerformance.Results.AddAlert(alert);
            }
        }

        //заполняем long и short резалты
        if (CalcResultsLong || CalcResultsShort)
        {
            foreach (Position position in MasterPositions)
            {
                if (position.Shares > 0.0)
                {
                    if (CalcResultsLong && position.PositionType == PositionType.Long)
                    {
                        Performance.ResultsLong.AddPosition(position);
                    }
                    if (CalcResultsShort && position.PositionType == PositionType.Short)
                    {
                        Performance.ResultsShort.AddPosition(position);
                    }
                }
                else
                {
                    if (CalcResultsLong && position.PositionType == PositionType.Long)
                    {
                        Performance.ResultsLong.TradesNSF++;
                    }
                    if (CalcResultsShort && position.PositionType == PositionType.Short)
                    {
                        Performance.ResultsShort.TradesNSF++;
                    }
                }
            }
        }

        if (CalcResultsLong)
        {
            Performance.ResultsLong.BuildEquityCurve(_barsSet, _nativeExecutor, callbackToSizePositions: false, posSizer);
        }
        if (CalcResultsShort)
        {
            Performance.ResultsShort.BuildEquityCurve(_barsSet, _nativeExecutor, callbackToSizePositions: false, posSizer);
        }

        if (posSizer != null)
        {
            Performance.Results.SetPosSizerPositions(posSizer); //выставить списки позиций резалта в позсайзер
        }

        foreach (Alert alert in Performance.Results.Alerts)
        {
            if (alert.AlertType != TradeType.Buy && alert.AlertType != TradeType.Short)
            {
                if (alert.Position != null)
                {
                    alert.Shares = alert.Position.Shares;
                }
            }
            else if (PosSize.Mode != PosSizeMode.ScriptOverride)
            {
                alert.Shares = CalcPositionSize(alert.Bars, alert.Bars.Count, alert.BasisPrice, alert.PositionType, alert.RiskStopLevel);
            }
        }

        //считает MAE/MFE каждой позиции в каждом резалте
        //https://smart-lab.ru/blog/676929.php?ysclid=mbpkgf92rm589150064
        //довольно накладная хрень, надо делать опциональным
        //вообще эти штуки расчитываются автоматом при закрытии позиции, зачем их пересчитать еще раз хз, мб актуально только для открытых позиций

        if (CalcMfeMae)
        {
            Performance.CalculateMfeMae();
        }
    }

    private void FillNativePerfomance()
    {
        var native = new SystemPerformance(null);

        native.PositionSizeProxy = Performance.PositionSizeProxy;
        native.Strategy = Performance.Strategy;
        native.ScaleProxy = Performance.ScaleProxy;
        native.BarIntervalProxy = Performance.BarIntervalProxy;
        native.PositionSizeProxy = Performance.PositionSizeProxy;
        native.BenchmarkSymbolbars = Performance.BenchmarkSymbolbars;
        native.RawTradesProxy = Performance.RawTradesProxy;
        native.PositionSizeProxy = Performance.PositionSizeProxy;
        native.CashReturnRate = Performance.CashReturnRate;

        foreach (var bar in Performance.Bars)
        {
            native.AddBars(bar);
        }

        FillNativeResults(native.Results, Performance.Results);
        if (CalcResultsLong)
            FillNativeResults(native.ResultsLong, Performance.ResultsLong);
        if (CalcResultsShort)
            FillNativeResults(native.ResultsShort, Performance.ResultsShort);

        PerfomanceNative = native;
    }

    private static void FillNativeResults(SystemResults native, SystemResultsOwn own)
    {
        native.EquityCurveProxy = own.EquityCurve;
        native.CashCurveProxy = own.CashCurve;
        native.CashReturnProxy = own.CashReturn;
        native.MarginInterestProxy = own.MarginInterest;
        native.DividendsPaidProxy = own.DividendsPaid;
        native.TradesNSF = own.TradesNSF;
        native.OpenPositionCount = own.OpenPositionCount;

        native.TotalCommissionProxy = own.TotalCommission;

        foreach (var position in own.Positions)
        {
            native.AddPosition(position);
        }
        foreach (var alert in own.Alerts)
        {
            native.AddAlert(alert);
        }
    }

    public void Clear(bool avoidClearingTradeList = false)
    {
        if (!avoidClearingTradeList)
        {
            MasterPositions.Clear();
        }

        _masterAlerts.Clear();
        Performance.Clear();
    }

    public int Compare(Position position_1, Position position_2)
    {
        if (position_1.EntryDate == position_2.EntryDate)
        {
            if (position_1.CombinedPriority == position_2.CombinedPriority)
            {
                if (!WorstTradeSimulation)
                {
                    return -position_1.Priority.CompareTo(position_2.Priority);
                }
                return position_1.NetProfit.CompareTo(position_2.NetProfit);
            }
            return position_1.CombinedPriority.CompareTo(position_2.CombinedPriority);
        }
        _ = position_1.EntryDate;
        _ = position_2.EntryDate;
        return position_1.EntryDate.CompareTo(position_2.EntryDate);
    }

    public void ApplySettings(TradingSystemExecutorOwn executor)
    {
        ApplyCommission = executor.ApplyCommission;
        Commission = executor.Commission;
        PosSize = executor.PosSize;
        EnableSlippage = executor.EnableSlippage;
        LimitOrderSlippage = executor.LimitOrderSlippage;
        SlippageUnits = executor.SlippageUnits;
        SlippageTicks = executor.SlippageTicks;
        RoundLots = executor.RoundLots;
        RoundLots50 = executor.RoundLots50;
        LimitOrderSlippage = executor.LimitOrderSlippage;
        ApplyInterest = executor.ApplyInterest;
        CashRate = executor.CashRate;
        MarginRate = executor.MarginRate;
        ApplyDividends = executor.ApplyDividends;
        ReduceQtyBasedOnVolume = executor.ReduceQtyBasedOnVolume;
        RedcuceQtyPct = executor.RedcuceQtyPct;
        LimitDaySimulation = executor.LimitDaySimulation;
        WorstTradeSimulation = executor.WorstTradeSimulation;
        BenchmarkSymbol = executor.BenchmarkSymbol;
        OverrideShareSize = executor.OverrideShareSize;
        if (FundamentalsLoader == null)
        {
            FundamentalsLoader = executor.FundamentalsLoader;
        }

        NoDecimalRoundingForLimitStopPrice = executor.NoDecimalRoundingForLimitStopPrice;
        PricingDecimalPlaces = executor.PricingDecimalPlaces;
        DividendItemName = executor.DividendItemName;

        CalcResultsLong = executor.CalcResultsLong;
        CalcResultsShort = executor.CalcResultsShort;
        CalcMfeMae = executor.CalcMfeMae;
    }

    public double CalcPositionSize(Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel, double currentEquity, double overrideShareSize, double currentCash)
    {
        return method_4(bars, barNum, basisPrice, positionType, riskStopLevel, currentEquity, overrideShareSize, currentCash, comingFromWealthScript: false);
    }

    //calc pos size
    internal double method_4(Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel, double currentEquity, double overrideShareSize, double currentCash, bool comingFromWealthScript)
    {
        double resultSize = 0.0;
        if (bars.SymbolInfo.SecurityType == SecurityType.Future && bars.SymbolInfo.Margin <= 0.0)
        {
            throw new ArgumentException("Margin must be greater than zero");
        }

        switch (PosSize.Mode)
        {
            case PosSizeMode.RawProfitDollar:
                resultSize = bars.SymbolInfo.SecurityType != SecurityType.Future ? bars.SymbolInfo.SecurityType != SecurityType.StockOption ? PosSize.RawProfitDollarSize / basisPrice : PosSize.RawProfitDollarSize / (basisPrice * 100.0) : PosSize.RawProfitDollarSize / bars.SymbolInfo.Margin;
                break;
            case PosSizeMode.RawProfitShare:
                resultSize = PosSize.RawProfitShareSize;
                break;
            case PosSizeMode.Dollar:
                resultSize = bars.SymbolInfo.SecurityType != SecurityType.Future ? bars.SymbolInfo.SecurityType != SecurityType.StockOption ? PosSize.DollarSize / basisPrice : PosSize.DollarSize / (basisPrice * 100.0) : PosSize.DollarSize / bars.SymbolInfo.Margin;
                break;
            case PosSizeMode.Share:
                resultSize = PosSize.ShareSize;
                break;
            case PosSizeMode.PctEquity:
                {
                    double num2 = PosSize.PctSize / 100.0 * currentEquity;
                    resultSize = bars.SymbolInfo.SecurityType != SecurityType.Future ? bars.SymbolInfo.SecurityType != SecurityType.StockOption ? num2 / basisPrice : num2 / (basisPrice * 100.0) : num2 / bars.SymbolInfo.Margin;
                    break;
                }
            case PosSizeMode.MaxRisk:
                {
                    if (basisPrice == 0.0)
                    {
                        return 0.0;
                    }

                    if (RiskStopLevel <= 0.0)
                    {
                        _riskStopLevelNotSet = true;
                        return 0.0;
                    }

                    double num3 = PosSize.RiskSize / 100.0;
                    num3 *= currentEquity;
                    double num4 = positionType == PositionType.Long ? basisPrice - riskStopLevel : riskStopLevel - basisPrice;
                    try
                    {
                        resultSize = num3 / (num4 * bars.SymbolInfo.PointValue);
                    }
                    catch
                    {
                        return 0.0;
                    }

                    if (bars.SymbolInfo.SecurityType == SecurityType.Future && bars.SymbolInfo.Margin > 0.0)
                    {
                        if (resultSize > currentEquity / bars.SymbolInfo.Margin)
                        {
                            resultSize = currentEquity / bars.SymbolInfo.Margin;
                        }
                    }
                    else if (bars.SymbolInfo.SecurityType == SecurityType.StockOption)
                    {
                        if (resultSize > currentEquity / (basisPrice * 100.0))
                        {
                            resultSize = currentEquity / (basisPrice * 100.0);
                        }
                    }
                    else if (resultSize > currentEquity / basisPrice)
                    {
                        resultSize = currentEquity / basisPrice;
                    }

                    break;
                }
            case PosSizeMode.SimuScript:
                if (posSizer != null)
                {
                    try
                    {
                        resultSize = posSizer.SizePosition(position_0, bars, barNum - 1, basisPrice, positionType, riskStopLevel, currentEquity, currentCash);
                    }
                    catch
                    {
                        resultSize = 0.0;
                    }
                }
                else
                {
                    resultSize = 0.0;
                }

                break;
            case PosSizeMode.ScriptOverride:
                resultSize = overrideShareSize;
                PosSize.OverrideShareSize = resultSize;
                break;
        }

        if (ReduceQtyBasedOnVolume && barNum < bars.Count)
        {
            double num5 = RedcuceQtyPct / 100.0;
            double num6 = bars.Volume[barNum] * num5;
            if (resultSize > num6)
            {
                resultSize = num6;
            }
        }

        if (bars.SymbolInfo.SecurityType != SecurityType.MutualFund)
        {
            resultSize = (int)resultSize;
        }
        else
        {
            int num7 = (int)(resultSize * 1000.0);
            resultSize = num7 / 1000.0;
        }

        if ((comingFromWealthScript && _rawProfitMode || !comingFromWealthScript) &&
            RoundLots &&
            bars.SymbolInfo.SecurityType == SecurityType.Equity &&
            resultSize > 0.0)
        {
            double a = resultSize / 100.0;
            a = Math.Round(a) * 100.0;
            if (resultSize < 100.0 && RoundLots50)
            {
                a = 100.0;
            }

            resultSize = a;
        }

        return resultSize;
    }

    public double CalcPositionSize(Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel)
    {
        double currentEquity = Performance.Results.CurrentEquity;
        double overrideShareSize = 0.0;
        if (PosSize.Mode == PosSizeMode.ScriptOverride)
        {
            overrideShareSize = PosSize.OverrideShareSize;
        }

        return CalcPositionSize(bars, barNum, basisPrice, positionType, riskStopLevel, currentEquity, overrideShareSize, 0.0);
    }

    public double CalcPositionSize(Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel, bool comingFromWealthScript)
    {
        double currentEquity = Performance.Results.CurrentEquity;
        double double_ = 0.0;
        if (PosSize.Mode == PosSizeMode.ScriptOverride)
        {
            double_ = PosSize.OverrideShareSize;
        }

        return method_4(bars, barNum, basisPrice, positionType, riskStopLevel, currentEquity, double_, 0.0, comingFromWealthScript);
    }

    public double CalcPositionSize(Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel, double equity)
    {
        double overrideShareSize = 0.0;
        if (PosSize.Mode == PosSizeMode.ScriptOverride)
        {
            overrideShareSize = PosSize.OverrideShareSize;
        }

        return CalcPositionSize(bars, barNum, basisPrice, positionType, riskStopLevel, equity, overrideShareSize, 0.0);
    }

    public double CalcPositionSize(Position position, Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel, bool useOverRide, double overrideShareSize, double thisBarCash)
    {
        if (Strategy != null && Strategy.StrategyType == StrategyType.CombinedStrategy)
        {
            return position.Shares;
        }

        position_0 = position;
        double currentEquity = Performance.Results.CurrentEquity;
        double num = CalcPositionSize(bars, barNum, basisPrice, positionType, riskStopLevel, currentEquity, overrideShareSize, thisBarCash);
        position_0 = null;

        return num;
    }
}
