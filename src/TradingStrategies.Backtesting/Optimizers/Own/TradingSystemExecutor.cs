using System.Drawing;
using System.Windows.Forms;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Own;

public class TradingSystemExecutorOwn
{
    private EventHandler<StrategyEventArgs> _lookupStrategyEvent;
    private EventHandler<DataSourceLookupEventArgs> _lookupDataSourceEvent;
    private EventHandler<LoadSymbolEventArgs> _externalSymbolRequestedEvent;
    private EventHandler<LoadSymbolFromDataSetEventArgs> _externalSymbolFromDataSetRequestedEvent;
    private EventHandler<BarsEventArgs> _executionCompletedForSymbolEvent;
    private EventHandler<BarsEventArgs> _executionCompletedForChildStrategySymbolEvent;
    private EventHandler<WSExceptionEventArgs> _wealthScriptExceptionEvent;
    private EventHandler<EventArgs> _flushDebugWindowEvent;
    private EventHandler<EventArgs> _clearDebugWindowEvent;
    private EventHandler<DebugStringEventArgs> _printToStatusBarEvent;
    private EventHandler<ChartBitmapEventArgs> _chartBitmapRequestedEvent;
    private EventHandler<TrendLineEventArgs> _trendlineGetValueEvent;
    private EventHandler<StrategyParameterEventArgs> _setParameterValuesEvent;

    public int StrategyWindowID;
    private Bars _barsBeingProcessed;
    private List<string> _debugStrings = new();
    private double _overrideShareSize;
    private bool _riskStopLevelNotSet;
    private double _cashRate;
    private double _marginRate;
    private double _cashAdjustmentFactor;
    private double _marginAdjustmentFactor;
    private double _autoProfitLevel;
    private PosSizer posSizer_0;
    private Position position_0;
    private bool bool_16 = true;
    private WealthScript _wealthScriptExecuting;
    private List<Bars> list_1 = new();
    private List<Bars> list_2 = new();
    private List<Alert> _masterAlerts = new();
    private IList<Bars> ilist_0;
    private Dictionary<string, Bars> dictionary_0 = new Dictionary<string, Bars>();

    private static bool bool_0 = false;
    private static PositionSize positionSize_1 = new PositionSize(PosSizeMode.RawProfitShare, 1.0);
    private static bool _tnp = false;
    private static double _tnpAdjustment = 1.0;
    private static int int_1 = 0;
    public static List<PosSizer> PosSizers = null;

    private List<Bars> list_7 = new();
    private DateTime _wFOStartDate;
    private DateTime _wFOEndDate;
    private object _tag;

    public Strategy Strategy { get; set; }
    public bool isChildStrategy { get; set; }
    public SystemPerformance ParentSysPerf { get; set; }
    public string DividendItemName { get; set; }
    public ChartRenderer Renderer { get; set; }
    public BarsLoader BarsLoader { get; set; }
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
    public bool ApplyWFODateRange { get; set; }
    public bool ExceptionEvents { get; set; }
    public bool BuildEquityCurves { get; set; } = true;
    public DataSource DataSet { get; set; }
    public string StrategyName { get; set; } = "";
    public bool ReduceQtyBasedOnVolume { get; set; }
    public double RedcuceQtyPct { get; set; } = 10.0;
    public SystemPerformance Performance { get; set; }
    public IList<string> DebugStrings => _debugStrings;
    public bool WorstTradeSimulation { get; set; }
    public bool BenchmarkBuyAndHoldON { get; set; }
    public string BenchmarkSymbol { get; set; }
    public bool EnableSlippage { get; set; }
    public bool LimitOrderSlippage { get; set; }
    public double SlippageUnits { get; set; } = 1.0;
    public int SlippageTicks { get; set; } = 1;
    public bool LimitDaySimulation { get; set; }
    public bool RoundLots { get; set; }
    public bool RoundLots50 { get; set; }
    public bool IsStreaming { get; set; }

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

    public TradingSystemExecutorOwn()
    {
        Performance = new SystemPerformance(null);
    }

    public void Initialize()
    {
        Performance.PositionSize = PosSize;
        Performance.Results.CurrentCash = PosSize.StartingCapital;
        Performance.Results.CurrentEquity = PosSize.StartingCapital;
    }

    public void Execute(Strategy strategy_1, WealthScript wealthScript_1, Bars barsCharted, List<Bars> barsCollection, bool avoidClearingTradeList = false)
    {
        if (wealthScript_1 != null)
        {
            wealthScript_1.StrategyWindowID = StrategyWindowID;
        }

        List<Bars> list = new();
        foreach (Bars item in barsCollection)
        {
            list.Add(item);
        }

        Strategy = strategy_1;
        Performance.Strategy = strategy_1;
        list_7.Clear();
        RiskStopLevel = 0.0;
        _autoProfitLevel = 0.0;
        _debugStrings.Clear();
        _riskStopLevelNotSet = false;
        if (barsCollection == null || barsCollection.Count == 0)
        {
            return;
        }

        Clear(avoidClearingTradeList);
        ilist_0 = barsCollection;
        foreach (Bars item2 in barsCollection)
        {
            Performance.method_1(item2);
        }

        Performance.Scale = barsCollection[0].Scale;
        Performance.BarInterval = barsCollection[0].BarInterval;
        Performance.PositionSize = PosSize;
        _wealthScriptExecuting = wealthScript_1;
        PositionSize posSize = PosSize;
        bool_16 = PosSize.RawProfitMode;
        if (!PosSize.RawProfitMode && PosSize.Mode != PosSizeMode.ScriptOverride)
        {
            PosSize = positionSize_1;
        }

        if (Strategy.StrategyType == StrategyType.CombinedStrategy)
        {
            bool_0 = true;
            try
            {
                Performance.PositionSize = PosSize;
                List<string> list2 = new();
                foreach (CombinedStrategyInfo combinedStrategyChild in Strategy.CombinedStrategyChildren)
                {
                    Strategy strategy = null;
                    if (_lookupStrategyEvent != null)
                    {
                        StrategyEventArgs strategyEventArgs = new StrategyEventArgs(combinedStrategyChild.StrategyID.ToString());
                        _lookupStrategyEvent(this, strategyEventArgs);
                        strategy = strategyEventArgs.Strategy;
                    }

                    if (strategy == null)
                    {
                        continue;
                    }

                    TradingSystemExecutorOwn tradingSystemExecutor = new TradingSystemExecutorOwn();
                    tradingSystemExecutor.isChildStrategy = true;
                    tradingSystemExecutor.BarsLoader = BarsLoader;
                    tradingSystemExecutor.StrategyName = strategy.Name;
                    tradingSystemExecutor.FundamentalsLoader = FundamentalsLoader;
                    tradingSystemExecutor.ParentSysPerf = Performance;
                    List<Bars> list3 = new();
                    if (strategy.StrategyType == StrategyType.CombinedStrategy)
                    {
                        foreach (Bars item3 in list)
                        {
                            list3.Add(item3);
                        }
                    }
                    else if (combinedStrategyChild.UseDefaultDataSet)
                    {
                        list3.AddRange(list);
                        tradingSystemExecutor.DataSet = DataSet;
                    }
                    else if (_lookupDataSourceEvent != null)
                    {
                        DataSourceLookupEventArgs dataSourceLookupEventArgs = new DataSourceLookupEventArgs(combinedStrategyChild.DataSetName);
                        _lookupDataSourceEvent(this, dataSourceLookupEventArgs);
                        if (dataSourceLookupEventArgs.DataSource != null)
                        {
                            BarsLoader barsLoader = new BarsLoader();
                            barsLoader.DataHost = BarsLoader.DataHost;
                            barsLoader.BarDataScale = combinedStrategyChild.DataScale;
                            barsLoader.StartDate = BarsLoader.StartDate;
                            barsLoader.EndDate = BarsLoader.EndDate;
                            barsLoader.MaxBars = BarsLoader.MaxBars;
                            barsLoader.AutoCreateProvider = true;
                            if (combinedStrategyChild.Symbol != "")
                            {
                                Bars data = barsLoader.GetData(dataSourceLookupEventArgs.DataSource, combinedStrategyChild.Symbol);
                                if (data != null)
                                {
                                    list3.Add(data);
                                }
                            }
                            else
                            {
                                foreach (string symbol in dataSourceLookupEventArgs.DataSource.Symbols)
                                {
                                    Bars data2 = barsLoader.GetData(dataSourceLookupEventArgs.DataSource, symbol);
                                    if (data2 != null)
                                    {
                                        list3.Add(data2);
                                    }
                                }
                            }

                            tradingSystemExecutor.DataSet = dataSourceLookupEventArgs.DataSource;
                        }
                    }

                    foreach (Bars item4 in list3)
                    {
                        string text = item4.ToString();
                        bool flag = false;
                        foreach (Bars item5 in ilist_0)
                        {
                            if (item5.ToString() == text && item5.DataScale == item4.DataScale)
                            {
                                flag = true;
                                break;
                            }
                        }

                        if (!flag)
                        {
                            ilist_0.Add(item4);
                        }
                    }

                    WealthScript wealthScript = (WealthScript)strategy.Tag;
                    tradingSystemExecutor.ApplySettings(this);
                    tradingSystemExecutor.LookupDataSource += _lookupDataSourceEvent;
                    tradingSystemExecutor.LookupStrategy += _lookupStrategyEvent;
                    tradingSystemExecutor.PosSize = combinedStrategyChild.PositionSize;
                    double startingCapital = posSize.StartingCapital;
                    startingCapital = combinedStrategyChild.Allocation.Mode != PosSizeMode.PctEquity ? combinedStrategyChild.Allocation.DollarSize : startingCapital * (combinedStrategyChild.Allocation.PctSize / 100.0);
                    tradingSystemExecutor.PosSize.StartingCapital = startingCapital;
                    if (tradingSystemExecutor.PosSize.Mode == PosSizeMode.RawProfitDollar)
                    {
                        tradingSystemExecutor.PosSize.Mode = PosSizeMode.Dollar;
                        tradingSystemExecutor.PosSize.DollarSize = tradingSystemExecutor.PosSize.RawProfitDollarSize;
                    }

                    if (tradingSystemExecutor.PosSize.Mode == PosSizeMode.RawProfitShare)
                    {
                        tradingSystemExecutor.PosSize.Mode = PosSizeMode.Share;
                        tradingSystemExecutor.PosSize.ShareSize = tradingSystemExecutor.PosSize.RawProfitShareSize;
                    }

                    if (wealthScript != null)
                    {
                        for (int i = 0; i < combinedStrategyChild.ParameterValues.Count; i++)
                        {
                            wealthScript.Parameters[i].Value = combinedStrategyChild.ParameterValues[i];
                        }

                        strategy.UsePreferredValues = combinedStrategyChild.UsePreferredValues;
                    }

                    tradingSystemExecutor.ExecutionCompletedForSymbol += method_0;
                    tradingSystemExecutor.ExternalSymbolRequested += _externalSymbolRequestedEvent;
                    tradingSystemExecutor.ExternalSymbolFromDataSetRequested += _externalSymbolFromDataSetRequestedEvent;
                    tradingSystemExecutor.ExceptionEvents = true;
                    tradingSystemExecutor.WealthScriptException += method_1;
                    try
                    {
                        wealthScript.Renderer = null;
                        tradingSystemExecutor.Execute(strategy, wealthScript, null, list3);
                    }
                    catch (Exception ex)
                    {
                        tradingSystemExecutor.method_15("Exception in Combination Strategy Child: " + strategy.Name);
                        tradingSystemExecutor.method_15(ex.Message);
                    }

                    tradingSystemExecutor.ExecutionCompletedForSymbol -= method_0;
                    tradingSystemExecutor.ExternalSymbolRequested -= _externalSymbolRequestedEvent;
                    tradingSystemExecutor.ExternalSymbolFromDataSetRequested -= _externalSymbolFromDataSetRequestedEvent;
                    tradingSystemExecutor.WealthScriptException -= method_1;
                    list2.AddRange(tradingSystemExecutor.DebugStrings);
                    foreach (Position item6 in tradingSystemExecutor.MasterPositions)
                    {
                        item6.CombinedPriority = combinedStrategyChild.Priority;
                        item6.CSI = combinedStrategyChild;
                    }

                    foreach (Alert alert in tradingSystemExecutor.Performance.Results.Alerts)
                    {
                        alert.Account = combinedStrategyChild.AccountNumber;
                    }

                    tradingSystemExecutor.ApplyPositionSize();
                    foreach (Position position in tradingSystemExecutor.Performance.Results.Positions)
                    {
                        foreach (Bars item7 in ilist_0)
                        {
                            if (item7.ToString() == position.Bars.ToString())
                            {
                                position.method_0(item7);
                            }
                        }
                    }

                    MasterPositions.AddRange(tradingSystemExecutor.MasterPositions);
                    tradingSystemExecutor.Performance.RawTrades = tradingSystemExecutor.MasterPositions;
                    _masterAlerts.AddRange(tradingSystemExecutor.Performance.Results.Alerts);
                    if (!BenchmarkBuyAndHoldON)
                    {
                        foreach (Position position2 in tradingSystemExecutor.Performance.ResultsBuyHold.Positions)
                        {
                            Performance.ResultsBuyHold.method_4(position2);
                        }
                    }

                    tradingSystemExecutor.Performance.Results.TradesNSF = tradingSystemExecutor.MasterPositions.Count - tradingSystemExecutor.Performance.Results.Positions.Count;
                    Performance.Results.TradesNSF += tradingSystemExecutor.Performance.Results.TradesNSF;
                    int num = 0;
                    foreach (Position item8 in tradingSystemExecutor.MasterPositions)
                    {
                        if (item8.PositionType == PositionType.Long)
                        {
                            num++;
                        }
                    }

                    int num2 = tradingSystemExecutor.MasterPositions.Count - num;
                    tradingSystemExecutor.Performance.ResultsLong.TradesNSF = num - tradingSystemExecutor.Performance.ResultsLong.Positions.Count;
                    Performance.ResultsLong.TradesNSF += tradingSystemExecutor.Performance.ResultsLong.TradesNSF;
                    tradingSystemExecutor.Performance.ResultsShort.TradesNSF = num2 - tradingSystemExecutor.Performance.ResultsShort.Positions.Count;
                    Performance.ResultsShort.TradesNSF += tradingSystemExecutor.Performance.ResultsShort.TradesNSF;
                    tradingSystemExecutor.LookupDataSource -= _lookupDataSourceEvent;
                    tradingSystemExecutor.LookupStrategy -= _lookupStrategyEvent;
                }

                _debugStrings.Clear();
                _debugStrings.AddRange(list2);
            }
            finally
            {
                PosSize = posSize;
                list_7.Clear();
                bool_0 = false;
            }
        }
        else
        {
            try
            {
                foreach (Bars item9 in barsCollection)
                {
                    ChartRenderer chartRenderer_ = barsCharted == item9 ? Renderer : null;
                    method_2(item9, wealthScript_1, chartRenderer_);
                }
            }
            finally
            {
                PosSize = posSize;
                list_7.Clear();
            }
        }

        MasterPositions.Sort(this);
        if (BuildEquityCurves)
        {
            ApplyPositionSize();
        }
    }

    private void method_0(object sender, BarsEventArgs e)
    {
        if (_executionCompletedForChildStrategySymbolEvent != null)
        {
            _executionCompletedForChildStrategySymbolEvent(this, e);
        }
    }

    private void method_1(object sender, WSExceptionEventArgs e)
    {
        TradingSystemExecutorOwn tradingSystemExecutor = sender as TradingSystemExecutorOwn;
        tradingSystemExecutor.method_15("Exception in Combination Strategy Child: " + e.Strategy.Name);
        tradingSystemExecutor.method_15(e.Exception.Message);
    }

    private void method_2(Bars bars_1, WealthScript wealthScript_1, ChartRenderer chartRenderer_1)
    {
        CurrentPositions.Clear();
        CurrentAlerts.Clear();
        ActivePositions.Clear();
        if (ApplyWFODateRange)
        {
            foreach (Position masterPosition in MasterPositions)
            {
                if (masterPosition.Active && masterPosition.Symbol == bars_1.Symbol)
                {
                    CurrentPositions.Add(masterPosition);
                }
            }
        }

        method_11();
        try
        {
            _barsBeingProcessed = bars_1;
            bars_1.method_1();
            if (!bool_0)
            {
                if (_setParameterValuesEvent != null)
                {
                    _setParameterValuesEvent(this, new StrategyParameterEventArgs(wealthScript_1, bars_1.Symbol));
                }
            }
            else if (Strategy.UsePreferredValues)
            {
                Strategy.LoadPreferredValues(bars_1.Symbol, wealthScript_1);
            }

            wealthScript_1.method_4(bars_1, chartRenderer_1, this, DataSet);
            wealthScript_1.RestoreScale();
            bars_1.method_2();
        }
        catch (Exception ex)
        {
            bars_1.method_2();
            if (!ExceptionEvents)
            {
                throw ex;
            }

            if (_wealthScriptExceptionEvent != null)
            {
                WSExceptionEventArgs wSExceptionEventArgs = new WSExceptionEventArgs(ex);
                wSExceptionEventArgs.Strategy = Strategy;
                wSExceptionEventArgs.Symbol = bars_1.Symbol;
                _wealthScriptExceptionEvent(this, wSExceptionEventArgs);
            }
        }

        if (_executionCompletedForSymbolEvent != null)
        {
            _executionCompletedForSymbolEvent(this, new BarsEventArgs(bars_1));
        }
    }

    public void ApplyPositionSize()
    {
        TradingSystemExecutorOwn tradingSystemExecutor = new TradingSystemExecutorOwn();
        if (BenchmarkBuyAndHoldON)
        {
            if (DataSet != null)
            {
                tradingSystemExecutor.ApplySettings(this);
                Bars bars = DataSet.Provider.RequestData(DataSet, BenchmarkSymbol, DateTime.MinValue, DateTime.MaxValue, 0, includePartialBar: false);
                if (bars.Count == 0)
                {
                    bars = method_10(BenchmarkSymbol, bool_21: false);
                }

                bars.SymbolInfo.SecurityType = SecurityType.MutualFund;
                int count = ilist_0.Count;
                DateTime minValue = DateTime.MinValue;
                Bars bars2 = ilist_0[0];
                minValue = ilist_0[0].Date[0];
                foreach (Bars item in ilist_0)
                {
                    if (item.Count > 0 && item.Date[0] < minValue)
                    {
                        bars2 = item;
                        minValue = item.Date[0];
                    }
                }

                while (count-- > 0)
                {
                    if (ilist_0[count].Count > 0)
                    {
                        _ = ilist_0[count].Date[0] < minValue;
                    }
                }

                if (bars.Count != 0)
                {
                    bars = BarScaleConverter.Synchronize(bars, bars2);
                }

                tradingSystemExecutor.ilist_0 = new Bars[1] { bars };
                foreach (Bars item2 in tradingSystemExecutor.ilist_0)
                {
                    if (item2.Count > 0)
                    {
                        tradingSystemExecutor.Performance.method_1(item2);
                    }
                }

                Performance.BenchmarkSymbolbars = bars;
            }
        }
        else
        {
            Performance.BenchmarkSymbolbars = null;
        }

        if (ilist_0 == null)
        {
            return;
        }

        Performance.RawTrades = MasterPositions;
        Performance.PositionSize = PosSize;
        SystemResults systemResults = new SystemResults(Performance);
        if (!BenchmarkBuyAndHoldON)
        {
            foreach (Position position3 in Performance.ResultsBuyHold.Positions)
            {
                systemResults.method_4(position3);
            }
        }

        int tradesNSF = Performance.Results.TradesNSF;
        int tradesNSF2 = Performance.ResultsLong.TradesNSF;
        int tradesNSF3 = Performance.ResultsShort.TradesNSF;
        Performance.method_2();
        if (Strategy.StrategyType == StrategyType.CombinedStrategy)
        {
            Performance.Results.TradesNSF = tradesNSF;
            Performance.ResultsLong.TradesNSF = tradesNSF2;
            Performance.ResultsShort.TradesNSF = tradesNSF3;
        }

        if (Strategy.StrategyType == StrategyType.CombinedStrategy && !BenchmarkBuyAndHoldON)
        {
            foreach (Position position4 in systemResults.Positions)
            {
                Performance.ResultsBuyHold.method_4(position4);
            }
        }

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

        foreach (Bars item3 in ilist_0)
        {
            Performance.method_1(item3);
        }

        Performance.Results.BuildEquityCurve(ilist_0, this, callbackToSizePositions: true, posSizer_0);
        Performance.Results.method_7(bool_0: true);
        foreach (Position item4 in MasterPositions)
        {
            if (item4.Shares > 0.0)
            {
                Performance.Results.method_4(item4);
                if (item4.PositionType == PositionType.Long)
                {
                    Performance.ResultsLong.method_4(item4);
                }
                else
                {
                    Performance.ResultsShort.method_4(item4);
                }
            }
            else if (Strategy.StrategyType != StrategyType.CombinedStrategy)
            {
                Performance.Results.TradesNSF++;
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
                    systemPerformance.Results.method_5(item5);
                }
            }
            else
            {
                systemPerformance.Results.method_5(item5);
            }
        }

        bool reduceQtyBasedOnVolume = ReduceQtyBasedOnVolume;
        ReduceQtyBasedOnVolume = false;
        int num = 0;
        if (!BenchmarkBuyAndHoldON)
        {
            if (Strategy.StrategyType != StrategyType.CombinedStrategy)
            {
                foreach (Bars item6 in ilist_0)
                {
                    if (item6.Count > 1)
                    {
                        num++;
                    }
                }

                foreach (Bars item7 in ilist_0)
                {
                    if (item7.Count <= 1)
                    {
                        continue;
                    }

                    Position position = new Position(item7, PositionType.Long, Strategy.ID.ToString());
                    if (PosSize.RawProfitMode)
                    {
                        position.Shares = CalcPositionSize(item7, 0, item7.Close[0], PositionType.Long, 0.0, 0.0);
                    }
                    else
                    {
                        double double_ = PosSize.StartingCapital * PosSize.MarginFactor / num;
                        position.Shares = method_3(item7, double_);
                    }

                    if (position.Shares > 0.0)
                    {
                        position.EntryBar = 1;
                        position.EntryPrice = item7.Open[1];
                        position.BasisPrice = item7.Close[0];
                        Performance.ResultsBuyHold.method_4(position);
                        if (Commission != null && ApplyCommission)
                        {
                            position.EntryCommission = Commission.Calculate(TradeType.Buy, OrderType.Market, position.EntryPrice, position.Shares, item7);
                        }
                    }
                }
            }
        }
        else
        {
            foreach (Bars item8 in tradingSystemExecutor.ilist_0)
            {
                if (item8.Count > 1)
                {
                    num++;
                }
            }

            foreach (Bars item9 in tradingSystemExecutor.ilist_0)
            {
                if (item9.Count <= 1)
                {
                    continue;
                }

                Position position2 = new Position(item9, PositionType.Long, Strategy.ID.ToString());
                int i;
                for (i = 0; i < item9.Count && item9.Close[i] == 0.0; i++)
                {
                }

                if (PosSize.RawProfitMode)
                {
                    position2.Shares = CalcPositionSize(item9, i + 1, item9.Close[i], PositionType.Long, 0.0, 0.0);
                }
                else
                {
                    double double_2 = PosSize.StartingCapital * PosSize.MarginFactor / num;
                    position2.Shares = method_3(item9, double_2, i);
                }

                if (position2.Shares > 0.0)
                {
                    position2.EntryBar = i + 1;
                    position2.EntryPrice = item9.Open[i + 1];
                    position2.BasisPrice = item9.Close[i];
                    Performance.ResultsBuyHold.method_4(position2);
                    if (Commission != null && ApplyCommission)
                    {
                        position2.EntryCommission = Commission.Calculate(TradeType.Buy, OrderType.Market, position2.EntryPrice, position2.Shares, item9);
                    }
                }
            }
        }

        ReduceQtyBasedOnVolume = reduceQtyBasedOnVolume;
        Performance.ResultsLong.BuildEquityCurve(ilist_0, this, callbackToSizePositions: false, posSizer_0);
        Performance.ResultsShort.BuildEquityCurve(ilist_0, this, callbackToSizePositions: false, posSizer_0);
        Performance.ResultsBuyHold.method_8();
        if (!BenchmarkBuyAndHoldON)
        {
            Performance.ResultsBuyHold.BuildEquityCurve(ilist_0, this, callbackToSizePositions: false, null);
        }
        else
        {
            if (tradingSystemExecutor.ilist_0[0].Count == 0)
            {
                tradingSystemExecutor.ilist_0[0] = ilist_0[0];
            }

            Performance.ResultsBuyHold.BuildEquityCurve(tradingSystemExecutor.ilist_0, tradingSystemExecutor, callbackToSizePositions: false, null);
        }

        if (posSizer_0 != null)
        {
            Performance.Results.method_9(posSizer_0);
        }

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

        Performance.method_0();
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
        Performance.method_2();
        list_1.Clear();
        list_2.Clear();
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
        BenchmarkBuyAndHoldON = executor.BenchmarkBuyAndHoldON;
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

    public double CalcPositionSize(Bars bars, int int_3, double basisPrice, PositionType positionType_0, double riskStopLevel, double equity, double overrideShareSize, double currentCash)
    {
        return method_4(bars, int_3, basisPrice, positionType_0, riskStopLevel, equity, overrideShareSize, currentCash, bool_21: false);
    }

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

        if ((bool_21 && bool_16 || !bool_21) && RoundLots && bars_1.SymbolInfo.SecurityType == SecurityType.Equity && num > 0.0)
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

    internal Bars method_10(string string_3, bool bool_21)
    {
        if (_wealthScriptExecuting == null)
        {
            return new Bars(string_3, BarScale.Daily, 0);
        }

        Bars bars = _wealthScriptExecuting.Bars;
        Bars bars2 = null;
        if (string_3 == bars.Symbol)
        {
            return bars;
        }

        foreach (Bars item in list_7)
        {
            if (item.Symbol == string_3)
            {
                bars2 = item;
                break;
            }
        }

        if (bars2 == null && BarsLoader != null)
        {
            if (IsStreaming)
            {
                BarsLoader.OverrideOnDemand = true;
                BarsLoader.OverrideOnDemandValue = true;
            }

            BarsLoader.method_1(DataSet);
            DataSet.Provider.IsStreamingRequest = IsStreaming;
            BarDataScale barDataScale = BarsLoader.BarDataScale;
            BarsLoader.Scale = bars.Scale;
            BarsLoader.BarInterval = bars.BarInterval;
            BarsLoader.AutoConvertScale = false;
            bars2 = BarsLoader.method_3(string_3);
            if (bars2 != null && bars2.Count > 0)
            {
                list_7.Add(bars2);
            }

            BarsLoader.AutoConvertScale = true;
            BarsLoader.OverrideOnDemand = false;
            BarsLoader.BarDataScale = barDataScale;
        }

        if ((bars2 == null || bars2.Count == 0) && _externalSymbolRequestedEvent != null)
        {
            LoadSymbolEventArgs loadSymbolEventArgs = new LoadSymbolEventArgs(string_3, bars.Scale, bars.BarInterval);
            _externalSymbolRequestedEvent(this, loadSymbolEventArgs);
            bars2 = loadSymbolEventArgs.SymbolData;
            if (bars2 == null)
            {
                string text = "Invalid Benchmark Buy and Hold Symbol: " + string_3;
                MessageBox.Show(text);
                throw new ArgumentException(text);
            }

            if (bars2 != null && bars2.Count > 0)
            {
                list_7.Add(bars2);
            }
        }

        Bars bars3 = new Bars(bars2);
        bars3.Append(bars2);
        bars2 = bars3;
        if (bool_21 && bars2 != null && bars2.Count > 0)
        {
            bars2 = BarScaleConverter.Synchronize(bars2, bars);
        }

        if (bars2 != null)
        {
            bars2.method_1();
            if (bool_21)
            {
                list_1.Add(bars2);
            }
            else
            {
                list_2.Add(bars2);
            }
        }

        return bars2;
    }

    internal int method_11()
    {
        int result = list_1.Count + list_2.Count;
        list_1.Clear();
        list_2.Clear();
        return result;
    }

    internal void method_15(string string_3)
    {
        _debugStrings.Add(string_3);
    }
}
