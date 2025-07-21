using Steema.TeeChart;
using Steema.TeeChart.Styles;
using System.Drawing;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Utility;
using WealthLab;
using WealthLab.Visualizers;

//расширяет встроенный Equity Curve визуалайзер

//позволяет убрать cash с графика
//позволяет отобразить кривую экспоненциальной регрессии

namespace TradingStrategies.Backtesting.Visualizers
{
    public class EquityCurveVisualizer : PVEquityCurve, IPerformanceVisualizer
    {
        //string IPerformanceVisualizer.TabText => "EquityCurve";

        private readonly TChart chart;
        private readonly Area cashArea;
        private readonly Area equityArea;
        private readonly Area openPositionsArea;

        private Line expRegCurve;

        private ToolStripMenuItem mniShowCash;
        private ToolStripMenuItem mniShowGrid;
        private ToolStripMenuItem mniShowExpReg;

        public EquityCurveVisualizer() : base()
        {
            chart = FetchChart();
            cashArea = FetchCashArea();
            equityArea = FetchEquityArea();
            openPositionsArea = FetchOpenPositionsArea();

            InitializeComponent();
        }

        void IPerformanceVisualizer.CreateVisualization(SystemPerformance performance, IVisualizerHost visHost)
        {
            CreateVisualization(performance, visHost);

            CreateExponentialRegressionVisualization(performance);
        }

        private void CreateExponentialRegressionVisualization(SystemPerformance performance)
        {
            var equitySeries = performance.Results.EquityCurve;

            if (equitySeries is null || equitySeries.Count == 0)
            {
                return;
            }

            var expReg = CalculateExponentialRegression(equitySeries);

            expRegCurve.BeginUpdate();

            expRegCurve.Clear();

            foreach (var (reg, i) in expReg.Select((x, i) => (x, i)))
            {
                expRegCurve.Add(i, reg.Value, reg.Date.ToShortDateString());
            }

            expRegCurve.EndUpdate();
        }

        private static IEnumerable<DataSeriesPoint> CalculateExponentialRegression(DataSeries equitySeries)
        {
            return IndicatorsCalculator.CalculateExponentialRegression(equitySeries);
        }

        private TChart FetchChart() => (TChart)Controls[1];
        private Area FetchEquityArea() => (Area)FetchChart().Series[0];
        private Area FetchCashArea() => (Area)FetchChart().Series[1];
        private Area FetchOpenPositionsArea() => (Area)FetchChart().Series[5];

        private void InitializeComponent()
        {
            equityArea.Opacity = 50;
            openPositionsArea.Opacity = 25;

            mniShowCash = new ToolStripMenuItem();
            mniShowCash.Click += mniShowCash_Click;
            mniShowCash.Checked = true;
            mniShowCash.CheckState = CheckState.Checked;
            mniShowCash.Name = "mniShowCash";
            mniShowCash.Size = new Size(268, 22);
            mniShowCash.Text = "Show Cash";

            mniShowGrid = new ToolStripMenuItem();
            mniShowGrid.Click += mniShowGrid_Click;
            mniShowGrid.Checked = false;
            mniShowGrid.CheckState = CheckState.Unchecked;
            mniShowGrid.Name = "mniShowGrid";
            mniShowGrid.Size = new Size(268, 22);
            mniShowGrid.Text = "Show Grid";

            mniShowExpReg = new ToolStripMenuItem();
            mniShowExpReg.Click += mniShowExpReg_Click;
            mniShowExpReg.Checked = true;
            mniShowExpReg.CheckState = CheckState.Checked;
            mniShowExpReg.Name = "mniShowExpReg";
            mniShowExpReg.Size = new Size(268, 22);
            mniShowExpReg.Text = "Show Equity exponential regression";

            var popup = chart.ContextMenuStrip;
            popup.Items.Insert(0, mniShowCash);
            popup.Items.Insert(1, mniShowGrid);
            popup.Items.Insert(2, mniShowExpReg);

            expRegCurve = new Line();
            expRegCurve.Brush.Color = Color.FromArgb(0, 0, 255);
            expRegCurve.Color = Color.FromArgb(0, 0, 255);
            expRegCurve.ColorEach = false;
            expRegCurve.LinePen.Color = Color.FromArgb(0, 0, 128);
            expRegCurve.LinePen.Width = 2;
            expRegCurve.Marks.Callout.ArrowHead = 0;
            expRegCurve.Marks.Callout.ArrowHeadSize = 8;
            expRegCurve.Marks.Callout.Brush.Color = Color.Black;
            expRegCurve.Marks.Callout.Distance = 0;
            expRegCurve.Marks.Callout.Draw3D = false;
            expRegCurve.Marks.Callout.Length = 10;
            expRegCurve.Marks.Callout.Style = 0;
            expRegCurve.Marks.Callout.Visible = false;
            expRegCurve.Pointer.Brush.Color = Color.Red;
            expRegCurve.Pointer.Style = 0;
            expRegCurve.Title = "Exponential Regression";
            expRegCurve.ValueFormat = "#,##0.00 ExpReg";
            expRegCurve.XValues.DataMember = "X";
            expRegCurve.XValues.Order = (ValueListOrder)1;
            expRegCurve.YValues.DataMember = "Y";

            chart.Series.Add(expRegCurve);
        }

        private void mniShowCash_Click(object sender, EventArgs e)
        {
            mniShowCash.Checked = !mniShowCash.Checked;
            cashArea.Visible = mniShowCash.Checked;
        }

        private void mniShowGrid_Click(object sender, EventArgs e)
        {
            mniShowGrid.Checked = !mniShowGrid.Checked;
            equityArea.GetHorizAxis.Grid.Visible = mniShowGrid.Checked;
            equityArea.GetVertAxis.Grid.Visible = mniShowGrid.Checked;
        }

        private void mniShowExpReg_Click(object sender, EventArgs e)
        {
            mniShowExpReg.Checked = !mniShowExpReg.Checked;
            expRegCurve.Visible = mniShowExpReg.Checked;
        }
    }
}
