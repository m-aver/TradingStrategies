using System.Text;

namespace TradingStrategies.Generators;

internal static class StringBuilderExtensions
{
    public static StringBuilder AppendSpace(this StringBuilder builder) => builder.Append(" ");
    public static StringBuilder AppendEscaped(this StringBuilder builder, string value) => builder.Append($"\"{value}\"");
    public static StringBuilder Prepend(this StringBuilder builder, string value) => builder.Insert(0, value);
    public static StringBuilder AppendLine(this StringBuilder builder, int linesCount)
    {
        for (int i = 0; i < linesCount; i++) 
            builder.AppendLine(); 
        return builder;
    }
}