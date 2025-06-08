using TradingStrategies.Backtesting.Utility;

namespace TradingStrategies.UnitTests
{
    public partial class PeriodicalSeriesCalculatorTests
    {
        [Theory]
        [MemberData(nameof(GetPeriodicalCalculatorTestData))]
        public void SeparateByPeriod_Success(PeriodicalSeparatorTestData data)
        {
            //arrange
            var separator = new PeriodSeparator();
            var periodicalCalculator = new PeriodicalSeriesCalculator(separator);

            //act
            var separatedSeries = periodicalCalculator.SeparateByPeriod(data.SeriesToSeparate, data.PeriodInfo);

            //assert
            Assert.Equal(data.SeparatedSeries.ToPoints(), separatedSeries.ToPoints());
        }

        [Theory]
        [MemberData(nameof(GetPeriodicalCalculatorTestData_Invalid))]
        public void SeparateByPeriod_Invalid_Exception(InvalidPeriodicalSeparatorTestData data)
        {
            //arrange
            var separator = new PeriodSeparator();
            var periodicalCalculator = new PeriodicalSeriesCalculator(separator);

            //act, assert
            var exception = Assert.Throws(data.TypeOfExeption, () => 
                periodicalCalculator.SeparateByPeriod(data.SeriesToSeparate, data.PeriodInfo));
        }
    }
}
