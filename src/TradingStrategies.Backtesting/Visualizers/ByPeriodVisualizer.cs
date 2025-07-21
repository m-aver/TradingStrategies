using Steema.TeeChart;
using Steema.TeeChart.Styles;
using System.Drawing;
using System.Windows.Forms;
using WealthLab;
using WealthLab.Visualizers;

//расширяет встроенный By Period визуалайзер

//- по умолчанию выбирается месячный период
//- значения вертикальной оси продублированы справа от диаграммы
//- возможность вывести значения свечей в аннотациях

namespace TradingStrategies.Backtesting.Visualizers
{
    public class ByPeriodVisualizer : PVByPeriod, IPerformanceVisualizer
    {
        //если не переопределять TabText, то замещает базовый
        //string IPerformanceVisualizer.TabText => "ByPeriod";

        private readonly ComboBox byPeriodBox;
        private readonly TChart returnsChart;
        private readonly ToolStripComboBox chartUnitsBox;

        private readonly Bar returnsBar;
        private readonly Bar returnsBarDuplicate;

        private ToolStripMenuItem mniShowRightAxis;
        private ToolStripMenuItem mniShowAnnotations;

        private int sourceBarWidthPercent;

        public ByPeriodVisualizer() : base()
        {
            byPeriodBox = FetchPeriodSelectionBox();
            returnsChart = FetchRawReturnsChart();
            chartUnitsBox = FetchCmbChartUnits();
            returnsBar = FetchRawReturnsBar();
            returnsBarDuplicate = new Bar();

            InitializeComponent();
            InitializeBarSeries();
        }

        void IPerformanceVisualizer.CreateVisualization(SystemPerformance performance, IVisualizerHost visHost)
        {
            base.CreateVisualization(performance, visHost);

            byPeriodBox.SelectedIndex = byPeriodBox.Items.IndexOf("Monthly");

            RefreshBar();
        }

        private void RefreshBar()
        {
            returnsBarDuplicate.Visible = mniShowRightAxis.Checked;

            if (mniShowRightAxis.Checked == false)
            {
                returnsBar.BarWidthPercent = sourceBarWidthPercent;
                returnsBar.OffsetPercent = 0;
                return;
            }

            returnsBarDuplicate.AssignValues(returnsBar);
            returnsChart.Axes.Right.Labels.ValueFormat = returnsChart.Axes.Left.Labels.ValueFormat;

            //прозрачность чтобы скрыть дублирующие свечи и их аннотации
            returnsBarDuplicate.Colors = new ColorList(returnsBarDuplicate.Colors.Select(x => Color.Transparent).ToArray());

            //дубликаты сдвигают исходные свечи
            //ширина и оффсет чтобы симулировать исходный размер и положение свечей на диаграмме
            returnsBar.BarWidthPercent = sourceBarWidthPercent * 2;
            returnsBar.OffsetPercent = 50;
            returnsBarDuplicate.OffsetPercent = -50;
        }

        private ComboBox FetchPeriodSelectionBox() =>
            (ComboBox)((SplitContainer)base.Controls[0]).Panel1.Controls[1].Controls[20];
        private TChart FetchRawReturnsChart() =>
            (TChart)FetchChartHost().Controls[0].Controls[0];
        private ToolStripComboBox FetchCmbChartUnits() =>
            (ToolStripComboBox)FetchChartHost().ContextMenuStrip.Items[0];
        private Control FetchChartHost() =>
            ((SplitContainer)base.Controls[0]).Panel1.Controls[0];
        private Bar FetchRawReturnsBar() =>
            (Bar)FetchRawReturnsChart().Series[0];

        private void InitializeComponent()
        {
            byPeriodBox.SelectedIndexChanged += byPeriodBox_SelectedIndexChanged;
            chartUnitsBox.SelectedIndexChanged += chartUnitsBox_SelectedIndexChanged;

            mniShowRightAxis = new ToolStripMenuItem();
            mniShowRightAxis.Click += mniShowRightAxis_Click;
            mniShowRightAxis.Checked = true;
            mniShowRightAxis.CheckState = CheckState.Checked;
            mniShowRightAxis.Name = "mniShowRightAxis";
            mniShowRightAxis.Size = new Size(268, 22);
            mniShowRightAxis.Text = "Show right axis";

            mniShowAnnotations = new ToolStripMenuItem();
            mniShowAnnotations.Click += mniShowAnnotations_Click;
            mniShowAnnotations.Checked = false;
            mniShowAnnotations.CheckState = CheckState.Unchecked;
            mniShowAnnotations.Name = "mniShowAnnotations";
            mniShowAnnotations.Size = new Size(268, 22);
            mniShowAnnotations.Text = "Show annotations";

            var popup = FetchChartHost().ContextMenuStrip;
            popup.Items.Insert(0, mniShowRightAxis);
            popup.Items.Insert(1, mniShowAnnotations);
        }

        private void InitializeBarSeries()
        {
            sourceBarWidthPercent = returnsBar.BarWidthPercent;

            returnsBar.Marks.Arrow.Color = Color.FromArgb(224, 224, 224);
            returnsBar.Marks.Style = MarksStyles.Value;

            returnsBar.Marks.Callout.Length = 10;
            returnsBar.Marks.Callout.Style = PointerStyles.Nothing;
            returnsBar.Marks.Callout.Color = Color.Transparent;
            returnsBar.GetSeriesMark += returnsBar_GetSeriesMark;

            //только через добавление новой серии можно добавить еще одну ось
            returnsBarDuplicate.Assign(returnsBar);
            returnsChart.Series.Add(this.returnsBarDuplicate);
            returnsBarDuplicate.VertAxis = VerticalAxis.Right;
            returnsBarDuplicate.GetSeriesMark += returnsBar_GetSeriesMark;
            returnsBarDuplicate.Pen.Visible = false;
        }

        private void mniShowRightAxis_Click(object sender, EventArgs e)
        {
            mniShowRightAxis.Checked = !mniShowRightAxis.Checked;
            returnsBarDuplicate.Visible = mniShowRightAxis.Checked;

            RefreshBar();
        }

        private void mniShowAnnotations_Click(object sender, EventArgs e)
        {
            mniShowAnnotations.Checked = !mniShowAnnotations.Checked;
            returnsBar.Marks.Visible = mniShowAnnotations.Checked;
            returnsBarDuplicate.Marks.Visible = mniShowAnnotations.Checked;

            RefreshBar();
        }

        private void chartUnitsBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            //подогнать масштаб вертикальной оси
            returnsChart.Axes.Left.Automatic = true;
            returnsChart.Axes.Right.Automatic = true;

            RefreshBar();
        }

        private void byPeriodBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            //восстановить масштаб
            returnsChart.Zoom.Undo();

            RefreshBar();
        }

        private void returnsBar_GetSeriesMark(Series series, GetSeriesMarkEventArgs e)
        {
            if (double.TryParse(e.MarkText, out var value))
            {
                e.MarkText = chartUnitsBox.SelectedItem.ToString() switch
                {
                    "Percent" => (value * 100).ToString("N0"),
                    "Dollar" => FormatAmount(value),
                    _ => e.MarkText,
                };
            }
        }

        private static string FormatAmount(double amount)
        {
            const double billion = 1_000_000_000.0;
            const double million = 1_000_000.0;
            const double thousand = 1_000.0;

            return Math.Abs(amount) switch
            {
                >= billion => $"{(amount / billion):F1} B",
                >= million => $"{(amount / million):F1} M",
                >= thousand => $"{(amount / thousand):F0} K",
                _ => $"{amount}"
            };
        }   
    }
}
