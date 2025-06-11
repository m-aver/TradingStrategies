using System;
using System.Collections.Generic;
using TradingStrategies.Utilities;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Own;

public class SystemResultsOwn : IComparer<Position>
{
    private List<Position> _positions = new List<Position>();
    private IList<Position> _positionsRo;
    private SystemPerformance _systemPerfomance;
    private static int int_1 = -1;
    private static Random random_0 = new Random();
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

    private static double _secureCode => DateTime.Now.Add(new TimeSpan(DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second)).ToOADate();
    private static double _secureCodeMin => DateTime.FromOADate(_secureCode).Subtract(new TimeSpan(0, 0, 0, 1)).ToOADate();

    public SystemResultsOwn(SystemPerformance sysPerf)
    {
        _systemPerfomance = sysPerf;
        if (int_1 == -1)
        {
            int_1 = random_0.Next(100);
        }
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

    public void BuildEquityCurve(IList<Bars> barsList, TradingSystemExecutor tradingSystemExecutor_0, bool callbackToSizePositions, PosSizer posSizer)
    {
        //IL_020d: Unknown result type (might be due to invalid IL or missing references)
        //IL_0214: Expected O, but got Unknown
        method_2(tradingSystemExecutor_0);
        TotalCommission = 0.0;
        EquityCurve = new DataSeries("Equity");
        CashCurve = new DataSeries("Cash");
        _drawdownCurve = new DataSeries("DrawDown");
        _drawdownPercentCurve = new DataSeries("DrawDownPct");
        _currentMaxEquity = double.MinValue;
        PositionSize positionSize = _systemPerfomance.PositionSize;
        double double_ = 0.0;
        OpenPositionCount = new DataSeries("OpenPositions");
        List<Bars> list = new List<Bars>();
        foreach (Bars bars in barsList)
        {
            list.Add(bars);
        }

        foreach (Position masterPosition in tradingSystemExecutor_0.MasterPositions)
        {
            if (!list.Contains(masterPosition.Bars))
            {
                list.Add(masterPosition.Bars);
            }
        }

        foreach (Position position4 in Positions)
        {
            if (!list.Contains(position4.Bars))
            {
                list.Add(position4.Bars);
            }
        }

        foreach (Bars item in list)
        {
            if (!tradingSystemExecutor_0.PosSize.RawProfitMode && tradingSystemExecutor_0.ApplyDividends)
            {
                IList<FundamentalItem> list2 = tradingSystemExecutor_0.FundamentalsLoader.RequestSymbolItems(item, item.Symbol, tradingSystemExecutor_0.DividendItemName);
                if (list2 == null)
                {
                    item.DivTag = null;
                }
                else if (list2.Count == 0)
                {
                    item.DivTag = null;
                }
                else
                {
                    item.DivTag = list2;
                }
            }
            else
            {
                item.DivTag = null;
            }
        }

        _currentPositionsPs.Clear();
        _positionsPs.Clear();
        _closedPositionsPs.Clear();
        SynchronizedBarIterator val = new SynchronizedBarIterator((ICollection<Bars>)list);
        if (!(val.Date != DateTime.MaxValue))
        {
            return;
        }

        List<Position> list3 = new List<Position>();
        List<Position> list4 = new List<Position>();
        if (callbackToSizePositions)
        {
            foreach (Position masterPosition2 in tradingSystemExecutor_0.MasterPositions)
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
            posSizer.method_0(tradingSystemExecutor_0, _currentPositionsPs, _positionsPs, _closedPositionsPs, EquityCurve, CashCurve, _drawdownCurve, _drawdownPercentCurve);
            posSizer.Initialize();
        }

        CurrentCash = positionSize.RawProfitMode ? 0.0 : positionSize.StartingCapital;
        CurrentEquity = CurrentCash;
        method_1(tradingSystemExecutor_0);
        do
        {
            double num = 0.0;
            for (int num2 = _currentPositionsPs.Count - 1; num2 >= 0; num2--)
            {
                Position position = _currentPositionsPs[num2];
                if (!position.Active && position.ExitDate == val.Date && position.ExitOrderType == OrderType.Market)
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
                    if (item2.EntryDate == val.Date)
                    {
                        list5.Add(item2);
                    }
                }

                posSizer.Candidates = list5;
            }

            double num3 = CurrentCash;
            while (list3.Count > 0)
            {
                Position position2 = list3[0];
                if (!(position2.EntryDate == val.Date))
                {
                    break;
                }

                if (callbackToSizePositions)
                {
                    position2.Shares = tradingSystemExecutor_0.CalcPositionSize(position2, position2.Bars, position2.EntryBar, position2.BasisPrice, position2.PositionType, position2.RiskStopLevel, useOverRide: true, position2.OverrideShareSize, num3) * position2.SplitFactor;
                    double num4 = position2.Shares * position2.EntryPrice + position2.EntryCommission;
                    num3 -= num4;
                    if (tradingSystemExecutor_0.Commission != null && tradingSystemExecutor_0.ApplyCommission)
                    {
                        position2.EntryCommission = tradingSystemExecutor_0.Commission.Calculate(position2.PositionType != 0 ? TradeType.Short : TradeType.Buy, position2.EntryOrderType, position2.EntryPrice, position2.Shares, position2.Bars);
                        if (!position2.Active)
                        {
                            position2.ExitCommission = tradingSystemExecutor_0.Commission.Calculate(position2.PositionType == PositionType.Long ? TradeType.Sell : TradeType.Cover, position2.ExitOrderType, position2.ExitPrice, position2.Shares, position2.Bars);
                        }
                    }
                }

                list3.RemoveAt(0);
                if (position2.Shares > 0.0)
                {
                    double num5 = CurrentCash;
                    if (!tradingSystemExecutor_0.PosSize.RawProfitMode)
                    {
                        double num6 = CurrentEquity - CurrentCash;
                        num5 = CurrentEquity * tradingSystemExecutor_0.PosSize.MarginFactor - num6;
                    }

                    bool flag;
                    if (!(flag = !callbackToSizePositions))
                    {
                        flag = positionSize.RawProfitMode || num5 >= position2.Size + position2.EntryCommission;
                    }

                    if (flag)
                    {
                        CurrentCash -= position2.Size;
                        CurrentCash -= position2.EntryCommission;
                        num5 -= position2.Size;
                        num5 -= position2.EntryCommission;
                        _currentPositionsPs.Add(position2);
                        _positionsPs.Add(position2);
                        TotalCommission += position2.EntryCommission + position2.ExitCommission;
                    }
                    else
                    {
                        position2.Shares = 0.0;
                    }
                }
            }

            for (int num7 = _currentPositionsPs.Count - 1; num7 >= 0; num7--)
            {
                Position position3 = _currentPositionsPs[num7];
                if (!position3.Active && position3.ExitDate == val.Date)
                {
                    _currentPositionsPs.RemoveAt(num7);
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
                int num8 = val.Bar(item3.Bars);
                CurrentEquity += item3.NetProfitAsOfBar(num8);
                method_3(item3, num8, ref double_);
            }

            foreach (Position item4 in list4)
            {
                int num9 = val.Bar(item4.Bars);
                CurrentEquity += item4.NetProfitAsOfBar(num9);
                method_3(item4, num9, ref double_);
            }

            list4.Clear();
            CurrentEquity += double_ - num;
            EquityCurve.Add(CurrentEquity, val.Date);
            CashCurve.Add(CurrentCash, val.Date);
            OpenPositionCount.Add(_currentPositionsPs.Count, val.Date);
            int num10 = CashCurve.Count - 1;
            if (tradingSystemExecutor_0.ApplyInterest && !tradingSystemExecutor_0.PosSize.RawProfitMode && CashCurve.Count > 1 && CashCurve.Date[num10].Date != CashCurve.Date[num10 - 1].Date)
            {
                TimeSpan timeSpan = CashCurve.Date[num10] - CashCurve.Date[num10 - 1];
                double num11 = 1.0;
                double num12 = CashCurve[num10];
                if (num12 > 0.0)
                {
                    num11 = tradingSystemExecutor_0.CashAdjustmentFactor;
                }
                else if (num12 < 0.0)
                {
                    num11 = tradingSystemExecutor_0.MarginAdjustmentFactor;
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
        while (val.Next());
    }

    private void method_1(TradingSystemExecutor tradingSystemExecutor_0)
    {
        if (tradingSystemExecutor_0.TNP < _secureCodeMin)
        {
            CurrentEquity *= tradingSystemExecutor_0.TNPAdjustment;
        }
    }

    private void method_2(TradingSystemExecutor tradingSystemExecutor_0)
    {
        if (!(tradingSystemExecutor_0.TNP < _secureCodeMin))
        {
            return;
        }

        int num = random_0.Next(100);
        bool flag = false;
        if (int_1 == 0)
        {
            flag = true;
        }
        else if (num == 66)
        {
            if (int_1 < 1)
            {
                flag = true;
            }
            else
            {
                int_1--;
            }
        }

        while (flag)
        {
        }
    }

    private void method_3(Position position_0, int int_2, ref double double_7)
    {
        if (position_0.Bars.DivTag == null)
        {
            return;
        }

        IList<FundamentalItem> list = (IList<FundamentalItem>)position_0.Bars.DivTag;
        DateTime date = position_0.Bars.Date[int_2].Date;
        DateTime dateTime = int_2 < 1 ? position_0.Bars.Date[int_2].Date.AddYears(-1) : position_0.Bars.Date[int_2 - 1].Date;
        foreach (FundamentalItem item in list)
        {
            DateTime date2 = item.Date;
            if (position_0.Bars.IsIntraday)
            {
                if (date2 == date && position_0.Bars.IntradayBarNumber(int_2) == 0 && position_0.EntryDate.Date != date2.Date)
                {
                    double num = item.Value * position_0.Shares;
                    if (position_0.PositionType == PositionType.Short)
                    {
                        num = 0.0 - num;
                    }

                    CurrentCash += num;
                    double_7 += num;
                    DividendsPaid += num;
                    if (list.Count == 0)
                    {
                        position_0.Bars.DivTag = null;
                    }

                    break;
                }
            }
            else if (position_0.Bars.Scale == BarScale.Daily)
            {
                if (date2 == date && position_0.EntryDate < date2)
                {
                    double num2 = item.Value * position_0.Shares;
                    if (position_0.PositionType == PositionType.Short)
                    {
                        num2 = 0.0 - num2;
                    }

                    CurrentCash += num2;
                    double_7 += num2;
                    DividendsPaid += num2;
                    if (list.Count == 0)
                    {
                        position_0.Bars.DivTag = null;
                    }

                    break;
                }
            }
            else if (date2 <= date && date2 > dateTime && position_0.EntryDate < date2 && (position_0.Active || position_0.ExitDate >= date2))
            {
                double num3 = item.Value * position_0.Shares;
                if (position_0.PositionType == PositionType.Short)
                {
                    num3 = 0.0 - num3;
                }

                CurrentCash += num3;
                double_7 += num3;
                DividendsPaid += num3;
                if (list.Count == 0)
                {
                    position_0.Bars.DivTag = null;
                }

                break;
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