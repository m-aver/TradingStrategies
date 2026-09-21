using Steema.TeeChart.Styles;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Optimizers.Charts.Controls;
using TradingStrategies.Backtesting.Optimizers.Utility;
using TradingStrategies.Backtesting.Utility;

//рисует симметричную матрицу корреляций всех переданных результатов на основе их доходностей (каждый с каждым)
//фичи
//возможность задать период для расчета доходностей (по умолчанию 1 месяц) (панель опций)
//отображение параметров и метрик систем под указателем мыши на панели деталей
//возможность перетащить матрицу из другого окна (drag and drop)
// - добавление внешней матрицы к исходной и расчет корреляций их комбинаций
// - нужно удерживать CTRL при перемещении
//возможность подсчитать все возможные группы систем, взаимные корреляции которых лежат в указанном диапазоне (панель опций)
// - на практике нужно чтобы найти комбинации систем, имеющие слабые корреляции друг с другом, объединив которые в теории можно получить хорошее усреднение доходностей
// - сейчас расчет brutforce и может требовать много времени, особенно если передано много систем и выбран большой диапазон корреляций для поиска
// - выводит результаты в MessageBox и в Clipboard

namespace TradingStrategies.Backtesting.Optimizers.Charts;

//что бы еще
//отображение параметров комбинации под указателем мыши
//1D диаграмки по осям выводящие метрику под выбор из скорекарда
//или может выводить отдельным цветовым слоем величину произвольной метрики или области где метрика достигает определенных значений

//возможность задать порядок сортировки по значениям параметров
//сортировка по "расстоянию" в пространстве параметров, например для 3d пространства это радиус сферы от некоторой опорной точки - стратегии с некоторым набором параметров, считается видимо как декартово произведение от разницы значений параметров (или от их индексов в упорядоченном простанстве)
//сортировка по значению корреляции ? есть ли смысл
//можно задать "идеальную" опорную стратегию для сравнения корреляций - видимо с равномерно распределенными доходностями

public class OptResultsCorrelationMatrix : UserControl
{
    private TChartEx graph;
    private ColorGrid colorGrid;

    private OptimizationResultListEx results;
    private DataSeriesPoint[][] returnsSeries;

    private IPeriodicalSeriesCalculator periodicalCalculator = PeriodicalSeriesCalculatorFactory.CreateAlignedSingleton();
    private PeriodInfo periodInfo = PeriodInfo.Monthly;

    internal void UpdateResults(OptimizationResultListEx results, bool doSort = true)
    {
        //сортировка, т.к. начальный порядок мог быть нарушен например параллельным оптимизером
        //TODO: при перетаскивании резалта нужно соритровать их отдельно
        //просится список OptimizationResultListEx, и менюшка откуда можно потом их удалять/скрывать/показывать
        if (doSort)
        {
            results = SortResults(results);
        }

        this.results = results;

        var rank = results.ResultsEx.Count;
        returnsSeries = new DataSeriesPoint[rank][];
        for (int i = 0; i < rank; i++)
        {
            var result = results.ResultsEx[i];
            var equity = result.Performance.Results.EquityCurve;
            returnsSeries[i] = periodicalCalculator.CalculatePercentDiff(equity, periodInfo).ToArray();
        }

        GenerateMatrix();
    }

    private OptimizationResultListEx SortResults(OptimizationResultListEx results)
    {
        var sortedResults = new OptimizationResultListEx()
        {
            Names = results.Names,
            StrategyID = results.StrategyID,
            Scorecard = results.Scorecard,
            OptimizationMethod = results.OptimizationMethod,
            ParameterNames = results.ParameterNames,
        };

        var ordering = results.ResultsEx.OrderBy(x => x.ParameterValues[0]);
        for (int i = 1; i < results.ParameterNames.Count; i++)
        {
            var idx = i; //capture index
            ordering = ordering.ThenBy(x => x.ParameterValues[idx]);
        }

        //расстояние в пространстве параметров
        //ordering = ordering.OrderBy(x => Math.Sqrt(x.ParameterValues.Sum(MathHelper.Sqr)));
        //ordering = ordering.OrderBy(x => Math.Sqrt(x.ParameterValues.Select((p, i) => (p, i)).Sum(x => MathHelper.Sqr(x.p / parameters[x.i].Step))));

        sortedResults.ResultsEx = ordering.ToList();

        return sortedResults;
    }

    private void GenerateMatrix()
    {
        if (results is null)
        {
            return;
        }

        var rank = returnsSeries.Length;
        var matrix = new double[rank, rank];

        for (int i = 0; i < rank; i++)
        {
            for (int j = 0; j < rank; j++)
            {
                var resultOne = returnsSeries[i];
                var resultTwo = returnsSeries[j];

                var corr = IndicatorsCalculator.Correlation(resultOne, resultTwo, force: true);
                matrix[i, j] = corr;
            }
        }

        DrawCorrelationHeatmap(matrix, []);
    }

    public OptResultsCorrelationMatrix()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        graph = new TChartEx();
        graph.Dock = DockStyle.Fill;
        Controls.Add(graph);

        var popup = new ContextMenuStrip();
        popup.Size = new Size(175, 48);
        graph.ContextMenuStrip = popup;
        graph.Header.Text = "Correlation Matrix Heatmap";

        ClientSize = new Size(800, 600);

        InitializeChart();
        InitializeCustomToolTip();
        InitializeDetailsPane();
        InitializeOptionsPane();
        InitializeDragAndDrop();
    }

    private void InitializeChart()
    {
        graph.Aspect.View3D = false;
        graph.Legend.Visible = false;

        graph.Series.Clear();

        colorGrid = new ColorGrid();
        colorGrid.Pen.Visible = false;

        graph.Series.Add(colorGrid);

        graph.MouseHoveredPointChanged += Graph_MouseHoveredPointChanged;
    }

    private void DrawCorrelationHeatmap(double[,] matrix, string[] labels)
    {
        colorGrid.Clear();

        int sizeX = matrix.GetLength(0);
        int sizeY = matrix.GetLength(1);

        // Add points to the grid
        for (int y = 0; y < sizeY; y++)
        {
            for (int x = 0; x < sizeX; x++)
            {
                var corr = matrix[x, y];
                colorGrid.Add(x + 1, corr, y + 1, GetColor(corr));
            }
        }

        // Set axes labels
        var bottomAxis = graph.Axes.Bottom;
        var leftAxis = graph.Axes.Left;

        bottomAxis.Labels.Items.Clear();
        leftAxis.Labels.Items.Clear();

        for (int i = 0; i < labels.Length; i++)
        {
            bottomAxis.Labels.Items.Add(i + 1, labels[i]);
            leftAxis.Labels.Items.Add(i + 1, labels[i]);
        }

        // Invert to match matrix layout
        //leftAxis.Inverted = true;
    }

    public static Color GetColor(double value)
    {
        value = MathHelper.MinMax(value, -1, 1);

        if (value < 0)
        {
            // Interpolate between Blue (-1) and White (0)
            double t = (value + 1) / 1; // Normalize to [0,1]
            return InterpolateColor(Color.Blue, Color.White, t);
        }
        else if (value > 0)
        {
            // Interpolate between White (0) and Red (1)
            double t = value; // Already in [0,1]
            return InterpolateColor(Color.White, Color.Red, t);
        }
        else if (value == 0)
        {
            return Color.White;
        }
        else //NaN
        {
            return Color.Gray;
        }
    }

    private static Color InterpolateColor(Color color1, Color color2, double t)
    {
        int r = (int)(color1.R + (color2.R - color1.R) * t);
        int g = (int)(color1.G + (color2.G - color1.G) * t);
        int b = (int)(color1.B + (color2.B - color1.B) * t);
        return Color.FromArgb(r, g, b);
    }

    //tooltips
    private ToolStripMenuItem mniTrackCursor;
    private void InitializeCustomToolTip()
    {
        mniTrackCursor = new ToolStripMenuItem();
        mniTrackCursor.Click += mniTrackCursor_Click;
        mniTrackCursor.Checked = true;
        mniTrackCursor.CheckState = CheckState.Checked;
        mniTrackCursor.Name = "mniTrackCursor";
        mniTrackCursor.Size = new Size(268, 22);
        mniTrackCursor.Text = "Track cursor";

        var separator = new ToolStripSeparator();
        separator.Size = new Size(178, 6);

        var popup = graph.ContextMenuStrip;
        popup.Items.Insert(0, mniTrackCursor);
        popup.Items.Insert(1, separator);
    }

    private void mniTrackCursor_Click(object sender, EventArgs e)
    {
        mniTrackCursor.Checked = !mniTrackCursor.Checked;
    }

    //highlight point
    private Color prevColor;
    private void Graph_MouseHoveredPointChanged(object sender, MouseHoveredPointChangedEventArgs e)
    {
        if (mniTrackCursor.Checked == false)
        {
            return;
        }

        var idx = e.CurrentPointIdx;
        var prevIdx = e.PreviousPointIdx;

        Color clr;
        switch (idx, prevIdx)
        {
            case (-1, -1): return;
            case (-1, _):
                {
                    clr = prevColor;
                    colorGrid.Colors[prevIdx] = prevColor;
                    break;
                }
            case (_, -1):
                {
                    clr = colorGrid.Colors[idx];
                    colorGrid.Colors[idx] = Color.YellowGreen;
                    break;
                }
            default:
                {
                    clr = colorGrid.Colors[idx];
                    colorGrid.Colors[idx] = Color.YellowGreen;
                    colorGrid.Colors[prevIdx] = prevColor;
                    break;
                }
        }
        prevColor = clr;

        colorGrid.Invalidate();
    }

    //details pane
    private Panel pnlDetails;
    private Label lblCurrCorr;
    private Label lblCurrX;
    private Label lblCurrY;
    private ToolStripMenuItem mniShowDetails;

    private void InitializeDetailsPane()
    {
        lblCurrCorr = new Label();
        lblCurrCorr.AutoSize = true;
        lblCurrCorr.Location = new Point(5, 5);
        lblCurrCorr.Size = new Size(20, 20);
        lblCurrCorr.Text = "place for current correlation value";

        lblCurrX = new Label();
        lblCurrX.AutoSize = true;
        lblCurrX.Location = new Point(5, 35);
        lblCurrX.Size = new Size(40, 13);
        lblCurrX.Text = "place for current X point info";

        lblCurrY = new Label();
        lblCurrY.AutoSize = true;
        lblCurrY.Location = new Point(200, 35);
        lblCurrY.Size = new Size(40, 13);
        lblCurrY.Text = "place for current Y point info";

        pnlDetails = new Panel();
        pnlDetails.Dock = DockStyle.Left;
        pnlDetails.Location = new Point(30, 0);
        pnlDetails.Name = "pnlDetails";
        pnlDetails.Size = new Size(400, 61);
        pnlDetails.TabIndex = 1;

        pnlDetails.Controls.Add(lblCurrCorr);
        pnlDetails.Controls.Add(lblCurrX);
        pnlDetails.Controls.Add(lblCurrY);

        mniShowDetails = new ToolStripMenuItem();
        mniShowDetails.Click += mniShowDetails_Click; ;
        mniShowDetails.Checked = false;
        mniShowDetails.CheckState = CheckState.Unchecked;
        mniShowDetails.Name = "mniShowDetails";
        mniShowDetails.Size = new Size(268, 22);
        mniShowDetails.Text = "Show details pane";

        var popup = graph.ContextMenuStrip;
        popup.Items.Insert(0, mniShowDetails);

        pnlDetails.Visible = false;
        Controls.Add(pnlDetails);

        graph.MouseHoveredPointChanged += Graph_MouseHoveredPointChanged1;
    }

    private void mniShowDetails_Click(object sender, EventArgs e)
    {
        mniShowDetails.Checked = !mniShowDetails.Checked;
        pnlDetails.Visible = mniShowDetails.Checked;
    }

    //fill details pane
    private void Graph_MouseHoveredPointChanged1(object sender, MouseHoveredPointChangedEventArgs e)
    {
        if (mniTrackCursor.Checked == false)
        {
            return;
        }

        var idx = e.CurrentPointIdx;

        if (idx == -1)
        {
            return;
        }

        var x = (int)colorGrid.XValues[idx]; //x
        var y = (int)colorGrid.ZValues[idx]; //y

        var corr = colorGrid.YValues[idx];

        //TODO: надо проверить что все правильно сделано
        var resultX = results.ResultsEx[x - 1];
        var resultY = results.ResultsEx[y - 1];

        var descResultX = DescribeResult(resultX);
        var descResultY = DescribeResult(resultY);

        var newLine = Environment.NewLine;

        lblCurrCorr.Text = $"Correlation: {corr}";
        lblCurrX.Text = $"By X axis: {x}" + $"{newLine}{newLine}{descResultX}";
        lblCurrY.Text = $"By Y axis: {y}" + $"{newLine}{newLine}{descResultY}";
    }

    private string DescribeResult(OptimizationResultEx result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Parameters:");
        for (var i = 0; i < result.ParameterNames.Count; i++)
        {
            var line = $"{result.ParameterNames[i]}: {result.ParameterValues[i]}";
            builder.AppendLine(line);
        }
        builder.AppendLine();
        builder.AppendLine("Metrics:");
        for (var i = 0; i < result.Names.Count; i++)
        {
            var line = $"{result.Names[i]}: {result.Results[i]}";
            builder.AppendLine(line);
        }

        return builder.ToString();
    }

    //drag and drop external matrix
    private Point _dragStartPoint;
    private bool _isDragging = false;

    private void InitializeDragAndDrop()
    {
        graph.AllowDrop = true;
        graph.DragEnter += Graph_DragEnter;
        graph.DragDrop += Graph_DragDrop;
        graph.MouseDown += Graph_MouseDown;
        graph.MouseMove += Graph_MouseMove;
    }

    private void Graph_MouseDown(object sender, MouseEventArgs e)
    {
        // Record the starting point when mouse is pressed
        _dragStartPoint = e.Location;
        _isDragging = false;
    }

    private void Graph_MouseMove(object sender, MouseEventArgs e)
    {
        //allow drag only when: mouse is pressed and moved by SystemInformation.DragSize and CTRL is pressed
        if (e.Button == MouseButtons.Left &&
            (Control.ModifierKeys & Keys.Control) == Keys.Control)
        {
            int dx = Math.Abs(e.X - _dragStartPoint.X);
            int dy = Math.Abs(e.Y - _dragStartPoint.Y);

            if (!_isDragging && (dx >= SystemInformation.DragSize.Width || dy >= SystemInformation.DragSize.Height))
            {
                _isDragging = true;
                graph.DoDragDrop(results, DragDropEffects.Copy);
            }
        }
    }

    private void Graph_DragEnter(object sender, DragEventArgs e)
    {
        if (_isDragging == false &&
            e.Data.GetDataPresent(typeof(OptimizationResultListEx)) &&
            (e.KeyState & 8) == 8) //CTRL is pressed
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void Graph_DragDrop(object sender, DragEventArgs e)
    {
        var externalResult = (OptimizationResultListEx)e.Data.GetData(typeof(OptimizationResultListEx));

        AddExternalResult(externalResult);
    }

    private void AddExternalResult(OptimizationResultListEx externalResult)
    {
        results.ResultsEx.AddRange(externalResult.ResultsEx);

        UpdateResults(results, doSort: false);
    }

    //options pane
    protected Panel pnlOptions;
    private ToolStripMenuItem mniShowOptions;

    private void InitializeOptionsPane()
    {
        pnlOptions = new Panel();
        pnlOptions.Dock = DockStyle.Right;
        pnlOptions.Location = new Point(0, 0);
        pnlOptions.Name = "pnlOptions";
        pnlOptions.Size = new Size(200, 61);
        pnlOptions.TabIndex = 1;

        mniShowOptions = new ToolStripMenuItem();
        mniShowOptions.Click += mniShowOptions_Click; ;
        mniShowOptions.Checked = false;
        mniShowOptions.CheckState = CheckState.Unchecked;
        mniShowOptions.Name = "mniShowOptions";
        mniShowOptions.Size = new Size(268, 22);
        mniShowOptions.Text = "Show options pane";

        var popup = graph.ContextMenuStrip;
        popup.Items.Insert(0, mniShowOptions);

        pnlOptions.Visible = false;
        Controls.Add(pnlOptions);

        InitializePeriodSelection();
        InitializeCorrGroupsCalc();
    }

    private void mniShowOptions_Click(object sender, EventArgs e)
    {
        mniShowOptions.Checked = !mniShowOptions.Checked;
        pnlOptions.Visible = mniShowOptions.Checked;
    }

    //period selection
    private ComboBox cmbPeriodUnit;
    private NumericUpDown nmPeriodNum;

    private void InitializePeriodSelection()
    {
        var lblPeriod = new Label();
        lblPeriod.Text = "Select period of returns to calc correlations";
        lblPeriod.Size = new Size(200, 20);
        lblPeriod.Location = new Point(10, 140);

        var lblUnits = new Label();
        lblUnits.Text = "units:";
        lblUnits.Size = new Size(30, 20);
        lblUnits.Location = new Point(10, 170);

        cmbPeriodUnit = new ComboBox();
        cmbPeriodUnit.Size = new Size(100, 30);
        cmbPeriodUnit.Location = new Point(50, 170);
        cmbPeriodUnit.DataSource = Enum.GetValues(typeof(Period));
        //cmbPeriodUnit.SelectedIndex = (int)Period.Month;
        cmbPeriodUnit.SelectedIndexChanged += CmbPeriodUnit_SelectedIndexChanged;

        var lblSize = new Label();
        lblSize.Text = "size:";
        lblSize.Size = new Size(30, 20);
        lblSize.Location = new Point(10, 200);

        nmPeriodNum = new NumericUpDown();
        nmPeriodNum.Size = new Size(100, 20);
        nmPeriodNum.Location = new Point(50, 200);
        nmPeriodNum.Minimum = 1;
        nmPeriodNum.Maximum = 1000;
        nmPeriodNum.Value = 1;
        nmPeriodNum.ValueChanged += NmPeriodNum_ValueChanged;

        pnlOptions.Controls.Add(lblPeriod);
        pnlOptions.Controls.Add(lblUnits);
        pnlOptions.Controls.Add(cmbPeriodUnit);
        pnlOptions.Controls.Add(lblSize);
        pnlOptions.Controls.Add(nmPeriodNum);
    }

    private void CmbPeriodUnit_SelectedIndexChanged(object sender, EventArgs e) => ChangePeriod();
    private void NmPeriodNum_ValueChanged(object sender, EventArgs e) => ChangePeriod();

    private void ChangePeriod()
    {
        try
        {
            var period = (Period)cmbPeriodUnit.SelectedIndex;

            if (period < 0) return;

            var units = (int)nmPeriodNum.Value;
            periodInfo = new PeriodInfo(period, units);

            UpdateResults(results);
        }
        catch
        {
        }
    }

    //calc correlation groups
    private NumericUpDown nmMinCorr;
    private NumericUpDown nmMaxCorr;
    private NumericUpDown nmMinGroupSize;
    private Button btnCalcCorrGroups;
    private CheckBox cbFilterHeatMap;

    private void InitializeCorrGroupsCalc()
    {
        var lblGroups = new Label();
        lblGroups.Text = "Calc correlation groups";
        lblGroups.Size = new Size(150, 20);
        lblGroups.Location = new Point(10, 300);

        var lblMin = new Label();
        lblMin.Text = "Min:";
        lblMin.Size = new Size(30, 20);
        lblMin.Location = new Point(10, 330);

        nmMinCorr = new NumericUpDown();
        nmMinCorr.Size = new Size(100, 20);
        nmMinCorr.Location = new Point(50, 330);
        nmMinCorr.Minimum = -1;
        nmMinCorr.Maximum = 1;
        nmMinCorr.Value = -1;
        nmMinCorr.Increment = 0.01m;
        nmMinCorr.DecimalPlaces = 2;
        nmMinCorr.ValueChanged += NmMinCorr_ValueChanged;

        var lblMax = new Label();
        lblMax.Text = "Max:";
        lblMax.Size = new Size(30, 20);
        lblMax.Location = new Point(10, 360);

        //TOD0: не отображаются дробные числа, хотя вроде значение норм приходит
        nmMaxCorr = new NumericUpDown();
        nmMaxCorr.Size = new Size(100, 20);
        nmMaxCorr.Location = new Point(50, 360);
        nmMaxCorr.Minimum = -1;
        nmMaxCorr.Maximum = 1;
        nmMaxCorr.Value = 1;
        nmMaxCorr.Increment = 0.01m;
        nmMaxCorr.DecimalPlaces = 2;
        nmMaxCorr.ValueChanged += NmMaxCorr_ValueChanged;

        var lblGroupSize = new Label();
        lblGroupSize.Text = "Min group size:";
        lblGroupSize.Size = new Size(95, 20);
        lblGroupSize.Location = new Point(10, 390);

        nmMinGroupSize = new NumericUpDown();
        nmMinGroupSize.Size = new Size(45, 20);
        nmMinGroupSize.Location = new Point(105, 390);
        nmMinGroupSize.Minimum = 1;
        nmMinGroupSize.Maximum = 100;
        nmMinGroupSize.Value = 2;

        btnCalcCorrGroups = new Button();
        btnCalcCorrGroups.Size = new Size(100, 50);
        btnCalcCorrGroups.Location = new Point(50, 420);
        btnCalcCorrGroups.Text = "Do calc";
        btnCalcCorrGroups.Click += BtnCalcCorrGroups_Click;

        var lblFilter = new Label();
        lblFilter.Text = "Filter heat map on changes";
        lblFilter.Size = new Size(150, 20);
        lblFilter.Location = new Point(50, 500);

        cbFilterHeatMap = new CheckBox();
        cbFilterHeatMap.Size = new Size(30, 30);
        cbFilterHeatMap.Location = new Point(10, 500);
        cbFilterHeatMap.Checked = false;
        cbFilterHeatMap.CheckState = CheckState.Unchecked;
        cbFilterHeatMap.Click += CbFilterHeatMap_Click;

        pnlOptions.Controls.Add(lblGroups);
        pnlOptions.Controls.Add(lblMin);
        pnlOptions.Controls.Add(nmMinCorr);
        pnlOptions.Controls.Add(lblMax);
        pnlOptions.Controls.Add(nmMaxCorr);
        pnlOptions.Controls.Add(lblGroupSize);
        pnlOptions.Controls.Add(nmMinGroupSize);
        pnlOptions.Controls.Add(btnCalcCorrGroups);
        pnlOptions.Controls.Add(lblFilter);
        pnlOptions.Controls.Add(cbFilterHeatMap);
    }

    private void CbFilterHeatMap_Click(object sender, EventArgs e)
    {
        try
        {
            if (cbFilterHeatMap.Checked)
                DoHeatMapFilter();
            else
                GenerateMatrix(); //restore colors
        }
        catch
        {
        }
    }

    private void NmMinCorr_ValueChanged(object sender, EventArgs e) => DoHeatMapFilter();
    private void NmMaxCorr_ValueChanged(object sender, EventArgs e) => DoHeatMapFilter();
    private void BtnCalcCorrGroups_Click(object sender, EventArgs e) => DoCalcCorrelationGroups();

    private void DoHeatMapFilter()
    {
        if (cbFilterHeatMap.Checked == false)
        {
            return;
        }

        var min = (double)nmMinCorr.Value;
        var max = (double)nmMaxCorr.Value;

        for (int i = 0; i < colorGrid.Count; i++)
        {
            var corr = colorGrid.YValues[i];

            if ((corr >= min && corr <= max) == false)
            {
                colorGrid.Colors[i] = Color.Transparent;
            }
            else
            {
                colorGrid.Colors[i] = GetColor(corr);
            }
        }

        colorGrid.Invalidate();
    }

    private void DoCalcCorrelationGroups()
    {
        var matrix = GetCurrentMatrix();

        var min = (double)nmMinCorr.Value;
        var max = (double)nmMaxCorr.Value;
        var size = (int)nmMinGroupSize.Value;

        var groups = CalcCorrelationGroups(matrix, min, max);
        groups = groups.Where(g => g.Length >= size).ToArray();

        //значения в heatmap начинаются с 1
        foreach (var group in groups)
            for (var i = 0; i < group.Length; i++)
                group[i]++;

        ShowCorrelationGroups(groups);
    }

    private double[,] GetCurrentMatrix()
    {
        var rank = returnsSeries.Length;
        var matrix = new double[rank, rank];

        for (int i = 0; i < colorGrid.Count; i++)
        {
            var x = (int)colorGrid.XValues[i];
            var y = (int)colorGrid.ZValues[i];
            var corr = colorGrid.YValues[i];

            matrix[x - 1, y - 1] = corr;
        }

        return matrix;
    }

    private void ShowCorrelationGroups(IEnumerable<int[]> groups)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Correlation groups: ");
        foreach (var group in groups)
        {
            var row = string.Join(",", group);
            builder.AppendLine(row);
        }
        var display = builder.ToString();

        MessageBox.Show(display);

        //clipboard
        builder = builder.Clear();
        foreach (var group in groups)
        {
            var row = string.Join(",", group);
            builder.AppendLine(row);
            builder.AppendLine("------------");

            foreach (var i in group)
            {
                var paramRow = string.Join(",", results.ResultsEx[i - 1].ParameterValues.Select(p => p.ToString(CultureInfo.InvariantCulture)));
                builder.AppendLine($"{i}:{paramRow}");
            }

            builder.AppendLine("============");
        }
        var clipboard = builder.ToString();

        Clipboard.SetText(clipboard);
    }

    private static IEnumerable<int[]> CalcCorrelationGroups(double[,] matrix, double minCorr, double maxCorr)
    {
        var groups = CalcCorrelationGroupsBrutforce(matrix, minCorr, maxCorr);

        return groups.Distinct(SymmetricCorrelationGroupsComparer.Instance);
    }

    private class SymmetricCorrelationGroupsComparer : IEqualityComparer<int[]>
    {
        public static SymmetricCorrelationGroupsComparer Instance { get; } = new();
        //public bool Equals(int[] x, int[] y) => x.SequenceEqual(y);
        public bool Equals(int[] x, int[] y) => x.OrderBy(v => v).SequenceEqual(y.OrderBy(v => v));
        public int GetHashCode(int[] obj) => obj.Aggregate((x, hash) => hash ^ x);
    }

    //сгруппировывает значения из симметричной матрицы корреляций так, чтобы взаимные корреляции каждого из элементов находились в указанном диапазоне
    //возвращает список групп из индексов элементов в матрице
    private static IEnumerable<int[]> CalcCorrelationGroupsBrutforce(double[,] matrix, double minCorr, double maxCorr)
    {
        if (matrix.GetLength(0) != matrix.GetLength(1))
        {
            throw new ArgumentException($"Correlation matrix is not symmetric");
        }

        var rank = matrix.GetLength(0);

        var group = new List<int>(rank);

        for (int y = 0; y < rank; y++)
        {
            group.Add(y);

            //начинать с y быстрее, но тогда сложнее избавляться от повторяющихся групп
            for (int x = 0 /*y*/; x < rank; x++)
            {
                if (group.All(g =>
                {
                    var corr = matrix[x, g];
                    return corr >= minCorr && corr <= maxCorr;
                }))
                {
                    group.Add(x);
                }
            }

            if (group.Count > 0)
            {
                yield return group.ToArray();
                //yield return group.Skip(1).ToArray();
            }

            group.Clear();
        }
    }
}