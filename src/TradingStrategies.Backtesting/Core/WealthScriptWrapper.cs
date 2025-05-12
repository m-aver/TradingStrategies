using System.Diagnostics;
using System.Reflection;
using WealthLab;

namespace TradingStrategies.Backtesting.Core
{
    /// <summary>
    /// Wrapper class that extends the default functional of the <see cref="WealthLab.WealthScript"/> class. 
    /// </summary>
    public class WealthScriptWrapper : WealthScript
    {
        private IStrategyExecuter _strategy;

        public event Action SymbolProcessingStart;
        public event Action SymbolProcessingComplete;
        public event Action DataSetProcessingStart;
        public event Action DataSetProcessingComplete;

        //WARN: use carefully and not for trading logic
        //there is no guarantee that property was set to true during some optimization process and set to false in some non-optimization processes
        public bool IsOptimizationRun { get; private set; }

        private string _startSymbol;
        private string _finalSymbol;

        public string StartSymbol
        {
            get => _startSymbol;
            set
            {
                if (IsCallFromInterface<IStrategyExecuter>(nameof(IStrategyExecuter.Initialize)))
                    _startSymbol = value;
                else
                    throw new Exception(
                        $"{nameof(StartSymbol)} can be assigned only from {nameof(IStrategyExecuter.Initialize)}" +
                        $"method of implements of {nameof(IStrategyExecuter)} interface");
            }
        }
        public string FinalSymbol
        {
            get => _finalSymbol;
            set
            {
                if (IsCallFromInterface<IStrategyExecuter>(nameof(IStrategyExecuter.Initialize)))
                    _finalSymbol = value;
                else
                    throw new Exception(
                        $"{nameof(FinalSymbol)} can be assigned only from {nameof(IStrategyExecuter.Initialize)}" +
                        $"method of implements of {nameof(IStrategyExecuter)} interface");
            }
        }

        public WealthScriptWrapper()
        {
            _strategy = StrategyFactory.CreateStrategyInstance(this);
            _strategy.Initialize();
        }

        private static bool ValidateBars(Bars bars)
        {
            //check for symbol bars (i.e. they may be filtered by ui)
            return bars.Count > 0;
        }

        private bool ValidateSymbol()
        {
            //need since DataSetSymbols is null when constructor is called
            if (_startSymbol != null && !DataSetSymbols.Contains(_startSymbol))
                throw new Exception($"{nameof(StartSymbol)} not correct symbol name, check existing dataset items");
            if (_finalSymbol != null && !DataSetSymbols.Contains(_finalSymbol))
                throw new Exception($"{nameof(FinalSymbol)}: not correct symbol name, check existing dataset items");

            //check for null to avoid symbol filtering if they is not assigned from outside
            var inStartRange = _startSymbol == null || DataSetSymbols.IndexOf(Bars.Symbol) >= DataSetSymbols.IndexOf(StartSymbol);
            var inFinalRange = _finalSymbol == null || DataSetSymbols.IndexOf(Bars.Symbol) <= DataSetSymbols.IndexOf(FinalSymbol);

            var hasValidBars = ValidateBars(Bars);

            if (!hasValidBars)
                PrintInvalidBarsWarning();

            return
                inStartRange &&
                inFinalRange &&
                hasValidBars;
        }

        protected override void Execute()
        {
            if (ValidateSymbol())
            {
                OnDataSetProcessingStart();

                OnSymbolProcessingStart();

                try
                {
                    _strategy.Execute();
                }
                catch (Exception ex)
                {
                    var msg = $"an exception was generated in the user execution method: {ex.Message}";

                    PrintDebug(msg);
                }

                OnSymbolProcessingComplete();

                OnDataSetProcessingComplete();
            }
        }

        private void OnSymbolProcessingStart()
        {
            try
            {
                SymbolProcessingStart?.Invoke();
            }
            catch (Exception ex)
            {
                throw new Exception(" [symbol processing start handlers exception] " + ex.Message, ex);
            }
        }

        private void OnSymbolProcessingComplete()
        {
            try
            {
                SymbolProcessingComplete?.Invoke();
            }
            catch (Exception ex)
            {
                throw new Exception(" [symbol processing compete handlers exception] " + ex.Message, ex);
            }
        }

        private void OnDataSetProcessingStart()
        {
            //StrategyWindowID is not set during regular optimization (from WealthLab.Optimization class), seems there's useful bug in WealthLab
            //but for example it is set while WFO-optimization, and maybe is not set while some other processes
            IsOptimizationRun = base.StrategyWindowID == 0;

            if (Bars.Symbol == (StartSymbol ?? DataSetSymbols.First()))
            {
                try
                {
                    DataSetProcessingStart?.Invoke();
                }
                catch (Exception ex)
                {
                    throw new Exception(" [dataset processing start handlers exception] " + ex.Message, ex);
                }
            }
        }

        private void OnDataSetProcessingComplete()
        {
            if (Bars.Symbol == (FinalSymbol ?? DataSetSymbols.Last()))
            {
                try
                {
                    DataSetProcessingComplete?.Invoke();
                }
                catch (Exception ex)
                {
                    throw new Exception(" [dataset processing complete handlers exception] " + ex.Message, ex);
                }
            }
        }

        private void PrintInvalidBarsWarning()
        {
            var msg = """
                    invalid bars detected (i.e. filtered by ui), 
                    it may be dangerous because dataset processing and optimization processing events may not be fired: 
                    if filtered bars is first or last of entire dataset
                    """;
            PrintDebug(msg);
        }

        //interceptor for debug messages
        new public void PrintDebug(string message)
        {
            var metaInfo = $"strategy: {StrategyName}; symbol: {Bars.Symbol}; date: {DateTime.Now.ToShortTimeString()};{Environment.NewLine}";
            base.PrintDebug(metaInfo + message);
        }

        //need for reason of protected access to default CreateParameter method
        new public StrategyParameter CreateParameter(string name, double defaultValue, double start, double stop, double step)
        {
            var param = base.CreateParameter(name, defaultValue, start, stop, step);
            return param;
        }

        private bool IsCallFromInterface<T>(string methodName) where T : class
        {
            var sf = new StackFrame(2);

            MethodBase callingMethod = sf.GetMethod();
            Type callingType = callingMethod.DeclaringType;
            Type interfaceType = typeof(T);

            if (!interfaceType.IsInterface)
                throw new ArgumentException("the generic type parameter is not an interface", nameof(T));
            if (!interfaceType.GetMethods().Any(mi => mi.Name == methodName))
                throw new ArgumentException("the specified interface not contains the passed method name", nameof(methodName));

            return
                interfaceType.IsAssignableFrom(callingType) &&          //встроенных способов определения принадлежности метода к интерфейсу не нашел, так что только так
                callingMethod.Name == methodName;                       //но это пиздец не безопасно, т.к. есть несколько возможностей определить в классе метод с таким же именем, но не являющийся реализацией указанного интерфейса - напр.: другая сигнатура, явная реализация интерфейса, перекрытие метода при реализации интерфейса в базовом классе
        }
    }
}
