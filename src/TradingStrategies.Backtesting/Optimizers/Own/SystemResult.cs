using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TradingStrategies.Utilities;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Own;

public class SystemResultsOwn : IComparer<Position>
{
    private List<Position> list_0 = new List<Position>();

    private IList<Position> ilist_0;

    private SystemPerformance systemPerformance_0;

    private DataSeries dataSeries_0 = new DataSeries("Equity");

    private DataSeries dataSeries_1 = new DataSeries("Cash");

    private double double_0;

    private double double_1;

    private double double_2;

    private List<Alert> list_1 = new List<Alert>();

    private int int_0;

    private double double_3;

    private double double_4;

    private double double_5;

    private static int int_1 = -1;

    private static Random random_0 = new Random();

    private DataSeries dataSeries_2 = new DataSeries("DrawDown");

    private DataSeries dataSeries_3 = new DataSeries("DrawDownPct");

    private double double_6;

    private List<Position> list_2 = new List<Position>();

    private List<Position> list_3 = new List<Position>();

    private List<Position> list_4 = new List<Position>();

    [CompilerGenerated]
    private DataSeries dataSeries_4;

    public int TradesNSF
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

    public IList<Position> Positions
    {
        get
        {
            if (ilist_0 == null)
            {
                ilist_0 = list_0.AsReadOnly();
            }

            return ilist_0;
        }
    }

    public List<Alert> Alerts => list_1;

    public double NetProfit
    {
        get
        {
            double num = 0.0;
            foreach (Position position in Positions)
            {
                num += position.NetProfit;
            }

            return num + double_3 + double_4 + double_5;
        }
    }

    public double ProfitPerBar
    {
        get
        {
            double num = 0.0;
            double num2 = 0.0;
            foreach (Position position in Positions)
            {
                num2 += position.NetProfit;
                num += position.BarsHeld;
            }

            num2 = num2 + double_3 + double_4 + double_5;
            if (num == 0.0)
            {
                return 0.0;
            }

            return num2 / num;
        }
    }

    public double AverageProfitAcrossTotalTimeSpan
    {
        get
        {
            if (dataSeries_0.Count == 0)
            {
                return 0.0;
            }

            return NetProfit / dataSeries_0.Count;
        }
    }

    public DataSeries EquityCurve
    {
        get
        {
            return dataSeries_0;
        }
        internal set
        {
            dataSeries_0 = value;
        }
    }

    public DataSeries CashCurve
    {
        get
        {
            return dataSeries_1;
        }
        internal set
        {
            dataSeries_1 = value;
        }
    }

    public double APR
    {
        get
        {
            if (EquityCurve.Count < 2)
            {
                return 0.0;
            }

            if (EquityCurve[0] == 0.0)
            {
                return 0.0;
            }

            TimeSpan timeSpan = EquityCurve.Date[EquityCurve.Count - 1] - EquityCurve.Date[0];
            double num = EquityCurve[0];
            double num2 = EquityCurve[EquityCurve.Count - 1];
            return (Math.Pow(num2 / num, 365.25 / timeSpan.Days) - 1.0) * 100.0;
        }
    }

    public double TotalCommission => double_2;

    public double CashReturn
    {
        get
        {
            return double_3;
        }
        internal set
        {
            double_3 = value;
        }
    }

    public double MarginInterest
    {
        get
        {
            return double_4;
        }
        internal set
        {
            double_4 = value;
        }
    }

    public double DividendsPaid
    {
        get
        {
            return double_5;
        }
        internal set
        {
            double_5 = value;
        }
    }

    public DataSeries OpenPositionCount
    {
        [CompilerGenerated]
        get
        {
            return dataSeries_4;
        }
        [CompilerGenerated]
        set
        {
            dataSeries_4 = value;
        }
    }

    private static double _secureCode => DateTime.Now.Add(new TimeSpan(DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second)).ToOADate();

    private static double _secureCodeMin => DateTime.FromOADate(_secureCode).Subtract(new TimeSpan(0, 0, 0, 1)).ToOADate();

    internal double CurrentEquity
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

    internal double CurrentCash
    {
        get
        {
            return double_1;
        }
        set
        {
            double_1 = value;
        }
    }

    public SystemResultsOwn(SystemPerformance sysPerf)
    {
        systemPerformance_0 = sysPerf;
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
        double_2 = 0.0;
        dataSeries_0 = new DataSeries("Equity");
        dataSeries_1 = new DataSeries("Cash");
        dataSeries_2 = new DataSeries("DrawDown");
        dataSeries_3 = new DataSeries("DrawDownPct");
        double_6 = double.MinValue;
        PositionSize positionSize = systemPerformance_0.PositionSize;
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

        list_2.Clear();
        list_4.Clear();
        list_3.Clear();
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
            posSizer.method_0(tradingSystemExecutor_0, list_2, list_4, list_3, dataSeries_0, dataSeries_1, dataSeries_2, dataSeries_3);
            posSizer.Initialize();
        }

        double_1 = positionSize.RawProfitMode ? 0.0 : positionSize.StartingCapital;
        double_0 = double_1;
        method_1(tradingSystemExecutor_0);
        do
        {
            double num = 0.0;
            for (int num2 = list_2.Count - 1; num2 >= 0; num2--)
            {
                Position position = list_2[num2];
                if (!position.Active && position.ExitDate == val.Date && position.ExitOrderType == OrderType.Market)
                {
                    list_2.RemoveAt(num2);
                    list4.Add(position);
                    list_3.Add(position);
                    double_ += position.NetProfit;
                    double_1 += position.Size;
                    double_1 += position.NetProfit;
                    num += position.NetProfit;
                    double_1 += position.EntryCommission;
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

            double num3 = double_1;
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
                    double num5 = double_1;
                    if (!tradingSystemExecutor_0.PosSize.RawProfitMode)
                    {
                        double num6 = double_0 - double_1;
                        num5 = double_0 * tradingSystemExecutor_0.PosSize.MarginFactor - num6;
                    }

                    bool flag;
                    if (!(flag = !callbackToSizePositions))
                    {
                        flag = positionSize.RawProfitMode || num5 >= position2.Size + position2.EntryCommission;
                    }

                    if (flag)
                    {
                        double_1 -= position2.Size;
                        double_1 -= position2.EntryCommission;
                        num5 -= position2.Size;
                        num5 -= position2.EntryCommission;
                        list_2.Add(position2);
                        list_4.Add(position2);
                        double_2 += position2.EntryCommission + position2.ExitCommission;
                    }
                    else
                    {
                        position2.Shares = 0.0;
                    }
                }
            }

            for (int num7 = list_2.Count - 1; num7 >= 0; num7--)
            {
                Position position3 = list_2[num7];
                if (!position3.Active && position3.ExitDate == val.Date)
                {
                    list_2.RemoveAt(num7);
                    list4.Add(position3);
                    list_3.Add(position3);
                    double_ += position3.NetProfit;
                    double_1 += position3.Size;
                    double_1 += position3.NetProfit;
                    num += position3.NetProfit;
                    double_1 += position3.EntryCommission;
                }
            }

            double_0 = positionSize.RawProfitMode ? 0.0 : positionSize.StartingCapital;
            foreach (Position item3 in list_2)
            {
                int num8 = val.Bar(item3.Bars);
                double_0 += item3.NetProfitAsOfBar(num8);
                method_3(item3, num8, ref double_);
            }

            foreach (Position item4 in list4)
            {
                int num9 = val.Bar(item4.Bars);
                double_0 += item4.NetProfitAsOfBar(num9);
                method_3(item4, num9, ref double_);
            }

            list4.Clear();
            double_0 += double_ - num;
            dataSeries_0.Add(double_0, val.Date);
            dataSeries_1.Add(double_1, val.Date);
            OpenPositionCount.Add(list_2.Count, val.Date);
            int num10 = dataSeries_1.Count - 1;
            if (tradingSystemExecutor_0.ApplyInterest && !tradingSystemExecutor_0.PosSize.RawProfitMode && dataSeries_1.Count > 1 && dataSeries_1.Date[num10].Date != dataSeries_1.Date[num10 - 1].Date)
            {
                TimeSpan timeSpan = dataSeries_1.Date[num10] - dataSeries_1.Date[num10 - 1];
                double num11 = 1.0;
                double num12 = dataSeries_1[num10];
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

                num11 = num12 - dataSeries_1[num10];
                if (num12 > 0.0)
                {
                    CashReturn += num11;
                }
                else
                {
                    MarginInterest += num11;
                }

                dataSeries_1[num10] = num12;
                dataSeries_0[num10] += num11;
                double_1 = dataSeries_1[num10];
                double_0 = dataSeries_0[num10];
                double_ += num11;
            }

            if (posSizer != null)
            {
                if (double_0 > double_6)
                {
                    double_6 = double_0;
                }

                double num13 = double_0 - double_6;
                double value = num13 * 100.0 / double_6;
                dataSeries_2.Add(num13, dataSeries_0.Date[num10]);
                dataSeries_3.Add(value, dataSeries_0.Date[num10]);
            }
        }
        while (val.Next());
    }

    private void method_1(TradingSystemExecutor tradingSystemExecutor_0)
    {
        if (tradingSystemExecutor_0.TNP < _secureCodeMin)
        {
            double_0 *= tradingSystemExecutor_0.TNPAdjustment;
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

                    double_1 += num;
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

                    double_1 += num2;
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

                double_1 += num3;
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
        list_0.Add(position_0);
    }

    internal void method_5(Alert alert_0)
    {
        list_1.Add(alert_0);
    }

    internal void method_6()
    {
        int_0 = 0;
        double_3 = 0.0;
        double_4 = 0.0;
        double_5 = 0.0;
        list_0.Clear();
        list_1.Clear();
        if (dataSeries_0 != null)
        {
            dataSeries_0.method_2();
            dataSeries_1.method_2();
            dataSeries_2.method_2();
            dataSeries_3.method_2();
        }
    }

    internal void method_7(bool bool_0)
    {
        list_0.Clear();
        if (!bool_0)
        {
            dataSeries_0.method_2();
            dataSeries_1.method_2();
            dataSeries_2.method_2();
            dataSeries_3.method_2();
        }
    }

    internal void method_8()
    {
        list_0.Sort(this);
    }

    internal void method_9(PosSizer posSizer_0)
    {
        posSizer_0.ActivePositions = list_2;
        posSizer_0.Positions = list_4;
        posSizer_0.ClosedPositions = list_3;
    }
}