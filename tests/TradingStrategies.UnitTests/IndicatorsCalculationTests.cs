using System;
using TradingStrategies.Backtesting.Utility;
using WealthLab;

namespace TradingStrategies.UnitTests;

public class IndicatorsCalculationTests
{
    //показывает, что sharpe не зависит от характера распределения доходностей, а только от их величин
    //стратегия, у которой положительные и отрицательные доходности распределены равномерно, будет иметь более плавную кривую
    //в отличие от стратегии, которая имеет продолжительные периоды заработков и убытков при тех же величинах
    //обе стратегии будут иметь одинаковые NetProfit и Sharpe, но предпочтительнее, имхо, все же первая
    [Fact]
    public void SharpeRatio_OrderOfReturns_DoesNotAffectSharpe()
    {
        //arrange
        var monthReturnValues = new double[]
        {
            20, -30, 0, -15, -10, 0, 10, 20, 100, -10, 110, 0, 10
        };

        var monthReturnSeries = ToMonthlySeries(monthReturnValues);
        var orderedMonthReturnSeries = ToMonthlySeries(monthReturnValues.Order());
        var orderedDescMonthReturnSeries = ToMonthlySeries(monthReturnValues.OrderDescending());

        //act
        var sharpe = IndicatorsCalculator.SharpeRatio(monthReturnSeries, 0);
        var sharpeOfOrderedSeries = IndicatorsCalculator.SharpeRatio(orderedMonthReturnSeries, 0);
        var sharpeOfOrderedDescSeries = IndicatorsCalculator.SharpeRatio(orderedDescMonthReturnSeries, 0);

        //assert
        Assert.Equal(sharpe, sharpeOfOrderedSeries);
        Assert.Equal(sharpe, sharpeOfOrderedDescSeries);
    }

    //пытался проверить получится ли Sharpe одинаковым, если равномерно увеличивать значения доходностей
    //не подтвердилось, но вот что интересно - модифицированная версия с увеличенным разбросом доходностей получила больший Sharpe, что плохо
    //хотя это не детерминированно - другая версия (c 90, -30) получила меньший
    [Fact(Skip = "not confirmed")]
    public void SharpeRatio_ValuesOfReturns_DoesNotAffectSharpe()
    {
        //arrange
        var monthReturnValues = new double[]
        {
            20, -30, 0, -15, -10, 0, 10, 20, 100, -10, 110, 0
        };

        //(20, -30) % = 1,2 * 0,7 = 0,84 = 1,4 * 0,6 = (40, -40) % = (20 + 20, -30 - 10) %
        var monthReturnFactors = new double[]
        {
            20, -10,
            0, 0,
            -15, 20,
            0, 0,
            0, 0,
            0, 0,
            //90, -30,
        };

        var multipliedMonthReturnValues = monthReturnValues.Zip(monthReturnFactors, (v, f) => v + f);

        var monthReturnSeries = ToMonthlySeries(monthReturnValues);
        var multipliedMonthReturnSeries = ToMonthlySeries(multipliedMonthReturnValues);

        const int startingCapital = 100_000;
        var equitySeries = ToEquity(startingCapital, monthReturnSeries);
        var multipliedEquitySeries = ToEquity(startingCapital, multipliedMonthReturnSeries);

        //act
        var sharpe = IndicatorsCalculator.SharpeRatio(monthReturnSeries, 0);
        var sharpeOfMultipliedSeries = IndicatorsCalculator.SharpeRatio(multipliedMonthReturnSeries, 0);

        //assert
        Assert.Equal(equitySeries.ToPoints().Last(), multipliedEquitySeries.ToPoints().Last(), precision: 5);
        Assert.Equal(sharpe, sharpeOfMultipliedSeries);
    }

    //ошибка от экспоненты выглядит более предпочтительным индикатором, т.к. учитывает равномерность распределения доходностей
    //хотя судя по опыту на реальных данных, все же на нее тоже нельзя однозначно ориентироваться при анализе близких значений, но подсветить хорошие стратегии она помогает
    [Theory]
    [MemberData(nameof(GetUniformMonthReturnValues))]
    public void SquaredError_OrderOfReturns_AffectsError(double[] uniformMonthReturnValues)
    {
        //arrange
        const double startingCapital = 100_000;

        var equityOfUniformReturns = ToEquity(startingCapital, ToMonthlySeries(uniformMonthReturnValues));
        var equityOfOrderedReturns = ToEquity(startingCapital, ToMonthlySeries(uniformMonthReturnValues.Order()));
        var equityOfOrderedDescReturns = ToEquity(startingCapital, ToMonthlySeries(uniformMonthReturnValues.OrderDescending()));

        //act
        var errorOfUniformReturns = CalculateError(IndicatorsCalculator.LogError(equityOfUniformReturns));
        var errorOfOrderedReturns = CalculateError(IndicatorsCalculator.LogError(equityOfOrderedReturns));
        var errorOfOrderedDescReturns = CalculateError(IndicatorsCalculator.LogError(equityOfOrderedReturns));

        //assert
        Assert.True(errorOfUniformReturns < errorOfOrderedReturns);
        Assert.True(errorOfUniformReturns < errorOfOrderedDescReturns);

        static double CalculateError(DataSeries errorSeries) => Math.Sqrt(errorSeries.GetValues().Sum(MathHelper.Sqr) / errorSeries.Count);
    }

    //test data
    public static IEnumerable<object[]> GetUniformMonthReturnValues()
    {
        yield return Wrap(new double[] { 20, -10, 20, -10, 20, -10, 20, -10, 20, -10, 20, -10 });
        yield return Wrap(new double[] { 15, -5, 15, -5, 15, -5, 15, -5, 15, -5, 15, -5, 15, -5, 15, -5 });
        yield return Wrap(
            new double[]
            {
                12.5, -3.2, 15.6, -7.1, 16.1, 4.9, 15.3, -2.2, 14.8, 5.0,
                13.5, 1.1, 15.9, -8.8, 14.0, 6.4, 13.6, -4.0, 14.5, 7.2,
                12.9, 2.4, 15.8, -6.5, 14.7, 3.1, 13.8, -1.2, 16.2, 5.5,
                12.7, 0.0, 15.7, -9.3, 14.0, 6.3, 13.9, -5.1, 14.8, 4.8,
                12.6, 1.5, 15.6, -2.5, 14.9, 3.7, 13.5, -7.0, 16.0, 6.1,
                12.5, -4.1, 15.4, -3.8, 14.7, 8.1, 12.9, 2.0, 15.3, -1.5,
                12.8, -2.3, 15.1, 0.5, 14.9, 6.5, 12.6, 1.2, 15.7, -5.3,
                12.7, 3.9, 15.6, -4.5, 14.1, 7.0, 12.5, -3.5, 15.9, 2.8,
                12.8, -1.2, 15.4, 1.9, 14.6, 5.4, 12.6, -3.0, 15.2, 0.3,
                12.5, -6.0, 15.8, 4.2, 14.3, 2.5, 12.9, -5.0, 15.1, 3.3
            }
        );

        yield return Wrap(new double[] { 15, -5, 15, -5, 15, -5, 15, -5, 15, -5 });
        yield return Wrap(new double[] { 10, 0, 10, 0, 10, 0, 10, 0, 10, 0 });

        static object[] Wrap(object x) => [x];
    }

    private static DataSeries ToMonthlySeries(IEnumerable<double> monthReturnValues)
    {
        var startDate = new DateTime(2000, 12, 1);
        return monthReturnValues
            .Select((x, i) => new DataSeriesPoint(
                value: x,
                date: startDate.AddMonths(i)))
            .ToSeries(
                "test-month-return-series");
    }

    private static DataSeries ToEquity(double startingCapital, DataSeries monthReturnSeries)
    {
        var currentEquity = startingCapital;
        return monthReturnSeries
            .ToPoints()
            .EnsureOrdered()
            .Select(mrp =>
            {
                var ep = mrp.WithValue(currentEquity);
                currentEquity *= (1 + mrp.Value / 100);
                return ep;
            })
            .ToList()
            .Append(new DataSeriesPoint(
                value: currentEquity,
                date: monthReturnSeries.Date[^1].AddMonths(1)))
            .ToSeries(
                "test-equity-from-month-returns");
    }
}