using Fidelity.Components;
using Steema.TeeChart;
using Steema.TeeChart.Styles;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WealthLab;
using WealthLab.Visualizers;

//расширяет встроенный Profit Distribution визуалайзер

//позволяет масштабировать количество выборок

//NOTE: 
//свечи показывают максимальные значения
//т.е. выборка 0% при условии шага в 1% показывает луз сделки с доходностью от -1% до 0%,
//а выборка 1% - профит от 0% до 1%

//Position.NetProfitPercent выдает соотношение между ценой входа и ценой выхода позиции
//это соответствует доходности от сделки в смысле увеличения общего капитала, когда цена входа в позицию равняется текущему капиталу
//т.е. вход на все деньги и без плечей
//если позиция была взята на половину портфеля, то доходность от сделки будет в 2 раза ниже NetProfitPercent, если позиция взята на весь портфель с плечом х2, то доходность будет х2

namespace TradingStrategies.Backtesting.Visualizers
{
    public class ProfitDistributionVisualizer : PVProfitDist, IPerformanceVisualizer
    {
        //string IPerformanceVisualizer.TabText => "ProfitDist";

        private readonly TChart chart;
        private readonly Bar barArea;
        private readonly Distribution distribution;

        private ToolStripMenuItem mniShowGrid;
        private ToolStripMenuItem mniShow3d;
        private TrackBar tbScale;

        private SystemPerformance performance;
        private IVisualizerHost visHost;

        public ProfitDistributionVisualizer() : base()
        {
            distribution = FetchDistribution();
            chart = FetchChart();
            barArea = FetchBar();

            InitializeComponent();
        }

        void IPerformanceVisualizer.CreateVisualization(SystemPerformance performance, IVisualizerHost visHost)
        {
            this.performance = performance;
            this.visHost = visHost;

            base.CreateVisualization(performance, visHost);
        }

        private TChart FetchChart() => (TChart)base.Controls[0];
        private Bar FetchBar() => (Bar)FetchChart().Series[0];

        private static readonly FieldInfo distributionField = typeof(PVProfitDist)
            .GetField("distribution_0", BindingFlags.Instance | BindingFlags.NonPublic);
        private Distribution FetchDistribution() => (Distribution)distributionField.GetValue(this);

        private void InitializeComponent()
        {
            mniShowGrid = new ToolStripMenuItem();
            mniShowGrid.Click += mniShowGrid_Click;
            mniShowGrid.Checked = false;
            mniShowGrid.CheckState = CheckState.Unchecked;
            mniShowGrid.Name = "mniShowGrid";
            mniShowGrid.Size = new Size(268, 22);
            mniShowGrid.Text = "Show Grid";

            mniShow3d = new ToolStripMenuItem();
            mniShow3d.Click += mniShow3d_Click;
            mniShow3d.Checked = true;
            mniShow3d.CheckState = CheckState.Checked;
            mniShow3d.Name = "mniShow3d";
            mniShow3d.Size = new Size(268, 22);
            mniShow3d.Text = "Show 3D";

            var separatorScale = new ToolStripSeparator();
            separatorScale.Size = new Size(178, 6);

            var lblScale = new ToolStripLabel();
            lblScale.Text = "Select scale";
            lblScale.Size = new Size(268, 22);

            tbScale = new TrackBar();
            tbScale.Size = new Size(268, 10);
            tbScale.Minimum = 0;
            tbScale.Maximum = 75;
            tbScale.TickFrequency = 5;
            tbScale.Value = distribution.BinsDesired;
            tbScale.Scroll += tbScale_Scroll;

            var popup = base.ContextMenuStrip;
            popup.Items.Insert(0, mniShowGrid);
            popup.Items.Insert(1, mniShow3d);

            popup.Items.Add(separatorScale);
            popup.Items.Add(lblScale);
            popup.Items.Add(new ToolStripControlHost(tbScale));
        }

        private void mniShowGrid_Click(object sender, EventArgs e)
        {
            mniShowGrid.Checked = !mniShowGrid.Checked;
            barArea.GetHorizAxis.Grid.Visible = mniShowGrid.Checked;
            barArea.GetVertAxis.Grid.Visible = mniShowGrid.Checked;
        }

        private void mniShow3d_Click(object sender, EventArgs e)
        {
            mniShow3d.Checked = !mniShow3d.Checked;
            chart.Aspect.View3D = mniShow3d.Checked;
        }

        private void tbScale_Scroll(object sender, EventArgs e)
        {
            distribution.BinsDesired = tbScale.Value;

            this.CreateVisualization(performance, visHost);
        }
    }
}
