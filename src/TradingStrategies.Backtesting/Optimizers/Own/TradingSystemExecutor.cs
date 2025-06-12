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
    private PosSizer posSizer_0;
    private Position position_0;
    private bool _rawProfitMode = true;
    private WealthScript _wealthScriptExecuting;
    private List<Alert> _masterAlerts = new();
    private IList<Bars> _barsSet;

    private static bool bool_0 = false;
    private static PositionSize positionSize_1 = new PositionSize(PosSizeMode.RawProfitShare, 1.0);
    private static bool _tnp = false;
    private static double _tnpAdjustment = 1.0;
    private static int int_1 = 0;
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

    public void Execute(Strategy strategy_1, WealthScript wealthScript_1, List<Bars> barsCollection, bool avoidClearingTradeList = false)
    {
        if (wealthScript_1 != null)
        {
            wealthScript_1.StrategyWindowID = StrategyWindowID;
        }

        Strategy = strategy_1;
        Performance.Strategy = strategy_1;
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

        _wealthScriptExecuting = wealthScript_1;
        PositionSize posSize = PosSize;
        _rawProfitMode = PosSize.RawProfitMode;
        if (!PosSize.RawProfitMode && PosSize.Mode != PosSizeMode.ScriptOverride)
        {
            PosSize = positionSize_1;
        }

        try
        {
            foreach (Bars item9 in barsCollection)
            {
                method_2(item9, wealthScript_1);
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

    //execute на одном инструменте (bars)
    private void method_2(Bars bars_1, WealthScript wealthScript_1)
    {
        CurrentPositions.Clear();
        CurrentAlerts.Clear();
        ActivePositions.Clear();

        try
        {
            _barsBeingProcessed = bars_1;
            bars_1.Block();

            if (bool_0 && Strategy.UsePreferredValues)
            {
                Strategy.LoadPreferredValues(bars_1.Symbol, wealthScript_1);
            }

            wealthScript_1.Execute(bars_1, null, _nativeExecutor, DataSet);
            wealthScript_1.RestoreScale();
        }
        finally
        {
            bars_1.Unblock();
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
        posSizer_0 = null;
        if (PosSize.Mode == PosSizeMode.SimuScript)
        {
            foreach (PosSizer posSizer in PosSizers)
            {
                if (!(posSizer.FriendlyName == PosSize.SimuScriptName))
                {
                    continue;
                }

                posSizer_0 = (PosSizer)Activator.CreateInstance(posSizer.GetType());
                if (PosSize.PosSizerConfig != "" && (PosSize.SimuScriptName == PosSize.PosSizerThatWasConfigured || PosSize.PosSizerThatWasConfigured == ""))
                {
                    try
                    {
                        posSizer_0.ApplyConfigString(PosSizer.ParseConfigString(PosSize.PosSizerConfig));
                    }
                    catch
                    {
                    }
                }

                break;
            }
        }

        //добавить бар во внутренний лист
        foreach (Bars item3 in _barsSet)
        {
            Performance.AddBars(item3);
        }

        Performance.Results.BuildEquityCurve(_barsSet, _nativeExecutor, callbackToSizePositions: true, posSizer_0);
        Performance.Results.Clear(avoidClearingEquity: true);

        foreach (Position item4 in MasterPositions)
        {
            if (item4.Shares > 0.0)
            {
                Performance.Results.AddPosition(item4);
            }
            else if (Strategy.StrategyType != StrategyType.CombinedStrategy)
            {
                Performance.Results.TradesNSF++;
            }
        }

        //заполняем long и short резалты
        /*
        foreach (Position item4 in MasterPositions)
        {
            if (item4.Shares > 0.0)
            {
                if (item4.PositionType == PositionType.Long)
                {
                    Performance.ResultsLong.AddPosition(item4);
                }
                else
                {
                    Performance.ResultsShort.AddPosition(item4);
                }
            }
            else if (Strategy.StrategyType != StrategyType.CombinedStrategy)
            {
                if (item4.PositionType == PositionType.Long)
                {
                    Performance.ResultsLong.TradesNSF++;
                }
                else
                {
                    Performance.ResultsShort.TradesNSF++;
                }
            }
        }

        foreach (Alert item5 in _masterAlerts)
        {
            SystemPerformance systemPerformance = Performance;
            if (item5.AlertType != 0 && item5.AlertType != TradeType.Short)
            {
                if (item5.Position != null && item5.Position.Shares > 0.0)
                {
                    systemPerformance.Results.AddAlert(item5);
                }
            }
            else
            {
                systemPerformance.Results.AddAlert(item5);
            }
        }

        Performance.ResultsLong.BuildEquityCurve(_barsSet, this, callbackToSizePositions: false, posSizer_0);
        Performance.ResultsShort.BuildEquityCurve(_barsSet, this, callbackToSizePositions: false, posSizer_0);
        */

        if (posSizer_0 != null)
        {
            Performance.Results.SetPosSizerPositions(posSizer_0); //выставить списки позиций резалта в позсайзер
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

    private int method_3(Bars bars_1, double double_10, int int_3 = 0)
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
            num = (int)(double_10 / bars_1.Close[int_3]);
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
    public double CalcPositionSize(Bars bars, int int_3, double basisPrice, PositionType positionType_0, double riskStopLevel, double equity, double overrideShareSize, double currentCash)
    {
        return method_4(bars, int_3, basisPrice, positionType_0, riskStopLevel, equity, overrideShareSize, currentCash, bool_21: false);
    }

    //calc pos size
    internal double method_4(Bars bars_1, int int_3, double double_10, PositionType positionType_0, double double_11, double double_12, double double_13, double double_14, bool bool_21)
    {
        double num = 0.0;
        if (bars_1.SymbolInfo.SecurityType == SecurityType.Future && bars_1.SymbolInfo.Margin <= 0.0)
        {
            throw new ArgumentException("Margin must be greater than zero");
        }

        switch (PosSize.Mode)
        {
            case PosSizeMode.RawProfitDollar:
                num = bars_1.SymbolInfo.SecurityType != SecurityType.Future ? bars_1.SymbolInfo.SecurityType != SecurityType.StockOption ? PosSize.RawProfitDollarSize / double_10 : PosSize.RawProfitDollarSize / (double_10 * 100.0) : PosSize.RawProfitDollarSize / bars_1.SymbolInfo.Margin;
                break;
            case PosSizeMode.RawProfitShare:
                num = PosSize.RawProfitShareSize;
                break;
            case PosSizeMode.Dollar:
                num = bars_1.SymbolInfo.SecurityType != SecurityType.Future ? bars_1.SymbolInfo.SecurityType != SecurityType.StockOption ? PosSize.DollarSize / double_10 : PosSize.DollarSize / (double_10 * 100.0) : PosSize.DollarSize / bars_1.SymbolInfo.Margin;
                break;
            case PosSizeMode.Share:
                num = PosSize.ShareSize;
                break;
            case PosSizeMode.PctEquity:
                {
                    double num2 = PosSize.PctSize / 100.0 * double_12;
                    num = bars_1.SymbolInfo.SecurityType != SecurityType.Future ? bars_1.SymbolInfo.SecurityType != SecurityType.StockOption ? num2 / double_10 : num2 / (double_10 * 100.0) : num2 / bars_1.SymbolInfo.Margin;
                    break;
                }
            case PosSizeMode.MaxRisk:
                {
                    if (double_10 == 0.0)
                    {
                        return 0.0;
                    }

                    if (RiskStopLevel <= 0.0)
                    {
                        _riskStopLevelNotSet = true;
                        return 0.0;
                    }

                    double num3 = PosSize.RiskSize / 100.0;
                    num3 *= double_12;
                    double num4 = positionType_0 == PositionType.Long ? double_10 - double_11 : double_11 - double_10;
                    try
                    {
                        num = num3 / (num4 * bars_1.SymbolInfo.PointValue);
                    }
                    catch
                    {
                        return 0.0;
                    }

                    if (bars_1.SymbolInfo.SecurityType == SecurityType.Future && bars_1.SymbolInfo.Margin > 0.0)
                    {
                        if (num > double_12 / bars_1.SymbolInfo.Margin)
                        {
                            num = double_12 / bars_1.SymbolInfo.Margin;
                        }
                    }
                    else if (bars_1.SymbolInfo.SecurityType == SecurityType.StockOption)
                    {
                        if (num > double_12 / (double_10 * 100.0))
                        {
                            num = double_12 / (double_10 * 100.0);
                        }
                    }
                    else if (num > double_12 / double_10)
                    {
                        num = double_12 / double_10;
                    }

                    break;
                }
            case PosSizeMode.SimuScript:
                if (posSizer_0 != null)
                {
                    try
                    {
                        num = posSizer_0.SizePosition(position_0, bars_1, int_3 - 1, double_10, positionType_0, double_11, double_12, double_14);
                    }
                    catch
                    {
                        num = 0.0;
                    }
                }
                else
                {
                    num = 0.0;
                }

                break;
            case PosSizeMode.ScriptOverride:
                num = double_13;
                PosSize.OverrideShareSize = num;
                break;
        }

        if (ReduceQtyBasedOnVolume && int_3 < bars_1.Count)
        {
            double num5 = RedcuceQtyPct / 100.0;
            double num6 = bars_1.Volume[int_3] * num5;
            if (num > num6)
            {
                num = num6;
            }
        }

        if (bars_1.SymbolInfo.SecurityType != SecurityType.MutualFund)
        {
            num = (int)num;
        }
        else
        {
            int num7 = (int)(num * 1000.0);
            num = num7 / 1000.0;
        }

        if ((bool_21 && _rawProfitMode || !bool_21) && RoundLots && bars_1.SymbolInfo.SecurityType == SecurityType.Equity && num > 0.0)
        {
            double a = num / 100.0;
            a = Math.Round(a) * 100.0;
            if (num < 100.0 && RoundLots50)
            {
                a = 100.0;
            }

            num = a;
        }

        return num;
    }

    public double CalcPositionSize(Bars bars, int int_3, double basisPrice, PositionType positionType_0, double riskStopLevel)
    {
        double currentEquity = Performance.Results.CurrentEquity;
        double overrideShareSize = 0.0;
        if (PosSize.Mode == PosSizeMode.ScriptOverride)
        {
            overrideShareSize = PosSize.OverrideShareSize;
        }

        return CalcPositionSize(bars, int_3, basisPrice, positionType_0, riskStopLevel, currentEquity, overrideShareSize, 0.0);
    }

    public double CalcPositionSize(Bars bars, int int_3, double basisPrice, PositionType positionType_0, double riskStopLevel, double equity)
    {
        double overrideShareSize = 0.0;
        if (PosSize.Mode == PosSizeMode.ScriptOverride)
        {
            overrideShareSize = PosSize.OverrideShareSize;
        }

        return CalcPositionSize(bars, int_3, basisPrice, positionType_0, riskStopLevel, equity, overrideShareSize, 0.0);
    }
    */
}
