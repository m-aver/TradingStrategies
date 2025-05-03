namespace TradingStrategies.Generators;

internal static class ConvertationExtensions
{
    public static int Abs(this int value) => Math.Abs(value);
    public static Guid ToGuid(this int value) => new(value, 0, 0, new byte[8]);
}