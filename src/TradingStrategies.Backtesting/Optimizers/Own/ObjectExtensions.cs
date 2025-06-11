namespace TradingStrategies.Backtesting.Optimizers.Own;

internal static class ObjectExtensions
{
    public static void Call(this object obj, string method)
    {
        var mi = obj.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        mi.Invoke(obj, null);
    }

    public static void Call(this object obj, string method, params object[] parametes)
    {
        var mi = obj.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        mi.Invoke(obj, parametes);
    }

    public static void Set(this object obj, string prop, object value)
    {
        var pi = obj.GetType().GetProperty(prop, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        pi.SetValue(obj, value);
    }

    public static object Get(this object obj, string prop)
    {
        var pi = obj.GetType().GetProperty(prop, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return pi.GetValue(obj);
    }
}
