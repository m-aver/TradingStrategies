using Steema.TeeChart;
using Steema.TeeChart.Styles;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Utility;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Charts;

//переработанная версия встроенного OptResultsGraph1D
//берет значения параметров из переданного OptimizationResultList, а не высчитывает их сама
//(были расхождения с конвертацией из double в decimal)
//добавлены слайдеры для выбора значения параметров

//TODO:
//отдельный контрол для слайдера параметров со всей логикой поиска и тп
//GetParameterValues вынести, также можно вынести поиск через LiteDictionary

public class OptResultsGraph1D : UserControl
{
    private OptimizationResultList results;

    private ComboBox cmbMetric;
    private ComboBox cmbParameters;
    private ComboBox cmbSymbol;
    private TChart chart;
    private Bar metricsBar;

    private List<TrackBar> parameterSliders = [];
    private List<double>[] parameterValues = [];

    private IPrintHost printHost { get; set; }

    public OptResultsGraph1D(Optimizer optimizer)
    {
        printHost = optimizer.PrintHost;

        InitializeComponent();
    }

    private List<double> GetSelectedParameterValues()
    {
        return parameterSliders.Select((x, i) => parameterValues[i][x.Value]).ToList();
    }

    private static List<double>[] GetParameterValues(OptimizationResultList results)
    {
        if (results.Results.Count == 0)
        {
            return [];
        }

        List<HashSet<double>> values = new(results.Results[0].ParameterValues.Count);

        foreach (var result in results.Results)
        {
            for (int i = 0; i < result.ParameterValues.Count; i++)
            {
                if (i >= values.Count)
                {
                    values.Add(new(results.Results.Count));
                }

                values[i].Add(result.ParameterValues[i]);
            }
        }

        List<double>[] output = new List<double>[values.Count];

        for (int i = 0; i < values.Count; i++)
        {
            (output[i] = values[i].ToList()).Sort();
        }

        return output;
    }

    public void RefreshView()
    {
        GenerateGraph();
    }

    private void InitializeParameterSlider(StrategyParameter parameter, List<double> parameterValues)
    {
        var prevSlider = parameterSliders.LastOrDefault(x => x.Parent is not null);
        var start = prevSlider is null ? new Point(10, 50) : prevSlider.Location + new Size(0, prevSlider.Height);

        var labelName = new Label();
        labelName.Text = parameter.Name;
        labelName.Size = new Size(100, 20);
        labelName.Location = new Point(start.X, start.Y);

        var defaultIndex = parameterValues.IndexOf(parameter.Value);

        var slider = new TrackBar();
        slider.Size = new Size(140, 20);
        slider.Minimum = 0;
        slider.Maximum = parameterValues.Count - 1;
        slider.SmallChange = 1;
        slider.TickFrequency = 5;
        slider.Value = defaultIndex == -1 ? 0 : defaultIndex;
        slider.Tag = parameter;
        slider.Location = new Point(start.X, labelName.Location.Y + labelName.Size.Height);

        var labelValue = new Label();
        labelValue.Text = parameter.Value.ToString();
        labelValue.Location = new Point(slider.Location.X + slider.Width + 10, slider.Location.Y);
        labelValue.Size = new Size(50, 20);

        slider.Scroll += (sender, e) =>
        {
            labelValue.Text = this.parameterValues[parameterSliders.IndexOf(slider)][slider.Value].ToString();
            GenerateGraph();
        };

        if (parameter.IsEnabled)
        {
            pnlParameters.Controls.AddRange([labelName, slider, labelValue]);
        }

        parameterSliders.Add(slider);
    }

    private void Clear()
    {
        Array.Clear(parameterValues, 0, parameterValues.Length);
        parameterSliders.ForEach(pnlParameters.Controls.Remove);
        parameterSliders.Clear();
        cmbParameters.Items.Clear();
        cmbMetric.Items.Clear();
        cmbSymbol.Items.Clear();
    }

    internal void UpdateResults(OptimizationResultList results, WealthScript ws)
    {
        Clear();

        this.results = results;

        parameterValues = GetParameterValues(results);

        foreach (var (parameter, i) in ws.Parameters.WithIndex())
        {
            cmbParameters.Items.Add(parameter);
            InitializeParameterSlider(parameter, parameterValues[i]);
        }

        foreach (var name in results.Names)
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

        if (cmbParameters.Items.Count > 0)
        {
            cmbParameters.SelectedIndex = 0;
        }
        if (cmbMetric.Items.Count > 0)
        {
            cmbMetric.SelectedIndex = 0;
        }
        if (cmbSymbol.Items.Count > 0)
        {
            cmbSymbol.SelectedIndex = 0;
        }

        GenerateGraph();
    }

    private void GenerateGraph()
    {
        if (results == null || cmbParameters.SelectedIndex == -1 || cmbMetric.SelectedIndex == -1 || cmbSymbol.SelectedIndex == -1)
        {
            return;
        }

        if (chart.Zoom.Zoomed)
        {
            chart.Zoom.Undo();
        }

        var parameter = (StrategyParameter)cmbParameters.SelectedItem;
        var metricName = (string)cmbMetric.SelectedItem;

        metricsBar.Clear();
        chart.Axes.Bottom.Title.Text = parameter.Name;
        chart.Axes.Left.Title.Text = metricName;

        var index = cmbParameters.SelectedIndex;
        var paramValues = GetSelectedParameterValues();

        foreach (var paramValue in parameterValues[index])
        {
            paramValues[index] = paramValue;
            var metricValue = results.FindMetric(cmbSymbol.Text, metricName, paramValues);

            if (!double.IsNaN(metricValue))
            {
                metricValue = double.IsInfinity(metricValue) ? 0.0 : metricValue;
                metricsBar.Add(paramValue, metricValue);
            }
        }
    }

    private void CopyToClipboard_Click(object sender, EventArgs e)
    {
        try
        {
            Clipboard.SetImage(chart.Bitmap);
        }
        catch (ExternalException)
        {
            MessageBox.Show("Copy to clipboard was blocked by another process.  Please try again", "ClipBoard Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
        }
    }

    private void Print_Click(object sender, EventArgs e)
    {
        printHost.Print("Optimization 1 Parameter Graph", chart.Bitmap, bUseDefaultDisclosure: true);
    }

    private void InitializeComponent()
    {
        var pnlTop = new System.Windows.Forms.Panel();
        var lblSymbol = new Label();
        var lblMetric = new Label();
        var lblParameter = new Label();
        var popupGraph1D = new ContextMenuStrip();
        var mniCopyToClipboard = new ToolStripMenuItem();
        var mniPrint = new ToolStripMenuItem();
        cmbSymbol = new ComboBox();
        cmbMetric = new ComboBox();
        cmbParameters = new ComboBox();
        chart = new TChart();
        metricsBar = new Bar();

        pnlTop.SuspendLayout();
        popupGraph1D.SuspendLayout();
        SuspendLayout();

        pnlTop.Controls.AddRange([cmbSymbol, lblSymbol, lblMetric, cmbMetric, cmbParameters, lblParameter]);
        pnlTop.Dock = DockStyle.Top;
        pnlTop.Location = new Point(0, 0);
        pnlTop.Size = new Size(589, 31);

        cmbSymbol.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSymbol.FormattingEnabled = true;
        cmbSymbol.Location = new Point(53, 4);
        cmbSymbol.Size = new Size(75, 21);
        cmbSymbol.SelectedIndexChanged += ViewUpdated;

        lblSymbol.AutoSize = true;
        lblSymbol.Location = new Point(3, 7);
        lblSymbol.Size = new Size(44, 13);
        lblSymbol.Text = "Symbol:";

        lblMetric.AutoSize = true;
        lblMetric.Location = new Point(325, 7);
        lblMetric.Size = new Size(39, 13);
        lblMetric.Text = "Metric:";

        cmbMetric.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbMetric.FormattingEnabled = true;
        cmbMetric.Location = new Point(370, 4);
        cmbMetric.Size = new Size(102, 21);
        cmbMetric.SelectedIndexChanged += ViewUpdated;

        cmbParameters.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbParameters.FormattingEnabled = true;
        cmbParameters.Location = new Point(198, 4);
        cmbParameters.Size = new Size(121, 21);
        cmbParameters.SelectedIndexChanged += ViewUpdated;

        lblParameter.AutoSize = true;
        lblParameter.Location = new Point(134, 7);
        lblParameter.Size = new Size(58, 13);
        lblParameter.Text = "Parameter:";

        chart.Aspect.ColorPaletteIndex = 0;
        chart.Aspect.ZOffset = 0.0;
        chart.Axes.Bottom.MaximumOffset = 50;
        chart.Axes.Bottom.MinimumOffset = 50;
        chart.Axes.Bottom.Title.Caption = "Parameter Value";
        chart.Axes.Bottom.Title.Lines = ["Parameter Value"];
        chart.Axes.Left.MaximumOffset = 37;
        chart.Axes.Left.Title.Caption = "Net Profit";
        chart.Axes.Left.Title.Lines = ["Net Profit"];
        chart.ContextMenuStrip = popupGraph1D;
        chart.Dock = DockStyle.Fill;
        chart.Header.Lines = [""];
        chart.Legend.Visible = false;
        chart.Location = new Point(0, 31);
        chart.Panning.Allow = ScrollModes.None;
        chart.Series.Add(metricsBar);
        chart.Size = new Size(589, 359);

        popupGraph1D.Items.AddRange([mniCopyToClipboard, mniPrint]);
        popupGraph1D.Size = new Size(172, 48);

        mniCopyToClipboard.Size = new Size(171, 22);
        mniCopyToClipboard.Text = "Copy to Clipboard";
        mniCopyToClipboard.Click += CopyToClipboard_Click;

        mniPrint.Size = new Size(171, 22);
        mniPrint.Text = "Print";
        mniPrint.Click += Print_Click;

        metricsBar.Brush.Color = Color.Red;
        metricsBar.Color = Color.Red;
        metricsBar.ColorEach = false;
        metricsBar.Marks.Callout.ArrowHead = ArrowHeadStyles.None;
        metricsBar.Marks.Callout.ArrowHeadSize = 8;
        metricsBar.Marks.Callout.Brush.Color = Color.Black;
        metricsBar.Marks.Callout.Distance = 0;
        metricsBar.Marks.Callout.Draw3D = false;
        metricsBar.Marks.Callout.Length = 20;
        metricsBar.Marks.Callout.Style = PointerStyles.Rectangle;
        metricsBar.Marks.Callout.Visible = false;
        metricsBar.Pen.Color = Color.FromArgb(153, 0, 0);
        metricsBar.XValues.DataMember = "X";
        metricsBar.XValues.Order = ValueListOrder.Ascending;
        metricsBar.YValues.DataMember = "Bar";

        AutoScaleDimensions = new SizeF(6f, 13f);
        AutoScaleMode = AutoScaleMode.Font;
        Controls.Add(chart);
        Controls.Add(pnlTop);
        Size = new Size(589, 390);

        pnlTop.ResumeLayout(false);
        pnlTop.PerformLayout();
        popupGraph1D.ResumeLayout(false);
        ResumeLayout(false);

        InitializeParametersPane();
    }

    private void ViewUpdated(object sender, EventArgs e)
    {
        GenerateGraph();
    }

    private System.Windows.Forms.Panel pnlParameters;
    private Label lblParametersPaneTitle;
    private ToolStripMenuItem mniShowParameters;

    private void InitializeParametersPane()
    {
        lblParametersPaneTitle = new Label();
        lblParametersPaneTitle.AutoSize = true;
        lblParametersPaneTitle.Location = new Point(10, 7);
        lblParametersPaneTitle.Name = "lblCurr";
        lblParametersPaneTitle.Size = new Size(39, 13);
        lblParametersPaneTitle.Text = "Select parameter values by sliders";

        pnlParameters = new System.Windows.Forms.Panel();
        pnlParameters.Controls.Add(lblParametersPaneTitle);
        pnlParameters.Dock = DockStyle.Left;
        pnlParameters.Location = new Point(0, 0);
        pnlParameters.Name = "pnlDetails";
        pnlParameters.Size = new Size(200, 61);

        mniShowParameters = new ToolStripMenuItem();
        mniShowParameters.Click += ShowParameters_Click;
        mniShowParameters.Checked = false;
        mniShowParameters.CheckState = CheckState.Unchecked;
        mniShowParameters.Size = new Size(268, 22);
        mniShowParameters.Text = "Show parameters pane";

        var popup = chart.ContextMenuStrip;
        popup.Items.Insert(0, mniShowParameters);

        pnlParameters.Visible = false;
        Controls.Add(pnlParameters);
    }

    private void ShowParameters_Click(object sender, EventArgs e)
    {
        mniShowParameters.Checked = !mniShowParameters.Checked;
        pnlParameters.Visible = mniShowParameters.Checked;
    }
}
