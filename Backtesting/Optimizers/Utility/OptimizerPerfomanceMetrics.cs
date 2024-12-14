using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingStrategies.Backtesting.Optimizers.Utility
{
    internal interface IOptimizerPerfomanceMetrics
    {
        void SetTime(string label, long milliseconds, int system, int currentRun);
    }

    internal class OptimizerPerfomanceMetrics : IOptimizerPerfomanceMetrics
    {
        public readonly IList<(string label, long milliseconds)>[] Perfomance;

        public OptimizerPerfomanceMetrics(int systemsCount) : this(systemsCount, 2, 2)
        {
        }

        public OptimizerPerfomanceMetrics(int systemsCount, int runsCount, int labelsCount)
        {
            Perfomance = Enumerable
                .Range(0, systemsCount)
                .Select(x => new List<(string, long)>(runsCount * labelsCount))
                .ToArray();
        }

        public void SetTime(string label, long milliseconds, int system, int currentRun)
        {
            Perfomance[system].Add(($"{label}_{currentRun}", milliseconds));
        }
    }

    internal class MockOptimizerPerfomanceMetrics : IOptimizerPerfomanceMetrics
    {
        public void SetTime(string label, long milliseconds, int system, int currentRun)
        {
        }
    }
}
