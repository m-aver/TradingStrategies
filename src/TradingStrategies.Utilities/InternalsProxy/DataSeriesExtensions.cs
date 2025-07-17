using System.Reflection;
using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class DataSeriesExtensions
{
    public static readonly FieldInfo _datesField = typeof(DataSeries).GetField("_dates", BindingFlags.NonPublic | BindingFlags.Instance);
    public static List<DateTime> GetDates(this DataSeries dataSeries) => (List<DateTime>)_datesField.GetValue(dataSeries);

    public static void ClearValues(this DataSeries dataSeries) => dataSeries.method_2();
    public static void ClearFull(this DataSeries dataSeries)
    {
        dataSeries.ClearValues();
        dataSeries.GetDates().Clear();
    }

    public static void RemoveValueAt(this DataSeries dataSeries, int index) => dataSeries.method_3(index);
    public static void RemoveDateAt(this DataSeries dataSeries, int index) => dataSeries.GetDates().RemoveAt(index);
    public static void RemoveAt(this DataSeries dataSeries, int index)
    {
        dataSeries.RemoveValueAt(index);
        dataSeries.RemoveDateAt(index);
    }
}
