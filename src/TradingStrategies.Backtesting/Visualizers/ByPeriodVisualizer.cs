using System.Windows.Forms;
using WealthLab;
using WealthLab.Visualizers;

//настраивает встроенный визуалайзер так, чтобы по умолчанию выбирался месячный период

namespace TradingStrategies.Backtesting.Visualizers
{
    public class ByPeriodVisualizer : PVByPeriod, IPerformanceVisualizer
    {
        //если не переопределять TabText, то замещает базовый
        //string IPerformanceVisualizer.TabText => "ByPeriod";

        private readonly ComboBox byPeriodBox;

        public ByPeriodVisualizer() : base()
        {
            byPeriodBox = FetchPeriodSelectionBox();
        }

        void IPerformanceVisualizer.CreateVisualization(SystemPerformance performance, IVisualizerHost visHost)
        {
            base.CreateVisualization(performance, visHost);

            byPeriodBox.SelectedIndex = byPeriodBox.Items.IndexOf("Monthly");
        }

        private ComboBox FetchPeriodSelectionBox()
        {
            return (ComboBox)((SplitContainer)base.Controls[0]).Panel1.Controls[1].Controls[20];
        }
    }
}
