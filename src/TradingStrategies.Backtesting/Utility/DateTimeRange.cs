using System.Globalization;
using System.Text;

namespace TradingStrategies.Backtesting.Utility
{
    public readonly partial struct DateTimeRange
    {
        public DateTime DateTime { get; }
        public TimeSpan Offset { get; }
        public DateTime EndDateTime { get; }
        public DateTimeKind Kind { get; }

        public DateTimeRange(DateTime dateTime, TimeSpan offset)
        {
            DateTime = dateTime;
            Offset = offset;
            EndDateTime = dateTime + offset;
        }

        public DateTimeRange(DateTime dateTime, DateTime endDateTime)
        {
            if (dateTime > endDateTime)
            {
                throw new ArgumentException($"{nameof(endDateTime)} must be greater than {nameof(dateTime)}");
            }
            if (dateTime.Kind != endDateTime.Kind)
            {
                throw new ArgumentException($"{nameof(endDateTime)} and {nameof(dateTime)} must have same kind");
            }
            
            DateTime = dateTime;
            Offset = endDateTime - dateTime;
            EndDateTime = endDateTime;
            Kind = endDateTime.Kind;
        }

        public bool IsWithin(DateTime date)
        {
            return
                date >= this.DateTime &&
                date < this.EndDateTime;
        }

        public DateTimeRange Shift(long ticks)
        {
            var shiftedStart = new DateTime(DateTime.Ticks + ticks, Kind);
            var shiftedEnd = new DateTime(EndDateTime.Ticks + ticks, Kind);
            return new DateTimeRange(shiftedStart, shiftedEnd);
        }

        //for tests, debug visualizing
        public override string ToString()
        {
            var start = DateTime.ToString();
            var end = EndDateTime.ToString();
            var str = start + Separator + end;
            return str;
        }
    }

    //factory
    public readonly partial struct DateTimeRange
    {
        public const char Separator = '-';

        public static DateTimeRange Parse(string str)
        {
            var dates = str.Split(Separator).Select(s => s.Trim()).ToArray();

            if (dates.Length != 2)
            {
                throw new ArgumentException($"invalid {nameof(DateTimeRange)} string representation: {str}", nameof(str));
            }

            var start = DateTime.Parse(dates[0]);
            var end = DateTime.Parse(dates[1]);

            return new DateTimeRange(start, end);
        }
    }
}
