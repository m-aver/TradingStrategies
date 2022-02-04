using System;
using System.Collections.Generic;
using System.Linq;
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

        //Avoid using the default values for optimization parameters that match their start values. 
        //This will generate the optimization start event for ever stategy run		
        //OptimizationCycleStart event is not generated for the first optimization cycle. 
        //Instead, use OptimizationStart event
        public event Action OptimizationStart;
        public event Action OptimizationCycleStart;
        public event Action OptimizationComplete;
        public event Action SymbolProcessingStart;
        public event Action SymbolProcessingComplete;
        public event Action DataSetProcessingStart;
        public event Action DataSetProcessingComplete;

        public bool IsOptimizationRun { get; private set; }
        private List<double> _previousParamValues;

        private string _startSymbol;
        private string _finalSymbol;

        public string StartSymbol
        {
            get { return _startSymbol; }
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
            get { return _finalSymbol; }
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

        private bool ValidateSymbol()
        {
            //need since DataSetSymbols is null when constructor is called
            if (_startSymbol != null && !DataSetSymbols.Contains(_startSymbol))
                throw new Exception($"{nameof(StartSymbol)} not correct symbol name, check existing dataset items");
            if (_finalSymbol != null && !DataSetSymbols.Contains(_finalSymbol))
                throw new Exception($"{nameof(FinalSymbol)}: not correct symbol name, check existing dataset items");

            //check for null to avoid symbol filtering if they is not assigned from outside
            return
                (_startSymbol == null || DataSetSymbols.IndexOf(Bars.Symbol) >= DataSetSymbols.IndexOf(StartSymbol)) &&
                (_finalSymbol == null || DataSetSymbols.IndexOf(Bars.Symbol) <= DataSetSymbols.IndexOf(FinalSymbol));
        }

        protected override void Execute()
        {
            if (ValidateSymbol())
            {
                OnOptimizationStart();

                OnOptimizationCycleStart();

                OnDataSetProcessingStart();

                OnSymbolProcessingStart();

                try
                {
                    _strategy.Execute();
                }
                catch (Exception ex)
                {
                    PrintDebug
                        (
                        " (" + Bars.Symbol + ") " +
                        "an exception was generated in the user execution method: " +
                        ex.Message
                        );
                }

                OnSymbolProcessingComplete();

                OnDataSetProcessingComplete();

                OnOptimizationComplete();
            }
        }

        private void OnOptimizationStart()
        {
            if (Bars.Symbol == (StartSymbol ?? DataSetSymbols.First()) &&
                Parameters != null &&
                Parameters.Count > 0 &&
                Parameters.All(param => param.Value == param.Start))
            {
                try
                {
                    Action optimizationStart = OptimizationStart;
                    if (optimizationStart != null)
                        optimizationStart.Invoke();
                }
                catch (Exception ex)
                {
                    throw new Exception(" [optimization start handlers exception] " + ex.Message, ex);
                }

                IsOptimizationRun = true;
                _previousParamValues = Parameters.Select(param => param.Start).ToList();
            }
        }

        private void OnOptimizationCycleStart()
        {
            if (IsOptimizationRun &&
                Bars.Symbol == (StartSymbol ?? DataSetSymbols.First()) &&
                !Parameters.All(param => param.Value == _previousParamValues[Parameters.IndexOf(param)]))
            {
                try
                {
                    Action optimizationCycleStart = OptimizationCycleStart;
                    if (optimizationCycleStart != null)
                        optimizationCycleStart.Invoke();
                }
                catch (Exception ex)
                {
                    throw new Exception(" [optimization cycle start handlers exception] " + ex.Message, ex);
                }

                _previousParamValues = Parameters.Select(param => param.Value).ToList();
            }
        }

        private void OnOptimizationComplete()
        {
            if (IsOptimizationRun &&
                Bars.Symbol == (FinalSymbol ?? DataSetSymbols.Last()) &&
                Parameters != null &&
                Parameters.Count > 0 &&
                Parameters.All(param => param.Value == param.Stop))
            {
                try
                {
                    Action optimizationComplete = OptimizationComplete;
                    if (optimizationComplete != null)
                        optimizationComplete.Invoke();
                }
                catch (Exception ex)
                {
                    throw new Exception(" [optimization complete handlers exception] " + ex.Message, ex);
                }

                IsOptimizationRun = false;
                _previousParamValues.Clear();
            }
        }

        private void OnSymbolProcessingStart()
        {
            try
            {
                Action symbolProcessingStart = SymbolProcessingStart;

                if (symbolProcessingStart != null)
                    symbolProcessingStart.Invoke();
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
                Action symbolProcessingComplete = SymbolProcessingComplete;

                if (symbolProcessingComplete != null)
                    symbolProcessingComplete.Invoke();
            }
            catch (Exception ex)
            {
                throw new Exception(" [symbol processing compete handlers exception] " + ex.Message, ex);
            }
        }

        private void OnDataSetProcessingStart()
        {
            if (Bars.Symbol == (StartSymbol ?? DataSetSymbols.First()))
            {
                try
                {
                    Action dataSetProcessingStart = DataSetProcessingStart;

                    if (dataSetProcessingStart != null)
                        dataSetProcessingStart.Invoke();
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
                    Action dataSetProcessingComplete = DataSetProcessingComplete;

                    if (dataSetProcessingComplete != null)
                        dataSetProcessingComplete.Invoke();
                }
                catch (Exception ex)
                {
                    throw new Exception(" [dataset processing complete handlers exception] " + ex.Message, ex);
                }
            }
        }

        //need for reason of protected access to default CreateParameter method
        new public StrategyParameter CreateParameter(string name, double defaultValue, double start, double stop, double step)
        {
            if (defaultValue == start)
                throw new ArgumentException(
                    "Avoid using the default values for optimization parameters that match their start values." +
                    "Instead, try to find other way to test these parameter values", nameof(defaultValue));
            else
            {
                var param = base.CreateParameter(name, defaultValue, start, stop, step);
                return param;
            }
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
