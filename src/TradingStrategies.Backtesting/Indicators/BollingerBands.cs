using System;
using WealthLab;
using WealthLab.Indicators;
using TradingStrategies.Backtesting.Utility;

namespace TradingStrategies.Backtesting.Indicators
{
    internal class BollingerBands
    {
        public DataSeries UpperBand { get; private set; }
        public DataSeries LowerBand { get; private set; }
        public DataSeries Median { get; private set; }

        public int Period { get; }
        public double VariancesFactor { get; }
        public EMACalculation EmaType { get; }

        private readonly DataSeries _sourceSeries;

        public BollingerBands(DataSeries sourceSeries, int period, double variancesFactor, EMACalculation emaType)
        {
            if (sourceSeries == null)
                throw new ArgumentNullException(nameof(sourceSeries));
            if (period <= 0)
                throw new ArgumentException("The period should be positive.", nameof(period));
            if (variancesFactor <= 0)
                throw new ArgumentException("The variances factor should be positive.", nameof(variancesFactor));

            Period = period;
            VariancesFactor = variancesFactor;
            EmaType = emaType;
            _sourceSeries = sourceSeries;

            CalculateBands();
        }

        private void CalculateBands()
        {
            DataSeries standartDeviation =
                StdDev.Series(_sourceSeries, Period, StdDevCalculation.Sample);

            Median = EMA.Series(_sourceSeries, Period, EmaType);
            UpperBand = Median + (VariancesFactor * standartDeviation);
            LowerBand = Median - (VariancesFactor * standartDeviation);
        }
    }

    internal class BollingerBandsOffsets
    {
        public DataSeries BandUpperOffset { get; }
        public DataSeries BandLowerOffset { get; }
        public DataSeries MedianUpperOffset { get; }
        public DataSeries MedianLowerOffset { get; }

        public BollingerBandsOffsets(BollingerBands bands, LogicDouble marginFactor)
            : this(bands, marginFactor, marginFactor)
        {
        }

        public BollingerBandsOffsets(BollingerBands bands, 
            LogicDouble bandsMarginFactor, LogicDouble medianMarginFactor)
            : this(bands, bandsMarginFactor, bandsMarginFactor, medianMarginFactor, medianMarginFactor)
        {
        }

        public BollingerBandsOffsets(BollingerBands bands, 
            LogicDouble upperBandsMarginFactor, LogicDouble lowerBandsMarginFactor, 
            LogicDouble upperMedianMarginFactor, LogicDouble lowerMedianMarginFactor)
        {
            if (bands == null) throw new ArgumentNullException(nameof(bands));

            BandUpperOffset = bands.UpperBand - upperBandsMarginFactor * (bands.UpperBand - bands.Median);
            BandLowerOffset = bands.LowerBand + lowerBandsMarginFactor * (bands.Median - bands.LowerBand);
            MedianUpperOffset = bands.Median + upperMedianMarginFactor * (bands.UpperBand - bands.Median);
            MedianLowerOffset = bands.Median - lowerMedianMarginFactor * (bands.Median - bands.LowerBand);
        }
    }
}
