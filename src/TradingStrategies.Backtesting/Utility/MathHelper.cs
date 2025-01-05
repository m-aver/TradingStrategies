using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingStrategies.Backtesting.Utility
{
    internal static class MathHelper
    {
        /// <summary>
        /// Calculates square power of number
        /// </summary>
        public static double Sqr(double value) => Math.Pow(value, 2);

        /// <summary>
        /// Calculates natural logarithm of value
        /// </summary>
        public static double NaturalLog(double value) => Math.Log(value);

        /// <summary>
        /// Calculates natural logarithm of series
        /// </summary>
        public static IEnumerable<double> NaturalLog(IEnumerable<double> values) => values.Select(static x => NaturalLog(x));

        /// <summary>
        /// Fits a line to a collection of (x,y) points.
        /// </summary>
        /// <remarks>
        /// https://gist.github.com/NikolayIT/d86118a3a0cb3f5ed63d674a350d75f2
        /// </remarks>
        public static LinearRegressionComponents LinearRegression(double[] xVals, double[] yVals)
        {
            if (xVals.Length != yVals.Length)
            {
                throw new Exception("Input values should be with the same length.");
            }

            double sumOfX = 0;
            double sumOfY = 0;
            double sumOfXSq = 0;
            double sumOfYSq = 0;
            double sumCodeviates = 0;

            for (var i = 0; i < xVals.Length; i++)
            {
                var x = xVals[i];
                var y = yVals[i];
                sumCodeviates += x * y;
                sumOfX += x;
                sumOfY += y;
                sumOfXSq += x * x;
                sumOfYSq += y * y;
            }

            var count = xVals.Length;
            var ssX = sumOfXSq - ((sumOfX * sumOfX) / count);
            var ssY = sumOfYSq - ((sumOfY * sumOfY) / count);

            var rNumerator = (count * sumCodeviates) - (sumOfX * sumOfY);
            var rDenom = (count * sumOfXSq - (sumOfX * sumOfX)) * (count * sumOfYSq - (sumOfY * sumOfY));
            var sCo = sumCodeviates - ((sumOfX * sumOfY) / count);

            var meanX = sumOfX / count;
            var meanY = sumOfY / count;
            var dblR = rNumerator / Math.Sqrt(rDenom);

            return new LinearRegressionComponents()
            {
                rSquared = dblR * dblR,
                yIntercept = meanY - ((sCo / ssX) * meanX),
                slope = sCo / ssX,
            };
        }
    }
}

public class LinearRegressionComponents
{
    /// <summary>
    /// The r^2 value of the line. Used to give an idea of the accuracy given the input values
    /// </summary>
    public double rSquared { get; set; }

    /// <summary>
    /// The y-intercept value of the line (i.e. y = ax + b, yIntercept is b).
    /// </summary>
    public double yIntercept { get; set; }

    /// <summary>
    /// The slop of the line (i.e. y = ax + b, slope is a).
    /// </summary>
    public double slope { get; set; }

    public double CalculatePrediction(double input)
    {
        return (input * slope) + yIntercept;
    }
}
