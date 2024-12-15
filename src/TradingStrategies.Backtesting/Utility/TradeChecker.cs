using System;
using System.Collections.Generic;
using System.Linq;
using WealthLab;

namespace TradingStrategies.Backtesting.Utility
{
    /// <summary>
    /// Contains trade filtration logic based on the security prices.
    /// </summary>
    internal abstract class TradeChecker
    {
        public Bars Bars { get; }

        public TradeChecker(Bars bars)
        {
            Bars = bars;
        }

        /// <summary>
        /// Checks whether this bar matches to trade conditions.
        /// Returns either 1 if the bar is intended for buying or -1 if for selling or 0 if there is no trade. 
        /// </summary>
        public abstract int CheckForTrade(int bar);
    }

    internal class SignesTradeChecker : TradeChecker
    {
        public SignesTradeChecker(Bars bars) : base(bars)
        {
        }

        public override int CheckForTrade(int bar)
        {
            int period = 2;
            int[] signes = new int[period];

            for (int i = 0; i < period; i++)
            {
                signes[period - 1 - i] = Math.Sign(Bars.Close[bar - i] - Bars.Open[bar - i]);
            }

            if (signes.All(s => s == signes[0]))
                return signes[0];
            else
                return 0;
        }
    }

    internal class DerivativesTradeChecker : TradeChecker
    {
        public int FilterPeriod { get; set; }

        public DerivativesTradeChecker(Bars bars, int filterPeriod) : base(bars)
        {
            FilterPeriod = filterPeriod;
        }

        public override int CheckForTrade(int bar)
        {
            int derivativeOrder = 2;
            double derivativeTolerance = 0.5;   //rub			

            double deltasSum = 0;
            double[] prices = new double[FilterPeriod];

            //price filter logic
            for (int i = 0; i < FilterPeriod; i++)
            {
                prices[FilterPeriod - 1 - i] = Bars.Close[bar - i];

                double delta = Bars.Close[bar - i] - Bars.Open[bar - i];
                deltasSum += delta;
            }

            //price dynamic filter logic
            double[] derivatives = prices;
            for (int i = 1; i <= derivativeOrder; i++)
            {
                derivatives = CalculateDerivative(derivatives);
            }

            double maxDt = derivatives.Max();
            double minDt = derivatives.Min();
            int sumSign = Math.Sign(deltasSum);

            //return conditions		
            if (maxDt <= derivativeTolerance &&
                minDt >= -derivativeTolerance)
            {
                return sumSign;
            }

            if (maxDt > derivativeTolerance &&
                minDt < -derivativeTolerance)
            {
                return 0;
            }

            if (maxDt > derivativeTolerance &&
                sumSign == 1)
            {
                return 1;
            }

            if (minDt < -derivativeTolerance &&
                sumSign == -1)
            {
                return -1;
            }

            return 0;
        }

        private double[] CalculateDerivative(IReadOnlyList<double> sourceSequence)
        {
            double[] derivative = new double[sourceSequence.Count - 1];

            for (int i = 0; i < sourceSequence.Count - 1; i++)
            {
                derivative[i] = sourceSequence[i + 1] - sourceSequence[i];
            }
            return derivative;
        }
    }
}
