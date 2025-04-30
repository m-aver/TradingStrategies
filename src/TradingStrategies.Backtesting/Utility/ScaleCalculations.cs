using System;

namespace TradingStrategies.Backtesting.Utility
{
    public interface IScaleFactorCalculator
    {
        //return: 0 - 1.0
        LogicDouble GetScaleFactor(double priceVariance, double xOffset, double xStretch);
    }

    public class AtanScaleCalculation : IScaleFactorCalculator
    {
        //xOffset doesn't impact
        public LogicDouble GetScaleFactor(double priceVariance, double xOffset, double xStretch)
        {
            priceVariance = Math.Abs(priceVariance);

            double arg = priceVariance / xStretch;
            double atan = Math.Atan(arg) / (Math.PI / 2);

            return (LogicDouble)(1 - atan);
        }
    }

    //-6 ~ 0; 6 ~ 1  ->  sigmoid overlaps the 12*x span, when xStretch = x; xOffset = 6*x
    public class SigmoidScaleCalculation : IScaleFactorCalculator
    {
        public LogicDouble GetScaleFactor(double priceVariance, double xOffset, double xStretch)
        {
            priceVariance = Math.Abs(priceVariance);

            double arg = (priceVariance - xOffset) / xStretch;
            double sgm = 1 / (1 + Math.Exp(-arg));

            try
            {
                return (LogicDouble)(1 - sgm);
            }
            catch
            {
                return (LogicDouble)1;
            }
        }
    }

    public class SecHScaleCalculation : IScaleFactorCalculator
    {
        public LogicDouble GetScaleFactor(double priceVariance, double xOffset, double xStretch)
        {
            priceVariance = Math.Abs(priceVariance);

            double arg = (priceVariance - xOffset) / xStretch;
            double sech = 2 / (Math.Exp(arg) + Math.Exp(-arg));

            return (LogicDouble)sech;
        }
    }

    public class ExponentialScaleCalculation : IScaleFactorCalculator
    {
        public LogicDouble GetScaleFactor(double priceVariance, double xOffset, double xStretch)
        {
            priceVariance = Math.Abs(priceVariance);

            double arg = (priceVariance - xOffset) / xStretch;
            double exp;
            if (arg > 0)
                exp = Math.Pow(0.5, Math.Abs(arg));
            else
                exp = 1;

            return (LogicDouble)exp;
        }
    }

    public class LinearScaleCalculation : IScaleFactorCalculator
    {
        private readonly double _km;

        public LinearScaleCalculation(double km)
        {
            if (km > 0)
            {
                throw new ArgumentException("koef must be less or equal to 0", nameof(km));
            }

            _km = km;
        }

        public LogicDouble GetScaleFactor(double priceVariance, double xOffset, double xStretch)
        {
            var val = 1 + (_km * priceVariance);
            return (LogicDouble)Math.Max(val, 0);
        }
    }

    public class NoScale : IScaleFactorCalculator
    {
        public LogicDouble GetScaleFactor(double priceVariance, double xOffset, double xStretch)
        {
            return new LogicDouble(1.0);
        }
    }
}
