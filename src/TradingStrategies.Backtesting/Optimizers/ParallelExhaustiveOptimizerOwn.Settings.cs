using Fidelity.Components;
using System.ComponentModel;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Optimizers.Own;

namespace TradingStrategies.Backtesting.Optimizers;

//ui settings provider
public partial class ParallelExhaustiveOptimizerOwn : ICustomSettings
{
    private int ThreadsNumber { get; set; }
    private bool CalcLongResults { get; set; }
    private bool CalcShortResults { get; set; }
    private bool CalcMfeMae { get; set; }
    public bool CalcOpenPositionsCount { get; set; }
    private EquityCalcMode EquityCalcMode { get; set; }

    public UserControl GetSettingsUI()
    {
        var settingsControl = new ParallelExhaustiveOptimizerOwnSettings();
        settingsControl.ThreadsNumber = ThreadsNumber;
        settingsControl.CalcLongResults = CalcLongResults;
        settingsControl.CalcShortResults = CalcShortResults;
        settingsControl.CalcMfeMae = CalcMfeMae;
        settingsControl.CalcOpenPositionsCount = CalcOpenPositionsCount;
        settingsControl.EquityCalcMode = EquityCalcMode;
        return settingsControl;
    }

    public void ChangeSettings(UserControl ui)
    {
        if (ui is not ParallelExhaustiveOptimizerOwnSettings)
        {
            throw new ArgumentException($"Control is not settings control, was: {ui.GetType().Name}");
        }

        var settingsControl = (ParallelExhaustiveOptimizerOwnSettings)ui;

        ThreadsNumber = settingsControl.ThreadsNumber;
        CalcLongResults = settingsControl.CalcLongResults;
        CalcShortResults = settingsControl.CalcShortResults;
        CalcMfeMae = settingsControl.CalcMfeMae;
        CalcOpenPositionsCount = settingsControl.CalcOpenPositionsCount;
        EquityCalcMode = settingsControl.EquityCalcMode;
    }

    public void ReadSettings(ISettingsHost host)
    {
        ThreadsNumber = host.Get(nameof(ParallelExhaustiveOptimizerOwnSettings.ThreadsNumber), defaultValue: Environment.ProcessorCount);
        CalcLongResults = host.Get(nameof(ParallelExhaustiveOptimizerOwnSettings.CalcLongResults), defaultValue: false);
        CalcShortResults = host.Get(nameof(ParallelExhaustiveOptimizerOwnSettings.CalcShortResults), defaultValue: false);
        CalcMfeMae = host.Get(nameof(ParallelExhaustiveOptimizerOwnSettings.CalcMfeMae), defaultValue: false);
        CalcOpenPositionsCount = host.Get(nameof(ParallelExhaustiveOptimizerOwnSettings.CalcOpenPositionsCount), defaultValue: false);
        EquityCalcMode = (EquityCalcMode)host.Get(nameof(ParallelExhaustiveOptimizerOwnSettings.EquityCalcMode), defaultValue: (int)EquityCalcMode.Full);
    }

    public void WriteSettings(ISettingsHost host)
    {
        host.Set(nameof(ParallelExhaustiveOptimizerOwnSettings.ThreadsNumber), ThreadsNumber);
        host.Set(nameof(ParallelExhaustiveOptimizerOwnSettings.CalcLongResults), CalcLongResults);
        host.Set(nameof(ParallelExhaustiveOptimizerOwnSettings.CalcShortResults), CalcShortResults);
        host.Set(nameof(ParallelExhaustiveOptimizerOwnSettings.CalcMfeMae), CalcMfeMae);
        host.Set(nameof(ParallelExhaustiveOptimizerOwnSettings.CalcOpenPositionsCount), CalcOpenPositionsCount);
        host.Set(nameof(ParallelExhaustiveOptimizerOwnSettings.EquityCalcMode), (int)EquityCalcMode);
    }
}

public class ParallelExhaustiveOptimizerOwnSettings : UserControl
{
    private IContainer components;

    private Label lblTreadsNum;
    private NumericUpDown numTreadsNum;
    private CheckBox cbCalcLong;
    private CheckBox cbCalcShort;
    private CheckBox cbCalcMfeMae;
    private CheckBox cbCalcOpenPositionsCount;
    private Label lblEquityCalcMode;
    private ComboBox cbEquityCalcMode;

    public int ThreadsNumber
    {
        get => (int)numTreadsNum.Value;
        set => numTreadsNum.Value = value;
    }
    public bool CalcLongResults
    {
        get => cbCalcLong.Checked; 
        set => cbCalcLong.Checked = false;
    }
    public bool CalcShortResults
    {
        get => cbCalcShort.Checked; 
        set => cbCalcShort.Checked = false;
    }
    public bool CalcMfeMae
    {
        get => cbCalcMfeMae.Checked; 
        set => cbCalcMfeMae.Checked = false;
    }
    public bool CalcOpenPositionsCount
    {
        get => cbCalcOpenPositionsCount.Checked;
        set => cbCalcOpenPositionsCount.Checked = false;
    }
    public EquityCalcMode EquityCalcMode
    {
        get => (EquityCalcMode)cbEquityCalcMode.SelectedValue;
        set => cbEquityCalcMode.SelectedValue = EquityCalcMode.Full;
    }

    public ParallelExhaustiveOptimizerOwnSettings()
    {
        InitializeComponent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.lblTreadsNum = new System.Windows.Forms.Label();
        this.numTreadsNum = new System.Windows.Forms.NumericUpDown();
        this.cbCalcLong = new System.Windows.Forms.CheckBox();
        this.cbCalcShort = new System.Windows.Forms.CheckBox();
        this.cbCalcMfeMae = new System.Windows.Forms.CheckBox();
        this.cbCalcOpenPositionsCount = new System.Windows.Forms.CheckBox();
        this.lblEquityCalcMode = new System.Windows.Forms.Label();
        this.cbEquityCalcMode = new System.Windows.Forms.ComboBox();

        ((System.ComponentModel.ISupportInitialize)this.numTreadsNum).BeginInit();
        base.SuspendLayout();

        this.lblTreadsNum.AutoSize = true;
        this.lblTreadsNum.Location = new System.Drawing.Point(10, 14);
        this.lblTreadsNum.Name = "lblTreadsNum";
        this.lblTreadsNum.Size = new System.Drawing.Size(94, 13);
        this.lblTreadsNum.TabIndex = 0;
        this.lblTreadsNum.Text = "Treads number:";

        this.numTreadsNum.Location = new System.Drawing.Point(104, 14);
        this.numTreadsNum.Name = "numTreadsNum";
        this.numTreadsNum.Size = new System.Drawing.Size(133, 21);
        this.numTreadsNum.TabIndex = 1;
        this.numTreadsNum.Maximum = Environment.ProcessorCount;
        this.numTreadsNum.Minimum = 1;
        this.numTreadsNum.Value = Environment.ProcessorCount;

        this.cbCalcLong.Location = new System.Drawing.Point(10, 50);
        this.cbCalcLong.Name = "cbCalcLong";
        this.cbCalcLong.Size = new System.Drawing.Size(91, 17);
        this.cbCalcLong.TabIndex = 2;
        this.cbCalcLong.AutoSize = true;
        this.cbCalcLong.Checked = false;
        this.cbCalcLong.TabStop = true;
        this.cbCalcLong.UseVisualStyleBackColor = true;
        this.cbCalcLong.Text = "Calculate long trades results";

        this.cbCalcShort.Location = new System.Drawing.Point(10, 76);
        this.cbCalcShort.Name = "cbCalcShort";
        this.cbCalcShort.Size = new System.Drawing.Size(58, 20);
        this.cbCalcShort.TabIndex = 3;
        this.cbCalcShort.AutoSize = true;
        this.cbCalcShort.Checked = false;
        this.cbCalcShort.TabStop = true;
        this.cbCalcShort.UseVisualStyleBackColor = true;
        this.cbCalcShort.Text = "Calculate short trades results";

        this.cbCalcMfeMae.Location = new System.Drawing.Point(10, 102);
        this.cbCalcMfeMae.Name = "cbCalcMfeMae";
        this.cbCalcMfeMae.Size = new System.Drawing.Size(58, 20);
        this.cbCalcMfeMae.TabIndex = 4;
        this.cbCalcMfeMae.AutoSize = true;
        this.cbCalcMfeMae.Checked = false;
        this.cbCalcMfeMae.TabStop = true;
        this.cbCalcMfeMae.UseVisualStyleBackColor = true;
        this.cbCalcMfeMae.Text = "Calculate MAE and MFE of all positions";

        this.cbCalcOpenPositionsCount.Location = new System.Drawing.Point(10, 128);
        this.cbCalcOpenPositionsCount.Name = "cbCalcOpenPositionsCount";
        this.cbCalcOpenPositionsCount.Size = new System.Drawing.Size(58, 20);
        this.cbCalcOpenPositionsCount.TabIndex = 5;
        this.cbCalcOpenPositionsCount.AutoSize = true;
        this.cbCalcOpenPositionsCount.Checked = false;
        this.cbCalcOpenPositionsCount.TabStop = true;
        this.cbCalcOpenPositionsCount.UseVisualStyleBackColor = true;
        this.cbCalcOpenPositionsCount.Text = "Calculate series of open positions count";

        this.lblEquityCalcMode.AutoSize = true;
        this.lblEquityCalcMode.Location = new System.Drawing.Point(10, 164);
        this.lblEquityCalcMode.Name = "lblEquityCalcMode";
        this.lblEquityCalcMode.Size = new System.Drawing.Size(94, 13);
        this.lblEquityCalcMode.TabIndex = 6;
        this.lblEquityCalcMode.Text = "Mode of equity and cash curves calculation:";

        this.cbEquityCalcMode.Location = new System.Drawing.Point(10, 187);
        this.cbEquityCalcMode.Name = "cbEquityCalcMode";
        this.cbEquityCalcMode.Size = new System.Drawing.Size(98, 30);
        this.cbEquityCalcMode.TabIndex = 7;
        this.cbEquityCalcMode.AutoSize = true;
        this.cbEquityCalcMode.TabStop = true;
        this.cbEquityCalcMode.DataSource = Enum.GetValues(typeof(EquityCalcMode));

        base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
        base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        base.Controls.Add(this.cbCalcMfeMae);
        base.Controls.Add(this.cbCalcShort);
        base.Controls.Add(this.cbCalcLong);
        base.Controls.Add(this.cbCalcOpenPositionsCount);
        base.Controls.Add(this.lblEquityCalcMode);
        base.Controls.Add(this.cbEquityCalcMode);
        base.Controls.Add(this.numTreadsNum);
        base.Controls.Add(this.lblTreadsNum);
        base.Name = nameof(ParallelExhaustiveOptimizerOwnSettings);
        base.Size = new System.Drawing.Size(250, 250);

        ((System.ComponentModel.ISupportInitialize)this.numTreadsNum).EndInit();
        base.ResumeLayout(false);
        base.PerformLayout();
    }
}
