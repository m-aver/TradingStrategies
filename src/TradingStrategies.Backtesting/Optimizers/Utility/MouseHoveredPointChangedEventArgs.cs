namespace TradingStrategies.Backtesting.Optimizers.Utility;

public readonly struct MouseHoveredPointChangedEventArgs(int currentPointIdx, int previousPointIdx)
{
    public int CurrentPointIdx { get; } = currentPointIdx;
    public int PreviousPointIdx { get; } = previousPointIdx;
};
