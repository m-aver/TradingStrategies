using Steema.TeeChart;
using Steema.TeeChart.Styles;
using System.Diagnostics;
using System.Windows.Forms;
using TradingStrategies.Backtesting.Optimizers.Utility;

namespace TradingStrategies.Backtesting.Optimizers.Charts.Controls;

//обертка над TChart
//добавлено событие изменения точки графика под указателем мыши

public class TChartEx : TChart
{
    public event EventHandler<MouseHoveredPointChangedEventArgs> MouseHoveredPointChanged;

    public TChartEx() : base()
    {
        base.MouseMove += Chart_MouseMove;
    }

    private void OnMouseHoveredPointChanged(object sender, MouseHoveredPointChangedEventArgs args)
    {
        MouseHoveredPointChanged?.Invoke(sender, args);
    }

    private int prevPointIdx = -1;
    private static SpinLock spinLock = new(Debugger.IsAttached);

    private void Chart_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.Location;
        var idx = -1;
        var series = Series.Cast<Series>().FirstOrDefault(s => (idx = s.Clicked(pos)) != -1);

        var prevIdx = Interlocked.Exchange(ref prevPointIdx, idx);

        if (prevIdx != idx)
        {
            var lockTaken = false;
            try
            {
                spinLock.Enter(ref lockTaken);

                OnMouseHoveredPointChanged((object)series ?? this, new(idx, prevIdx));
            }
            finally
            {
                if (lockTaken)
                {
                    spinLock.Exit();
                }
            }
        }
    }
}
