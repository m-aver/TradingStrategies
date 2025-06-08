using System.Drawing;
using System.Runtime.CompilerServices;
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

    private static bool bool_0 = false;

    private ChartRenderer _renderer;

    private BarsLoader _barsLoader;

    private FundamentalsLoader _fundamentalsLoader;

    private bool _buildEquityCurves = true;

    private bool _exceptionEvents;

    private Bars _barsBeingProcessed;

    private Commission _commission;

    private bool _enableSlippage;

    private bool _worstTradeSimulation;

    private string _benchmarkSymbol;

    private bool _benchmarkBuyAndHoldOn;

    private bool _limitOrderSlippage;

    private double _slippageUnits = 1.0;

    private int _slippageTicks = 1;

    private bool _roundLots;

    private bool _roundLots50;

    private bool _applyCommission;

    private List<string> _debugStrings = new List<string>();

    private bool _isStreaming;

    private double _overrideShareSize;

    private DataSource _dataSet;

    private double _riskStopLevel;

    private bool _riskStopLevelNotSet;

    public List<Position> _activePositions = new List<Position>();

    private bool _limitDaySimulation;

    private double _cashRate;

    private double _marginRate;

    private bool _applyInterest;

    private bool _applyDividends;

    private double _cashAdjustmentFactor;

    private double _marginAdjustmentFactor;

    private string _strategyName = "";

    private bool _reduceQtyBasedOnVolume;

    private double _redcuceQtyPct = 10.0;

    private double _autoProfitLevel;

    private PosSizer posSizer_0;

    private Position position_0;

    private bool bool_16 = true;

    private WealthScript _wealthScriptExecuting;

    private List<Bars> list_1 = new List<Bars>();

    private List<Bars> list_2 = new List<Bars>();

    private PositionSize _posSize = new PositionSize();

    private List<Position> _masterPositions = new List<Position>();

    private List<Alert> _masterAlerts = new List<Alert>();

    private List<Position> _currentPositions = new List<Position>();

    private List<Alert> _currentAlerts = new List<Alert>();

    private SystemPerformance systemPerformance_0;

    private IList<Bars> ilist_0;

    private static PositionSize positionSize_1 = new PositionSize(PosSizeMode.RawProfitShare, 1.0);

    private Dictionary<string, Bars> dictionary_0 = new Dictionary<string, Bars>();

    private static bool _tnp = false;

    private static double _tnpAdjustment = 1.0;

    private static int int_1 = 0;

    public static List<PosSizer> PosSizers = null;

    private List<Bars> list_7 = new List<Bars>();

    private Strategy _strategy;

    private bool _isChildStrategy;

    private SystemPerformance _parentSysPerf;

    private string _dividendItemName;

    private bool _applyWFODateRange;

    private DateTime _wFOStartDate;

    private DateTime _wFOEndDate;

    private int _pricingDecimalPlaces;

    private bool _noDecimalRoundingForLimitStopPrice;

    private object _tag;

    public Strategy Strategy
    {
        get
        {
            return _strategy;
        }
        set
        {
            _strategy = value;
        }
    }

    public bool isChildStrategy
    {
        get
        {
            return _isChildStrategy;
        }
        set
        {
            _isChildStrategy = value;
        }
    }

    public SystemPerformance ParentSysPerf
    {
        get
        {
            return _parentSysPerf;
        }
        set
        {
            _parentSysPerf = value;
        }
    }

    public string DividendItemName
    {
        get
        {
            return _dividendItemName;
        }
        set
        {
            _dividendItemName = value;
        }
    }

    public ChartRenderer Renderer
    {
        get
        {
            return _renderer;
        }
        set
        {
            _renderer = value;
        }
    }

    public BarsLoader BarsLoader
    {
        get
        {
            return _barsLoader;
        }
        set
        {
            _barsLoader = value;
        }
    }

    public FundamentalsLoader FundamentalsLoader
    {
        get
        {
            return _fundamentalsLoader;
        }
        set
        {
            _fundamentalsLoader = value;
        }
    }

    public PositionSize PosSize
    {
        get
        {
            return _posSize;
        }
        set
        {
            _posSize = value;
        }
    }

    public Commission Commission
    {
        get
        {
            return _commission;
        }
        set
        {
            _commission = value;
        }
    }

    public bool ApplyCommission
    {
        get
        {
            return _applyCommission;
        }
        set
        {
            _applyCommission = value;
        }
    }

    public bool ApplyInterest
    {
        get
        {
            return _applyInterest;
        }
        set
        {
            _applyInterest = value;
        }
    }

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

    public bool ApplyDividends
    {
        get
        {
            return _applyDividends;
        }
        set
        {
        }
    }

    public bool ApplyWFODateRange
    {
        get
        {
            return _applyWFODateRange;
        }
        set
        {
            _applyWFODateRange = value;
        }
    }

    public bool ExceptionEvents
    {
        get
        {
            return _exceptionEvents;
        }
        set
        {
            _exceptionEvents = value;
        }
    }

    public bool BuildEquityCurves
    {
        get
        {
            return _buildEquityCurves;
        }
        set
        {
        }
    }

    public DataSource DataSet
    {
        get
        {
            return _dataSet;
        }
        set
        {
            _dataSet = value;
        }
    }

    public string StrategyName
    {
        get
        {
            return _strategyName;
        }
        set
        {
            _strategyName = value;
        }
    }

    public bool ReduceQtyBasedOnVolume
    {
        get
        {
            return _reduceQtyBasedOnVolume;
        }
        set
        {
            _reduceQtyBasedOnVolume = value;
        }
    }

    public double RedcuceQtyPct
    {
        get
        {
            return _redcuceQtyPct;
        }
        set
        {
        }
    }

    public SystemPerformance Performance
    {
        get
        {
            return systemPerformance_0;
        }
        set
        {
            systemPerformance_0 = value;
        }
    }

    public IList<string> DebugStrings => _debugStrings;

    public bool WorstTradeSimulation
    {
        get
        {
            return _worstTradeSimulation;
        }
        set
        {
            _worstTradeSimulation = value;
        }
    }

    public bool BenchmarkBuyAndHoldON
    {
        get
        {
            return _benchmarkBuyAndHoldOn;
        }
        set
        {
            _benchmarkBuyAndHoldOn = value;
        }
    }

    public string BenchmarkSymbol
    {
        get
        {
            return _benchmarkSymbol;
        }
        set
        {
            _benchmarkSymbol = value;
        }
    }

    public bool EnableSlippage
    {
        get
        {
            return _enableSlippage;
        }
        set
        {
            _enableSlippage = value;
        }
    }

    public bool LimitOrderSlippage
    {
        get
        {
            return _limitOrderSlippage;
        }
        set
        {
            _limitOrderSlippage = value;
        }
    }

    public double SlippageUnits
    {
        get
        {
            return _slippageUnits;
        }
        set
        {
            _slippageUnits = value;
        }
    }

    public int SlippageTicks
    {
        get
        {
            return _slippageTicks;
        }
        set
        {
            _slippageTicks = value;
        }
    }

    public bool LimitDaySimulation
    {
        get
        {
            return _limitDaySimulation;
        }
        set
        {
            _limitDaySimulation = value;
        }
    }

    public bool RoundLots
    {
        get
        {
            return _roundLots;
        }
        set
        {
            _roundLots = value;
        }
    }

    public bool RoundLots50
    {
        get
        {
            return _roundLots50;
        }
        set
        {
            _roundLots50 = value;
        }
    }

    public bool IsStreaming
    {
        get
        {
            return _isStreaming;
        }
        set
        {
            _isStreaming = value;
        }
    }

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

    public int PricingDecimalPlaces
    {
        get
        {
            return _pricingDecimalPlaces;
        }
        set
        {
            _pricingDecimalPlaces = value;
        }
    }

    public bool NoDecimalRoundingForLimitStopPrice
    {
        get
        {
            return _noDecimalRoundingForLimitStopPrice;
        }
        set
        {
            _noDecimalRoundingForLimitStopPrice = value;
        }
    }

    public List<Position> MasterPositions => _masterPositions;

    internal List<Position> CurrentPositions => _currentPositions;

    internal List<Alert> CurrentAlerts => _currentAlerts;

    internal List<Position> ActivePositions => _activePositions;

    internal double RiskStopLevel
    {
        get
        {
            return _riskStopLevel;
        }
        set
        {
            _riskStopLevel = value;
        }
    }

    public TradingSystemExecutorOwn()
    {
        systemPerformance_0 = new SystemPerformance(null);
    }

    public void Initialize()
    {
        Performance.PositionSize = PosSize;
        systemPerformance_0.Results.CurrentCash = PosSize.StartingCapital;
        systemPerformance_0.Results.CurrentEquity = PosSize.StartingCapital;
    }

    public void Execute(Strategy strategy_1, WealthScript wealthScript_1, Bars barsCharted, List<Bars> barsCollection, bool avoidClearingTradeList = false)
    {
        if (wealthScript_1 != null)
        {
            wealthScript_1.StrategyWindowID = StrategyWindowID;
        }

        List<Bars> list = new List<Bars>();
        foreach (Bars item in barsCollection)
        {
            list.Add(item);
        }

        Strategy = strategy_1;
        systemPerformance_0.Strategy = strategy_1;
        list_7.Clear();
        _riskStopLevel = 0.0;
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
                List<string> list2 = new List<string>();
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
                    List<Bars> list3 = new List<Bars>();
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
                    foreach (Position item6 in tradingSystemExecutor._masterPositions)
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

                    _masterPositions.AddRange(tradingSystemExecutor._masterPositions);
                    tradingSystemExecutor.Performance.RawTrades = tradingSystemExecutor._masterPositions;
                    _masterAlerts.AddRange(tradingSystemExecutor.Performance.Results.Alerts);
                    if (!BenchmarkBuyAndHoldON)
                    {
                        foreach (Position position2 in tradingSystemExecutor.systemPerformance_0.ResultsBuyHold.Positions)
                        {
                            systemPerformance_0.ResultsBuyHold.method_4(position2);
                        }
                    }

                    tradingSystemExecutor.Performance.Results.TradesNSF = tradingSystemExecutor._masterPositions.Count - tradingSystemExecutor.Performance.Results.Positions.Count;
                    Performance.Results.TradesNSF += tradingSystemExecutor.Performance.Results.TradesNSF;
                    int num = 0;
                    foreach (Position item8 in tradingSystemExecutor._masterPositions)
                    {
                        if (item8.PositionType == PositionType.Long)
                        {
                            num++;
                        }
                    }

                    int num2 = tradingSystemExecutor._masterPositions.Count - num;
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
                    ChartRenderer chartRenderer_ = barsCharted == item9 ? _renderer : null;
                    method_2(item9, wealthScript_1, chartRenderer_);
                }
            }
            finally
            {
                PosSize = posSize;
                list_7.Clear();
            }
        }

        _masterPositions.Sort(this);
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

            wealthScript_1.method_4(bars_1, chartRenderer_1, this, _dataSet);
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

        systemPerformance_0.RawTrades = _masterPositions;
        systemPerformance_0.PositionSize = PosSize;
        SystemResults systemResults = new SystemResults(systemPerformance_0);
        if (!BenchmarkBuyAndHoldON)
        {
            foreach (Position position3 in systemPerformance_0.ResultsBuyHold.Positions)
            {
                systemResults.method_4(position3);
            }
        }

        int tradesNSF = Performance.Results.TradesNSF;
        int tradesNSF2 = Performance.ResultsLong.TradesNSF;
        int tradesNSF3 = Performance.ResultsShort.TradesNSF;
        systemPerformance_0.method_2();
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
                systemPerformance_0.ResultsBuyHold.method_4(position4);
            }
        }

        if (ApplyInterest)
        {
            systemPerformance_0.CashReturnRate = CashRate;
        }
        else
        {
            systemPerformance_0.CashReturnRate = 0.0;
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
            systemPerformance_0.method_1(item3);
        }

        systemPerformance_0.Results.BuildEquityCurve(ilist_0, this, callbackToSizePositions: true, posSizer_0);
        systemPerformance_0.Results.method_7(bool_0: true);
        foreach (Position item4 in _masterPositions)
        {
            if (item4.Shares > 0.0)
            {
                systemPerformance_0.Results.method_4(item4);
                if (item4.PositionType == PositionType.Long)
                {
                    systemPerformance_0.ResultsLong.method_4(item4);
                }
                else
                {
                    systemPerformance_0.ResultsShort.method_4(item4);
                }
            }
            else if (Strategy.StrategyType != StrategyType.CombinedStrategy)
            {
                systemPerformance_0.Results.TradesNSF++;
                if (item4.PositionType == PositionType.Long)
                {
                    systemPerformance_0.ResultsLong.TradesNSF++;
                }
                else
                {
                    systemPerformance_0.ResultsShort.TradesNSF++;
                }
            }
        }

        foreach (Alert item5 in _masterAlerts)
        {
            SystemPerformance systemPerformance = systemPerformance_0;
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
                        systemPerformance_0.ResultsBuyHold.method_4(position);
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
                    systemPerformance_0.ResultsBuyHold.method_4(position2);
                    if (Commission != null && ApplyCommission)
                    {
                        position2.EntryCommission = Commission.Calculate(TradeType.Buy, OrderType.Market, position2.EntryPrice, position2.Shares, item9);
                    }
                }
            }
        }

        ReduceQtyBasedOnVolume = reduceQtyBasedOnVolume;
        systemPerformance_0.ResultsLong.BuildEquityCurve(ilist_0, this, callbackToSizePositions: false, posSizer_0);
        systemPerformance_0.ResultsShort.BuildEquityCurve(ilist_0, this, callbackToSizePositions: false, posSizer_0);
        systemPerformance_0.ResultsBuyHold.method_8();
        if (!BenchmarkBuyAndHoldON)
        {
            systemPerformance_0.ResultsBuyHold.BuildEquityCurve(ilist_0, this, callbackToSizePositions: false, null);
        }
        else
        {
            if (tradingSystemExecutor.ilist_0[0].Count == 0)
            {
                tradingSystemExecutor.ilist_0[0] = ilist_0[0];
            }

            systemPerformance_0.ResultsBuyHold.BuildEquityCurve(tradingSystemExecutor.ilist_0, tradingSystemExecutor, callbackToSizePositions: false, null);
        }

        if (posSizer_0 != null)
        {
            systemPerformance_0.Results.method_9(posSizer_0);
        }

        if (Strategy.StrategyType != StrategyType.CombinedStrategy)
        {
            foreach (Alert alert in systemPerformance_0.Results.Alerts)
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
            _masterPositions.Clear();
        }

        _masterAlerts.Clear();
        systemPerformance_0.method_2();
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
        double currentEquity = systemPerformance_0.Results.CurrentEquity;
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

            BarsLoader.method_1(_dataSet);
            _dataSet.Provider.IsStreamingRequest = IsStreaming;
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
