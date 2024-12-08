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
            var oldIterator = new WealthLab.SynchronizedBarIterator(barsCollection);
            var ownIterator = new TradingStrategies.Utilities.SynchronizedBarIterator(barsCollection);

            do
            {
                var oldResult = new IterationResult(oldIterator.Date);
                var ownResult = new IterationResult(ownIterator.Date);

                foreach (var bars in barsCollection)
                {
                    oldResult.Iterations.Add(oldIterator.Bar(bars));
                    ownResult.Iterations.Add(ownIterator.Bar(bars));
                }

                oldResults.Add(oldResult);
                ownResults.Add(ownResult);
            }
            while (oldIterator.Next() && ownIterator.Next());

            //assert
            Assert.False(oldIterator.Next());
            Assert.False(ownIterator.Next());

            Assert.Equal(oldResults, ownResults, IterationResultsComparer.Instance);
        }
    }
}