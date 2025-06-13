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
//думаю не стоит прям отказываться от него так сразу
//upd:
//но с другой стороны появляются накладные расходы на вызов кастомного PosSizer для каждой позиции, что может снизить производительность

public class SystemResultsOwn : IComparer<Position>
{
    private List<Position> _positions = new();
    private IList<Position> _positionsRo;
    private SystemPerformance _systemPerfomance;

    //эти штуки используются только для передачи в PosSizer,
    //если он null (а вроде при PosSizeMode == ScriptOverride он дб null), то можно оптимизнуть и не создавать серии
    //расчет просадок например и не выполняется, если PosSizer null
    private DataSeries _drawdownCurve = new DataSeries("DrawDown");
    private DataSeries _drawdownPercentCurve = new DataSeries("DrawDownPct");
    private double _currentMaxEquity; //for drawdown

    //списки позиций, обрабатывавемые во время итереирования по SynchronizedBarIterator
    private List<Position> _currentlyActivePositions = new(); //открытые на момент итерации (постоянно добавляются и удаляются)
    private List<Position> _closedPositions = new(); //все обработанные и закрытые на момент итерации
    private List<Position> _accountedPositions = new(); //начинающие обрабатываться с текущей итерации и имеющие больше 0 лотов (Shares)

    public int TradesNSF { get; set; }
    public List<Alert> Alerts { get; } = new List<Alert>();

    public DataSeries EquityCurve { get; internal set; } = new DataSeries("Equity");
    public DataSeries CashCurve { get; internal set; } = new DataSeries("Cash");
    internal double CurrentEquity { get; set; }
    internal double CurrentCash { get; set; }

    public double TotalCommission { get; private set; }
    public double CashReturn { get; internal set; }
    public double MarginInterest { get; internal set; }
    public double DividendsPaid { get; internal set; }
    public DataSeries OpenPositionCount { get; set; }

    public IList<Position> Positions => _positionsRo ??= _positions.AsReadOnly();

    public double NetProfit => Positions.Sum(x => x.NetProfit) + CashReturn + MarginInterest + DividendsPaid;
    public double ProfitPerBar => Positions.Count == 0 ? 0.0 : NetProfit / Positions.Sum(x => x.BarsHeld);
    public double AverageProfitAcrossTotalTimeSpan => EquityCurve.Count == 0 ? 0.0 : NetProfit / EquityCurve.Count;

    public double APR
    {
        get
        {
            if (EquityCurve.Count < 2 || EquityCurve[0] == 0.0)
            {
                return 0.0;
            }

            TimeSpan timeSpan = EquityCurve.Date.Last() - EquityCurve.Date.First();
            double startEquity = EquityCurve[0];
            double finalEquity = EquityCurve[EquityCurve.Count - 1];
            return (Math.Pow(finalEquity / startEquity, 365.25 / timeSpan.Days) - 1.0) * 100.0;
        }
    }

    public SystemResultsOwn(SystemPerformance sysPerf)
    {
        _systemPerfomance = sysPerf;
    }

    public int Compare(Position position_0, Position position_1)
    {
        if (position_0.EntryDate == position_1.EntryDate)
        {
            return position_0.CombinedPriority.CompareTo(position_1.CombinedPriority);
        }

        return position_0.EntryDate.CompareTo(position_1.EntryDate);
    }

    //MFE/MAE
    internal void method_0()
    {
        foreach (Position position in Positions)
        {
            position.CalculateMfeMae();
        }
    }

    public void BuildEquityCurve(IList<Bars> barsList, TradingSystemExecutor tradingSystemExecutor, bool callbackToSizePositions, PosSizer posSizer)
    {
        TotalCommission = 0.0;
        EquityCurve = new DataSeries("Equity");
        CashCurve = new DataSeries("Cash");
        _drawdownCurve = new DataSeries("DrawDown");
        _drawdownPercentCurve = new DataSeries("DrawDownPct");
        _currentMaxEquity = double.MinValue;
        PositionSize positionSize = _systemPerfomance.PositionSize;
        double currentNetProfit = 0.0;
        OpenPositionCount = new DataSeries("OpenPositions");

        //заполняется датасет для итерирования
        List<Bars> barsSet = barsList.ToList();

        foreach (Position masterPosition in tradingSystemExecutor.MasterPositions)
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

        //заполняется инфа по дивидендам
        foreach (Bars bars in barsSet)
        {
            if (tradingSystemExecutor.ApplyDividends && !tradingSystemExecutor.PosSize.RawProfitMode)
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

        _currentlyActivePositions.Clear();
        _accountedPositions.Clear();
        _closedPositions.Clear();

        SynchronizedBarIterator barIterator = new SynchronizedBarIterator(barsSet);

        if (barIterator.Date == DateTime.MaxValue)
        {
            return;
        }

        List<Position> remainingPositions = new(); //список всех позиций для обсчета, должны быть отсортированы по дате, при итерировании обсчитанные позиции удаляются, тут как будто бы лучше подошел стек
        List<Position> currentlyClosedPositions = new(); //список закрытых позиций на текущей итерации, очищается по кончанию итерации

        if (callbackToSizePositions)
        {
            foreach (Position masterPosition in tradingSystemExecutor.MasterPositions)
            {
                remainingPositions.Add(masterPosition);
            }
        }
        else
        {
            foreach (Position position in Positions)
            {
                remainingPositions.Add(position);
            }
        }

        if (posSizer != null)
        {
            posSizer.PreInitialize(tradingSystemExecutor, _currentlyActivePositions, _accountedPositions, _closedPositions, EquityCurve, CashCurve, _drawdownCurve, _drawdownPercentCurve);
            posSizer.Initialize();
        }

        CurrentCash = positionSize.RawProfitMode ? 0.0 : positionSize.StartingCapital;
        CurrentEquity = CurrentCash;

        //цикл по SynchronizedBarIterator
        //каждая свеча со всех инструментов, упорядочено по времени
        do
        {
            double netProfitOfCurrentlyClosedPositions = 0.0;

            for (int pos = _currentlyActivePositions.Count - 1; pos >= 0; pos--)
            {
                Position position = _currentlyActivePositions[pos];

                //этот блок кажется нужен для того, чтобы накопился кеш от сделок сделанных по рыночной цене
                //чтобы можно было этот кеш использовать для открытия других позиций на этой свече
                //но кажется это может быть опасно, если вход в позицию делается на открытии например
                if (position.ExitOrderType == OrderType.Market &&
                    position.ExitDate == barIterator.Date && 
                    position.Active == false)
                {
                    _currentlyActivePositions.RemoveAt(pos);
                    currentlyClosedPositions.Add(position);
                    _closedPositions.Add(position);
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
                List<Position> candidates = new();
                foreach (Position position in remainingPositions)
                {
                    if (position.EntryDate == barIterator.Date)
                    {
                        candidates.Add(position);
                    }
                }

                posSizer.CandidatesProxy = candidates;
            }

            double currCash = CurrentCash; //это нужно только для callbackToSizePositions блока

            //цикл по конкурирующим позициям с одинаковой датой входа (текущей датой итератора)
            //корректируются CurrentCash и TotalCommission
            //добавляются позиции в  _currentlyActivePositions и _accountedPositions
            //удаляются позиции из remainingPositions
            while (remainingPositions.Count > 0)
            {
                Position position = remainingPositions[0];
                if (position.EntryDate != barIterator.Date)
                {
                    break;
                }

                //проставляются Shares и Commision на позиции
                if (callbackToSizePositions)
                {
                    //вызывает PosSizer, переданный и сконфигурированный выше, если PosSizeMode == SimuScript
                    //если PosSizeMode == ScriptOverride, то просто использует OverrideShareSize, установленный через WealthScript.SetShareSize перед открытием позиции
                    //но может дополнительно скорректироваться по ReduceQtyBasedOnVolume, RoundLots и пр.
                    //TODO: возможно стоит отказаться от вызова CalcPositionSize в случае ScriptOverride, для производительности

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

                remainingPositions.RemoveAt(0);

                if (position.Shares > 0.0)
                {
                    double num5 = CurrentCash;
                    if (!tradingSystemExecutor.PosSize.RawProfitMode)
                    {
                        double num6 = CurrentEquity - CurrentCash;
                        num5 = CurrentEquity * tradingSystemExecutor.PosSize.MarginFactor - num6;
                    }

                    bool isSufficient = !callbackToSizePositions;
                    if (!isSufficient)
                    {
                        isSufficient = positionSize.RawProfitMode || num5 >= position.Size + position.EntryCommission;
                    }

                    if (isSufficient)
                    {
                        CurrentCash -= position.Size;
                        CurrentCash -= position.EntryCommission;
                        num5 -= position.Size; //тут вроде уже бессмысленно корректировать локальную переменную
                        num5 -= position.EntryCommission;
                        _currentlyActivePositions.Add(position);
                        _accountedPositions.Add(position);
                        TotalCommission += position.EntryCommission + position.ExitCommission;
                    }
                    else
                    {
                        //это похоже обнуление позиции если она не удовлетворяет потрфелю
                        //потом на основе этого проставляется TradesNSF из TradingSystemExecutor
                        position.SharesProxy = 0.0;
                    }
                }
            }

            //тут блок с подсчетом текущей прибыли от открытых на данный момент позиций _currentlyActivePositions
            {
                for (int pos = _currentlyActivePositions.Count - 1; pos >= 0; pos--)
                {
                    Position position = _currentlyActivePositions[pos];

                    if (position.ExitDate == barIterator.Date && 
                        position.Active == false)
                    {
                        _currentlyActivePositions.RemoveAt(pos);
                        currentlyClosedPositions.Add(position);
                        _closedPositions.Add(position);
                        currentNetProfit += position.NetProfit;
                        CurrentCash += position.Size;
                        CurrentCash += position.NetProfit;
                        netProfitOfCurrentlyClosedPositions += position.NetProfit;
                        CurrentCash += position.EntryCommission; //Position.NetProfit учитывает комиссии, поэтому тут компенсация прошлого вычета
                    }
                }

                //_currentlyActivePositions расщепляется на _currentlyActivePositions и currentlyClosedPositions

                CurrentEquity = positionSize.RawProfitMode ? 0.0 : positionSize.StartingCapital;
                foreach (Position position in _currentlyActivePositions)
                {
                    int bar = barIterator.Bar(position.Bars);
                    CurrentEquity += position.NetProfitAsOfBar(bar);
                    ApplyDividents(position, bar, ref currentNetProfit);
                }
                foreach (Position position in currentlyClosedPositions)
                {
                    int bar = barIterator.Bar(position.Bars);
                    CurrentEquity += position.NetProfitAsOfBar(bar);
                    ApplyDividents(position, bar, ref currentNetProfit);
                }

                currentlyClosedPositions.Clear();

                double netProfitBeforeThisBar = currentNetProfit - netProfitOfCurrentlyClosedPositions; //доход от прошлых позиций + дивиденды от текущих
                CurrentEquity += netProfitBeforeThisBar;
                EquityCurve.Add(CurrentEquity, barIterator.Date);
                CashCurve.Add(CurrentCash, barIterator.Date);
                OpenPositionCount.Add(_currentlyActivePositions.Count, barIterator.Date);
            }

            int cashPos = CashCurve.Count - 1;

            //тут применение AdjustmentFactor к текущим результатам
            if (tradingSystemExecutor.ApplyInterest && 
                !tradingSystemExecutor.PosSize.RawProfitMode 
                && CashCurve.Count > 1 
                && CashCurve.Date[cashPos].Date != CashCurve.Date[cashPos - 1].Date)
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

    internal void method_4(Position position_0)
    {
        _positions.Add(position_0);
    }

    internal void method_5(Alert alert_0)
    {
        Alerts.Add(alert_0);
    }

    //полная очистка
    internal void method_6()
    {
        TradesNSF = 0;
        CashReturn = 0.0;
        MarginInterest = 0.0;
        DividendsPaid = 0.0;
        _positions.Clear();
        Alerts.Clear();
        if (EquityCurve != null)
        {
            EquityCurve.ClearValues();
            CashCurve.ClearValues();
            _drawdownCurve.ClearValues();
            _drawdownPercentCurve.ClearValues();
        }
    }

    //очистка
    internal void method_7(bool bool_0)
    {
        _positions.Clear();
        if (!bool_0)
        {
            EquityCurve.ClearValues();
            CashCurve.ClearValues();
            _drawdownCurve.ClearValues();
            _drawdownPercentCurve.ClearValues();
        }
    }

    internal void method_8()
    {
        _positions.Sort(this);
    }

    internal void method_9(PosSizer posSizer_0)
    {
        posSizer_0.ActivePositionsProxy = _currentlyActivePositions;
        posSizer_0.PositionsProxy = _accountedPositions;
        posSizer_0.ClosedPositionsProxy = _closedPositions;
    }
}