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
//позволяет добавить метки времени на ось дат
//позволяет выровнять сетку оси дат на начало дня

namespace TradingStrategies.Backtesting.Visualizers
{
    public class EquityCurveVisualizer : PVEquityCurve, IPerformanceVisualizer
    {
        //string IPerformanceVisualizer.TabText => "EquityCurve";

        private readonly TChart chart;
        private readonly Area cashArea;
        private readonly Area equityArea;
        private readonly Area openPositionsArea;
        private readonly ToolStripMenuItem mniShowBuyAndHold;

        private Line expRegCurve;

        private ToolStripMenuItem mniShowCash;
        private ToolStripMenuItem mniShowGrid;
        private ToolStripMenuItem mniShowExpReg;
        private ToolStripMenuItem mniShowTimeLabels;
        private ToolStripMenuItem mniAlignGridToStartOfDay;

        private SystemPerformance performance;

        public EquityCurveVisualizer() : base()
        {
            chart = FetchChart();
            cashArea = FetchCashArea();
            equityArea = FetchEquityArea();
            openPositionsArea = FetchOpenPositionsArea();
            mniShowBuyAndHold = FetchShowBuyAndHoldToggle();

            InitializeComponent();
        }

        void IPerformanceVisualizer.CreateVisualization(SystemPerformance performance, IVisualizerHost visHost)
        {
            this.performance = performance;

            base.CreateVisualization(performance, visHost);

            CreateExponentialRegressionVisualization(performance);

            mniShowBuyAndHold.PerformClick(); //hide B&H
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
            return IndicatorsCalculator.CalculateExponentialRegression(equitySeries.ToPoints());
        }

        private void SetEquityDateLabelsFormat(string format)
        {
            if (performance is null)
            {
                return;
            }

            foreach (var (equity, i) in performance.Results.EquityCurve.ToPoints().Select((x, i) => (x, i)))
            {
                equityArea.Labels[i] = equity.Date.ToString(format);
            }

            chart.Refresh();
        }

        private void AlignDatesGridToStartOfDay()
        {
            if (performance is null)
            {
                return;
            }

            //restore default
            if (!mniAlignGridToStartOfDay.Checked)
            {
                chart.Axes.Bottom.Labels.Items.Clear();
                chart.Axes.Bottom.Labels.Angle = 0;
                return;
            }

            var prev = new DataSeriesPoint(0, DateTime.MinValue);
            foreach (var (equity, i) in performance.Results.EquityCurve.ToPoints().Select((x, i) => (x, i)))
            {
                if (equity.Date.Date > prev.Date.Date)
                {
                    var label = new AxisLabelItem(chart.Chart)
                    {
                        Value = i,
                        Text = equity.Date.ToString("dd.MM.yy"),
                        Color = Color.Transparent,
                        AutoSize = true,
                    };
                    if (equity.Date.Month > prev.Date.Month)
                    {
                        label.Font.Color = Color.Red;
                    }
                    label.Pen.Color = Color.Transparent;
                    label.Brush.Color = Color.Transparent;
                    label.Brush.ForegroundColor = Color.Transparent;

                    chart.Axes.Bottom.Labels.Items.Add(label);
                    prev = equity;
                }
            }
            chart.Axes.Bottom.Labels.Angle = 90;

            chart.Refresh();
        }

        private TChart FetchChart() => (TChart)Controls[1];
        private Area FetchEquityArea() => (Area)FetchChart().Series[0];
        private Area FetchCashArea() => (Area)FetchChart().Series[1];
        private Area FetchOpenPositionsArea() => (Area)FetchChart().Series[5];
        private ToolStripMenuItem FetchShowBuyAndHoldToggle() =>
            FetchChart().ContextMenuStrip.Items.OfType<ToolStripMenuItem>().First(x => x.Name == "mniShowBuyAndHold");

        private void InitializeComponent()
        {
            equityArea.Opacity = 50;
            openPositionsArea.Opacity = 25;
            cashArea.Visible = false;

            mniShowCash = new ToolStripMenuItem();
            mniShowCash.Click += mniShowCash_Click;
            mniShowCash.Checked = false;
            mniShowCash.CheckState = CheckState.Unchecked;
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
            mniShowExpReg.Checked = false;
            mniShowExpReg.CheckState = CheckState.Unchecked;
            mniShowExpReg.Name = "mniShowExpReg";
            mniShowExpReg.Size = new Size(268, 22);
            mniShowExpReg.Text = "Show Equity exponential regression";

            mniShowTimeLabels = new ToolStripMenuItem();
            mniShowTimeLabels.Click += mniShowTimeLabels_Click;
            mniShowTimeLabels.Checked = false;
            mniShowTimeLabels.CheckState = CheckState.Unchecked;
            mniShowTimeLabels.Name = "mniShowTimeLabels";
            mniShowTimeLabels.Size = new Size(268, 22);
            mniShowTimeLabels.Text = "Show Equity time labels";

            mniAlignGridToStartOfDay = new ToolStripMenuItem();
            mniAlignGridToStartOfDay.Click += mniAlignGridToStartOfDay_Click; ;
            mniAlignGridToStartOfDay.Checked = false;
            mniAlignGridToStartOfDay.CheckState = CheckState.Unchecked;
            mniAlignGridToStartOfDay.Name = "mniAlignGridToStartOfDay";
            mniAlignGridToStartOfDay.Size = new Size(268, 22);
            mniAlignGridToStartOfDay.Text = "Align dates grid to start of day";

            var popup = chart.ContextMenuStrip;
            popup.Items.Insert(0, mniShowCash);
            popup.Items.Insert(1, mniShowGrid);
            popup.Items.Insert(2, mniShowExpReg);
            popup.Items.Insert(3, mniShowTimeLabels);
            popup.Items.Insert(4, mniAlignGridToStartOfDay);

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
            expRegCurve.Visible = false;

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

        private void mniShowTimeLabels_Click(object sender, EventArgs e)
        {
            mniShowTimeLabels.Checked = !mniShowTimeLabels.Checked;

            var format = mniShowTimeLabels.Checked ? "dd.MM.yyyy HH:mm" : "dd.MM.yyyy";
            SetEquityDateLabelsFormat(format);
        }

        private void mniAlignGridToStartOfDay_Click(object sender, EventArgs e)
        {
            mniAlignGridToStartOfDay.Checked = !mniAlignGridToStartOfDay.Checked;
            AlignDatesGridToStartOfDay();
        }
    }
}
