using TradingStrategies.Generators;

namespace TradingStrategies.UnitTests;

public class StrategyHelperSourceCodeBuilderTests
{
    [Fact]
    public void BuildCode_Success()
    {
        //arrange
        var date = new DateTime();
        var guid = new Guid("706fa5b0-5ae6-4df0-bd97-ab09bbb1073b");

        var expected =
            """
            public sealed class MyStrategyHelper_Generated : WealthLab.StrategyHelper
            {
                public override string Author { get; } = "Misha";
                public override DateTime CreationDate { get; } = DateTime.Parse("01.01.0001 0:00:00");
                public override string Description { get; } = "A strategy from visual studio source generator";
                public override Guid ID { get; } = Guid.Parse("706fa5b0-5ae6-4df0-bd97-ab09bbb1073b");
                public override DateTime LastModifiedDate { get; } = DateTime.Parse("01.01.0001 0:00:00");
                public override string Name { get; } = "test";
                public override Type WealthScriptType { get; } = typeof(WealthScriptWrapper_Generated);
            }
            """;

        var name = "\"test\"";
        var wsClassName = "WealthScriptWrapper_Generated";
        var helperClassName = "MyStrategyHelper_Generated";

        var helperClassBuilder = new StrategyHelperSourceCodeBuilder(helperClassName)
            .AddAuthor("Misha")
            .AddCreationDate(date)
            .AddDescription("A strategy from visual studio source generator")
            .AddID(guid)
            .AddLastModifiedDate(date)
            .AddName(name)
            .AddWealthScriptType(wsClassName);

        //act
        var result = helperClassBuilder.Build();

        //assert
        Assert.Equal(expected, result, 
            ignoreLineEndingDifferences: true, 
            ignoreWhiteSpaceDifferences: true, 
            ignoreAllWhiteSpace: true);
    }
}
