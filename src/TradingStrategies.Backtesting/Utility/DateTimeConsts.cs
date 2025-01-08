using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingStrategies.Backtesting.Utility
{
    public static class DateTimeConsts
    {
        public const int MonthsInYear = 12;
        public const int DaysInWeek = 7;
        public const int QuartersInYear = 4;
        public const int MonthsInQuarter = MonthsInYear / QuartersInYear;
    }
}
