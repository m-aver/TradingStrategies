using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace TradingStrategies.Generators;

internal class AttributeCollector : CSharpSyntaxVisitor
{
    private readonly HashSet<string> attributeNames;

    public List<AttributeDefinition> AttributeDefinitions { get; } = new();

    public AttributeCollector(params string[] attributeNames)
    {
        this.attributeNames = new HashSet<string>(attributeNames);
    }

    public override void VisitAttribute(AttributeSyntax node)
    {
        base.VisitAttribute(node);

        if (!attributeNames.Contains(node.Name.ToString()))
        {
            return;
        }

        var fieldArguments = new List<(string Name, object Value)>();
        var propertyArguments = new List<(string Name, object Value)>();

        var arguments = node.ArgumentList?.Arguments.ToArray() ?? Array.Empty<AttributeArgumentSyntax>();
        foreach (var syntax in arguments)
        {
            if (syntax.NameColon != null)
            {
                fieldArguments.Add((syntax.NameColon.Name.ToString(), syntax.Expression));
            }
            else if (syntax.NameEquals != null)
            {
                propertyArguments.Add((syntax.NameEquals.Name.ToString(), syntax.Expression));
            }
            else
            {
                fieldArguments.Add((string.Empty, syntax.Expression));
            }
        }

        AttributeDefinitions.Add(new AttributeDefinition
        {
            Name = node.Name.ToString(),
            FieldArguments = fieldArguments.ToArray(),
            PropertyArguments = propertyArguments.ToArray()
        });
    }
}
