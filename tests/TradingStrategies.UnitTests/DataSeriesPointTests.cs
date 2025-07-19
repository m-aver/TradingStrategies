using TradingStrategies.Backtesting.Utility;
using WealthLab;

namespace TradingStrategies.UnitTests;

public class DataSeriesPointTests
{
    [Theory]
    [MemberData(nameof(GetTestData))]
    public void ToPoints_IterateCorrect(DataSeries series, DataSeriesPoint[] points)
    {
        var result = series.ToPoints();
        Assert.Equal(points, result);
    }

    public static IEnumerable<object[]> GetTestData()
    {
        DataSeries series;
        DataSeriesPoint[] points;

        series = new DataSeries("empty-series");
        points = new DataSeriesPoint[0];
        yield return new object[]
        {
            series,
            points,
        };

        series = new DataSeries("one-point-series");
        series.Add(1, new(2020, 1, 1));
        points = [new(1, new(2020, 1, 1))];
        yield return new object[]
        {
            series,
            points,
        };

        series = new DataSeries("multi-point-series");
        series.Add(1, new(2020, 1, 1));
        series.Add(1, new(2021, 1, 1));
        series.Add(2, new(2022, 1, 1));
        series.Add(3, new(2023, 1, 1));
        series.Add(3, new(2024, 1, 1));

        points = 
        [
            new(1, new(2020, 1, 1)),
            new(1, new(2021, 1, 1)),
            new(2, new(2022, 1, 1)),
            new(3, new(2023, 1, 1)),
            new(3, new(2024, 1, 1)),
        ];

        yield return new object[]
        {
            series,
            points,
        };
    }
}
