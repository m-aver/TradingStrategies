namespace TradingStrategies.Backtesting.Core
{
    internal interface IStrategyExecuter
    {
        void Initialize();
        void Execute();
    }
}
