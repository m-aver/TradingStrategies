using Steema.TeeChart;
using Steema.TeeChart.Styles;
using System.Drawing;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Utility;
using TradingStrategies.Utilities.InternalsProxy;
using WealthLab;
using WealthLab.Visualizers;

//расширяет встроенный By Period визуалайзер

//- по умолчанию выбирается месячный период
//- значения вертикальной оси продублированы справа от диаграммы
//- возможность вывести значения свечей в аннотациях
//- возможность пересчитать периоды на момент вечернего клиринга, удобно для согласования результатов тестирования с данными реальных торгов
//- возможность выбрать одну из побочных стратегий при работе с комбинированной стратегией

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

        private ToolStrip strategySelectionToolStrip;
        private ToolStripLabel strategySelectionLabel;
        private ToolStripComboBox strategySelectionComboBox;

        private ToolStripMenuItem mniShowRightAxis;
        private ToolStripMenuItem mniShowAnnotations;
        private ToolStripMenuItem mniShiftToEveningClearing;

        private int sourceBarWidthPercent;

        private SystemPerformance performance;
        private IVisualizerHost visualizer;

        public ByPeriodVisualizer() : base()
        {
            byPeriodBox = FetchPeriodSelectionBox();
            returnsChart = FetchRawReturnsChart();
            chartUnitsBox = FetchCmbChartUnits();
            returnsBar = FetchRawReturnsBar();
            returnsBarDuplicate = new Bar();

            InitializeComponent();
            InitializeBarSeries();
            InitializeStrategySelector();
        }

        //точка входа для первого вызова из фреймворка
        void IPerformanceVisualizer.CreateVisualization(SystemPerformance performance, IVisualizerHost visHost)
        {
            this.performance = performance;
            this.visualizer = visHost;

            ConfigureStrategySelector(performance);

            UpdateVisualization();

            byPeriodBox.SelectedIndex = byPeriodBox.Items.IndexOf("Monthly");
        }

        //для апдейтов из кода контрола
        private void UpdateVisualization()
        {
            if (performance is null || visualizer is null)
            {
                return;
            }

            var targetPerformance = BuildPerformance();
            base.CreateVisualization(targetPerformance, visualizer);
            RefreshBar();
        }

        private SystemPerformance BuildPerformance()
        {
            var performance = BuildPerformanceByStrategySelection();
            performance = BuildPerformanceByEveningShifting(performance);

            return performance;
        }

        private void ConfigureStrategySelector(SystemPerformance performance)
        {
            if (performance.Strategy.StrategyType == StrategyType.CombinedStrategy)
            {
                strategySelectionToolStrip.Visible = true;

                strategySelectionComboBox.Items.Clear();
                strategySelectionComboBox.Items.Add("Strategies in Aggregate");

                foreach (var combinedStrategyChild in performance.Strategy.CombinedStrategyChildren)
                {
                    strategySelectionComboBox.Items.Add(combinedStrategyChild);
                }

                strategySelectionComboBox.SelectedIndex = 0;
            }
            else
            {
                strategySelectionToolStrip.Visible = false;
            }
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

            mniShiftToEveningClearing = new ToolStripMenuItem();
            mniShiftToEveningClearing.Click += mniShiftToEveningClearing_Click; ;
            mniShiftToEveningClearing.Checked = false;
            mniShiftToEveningClearing.CheckState = CheckState.Unchecked;
            mniShiftToEveningClearing.Name = "mniShiftToEveningClearing";
            mniShiftToEveningClearing.Size = new Size(268, 22);
            mniShiftToEveningClearing.Text = "Shift periods to evening clearings";

            var popup = FetchChartHost().ContextMenuStrip;
            popup.Items.Insert(0, mniShowRightAxis);
            popup.Items.Insert(1, mniShowAnnotations);
            popup.Items.Insert(2, mniShiftToEveningClearing);
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

        private void InitializeStrategySelector()
        {
            strategySelectionToolStrip = new ToolStrip();
            strategySelectionComboBox = new ToolStripComboBox();
            strategySelectionLabel = new ToolStripLabel();

            strategySelectionToolStrip.GripStyle = ToolStripGripStyle.Hidden;
            strategySelectionToolStrip.Items.AddRange([strategySelectionComboBox, strategySelectionLabel]);
            strategySelectionToolStrip.Location = new Point(0, 0);
            strategySelectionToolStrip.Name = nameof(strategySelectionToolStrip);
            strategySelectionToolStrip.Size = new Size(522, 25);
            strategySelectionToolStrip.Visible = false;

            strategySelectionComboBox.Alignment = ToolStripItemAlignment.Right;
            strategySelectionComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            strategySelectionComboBox.DropDownWidth = 150;
            strategySelectionComboBox.Items.Add("Strategies in Aggregate");
            strategySelectionComboBox.Name = nameof(strategySelectionComboBox);
            strategySelectionComboBox.Size = new Size(160, 25);
            strategySelectionComboBox.Visible = true;
            strategySelectionComboBox.SelectedIndexChanged += strategySelectionComboBox_SelectedIndexChanged;

            strategySelectionLabel.Alignment = ToolStripItemAlignment.Right;
            strategySelectionLabel.Name = nameof(strategySelectionLabel);
            strategySelectionLabel.Size = new Size(35, 22);
            strategySelectionLabel.Text = "View:";
            strategySelectionLabel.Visible = true;

            Controls.Add(strategySelectionToolStrip);
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

        private void mniShiftToEveningClearing_Click(object sender, EventArgs e)
        {
            mniShiftToEveningClearing.Checked = !mniShiftToEveningClearing.Checked;

            UpdateVisualization();
        }

        private SystemPerformance BuildPerformanceByEveningShifting(SystemPerformance performance)
        {
            if (mniShiftToEveningClearing.Checked)
            {
                return ShiftToEveningClearing(performance);
            }
            else
            {
                return performance;
            }
        }

        private static SystemPerformance ShiftToEveningClearing(SystemPerformance performance)
        {
            var shiftedEquity = ShiftToEveningClearing(performance.Results.EquityCurve);
            var shiftedCash = ShiftToEveningClearing(performance.Results.CashCurve);

            //тут бы еще даты позиций сместить, чтобы корректно отображалось количество входов/выходов за период, но там не так просто
            var shiftedPerformance = new SystemPerformance(performance.Strategy);
            shiftedPerformance.CashReturnRate = performance.CashReturnRate;
            shiftedPerformance.Results.EquityCurveProxy = shiftedEquity;
            shiftedPerformance.Results.CashCurveProxy = shiftedCash;
            shiftedPerformance.Results.RawPositions = performance.Results.Positions.ToList();

            return shiftedPerformance;
        }

        private static DataSeries ShiftToEveningClearing(DataSeries series)
        {
            if (series.Count == 0)
            {
                return series;
            }

            var eveningClearingTime = new TimeSpan(19, 0, 0);

            var reversedSeries = series.ToPoints().Reverse();

            var shiftedSeries = new Stack<DataSeriesPoint>(); //return reverse order

            var nextPeriodDate = DateTime.MaxValue;

            foreach (var point in reversedSeries)
            {
                //базовая реализация не опирается на сортировку серии, поэтому можно не перезаписывать время смещенных точек
                var shiftedPoint = point.Date.TimeOfDay >= eveningClearingTime 
                    ? new(point.Value, nextPeriodDate)
                    : point;

                shiftedSeries.Push(shiftedPoint);

                nextPeriodDate = shiftedPoint.Date;
            }

            return shiftedSeries.ToSeries($"shifted {series.Description}");
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

        private void strategySelectionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateVisualization();
        }

        private SystemPerformance BuildPerformanceByStrategySelection()
        {
            if (strategySelectionComboBox.SelectedIndex is 0 or -1)
            {
                return performance;
            }
            else
            {
                var combinedStrategyInfo = (CombinedStrategyInfo)strategySelectionComboBox.SelectedItem;
                var childPerfomance = performance.GenerateChildStrategyPerformance(combinedStrategyInfo, visualizer.GetExecutor());
                return childPerfomance;
            }
        }
    }
}
