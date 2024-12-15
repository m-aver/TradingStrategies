using WealthLab;
using WealthLab.Visualizers;

//простой способ расширять визуализеры
//без необходимости реализовывать win forms контролы

//есть идейка отправлять результаты стратегии (SystemPerformance) в матлаб
//например через http по локалхосту

namespace TradingStrategies.Backtesting.Visualizers
{
    public class TestTradesVisualizer : PVTradeList, IPerformanceVisualizer
    {
        private SystemPerformance _performance;

        string IPerformanceVisualizer.TabText => "test tab";

        void IPerformanceVisualizer.CreateVisualization(SystemPerformance performance, IVisualizerHost visHost)
        {
            _performance = performance;
            base.CreateVisualization(performance, visHost);
        }

        void IPerformanceVisualizer.CopyToClipboard()
        {
            base.CopyToClipboard();
        }
    }
}
