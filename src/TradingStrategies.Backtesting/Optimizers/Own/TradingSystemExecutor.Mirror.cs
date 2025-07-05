using WealthLab;
using TradingStrategies.Utilities.InternalsProxy;

namespace TradingStrategies.Backtesting.Optimizers.Own;

//mirror of native executor members
public partial class TradingSystemExecutorOwn
{
    private TradingSystemExecutor tse => _nativeExecutor;

    public int StrategyWindowID { get => tse.StrategyWindowID; set => tse.StrategyWindowID = value; }

    private Bars _barsBeingProcessed { get => tse._barsBeingProcessed; set => tse._barsBeingProcessed = value; }

    private double _overrideShareSize { get => tse.OverrideShareSize; set => tse.OverrideShareSize = value; }
    private bool _riskStopLevelNotSet { get => tse.RiskStopLevelNotSet; set => tse.RiskStopLevelNotSet = value; }
    private double _cashRate { get => tse.CashRate; set => tse.CashRate = value; }
    private double _marginRate { get => tse.MarginRate; set => tse.MarginRate = value; }
    private double _autoProfitLevel { get => tse.AutoProfitLevel; set => tse.AutoProfitLevel = value; }
    private List<Alert> _masterAlerts { get => tse.MasterAlerts; }

    private double _cashAdjustmentFactor { get => tse.CashAdjustmentFactor; }
    private double _marginAdjustmentFactor { get => tse.MarginAdjustmentFactor; }

    private PosSizer posSizer { get => tse.posSizer; set => tse.posSizer = value; }
    private Position position_0 { get => tse.position_0; set => tse.position_0 = value; }
    private bool _rawProfitMode { get => tse._rawProfitMode; set => tse._rawProfitMode = value; }
    private WealthScript _wealthScriptExecuting { get => tse._wealthScriptExecuting; set => tse._wealthScriptExecuting = value; }
    private IList<Bars> _barsSet { get => tse._barsSet; set => tse._barsSet = value; }

    private static PositionSize positionSize { get => TradingSystemExecutorExtensions.positionSize; }
    public static List<PosSizer> PosSizers { get => TradingSystemExecutor.PosSizers; set => TradingSystemExecutor.PosSizers = value; }

    public Strategy Strategy { get => tse.Strategy; set => tse.Strategy = value; }
    public string DividendItemName { get => tse.DividendItemName; set => tse.DividendItemName = value; }
    public FundamentalsLoader FundamentalsLoader { get => tse.FundamentalsLoader; set => tse.FundamentalsLoader = value; }
    public PositionSize PosSize { get => tse.PosSize; set => tse.PosSize = value; }
    public Commission Commission { get => tse.Commission; set => tse.Commission = value; }
    public bool ApplyCommission { get => tse.ApplyCommission; set => tse.ApplyCommission = value; }
    public bool ApplyInterest { get => tse.ApplyInterest; set => tse.ApplyInterest = value; }
    public double CashRate { get => tse.CashRate; set => tse.CashRate = value; }
    public double MarginRate { get => tse.MarginRate; set => tse.MarginRate = value; }
    public bool ApplyDividends { get => tse.ApplyDividends; set => tse.ApplyDividends = value; }
    public bool BuildEquityCurves { get => tse.BuildEquityCurves; set => tse.BuildEquityCurves = value; }
    public DataSource DataSet { get => tse.DataSet; set => tse.DataSet = value; }
    public bool ReduceQtyBasedOnVolume { get => tse.ReduceQtyBasedOnVolume; set => tse.ReduceQtyBasedOnVolume = value; }
    public double RedcuceQtyPct { get => tse.RedcuceQtyPct; set => tse.RedcuceQtyPct = value; }
    //public SystemPerformance Performance { get => tse.Performance; set => tse.Performance = value; }
    public bool WorstTradeSimulation { get => tse.WorstTradeSimulation; set => tse.WorstTradeSimulation = value; }
    public string BenchmarkSymbol { get => tse.BenchmarkSymbol; set => tse.BenchmarkSymbol = value; }
    public bool EnableSlippage { get => tse.EnableSlippage; set => tse.EnableSlippage = value; }
    public bool LimitOrderSlippage { get => tse.LimitOrderSlippage; set => tse.LimitOrderSlippage = value; }
    public double SlippageUnits { get => tse.SlippageUnits; set => tse.SlippageUnits = value; }
    public int SlippageTicks { get => tse.SlippageTicks; set => tse.SlippageTicks = value; }
    public bool LimitDaySimulation { get => tse.LimitDaySimulation; set => tse.LimitDaySimulation = value; }
    public bool RoundLots { get => tse.RoundLots; set => tse.RoundLots = value; }
    public bool RoundLots50 { get => tse.RoundLots50; set => tse.RoundLots50 = value; }
    public double OverrideShareSize { get => tse.OverrideShareSize; set => tse.OverrideShareSize = value; }
    public int PricingDecimalPlaces { get => tse.PricingDecimalPlaces; set => tse.PricingDecimalPlaces = value; }
    public bool NoDecimalRoundingForLimitStopPrice { get => tse.NoDecimalRoundingForLimitStopPrice; set => tse.NoDecimalRoundingForLimitStopPrice = value; }
    internal double RiskStopLevel { get => tse.RiskStopLevel; set => tse.RiskStopLevel = value; }

    public List<Position> MasterPositions { get => tse.MasterPositions; }
    internal List<Position> CurrentPositions { get => tse.CurrentPositions; }
    internal List<Alert> CurrentAlerts { get => tse.CurrentAlerts; }
    internal List<Position> ActivePositions { get => tse.ActivePositions; }

    //own fields
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
}
