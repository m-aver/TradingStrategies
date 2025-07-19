using System.Windows.Forms;
using WealthLab;

namespace TradingStrategies.Backtesting.Optimizers.Utility;

internal class OptimizationErrorData(IReadOnlyCollection<double> ParameterValues, Exception Error)
{
    public IReadOnlyCollection<double> ParameterValues { get; } = ParameterValues;
    public Exception Error { get; } = Error;
}

internal class ErrorReporter
{
    private readonly ListView _errorsListView;

    public ErrorReporter(Optimizer optimizer)
    {
        _errorsListView = OptimizationFormExtractor.ExtractOptimizationErrorsListView(optimizer);
    }

    public void Report(IReadOnlyCollection<OptimizationErrorData> errorData)
    {
        const string symbol = "not specified";

        var rows = errorData.Select(e =>
        {
            var row = new ListViewItem(symbol);
            row.SubItems.Add(string.Join(",", e.ParameterValues));
            row.SubItems.Add(e.Error.Message);
            return row;
        }).ToArray();

        if (_errorsListView.InvokeRequired)
        {
            _errorsListView.Invoke(
                static (ListView view, ListViewItem[] newRows) => view.Items.AddRange(newRows),
                _errorsListView, rows);
        }
        else
        {
            _errorsListView.Items.AddRange(rows);
        }
    }
}
