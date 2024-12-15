namespace TradingStrategies.Backtesting.Core
{
    public interface IStrategyExecuter
    {
        void Initialize();
        void Execute();
    }
}
