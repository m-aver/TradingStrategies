using Microsoft.CodeAnalysis;
using System.Text;

namespace TradingStrategies.Generators;

internal record AttributeDefinition
{
    public string Name { get; set; }
    public (string Name, object Value)[] FieldArguments { get; set; } = Array.Empty<(string Name, object Value)>();
    public (string Name, object Value)[] PropertyArguments { get; set; } = Array.Empty<(string Name, object Value)>();

    public string ToSource()
    {
        var definition = new StringBuilder(Name);
        if (!FieldArguments.Any() && !PropertyArguments.Any())
        {
            return definition.ToString();
        }

        return definition
            .Append("(")
            .Append(ArgumentsToString())
            .Append(")")
            .ToString();
    }

    private string ArgumentsToString()
    {
        var arguments = new StringBuilder();

        if (FieldArguments.Any())
        {
            arguments.Append(string.Join(", ", FieldArguments.Select(
                param => string.IsNullOrEmpty(param.Name)
                    ? $"{param.Value}"
                    : $"{param.Name}: {param.Value}")
            ));
        }

        if (PropertyArguments.Any())
        {
            arguments
                .Append(arguments.Length > 0 ? ", " : "")
                .Append(string.Join(", ", PropertyArguments.Select(
                    param => $"{param.Name} = {param.Value}")
                ));
        }

        return arguments.ToString();
    }
}
