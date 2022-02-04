using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using WealthLab;
using WealthLab.Indicators;
using TradingStrategies.Backtesting.Core;
using TradingStrategies.Backtesting.Indicators;
using TradingStrategies.Backtesting.Utility;

namespace TradingStrategies.Backtesting.Strategies
{
    /// <summary>
    /// Simple coup strategy that based on the Bollinger Bands indicator.
    /// </summary>
    internal partial class CoupStrategy : IStrategyExecuter
    {
        //use the _sw variable to access the WealthScript state

        //optimization params
        private StrategyParameter _period;
        private StrategyParameter _variancesFactor;
        private StrategyParameter _bandsMargin;
        private StrategyParameter _medianMargin;
        private StrategyParameter _filterPeriod;
        private StrategyParameter _bandsWidth;
        private StrategyParameter _lotFilterPeriod;
        private StrategyParameter _lotFilterStretch;
        private StrategyParameter _lotFilterOffset;

        //native params
        private int period = 20;
        private double variancesFactor = 2;
        private double bandsMargin = 0.1;
        private double medianMargin = 0.1;
        private int filterPeriod = 2;
        private double bandsWidth = 10;
        private int lotFilterPeriod = 20;
        private double lotFilterStretch = 1;
        private double lotFilterOffset = 0;

        //money settings
        private const double commiss = 2 * 0.15;        //сommission for 1 contract in 2 parties		
        private const double startingCapital = 100000;
        private double equitySize;

        private const double stopPercent = 1;
        private const double riskPercent = 50;

        //date-time
        private readonly TimeSpan morningTime = new TimeSpan(10, 0, 0);  //session start
        private readonly TimeSpan eveningTime = new TimeSpan(19, 0, 0);  //session end		
        private readonly TimeSpan firstSymbolDateOffset = new TimeSpan(days: 3090, hours: 0, minutes: 0, seconds: 0);
        private DateTime entryDate;
        private DateTime expireDate;
        private const bool exitOnEvening = true;

        //utility
        public const EMACalculation EmaType = EMACalculation.Modern;
        private readonly IScaleFactorCalculator scaler = new SigmoidScaleCalculation();

        void IStrategyExecuter.Initialize()
        {
            equitySize = startingCapital;
            
            //event handlers
            _sw.OptimizationStart += OptimizationStartHandler;
            _sw.OptimizationCycleStart += OptimizationCycleStartHandler;
            _sw.OptimizationComplete += OptimizationCompleteHandler;
            _sw.SymbolProcessingStart += SymbolProcessingStartHandler;
            _sw.SymbolProcessingComplete += SymbolProcessingCompleteHandler;
            _sw.DataSetProcessingStart += DataSetProcessingStartHandler;
            _sw.DataSetProcessingComplete += DataSetProcessingCompleteHandler;

            //params init
            _period = _sw.CreateParameter("ema period", 12, 5, 8, 1);
            _variancesFactor = _sw.CreateParameter("band variances factor", 2.01, 2, 3, 0.25);
            _bandsMargin = _sw.CreateParameter("bands margin", 0.1, 0, 0.4, 0.1);
            _medianMargin = _sw.CreateParameter("median margin", 0.1, 0, 0.4, 0.1);
            _filterPeriod = _sw.CreateParameter("filter period", 3, 1, 4, 1);
            _bandsWidth = _sw.CreateParameter("bands width", 2, 1, 5, 1);
            _lotFilterPeriod = _sw.CreateParameter("LF period", 20, 30, 50, 5);
            _lotFilterStretch = _sw.CreateParameter("LF stretch", 0.25, 0.1, 0.9, 0.2);
            _lotFilterOffset = _sw.CreateParameter("LF offset", 6 * 0.25, 0, 6, 1);
        }

        void IStrategyExecuter.Execute()
        {
            DataSeries equitySeries = new DataSeries(_sw.Bars, "equity");

            BollingerBands BB = new BollingerBands(_sw.Close, period, variancesFactor, EmaType);
            BollingerBandsOffsets BO = new BollingerBandsOffsets(BB, (LogicDouble)bandsMargin, (LogicDouble)medianMargin);
            PlotBands(BB, BO);

            TradeChecker tradeChecker = new SignesTradeChecker(_sw.Bars);
            BollingerFilter BF =
                new WidthBollingerFilterDecorator(
                new BollingerFilterDummy(BB), bandsWidth, true);     

            //trade signals
            bool isSignalBuy = false;
            bool isSignalShort = false;
            bool isSignalSell = false;
            bool isSignalCover = false;

            //helper signals
            bool inLowerBand = false;
            bool inUpperBand = false;
            bool isBuyTrend = false;
            bool isShortTrend = false;

            double stopUp = 10000000;
            double stopDown = 0;
            double lotNum = 0;

            //bars loop
            for (int bar = period; bar < _sw.Bars.Count - 1; bar++)
            {
                #region SIGNALS DEFINING
                if (_sw.CrossUnder(bar, _sw.Close, BO.BandLowerOffset)) inLowerBand = true;
                if (_sw.CrossOver(bar, _sw.Close, BO.MedianUpperOffset)) inLowerBand = false;
                if (_sw.CrossOver(bar, _sw.Close, BO.BandUpperOffset)) inUpperBand = true;
                if (_sw.CrossUnder(bar, _sw.Close, BO.MedianLowerOffset)) inUpperBand = false;

                switch (tradeChecker.CheckForTrade(bar))
                {
                    case 1: isBuyTrend = true; isShortTrend = false; break;
                    case -1: isBuyTrend = false; isShortTrend = true; break;
                    default: isBuyTrend = false; isShortTrend = false; break;
                }

                isSignalBuy = inLowerBand && isBuyTrend
                    && BF.IsBarValid(bar, new FilterParameters() { TradeType = TradeType.Buy });
                isSignalShort = inUpperBand && isShortTrend
                    && BF.IsBarValid(bar, new FilterParameters() { TradeType = TradeType.Buy });

                isSignalSell = isSignalShort || _sw.Close[bar] < stopDown
                    || _sw.CrossUnder(bar, _sw.Close, BO.BandLowerOffset);
                isSignalCover = isSignalBuy || _sw.Close[bar] > stopUp
                    || _sw.CrossOver(bar, _sw.Close, BO.BandUpperOffset);
                #endregion

                DateTime currBarDate = _sw.Date[bar];
                DateTime nextBarDate = _sw.Date[bar + 1];

                //trade logic
                if (currBarDate.Date >= entryDate.Date &&
                    nextBarDate.Date < expireDate.Date &&
                    currBarDate.TimeOfDay > morningTime &&
                    currBarDate.TimeOfDay < eveningTime)
                {
                    #region EXIT FROM CURRENT TRADE
                    if (_sw.IsLastPositionActive)
                    {
                        if (isSignalSell && _sw.LastPosition.PositionType == PositionType.Long)
                        {
                            equitySize += GetIncomeOfLastTrade(bar);
                            _sw.SellAtClose(bar, _sw.LastPosition, equitySize.ToString() + "_" + "Sell");
                        }
                        if (isSignalCover && _sw.LastPosition.PositionType == PositionType.Short)
                        {
                            equitySize += GetIncomeOfLastTrade(bar);
                            _sw.CoverAtClose(bar, _sw.LastPosition, equitySize.ToString() + "_" + "Cover");
                        }
                    }
                    #endregion
                    #region ENTRY TO TRADE
                    if (!_sw.IsLastPositionActive)
                    {
                        LotsFactors lotsFactors = default(LotsFactors);
                        if (isSignalBuy || isSignalShort)
                        {
                            lotsFactors = CalculateFactors(bar);
                            lotNum = (int)(equitySize * riskPercent / (100 * _sw.Close[bar]));

                            stopUp = _sw.Close[bar] * (1 + stopPercent / 100);
                            stopDown = _sw.Close[bar] * (1 - stopPercent / 100);

                            _sw.SetShareSize(lotNum);
                        }

                        if (isSignalBuy)
                        {
                            lotNum *= lotsFactors.BuyFactor;
                            _sw.BuyAtClose(bar, "Buy");
                        }

                        if (isSignalShort)
                        {
                            lotNum *= lotsFactors.SellFactor;
                            _sw.ShortAtClose(bar, "Short");
                        }
                    }
                    #endregion
                } //if date	

                #region EXIT ON EVENING
                if (exitOnEvening &&
                    _sw.IsLastPositionActive &&
                    currBarDate.TimeOfDay >= eveningTime)
                {
                    equitySize += GetIncomeOfLastTrade(bar);
                    _sw.ExitAtClose(bar, Position.AllPositions, "Exit on evening");
                }
                #endregion
                #region EXIT ON EXPIRING
                if (_sw.IsLastPositionActive &&
                    nextBarDate.Date >= expireDate.Date)
                {
                    equitySize += GetIncomeOfLastTrade(bar);
                    _sw.ExitAtClose(bar, Position.AllPositions, equitySize.ToString() + "_" + "Exit on expiring");
                }
                #endregion

                equitySeries[bar] = equitySize;
            } //bars loop

            PlotEquity(equitySeries);
        } //execute			

        private double GetIncomeOfLastTrade(int bar)
        {
            return _sw.LastPosition.NetProfitAsOfBar(bar) - commiss * _sw.LastPosition.Shares;
        }

        private LotsFactors CalculateFactors(int bar)
        {
            int lastBar = (bar < lotFilterPeriod) ? 0 : bar - lotFilterPeriod;

            double priceDeviation = _sw.Close[bar] - _sw.Close[lastBar];
            double factor = scaler.GetScaleFactor(priceDeviation, lotFilterOffset, lotFilterStretch);

            double buyFactor = 1, sellFactor = 1;
            if (priceDeviation > 0) sellFactor = factor;
            if (priceDeviation < 0) buyFactor = factor;

            return
                new LotsFactors(buyFactor, sellFactor);
        }

        private void PlotBands(BollingerBands bands, BollingerBandsOffsets offsets = null)
        {
            //plot bands
            _sw.PlotSeries(_sw.PricePane, bands.UpperBand, Color.DarkViolet, LineStyle.Solid, 1, "Upper band");
            _sw.PlotSeries(_sw.PricePane, bands.LowerBand, Color.DarkViolet, LineStyle.Solid, 1, "Lower band");
            _sw.PlotSeries(_sw.PricePane, bands.Median, Color.Red, LineStyle.Solid, 1, "Median");

            //plot offsets
            if (offsets != null)
            {
                _sw.PlotSeries(_sw.PricePane, offsets.BandUpperOffset, Color.LightPink, LineStyle.Dashed, 1, "Upper band offset");
                _sw.PlotSeries(_sw.PricePane, offsets.BandLowerOffset, Color.LightPink, LineStyle.Dashed, 1, "Lower band offset");
                _sw.PlotSeries(_sw.PricePane, offsets.MedianUpperOffset, Color.LightPink, LineStyle.Dashed, 1, "Upper median offset");
                _sw.PlotSeries(_sw.PricePane, offsets.MedianLowerOffset, Color.LightPink, LineStyle.Dashed, 1, "Lower median offset");
            }
        }

        private void PlotEquity(DataSeries equitySeries)
        {
            ChartPane equityPane = _sw.CreatePane(15, false, true);
            _sw.PlotSeries(equityPane, equitySeries, Color.Green, LineStyle.Histogram, 1);
        }

        private void InitializeParameters()
        {
            period = _period.ValueInt;
            variancesFactor = _variancesFactor.Value;
            bandsMargin = _bandsMargin.Value;
            medianMargin = _medianMargin.Value;
            filterPeriod = _filterPeriod.ValueInt;
            bandsWidth = _bandsWidth.Value;
            lotFilterPeriod = _lotFilterPeriod.ValueInt;
            lotFilterStretch = _lotFilterStretch.Value;
            lotFilterOffset = _lotFilterOffset.Value;

            ValidateParameters();
        }

        private void ValidateParameters()
        {
            if (period <= 0)
                throw new InvalidParameterValueException(nameof(period), period,
                    "The period of EMA must be positive");
            if (variancesFactor <= 0)
                throw new InvalidParameterValueException(nameof(variancesFactor), variancesFactor,
                    "The variances factor of Bollinger Bands must be positive");
            if (bandsMargin < 0 || bandsMargin > 1)
                throw new InvalidParameterValueException(nameof(bandsMargin), bandsMargin,
                    "The margin of Bollinger Bands must be in the [0,1] span");
            if (medianMargin < 0 || medianMargin > 1)
                throw new InvalidParameterValueException(nameof(medianMargin), medianMargin,
                    "The margin of Bollinger Median must be in the [0,1] span");
            if (bandsWidth < 0)
                throw new InvalidParameterValueException(nameof(bandsWidth), bandsWidth,
                    "The filter of stretching width between the Bollinger Bands must be positive");

            if (filterPeriod > period)
                throw new InvalidParameterValueException(nameof(filterPeriod), filterPeriod,
                    "The filter period must be less than the period of EMA");
            if (lotFilterPeriod <= 0)
                throw new InvalidParameterValueException(nameof(lotFilterPeriod), lotFilterPeriod,
                    "The period of the lot filter must be positive");
            if (lotFilterStretch <= 0)
                throw new InvalidParameterValueException(nameof(lotFilterStretch), lotFilterStretch,
                    "The stretch factor of the lot filter must be positive");
        }

        #region EVENT HANDLERS
        private void OptimizationStartHandler()
        {
            _sw.PrintDebug("optimization has been started");
        }

        private void OptimizationCycleStartHandler()
        {
        }

        private void OptimizationCompleteHandler()
        {
        }

        private void SymbolProcessingStartHandler()
        {
            //_sw.PrintDebug(_sw.Bars.Symbol);	
            expireDate = _sw.Date[_sw.Bars.Count - 1];

            InitializeParameters();
        }

        private void SymbolProcessingCompleteHandler()
        {
            entryDate = expireDate;
        }

        private void DataSetProcessingStartHandler()
        {
            entryDate = _sw.Date.Last() - firstSymbolDateOffset;
            equitySize = startingCapital;
        }

        private void DataSetProcessingCompleteHandler()
        {
        }
        #endregion
    }


    #region Core code
    internal partial class CoupStrategy
    {
        private readonly WealthScriptWrapper _sw;

        public CoupStrategy(WealthScriptWrapper scriptWrapper)
        {
            _sw = scriptWrapper;
        }
    }
    #endregion
}
