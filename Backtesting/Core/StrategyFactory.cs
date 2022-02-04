using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingStrategies.Backtesting.Strategies;

namespace TradingStrategies.Backtesting.Core
{
    internal static class StrategyFactory
    {
        public static string StrategyName { get; } = nameof(CoupStrategy);

        public static IStrategyExecuter CreateStrategyInstance(WealthScriptWrapper wrapper)
        {
            return
                new CoupStrategy(wrapper);
        }
    }
}
