using System;

namespace TradingStrategies.Backtesting.Utility
{
    /// <summary>
    /// Represents a double number that may take values only from the [0,1] span.
    /// </summary>
    public struct LogicDouble
    {
        public const double MaxValue = 1;
        public const double MinValue = 0;

        public double Value { get; }

        /// <summary> 
        /// Creates a new instance of the <see cref="LogicDouble"/> struct 
        /// based on the passed double value that should lie in the [0,1] span. 
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Occurs if <param name="value"/> does not lie in the [0,1] span. 
        /// </exception>
        public LogicDouble(double value)
        {
            if (value >= 0 && value <= 1)
                Value = value;
            else
                throw new ArgumentException("LogicDouble value may take values only from the [0,1] span.");
        }

        public static double operator + (LogicDouble left, double right) => left.Value + right;
        public static double operator - (LogicDouble left, double right) => left.Value - right;
        public static implicit operator double (LogicDouble val) => val.Value;
        public static explicit operator LogicDouble(double val)
        {
            try
            {
                return new LogicDouble(val);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(ex.Message, ex);
            }
        }
    }
}
