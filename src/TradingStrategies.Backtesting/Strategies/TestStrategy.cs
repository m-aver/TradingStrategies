using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using TradingStrategies.Backtesting.Core;
using TradingStrategies.Backtesting.Indicators;
using TradingStrategies.Backtesting.Utility;
using WealthLab;
using WealthLab.Indicators;

//ADX and BollingerBands
//targed DataSet: min 5 RTS 
//result: 
//на сбере хуйня, на RTS есть варики, но пока видел доходность не больше 11% за год

#region INFO
//только шорты, выход по стопу
//вход в шорт по пробитию вехней полосы Боллинджера
//вход в лонг по пробитию нижней полосы Боллинджера
//фильтр - ADX ниже граничного значения (показывает силу тренда)

//добавил дополнительные корректирующие условия
//с которыми стратегия начинает выглядеть довольно сочно
//ADX какой-то никчемный

//на датасете h1 ford примерно после 04.21 и до конца сета
//очень явно проявляются ситуации на которых стратегия активно сливает
//надо будет детально проанализировать этот период

//нужно попробовать фильтровать мелкие движения цены 
//в противоположную от основного тренда сторону
//т.е. если цена падала, но на границе боллинджера появился растущий бар
//то не переворачиваться в лонг сразу на этом баре, а подождать куда цена пойдет дальше
//заметил, что такие бары дают фиктивные сигналы, приводящие к убыткам

//optimization preferences (shorts + longs):
//ema period: all
//variances factor: 2-3
//bands margin: all
//median margin: 0, 0.4
//best: 11/2.5/0.3/0.4
//standart: 12/2/0.1/0.1

//такая идейка:
//попробовать отслеживать на часовиках глобальный тренд
//и в зависимости от его силы и направления 
//регулировать кэфы на наколичество покупаемых/продаваемых лотов 
//на локальных пятиминутных движениях
//напр., если глобальный тренд идет на падение, то увеличивать лоты для шортов и уменьшать для лонгов

//фильтр по проценту роста вместо угла наклона
//суть таже, но угол наклона плох тем, что необходимо подбирать настроечный кэф отдельно под каждую бумагу
//плюс можно попробовать не полную фильтрацию, а частичное подавление лотов как описано выше

//ADX info: https://bcs-express.ru/novosti-i-analitika/indikator-adx-opredeliaem-sil-nye-trendy
//BB info: https://bcs-express.ru/novosti-i-analitika/indikator-polosy-bollindzhera-ili-kak-priruchit-volatil-nost
#endregion
#region BUGS
//какой-то косяк с обсчетом и выводом кастомных панелей диаграмм
//при запуске стратегии на всем датасете, даже если в датасете всего один symbol
//например, диаграмма эквити для <5min Sber> показывает некорректные цифры, хотя основная тенденция сохраняется
//все становится норм, если запускать стретегию не для датасета, а для отдельного symbol
#endregion

namespace WealthLabProject.Strategies
{
    internal partial class TestStrategy : IStrategyExecuter
    {
        //use the _sw variable to access the WealthScript state

        //optimization params
        private StrategyParameter _period;
        private StrategyParameter _variancesFactor;
        private StrategyParameter _adxFilter;       //not used
        private StrategyParameter _bandsMargin;
        private StrategyParameter _medianMargin;
        private StrategyParameter _filterPeriod;
        private StrategyParameter _slopeDegree;     //not used
        private StrategyParameter _slopePeriod;     //not used
        private StrategyParameter _bandsWidth;
        private StrategyParameter _lotFilterPeriod;
        private StrategyParameter _lotFilterStretch;
        private StrategyParameter _lotFilterOffset;

        //native params
        private int period = 20;
        private double variancesFactor = 2;
        private double adxFilter = 20;
        private double bandsMargin = 0.1;
        private double medianMargin = 0.1;
        private int filterPeriod = 2;
        private double slopeDegree = 45;
        private int slopePeriod = 10;
        private double bandsWidth = 10;
        private int lotFilterPeriod = 20;
        private double lotFilterStretch = 1;
        private double lotFilterOffset = 0;

        //money settings
        private const double commiss = 2 * 0.15;        // Комиссия на 1 контракт в 2 стороны		
        private const double startingCapital = 100000;
        private double equitySize;
        private double equitySizeMax;

        private const double stopPercent = 1;
        private const double riskPercent = 50;

        //date-time
        private readonly TimeSpan morningTime = new TimeSpan(10, 0, 0);  //session start
        private readonly TimeSpan eveningTime = new TimeSpan(19, 0, 0);  //session end		
        private DateTime _entryDate;
        private DateTime _expireDate;
        private const bool exitOnEvening = true;

        //utility
        public const EMACalculation EmaType = EMACalculation.Modern;
        private readonly IScaleFactorCalculator scaler = new SigmoidScaleCalculation();

        void IStrategyExecuter.Initialize()
        {
            equitySize = startingCapital;

            //event handlers
            _sw.SymbolProcessingStart += SymbolProcessingStartHandler;
            _sw.SymbolProcessingComplete += SymbolProcessingCompleteHandler;
            _sw.DataSetProcessingStart += DataSetProcessingStartHandler;
            _sw.DataSetProcessingComplete += DataSetProcessingCompleteHandler;

            //params init
            _period = _sw.CreateParameter("ema period", 12, 5, 8, 1);
            _variancesFactor = _sw.CreateParameter("band variances factor", 2.01, 2, 3, 0.25);
            _adxFilter = _sw.CreateParameter("adx filter", 20, 19, 20, 1);
            _bandsMargin = _sw.CreateParameter("bands margin", 0.1, 0, 0.4, 0.1);
            _medianMargin = _sw.CreateParameter("median margin", 0.1, 0, 0.4, 0.1);
            _filterPeriod = _sw.CreateParameter("filter period", 3, 1, 4, 1);
            _slopeDegree = _sw.CreateParameter("slope degree", 90, 5, 70, 10);
            _slopePeriod = _sw.CreateParameter("slope period", 6, 5, 50, 5);
            _bandsWidth = _sw.CreateParameter("bands width", 2, 1, 5, 1);
            _lotFilterPeriod = _sw.CreateParameter("LF period", 20, 30, 50, 5);
            _lotFilterStretch = _sw.CreateParameter("LF stretch", 0.25, 0.1, 0.9, 0.2);
            _lotFilterOffset = _sw.CreateParameter("LF offset", 6 * 0.25, 0, 6, 1);
        }

        void IStrategyExecuter.Execute()
        {
            //place your strategy code here

            DataSeries equitySeries = new DataSeries(_sw.Bars, "equity");
            BollingerBands BB = new BollingerBands(_sw.Close, period, variancesFactor, EMACalculation.Modern);
            //DataSeries ADX = GetAdx(period);
            PlotIndicators(BB);

            DataSeries bandUpperOffset = BB.UpperBand - bandsMargin * (BB.UpperBand - BB.Median);
            DataSeries bandLowerOffset = BB.LowerBand + bandsMargin * (BB.Median - BB.LowerBand);
            DataSeries medianUpperOffset = BB.Median + medianMargin * (BB.UpperBand - BB.Median);
            DataSeries medianLowerOffset = BB.Median - medianMargin * (BB.Median - BB.LowerBand);

            bool outUpperMedian = false;
            bool outLowerMedian = false;
            bool inLowerBand = false;
            bool inUpperBand = false;
            bool outLowerBand = false;
            bool outUpperBand = false;
            bool isBuyTrend = false;
            bool isShortTrend = false;

            //BandsValidator BV = new BandsValidator(BB) { SlopePeriod = slopePeriod, SlopeDegree = slopeDegree, BandsWidth = bandsWidth };

            bool isSignalBuy = false;
            bool isSignalShort = false; // Сигналы на вход в длинную и короткую позиции
            bool isSignalSell = false;
            bool isSignalCover = false; // Сигналы на выход из длинной и короткой позиции 

            double stopUp = 10000000;
            double stopDown = 0;
            double lotNum = 0;

            for (int bar = period; bar < _sw.Bars.Count - 1; bar++)
            {
                #region BARS LAYOUT
                //in bands
                if (_sw.CrossUnder(bar, _sw.Close, bandLowerOffset)) inLowerBand = true;
                if (_sw.CrossOver(bar, _sw.Close, medianUpperOffset)) inLowerBand = false;
                if (_sw.CrossOver(bar, _sw.Close, bandUpperOffset)) inUpperBand = true;
                if (_sw.CrossUnder(bar, _sw.Close, medianLowerOffset)) inUpperBand = false;

                //out bands
                if (_sw.CrossOver(bar, _sw.Close, bandLowerOffset)) outLowerBand = true;
                if (_sw.CrossOver(bar, _sw.Close, medianLowerOffset)) outLowerBand = false;
                if (_sw.CrossUnder(bar, _sw.Close, bandUpperOffset)) outUpperBand = true;
                if (_sw.CrossUnder(bar, _sw.Close, medianUpperOffset)) outUpperBand = false;

                //out medians
                if (_sw.CrossUnder(bar, _sw.Close, medianLowerOffset)) outLowerMedian = true;
                if (_sw.CrossOver(bar, _sw.Close, medianUpperOffset) || _sw.CrossUnder(bar, _sw.Close, bandLowerOffset)) outLowerMedian = false;
                if (_sw.CrossOver(bar, _sw.Close, medianUpperOffset)) outUpperMedian = true;
                if (_sw.CrossUnder(bar, _sw.Close, medianLowerOffset) || _sw.CrossOver(bar, _sw.Close, bandUpperOffset)) outUpperMedian = false;

                /*
                if (_sw.CrossUnder(bar, _sw.Close, bandLowerOffset)) inLowerBand = true;
                if (isSignalBuy) inLowerBand = false;
                if (_sw.CrossOver(bar, _sw.Close, bandUpperOffset)) inUpperBand = true;
                if (isSignalShort) inUpperBand = false;
                
                if (_sw.CrossUnder(bar, _sw.Close, medianLowerOffset)) outLowerMedian = true;
                if (isSignalSell) outLowerMedian = false;
                if (_sw.CrossOver(bar, _sw.Close, medianUpperOffset)) outUpperMedian = true;
                if (isSignalCover) outUpperMedian = false;
                */
                #endregion

                switch (CheckForTrade(bar))
                {
                    case 1: isBuyTrend = true; isShortTrend = false; break;
                    case -1: isBuyTrend = false; isShortTrend = true; break;
                    default: isBuyTrend = false; isShortTrend = false; break;
                }

                #region TRADE SIGNALS
                //isSignalBuy = //inLowerBand && isBuyTrend
                //    outLowerBand && _sw.Close[bar] > _sw.Close[bar-1]
                //    && BV.IsBarValid(bar, TradeType.Buy);
                //isSignalShort = //inUpperBand && isShortTrend
                //    outUpperBand && _sw.Close[bar] < _sw.Close[bar - 1]
                //    && BV.IsBarValid(bar, TradeType.Short);

                isSignalBuy = (outLowerBand || outUpperMedian)
                    && !(outLowerBand && outUpperMedian)
                    && isBuyTrend;
                //&& BV.IsBarValid(bar, TradeType.Buy);
                isSignalShort = (outUpperBand || outLowerMedian)
                    && !(outUpperBand && outLowerMedian)
                    && isShortTrend;
                //&& BV.IsBarValid(bar, TradeType.Short);

                isSignalSell = isSignalShort || _sw.Close[bar] < stopDown
                    || _sw.CrossUnder(bar, _sw.Close, bandLowerOffset);
                //|| (isShortTrend && outLowerMedian);
                isSignalCover = isSignalBuy || _sw.Close[bar] > stopUp
                    || _sw.CrossOver(bar, _sw.Close, bandUpperOffset);
                //|| (isBuyTrend && outUpperMedian);
                #endregion


                DateTime currBarDate = _sw.Date[bar];
                DateTime nextBarDate = _sw.Date[bar + 1];

                if (currBarDate.Date >= _entryDate.Date &&
                    nextBarDate.Date < _expireDate.Date &&
                    currBarDate.TimeOfDay > morningTime &&
                    currBarDate.TimeOfDay < eveningTime)
                {
                    #region EXIT FROM CURRENT TRADE
                    if (_sw.IsLastPositionActive)
                    {
                        if (isSignalSell && _sw.LastPosition.PositionType == PositionType.Long)
                        {
                            equitySize = equitySize + _sw.LastPosition.NetProfitAsOfBar(bar) - commiss * _sw.LastPosition.Shares;
                            _sw.SellAtClose(bar, _sw.LastPosition, equitySize.ToString() + "_" + "Sell");
                        }
                        if (isSignalCover && _sw.LastPosition.PositionType == PositionType.Short)
                        {
                            equitySize = equitySize + _sw.LastPosition.NetProfitAsOfBar(bar) - commiss * _sw.LastPosition.Shares;
                            _sw.CoverAtClose(bar, _sw.LastPosition, equitySize.ToString() + "_" + "Cover");
                        }
                    }
                    #endregion
                    #region ENTRY TO TRADE
                    if (!_sw.IsLastPositionActive)
                    {
                        if (equitySizeMax < equitySize)
                            equitySizeMax = equitySize;

                        LotsFactors lf = (isSignalBuy || isSignalShort) ? CalculateFactors(bar) : new LotsFactors();

                        if (isSignalBuy)
                        {
                            lotNum = (int)(equitySize * riskPercent / (100 * _sw.Close[bar]));
                            lotNum *= lf.BuyFactor;

                            _sw.SetShareSize(lotNum);
                            _sw.BuyAtClose(bar, "Buy");

                            stopDown = _sw.Close[bar] * (1 - stopPercent / 100);
                        }

                        if (isSignalShort)
                        {
                            lotNum = (int)(equitySize * riskPercent / (100 * _sw.Close[bar]));
                            lotNum *= lf.SellFactor;

                            _sw.SetShareSize(lotNum);
                            _sw.ShortAtClose(bar, "Short");

                            stopUp = _sw.Close[bar] * (1 + stopPercent / 100);
                        }
                    }
                    #endregion
                } //if date	

                #region EXIT ON EVENING
                if (exitOnEvening &&
                    _sw.IsLastPositionActive &&
                    currBarDate.TimeOfDay >= eveningTime)
                {
                    equitySize = equitySize + _sw.LastPosition.NetProfitAsOfBar(bar) - commiss * _sw.LastPosition.Shares;
                    _sw.ExitAtClose(bar, Position.AllPositions, "Exit on evening");
                }
                #endregion
                #region EXIT ON EXPIRING
                if (_sw.IsLastPositionActive &&
                    nextBarDate.Date >= _expireDate.Date)
                {
                    equitySize = equitySize + _sw.LastPosition.NetProfitAsOfBar(bar) - commiss * _sw.LastPosition.Shares;
                    _sw.ExitAtClose(bar, Position.AllPositions, equitySize.ToString() + "_" + "Exit on expiring");
                }
                #endregion

                equitySeries[bar] = equitySize;
            } //bars for

            //plotting equity
            ChartPane equityPane = _sw.CreatePane(15, false, true);
            _sw.PlotSeries(equityPane, equitySeries, Color.Green, WealthLab.LineStyle.Histogram, 1);
        } //execute			


        /// <summary>
        /// Checks whether this bar matchs to trade conditions.
        /// Returns either 1 if the bar is intended for buying or -1 if for selling or 0 if there is no trade. 
        /// </summary>
        private int CheckForTrade(int bar)
        {
            #region old
            /*
             int derivativeOrder = 2;
             double derivativeTolerance = 0.5;   //rub			

             double deltasSum = 0;
             double[] prices = new double[filterPeriod];

             //price filter logic
             for (int i = 0; i < filterPeriod; i++)
             {
                 prices[filterPeriod - 1 - i] = _sw.Close[bar - i];

                 double delta = _sw.Close[bar - i] - _sw.Open[bar - i];
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
             */
            #endregion

            int period = 2;
            int[] signes = new int[period];

            for (int i = 0; i < period; i++)
            {
                signes[period - 1 - i] = Math.Sign(_sw.Close[bar - i] - _sw.Open[bar - i]);
            }

            if (signes.All(s => s == signes[0]))
                return signes[0];
            else
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

        private LotsFactors CalculateFactors(int bar)
        {
            int lastBar = (bar < lotFilterPeriod) ? 0 : bar - lotFilterPeriod;

            //OLD
            /*
            double[] prices = new double[bar - lastBar];
            for (int b = bar; b > lastBar; b--)
                prices[b - lastBar - 1] = _sw.Close[b];
            double avgPrice = prices.Average();
            */

            double priceDeviation = _sw.Close[bar] - _sw.Close[lastBar];
            double factor = scaler.GetScaleFactor(priceDeviation, lotFilterOffset, lotFilterStretch);

            //OLD
            /*
            double factor = Math.Pow(1 / lotFilterStretch, Math.Abs(priceDeviation / avgPrice));     //base >= 1 | power >= 0  (показательная функция)
            //priceDeviation / avgPrice  RTS
            //priceDeviation  СБЕР
            */

            double buyFactor = 1, sellFactor = 1;
            if (priceDeviation > 0) sellFactor = factor;
            if (priceDeviation < 0) buyFactor = factor;

            return
                new LotsFactors(buyFactor, sellFactor);
        }

        private DataSeries GetAdx(int period)
        {
            DataSeries adx;

            DataSeries posDM = new DataSeries(_sw.Bars, "+DM");
            DataSeries negDM = new DataSeries(_sw.Bars, "-DM");
            DataSeries TR = new DataSeries(_sw.Bars, "TR");
            DataSeries posDI = new DataSeries(_sw.Bars, "+DI");
            DataSeries negDI = new DataSeries(_sw.Bars, "-DI");

            posDM[0] = 0;
            negDM[0] = 0;
            TR[0] = 0;

            for (int bar = 1; bar < _sw.Bars.Count; bar++)
            {
                double currPosM = _sw.High[bar] - _sw.High[bar - 1];
                double currNegM = _sw.Low[bar - 1] - _sw.Low[bar];
                double currPosDM = (currPosM > currNegM && currPosM > 0) ? currPosM : 0;
                double currNegDM = (currNegM > currPosM && currNegM > 0) ? currNegM : 0;
                double currTR = Math.Max(_sw.High[bar], _sw.Close[bar - 1]) - Math.Min(_sw.Low[bar], _sw.Close[bar - 1]);

                posDM[bar] = currPosDM;
                negDM[bar] = currNegDM;
                TR[bar] = currTR;
            }
            // for (int i = 0; i < TR.Count; i++) _sw.PrintDebug(TR[i]);	//DEBUG

            posDI = EMA.Series(posDM / TR, period, EmaType);
            negDI = EMA.Series(negDM / TR, period, EmaType);

            DataSeries abs = posDI - negDI;
            for (int i = 0; i < abs.Count; i++) abs[i] = Math.Abs(abs[i]);

            adx = 100 * EMA.Series(abs / (posDI + negDI), period, EmaType);
            adx.Description = "ADX";
            return adx;
        }

        private void PlotIndicators(BollingerBands BB, DataSeries ADX = null)
        {
            _sw.PlotSeries(_sw.PricePane, BB.UpperBand, Color.DarkViolet, WealthLab.LineStyle.Solid, 1, "Upper band");
            _sw.PlotSeries(_sw.PricePane, BB.LowerBand, Color.DarkViolet, WealthLab.LineStyle.Solid, 1, "Lower band");
            _sw.PlotSeries(_sw.PricePane, BB.Median, Color.Red, WealthLab.LineStyle.Solid, 1, "Median");

            if (ADX != null)
            {
                ChartPane adxPane = _sw.CreatePane(10, false, true);
                _sw.PlotSeries(adxPane, ADX, Color.Blue, WealthLab.LineStyle.Solid, 1, "ADX");
                _sw.DrawHorzLine(adxPane, adxFilter, Color.Gray, WealthLab.LineStyle.Dashed, 1);
            }

            //temp
            DataSeries bandUpperOffset = BB.UpperBand - bandsMargin * (BB.UpperBand - BB.Median);
            DataSeries bandLowerOffset = BB.LowerBand + bandsMargin * (BB.Median - BB.LowerBand);
            DataSeries medianUpperOffset = BB.Median + medianMargin * (BB.UpperBand - BB.Median);
            DataSeries medianLowerOffset = BB.Median - medianMargin * (BB.Median - BB.LowerBand);

            _sw.PlotSeries(_sw.PricePane, bandUpperOffset, Color.LightPink, WealthLab.LineStyle.Dashed, 1, "Upper band offset");
            _sw.PlotSeries(_sw.PricePane, bandLowerOffset, Color.LightPink, WealthLab.LineStyle.Dashed, 1, "Lower band offset");
            _sw.PlotSeries(_sw.PricePane, medianUpperOffset, Color.LightPink, WealthLab.LineStyle.Dashed, 1, "Upper median offset");
            _sw.PlotSeries(_sw.PricePane, medianLowerOffset, Color.LightPink, WealthLab.LineStyle.Dashed, 1, "Lower median offset");
        }

        private void InitSystemParams()
        {
            period = _period.ValueInt;
            variancesFactor = _variancesFactor.Value;
            adxFilter = _adxFilter.Value;
            bandsMargin = _bandsMargin.Value;
            medianMargin = _medianMargin.Value;
            filterPeriod = _filterPeriod.ValueInt;
            slopeDegree = _slopeDegree.Value;
            slopePeriod = _slopePeriod.ValueInt;
            bandsWidth = _bandsWidth.Value;
            lotFilterPeriod = _lotFilterPeriod.ValueInt;
            lotFilterStretch = _lotFilterStretch.Value;
            lotFilterOffset = _lotFilterOffset.Value;

            ValidateSystemParams();
        }

        private void ValidateSystemParams()
        {
            if (period <= 0)
                throw new Exception(
                    "The period of EMA must be positive");
            if (variancesFactor <= 0)
                throw new Exception(
                    "The variances factor of Bollinger Bands must be positive");
            if (bandsMargin < 0 || bandsMargin > 1)
                throw new Exception(
                    "The margin of Bollinger Bands must be in the [0,1] span");
            if (medianMargin < 0 || medianMargin > 1)
                throw new Exception(
                    "The margin of Bollinger Median must be in the [0,1] span");
            if (bandsWidth < 0)
                throw new Exception(
                    "The filter of stretching width between the Bollinger Bands must be positive");
            if (slopeDegree < 0 || slopeDegree > 90)
                throw new Exception(
                    "The filter of EMA slope degree must be in the [0,90] span");
            if (slopePeriod <= 0)
                throw new Exception(
                    "The period for calculating a slope degree must be positive");

            if (filterPeriod > period)
                throw new Exception(
                    "The filter period must be less than the period of EMA");
            if (lotFilterPeriod <= 0)
                throw new Exception(
                    "The period of the lot filter must be positive");
            if (lotFilterStretch <= 0)
                throw new Exception(
                    "The stretch factor of the lot filter must be positive");
            //OLD
            /*
            if (lotFilterStretch < 1)
                throw new Exception(
                    "The factor of the slow filter must be more than 1");
            */
        }


        #region Event handlers
        private void SymbolProcessingStartHandler()
        {
            //_sw.PrintDebug(_sw.Bars.Symbol);	
            _expireDate = _sw.Date[_sw.Bars.Count - 1];

            InitSystemParams();
        }

        private void SymbolProcessingCompleteHandler()
        {
            _entryDate = _expireDate;
        }

        private void DataSetProcessingStartHandler()
        {
            _entryDate = DateTime.MinValue;
            equitySize = startingCapital;
            equitySizeMax = 0;
        }

        private void DataSetProcessingCompleteHandler()
        {
        }
        #endregion
    }

    [StructLayout(LayoutKind.Auto)]
    internal struct LotsFactors
    {
        public double BuyFactor { get; }
        public double SellFactor { get; }

        public LotsFactors(double buyFactor, double sellFactor)
        {
            BuyFactor = buyFactor;
            SellFactor = sellFactor;
        }
    }

    #region System code
    internal partial class TestStrategy
    {
        private readonly WealthScriptWrapper _sw;

        public TestStrategy(WealthScriptWrapper scriptWrapper)
        {
            _sw = scriptWrapper;
        }
    }
    #endregion
}
