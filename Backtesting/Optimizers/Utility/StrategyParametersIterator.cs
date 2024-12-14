using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Utility
{
    //iterate througth parameters values
    public interface IStrategyParametersIterator
    {
        IEnumerable<StrategyParameter> CurrentParameters { get; }
        bool MoveNext();
        void Reset();
    }

    //iterate for each combination of parameter values
    internal class StrategyParametersIterator : IStrategyParametersIterator
    {
        private readonly AccuracyStrategyParameter[] parameters;

        public StrategyParametersIterator(IReadOnlyCollection<StrategyParameter> parameters)
        {
            this.parameters = parameters.Select(p => new AccuracyStrategyParameter(p)).ToArray();
        }

        public IEnumerable<StrategyParameter> CurrentParameters => parameters.Select(x => (StrategyParameter)x);

        public bool MoveNext()
        {
            var moved = SetNextRunParameters(0);
            UpdateParameters();
            return moved;
        }

        public void Reset()
        {
            ResetParameters();
            UpdateParameters();
        }

        private void ResetParameters()
        {
            foreach (var parameter in parameters)
            {
                parameter.ResetValue();
            }
        }

        private void UpdateParameters()
        {
            foreach (var parameter in parameters)
            {
                parameter.UpdateNative();
            }
        }

        private bool SetNextRunParameters(int currentParam)
        {
            if (currentParam >= parameters.Length)
            {
                return false;
            }

            var current = parameters[currentParam];

            if (!current.IsEnabled)
            {
                return SetNextRunParameters(currentParam + 1);
            }

            if (!current.MoveValue())
            {
                current.ResetValue();
                return SetNextRunParameters(currentParam + 1);
            }

            return true;
        }
    }

    internal static class StrategyParametersIteratorExtensions
    {
        public static int RunsCount(this IStrategyParametersIterator iterator)
        {
            var count = 0;
            while (iterator.MoveNext())
            {
                count++;
            }
            return count;
        }
    }
}
