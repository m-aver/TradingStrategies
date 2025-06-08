using System;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Utility
{
    //uses decimals for accuracy iterating
    //and provides methods for manage iterating
    internal class AccuracyStrategyParameter
    {
        public decimal DefaultValue { get; private set; }
        public decimal Value { get; private set; }
        public decimal Start { get; private set; }
        public decimal Stop { get; private set; }
        public decimal Step { get; private set; }

        public bool IsEnabled => _nativeParameter.IsEnabled;

        private readonly StrategyParameter _nativeParameter;

        public AccuracyStrategyParameter(StrategyParameter nativeParameter)
        {
            _nativeParameter = nativeParameter;

            DefaultValue = Convert.ToDecimal(nativeParameter.DefaultValue);
            Value = Convert.ToDecimal(nativeParameter.Value);
            Start = Convert.ToDecimal(nativeParameter.Start);
            Stop = Convert.ToDecimal(nativeParameter.Stop);
            Step = Convert.ToDecimal(nativeParameter.Step);
        }

        public long StepsRetain => IsEnabled ? Math.Max(0, Convert.ToInt64(Math.Ceiling((Stop - Value) / Step))) : 0;

        public bool MoveValue()
        {
            if (!IsEnabled || Step == 0)
            {
                return false;
            }
            //else if (Step > 0 ? Value < Stop : Value > Stop)
            else if (Step > 0 ? Value <= Stop - Step : Value >= Stop - Step)    //костыль на самом деле, итератор бы править
            {
                Value += Step;
                return true;
            }
            else
            {
                Value = Stop;
                return false;
            }
        }

        public void ResetValue()
        {
            Value = IsEnabled ? Start : DefaultValue;
        }

        public void UpdateNative()
        {
            _nativeParameter.Value = Convert.ToDouble(Value);
        }

        public static explicit operator StrategyParameter(AccuracyStrategyParameter param)
        {
            return param._nativeParameter;
        }
    }
}
