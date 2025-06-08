using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace TradingStrategies.Generators;

[Generator]
internal class StrategyHelperGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new AttributeSyntaxReceiver("StrategyIntegration", "StrategyIntegrationAttribute"));
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not AttributeSyntaxReceiver receiver)
        {
            return;
        }

        var sourceBuilder = new StringBuilder();

        foreach (var attributeDefinition in receiver.AttributeDefinitions)
        {
            var nameExpression = attributeDefinition.FieldArguments.Single().Value.ToString();
            nameExpression = NormalizeName(nameExpression);
            var helper = GetHelperCode(nameExpression);
            sourceBuilder.Append(helper);

            context.AddSource($"StrategyHelperGenerator_{nameExpression.GetHashCode().Abs()}", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
            sourceBuilder.Clear();
        }
    }

    //converts nameof() to don't depend on namespaces
    internal static string NormalizeName(string name)
    {
        //there's many holes but seems it's enough
        return Regex.Replace(name, "nameof\\s*\\(\\s*(\\w+)\\s*\\)", "\"$1\"");
    }

    private static string GetHelperCode(string name)
    {
        var meta =
            $"""
            //this is generated code for integration strategy with WealthLab
            //strategy name: {name}
            """;

        var header =
            $"""
            using System;
            using WealthLab;
            using TradingStrategies.Backtesting.Tools;

            namespace TradingStrategies.Generator.StrategyHelper_{name.GetHashCode().Abs()};
            """;

        var wsClassName = "WealthScriptWrapper_Generated";
        var wsClass =
            $"""
            [StrategyIntegrationAttribute({name})]
            public class {wsClassName} : GeneratedWealthScriptWrapper;
            """;

        var helperClassName = "MyStrategyHelper_Generated";
        var helperClassBuilder = new StrategyHelperSourceCodeBuilder(helperClassName)
            .AddAuthor("Misha")
            .AddDescription("A strategy from visual studio source generator")
            //to keep strategy in start window
            .AddID(name.GetHashCode().ToGuid())
            .AddCreationDate(new DateTime())
            .AddLastModifiedDate(new DateTime())
            .AddWealthScriptType(wsClassName)
            .AddName(name);

        return new StringBuilder()
            .Append(meta)
            .AppendLine(2)
            .Append(header)
            .AppendLine(2)
            .Append(wsClass)
            .AppendLine(2)
            .Append(helperClassBuilder.Build())
            .ToString();
    }

    private static string GetHelperRaw(string name)
    {
        return
        $"""
        using System;
        using WealthLab;
        using TradingStrategies.Backtesting.Tools;

        namespace TradingStrategies.Generator.StrategyHelper_{name.GetHashCode().Abs()};
        """
        +
        """
        public class MyStrategyHelper_Generated : WealthLab.StrategyHelper
        {
            public override string Author { get; } = "Misha";
            public override DateTime CreationDate { get; } = new DateTime();
            public override string Description { get; } = "A strategy from visual studio source generator";
            public override Guid ID { get; } = Guid.NewGuid();
            public override DateTime LastModifiedDate { get; } = new DateTime();
            public override string Name { get; } = 
        """ + name +
        """
        ;

            public override Type WealthScriptType { get; } = typeof(WealthScriptWrapper_Generated);
        }
        """
        +
        $"""
        [StrategyIntegrationAttribute({name})]
        public class WealthScriptWrapper_Generated : GeneratedWealthScriptWrapper;
        """
        ;
    }
}
