using System;
using System.Collections.Generic;
using TradingStrategies.Utilities;
using WealthLab;
using TradingStrategies.Utilities.InternalsProxy;
using SynchronizedBarIterator = TradingStrategies.Utilities.SynchronizedBarIterator;

namespace TradingStrategies.Backtesting.Optimizers.Own;

//вообще идея PosSizer видимо в том, чтобы отделить логику торговых сигналов от размера позиций
//удобно когда торги ведутся одновременно по множеству бумаг/систем
//в конкретной системе можно не заморачиваться и не считать текущую многосоставную эквити, чтобы посчитать размер позиции
//а задавать размер уже постфактум, в отдельном модуле (своя перегрузка PosSizer или готовый), основываясь на текущей эквити (или других метриках), которую передат фреймворк
//но с другой стороны появляются накладные расходы на вызов кастомного PosSizer для каждой позиции, что может снизить производительность

public class SystemResultsOwn : IComparer<Position>
{
    private SystemPerformanceOwn _systemPerfomance;

    //используются только для передачи в PosSizer
    private DataSeries _drawdownCurve;
    private DataSeries _drawdownPercentCurve;
    private double _currentMaxEquity; //for drawdown

    //списки позиций, обрабатывавемые во время итерирования по SynchronizedBarIterator
    private List<Position> _currentlyActivePositions; //открытые на момент итерации (постоянно добавляются и удаляются)
    private List<Position> _closedPositions; //все обработанные и закрытые на момент итерации
    private List<Position> _accountedPositions; //начинающие обрабатываться с текущей итерации и имеющие больше 0 лотов (Shares)
    private List<Position> _currentlyClosedPositions; //список закрытых позиций на текущей итерации, очищается по кончанию итерации

    public List<Alert> Alerts { get; } = new();
    public List<Position> Positions { get; } = new();

    public DataSeries EquityCurve { get; internal set; }
    public DataSeries CashCurve { get; internal set; }
    public DataSeries OpenPositionCount { get; set; }

    internal double CurrentEquity { get; set; }
    internal double CurrentCash { get; set; }

    public int TradesNSF { get; set; }
    public double TotalCommission { get; private set; }
    public double CashReturn { get; internal set; }
    public double MarginInterest { get; internal set; }
    public double DividendsPaid { get; internal set; }

    public double NetProfit => Positions.Sum(x => x.NetProfit) + CashReturn + MarginInterest + DividendsPaid;
    public double ProfitPerBar => Positions.Count == 0 ? 0.0 : NetProfit / Positions.Sum(x => x.BarsHeld);
    public double AverageProfitAcrossTotalTimeSpan => EquityCurve is { Count: > 0 } ? NetProfit / EquityCurve.Count : 0.0;

    public double APR
    {
        get
        {
            if (EquityCurve == null || EquityCurve.Count < 2 || EquityCurve[0] == 0.0)
            {
                return 0.0;
            }

            TimeSpan timeSpan = EquityCurve.Date.Last() - EquityCurve.Date.First();
            double startEquity = EquityCurve[0];
            double finalEquity = EquityCurve[EquityCurve.Count - 1];
            return (Math.Pow(finalEquity / startEquity, 365.25 / timeSpan.Days) - 1.0) * 100.0;
        }
    }

    public bool CalcOpenPositionsCount { get; set; } = false;
    public EquityCalcMode EquityCalcMode { get; set; } = EquityCalcMode.Full;

    public SystemResultsOwn(SystemPerformanceOwn sysPerf)
    {
        _systemPerfomance = sysPerf;
    }

    public void BuildEquityCurve(IList<Bars> barsList, TradingSystemExecutor tradingSystemExecutor, bool callbackToSizePositions, PosSizer posSizer)
    {
        EquityCurve ??= new DataSeries("Equity");
        CashCurve ??= new DataSeries("Cash");
        OpenPositionCount ??= CalcOpenPositionsCount ? new DataSeries("OpenPositions") : null!;
        TotalCommission = 0.0;
        PositionSize positionSize = _systemPerfomance.PositionSize;
        double currentNetProfit = 0.0;

        //заполняется датасет для итерирования
        IList<Bars> barsSet = barsList;
        //barsSet = FillBarsSet(barsList, tradingSystemExecutor);

        //заполняется инфа по дивидендам
        //это будет очень сильно тормозить, если дивиденды появятся, так же как и ApplyDividents
        foreach (Bars bars in barsSet)
        {
            if (tradingSystemExecutor.ApplyDividends &&
                tradingSystemExecutor.PosSize.RawProfitMode == false)
            {
                IList<FundamentalItem> dividents =
                    tradingSystemExecutor.FundamentalsLoader.RequestSymbolItems(bars, bars.Symbol, tradingSystemExecutor.DividendItemName);

                bars.DivTag = dividents is { Count: > 0 } ? dividents : null!;
            }
            else
            {
                bars.DivTag = null;
            }
        }

        if (posSizer != null)
        {
            _accountedPositions ??= new();
            _closedPositions ??= new();
            _drawdownCurve ??= new DataSeries("DrawDown");
            _drawdownPercentCurve ??= new DataSeries("DrawDownPct");
            _currentMaxEquity = double.MinValue;
        }

        SynchronizedBarIterator barIterator = new SynchronizedBarIterator(barsSet);
        DateTime barDate = barIterator.Date;

        if (barDate == DateTime.MaxValue)
        {
            return;
        }

        _currentlyActivePositions ??= new();
        _currentlyClosedPositions ??= new();

        int currRemainingPos = 0;
        List<Position> remainingPositions = callbackToSizePositions ? tradingSystemExecutor.MasterPositions : Positions;
        //список всех позиций для обсчета, должны быть отсортированы по дате, при итерировании двигается указатель currRemainingPos, оставляя обсчитанные позиции позади

        if (posSizer != null)
        {
            posSizer.CandidatesProxy ??= [];
            posSizer.PreInitialize(tradingSystemExecutor, _currentlyActivePositions, _accountedPositions, _closedPositions, EquityCurve, CashCurve, _drawdownCurve, _drawdownPercentCurve);
            posSizer.Initialize();
        }

        CurrentCash = positionSize.RawProfitMode ? 0.0 : positionSize.StartingCapital;
        CurrentEquity = CurrentCash;

        //начальная граница
        if (EquityCalcMode != EquityCalcMode.Full)
        {
            EquityCurve.Add(CurrentEquity, barDate);
            CashCurve.Add(CurrentCash, barDate);
        }

        //цикл по SynchronizedBarIterator
        //каждая свеча со всех инструментов, упорядочено по времени
        do
        {
            barDate = barIterator.Date;

            double netProfitOfCurrentlyClosedPositions = 0.0;

            for (int pos = _currentlyActivePositions.Count - 1; pos >= 0; pos--)
            {
                Position position = _currentlyActivePositions[pos];

                //этот блок кажется нужен для того, чтобы накопился кеш от сделок закрытых по рыночной цене
                //видимо чтобы можно было этот кеш использовать для открытия других позиций на этой свече
                //но кажется это может быть опасно, если вход в позицию делается на открытии например
                if (position.ExitOrderType == OrderType.Market &&
                    position.ExitDate == barDate &&
                    position.Active == false)
                {
                    _currentlyActivePositions.RemoveAt(pos);
                    _currentlyClosedPositions.Add(position);
                    _closedPositions?.Add(position);
                    currentNetProfit += position.NetProfit;
                    CurrentCash += position.Size;
                    CurrentCash += position.NetProfit;
                    netProfitOfCurrentlyClosedPositions += position.NetProfit;
                    CurrentCash += position.EntryCommission;
                }
            }

            //выставляются конкурирующие позиции в PosSizer
            //видимо чтобы можно было учесть их при дальнешем вызове CalcPositionSize и правильно распределить кол-во лотов
            if (posSizer != null)
            {
                posSizer.Candidates.Clear();
                for (int i = currRemainingPos; i < remainingPositions.Count; i++)
                {
                    Position position = remainingPositions[i];
                    if (position.EntryDate == barDate)
                    {
                        posSizer.Candidates.Add(position);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            double currCash = CurrentCash; //это нужно только для callbackToSizePositions блока

            //цикл по конкурирующим позициям с одинаковой датой входа (текущей датой итератора)
            //корректируются CurrentCash и TotalCommission
            //определяется хватает ли текущего капитала для открытия позиций, расчитывается количество лотов (shares) и комиссии позиций
            //позиции добавляются в _currentlyActivePositions
            //двигается указатель currRemainingPos
            while (remainingPositions.Count > currRemainingPos)
            {
                Position position = remainingPositions[currRemainingPos];

                if (position.EntryDate != barDate)
                {
                    break;
                }

                //проставляются Shares и Commission на позиции
                if (callbackToSizePositions)
                {
                    //вызывает PosSizer, переданный и сконфигурированный выше, если PosSizeMode == SimuScript
                    //если PosSizeMode == ScriptOverride, то просто использует OverrideShareSize, установленный через WealthScript.SetShareSize перед открытием позиции
                    //но может дополнительно скорректироваться по ReduceQtyBasedOnVolume, RoundLots и пр.

                    var sharesSize = tradingSystemExecutor.CalcPositionSize(position, position.Bars, position.EntryBar, position.BasisPrice, position.PositionType, position.RiskStopLevel, useOverRide: true, position.OverrideShareSize, currCash);
                    position.SharesProxy = sharesSize * position.SplitFactor;

                    double positionPrice = position.Shares * position.EntryPrice + position.EntryCommission;
                    currCash -= positionPrice;

                    if (tradingSystemExecutor.Commission != null && tradingSystemExecutor.ApplyCommission)
                    {
                        var tradeType = position.PositionType != PositionType.Long ? TradeType.Short : TradeType.Buy;
                        position.EntryCommission = tradingSystemExecutor.Commission.Calculate(
                            tradeType, position.EntryOrderType, position.EntryPrice, position.Shares, position.Bars);

                        if (!position.Active)
                        {
                            tradeType = position.PositionType == PositionType.Long ? TradeType.Sell : TradeType.Cover;
                            position.ExitCommission = tradingSystemExecutor.Commission.Calculate(
                                tradeType, position.ExitOrderType, position.ExitPrice, position.Shares, position.Bars);
                        }
                    }
                }

                currRemainingPos++;

                //определяется, хватает ли запрошенного количества лотов при текущем капитале
                if (position.Shares > 0.0)
                {
                    double rest = CurrentCash;

                    //пока не до конца понятна суть расчета при наличии MarginFactor > 1
                    if (!tradingSystemExecutor.PosSize.RawProfitMode)
                    {
                        rest += CurrentEquity * (tradingSystemExecutor.PosSize.MarginFactor - 1);
                    }

                    bool isSufficient = true;
                    if (callbackToSizePositions)
                    {
                        isSufficient = positionSize.RawProfitMode || rest >= position.Size + position.EntryCommission;
                    }
                    if (isSufficient)
                    {
                        CurrentCash -= position.Size;
                        CurrentCash -= position.EntryCommission;
                        _currentlyActivePositions.Add(position);
                        _accountedPositions?.Add(position);
                        TotalCommission += position.EntryCommission + position.ExitCommission;
                    }
                    else
                    {
                        //это похоже обнуление позиции если она не удовлетворяет портфелю
                        //потом на основе этого проставляется TradesNSF из TradingSystemExecutor
                        position.SharesProxy = 0.0;
                    }
                }
            }

            //тут блок с подсчетом текущей прибыли от открытых на данный момент позиций _currentlyActivePositions
            //_currentlyActivePositions расщепляется на _currentlyActivePositions и _currentlyClosedPositions
            //подсчитывается CurrentCash от закрытых позиций и CurrentEquity от движений котировок по открытым позициям на текущей свече
            if (_currentlyActivePositions.Count > 0 || _currentlyClosedPositions.Count > 0)
            {
                for (int pos = _currentlyActivePositions.Count - 1; pos >= 0; pos--)
                {
                    Position position = _currentlyActivePositions[pos];

                    if (position.ExitDate == barDate &&
                        position.Active == false)
                    {
                        _currentlyActivePositions.RemoveAt(pos);
                        _currentlyClosedPositions.Add(position);
                        _closedPositions?.Add(position);
                        currentNetProfit += position.NetProfit;
                        CurrentCash += position.Size;
                        CurrentCash += position.NetProfit;
                        netProfitOfCurrentlyClosedPositions += position.NetProfit;
                        CurrentCash += position.EntryCommission; //Position.NetProfit учитывает комиссии, поэтому тут компенсация прошлого вычета
                    }
                }

                CurrentEquity = positionSize.RawProfitMode ? 0.0 : positionSize.StartingCapital;
                foreach (Position position in _currentlyActivePositions)
                {
                    int bar = barIterator.Bar(position.Bars);
                    CurrentEquity += position.NetProfitAsOfBar(bar);
                    ApplyDividents(position, bar, ref currentNetProfit);
                }
                foreach (Position position in _currentlyClosedPositions)
                {
                    int bar = barIterator.Bar(position.Bars);
                    CurrentEquity += position.NetProfitAsOfBar(bar);
                    ApplyDividents(position, bar, ref currentNetProfit);
                }

                double netProfitBeforeThisBar = currentNetProfit - netProfitOfCurrentlyClosedPositions; //доход от прошлых позиций + дивиденды от текущих
                CurrentEquity += netProfitBeforeThisBar;

                if (EquityCalcMode == EquityCalcMode.Sampled ||
                    (EquityCalcMode == EquityCalcMode.Closed && _currentlyClosedPositions.Count > 0))
                {
                    EquityCurve.Add(CurrentEquity, barDate);
                    CashCurve.Add(CurrentCash, barDate);
                }

                _currentlyClosedPositions.Clear();
            }

            OpenPositionCount?.Add(_currentlyActivePositions.Count, barDate);

            if (EquityCalcMode == EquityCalcMode.Full)
            {
                EquityCurve.Add(CurrentEquity, barDate);
                CashCurve.Add(CurrentCash, barDate);
            }

            int cashPos = CashCurve.Count - 1;

            //тут применение AdjustmentFactor к текущим результатам
            //cудя по всему CashRate - это процент вывода средств из портфеля за год, а CashAdjustmentFactor - доля вывода в день
            //кажется оно не работает для intraday свечей
            if (tradingSystemExecutor.ApplyInterest &&
                tradingSystemExecutor.PosSize.RawProfitMode == false &&
                CashCurve.Count > 1 &&
                CashCurve.Date[cashPos].Date != CashCurve.Date[cashPos - 1].Date)
            {
                TimeSpan timeSpan = CashCurve.Date[cashPos] - CashCurve.Date[cashPos - 1];
                double adjustmentFactor = 1.0;
                double cash = CashCurve[cashPos];
                if (cash > 0.0)
                {
                    adjustmentFactor = tradingSystemExecutor.CashAdjustmentFactor;
                }
                else if (cash < 0.0)
                {
                    adjustmentFactor = tradingSystemExecutor.MarginAdjustmentFactor;
                }

                for (int i = 1; i <= timeSpan.Days; i++)
                {
                    cash *= adjustmentFactor;
                }

                adjustmentFactor = cash - CashCurve[cashPos];
                if (cash > 0.0)
                {
                    CashReturn += adjustmentFactor;
                }
                else
                {
                    MarginInterest += adjustmentFactor;
                }

                CashCurve[cashPos] = cash;
                EquityCurve[cashPos] += adjustmentFactor;
                CurrentCash = CashCurve[cashPos];
                CurrentEquity = EquityCurve[cashPos];
                currentNetProfit += adjustmentFactor;
            }

            //расчет текущей просадки, чтобы PosSizer мог использовать ее для расчета следующих позиций
            if (posSizer != null)
            {
                if (CurrentEquity > _currentMaxEquity)
                {
                    _currentMaxEquity = CurrentEquity;
                }

                double drawdown = CurrentEquity - _currentMaxEquity;
                double drawdownPercent = drawdown * 100.0 / _currentMaxEquity;
                _drawdownCurve.Add(drawdown, EquityCurve.Date[cashPos]);
                _drawdownPercentCurve.Add(drawdownPercent, EquityCurve.Date[cashPos]);
            }
        }
        while (barIterator.Next());

        //конечная граница
        if (EquityCalcMode != EquityCalcMode.Full &&
            EquityCurve.Count > 0 && EquityCurve.Date[EquityCurve.Date.Count - 1] != barDate)
        {
            EquityCurve.Add(CurrentEquity, barDate);
            CashCurve.Add(CurrentCash, barDate);
        }

        //устранение возможного дубликата в начальной границе, редкая ситуация
        if (EquityCalcMode != EquityCalcMode.Full &&
            EquityCurve.Count > 1 && EquityCurve.Date[0] == EquityCurve.Date[1])
        {
            EquityCurve.RemoveAt(0);
            CashCurve.RemoveAt(0);
        }
    }

    private void ApplyDividents(Position position, int bar, ref double valueToApply)
    {
        if (position.Bars.DivTag == null)
        {
            return;
        }

        IList<FundamentalItem> dividents = (IList<FundamentalItem>)position.Bars.DivTag;
        DateTime barDate = position.Bars.Date[bar].Date;
        DateTime prevBarDate = bar < 1 ? position.Bars.Date[bar].Date.AddYears(-1) : position.Bars.Date[bar - 1].Date;

        foreach (FundamentalItem divident in dividents)
        {
            DateTime divDate = divident.Date;
            if (position.Bars.IsIntraday)
            {
                if (divDate == barDate &&
                    position.Bars.IntradayBarNumber(bar) == 0 &&
                    position.EntryDate.Date != divDate.Date)
                {
                    ApplyDivident(divident, ref valueToApply);
                    break;
                }
            }
            else if (position.Bars.Scale == BarScale.Daily)
            {
                if (divDate == barDate &&
                    position.EntryDate < divDate)
                {
                    ApplyDivident(divident, ref valueToApply);
                    break;
                }
            }
            else //это видимо когда шаг свечи больше дня
            {
                if (divDate <= barDate &&
                    divDate > prevBarDate &&
                    position.EntryDate < divDate &&
                    (position.Active || position.ExitDate >= divDate))
                {
                    ApplyDivident(divident, ref valueToApply);
                    break;
                }
            }
        }

        void ApplyDivident(FundamentalItem divident, ref double valueToApply)
        {
            double divs = divident.Value * position.Shares;
            divs = position.PositionType == PositionType.Short ? -divs : divs;

            CurrentCash += divs;
            valueToApply += divs; //видимо equity, но пока не уверен
            DividendsPaid += divs;

            if (dividents.Count == 0)
            {
                position.Bars.DivTag = null;
            }
        }
    }

    private IList<Bars> FillBarsSet(IList<Bars> sourceBars, TradingSystemExecutor executor)
    {
        //почему-то в исходной реализации к переданным свечам добавляются свечи из позиций
        //не очень понятно в каких случаях могут подмешаться позиции не из исходного датасета
        //но на всякий случай оставлю этот кусок как напоминание

        IList<Bars> barsSet = sourceBars.ToList();
        foreach (Position masterPosition in executor.MasterPositions)
        {
            if (!barsSet.Contains(masterPosition.Bars))
            {
                barsSet.Add(masterPosition.Bars);
            }
        }
        foreach (Position position in Positions)
        {
            if (!barsSet.Contains(position.Bars))
            {
                barsSet.Add(position.Bars);
            }
        }
        return barsSet;
    }

    //method_4
    internal void AddPosition(Position position)
    {
        Positions.Add(position);
    }

    //method_5
    internal void AddAlert(Alert alert)
    {
        Alerts.Add(alert);
    }

    //method_6
    internal void FullClear()
    {
        TradesNSF = 0;
        CashReturn = 0.0;
        MarginInterest = 0.0;
        DividendsPaid = 0.0;
        Positions.Clear();
        Alerts.Clear();

        EquityCurve?.ClearFull();
        CashCurve?.ClearFull();
        OpenPositionCount?.ClearFull();
        _drawdownCurve?.ClearFull();
        _drawdownPercentCurve?.ClearFull();

        _currentlyActivePositions?.Clear();
        _closedPositions?.Clear();
        _accountedPositions?.Clear();
        _currentlyClosedPositions?.Clear();
    }

    //method_7
    internal void Clear(bool avoidClearingEquity)
    {
        Positions.Clear();

        if (!avoidClearingEquity)
        {
            EquityCurve?.ClearFull();
            CashCurve?.ClearFull();
            OpenPositionCount?.ClearFull();
            _drawdownCurve?.ClearFull();
            _drawdownPercentCurve?.ClearFull();

            _currentlyActivePositions?.Clear();
            _closedPositions?.Clear();
            _accountedPositions?.Clear();
            _currentlyClosedPositions?.Clear();
        }
    }

    //method_8
    internal void SortPositions()
    {
        Positions.Sort(this);
    }

    //method_9
    internal void SetPosSizerPositions(PosSizer posSizer)
    {
        if (_accountedPositions == null || _closedPositions == null)
        {
            throw new InvalidOperationException("positions has not been initialized yet");
        }

        posSizer.ActivePositionsProxy = _currentlyActivePositions;
        posSizer.PositionsProxy = _accountedPositions;
        posSizer.ClosedPositionsProxy = _closedPositions;
    }

    //method_0
    internal void CalculateMfeMae()
    {
        foreach (Position position in Positions)
        {
            position.CalculateMfeMae();
        }
    }

    public int Compare(Position first, Position second)
    {
        if (first.EntryDate == second.EntryDate)
        {
            return first.CombinedPriority.CompareTo(second.CombinedPriority);
        }

        return first.EntryDate.CompareTo(second.EntryDate);
    }
}