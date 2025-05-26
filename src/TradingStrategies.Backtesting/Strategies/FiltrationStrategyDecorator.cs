using TradingStrategies.Backtesting.Core;
using TradingStrategies.Backtesting.Utility;
using WealthLab;

//позволяет увеличить производительность оптимизации
//предварительно отфильтровав сессию по результатам, доступным на момент завершения выполнения стратегии

namespace TradingStrategies.Backtesting.Strategies;

internal interface IStrategyResultFilter
{
    bool FilterResults();
}

internal class FiltrationStrategyDecorator : IStrategyExecuter
{
    private readonly WealthScriptWrapper _sw;
    private readonly IStrategyExecuter _strategy;
    private readonly IStrategyResultFilter _filter;
    private TradingSystemExecutor? _executor;

    public FiltrationStrategyDecorator(WealthScriptWrapper scriptWrapper, IStrategyExecuter strategy, IStrategyResultFilter filter)
    {
        _sw = scriptWrapper;
        _strategy = strategy;
        _filter = filter;
    }

    public void Execute() => _strategy.Execute();

    public void Initialize()
    {
        //в общем случае может быть важен порядок вызовов
        //скоуп обработчиков декоратора должен быть шире скоупа обработчиков стратегии

        _sw.DataSetProcessingStart += DataSetProcessingStartHandler;

        _strategy.Initialize();

        _sw.DataSetProcessingComplete += DataSetProcessingCompleteHandler;
    }

    private void DataSetProcessingStartHandler()
    {
        if (_sw.IsOptimizationRun)
        {
            _executor = WealthScriptHelper.ExtractExecutor(_sw);
        }

        //восстанавливаем признак, кажется он всегда true
        if (_executor != null)
        {
            _executor.BuildEquityCurves = true;
        }
    }

    private void DataSetProcessingCompleteHandler()
    {
        if (_sw.IsOptimizationRun && 
            _executor != null && 
            _filter.FilterResults())
        {
            _executor.BuildEquityCurves = false;
            _executor.Performance = new SystemPerformance(_sw.Strategy);
        }
    }

    //есть несколько способов увеличить производительность
    //- WealthScript.ClearPositions : самый простой в поддержке, но самый слабый
    //  проблема в вызове TradingSystemExecutor.ApplyPositionSize - долго выполянется даже для пустого списка позиций
    //- выброс эксепшена, например с помощью WealthScript.Abort : работает только с кастомными оптимизерами
    //  нужно устанавливать TradingSystemExecutor.ExceptionEvents в false
    //  требует поддержать выбрасываемый эксепшен в WealthScriptWrapper и оптимизерах
    //  не влияет на выполнение стратегии, запущенных из кода фреймворка
    //  не требует знать о запущенной оптимизации из кода, выводит результаты стратегии даже при выброшенном эксепшене
    //  блочит побочные потоки в параллельных оптимизерах
    //- TradingSystemExecutor.BuildEquityCurves, самый эффективный способ (отфильтрованные запуски быстрее в среднем в 4 раза)
    //  требует получать TradingSystemExecutor рефлексей
    //  требует перевыставлять TradngSystemExecutor.BuildEquityCurves в последующих запусках, чтобы выводились результаты в обычном режиме
}
