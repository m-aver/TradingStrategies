using System.Reflection;
using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class DataSeriesExtensions
{
    public static readonly FieldInfo _datesField = typeof(DataSeries).GetField("_dates", BindingFlags.NonPublic | BindingFlags.Instance);

    public static void ClearValues(this DataSeries dataSeries) => dataSeries.method_2();
    public static void ClearFull(this DataSeries dataSeries)
    {
        dataSeries.method_2();
        var dates = (List<DateTime>) _datesField.GetValue(dataSeries);
        dates.Clear();
    }
}
