using System.Reflection;
using System.Windows.Forms;
using WealthLab;

//подменяет основные результаты на результаты только Long и Short сделок
//так чтобы базовый скорекард пересчитал свои показатели на этих результатах

namespace TradingStrategies.Backtesting.Optimizers.Scorecards;

internal class LongShortScorecard : CustomScorecard
{
    //settings
    private const bool ApplyBaseResults = true;
    private const bool ApplyLongResults = true;
    private const bool ApplyShortResults = true;
    private const bool ApplyVisualSeparation = true;

    public new const string DisplayName = "Long and Short Scorecard";
    public override string FriendlyName => DisplayName;

    public override IList<string> ColumnHeadersRawProfit => BuildColumnHeaders(base.ColumnHeadersRawProfit);
    public override IList<string> ColumnHeadersPortfolioSim => BuildColumnHeaders(base.ColumnHeadersPortfolioSim);
    public override IList<string> ColumnTypesRawProfit => BuildColumnTypes(base.ColumnTypesRawProfit);
    public override IList<string> ColumnTypesPortfolioSim => BuildColumnTypes(base.ColumnTypesPortfolioSim);

    private const string LongPostfix = "_Long";
    private const string ShortPostfix = "_Short";

    //visual separation
    private const string LongSeparatorColumnName = "LongSeparator";
    private const string ShortSeparatorColumnName = "ShortSeparator";
    private const string LongSeparator = "▲";
    private const string ShortSeparator = "▼";
    private const string SaperatorColumnType = "S";

    private static IList<string> BuildColumnHeaders(IList<string> baseHeaders)
    {
        return Enumerable.Empty<string>()
            .Concat(ApplyBaseResults ? baseHeaders : [])
            .Concat(ApplyLongResults && ApplyVisualSeparation ? [LongSeparatorColumnName] : [])
            .Concat(ApplyLongResults ? baseHeaders.Select(x => x + LongPostfix) : [])
            .Concat(ApplyShortResults && ApplyVisualSeparation ? [ShortSeparatorColumnName] : [])
            .Concat(ApplyShortResults ? baseHeaders.Select(x => x + ShortPostfix) : [])
            .ToArray();
    }

    private static IList<string> BuildColumnTypes(IList<string> baseTypes)
    {
        return Enumerable.Empty<string>()
            .Concat(ApplyBaseResults ? baseTypes : [])
            .Concat(ApplyLongResults && ApplyVisualSeparation ? [SaperatorColumnType] : [])
            .Concat(ApplyLongResults ? baseTypes : [])
            .Concat(ApplyShortResults && ApplyVisualSeparation ? [SaperatorColumnType] : [])
            .Concat(ApplyShortResults ? baseTypes : [])
            .ToArray();
    }

    public override void PopulateScorecard(ListViewItem resultRow, SystemPerformance performance)
    {
        var results = performance.Results;

        if (ApplyBaseResults)
        {
            base.PopulateScorecard(resultRow, performance);
        }

        //long
        if (ApplyLongResults)
        {
            if (ApplyVisualSeparation)
                resultRow.SubItems.Add(LongSeparator);

            SetResults(performance, performance.ResultsLong);
            base.PopulateScorecard(resultRow, performance);
        }

        //short
        if (ApplyShortResults)
        {
            if (ApplyVisualSeparation)
                resultRow.SubItems.Add(ShortSeparator);

            SetResults(performance, performance.ResultsShort);
            base.PopulateScorecard(resultRow, performance);
        }

        //restore
        SetResults(performance, results);
    }

    private static void SetResults(SystemPerformance performance, SystemResults results)
    {
        _resultsProp.SetValue(performance, results);
    }

    private static readonly PropertyInfo _resultsProp = typeof(SystemPerformance)
        .GetProperty(nameof(SystemPerformance.Results), BindingFlags.Instance | BindingFlags.Public);
}
