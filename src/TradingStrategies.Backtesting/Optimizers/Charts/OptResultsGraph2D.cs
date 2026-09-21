using Steema.TeeChart;
using Steema.TeeChart.Styles;
using Steema.TeeChart.Tools;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Optimizers.Charts.Controls;
using TradingStrategies.Backtesting.Optimizers.Utility;
using TradingStrategies.Backtesting.Utility;
using WealthLab;
using Panel = System.Windows.Forms.Panel;
using ParameterSlider = TradingStrategies.Backtesting.Optimizers.Charts.Controls.ParameterSlider;

//переработанная версия встроенного OptResultsGraph2D
//дополнительно:
//слайдеры для выбора значения параметров
//запуск бэктеста по клику на точку графика
//вывод инфы о точке графика под указателем мыши
//окрашивание графика взависимости от величины метрики
//зум по колесику мыши и настройки масштаба

namespace TradingStrategies.Backtesting.Optimizers.Charts;

//TODO:

//можно еще ускорить отрисовку/расчеты распараллеливанием
//кажется если добавлять точки на поверхность не в цикле, а один раз из предготовленных массивов, то мб будет побыстрее, т.к. внутри вызывается Invalidate

//добавить поверхности с max и min результатами по всему датасету, кнопка для вкл/выкл отображение
//вывести список параметров на которых эти результаты достигаются в конкретной точке

//мб добавить возможность закрепить точку например по одиночному клику мыши
//типо окрасить ее например в красный и зафиксировать результаты на панели деталей 
//чтобы не терять результаты при перемещении курсора по графику
//можно еще сделать например так, чтобы зафиксированная точка сохранялась при измении параметров (те что не выбраны как оси графика)
//чтобы можно было отследить ее при сдвиге графика, + обновление результатов на панели деталей

//настроить масштаб - сейчас колесиком мыши масштабируются только х и z (верхняя, y в англ терминологии) оси

public class OptResultsGraph2D : UserControl
{
    private Optimizer optimizer;
    protected WealthScript ws;
    private OptimizationResultList results;

    private Panel pnlTop;
    protected ComboBox cmbMetric;
    protected ComboBox cmbParameter1;
    protected ComboBox cmbParameter2;
    protected TChartEx graph;
    protected Surface surface;
    protected ComboBox cmbSymbol;

    private ListView resultsListView;
    protected OptimizationResultMap resultsMap;

    private List<ParameterSlider> parameterSliders = [];
    private List<double>[] parameterValues = [];

    public OptResultsGraph2D(Optimizer optimizer)
    {
        this.optimizer = optimizer;

        InitializeComponent();
    }

    public void RefreshView()
    {
        GenerateGraph();
    }

    private void Restore()
    {
        Array.Clear(parameterValues, 0, parameterValues.Length);
        parameterSliders.ForEach(pnlTop.Controls.Remove);
        parameterSliders.Clear();

        cmbParameter1.Items.Clear();
        cmbParameter2.Items.Clear();
        cmbMetric.Items.Clear();
        cmbSymbol.Items.Clear();

        lblCurr.Text = string.Empty;
    }

    internal void UpdateResults(OptimizationResultList results, WealthScript ws)
    {
        this.results = results;
        this.ws = ws;

        Restore();

        resultsMap = new OptimizationResultMap(results);
        parameterValues = OptimizationResultHelper.GetParameterValues(results);
        resultsListView = OptimizationFormExtractor.ExtractOptimizationResultListView(optimizer);

        foreach (var (parameter, i) in ws.Parameters.WithIndex())
        {
            cmbParameter1.Items.Add(parameter);
            cmbParameter2.Items.Add(parameter);

            InitializeParameterSlider(parameter, parameterValues[i]);
        }

        foreach (string name in results.Names)
        {
            cmbMetric.Items.Add(name);
        }
        foreach (var symbol in results.Symbols)
        {
            cmbSymbol.Items.Add(symbol);
        }
        if (results.Symbols.Count > 1)
        {
            cmbSymbol.Items.Add(WealthLabConsts.AverageSymbolResultCode);
        }

        if (cmbParameter1.Items.Count > 0)
        {
            cmbParameter1.SelectedIndex = 0;
        }
        if (cmbMetric.Items.Count > 0)
        {
            cmbMetric.SelectedIndex = 0;
        }
        if (cmbParameter2.Items.Count > 1)
        {
            cmbParameter2.SelectedIndex = 1;
        }
        if (cmbSymbol.Items.Count > 0)
        {
            cmbSymbol.SelectedIndex = 0;
        }

        GenerateGraph();
    }

    protected List<double> GetSelectedParameterValues()
    {
        return parameterSliders.Select(x => x.SelectedValue).ToList();
    }

    protected List<double> GetSelectedParameterValues(int pointIdx)
    {
        var paramValues = GetSelectedParameterValues();

        var param1Idx = cmbParameter1.SelectedIndex;
        var param2Idx = cmbParameter2.SelectedIndex;

        var param1Value = surface.XValues[pointIdx];
        var param2Value = surface.ZValues[pointIdx];

        paramValues[param1Idx] = param1Value;
        paramValues[param2Idx] = param2Value;

        return paramValues;
    }

    private void InitializeParameterSlider(StrategyParameter parameter, List<double> parameterValues)
    {
        var prevSlider = parameterSliders.LastOrDefault(x => x.Parent is not null);
        var start = prevSlider is null ? new Point(10, 68) : prevSlider.Location + new Size(prevSlider.Width, 0);

        var slider = new ParameterSlider(parameter, parameterValues);
        slider.Location = new Point(start.X, start.Y);
        slider.Scroll += ViewUpdated;

        if (parameter.IsEnabled)
        {
            pnlTop.Controls.Add(slider);
        }

        parameterSliders.Add(slider);
    }

    private bool NotInitialized =>
        results == null ||
        cmbParameter1.SelectedIndex == -1 ||
        cmbMetric.SelectedIndex == -1 ||
        cmbParameter2.SelectedIndex == -1 ||
        cmbParameter1.SelectedIndex == cmbParameter2.SelectedIndex ||
        cmbSymbol.SelectedIndex == -1;

    protected virtual void GenerateGraph()
    {
        if (NotInitialized)
        {
            return;
        }

        var parameter1 = (StrategyParameter)cmbParameter1.SelectedItem;
        var parameter2 = (StrategyParameter)cmbParameter2.SelectedItem;
        var param1Idx = cmbParameter1.SelectedIndex;
        var param2Idx = cmbParameter2.SelectedIndex;

        var symbolName = cmbSymbol.Text;
        var metricName = (string)cmbMetric.SelectedItem;
        var metricIndex = cmbMetric.SelectedIndex;

        var paramValues = GetSelectedParameterValues();

        if (graph.Zoom.Zoomed)
        {
            graph.Zoom.Undo();
        }

        surface.Clear();
        surface.IrregularGrid = true;

        graph.Axes.Bottom.Title.Text = parameter1.Name;
        graph.Axes.Left.Title.Text = metricName;
        graph.Axes.Depth.Title.Text = parameter2.Name;

        foreach (var (paramValue1, paramValue2) in parameterValues[param1Idx].FullJoin(parameterValues[param2Idx]))
        {
            paramValues[param1Idx] = paramValue1;
            paramValues[param2Idx] = paramValue2;

            var result = resultsMap.FindResult(symbolName, paramValues);

            if (result is null)
            {
                continue;
            }
            var metricValue = result.Results[metricIndex];

            if (double.IsNaN(metricValue))
            {
                continue;
            }
            if (double.IsInfinity(metricValue))
            {
                metricValue = 0;
            }

            surface.Add(paramValue1, metricValue, paramValue2);
        }

        ColorizeSurface();
    }

    private void InitializeComponent()
    {
        pnlTop = new Panel();
        cmbSymbol = new ComboBox();
        var lblSymbol = new Label();
        var lblBy = new Label();
        cmbParameter2 = new ComboBox();
        var lblMetric = new Label();
        cmbMetric = new ComboBox();
        cmbParameter1 = new ComboBox();
        var lblParameter = new Label();
        graph = new TChartEx();
        var popupGraph2d = new ContextMenuStrip();
        var mniCopyToClipboard = new ToolStripMenuItem();
        var mniPrint = new ToolStripMenuItem();
        surface = new Surface();
        var rotate = new Rotate();

        pnlTop.SuspendLayout();
        popupGraph2d.SuspendLayout();
        SuspendLayout();

        pnlTop.Controls.Add(cmbSymbol);
        pnlTop.Controls.Add(lblSymbol);
        pnlTop.Controls.Add(lblBy);
        pnlTop.Controls.Add(cmbParameter2);
        pnlTop.Controls.Add(lblMetric);
        pnlTop.Controls.Add(cmbMetric);
        pnlTop.Controls.Add(cmbParameter1);
        pnlTop.Controls.Add(lblParameter);
        pnlTop.Dock = DockStyle.Top;
        pnlTop.Location = new Point(0, 0);
        pnlTop.Size = new Size(609, 61 + 65); //расширение под слайдеры параметров

        cmbSymbol.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSymbol.FormattingEnabled = true;
        cmbSymbol.Location = new Point(57, 4);
        cmbSymbol.Size = new Size(76, 21);
        cmbSymbol.SelectedIndexChanged += ViewUpdated;

        lblSymbol.AutoSize = true;
        lblSymbol.Location = new Point(7, 7);
        lblSymbol.Size = new Size(44, 13);
        lblSymbol.Text = "Symbol:";

        lblBy.AutoSize = true;
        lblBy.Location = new Point(203, 37);
        lblBy.Size = new Size(18, 13);
        lblBy.Text = "by";

        cmbParameter2.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbParameter2.FormattingEnabled = true;
        cmbParameter2.Location = new Point(227, 34);
        cmbParameter2.Size = new Size(121, 21);
        cmbParameter2.SelectedIndexChanged += ViewUpdated;

        lblMetric.AutoSize = true;
        lblMetric.Location = new Point(139, 7);
        lblMetric.Size = new Size(39, 13);
        lblMetric.Text = "Metric:";

        cmbMetric.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbMetric.FormattingEnabled = true;
        cmbMetric.Location = new Point(184, 4);
        cmbMetric.Size = new Size(108, 21);
        cmbMetric.SelectedIndexChanged += ViewUpdated;

        cmbParameter1.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbParameter1.FormattingEnabled = true;
        cmbParameter1.Location = new Point(76, 34);
        cmbParameter1.Size = new Size(121, 21);
        cmbParameter1.SelectedIndexChanged += ViewUpdated;

        lblParameter.AutoSize = true;
        lblParameter.Location = new Point(7, 37);
        lblParameter.Size = new Size(63, 13);
        lblParameter.Text = "Parameters:";

        graph.Aspect.Chart3DPercent = 50;
        graph.Aspect.ColorPaletteIndex = 0;
        graph.Aspect.ZOffset = 0.0;
        graph.Aspect.Zoom = 95;
        graph.Aspect.ZoomFloat = 95.0;
        graph.Axes.Bottom.Title.Caption = "Parameter Value";
        graph.Axes.Bottom.Title.Lines = ["Parameter Value"];
        graph.Axes.Depth.Visible = true;
        graph.Axes.Left.Title.Caption = "Net Profit";
        graph.Axes.Left.Title.Lines = ["Net Profit"];
        graph.ContextMenuStrip = popupGraph2d;
        graph.Dock = DockStyle.Fill;
        graph.Header.Lines = [""];
        graph.Legend.Visible = false;
        graph.Location = new Point(0, 61);
        graph.Panning.Allow = ScrollModes.None;
        graph.Series.Add(surface);
        graph.Size = new Size(609, 379);
        graph.Tools.Add(rotate);

        popupGraph2d.Items.AddRange([mniCopyToClipboard, mniPrint]);
        popupGraph2d.Size = new Size(175, 48);

        mniCopyToClipboard.Size = new Size(174, 22);
        mniCopyToClipboard.Text = "Copy To Clipboard";
        mniCopyToClipboard.Click += CopyToClipboard_Click;

        mniPrint.Size = new Size(174, 22);
        mniPrint.Text = "Print";
        mniPrint.Click += Print_Click;

        surface.Brush.Color = Color.FromArgb(68, 102, 163);
        surface.Color = Color.FromArgb(68, 102, 163);
        surface.ColorEach = false;
        surface.Marks.Callout.ArrowHead = ArrowHeadStyles.None;
        surface.Marks.Callout.ArrowHeadSize = 8;
        surface.Marks.Callout.Brush.Color = Color.Black;
        surface.Marks.Callout.Distance = 0;
        surface.Marks.Callout.Draw3D = false;
        surface.Marks.Callout.Length = 10;
        surface.Marks.Callout.Style = PointerStyles.Rectangle;
        surface.Marks.Callout.Visible = false;
        surface.PaletteMin = 0.0;
        surface.PaletteStep = 0.0;
        surface.PaletteStyle = PaletteStyles.Pale;
        surface.Title = "surface";
        surface.XValues.DataMember = "X";
        surface.YValues.DataMember = "Y";
        surface.ZValues.DataMember = "Z";

        AutoScaleDimensions = new SizeF(6f, 13f);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(graph);
        Controls.Add(pnlTop);
        Size = new Size(609, 440);

        InitializeCustomToolTip();

        graph.MouseDoubleClick += Graph_MouseDoubleClick;
        graph.MouseWheel += Graph_MouseWheel;
        graph.MouseHoveredPointChanged += Graph_MouseHoveredPointChanged;

        InitializeDetailsPane();
        InitializeOptionsPane();

        pnlTop.ResumeLayout(false);
        pnlTop.PerformLayout();
        popupGraph2d.ResumeLayout(false);
        ResumeLayout(false);
    }

    //parse row and run backtest
    private void Graph_MouseDoubleClick(object sender, MouseEventArgs e)
    {
        if (mniTrackCursor.Checked == false || resultsListView is null)
        {
            return;
        }

        var pos = e.Location;
        var idx = surface.Clicked(pos);

        if (idx == -1)
        {
            return;
        }

        var paramValues = GetSelectedParameterValues(idx);

        var paramNames = ws.Parameters.Select(p => p.Name).ToHashSet();
        var paramNamesIdx = new List<int>(paramNames.Count);
        for (int i = 0; i < resultsListView.Columns.Count; i++)
            if (paramNames.Contains(resultsListView.Columns[i].Text))
                paramNamesIdx.Add(i);

        var row = resultsListView.Items
            .Cast<ListViewItem>()
            .FirstOrDefault(item => paramValues
                .Zip(paramNamesIdx, (value, i) => (value, i))
                .All(x => double.TryParse(item.SubItems[x.i].Text, out var value) && value == x.value));

        if (row is null)
        {
            return;
        }

        //к дабл клику по списку результатов привязан обрабочик запуска бектеста (см. класс WealthLabPro.Optimization)
        resultsListView.SelectedItems.Clear();
        row.Selected = true;
        resultsListView.OnDoubleClick();
    }

    private void ViewUpdated(object sender, EventArgs e)
    {
        GenerateGraph();
    }

    private void CopyToClipboard_Click(object sender, EventArgs e)
    {
        try
        {
            Clipboard.SetImage(graph.Bitmap);
        }
        catch (ExternalException)
        {
            MessageBox.Show("Copy to clipboard was blocked by another process. Please try again", "ClipBoard Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
        }
    }

    private void Print_Click(object sender, EventArgs e)
    {
        optimizer.PrintHost.Print("Optimization 2 Parameter Graph", graph.Bitmap, bUseDefaultDisclosure: true);
    }

    //градиент поверхности в зависимости от высоты точки

    private const int StepsNum = 5;
    private static readonly double[] StepsPct = Enumerable //[0, 0.01, 20, 40, 60, 80] %
        .Range(0, StepsNum)
        .Select(x => x * 100.0 / StepsNum)
        .Append(0.01) //добавочный уровень, чтобы отличать области от абсолютного 0
        .OrderBy(x => x)
        .ToArray();

    private void ColorizeSurface()
    {
        var min = surface.GetVertAxis.Minimum;
        var max = surface.GetVertAxis.Maximum;

        var cMin = min < 0 && max > 0 ? 0 : min; //если диапазон разделен на положительную и отрицательную области, то разделяем их

        for (int i = 0; i < surface.Count; i++)
        {
            var value = surface.YValues[i];
            var cMax = value < 0 ? min : max;
            var percent = 100 * (value - cMin) / (cMax - cMin);
            percent = double.IsNaN(percent) ? 0 : percent;
            percent = StepsPct.Where(s => s <= percent).MaxOrDefault(); //квантуем по уровням
            var diff = (int)Math.Ceiling(255 * percent / 100);
            diff = diff is >= 1 and <= 11 ? 11 : diff; //меньшую интенсивность сложно разглядеть
            var color = value > 0
                ? Color.FromArgb(255, 255 - diff, 255 - diff)  //красный в положительном диапазоне
                : Color.FromArgb(255 - diff, 255 - diff, 255); //синий в отрицательном диапазоне
            surface.Colors[i] = color;
        }

        ColorizeParamLines();
    }

    private void ColorizeParamLines()
    {
        if (mniShowParamLines.Checked == false)
        {
            return;
        }

        var paramValues = GetSelectedParameterValues();
        var param1Value = paramValues[cmbParameter1.SelectedIndex];
        var param2Value = paramValues[cmbParameter2.SelectedIndex];

        for (int i = 0; i < surface.Count; i++)
        {
            if (surface.XValues[i] == param1Value || surface.ZValues[i] == param2Value)
            {
                var color = surface.Colors[i];

                surface.Colors[i] = Color.FromArgb(
                    EnsureByte(color.R - 30),
                    EnsureByte(color.G - 30),
                    EnsureByte(color.B - 30));
            }
        }

        static int EnsureByte(int value) => value < byte.MinValue ? byte.MinValue : value > byte.MaxValue ? byte.MaxValue : value;
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
                    surface.Colors[prevIdx] = prevColor;
                    break;
                }
            case (_, -1):
                {
                    clr = surface.Colors[idx];
                    surface.Colors[idx] = Color.YellowGreen;
                    break;
                }
            default:
                {
                    clr = surface.Colors[idx];
                    surface.Colors[idx] = Color.YellowGreen;
                    surface.Colors[prevIdx] = prevColor;
                    break;
                }
        }
        prevColor = clr;

        surface.Invalidate();
    }

    //zoom
    private void Graph_MouseWheel(object sender, MouseEventArgs e)
    {
        const int stepPct = 5;
        var sign = Math.Sign(e.Delta);
        var zoomPct = 100 + (sign * stepPct);
        graph.Zoom.ZoomPercent(zoomPct);
        //graph.Zoom.Direction = ZoomDirections.Both;

        //var zoomPct = 1 + (sign * stepPct) / 100.0;
        //graph.Aspect.ZoomFloat *= zoomPct;
        //было бы еще не плохо сделать так чтобы зумилось не в центр, а в точку под указателем мыши
        //можно и самому такое нахуячить, во внутрянке не очень сложные расчеты, там просто нужно выставить правильно SetMinMax на осях
    }

    //custom tooltip
    private ToolStripMenuItem mniDisableAutoScale;
    private ToolStripMenuItem mniTrackCursor;
    private ToolStripMenuItem mniShowParamLines;

    private void InitializeCustomToolTip()
    {
        mniDisableAutoScale = new ToolStripMenuItem();
        mniDisableAutoScale.Click += DisableAutoScale_Click;
        mniDisableAutoScale.Checked = false;
        mniDisableAutoScale.CheckState = CheckState.Unchecked;
        mniDisableAutoScale.Size = new Size(268, 22);
        mniDisableAutoScale.Text = "Disable autoscale";

        mniTrackCursor = new ToolStripMenuItem();
        mniTrackCursor.Click += TrackCursor_Click;
        mniTrackCursor.Checked = true;
        mniTrackCursor.CheckState = CheckState.Checked;
        mniTrackCursor.Size = new Size(268, 22);
        mniTrackCursor.Text = "Track cursor";

        mniShowParamLines = new ToolStripMenuItem();
        mniShowParamLines.Click += ShowParamLines_Click; ;
        mniShowParamLines.Checked = false;
        mniShowParamLines.CheckState = CheckState.Unchecked;
        mniShowParamLines.Size = new Size(268, 22);
        mniShowParamLines.Text = "Show parameter lines";

        var separator = new ToolStripSeparator();
        separator.Size = new Size(178, 6);

        var popup = graph.ContextMenuStrip;
        popup.Items.Insert(0, mniDisableAutoScale);
        popup.Items.Insert(1, mniTrackCursor);
        popup.Items.Insert(2, mniShowParamLines);
        popup.Items.Insert(3, separator);
    }

    private void ShowParamLines_Click(object sender, EventArgs e)
    {
        mniShowParamLines.Checked = !mniShowParamLines.Checked;

        ViewUpdated(sender, e);
    }

    private void TrackCursor_Click(object sender, EventArgs e)
    {
        mniTrackCursor.Checked = !mniTrackCursor.Checked;
    }

    private void DisableAutoScale_Click(object sender, EventArgs e)
    {
        mniDisableAutoScale.Checked = !mniDisableAutoScale.Checked;

        var axis = surface.GetVertAxis;
        if (mniDisableAutoScale.Checked)
        {
            var metricIdx = cmbMetric.SelectedIndex;
            var values = results.Results
                .Select(x => x.Results[metricIdx])
                .Where(x => !double.IsNaN(x))
                .Select(x => double.IsInfinity(x) ? 0 : x);

            var min = values.Min();
            var max = values.Max();

            axis.Automatic = false;
            axis.Minimum = min;
            axis.Maximum = max;
        }
        else
        {
            axis.Minimum = axis.MinYValue;
            axis.Maximum = axis.MaxYValue;
            axis.Automatic = true;
        }
    }

    //details pane
    private Panel pnlDetails;
    private Label lblCurr;
    private ToolStripMenuItem mniShowDetails;

    private void InitializeDetailsPane()
    {
        lblCurr = new Label();
        lblCurr.AutoSize = true;
        lblCurr.Location = new Point(10, 7);
        lblCurr.Size = new Size(39, 13);
        lblCurr.Text = "place for current point info";

        pnlDetails = new Panel();
        pnlDetails.Controls.Add(lblCurr);
        pnlDetails.Dock = DockStyle.Left;
        pnlDetails.Location = new Point(0, 0);
        pnlDetails.Size = new Size(200, 61);

        mniShowDetails = new ToolStripMenuItem();
        mniShowDetails.Click += ShowDetails_Click;
        mniShowDetails.Checked = false;
        mniShowDetails.CheckState = CheckState.Unchecked;
        mniShowDetails.Size = new Size(268, 22);
        mniShowDetails.Text = "Show details pane";

        var popup = graph.ContextMenuStrip;
        popup.Items.Insert(0, mniShowDetails);

        pnlDetails.Visible = false;
        Controls.Add(pnlDetails);

        graph.MouseHoveredPointChanged += Graph_MouseHoveredPointChanged1;
    }

    private void ShowDetails_Click(object sender, EventArgs e)
    {
        mniShowDetails.Checked = !mniShowDetails.Checked;
        pnlDetails.Visible = mniShowDetails.Checked;
    }

    //fill details pane
    private void Graph_MouseHoveredPointChanged1(object sender, MouseHoveredPointChangedEventArgs e)
    {
        if (mniTrackCursor.Checked == false || mniShowDetails.Checked == false)
        {
            return;
        }

        var idx = e.CurrentPointIdx;

        if (idx == -1)
        {
            return;
        }

        var paramValues = GetSelectedParameterValues(idx);

        var symbol = cmbSymbol.SelectedItem.ToString();
        var result = resultsMap.FindResult(symbol, paramValues);

        if (result is null)
        {
            lblCurr.Text = "no data";
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Parameters:");
        for (var i = 0; i < ws.Parameters.Count; i++)
        {
            var line = $"{ws.Parameters[i].Name}: {paramValues[i]}";
            builder.AppendLine(line);
        }
        builder.AppendLine();
        builder.AppendLine("Metrics:");
        for (var i = 0; i < results.Names.Count; i++)
        {
            var line = $"{results.Names[i]}: {result.Results[i]}";
            builder.AppendLine(line);
        }

        lblCurr.Text = builder.ToString();
    }

    //options pane
    protected Panel pnlOptions;
    private ToolStripMenuItem mniShowOptions;
    private NumericUpDown nmUpScale;
    private NumericUpDown nmDownScale;

    private void InitializeOptionsPane()
    {
        pnlOptions = new Panel();
        pnlOptions.Dock = DockStyle.Right;
        pnlOptions.Location = new Point(0, 0);
        pnlOptions.Size = new Size(200, 61);

        var lblTitle = new Label();
        lblTitle.Text = "Specify metrics axis scale";
        lblTitle.Size = new Size(150, 20);
        lblTitle.Location = new Point(10, 5);

        var lblUp = new Label();
        lblUp.Text = "Up scale:";
        lblUp.Size = new Size(70, 20);
        lblUp.Location = new Point(10, 30);

        nmUpScale = new NumericUpDown();
        nmUpScale.Size = new Size(100, 10);
        nmUpScale.Location = new Point(100, 30);
        nmUpScale.ValueChanged += UpScale_ValueChanged;

        var lblDown = new Label();
        lblDown.Text = "Down scale:";
        lblDown.Size = new Size(70, 20);
        lblDown.Location = new Point(10, 60);

        nmDownScale = new NumericUpDown();
        nmDownScale.Size = new Size(100, 20);
        nmDownScale.Location = new Point(100, 60);
        nmDownScale.ValueChanged += DownScale_ValueChanged;

        pnlOptions.Controls.Add(lblTitle);
        pnlOptions.Controls.Add(lblUp);
        pnlOptions.Controls.Add(nmUpScale);
        pnlOptions.Controls.Add(lblDown);
        pnlOptions.Controls.Add(nmDownScale);

        mniShowOptions = new ToolStripMenuItem();
        mniShowOptions.Click += ShowOptions_Click; ;
        mniShowOptions.Checked = false;
        mniShowOptions.CheckState = CheckState.Unchecked;
        mniShowOptions.Size = new Size(268, 22);
        mniShowOptions.Text = "Show options pane";

        var popup = graph.ContextMenuStrip;
        popup.Items.Insert(0, mniShowOptions);

        pnlOptions.Visible = false;
        Controls.Add(pnlOptions);

        cmbMetric.SelectedIndexChanged += Metric_SelectedIndexChanged;
    }

    private void Metric_SelectedIndexChanged(object sender, EventArgs e)
    {
        var metricIdx = cmbMetric.SelectedIndex;

        var values = results.Results
            .Select(x => x.Results[metricIdx])
            .Where(x => !double.IsNaN(x))
            .Select(x => double.IsInfinity(x) ? 0 : x);

        var min = (decimal)values.Min();
        var max = (decimal)values.Max();

        nmDownScale.Minimum = min;
        nmDownScale.Maximum = max;
        nmUpScale.Minimum = min;
        nmUpScale.Maximum = max;

        nmUpScale.Value = max;
        nmDownScale.Value = min;
    }

    private void DownScale_ValueChanged(object sender, EventArgs e)
    {
        var axis = surface.GetVertAxis;

        axis.Automatic = false;
        axis.Minimum = (double)nmDownScale.Value;
    }

    private void UpScale_ValueChanged(object sender, EventArgs e)
    {
        var axis = surface.GetVertAxis;

        axis.Automatic = false;
        axis.Maximum = (double)nmUpScale.Value;
    }

    private void ShowOptions_Click(object sender, EventArgs e)
    {
        mniShowOptions.Checked = !mniShowOptions.Checked;
        pnlOptions.Visible = mniShowOptions.Checked;
    }
}
