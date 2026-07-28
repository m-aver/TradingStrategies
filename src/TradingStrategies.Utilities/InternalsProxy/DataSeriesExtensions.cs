using System.Reflection;
using WealthLab;

namespace TradingStrategies.Utilities.InternalsProxy;

public static class DataSeriesExtensions
{
    private static readonly FieldInfo _datesField = typeof(DataSeries).GetField("_dates", BindingFlags.NonPublic | BindingFlags.Instance);
    public static List<DateTime> GetRawDates(this DataSeries dataSeries) => (List<DateTime>)_datesField.GetValue(dataSeries);

    private static readonly FieldInfo _valuesField = typeof(DataSeries).GetField("_values", BindingFlags.NonPublic | BindingFlags.Instance);
    public static List<double> GetRawValues(this DataSeries dataSeries) => (List<double>)_valuesField.GetValue(dataSeries);

    public static void ClearValues(this DataSeries dataSeries) => dataSeries.method_2();
    public static void ClearFull(this DataSeries dataSeries)
    {
        dataSeries.ClearValues();
        dataSeries.GetRawDates().Clear();
    }

    public static void RemoveValueAt(this DataSeries dataSeries, int index) => dataSeries.method_3(index);
    public static void RemoveDateAt(this DataSeries dataSeries, int index) => dataSeries.GetRawDates().RemoveAt(index);
    public static void RemoveAt(this DataSeries dataSeries, int index)
    {
        dataSeries.RemoveValueAt(index);
        dataSeries.RemoveDateAt(index);
    }
}
