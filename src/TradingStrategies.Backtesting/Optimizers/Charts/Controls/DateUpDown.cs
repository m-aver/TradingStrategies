using System.Globalization;
using System.Windows.Forms;

namespace TradingStrategies.Backtesting.Optimizers.Charts.Controls;

//wrapper for NumericUpDown with yyyyMMdd format
public class DateUpDown : UserControl
{
    private NumericUpDown _input;

    public event EventHandler ValueChanged;

    public DateUpDown()
    {
        InitializeComponent();
    }

    public DateTime Minimum
    {
        get => FromNumber(_input.Minimum);
        set => _input.Minimum = ToNumber(value);
    }
    public DateTime Maximum
    {
        get => FromNumber(_input.Maximum);
        set => _input.Maximum = ToNumber(value);
    }
    public DateTime Value
    {
        get => FromNumber(_input.Value);
        set => _input.Value = ToNumber(value);
    }

    private static DateTime FromNumber(decimal value)
    {
        const string format = "yyyyMMdd";
        CultureInfo culture = CultureInfo.InvariantCulture;
        const DateTimeStyles style = DateTimeStyles.AllowWhiteSpaces;

        var date = DateTime.TryParseExact(value.ToString(), format, culture, style, out var x) ? x : DateTime.MinValue;
        date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
        return date;
    }

    private static int ToNumber(DateTime date) => int.Parse($"{date.Year}{date.Month:00}{date.Day:00}");

    private void InitializeComponent()
    {
        _input = new NumericUpDown();
        _input.ValueChanged += Input_ValueChanged;
        SizeChanged += DateUpDown_SizeChanged;

        Controls.Add( _input);
    }

    private void Input_ValueChanged(object sender, EventArgs e)
    {
        ValueChanged?.Invoke(this, e);
    }

    private void DateUpDown_SizeChanged(object sender, EventArgs e)
    {
        _input.Size = this.Size;
    }
}
