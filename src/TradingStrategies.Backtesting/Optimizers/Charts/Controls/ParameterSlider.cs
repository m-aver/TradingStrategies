using System.Drawing;
using System.Windows.Forms;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Charts.Controls;

//ползунок для выбора значения параметра отптимизации
internal class ParameterSlider : UserControl
{
    private Label _labelName;
    private Label _labelValue;
    private TrackBar _slider;

    private StrategyParameter _parameter;
    private IReadOnlyList<double> _parameterValues;

    public ParameterSlider(StrategyParameter parameter, IReadOnlyList<double> parameterValues)
    {
        if (parameterValues.Count == 0)
        {
            throw new ArgumentException("Parameter values must be not empty", nameof(parameterValues));
        }

        _parameter = parameter;
        _parameterValues = parameterValues;

        InitializeComponent();
    }

    public double SelectedValue => _parameterValues[_slider.Value];

    private void InitializeComponent()
    {
        var start = new Point(0, 0);

        _labelName = new Label();
        _labelName.Text = _parameter.Name;
        _labelName.Size = new Size(100, 20);
        _labelName.Location = new Point(start.X, start.Y);

        var defaultIndex = _parameterValues.TakeWhile(x => x != _parameter.Value).Count();

        _slider = new TrackBar();
        _slider.Size = new Size(140, 20);
        _slider.Minimum = 0;
        _slider.Maximum = _parameterValues.Count - 1;
        _slider.SmallChange = 1;
        _slider.TickFrequency = 5;
        _slider.Value = defaultIndex == _parameterValues.Count ? 0 : defaultIndex;
        _slider.Location = new Point(start.X, _labelName.Location.Y + _labelName.Size.Height);
        _slider.Scroll += Slider_Scroll;

        _labelValue = new Label();
        _labelValue.Text = _parameter.Value.ToString();
        _labelValue.Location = new Point(_slider.Location.X + _slider.Width + 10, _slider.Location.Y);
        _labelValue.Size = new Size(50, 20);

        Size = new Size(200, 60);
        Controls.AddRange([_labelName, _slider, _labelValue]);
    }

    private void Slider_Scroll(object sender, EventArgs e)
    {
        _labelValue.Text = _parameterValues[_slider.Value].ToString();

        base.OnScroll(new ScrollEventArgs(ScrollEventType.ThumbPosition, _slider.Value));
    }
}
