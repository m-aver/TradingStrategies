using System.Reflection;
using System.Windows.Forms;
using WealthLab;

//подменяет основные результаты на результаты только Long и Short сделок
//так чтобы базовый скорекард пересчитал свои показатели на этих результатах

namespace TradingStrategies.Backtesting.Optimizers.Scorecards;

internal class LongShortScorecard : BasicExScorecard
{
    public new const string DisplayName = "Long and Short Scorecard";
    public override string FriendlyName => DisplayName;

    protected const string LongPostfix = "_Long";
    protected const string ShortPostfix = "_Short";

    private readonly string[] columnNames;
    private readonly string[] columnTypes;

    public override IList<string> ColumnHeadersRawProfit => [.. base.ColumnHeadersRawProfit, .. columnNames];
    public override IList<string> ColumnHeadersPortfolioSim => [.. base.ColumnHeadersPortfolioSim, .. columnNames];
    public override IList<string> ColumnTypesRawProfit => [.. base.ColumnTypesRawProfit, .. columnTypes];
    public override IList<string> ColumnTypesPortfolioSim => [.. base.ColumnTypesPortfolioSim, .. columnTypes];

    public LongShortScorecard()
    {
        columnNames =
            base.ColumnHeadersPortfolioSim.Select(x => x + LongPostfix)
            .Concat(base.ColumnHeadersPortfolioSim.Select(x => x + ShortPostfix))
            .ToArray();

        columnTypes =
            base.ColumnTypesPortfolioSim
            .Concat(base.ColumnTypesPortfolioSim)
            .ToArray();
    }

    public override void PopulateScorecard(ListViewItem resultRow, SystemPerformance performance)
    {
        var results = performance.Results;
        base.PopulateScorecard(resultRow, performance);
        
        //long
        SetResults(performance, performance.ResultsLong);
        base.PopulateScorecard(resultRow, performance);

        //short
        SetResults(performance, performance.ResultsShort);
        base.PopulateScorecard(resultRow, performance);

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
