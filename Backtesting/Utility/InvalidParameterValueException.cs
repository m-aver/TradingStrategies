using System;
using System.Runtime.Serialization;

namespace TradingStrategies.Backtesting.Utility
{
    /// <summary>
    /// Represents exception that occurs when a Strategy Parameter takes an incorrect value.
    /// </summary>
    [Serializable]
    class InvalidParameterValueException : Exception
    {
        public string ParameterName { get; }
        public double ParameterValue { get; }

        public InvalidParameterValueException(string parameterName, double parameterValue)
        {
            ParameterName = parameterName;
            ParameterValue = parameterValue;
        }
        public InvalidParameterValueException(string parameterName, double parameterValue, 
            string message) : base(message)
        {
            ParameterName = parameterName;
            ParameterValue = parameterValue;
        }
        public InvalidParameterValueException(string parameterName, double parameterValue, 
            string message, Exception innerException) : base(message, innerException)
        {
            ParameterName = parameterName;
            ParameterValue = parameterValue;
        }
        protected InvalidParameterValueException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

        public override string ToString()
        {
            return 
                $"Parameter <{ParameterName}> was assigned with an incorrect value: {ParameterValue}";
        }
    }
}
