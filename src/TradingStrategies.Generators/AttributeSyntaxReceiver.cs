using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TradingStrategies.Generators;

internal class AttributeSyntaxReceiver : ISyntaxReceiver
{
    public List<AttributeDefinition> AttributeDefinitions { get; } = new();

    private readonly HashSet<string> attributeNames;

    public AttributeSyntaxReceiver(params string[] attributeNames)
    {
        this.attributeNames = new HashSet<string>(attributeNames);
    }

    public void OnVisitSyntaxNode(SyntaxNode node)
    {
        if (node is AttributeSyntax attributeSyntax)
        {
            var collector = new AttributeCollector(attributeNames.ToArray());
            attributeSyntax.Accept(collector);
            AttributeDefinitions.AddRange(collector.AttributeDefinitions);
        }
    }
}
