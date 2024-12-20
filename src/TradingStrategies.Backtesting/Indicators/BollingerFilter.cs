using System;
using System.Linq;
using System.Runtime.Serialization;
using WealthLab;

namespace TradingStrategies.Backtesting.Indicators
{
    /// <summary>
    /// Executes filtration of trades at a specified bar 
    /// based on the <see cref="BollingerBands"/> indicator data
    /// </summary>
    abstract class BollingerFilter
    {
        public BollingerBands SourceBands { get; }

        public BollingerFilter(BollingerBands bands)
        {
            if (bands == null)
                throw new ArgumentNullException(nameof(bands));

            SourceBands = bands;
        }

        public abstract bool IsBarValid(int bar, FilterParameters parameters);
    }


    class BollingerFilterDummy : BollingerFilter
    {
        public BollingerFilterDummy(BollingerBands bands) : base(bands) { }
        public override bool IsBarValid(int bar, FilterParameters parameters) => true;
    }


    /// <summary>
    /// Represents a particular filtration condition that can decorate other conditions
    /// </summary>
    abstract class BollingerFilterDecorator : BollingerFilter
    {
        protected BollingerFilter component { get; }

        public BollingerFilterDecorator(BollingerFilter component) : base(component.SourceBands)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            this.component = component;
        }

        public override bool IsBarValid(int bar, FilterParameters parameters)
        {
            return component.IsBarValid(bar, parameters) && IsBarValid_Decoration(bar, parameters);
        }

        abstract protected bool IsBarValid_Decoration(int bar, FilterParameters parameters);
    }


    class WidthBollingerFilterDecorator : BollingerFilterDecorator
    {
        public double BandsWidth { get; set; }
        public bool TakeAbove { get; set; }

        public WidthBollingerFilterDecorator(BollingerFilter component,
            double bandsWidth, bool takeAbove) : base(component)
        {
            BandsWidth = bandsWidth;
            TakeAbove = takeAbove;
        }

        protected override bool IsBarValid_Decoration(int bar, FilterParameters parameters)
        {
            double bandsWidth = SourceBands.UpperBand[bar] - SourceBands.LowerBand[bar];

            if (TakeAbove)
                return bandsWidth > BandsWidth;
            else
                return bandsWidth < BandsWidth;
        }
    }


    class SlopeBollingerFilterDecorator : BollingerFilterDecorator
    {
        public double SlopeDegree { get; set; } = 0;
        public int SlopePeriod { get; set; } = 1;

        public SlopeBollingerFilterDecorator(BollingerFilter component,
            double slopeDegree, int slopePeriod) : base(component)
        {
            SlopeDegree = slopeDegree;
            SlopePeriod = slopePeriod;
        }

        protected override bool IsBarValid_Decoration(int bar, FilterParameters parameters)
        {
            if (parameters.TradeType == null)
                throw new NoFiltrationParameterException(nameof(FilterParameters.TradeType), this);
            var tradeType = parameters.TradeType.Value;

            return IsBarValid(bar, tradeType);
        }

        public bool IsBarValid(int bar, TradeType tradeType)
        {
            double currentSlopeDegree = CalculateSlopeDegree(bar);
            return
                (-currentSlopeDegree < SlopeDegree && tradeType == TradeType.Buy) ||
                 (currentSlopeDegree < SlopeDegree && tradeType == TradeType.Short);
        }

        private double CalculateSlopeDegree(int bar)
        {
            double k = 1000 / (60 / 5);  //настроечный кэф (1000руб - 1час = 12 5мин)
            double priceVar = SourceBands.Median[bar] - SourceBands.Median[bar - SlopePeriod];
            double timeVar = k * SlopePeriod;

            return (180 / Math.PI) * Math.Atan(priceVar / timeVar);
        }
    }


    /// <summary>
    /// Represents optional filtration parameters which may be taken by various decorators.
    /// </summary>
    struct FilterParameters
    {
        public TradeType? TradeType { get; set; }
    }

    /// <summary>
    /// Occurs if some bollinger filtration decorator cannot find a necessary parameter in <see cref="FilterParameters"/> 
    /// </summary>
    [Serializable]
    class NoFiltrationParameterException : Exception
    {
        public string ParameterName { get; private set; }
        public BollingerFilterDecorator SenderDecorator { get; private set; }

        public NoFiltrationParameterException(string parameterName, BollingerFilterDecorator sender)
        {
            InitializeProperties(parameterName, sender);
        }
        public NoFiltrationParameterException(string parameterName, BollingerFilterDecorator sender,
            string message) : base(message)
        {
            InitializeProperties(parameterName, sender);
        }
        public NoFiltrationParameterException(string parameterName, BollingerFilterDecorator sender,
            string message, Exception innerException) : base(message, innerException)
        {
            InitializeProperties(parameterName, sender);
        }
        protected NoFiltrationParameterException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        private void InitializeProperties(string parameterName, BollingerFilterDecorator sender)
        {
            if (parameterName == null) throw new ArgumentNullException(nameof(parameterName));
            if (sender == null) throw new ArgumentNullException(nameof(sender));

            CheckParameterName(parameterName);

            ParameterName = parameterName;
            SenderDecorator = sender;
        }

        /// <summary>
        /// Checks that the passed parameter name is contained in the <see cref="FilterParameters"/> struct
        /// </summary>        
        private void CheckParameterName(string paramName)
        {
            var props = typeof(FilterParameters).GetProperties();

            if (props.Any(p => p.Name == paramName) == false)
                throw new ArgumentException(
                    $"Passed parameter name is not contained in the {nameof(FilterParameters)} struct", nameof(paramName));
        }
    }
}
