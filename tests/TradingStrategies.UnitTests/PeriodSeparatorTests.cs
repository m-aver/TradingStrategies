using System.Globalization;
using TradingStrategies.Backtesting.Utility;

namespace TradingStrategies.UnitTests
{
    public partial class PeriodSeparatorTests
    {
        [Theory]
        [MemberData(nameof(GetPeriodSeparatorTestData_Simple))]
        public void GetPeriods_Simple_Success(PeriodSeparatorTestData data)
        {
            //arrange
            var separator = new PeriodSeparator();

            //act
            var periods = separator.GetPeriods(data.RangeToSeparate, data.PeriodInfo);

            //assert
            Assert.Equal(data.ResultRanges, periods);
        }

        [Theory]
        [MemberData(nameof(GetPeriodSeparatorTestData_Aligned))]
        public void GetPeriods_Aligned_Success(PeriodSeparatorTestData data)
        {
            //arrange
            var separator = new AlignedPeriodSeparator();

            //act
            var periods = separator.GetPeriods(data.RangeToSeparate, data.PeriodInfo);

            //assert
            Assert.Equal(data.ResultRanges, periods);
        }

        [Theory]
        [MemberData(nameof(GetPeriodSeparatorTestData_Aligned_Week))]
        public void GetPeriods_Aligned_Week_Success(PeriodSeparatorTestData_Aligned_Week data)
        {
            //arrange
            var separator = new AlignedPeriodSeparator();

            var oldCulture = CultureInfo.CurrentCulture;
            var newCulture = oldCulture.Clone() as CultureInfo;
            newCulture!.DateTimeFormat.FirstDayOfWeek = data.FirstDayOfWeek;

            //act
            IEnumerable<DateTimeRange> periods = [];
            try
            {
                CultureInfo.CurrentCulture = newCulture;
                periods = separator.GetPeriods(data.RangeToSeparate, data.PeriodInfo);
            }
            finally
            {
                CultureInfo.CurrentCulture = oldCulture;
            }

            //assert
            Assert.Equal(data.ResultRanges, periods);
        }
    }
}
