using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Utility;

//обслуживает отображение прогресса оптимизации
internal class ProgressReporter
{
    private readonly ProgressBar _progressBar;
    private readonly Label _elapsed;
    private readonly Label _remaining;
    private readonly Stopwatch _runWatch;

    public ProgressReporter(Optimizer optimizer)
    {
        _progressBar = OptimizationFormExtractor.ExtractOptimizationProgressBar(optimizer);
        _elapsed = OptimizationFormExtractor.ExtractTimeElapsedLabel(optimizer);
        _remaining = OptimizationFormExtractor.ExtractTimeRemainingLabel(optimizer);
        _runWatch = new();
    }

    public void Start()
    {
        _runWatch.Restart();
    }

    public void ReportProgress(int current)
    {
        _progressBar.Value = current;
        _elapsed.Text = ToLabel(_runWatch.Elapsed);
        _remaining.Text = ToLabel(CalcRemaining());
    }

    private TimeSpan CalcRemaining()
    {
        long elapsed = _runWatch.Elapsed.Ticks;
        double ratio = ((double)_progressBar.Maximum / (double)_progressBar.Value) - 1;
        var remaining = elapsed * ratio;
        return new TimeSpan((long)remaining);
    }

    private static string ToLabel(TimeSpan timeSpan)
    {
        var builder = new StringBuilder();

        if (timeSpan.TotalDays >= 1.0)
        {
            builder.Append(timeSpan.TotalDays.ToString("N0") + " days ");
        }
        if (timeSpan.TotalDays >= 1.0 || timeSpan.Hours > 0)
        {
            builder.Append(timeSpan.Hours + " hours ");
        }
        if (timeSpan.TotalHours > 0.0 || timeSpan.Minutes > 0)
        {
            builder.Append(timeSpan.Minutes + " min ");
        }

        return builder.Append(timeSpan.Seconds + " sec").ToString();
    }
}
