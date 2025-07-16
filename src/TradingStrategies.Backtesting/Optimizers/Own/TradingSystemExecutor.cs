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
    public bool CalcResultsLong { get; } = true;
    public bool CalcResultsShort { get; } = true;
    public bool CalcMfeMae { get; } = true;
    public bool CalcOpenPositionsCount { get; } = true;
    public EquityCalcMode EquityCalcMode { get; } = EquityCalcMode.Full;

    private SystemPerformanceOwn Performance { get; }
    public SystemPerformance PerformanceNative { get; private set; }

    private readonly TradingSystemExecutor _nativeExecutor;

    public TradingSystemExecutorOwn(TradingSystemExecutor nativeExecutor)
    {
        _nativeExecutor = nativeExecutor;
        //Performance = new SystemPerformance(null);
    }

    public TradingSystemExecutorOwn(
        TradingSystemExecutor nativeExecutor,
        bool calcResultsLong,
        bool calcResultsShort,
        bool calcMfeMae,
        bool calcOpenPositionsCount,
        EquityCalcMode equityCalcMode
    )
    {
        _nativeExecutor = nativeExecutor;

        CalcResultsLong = calcResultsLong;
        CalcResultsShort = calcResultsShort;
        CalcMfeMae = calcMfeMae;
        CalcOpenPositionsCount = calcOpenPositionsCount;
        EquityCalcMode = equityCalcMode;

        Performance = new SystemPerformanceOwn(null);

        Performance.Results = new SystemResultsOwn(Performance);
        Performance.ResultsLong = CalcResultsLong ? new SystemResultsOwn(Performance) : null;
        Performance.ResultsShort = CalcResultsShort ? new SystemResultsOwn(Performance) : null;

        Performance.Results.CalcOpenPositionsCount = CalcOpenPositionsCount;
        Performance.ResultsLong?.CalcOpenPositionsCount = CalcOpenPositionsCount;
        Performance.ResultsShort?.CalcOpenPositionsCount  = CalcOpenPositionsCount;

        Performance.Results.EquityCalcMode = EquityCalcMode;
        Performance.ResultsLong?.EquityCalcMode = EquityCalcMode;
        Performance.ResultsShort?.EquityCalcMode = EquityCalcMode;
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
        foreach (Bars bars in barsCollection)
        {
            Performance.AddBars(bars);
        }

        Performance.ScaleProxy = barsCollection[0].Scale;
        Performance.BarIntervalProxy = barsCollection[0].BarInterval;
        Performance.PositionSizeProxy = PosSize;

        _wealthScriptExecuting = wealthScript;
        _rawProfitMode = PosSize.RawProfitMode;

        PositionSize posSize = PosSize;

        if (PosSize.RawProfitMode == false && 
            PosSize.Mode != PosSizeMode.ScriptOverride)
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

        FillNativePerformance();
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
        _posSizer = null;

        if (PosSize.Mode == PosSizeMode.SimuScript)
        {
            foreach (PosSizer posSizer in PosSizers)
            {
                if (posSizer.FriendlyName != PosSize.SimuScriptName)
                {
                    continue;
                }

                _posSizer = (PosSizer)Activator.CreateInstance(posSizer.GetType());

                if (PosSize.PosSizerConfig != string.Empty && 
                    (PosSize.SimuScriptName == PosSize.PosSizerThatWasConfigured || PosSize.PosSizerThatWasConfigured == string.Empty))
                {
                    try
                    {
                        _posSizer.ApplyConfigString(PosSizer.ParseConfigString(PosSize.PosSizerConfig));
                    }
                    catch
                    {
                    }
                }

                break;
            }
        }

        foreach (Bars bars in _barsSet)
        {
            Performance.AddBars(bars);
        }

        Performance.Results.BuildEquityCurve(_barsSet, _nativeExecutor, callbackToSizePositions: true, _posSizer);
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
            if (alert.AlertType is not (TradeType.Buy or TradeType.Short))
            {
                if (alert.Position != null && alert.Position.Shares > 0.0)
                {
                    Performance.Results.AddAlert(alert);
                }
            }
            else
            {
                Performance.Results.AddAlert(alert);
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
            Performance.ResultsLong.BuildEquityCurve(_barsSet, _nativeExecutor, callbackToSizePositions: false, _posSizer);
        }
        if (CalcResultsShort)
        {
            Performance.ResultsShort.BuildEquityCurve(_barsSet, _nativeExecutor, callbackToSizePositions: false, _posSizer);
        }

        if (posSizer != null)
        if (_posSizer != null)
        {
            Performance.Results.SetPosSizerPositions(_posSizer);
        }

        foreach (Alert alert in Performance.Results.Alerts)
        {
            if (alert.AlertType is not (TradeType.Buy or TradeType.Short))
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

        //расчитывается автоматом при закрытии позиции, зачем пересчитывать еще раз хз,
        //мб актуально только для открытых позиций
        if (CalcMfeMae)
        {
            Performance.CalculateMfeMae();
        }
    }

    private void FillNativePerformance()
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

        PerformanceNative = native;
    }

    private static void FillNativeResults(SystemResults native, SystemResultsOwn own)
    {
        //equity и cash в нативной реализации изначально проинициализированы пустой серией
        if (own.EquityCurve != null)
            native.EquityCurveProxy = own.EquityCurve;
        if (own.CashCurve != null)
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
        if (avoidClearingTradeList == false)
        {
            MasterPositions.Clear();
        }

        _masterAlerts.Clear();
        Performance.Clear();
    }

    public int Compare(Position firstPosition, Position secondPosition)
    {
        if (firstPosition.EntryDate == secondPosition.EntryDate)
        {
            if (firstPosition.CombinedPriority == secondPosition.CombinedPriority)
            {
                if (WorstTradeSimulation == false)
                {
                    return -firstPosition.Priority.CompareTo(secondPosition.Priority);
                }
                return firstPosition.NetProfit.CompareTo(secondPosition.NetProfit);
            }
            return firstPosition.CombinedPriority.CompareTo(secondPosition.CombinedPriority);
        }
        return firstPosition.EntryDate.CompareTo(secondPosition.EntryDate);
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
        return CalcPositionSizeInternal(bars, barNum, basisPrice, positionType, riskStopLevel, currentEquity, overrideShareSize, currentCash, comingFromWealthScript: false);
    }

    //method_4
    internal double CalcPositionSizeInternal(Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel, double currentEquity, double overrideShareSize, double currentCash, bool comingFromWealthScript)
    {
        double resultSize = 0.0;

        if (bars.SymbolInfo.SecurityType == SecurityType.Future && bars.SymbolInfo.Margin <= 0.0)
        {
            throw new ArgumentException("Margin must be greater than zero");
        }

        switch (PosSize.Mode)
        {
            case PosSizeMode.RawProfitDollar:
                resultSize = bars.SymbolInfo.SecurityType switch
                {
                    SecurityType.Future => PosSize.RawProfitDollarSize / bars.SymbolInfo.Margin,
                    SecurityType.StockOption => PosSize.RawProfitDollarSize / (basisPrice * 100.0),
                    _ => PosSize.RawProfitDollarSize / basisPrice,
                };
                break;
            case PosSizeMode.RawProfitShare:
                resultSize = PosSize.RawProfitShareSize;
                break;
            case PosSizeMode.Dollar:
                resultSize = bars.SymbolInfo.SecurityType switch
                {
                    SecurityType.Future => PosSize.DollarSize / bars.SymbolInfo.Margin,
                    SecurityType.StockOption => PosSize.DollarSize / (basisPrice * 100.0),
                    _ => PosSize.DollarSize / basisPrice,
                };
                break;
            case PosSizeMode.Share:
                resultSize = PosSize.ShareSize;
                break;
            case PosSizeMode.PctEquity:
                {
                    double size = PosSize.PctSize / 100.0 * currentEquity;
                    resultSize = bars.SymbolInfo.SecurityType switch
                    {
                        SecurityType.Future => size / bars.SymbolInfo.Margin,
                        SecurityType.StockOption => size / (basisPrice * 100.0),
                        _ => size / basisPrice,
                    };
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

                    var equity = currentEquity * (PosSize.RiskSize / 100.0);
                    double price = positionType == PositionType.Long ? basisPrice - riskStopLevel : riskStopLevel - basisPrice;
                    try
                    {
                        resultSize = equity / (price * bars.SymbolInfo.PointValue);
                    }
                    catch
                    {
                        return 0.0;
                    }

                    if (bars.SymbolInfo.SecurityType == SecurityType.Future && bars.SymbolInfo.Margin > 0.0)
                    {
                        resultSize = Math.Min(resultSize, currentEquity / bars.SymbolInfo.Margin);
                    }
                    else if (bars.SymbolInfo.SecurityType == SecurityType.StockOption)
                    {
                        resultSize = Math.Min(resultSize, currentEquity / (basisPrice * 100.0));
                    }
                    else
                    {
                        resultSize = Math.Min(resultSize, currentEquity / basisPrice);
                    }

                    break;
                }
            case PosSizeMode.SimuScript:
                {
                    if (_posSizer != null)
                    {
                        try
                        {
                            resultSize = _posSizer.SizePosition(_position, bars, barNum - 1, basisPrice, positionType, riskStopLevel, currentEquity, currentCash);
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
                }
            case PosSizeMode.ScriptOverride:
                resultSize = overrideShareSize;
                PosSize.OverrideShareSize = resultSize;
                break;
        }

        if (ReduceQtyBasedOnVolume && barNum < bars.Count)
        {
            double size = bars.Volume[barNum] * (RedcuceQtyPct / 100.0);
            resultSize = Math.Min(resultSize, size);
        }
        
        if (bars.SymbolInfo.SecurityType != SecurityType.MutualFund)
        {
            resultSize = (int)resultSize; //в обычном режиме дробные позиции округляются
        }
        else
        {
            int size = (int)(resultSize * 1000.0);
            resultSize = size / 1000.0;
        }

        if ((comingFromWealthScript && _rawProfitMode || !comingFromWealthScript) &&
            RoundLots &&
            bars.SymbolInfo.SecurityType == SecurityType.Equity &&
            resultSize > 0.0)
        {
            double size = resultSize / 100.0;
            size = Math.Round(size) * 100.0;

            if (RoundLots50 && resultSize < 100.0)
            {
                size = 100.0;
            }

            resultSize = size;
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
        double overrideShareSize = PosSize.Mode == PosSizeMode.ScriptOverride 
            ? PosSize.OverrideShareSize 
            : 0.0;

        return CalcPositionSizeInternal(bars, barNum, basisPrice, positionType, riskStopLevel, currentEquity, overrideShareSize, 0.0, comingFromWealthScript);
    }

    public double CalcPositionSize(Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel, double equity)
    {
        double overrideShareSize = PosSize.Mode == PosSizeMode.ScriptOverride
            ? PosSize.OverrideShareSize
            : 0.0;

        return CalcPositionSize(bars, barNum, basisPrice, positionType, riskStopLevel, equity, overrideShareSize, 0.0);
    }

    public double CalcPositionSize(Position position, Bars bars, int barNum, double basisPrice, PositionType positionType, double riskStopLevel, bool useOverRide, double overrideShareSize, double thisBarCash)
    {
        if (Strategy != null && Strategy.StrategyType == StrategyType.CombinedStrategy)
        {
            return position.Shares;
        }

        _position = position;

        double currentEquity = Performance.Results.CurrentEquity;
        double size = CalcPositionSize(bars, barNum, basisPrice, positionType, riskStopLevel, currentEquity, overrideShareSize, thisBarCash);

        _position = null;

        return size;
    }
}
