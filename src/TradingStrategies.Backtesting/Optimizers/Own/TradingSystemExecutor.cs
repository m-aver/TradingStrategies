using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Own;

public class TradingSystemExecutor
{
    private EventHandler<StrategyEventArgs> eventHandler_0;

    private EventHandler<DataSourceLookupEventArgs> eventHandler_1;

    private EventHandler<LoadSymbolEventArgs> eventHandler_2;

    private EventHandler<LoadSymbolFromDataSetEventArgs> eventHandler_3;

    private EventHandler<BarsEventArgs> eventHandler_4;

    private EventHandler<BarsEventArgs> eventHandler_5;

    private EventHandler<WSExceptionEventArgs> eventHandler_6;

    private EventHandler<EventArgs> eventHandler_7;

    private EventHandler<EventArgs> eventHandler_8;

    private EventHandler<DebugStringEventArgs> eventHandler_9;

    private EventHandler<ChartBitmapEventArgs> eventHandler_10;

    private EventHandler<TrendLineEventArgs> eventHandler_11;

    private EventHandler<StrategyParameterEventArgs> eventHandler_12;

    public int StrategyWindowID;

    private static bool bool_0 = false;

    private ChartRenderer chartRenderer_0;

    private BarsLoader barsLoader_0;

    private FundamentalsLoader fundamentalsLoader_0;

    private bool bool_1 = true;

    private bool bool_2;

    private Bars bars_0;

    private Commission commission_0;

    private bool bool_3;

    private bool bool_4;

    private string string_0;

    private bool bool_5;

    private bool bool_6;

    private double double_0 = 1.0;

    private int int_0 = 1;

    private bool bool_7;

    private bool bool_8;

    private bool bool_9;

    private List<string> list_0 = new List<string>();

    private bool bool_10;

    private double double_1;

    private DataSource dataSource_0;

    private double double_2;

    private bool bool_11;

    public List<Position> _activePositions = new List<Position>();

    private bool bool_12;

    private double double_3;

    private double double_4;

    private bool bool_13;

    private bool bool_14;

    private double double_5;

    private double double_6;

    private string string_1 = "";

    private bool bool_15;

    private double double_7 = 10.0;

    private double double_8;

    private PosSizer posSizer_0;

    private Position position_0;

    private bool bool_16 = true;

    private WealthScript wealthScript_0;

    private List<Bars> list_1 = new List<Bars>();

    private List<Bars> list_2 = new List<Bars>();

    private PositionSize positionSize_0 = new PositionSize();

    private List<Position> list_3 = new List<Position>();

    private List<Alert> list_4 = new List<Alert>();

    private List<Position> list_5 = new List<Position>();

    private List<Alert> list_6 = new List<Alert>();

    private SystemPerformance systemPerformance_0;

    private IList<Bars> ilist_0;

    private static PositionSize positionSize_1 = new PositionSize(PosSizeMode.RawProfitShare, 1.0);

    private Dictionary<string, Bars> dictionary_0 = new Dictionary<string, Bars>();

    private static bool bool_17 = false;

    private static double double_9 = 1.0;

    private static int int_1 = 0;

    public static List<PosSizer> PosSizers = null;

    private List<Bars> list_7 = new List<Bars>();

    [CompilerGenerated]
    private Strategy strategy_0;

    [CompilerGenerated]
    private bool bool_18;

    [CompilerGenerated]
    private SystemPerformance systemPerformance_1;

    [CompilerGenerated]
    private string string_2;

    [CompilerGenerated]
    private bool bool_19;

    [CompilerGenerated]
    private DateTime dateTime_0;

    [CompilerGenerated]
    private DateTime dateTime_1;

    [CompilerGenerated]
    private int int_2;

    [CompilerGenerated]
    private bool bool_20;

    [CompilerGenerated]
    private object object_0;

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

    public bool isChildStrategy
    {
        [CompilerGenerated]
        get
        {
            return bool_18;
        }
        [CompilerGenerated]
        set
        {
            bool_18 = value;
        }
    }

    public SystemPerformance ParentSysPerf
    {
        [CompilerGenerated]
        get
        {
            return systemPerformance_1;
        }
        [CompilerGenerated]
        set
        {
            systemPerformance_1 = value;
        }
    }

    public string DividendItemName
    {
        [CompilerGenerated]
        get
        {
            return string_2;
        }
        [CompilerGenerated]
        set
        {
            string_2 = value;
        }
    }

    public ChartRenderer Renderer
    {
        get
        {
            return chartRenderer_0;
        }
        set
        {
            chartRenderer_0 = value;
        }
    }

    public BarsLoader BarsLoader
    {
        get
        {
            return barsLoader_0;
        }
        set
        {
            barsLoader_0 = value;
        }
    }

    public FundamentalsLoader FundamentalsLoader
    {
        get
        {
            return fundamentalsLoader_0;
        }
        set
        {
            fundamentalsLoader_0 = value;
        }
    }

    public PositionSize PosSize
    {
        get
        {
            return positionSize_0;
        }
        set
        {
            positionSize_0 = value;
        }
    }

    public Commission Commission
    {
        get
        {
            return commission_0;
        }
        set
        {
            commission_0 = value;
        }
    }

    public bool ApplyCommission
    {
        get
        {
            return bool_9;
        }
        set
        {
            bool_9 = value;
        }
    }

    public bool ApplyInterest
    {
        get
        {
            return bool_13;
        }
        set
        {
            bool_13 = value;
        }
    }

    public double CashRate
    {
        get
        {
            return double_3;
        }
        set
        {
            double_3 = value;
            double_5 = Math.Exp(Math.Log(1.0 + double_3 / 100.0) / 365.25);
        }
    }

    public double MarginRate
    {
        get
        {
            return double_4;
        }
        set
        {
            double_4 = value;
            double_6 = Math.Exp(Math.Log(1.0 + double_4 / 100.0) / 365.25);
        }
    }

    public bool ApplyDividends
    {
        get
        {
            return bool_14;
        }
        set
        {
        }
    }

    public bool ApplyWFODateRange
    {
        [CompilerGenerated]
        get
        {
            return bool_19;
        }
        [CompilerGenerated]
        set
        {
            bool_19 = value;
        }
    }

    public bool ExceptionEvents
    {
        get
        {
            return bool_2;
        }
        set
        {
            bool_2 = value;
        }
    }

    public bool BuildEquityCurves
    {
        get
        {
            return bool_1;
        }
        set
        {
        }
    }

    public DataSource DataSet
    {
        get
        {
            return dataSource_0;
        }
        set
        {
            dataSource_0 = value;
        }
    }

    public string StrategyName
    {
        get
        {
            return string_1;
        }
        set
        {
            string_1 = value;
        }
    }

    public bool ReduceQtyBasedOnVolume
    {
        get
        {
            return bool_15;
        }
        set
        {
            bool_15 = value;
        }
    }

    public double RedcuceQtyPct
    {
        get
        {
            return double_7;
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

    public IList<string> DebugStrings => list_0;

    public bool WorstTradeSimulation
    {
        get
        {
            return bool_4;
        }
        set
        {
            bool_4 = value;
        }
    }

    public bool BenchmarkBuyAndHoldON
    {
        get
        {
            return bool_5;
        }
        set
        {
            bool_5 = value;
        }
    }

    public string BenchmarkSymbol
    {
        get
        {
            return string_0;
        }
        set
        {
            string_0 = value;
        }
    }

    public bool EnableSlippage
    {
        get
        {
            return bool_3;
        }
        set
        {
            bool_3 = value;
        }
    }

    public bool LimitOrderSlippage
    {
        get
        {
            return bool_6;
        }
        set
        {
            bool_6 = value;
        }
    }

    public double SlippageUnits
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

    public int SlippageTicks
    {
        get
        {
            return int_0;
        }
        set
        {
            int_0 = value;
        }
    }

    public bool LimitDaySimulation
    {
        get
        {
            return bool_12;
        }
        set
        {
            bool_12 = value;
        }
    }

    public bool RoundLots
    {
        get
        {
            return bool_7;
        }
        set
        {
            bool_7 = value;
        }
    }

    public bool RoundLots50
    {
        get
        {
            return bool_8;
        }
        set
        {
            bool_8 = value;
        }
    }

    public bool IsStreaming
    {
        get
        {
            return bool_10;
        }
        set
        {
            bool_10 = value;
        }
    }

    public double OverrideShareSize
    {
        get
        {
            return double_1;
        }
        set
        {
            double_1 = value;
            if (PosSize.Mode == PosSizeMode.ScriptOverride)
            {
                PosSize.OverrideShareSize = value;
            }
        }
    }

    public int PricingDecimalPlaces
    {
        [CompilerGenerated]
        get
        {
            return int_2;
        }
        [CompilerGenerated]
        set
        {
            int_2 = value;
        }
    }

    public bool NoDecimalRoundingForLimitStopPrice
    {
        [CompilerGenerated]
        get
        {
            return bool_20;
        }
        [CompilerGenerated]
        set
        {
            bool_20 = value;
        }
    }

    public List<Position> MasterPositions => list_3;

    internal List<Position> CurrentPositions => list_5;

    internal List<Alert> CurrentAlerts => list_6;

    internal List<Position> ActivePositions => _activePositions;

    internal double RiskStopLevel
    {
        get
        {
            return double_2;
        }
        set
        {
            double_2 = value;
        }
    }

    public TradingSystemExecutor()
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
        double_2 = 0.0;
        double_8 = 0.0;
        list_0.Clear();
        bool_11 = false;
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
        wealthScript_0 = wealthScript_1;
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
                    if (eventHandler_0 != null)
                    {
                        StrategyEventArgs strategyEventArgs = new StrategyEventArgs(combinedStrategyChild.StrategyID.ToString());
                        eventHandler_0(this, strategyEventArgs);
                        strategy = strategyEventArgs.Strategy;
                    }

                    if (strategy == null)
                    {
                        continue;
                    }

                    TradingSystemExecutor tradingSystemExecutor = new TradingSystemExecutor();
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
                    else if (eventHandler_1 != null)
                    {
                        DataSourceLookupEventArgs dataSourceLookupEventArgs = new DataSourceLookupEventArgs(combinedStrategyChild.DataSetName);
                        eventHandler_1(this, dataSourceLookupEventArgs);
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
                    tradingSystemExecutor.LookupDataSource += eventHandler_1;
                    tradingSystemExecutor.LookupStrategy += eventHandler_0;
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
                    tradingSystemExecutor.ExternalSymbolRequested += eventHandler_2;
                    tradingSystemExecutor.ExternalSymbolFromDataSetRequested += eventHandler_3;
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
                    tradingSystemExecutor.ExternalSymbolRequested -= eventHandler_2;
                    tradingSystemExecutor.ExternalSymbolFromDataSetRequested -= eventHandler_3;
                    tradingSystemExecutor.WealthScriptException -= method_1;
                    list2.AddRange(tradingSystemExecutor.DebugStrings);
                    foreach (Position item6 in tradingSystemExecutor.list_3)
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

                    list_3.AddRange(tradingSystemExecutor.list_3);
                    tradingSystemExecutor.Performance.RawTrades = tradingSystemExecutor.list_3;
                    list_4.AddRange(tradingSystemExecutor.Performance.Results.Alerts);
                    if (!BenchmarkBuyAndHoldON)
                    {
                        foreach (Position position2 in tradingSystemExecutor.systemPerformance_0.ResultsBuyHold.Positions)
                        {
                            systemPerformance_0.ResultsBuyHold.method_4(position2);
                        }
                    }

                    tradingSystemExecutor.Performance.Results.TradesNSF = tradingSystemExecutor.list_3.Count - tradingSystemExecutor.Performance.Results.Positions.Count;
                    Performance.Results.TradesNSF += tradingSystemExecutor.Performance.Results.TradesNSF;
                    int num = 0;
                    foreach (Position item8 in tradingSystemExecutor.list_3)
                    {
                        if (item8.PositionType == PositionType.Long)
                        {
                            num++;
                        }
                    }

                    int num2 = tradingSystemExecutor.list_3.Count - num;
                    tradingSystemExecutor.Performance.ResultsLong.TradesNSF = num - tradingSystemExecutor.Performance.ResultsLong.Positions.Count;
                    Performance.ResultsLong.TradesNSF += tradingSystemExecutor.Performance.ResultsLong.TradesNSF;
                    tradingSystemExecutor.Performance.ResultsShort.TradesNSF = num2 - tradingSystemExecutor.Performance.ResultsShort.Positions.Count;
                    Performance.ResultsShort.TradesNSF += tradingSystemExecutor.Performance.ResultsShort.TradesNSF;
                    tradingSystemExecutor.LookupDataSource -= eventHandler_1;
                    tradingSystemExecutor.LookupStrategy -= eventHandler_0;
                }

                list_0.Clear();
                list_0.AddRange(list2);
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
                    ChartRenderer chartRenderer_ = barsCharted == item9 ? chartRenderer_0 : null;
                    method_2(item9, wealthScript_1, chartRenderer_);
                }
            }
            finally
            {
                PosSize = posSize;
                list_7.Clear();
            }
        }

        list_3.Sort(this);
        if (BuildEquityCurves)
        {
            ApplyPositionSize();
        }
    }

    private void method_0(object sender, BarsEventArgs e)
    {
        if (eventHandler_5 != null)
        {
            eventHandler_5(this, e);
        }
    }

    private void method_1(object sender, WSExceptionEventArgs e)
    {
        TradingSystemExecutor tradingSystemExecutor = sender as TradingSystemExecutor;
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
            bars_0 = bars_1;
            bars_1.method_1();
            if (!bool_0)
            {
                if (eventHandler_12 != null)
                {
                    eventHandler_12(this, new StrategyParameterEventArgs(wealthScript_1, bars_1.Symbol));
                }
            }
            else if (Strategy.UsePreferredValues)
            {
                Strategy.LoadPreferredValues(bars_1.Symbol, wealthScript_1);
            }

            wealthScript_1.method_4(bars_1, chartRenderer_1, this, dataSource_0);
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

            if (eventHandler_6 != null)
            {
                WSExceptionEventArgs wSExceptionEventArgs = new WSExceptionEventArgs(ex);
                wSExceptionEventArgs.Strategy = Strategy;
                wSExceptionEventArgs.Symbol = bars_1.Symbol;
                eventHandler_6(this, wSExceptionEventArgs);
            }
        }

        if (eventHandler_4 != null)
        {
            eventHandler_4(this, new BarsEventArgs(bars_1));
        }
    }

    public void ApplyPositionSize()
    {
        TradingSystemExecutor tradingSystemExecutor = new TradingSystemExecutor();
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

        systemPerformance_0.RawTrades = list_3;
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

        bool_11 = false;
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
        foreach (Position item4 in list_3)
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

        foreach (Alert item5 in list_4)
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
            list_3.Clear();
        }

        list_4.Clear();
        systemPerformance_0.method_2();
        list_1.Clear();
        list_2.Clear();
    }

    public void ApplySettings(TradingSystemExecutor executor)
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
                        bool_11 = true;
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
        if (wealthScript_0 == null)
        {
            return new Bars(string_3, BarScale.Daily, 0);
        }

        Bars bars = wealthScript_0.Bars;
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

            BarsLoader.method_1(dataSource_0);
            dataSource_0.Provider.IsStreamingRequest = IsStreaming;
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

        if ((bars2 == null || bars2.Count == 0) && eventHandler_2 != null)
        {
            LoadSymbolEventArgs loadSymbolEventArgs = new LoadSymbolEventArgs(string_3, bars.Scale, bars.BarInterval);
            eventHandler_2(this, loadSymbolEventArgs);
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
        list_0.Add(string_3);
    }
}
