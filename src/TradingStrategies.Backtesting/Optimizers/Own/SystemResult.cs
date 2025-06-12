using System;
using System.Collections.Generic;
using TradingStrategies.Utilities;
using WealthLab;
using SynchronizedBarIterator = TradingStrategies.Utilities.SynchronizedBarIterator;

namespace TradingStrategies.Backtesting.Optimizers.Own;

//вообще идея PosSizer видимо в том, чтобы отделить логику торговых сигналов от размера позиций
//удобно когда торги ведутся одновременно по множеству бумаг/систем
//в конкретной системе можно не заморачиваться и не считать текущую многосоставную эквити, чтобы посчитать размер позиции
//а задавать размер уже постфактум, в отдельном модуле (своя перегрузка PosSizer или готовый), основываясь на текущей эквити (или других метриках), которую передат фреймворк
//думаю не стоит прям отказываться от него так сразу

public class SystemResultsOwn : IComparer<Position>
{
    private List<Position> _positions = new List<Position>();
    private IList<Position> _positionsRo;
    private SystemPerformance _systemPerfomance;

    //эти штуки используются только для передачи в PosSizer,
    //если он null (а вроде при PosSizeMode == ScriptOverride он дб null), то можно оптимизнуть и не создавать серии
    //расчет просадок например и не выполняется, если PosSizer null
    private DataSeries _drawdownCurve = new DataSeries("DrawDown");
    private DataSeries _drawdownPercentCurve = new DataSeries("DrawDownPct");
    private double _currentMaxEquity; //for drawdown

    private List<Position> _currentPositionsPs = new List<Position>();
    private List<Position> _closedPositionsPs = new List<Position>();
    private List<Position> _positionsPs = new List<Position>();

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
            position.method_2();
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
        double double_ = 0.0;
        OpenPositionCount = new DataSeries("OpenPositions");
        List<Bars> barsSet = barsList.ToList();

        foreach (Position masterPosition in tradingSystemExecutor.MasterPositions)
        {
            if (!barsSet.Contains(masterPosition.Bars))
            {
                barsSet.Add(masterPosition.Bars);
            }
        }

        foreach (Position position4 in Positions)
        {
            if (!barsSet.Contains(position4.Bars))
            {
                barsSet.Add(position4.Bars);
            }
        }

        foreach (Bars bars in barsSet)
        {
            if (!tradingSystemExecutor.PosSize.RawProfitMode && tradingSystemExecutor.ApplyDividends)
            {
                IList<FundamentalItem> dividents = 
                    tradingSystemExecutor.FundamentalsLoader.RequestSymbolItems(bars, bars.Symbol, tradingSystemExecutor.DividendItemName);

                if (dividents == null)
                {
                    bars.DivTag = null;
                }
                else if (dividents.Count == 0)
                {
                    bars.DivTag = null;
                }
                else
                {
                    bars.DivTag = dividents;
                }
            }
            else
            {
                bars.DivTag = null;
            }
        }

        _currentPositionsPs.Clear();
        _positionsPs.Clear();
        _closedPositionsPs.Clear();
        SynchronizedBarIterator barIterator = new SynchronizedBarIterator(barsSet);
        if (!(barIterator.Date != DateTime.MaxValue))
        {
            return;
        }

        List<Position> list3 = new List<Position>();
        List<Position> list4 = new List<Position>();
        if (callbackToSizePositions)
        {
            foreach (Position masterPosition2 in tradingSystemExecutor.MasterPositions)
            {
                list3.Add(masterPosition2);
            }
        }
        else
        {
            foreach (Position position5 in Positions)
            {
                list3.Add(position5);
            }
        }

        if (posSizer != null)
        {
            posSizer.method_0(tradingSystemExecutor, _currentPositionsPs, _positionsPs, _closedPositionsPs, EquityCurve, CashCurve, _drawdownCurve, _drawdownPercentCurve);
            posSizer.Initialize();
        }

        CurrentCash = positionSize.RawProfitMode ? 0.0 : positionSize.StartingCapital;
        CurrentEquity = CurrentCash;

        //цикл по SynchronizedBarIterator
        do
        {
            double num = 0.0;
            for (int num2 = _currentPositionsPs.Count - 1; num2 >= 0; num2--)
            {
                Position position = _currentPositionsPs[num2];
                if (!position.Active && position.ExitDate == barIterator.Date && position.ExitOrderType == OrderType.Market)
                {
                    _currentPositionsPs.RemoveAt(num2);
                    list4.Add(position);
                    _closedPositionsPs.Add(position);
                    double_ += position.NetProfit;
                    CurrentCash += position.Size;
                    CurrentCash += position.NetProfit;
                    num += position.NetProfit;
                    CurrentCash += position.EntryCommission;
                }
            }

            if (posSizer != null)
            {
                List<Position> list5 = new List<Position>();
                foreach (Position item2 in list3)
                {
                    if (item2.EntryDate == barIterator.Date)
                    {
                        list5.Add(item2);
                    }
                }

                posSizer.Candidates = list5;
            }

            //цикл по конкурирующим позициям с одинаковой датой входа (текущей датой итератора)
            double cash = CurrentCash;
            while (list3.Count > 0)
            {
                Position position = list3[0];
                if (!(position.EntryDate == barIterator.Date))
                {
                    break;
                }

                if (callbackToSizePositions)
                {
                    //вызывает PosSizer, переданный и сконфигурированный выше, если PosSizeMode == SimuScript
                    //если PosSizeMode == ScriptOverride, то просто использует OverrideShareSize, установленный через WealthScript.SetShareSize перед открытием позиции
                    //но может дополнительно скорректироваться по ReduceQtyBasedOnVolume, RoundLots и пр.
                    var sharesSize = tradingSystemExecutor.CalcPositionSize(position, position.Bars, position.EntryBar, position.BasisPrice, position.PositionType, position.RiskStopLevel, useOverRide: true, position.OverrideShareSize, cash);
                    position.Shares = sharesSize * position.SplitFactor;

                    double positionPrice = position.Shares * position.EntryPrice + position.EntryCommission;
                    cash -= positionPrice;

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

                list3.RemoveAt(0);
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
                        num5 -= position.Size;
                        num5 -= position.EntryCommission;
                        _currentPositionsPs.Add(position);
                        _positionsPs.Add(position);
                        TotalCommission += position.EntryCommission + position.ExitCommission;
                    }
                    else
                    {
                        //это похоже обнуление позиции если она не удовлетворяет потрфелю
                        //потом на основе этого проставляется TradesNSF из TradingSystemExecutor
                        position.Shares = 0.0;
                    }
                }
            }

            for (int pos = _currentPositionsPs.Count - 1; pos >= 0; pos--)
            {
                Position position3 = _currentPositionsPs[pos];
                if (!position3.Active && position3.ExitDate == barIterator.Date)
                {
                    _currentPositionsPs.RemoveAt(pos);
                    list4.Add(position3);
                    _closedPositionsPs.Add(position3);
                    double_ += position3.NetProfit;
                    CurrentCash += position3.Size;
                    CurrentCash += position3.NetProfit;
                    num += position3.NetProfit;
                    CurrentCash += position3.EntryCommission;
                }
            }

            CurrentEquity = positionSize.RawProfitMode ? 0.0 : positionSize.StartingCapital;
            foreach (Position item3 in _currentPositionsPs)
            {
                int num8 = barIterator.Bar(item3.Bars);
                CurrentEquity += item3.NetProfitAsOfBar(num8);
                ApplyDividents(item3, num8, ref double_);
            }

            foreach (Position item4 in list4)
            {
                int num9 = barIterator.Bar(item4.Bars);
                CurrentEquity += item4.NetProfitAsOfBar(num9);
                ApplyDividents(item4, num9, ref double_);
            }

            list4.Clear();
            CurrentEquity += double_ - num;
            EquityCurve.Add(CurrentEquity, barIterator.Date);
            CashCurve.Add(CurrentCash, barIterator.Date);
            OpenPositionCount.Add(_currentPositionsPs.Count, barIterator.Date);
            int num10 = CashCurve.Count - 1;
            if (tradingSystemExecutor.ApplyInterest && !tradingSystemExecutor.PosSize.RawProfitMode && CashCurve.Count > 1 && CashCurve.Date[num10].Date != CashCurve.Date[num10 - 1].Date)
            {
                TimeSpan timeSpan = CashCurve.Date[num10] - CashCurve.Date[num10 - 1];
                double num11 = 1.0;
                double num12 = CashCurve[num10];
                if (num12 > 0.0)
                {
                    num11 = tradingSystemExecutor.CashAdjustmentFactor;
                }
                else if (num12 < 0.0)
                {
                    num11 = tradingSystemExecutor.MarginAdjustmentFactor;
                }

                for (int i = 1; i <= timeSpan.Days; i++)
                {
                    num12 *= num11;
                }

                num11 = num12 - CashCurve[num10];
                if (num12 > 0.0)
                {
                    CashReturn += num11;
                }
                else
                {
                    MarginInterest += num11;
                }

                CashCurve[num10] = num12;
                EquityCurve[num10] += num11;
                CurrentCash = CashCurve[num10];
                CurrentEquity = EquityCurve[num10];
                double_ += num11;
            }

            if (posSizer != null)
            {
                if (CurrentEquity > _currentMaxEquity)
                {
                    _currentMaxEquity = CurrentEquity;
                }

                double num13 = CurrentEquity - _currentMaxEquity;
                double value = num13 * 100.0 / _currentMaxEquity;
                _drawdownCurve.Add(num13, EquityCurve.Date[num10]);
                _drawdownPercentCurve.Add(value, EquityCurve.Date[num10]);
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
            EquityCurve.method_2();
            CashCurve.method_2();
            _drawdownCurve.method_2();
            _drawdownPercentCurve.method_2();
        }
    }

    //очистка
    internal void method_7(bool bool_0)
    {
        _positions.Clear();
        if (!bool_0)
        {
            EquityCurve.method_2();
            CashCurve.method_2();
            _drawdownCurve.method_2();
            _drawdownPercentCurve.method_2();
        }
    }

    internal void method_8()
    {
        _positions.Sort(this);
    }

    internal void method_9(PosSizer posSizer_0)
    {
        posSizer_0.ActivePositions = _currentPositionsPs;
        posSizer_0.Positions = _positionsPs;
        posSizer_0.ClosedPositions = _closedPositionsPs;
    }
}