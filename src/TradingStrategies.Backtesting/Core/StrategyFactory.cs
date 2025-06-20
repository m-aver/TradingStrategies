﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingStrategies.Backtesting.Strategies;
using TradingStrategies.Backtesting.Tools;

namespace TradingStrategies.Backtesting.Core
{
    internal static class StrategyFactory
    {
        public static string StrategyName { get; } = nameof(CoupStrategy);

        public static IStrategyExecuter CreateStrategyInstance(WealthScriptWrapper wrapper)
        {
            var strategy = CreateStrategyInstanceInternal(wrapper);

            if (strategy is IStrategyResultFilter filter)
            {
                strategy = new FiltrationStrategyDecorator(wrapper, strategy, filter);
            }

            return strategy;
        }

        private static IStrategyExecuter CreateStrategyInstanceInternal(WealthScriptWrapper wrapper)
        {
            if (wrapper is GeneratedWealthScriptWrapper generated)
            {
                var strategy = GeneratedStrategyFactory.CreateStrategyInstance(generated);

                return strategy ?? CreateDefautStrategyInstance(wrapper);
            }

            return CreateDefautStrategyInstance(wrapper);
        }

        private static IStrategyExecuter CreateDefautStrategyInstance(WealthScriptWrapper wrapper)
        {
            return new CoupStrategy(wrapper);
        }
    }
}
