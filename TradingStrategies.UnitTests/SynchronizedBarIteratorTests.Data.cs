using System;
using WealthLab;

namespace TradingStrategies.UnitTests
{
    public partial class SynchronizedBarIteratorTests
    {
        public static IEnumerable<object[]> GetBarDates(int seriesCount)
        {
            var barsCount = 100;

            var linear = Enumerable
                .Range(0, seriesCount)
                .Select(x => Enumerable
                    .Range(0, barsCount)
                    .Select(y => DateTime.Now + TimeSpan.FromDays(x) + TimeSpan.FromSeconds(y))
                    .ToArray())
                .ToArray();

            yield return [linear];

            var parallel = Enumerable
                .Range(0, seriesCount)
                .Select(x => Enumerable
                    .Range(0, barsCount)
                    .Select(y => DateTime.Now + TimeSpan.FromDays(y))
                    .ToArray())
                .ToArray();

            yield return [parallel];

            var overlaped = Enumerable
                .Range(0, seriesCount)
                .Select(x => Enumerable
                    .Range(0, barsCount)
                    .Select(y => DateTime.Now + TimeSpan.FromDays(x) + TimeSpan.FromHours(y))
                    .ToArray())
                .ToArray();

            yield return [overlaped];

            var reverseOverlaped = Enumerable
                .Range(0, seriesCount)
                .OrderDescending()
                .Select(x => Enumerable
                    .Range(0, barsCount)
                    .Select(y => DateTime.Now + TimeSpan.FromDays(x) + TimeSpan.FromHours(y))
                    .OrderDescending()
                    .ToArray())
                .ToArray();

            yield return [reverseOverlaped];
        }

        public static IEnumerable<object[]> GetRandomBarDates(int testsCount)
        {
            for (int i = 0; i < testsCount; i++)
            {
                var seriesCount = Random.Shared.Next(1, 10);
                var barsCount = () => Random.Shared.Next(10, 100);
                var hoursOffset = () => Random.Shared.Next(1, 1000);

                var random = Enumerable
                    .Range(0, seriesCount)
                    .Select(x => Enumerable
                        .Range(0, barsCount())
                        .Select(y => DateTime.Now + TimeSpan.FromHours(hoursOffset()))
                        .ToArray())
                    .ToArray();

                yield return [random];
            }
        }
    }
}