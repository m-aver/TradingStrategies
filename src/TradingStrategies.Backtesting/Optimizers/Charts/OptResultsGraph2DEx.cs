using Steema.TeeChart.Styles;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Optimizers.Charts.Controls;
using TradingStrategies.Backtesting.Optimizers.Utility;
using TradingStrategies.Backtesting.Utility;
using TradingStrategies.Utilities.InternalsProxy;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Charts;

//расширение для OptResultsGraph2D
//добавлена фича с оконным пересчетом результатов (обрезка результатов по указанным датам)
//  идея в том чтобы посмотреть как изменятся результаты стратегии, если бы она запускалась на каком-то из поддиапазонов дат исходного датасета
//  но смещение выполняется наивно (ради производительности), простой обрезкой датасерий (эквити) и списка позиций
//  из-за чего метрики основанные на данных позиций, такие как NetProfit, могут быть искажены
//  но метрики основанные только на процентном соотношении эквити должны быть адекватны (AvgMr, Drawdown, Sharpe, LeFactor, ...)
//добавлена фича с расчетом корреляций (по сериям месячных доходностей)
//  можно отобразить корреляции при наведии ук мыши на точку графика, остальные точки окрасятся в зависимости от коэффициента корреляци к выбранной точке
//  или ввести целевую серию доходностей самому в текстовое поле (csv)

internal class OptResultsGraph2DEx : OptResultsGraph2D
{
    private OptimizationResultListEx results;
    private StrategyScorecard scorecard;

    private static readonly PeriodInfo CorrelationReturnsPeriod = PeriodInfo.Monthly;

    public OptResultsGraph2DEx(Optimizer optimizer) : base(optimizer)
    {
        InitializeComponents();
    }

    internal void UpdateResults(OptimizationResultListEx results, WealthScript ws, StrategyScorecard scorecard)
    {
        base.UpdateResults(results, ws);

        this.results = results;
        this.scorecard = scorecard;

        var (from, to) = results.GetDatesRange();

        ignoreRangeChanges = true;

        nmToScale.Minimum = from;
        nmToScale.Maximum = to;
        nmFromScale.Minimum = from;
        nmFromScale.Maximum = to;
        nmToScale.Value = to;
        nmFromScale.Value = from;

        ignoreRangeChanges = false;

        RestoreResults();
    }

    private void RecalculateReturns()
    {
        //пересчитываем только если включен режим расчета корреляций, т.к. пока что MRE переиспользуется только в этом кейсе
        if (cbCorrelationMode.Checked == false)
        {
            return;
        }

        var symbol = cmbSymbol.SelectedItem.ToString();
        var paramValues = GetSelectedParameterValues();

        var param1Idx = cmbParameter1.SelectedIndex;
        var param2Idx = cmbParameter2.SelectedIndex;

        //очищаем, чтобы не занимать память
        results.ResultsEx.ForEach(r => r.MonthReturnsEquity = null);

        //пересчитываем только на точках, которые отображенны на текущий момент
        Parallel.For(0, surface.Count, i =>
        {
            var parameterValues = paramValues
                .ReplaceWith(surface.XValues[i], param1Idx)
                .ReplaceWith(surface.ZValues[i], param2Idx);

            var result = (OptimizationResultEx?)resultsMap.FindResult(symbol, parameterValues);

            if (result is null)
            {
                return;
            }

            result.MonthReturnsEquity = periodicalCalculator
                .CalculatePercentDiff(result.PerformanceShifted.Results.EquityCurve, CorrelationReturnsPeriod)
                .ToArray();
        });
    }

    //обновляются листы значений метрик (OptimizationResult.Results), на основе которых потом в базовом классе строятся поверхности и пр.
    private void ChangeRange()
    {
        if (ignoreRangeChanges || !cbRangeMode.Checked)
        {
            return;
        }

        var (from, to) = GetCurrentRange();

        var symbol = cmbSymbol.SelectedItem.ToString();
        var paramValues = GetSelectedParameterValues();

        var param1Idx = cmbParameter1.SelectedIndex;
        var param2Idx = cmbParameter2.SelectedIndex;

        //очищаем, чтобы не занимать память
        RestoreResults();

        //пересчитываем только на точках, которые отображенны на текущий момент
        Parallel.For(0, surface.Count, i =>
        {
            var parameterValues = paramValues
                .ReplaceWith(surface.XValues[i], param1Idx)
                .ReplaceWith(surface.ZValues[i], param2Idx);

            var optResult = (OptimizationResultEx?)resultsMap.FindResult(symbol, parameterValues);

            if (optResult is null)
            {
                return;
            }

            ShiftOptimizationResults(optResult, from, to);
        });
    }

    private DateTimeRange GetCurrentRange()
    {
        return new(nmFromScale.Value, nmToScale.Value);
    }

    private void ShiftOptimizationResults(OptimizationResultEx optResult, DateTime from, DateTime to)
    {
        var performance = new SystemPerformance(optResult.Performance.Strategy);
        CopyPerformance(performance, optResult.Performance);

        ShiftResults(performance.Results, from, to);
        ShiftResults(performance.ResultsLong, from, to);
        ShiftResults(performance.ResultsShort, from, to);

        optResult.PerformanceShifted = performance;
        optResult.OriginalResults = optResult.Results.ToList();

        var row = new ListViewItem(); //первая ячейка по умолчанию пустая
        scorecard.PopulateScorecard(row, performance);
        for (int j = 1; j < row.SubItems.Count; j++)
        {
            var cell = row.SubItems[j].Text;
            optResult.Results[j - 1] = double.TryParse(cell, out var value) ? value : 0.0;
        }

        //значительную часть жрет PopulateScorecard: ~60% для CustomScorecard
        //CopyPerformance ~30% (много рефлексии для копирования приватных полей ?) и ~10% на все остальное
        //но это при условии отсутствия ResultsLong, ResultsShort, OpenPositionsCount, с ними копирование и смещение должно быть ощутимо затратнее
        //затраты на скорекард можно снизить уменьшением диапазона дат и количества метрик,
    }

    //такие штуки как TotalCommission, DividendsPaid, CashReturn и т.д. не аффектятся
    //либо нужно пересчитывать BuildEquityCurve, что сильно затратнее и требует доп анализа на переиспользование TradingSystemExecutor 
    public static void ShiftResults(SystemResults result, DateTime from, DateTime to)
    {
        if (result.IsEmpty())
        {
            return;
        }
        if (from > to)
        {
            throw new ArgumentException($"{nameof(from)} must be less than {nameof(to)}");
        }

        //TODO: пока довольно не точно
        //есть идейка обрезать позиции, которые пересекают границы периода, установкой своего EntryBar и ExitBar
        //и кажется проще делать это через отдельный лист, в целом как будто такой лист не должен сильно грузить память
        var positions = result.RawPositions;
        var startIdx = 0;
        var finalIdx = 0;
        foreach (var pos in positions)
        {
            if (pos.EntryDate < from)
            {
                startIdx++;
            }
            else if (pos.EntryDate > to)
            {
                break;
            }
            finalIdx++;
        }
        if (startIdx > 0)
        {
            positions.RemoveRange(0, startIdx);
            finalIdx = finalIdx - startIdx;
        }
        if (finalIdx < positions.Count)
        {
            positions.RemoveRange(finalIdx, positions.Count - finalIdx);
        }

        ShiftSeries(result.EquityCurve, from, to);
        ShiftSeries(result.CashCurve, from, to);
        ShiftSeries(result.OpenPositionCount, from, to);

        //смещение EquityCurve и CashCurve можно объединить, они должны всегда совпадать по датам, но пока не сильно нужно
    }

    //подоптимизированный аналог для
    //series = series.ToPoints().Where(x => x.Date >= from && x.Date <= to).ToSeries();
    private static void ShiftSeries(DataSeries series, DateTime from, DateTime to)
    {
        if (series is null || series.Count is 0)
        {
            return;
        }
        if (from > to)
        {
            throw new ArgumentException($"{nameof(from)} must be less than {nameof(to)}");
        }

        var equityDates = series.GetRawDates();
        var equityValues = series.GetRawValues();

        var startIdx = equityDates.BinarySearch(from);
        var finalIdx = equityDates.BinarySearch(to);

        startIdx = Math.Abs(startIdx);
        finalIdx = Math.Abs(finalIdx);

        if (startIdx <= finalIdx && startIdx >= 0 && finalIdx < equityDates.Count)
        {
            finalIdx = finalIdx - startIdx;

            equityDates.RemoveRange(0, startIdx);
            equityDates.RemoveRange(finalIdx, equityDates.Count - finalIdx);
            equityValues.RemoveRange(0, startIdx);
            equityValues.RemoveRange(finalIdx, equityValues.Count - finalIdx);
        }
    }

    private void ChangePeriod()
    {
        if (ignoreRangeChanges)
        {
            return;
        }

        var periodUnit = (Period)cmbPeriodUnit.SelectedIndex;
        var periodSize = (int)nmPeriodNum.Value;
        var periodInfo = new PeriodInfo(periodUnit, periodSize);

        var (from, to) = GetCurrentRange();

        var separator = PeriodSeparatorFactory.Create(PeriodCalcType.Simple);
        var max = new DateTime(DateTime.MaxValue.Ticks, from.Kind);
        var periodsStart = separator.GetPeriods(new(from, max), periodInfo);
        var periodsFinal = separator.GetPeriods(new(to, max), periodInfo);

        var periodOffset = (int)nmPeriodOffset.Value * currScrollOffset;
        var newStart = periodsStart.Skip(Math.Abs(periodOffset)).First().DateTime;
        var newFinal = periodsFinal.Skip(Math.Abs(periodOffset)).First().DateTime;

        //пока лень было заморачиваться
        if (periodOffset < 0)
        {
            newStart = from - (newStart - from);
            newFinal = to - (newFinal - to);
        }

        newStart = newStart < nmFromScale.Minimum ? nmFromScale.Minimum : newStart > nmFromScale.Maximum ? nmFromScale.Maximum : newStart;
        newFinal = newFinal < nmFromScale.Minimum ? nmFromScale.Minimum : newFinal > nmFromScale.Maximum ? nmFromScale.Maximum : newFinal;

        //вызывает два перестроения сразу
        ignoreRangeChanges = true;

        nmFromScale.Value = newStart;
        nmToScale.Value = newFinal;

        ignoreRangeChanges = false;

        GenerateGraph();
    }

    private void RestoreResults()
    {
        if (results is null)
        {
            return;
        }

        foreach (var result in results.ResultsEx)
        {
            result.PerformanceShifted = result.Performance; //все считаем от PerformanceShifted, инициализируем обычным Performance

            if (result.OriginalResults != null)
            {
                result.Results = result.OriginalResults;
            }
        }
    }

    private static void CopyPerformance(SystemPerformance to, SystemPerformance from)
    {
        OptimizationResultEx.CopyPerformance(to, from);
    }

    private void InitializeComponents()
    {
        InitializeScale();
        InitializePeriod();
        InitializeCorrelation();
    }

    //date range selection
    private CheckBox cbRangeMode;
    private DateUpDown nmToScale;
    private DateUpDown nmFromScale;
    private bool ignoreRangeChanges = false;

    private void InitializeScale()
    {
        var lblMode = new Label();
        lblMode.Text = "Activate range mode";
        lblMode.Size = new Size(150, 20);
        lblMode.Location = new Point(40, 100);

        cbRangeMode = new CheckBox();
        cbRangeMode.Checked = false;
        cbRangeMode.Size = new Size(20, 20);
        cbRangeMode.Location = new Point(10, 100);
        cbRangeMode.Click += CbRangeMode_Click;

        var lblTitle = new Label();
        lblTitle.Text = "Specify date window (yyyyMMdd)";
        lblTitle.Size = new Size(200, 20);
        lblTitle.Location = new Point(10, 130);

        var lblTo = new Label();
        lblTo.Text = "To scale:";
        lblTo.Size = new Size(30, 20);
        lblTo.Location = new Point(10, 160);

        nmToScale = new DateUpDown();
        nmToScale.Size = new Size(100, 20);
        nmToScale.Location = new Point(50, 160);
        nmToScale.ValueChanged += NmToScale_ValueChanged;

        var lblFrom = new Label();
        lblFrom.Text = "From scale:";
        lblFrom.Size = new Size(30, 20);
        lblFrom.Location = new Point(10, 190);

        nmFromScale = new DateUpDown();
        nmFromScale.Size = new Size(100, 20);
        nmFromScale.Location = new Point(50, 190);
        nmFromScale.ValueChanged += NmFromScale_ValueChanged;

        pnlOptions.Controls.Add(lblMode);
        pnlOptions.Controls.Add(cbRangeMode);
        pnlOptions.Controls.Add(lblTitle);
        pnlOptions.Controls.Add(lblTo);
        pnlOptions.Controls.Add(nmToScale);
        pnlOptions.Controls.Add(lblFrom);
        pnlOptions.Controls.Add(nmFromScale);
    }

    private void CbRangeMode_Click(object sender, EventArgs e)
    {
        if (!cbRangeMode.Checked)
        {
            RestoreResults();
        }

        GenerateGraph();
    }

    private void NmFromScale_ValueChanged(object sender, EventArgs e) => HandleRangeChanged();
    private void NmToScale_ValueChanged(object sender, EventArgs e) => HandleRangeChanged();

    private void HandleRangeChanged()
    {
        if (ignoreRangeChanges || !cbRangeMode.Checked)
        {
            return;
        }

        GenerateGraph();
    }

    //period selection
    private ComboBox cmbPeriodUnit;
    private NumericUpDown nmPeriodNum;
    private NumericUpDown nmPeriodOffset;
    private TrackBar tbPeriodOffset;

    private void InitializePeriod()
    {
        var lblPeriod = new Label();
        lblPeriod.Text = "Period";
        lblPeriod.Size = new Size(100, 20);
        lblPeriod.Location = new Point(10, 220);

        var lblUnits = new Label();
        lblUnits.Text = "units:";
        lblUnits.Size = new Size(30, 20);
        lblUnits.Location = new Point(10, 250);

        cmbPeriodUnit = new ComboBox();
        cmbPeriodUnit.Size = new Size(100, 30);
        cmbPeriodUnit.Location = new Point(50, 250);
        cmbPeriodUnit.DataSource = Enum.GetValues(typeof(Period));
        cmbPeriodUnit.SelectedIndexChanged += CmbPeriodUnit_SelectedIndexChanged;

        var lblSize = new Label();
        lblSize.Text = "size:";
        lblSize.Size = new Size(30, 20);
        lblSize.Location = new Point(10, 280);

        nmPeriodNum = new NumericUpDown();
        nmPeriodNum.Size = new Size(100, 20);
        nmPeriodNum.Location = new Point(50, 280);
        nmPeriodNum.Minimum = 0;
        nmPeriodNum.Maximum = 1000;
        nmPeriodNum.Value = 1;
        nmPeriodNum.ValueChanged += NmPeriodNum_ValueChanged;

        var lblOffset = new Label();
        lblOffset.Text = "offset:";
        lblOffset.Size = new Size(30, 20);
        lblOffset.Location = new Point(10, 310);

        nmPeriodOffset = new NumericUpDown();
        nmPeriodOffset.Size = new Size(100, 20);
        nmPeriodOffset.Location = new Point(50, 310);
        nmPeriodOffset.Minimum = 0;
        nmPeriodOffset.Maximum = 1000;
        nmPeriodOffset.Value = 1;
        nmPeriodOffset.ValueChanged += NmPeriodOffset_ValueChanged;

        tbPeriodOffset = new TrackBar();
        tbPeriodOffset.Size = new Size(100, 20);
        tbPeriodOffset.Location = new Point(50, 340);
        tbPeriodOffset.Minimum = -50;
        tbPeriodOffset.Maximum = 50;
        tbPeriodOffset.TickFrequency = 10;
        tbPeriodOffset.Value = 0;
        tbPeriodOffset.Scroll += TbPeriodOffset_Scroll;

        pnlOptions.Controls.Add(lblPeriod);
        pnlOptions.Controls.Add(lblUnits);
        pnlOptions.Controls.Add(cmbPeriodUnit);
        pnlOptions.Controls.Add(lblSize);
        pnlOptions.Controls.Add(nmPeriodNum);
        pnlOptions.Controls.Add(lblOffset);
        pnlOptions.Controls.Add(nmPeriodOffset);
        pnlOptions.Controls.Add(tbPeriodOffset);
    }

    private void CmbPeriodUnit_SelectedIndexChanged(object sender, EventArgs e) => ChangePeriod();
    private void NmPeriodNum_ValueChanged(object sender, EventArgs e) => ChangePeriod();
    private void NmPeriodOffset_ValueChanged(object sender, EventArgs e) => ChangePeriod();

    private int prevScrollValue = 0;
    private int currScrollOffset = 0;
    private void TbPeriodOffset_Scroll(object sender, EventArgs e)
    {
        //скролл вперед - увеличиваем окно на выбранный период, назад - уменьшаем, середина скролла устанавливает исходное окно
        currScrollOffset = tbPeriodOffset.Value - prevScrollValue;

        ChangePeriod();

        prevScrollValue = tbPeriodOffset.Value;
        currScrollOffset = 0;
    }


    //корреляция по доходностям
    private int correlationPivot = -1;
    private DataSeriesPoint[] correlationPivotSeries;
    private IPeriodicalSeriesCalculator periodicalCalculator;

    private CheckBox cbCorrelationMode;
    private TextBox tbCustomCorrelationPivotData;
    private Label lblInvalidCustomData;

    private void InitializeCorrelation()
    {
        periodicalCalculator = PeriodicalSeriesCalculatorFactory.CreateAlignedSingleton();

        var lblMode = new Label();
        lblMode.Text = "Activate correlation mode";
        lblMode.Size = new Size(150, 20);
        lblMode.Location = new Point(40, 400);

        cbCorrelationMode = new CheckBox();
        cbCorrelationMode.Checked = false;
        cbCorrelationMode.Size = new Size(20, 20);
        cbCorrelationMode.Location = new Point(10, 400);
        cbCorrelationMode.Click += CbCorrelationMode_Click;

        var lblCustomData = new Label();
        lblCustomData.Text = "Set custom correlation data (month return percents) : [mr1; mr2; mr3]";
        lblCustomData.Size = new Size(150, 20);
        lblCustomData.Location = new Point(10, 430);

        tbCustomCorrelationPivotData = new TextBox();
        tbCustomCorrelationPivotData.Size = new Size(100, 20);
        tbCustomCorrelationPivotData.Location = new Point(10, 460);
        tbCustomCorrelationPivotData.TextChanged += TbCustomCorrelationPivotData_TextChanged;

        lblInvalidCustomData = new Label();
        lblInvalidCustomData.Text = "Invalid data provided";
        lblInvalidCustomData.ForeColor = Color.Red;
        lblInvalidCustomData.Size = new Size(150, 20);
        lblInvalidCustomData.Location = new Point(10, 490);
        lblInvalidCustomData.Visible = false;

        pnlOptions.Controls.Add(lblMode);
        pnlOptions.Controls.Add(cbCorrelationMode);
        pnlOptions.Controls.Add(lblCustomData);
        pnlOptions.Controls.Add(tbCustomCorrelationPivotData);
        pnlOptions.Controls.Add(lblInvalidCustomData);

        graph.MouseHoveredPointChanged += Graph_MouseHoveredPointChanged;
        //graph.AfterDraw += Graph_AfterDraw;
    }

    //нужно чтобы при обновлениях графика он перерисовывал корреляции, но эта штука пока не заработала
    private void Graph_AfterDraw(object sender, Steema.TeeChart.Drawing.Graphics3D g)
    {
        if (cbCorrelationMode.Checked == false)
        {
            return;
        }

        ColorizeCorrelations();
    }

    //пока так буду перерисовывать корреляции
    protected override void GenerateGraph()
    {
        ChangeRange();

        base.GenerateGraph();

        if (cbCorrelationMode.Checked == false)
        {
            return;
        }

        RecalculateReturns();

        ColorizeCorrelations();

        surface.Invalidate();
    }

    //parse and validate provided text, build correlationPivotSeries
    private void TbCustomCorrelationPivotData_TextChanged(object sender, EventArgs e)
    {
        if (cbCorrelationMode.Checked == false)
        {
            return;
        }

        var invalidReasonBuilder = new StringBuilder();
        var text = tbCustomCorrelationPivotData.Text;

        if (text == string.Empty)
        {
            correlationPivotSeries = null;
            lblInvalidCustomData.Text = string.Empty;
            lblInvalidCustomData.Visible = false;
            return;
        }

        var values = text.Trim().Split(';').Select(x =>
        {
            if (double.TryParse(x, out var v)) return v;
            else { invalidReasonBuilder.AppendLine($"Cannot parse value: {x}"); return 0; }
        }).ToArray();

        if (invalidReasonBuilder.Length != 0)
        {
            lblInvalidCustomData.Visible = true;
            lblInvalidCustomData.Text = invalidReasonBuilder.ToString();
            return;
        }

        var datasetEquity = results.ResultsEx.First().PerformanceShifted.Results.EquityCurve;
        var mrEquity = periodicalCalculator.CalculatePercentDiff(datasetEquity, CorrelationReturnsPeriod).ToArray();
        if (values.Length != mrEquity.Length)
        {
            lblInvalidCustomData.Visible = true;
            lblInvalidCustomData.Text = $"inconsistent amount of returns, should be {mrEquity.Length}, was: {values.Length}";
            return;
        }

        correlationPivotSeries = mrEquity.Zip(values, (e, v) => (e, v)).Select(x => x.e.WithValue(x.v)).ToArray();

        lblInvalidCustomData.Text = string.Empty;
        lblInvalidCustomData.Visible = false;

        ColorizeCorrelations();
    }

    private void CbCorrelationMode_Click(object sender, EventArgs e)
    {
        if (cbCorrelationMode.Checked)
        {
            RecalculateReturns();
        }
        else
        {
            GenerateGraph();
        }
    }

    private void Graph_MouseHoveredPointChanged(object sender, MouseHoveredPointChangedEventArgs e)
    {
        if (cbCorrelationMode.Checked == false)
        {
            return;
        }

        try
        {
            var idx = e.CurrentPointIdx;
            if (idx == -1)
            {
                return;
            }

            //build correlationPivotSeries
            var paramValues = GetSelectedParameterValues(idx);
            var symbol = cmbSymbol.SelectedItem.ToString();
            var pivotResult = (OptimizationResultEx?)resultsMap.FindResult(symbol, paramValues);

            if (pivotResult == null)
            {
                return;
            }

            var equity = pivotResult.PerformanceShifted.Results.EquityCurve;
            correlationPivotSeries = periodicalCalculator.CalculatePercentDiff(equity, CorrelationReturnsPeriod).ToArray();
            correlationPivot = idx;

            ColorizeCorrelations();
        }
        catch
        {
            //ingore out of positions
        }
    }

    private void ColorizeCorrelations()
    {
        if (correlationPivotSeries is null)
        {
            return;
        }

        var paramValues = GetSelectedParameterValues();

        var param1Idx = cmbParameter1.SelectedIndex;
        var param2Idx = cmbParameter2.SelectedIndex;
        var symbol = cmbSymbol.SelectedItem.ToString();

        //var pivotResult = (OptimizationResultEx)FindResult(symbol, paramValues);

        //посчитать корреляции и раскрасить поверхность
        const double max = 1.0;
        const double min = 0.0;

        var colors = new Color[surface.Count];

        Parallel.For(0, surface.Count, i =>
        {
            try
            {
                if (i == correlationPivot) //не трогаем выбранную точку
                {
                    colors[i] = surface.Colors[i];
                    return;
                }

                var parameterValues = paramValues
                    .ReplaceWith(surface.XValues[i], param1Idx)
                    .ReplaceWith(surface.ZValues[i], param2Idx);

                var result = (OptimizationResultEx?)resultsMap.FindResult(symbol, parameterValues);

                if (result == null)
                {
                    colors[i] = Color.LightGray;
                    return;
                }

                var corr = IndicatorsCalculator.Correlation(correlationPivotSeries, result.MonthReturnsEquity, force: true);

                if (double.IsNaN(corr))
                {
                    colors[i] = Color.LightGray;
                    return;
                }

                //интенсивность цвета в соответствии с амплитудой метрики
                var val = corr;
                var pct = 100 * (val - min) / (max - min);
                pct = Math.Abs(pct);
                var diff = (int)(255 * pct / 100);
                var clr = val > 0
                    ? Color.FromArgb(255, 255 - diff, 255 - diff)  //красный в положительном диапазоне
                    : Color.FromArgb(255 - diff, 255 - diff, 255); //синий в отрицательном диапазоне
                colors[i] = clr;
            }
            catch
            {
                colors[i] = Color.DarkGray;
            }
        });

        surface.Colors = new ColorList(colors);

        surface.Invalidate();
    }
}
