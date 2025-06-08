using System.Text;

namespace TradingStrategies.Generators;

internal class StrategyHelperSourceCodeBuilder
{
    private readonly StringBuilder _builder = new();

    private const string StrategyHelperName = "WealthLab.StrategyHelper";

    public StrategyHelperSourceCodeBuilder(string className)
    {
        _builder
            .Append("public")
            .AppendSpace()
            .Append("sealed")
            .AppendSpace()
            .Append("class")
            .AppendSpace()
            .Append(className)
            .AppendSpace()
            .Append(":")
            .AppendSpace()
            .Append(StrategyHelperName);

        _builder
            .AppendLine()
            .Append('{')
            .AppendLine();
    }

    public StrategyHelperSourceCodeBuilder AddAuthor(string value)
    {
        AppendPublicOverrideTokens()
            .Append("string")
            .AppendSpace()
            .Append("Author");
        AppendPropertyInitializer()
            .AppendEscaped(value)
            .Append(';')
            .AppendLine();

        return this;
    }
    public StrategyHelperSourceCodeBuilder AddCreationDate(DateTime value)
    {
        AppendPublicOverrideTokens()
            .Append("DateTime")
            .AppendSpace()
            .Append("CreationDate");
        AppendPropertyInitializer()
            .Append(ToCode(value))
            .Append(';')
            .AppendLine();

        return this;
    }
    public StrategyHelperSourceCodeBuilder AddDescription(string value)
    {
        AppendPublicOverrideTokens()
            .Append("string")
            .AppendSpace()
            .Append("Description");
        AppendPropertyInitializer()
            .AppendEscaped(value)
            .Append(';')
            .AppendLine();

        return this;
    }
    public StrategyHelperSourceCodeBuilder AddID(Guid value)
    {
        AppendPublicOverrideTokens()
            .Append("Guid")
            .AppendSpace()
            .Append("ID");
        AppendPropertyInitializer()
            .Append(ToCode(value))
            .Append(';')
            .AppendLine();

        return this;
    }
    public StrategyHelperSourceCodeBuilder AddLastModifiedDate(DateTime value)
    {
        AppendPublicOverrideTokens()
            .Append("DateTime")
            .AppendSpace()
            .Append("LastModifiedDate");
        AppendPropertyInitializer()
            .Append(ToCode(value))
            .Append(';')
            .AppendLine();

        return this;
    }
    public StrategyHelperSourceCodeBuilder AddName(string value)
    {
        AppendPublicOverrideTokens()
            .Append("string")
            .AppendSpace()
            .Append("Name");
        AppendPropertyInitializer()
            .Append(value)
            .Append(';')
            .AppendLine();

        return this;
    }

    public StrategyHelperSourceCodeBuilder AddWealthScriptType(string typeName)
    {
        AppendPublicOverrideTokens()
            .Append("Type")
            .AppendSpace()
            .Append("WealthScriptType");
        AppendPropertyInitializer()
            .Append($"typeof({typeName})")
            .Append(';')
            .AppendLine();

        return this;
    }

    public string Build()
    {
        _builder.Append('}');

        return _builder.ToString();
    }

    private StringBuilder AppendPublicOverrideTokens()
    {
        return _builder
           .Append("public")
           .AppendSpace()
           .Append("override")
           .AppendSpace();
    }

    private StringBuilder AppendPropertyInitializer()
    {
        return _builder
           .AppendSpace()
           .Append('{')
           .AppendSpace()
           .Append("get;")
           .AppendSpace()
           .Append('}')
           .AppendSpace()
           .Append('=')
           .AppendSpace();
    }

    private static string ToCode(Guid value) => $"Guid.Parse({Escape(value.ToString())})";
    private static string ToCode(DateTime value) => $"DateTime.Parse({Escape(value.ToString())})";

    private static string Escape(string value) => $"\"{value}\"";
}

internal class WealthScriptSourceCodeBuilder
{
    private readonly StringBuilder _builder = new();

    private const string WealthScriptName = "TradingStrategies.Backtesting.Tools.GeneratedWealthScriptWrapper";
    private const string StrategyNameAttribute = "TradingStrategies.Backtesting.Tools.StrategyAttribute";

    public WealthScriptSourceCodeBuilder(string className)
    {
        _builder
            .Append("public")
            .AppendSpace()
            .Append("sealed")
            .AppendSpace()
            .Append("class")
            .AppendSpace()
            .Append(className)
            .AppendSpace()
            .Append(":")
            .AppendSpace()
            .Append(WealthScriptName);

        _builder
            .AppendLine()
            .Append('{')
            .AppendLine()
            .Append('}');
    }

    public WealthScriptSourceCodeBuilder JoinStrategyName(string strategyName)
    {
        var attributeExpression = new StringBuilder()
            .Append('[')
            .Append(StrategyNameAttribute)
            .Append('(')
            .Append(strategyName)
            .Append(')')
            .Append(']')
            .AppendLine()
            .ToString();

        _builder
            .Prepend(attributeExpression);

        return this;
    }

    public string Build()
    {
        return _builder.ToString();
    }
}
