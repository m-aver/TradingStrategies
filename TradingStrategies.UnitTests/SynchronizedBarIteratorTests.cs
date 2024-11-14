using System;
using WealthLab;

namespace TradingStrategies.UnitTests
{
    public partial class SynchronizedBarIteratorTests
    {
        [Theory]
        [MemberData(nameof(GetBarDates), parameters: 1)]
        [MemberData(nameof(GetBarDates), parameters: 10)]
        [MemberData(nameof(GetRandomBarDates), parameters: 100)]
        public void IterateIdentical(DateTime[][] barDates)
        {
            //arrange
            var barsCollection = barDates.Select(BarsHelper.FromDates).ToArray();

            var oldResults = new List<IterationResult>();
            var ownResults = new List<IterationResult>();

            //act
            var old = new WealthLab.SynchronizedBarIterator(barsCollection);
            var own = new TradingStrategies.Utilities.SynchronizedBarIterator(barsCollection);

            do
            {
                var oldResult = new IterationResult(old.Date);
                var ownResult = new IterationResult(own.Date);

                foreach (var bars in barsCollection)
                {
                    oldResult.Iterations.Add(old.Bar(bars));
                    ownResult.Iterations.Add(own.Bar(bars));
                }

                oldResults.Add(oldResult);
                ownResults.Add(ownResult);
            }
            while (old.Next() && own.Next());

            //assert
            Assert.False(old.Next());
            Assert.False(own.Next());

            Assert.Equal(oldResults, ownResults, IterationResultsComparer.Instance);
        }
    }
}