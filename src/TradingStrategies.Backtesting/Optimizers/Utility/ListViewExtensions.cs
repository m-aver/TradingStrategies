using System.Reflection;
using System.Windows.Forms;

namespace TradingStrategies.Backtesting.Optimizers.Utility;

internal static class ListViewExtensions
{
    private static readonly MethodInfo _onDoubleClickMethod = typeof(ListView)
        .GetMethod("OnDoubleClick", BindingFlags.Instance | BindingFlags.NonPublic);

    public static void OnDoubleClick(this ListView listView) => _onDoubleClickMethod.Invoke(listView, [EventArgs.Empty]);
}
