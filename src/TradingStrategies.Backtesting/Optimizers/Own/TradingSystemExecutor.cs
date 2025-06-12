using WealthLab;
using TradingStrategies.Utilities.InternalsProxy;

namespace TradingStrategies.Backtesting.Optimizers.Own;

//WARN:
//оказывается я коментил участки кода с обработкой Long и Short резалтов в WealthLab.dll
//надо бы удостоверится что ничего важного не упускаю тут

public class TradingSystemExecutorOwn : IComparer<Position>
{
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
        get
        {
            return _cashRate;
        }
        set
        {
            _cashRate = value;
            _cashAdjustmentFactor = Math.Exp(Math.Log(1.0 + _cashRate / 100.0) / 365.25);
        }
    }

    public double MarginRate
    {
        get
        {
            return _marginRate;
        }
        set
        {
            _marginRate = value;
            _marginAdjustmentFactor = Math.Exp(Math.Log(1.0 + _marginRate / 100.0) / 365.25);
        }
    }

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

    public double OverrideShareSize
    {
        get
        {
            return _overrideShareSize;
        }
        set
        {
            _overrideShareSize = value;
            if (PosSize.Mode == PosSizeMode.ScriptOverride)
            {
                PosSize.OverrideShareSize = value;
            }
        }
    }

    public int PricingDecimalPlaces { get; set; }
    public bool NoDecimalRoundingForLimitStopPrice { get; set; }
    internal double RiskStopLevel { get; set; }

    public List<Position> MasterPositions { get; } = new();
    internal List<Position> CurrentPositions { get; } = new();
    internal List<Alert> CurrentAlerts { get; } = new();
    internal List<Position> ActivePositions { get; } = new();

    private TradingSystemExecutor _nativeExecutor;

    public TradingSystemExecutorOwn(TradingSystemExecutor nativeExecutor)
    {
        Performance = new SystemPerformance(null);
        _nativeExecutor = nativeExecutor;
    }

    public void Initialize()
    {
        Performance.PositionSizeProxy = PosSize;
        Performance.Results.CurrentCash = PosSize.StartingCapital;
        Performance.Results.CurrentEquity = PosSize.StartingCapital;
    }

    public void Execute(Strategy strategy, WealthScript wealthScript, List<Bars> barsCollection, bool avoidClearingTradeList = false)
    {
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
            else if (Strategy.StrategyType != StrategyType.CombinedStrategy)
            {
                Performance.Results.TradesNSF++;
            }
        }

        //заполняем long и short резалты
        /*
        foreach (Position position in MasterPositions)
        {
            if (position.Shares > 0.0)
            {
                if (position.PositionType == PositionType.Long)
                {
                    Performance.ResultsLong.AddPosition(position);
                }
                else
                {
                    Performance.ResultsShort.AddPosition(position);
                }
            }
            else if (Strategy.StrategyType != StrategyType.CombinedStrategy)
            {
                if (position.PositionType == PositionType.Long)
                {
                    Performance.ResultsLong.TradesNSF++;
                }
                else
                {
                    Performance.ResultsShort.TradesNSF++;
                }
            }
        }

        foreach (Alert alert in _masterAlerts)
        {
            SystemPerformance systemPerformance = Performance;
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

        Performance.ResultsLong.BuildEquityCurve(_barsSet, this, callbackToSizePositions: false, posSizer);
        Performance.ResultsShort.BuildEquityCurve(_barsSet, this, callbackToSizePositions: false, posSizer);
        */

        if (posSizer != null)
        {
            Performance.Results.SetPosSizerPositions(posSizer); //выставить списки позиций резалта в позсайзер
        }

        /*
        if (Strategy.StrategyType != StrategyType.CombinedStrategy)
        {
            foreach (Alert alert in Performance.Results.Alerts)
            {
                if (alert.AlertType != 0 && alert.AlertType != TradeType.Short)
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
        }
        */

        //считает MAE/MFE каждой позиции в каждом резалте
        //https://smart-lab.ru/blog/676929.php?ysclid=mbpkgf92rm589150064
        //довольно накладная хрень, надо делать опциональным
        //вообще эти штуки расчитываются автоматом при закрытии позиции, зачем их пересчитать еще раз хз, мб актуально только для открытых позиций

        Performance.CalculateMfeMae();
    }

    private int method_3(Bars bars_1, double double_10, int barNum = 0)
    {
        int num = 0;
        if (BarsLoader.FuturesMode)
        {
            SymbolInfo symbolInfo = bars_1.SymbolInfo;
            if (symbolInfo.SecurityType == SecurityType.Future && symbolInfo.Margin > 0.0)
            {
                num = (int)(double_10 / symbolInfo.Margin);
            }
        }

        if (num == 0)
        {
            num = (int)(double_10 / bars_1.Close[barNum]);
        }

        return num;
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
    }

    /*
    public double CalcPositionSize(Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel, double currentEquity, double overrideShareSize, double currentCash)
    {
        return method_4(bars, barNum, basisPrice, positionType, riskStopLevel, currentEquity, overrideShareSize, currentCash, bool_21: false);
    }

    //calc pos size
    internal double method_4(Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel, double currentEquity, double overrideShareSize, double currentCash, bool bool_21)
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

        if ((bool_21 && _rawProfitMode || !bool_21) && RoundLots && bars.SymbolInfo.SecurityType == SecurityType.Equity && resultSize > 0.0)
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

    public double CalcPositionSize(Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel, double equity)
    {
        double overrideShareSize = 0.0;
        if (PosSize.Mode == PosSizeMode.ScriptOverride)
        {
            overrideShareSize = PosSize.OverrideShareSize;
        }

        return CalcPositionSize(bars, barNum, basisPrice, positionType, riskStopLevel, equity, overrideShareSize, 0.0);
    }
    */
}
